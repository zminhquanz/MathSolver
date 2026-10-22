using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixUniformScanExact()
    {
        foreach (int length in new[] { 1, 2, 7, 8, 9, 65541 })
        foreach (long value in new[] { long.MinValue, 1L << 32, -3L })
        foreach (var cap in new[] { MatrixBackend.Scalar, MatrixBackend.Sse2, MatrixBackend.Avx2, MatrixBackend.Avx512 })
        {
            var values = new long[length];
            Array.Fill(values, value);
            var scan = MatrixEngine.ScanUniform(values, cap, 1);
            var stats = scan.Statistics;
            TestAssert.That(scan.IsUniform && scan.Value == value && stats.CellsExamined == length
                && stats.WorkerCount == 1 && stats.WorkerLimit == 1, "Uniform verification must compare every cell exactly once.");
            int lanes = stats.Backend == MatrixBackend.Sse2 ? 2 : stats.Backend == MatrixBackend.Avx2 ? 4 : 8;
            TestAssert.That(stats.Backend == MatrixBackend.Scalar ? stats.VectorBatches == 0
                : stats.VectorBatches == length / lanes, "Only executed full vectors belong in scan telemetry.");
            TestAssert.That(cap != MatrixBackend.Scalar || stats.Backend == MatrixBackend.Scalar,
                "Disabling SIMD must disable vector comparisons too.");
            foreach (int position in new[] { 0, 1, length - 1, Math.Min(65535, length - 1) }.Distinct())
            {
                if (position >= length || length == 1) continue;
                values[position] = value + 1;
                TestAssert.That(!MatrixEngine.ScanUniform(values, cap, 1).IsUniform,
                    "A differing value in either half of an Int64, a vector, or a scalar tail must be detected.");
                values[position] = value;
            }
            TestAssert.That(values.All(v => v == value), "Verification must not mutate inputs.");
            if (length > 1)
            {
                values[^1] = value ^ (1L << 32);
                TestAssert.That(!MatrixEngine.ScanUniform(values, cap, 1).IsUniform, "A high 32-bit-only difference must also be detected by SSE2.");
            }
        }
        TestAssert.Throws<ArgumentException>(() => MatrixEngine.ScanUniform([], MatrixBackend.Auto, 1), "Empty scans are invalid.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MatrixEngine.ScanUniform([1], MatrixBackend.Auto, 0), "Worker limit must be positive.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        TestAssert.Throws<OperationCanceledException>(() => MatrixEngine.ScanUniform([1], MatrixBackend.Auto, 1, canceled.Token), "Pre-canceled scans must stop.");
    }

    public static void CheckMatrixUniformScanParallel()
    {
        var values = new long[1024 * 1024 + 3]; Array.Fill(values, 3);
        foreach (var cap in new[] { MatrixBackend.Scalar, MatrixBackend.Auto })
        {
            var scan = MatrixEngine.ScanUniform(values, cap, 4);
            TestAssert.That(scan.IsUniform && scan.Statistics.CellsExamined == values.Length
                && scan.Statistics.WorkerLimit == Math.Min(4, Environment.ProcessorCount)
                && scan.Statistics.WorkerCount >= 1 && scan.Statistics.WorkerCount <= scan.Statistics.WorkerLimit,
                "Large verification must use a bounded plan and report observed concurrency, not configured workers as actual workers.");
            TestAssert.That(cap != MatrixBackend.Scalar || scan.Statistics.VectorBatches == 0, "Scalar verification can still use multiple workers.");
        }
        values[^1] = 4;
        var different = MatrixEngine.ScanUniform(values, MatrixBackend.Auto, 4);
        TestAssert.That(!different.IsUniform && different.Statistics.CellsExamined <= values.Length, "Parallel tail mismatch must stop verification.");
        values[^1] = 3;
        using var cancel = new CancellationTokenSource();
        TestAssert.Throws<OperationCanceledException>(() => MatrixEngine.ScanUniform(values, MatrixBackend.Auto, 4,
            cancel.Token, new MatrixProgress(p => { if (p >= .1) cancel.Cancel(); })), "Cancellation during verification must prevent a result.");
        var small = MatrixEngine.ScanUniform(new long[1024 * 1024 - 1], MatrixBackend.Auto, 4);
        TestAssert.That(small.Statistics.WorkerLimit == 1, "Below the million-cell threshold verification stays single-threaded.");
    }

    public static void CheckMatrixUniformScanIntegration()
    {
        var a = new IntegerMatrix(1024, 1024); var b = new IntegerMatrix(1024, 1024);
        Array.Fill(a.Data, 3); Array.Fill(b.Data, 3);
        foreach (var cap in new[] { MatrixBackend.Scalar, MatrixBackend.Auto })
        {
            using var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, cap, maximumDegreeOfParallelism: 4);
            TestAssert.That(result.UsedUniformShortcut && result.GetValue(1023, 1023) == 9216
                && result.Backend == MatrixBackend.Scalar && result.VectorBatches == 0 && result.WorkerCount == 1 && result.WorkerLimit == 1,
                "Direct constant arithmetic must remain exact and separate from verification telemetry.");
            TestAssert.That(result.UniformScan.CellsExamined == 2L * a.Data.Length
                && result.UniformScan.WorkerLimit == Math.Min(4, Environment.ProcessorCount), "Both inputs must be verified using the bounded parallel plan.");
            TestAssert.That(cap == MatrixBackend.Scalar ? result.UniformScan.VectorBatches == 0
                : !Sse2.IsSupported || result.UniformScan.VectorBatches > 0, "The selected SIMD preference must reach the real comparison kernels.");
        }
        Array.Clear(a.Data); b.Data[^1] = 8;
        using (var zero = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply))
            TestAssert.That(zero.UsedUniformShortcut && zero.GetValue(0, 0) == 0 && zero.UniformScan.CellsExamined == a.Data.Length,
                "A verified all-zero operand can skip the other input safely.");
        var left = new IntegerMatrix(16, 17); var right = new IntegerMatrix(17, 16);
        Array.Fill(left.Data, 3); Array.Fill(right.Data, 3); left.Data[^1] = 4;
        var progress = new List<double>();
        using var fallback = MatrixEngine.Calculate(left, right, MatrixOperation.Multiply,
            progress: new MatrixProgress(p => progress.Add(p)));
        TestAssert.That(!fallback.UsedUniformShortcut && fallback.GetValue(0, 0) == 153 && fallback.GetValue(15, 15) == 156
            && fallback.UniformScan.CellsExamined > 0, "Nonuniform input must fall back to the exact matrix kernels with verification telemetry retained.");
        TestAssert.That(progress[0] == 0 && progress[^1] == 1 && progress.Zip(progress.Skip(1)).All(p => p.First <= p.Second),
            "Progress must not move backwards when verification falls back to multiplication.");
        var narrow = new IntegerMatrix(1, 16); var column = new IntegerMatrix(16, 1);
        using var small = MatrixEngine.Calculate(narrow, column, MatrixOperation.Multiply);
        TestAssert.That(small.UniformScan.Backend == MatrixBackend.Scalar && small.UniformScan.VectorBatches == 0,
            "The existing matrix dimension gate must also apply to verification.");
    }

    public static void CheckMatrixUniformScanBenchmark()
    {
        var values = new long[8 * 1024 * 1024]; Array.Fill(values, 3);
        foreach (var mode in new[] { (MatrixBackend.Scalar, 1), (MatrixBackend.Auto, 1), (MatrixBackend.Auto, Math.Min(16, Environment.ProcessorCount)) })
        {
            MatrixEngine.ScanUniform(values, mode.Item1, mode.Item2);
            double[] samples = new double[5];
            MatrixUniformScanStatistics stats = default;
            for (int i = 0; i < samples.Length; i++)
            {
                var timer = Stopwatch.StartNew();
                var scan = MatrixEngine.ScanUniform(values, mode.Item1, mode.Item2);
                timer.Stop(); samples[i] = timer.Elapsed.TotalMilliseconds; stats = scan.Statistics;
                TestAssert.That(scan.IsUniform && stats.CellsExamined == values.Length, "Every benchmark mode must verify the same full input.");
            }
            Array.Sort(samples);
            Console.WriteLine($"Uniform scan 64 MiB: cap={mode.Item1}, actual={stats.Backend}, workers={stats.WorkerCount}/{stats.WorkerLimit}, vectors={stats.VectorBatches:N0}, median={samples[2]:F3} ms.");
        }
    }
}
