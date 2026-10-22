using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Diagnostics;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixParallel()
    {
        var a = new IntegerMatrix(1024, 1024);
        var b = new IntegerMatrix(1024, 1024);
        for (int i = 0; i < a.Data.Length; i++)
        {
            a.Data[i] = (i % 5) switch { 0 => long.MinValue, 1 => long.MaxValue, _ => i * 101L - 90000000 };
            b.Data[i] = (i % 7) switch { 0 => long.MaxValue, 1 => long.MinValue, _ => -i * 79L };
        }
        foreach (var operation in new[] { MatrixOperation.Add, MatrixOperation.Subtract })
        {
            var reference = MatrixEngine.Calculate(a, b, operation, MatrixBackend.Scalar, maximumDegreeOfParallelism: 1);
            foreach (var backend in new[] { MatrixBackend.Scalar, MatrixBackend.Sse2, MatrixBackend.Avx2, MatrixBackend.Avx512 })
            {
                var result = MatrixEngine.Calculate(a, b, operation, backend, maximumDegreeOfParallelism: 4);
                TestAssert.That(reference.WideValues!.AsSpan().SequenceEqual(result.WideValues), "Parallel elementwise kernels must match scalar Int128 results exactly.");
                TestAssert.That(result.WorkerCount >= 1 && result.WorkerCount <= result.WorkerLimit && result.WorkerLimit <= 4,
                    "Telemetry must report actual bounded concurrency.");
                TestAssert.That(result.WorkerLimit == Math.Min(4, Environment.ProcessorCount), "A 1024-square matrix must enable the automatic parallel plan.");
                TestAssert.That(result.Backend == MatrixBackend.Scalar || result.VectorBatches == 1024L * 1024 / (result.Backend == MatrixBackend.Sse2 ? 2 : result.Backend == MatrixBackend.Avx2 ? 4 : 8),
                    "Vector batches must aggregate exactly across workers.");
            }
        }
        var small = new IntegerMatrix(1023, 1024);
        TestAssert.That(MatrixEngine.Calculate(small, small, MatrixOperation.Add, maximumDegreeOfParallelism: 4).WorkerLimit == 1,
            "Below 1024-square cell count the existing single-thread behavior must remain.");
        var progress = new List<double>();
        var report = new MatrixProgress(p => progress.Add(p));
        MatrixEngine.Calculate(a, b, MatrixOperation.Add, progress: report, maximumDegreeOfParallelism: 4);
        TestAssert.That(progress[0] == 0 && progress[^1] == 1 && progress.Zip(progress.Skip(1)).All(p => p.First <= p.Second),
            "Parallel progress must stay monotonic and complete.");
        using var cancel = new CancellationTokenSource();
        TestAssert.Throws<OperationCanceledException>(() => MatrixEngine.Calculate(a, b, MatrixOperation.Add,
            cancellationToken: cancel.Token, progress: new MatrixProgress(p => { if (p >= .05) cancel.Cancel(); }), maximumDegreeOfParallelism: 4),
            "Stopping a parallel computation must cancel without publishing a partial result.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MatrixEngine.Calculate(a, b, MatrixOperation.Add, maximumDegreeOfParallelism: -1), "Invalid thread limits must be rejected.");

        var right = new IntegerMatrix(1024, 16);
        for (int i = 0; i < right.Data.Length; i++) right.Data[i] = (i & 1) == 0 ? long.MinValue : long.MaxValue;
        var serialProduct = MatrixEngine.Calculate(a, right, MatrixOperation.Multiply, MatrixBackend.Scalar, maximumDegreeOfParallelism: 1);
        var parallelProduct = MatrixEngine.Calculate(a, right, MatrixOperation.Multiply, maximumDegreeOfParallelism: 4);
        for (int r = 0; r < serialProduct.Rows; r++)
        for (int c = 0; c < serialProduct.Columns; c++)
            TestAssert.That(serialProduct.GetValue(r, c) == parallelProduct.GetValue(r, c),
                "Parallel blocked SIMD dot products must retain every exact result.");
        Console.WriteLine($"Checked exact add/subtract/multiply, SIMD variants, threshold, progress and stop; workers {parallelProduct.WorkerCount}/{parallelProduct.WorkerLimit} on {Environment.ProcessorCount} logical CPUs.");
    }

    public static void CheckMatrixLongDot()
    {
        var a = new IntegerMatrix(16, 16384); var b = new IntegerMatrix(16384, 16);
        for (int r = 0; r < a.Rows; r++) Array.Fill(a.Data, (r & 1) == 0 ? long.MinValue : long.MaxValue, r * a.Columns, a.Columns);
        for (int r = 0; r < b.Rows; r++)
            for (int c = 0; c < b.Columns; c++) b.Data[r * b.Columns + c] = (c & 1) == 0 ? long.MinValue : long.MaxValue;
        foreach (var backend in new[] { MatrixBackend.Scalar, MatrixBackend.Sse2, MatrixBackend.Avx2, MatrixBackend.Avx512 })
        {
            var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, backend, maximumDegreeOfParallelism: 1);
            for (int r = 0; r < result.Rows; r++)
                for (int c = 0; c < result.Columns; c++)
                {
                    BigInteger expected = (BigInteger)((r & 1) == 0 ? long.MinValue : long.MaxValue)
                        * ((c & 1) == 0 ? long.MinValue : long.MaxValue) * 16384;
                    TestAssert.That(result.GetValue(r, c) == expected, "A 16384-term full-range signed dot product must not overflow SIMD limb accumulators.");
                }
        }
        Console.WriteLine("Checked 16x16384 times 16384x16 with maximum-magnitude Int64 products across all available kernels.");
    }

    public static void CheckMatrixParallelBenchmark()
    {
        // Nonuniform inputs exercise the actual multiplication kernels, not the constant shortcut.
        foreach (int size in new[] { 128, 512, 1024 })
        {
            var a = new IntegerMatrix(size, size); var b = new IntegerMatrix(size, size);
            for (int i = 0; i < a.Data.Length; i++)
            { a.Data[i] = i * 17L % 101 - 50; b.Data[i] = i * 29L % 113 - 56; }
            foreach (var mode in new[] { (MatrixBackend.Scalar, 1, "Scalar, one thread"),
                (MatrixBackend.Auto, 1, "SIMD, one thread"), (MatrixBackend.Auto, 0, "SIMD, automatic threads") })
            {
                using (MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, mode.Item1, maximumDegreeOfParallelism: mode.Item2)) { }
                var samples = new double[3];
                string details = "";
                for (int i = 0; i < samples.Length; i++)
                {
                    var timer = Stopwatch.StartNew();
                    using var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, mode.Item1,
                        maximumDegreeOfParallelism: mode.Item2);
                    samples[i] = timer.Elapsed.TotalMilliseconds;
                    TestAssert.That(!result.UsedUniformShortcut, "Performance fixtures must exercise real dot products.");
                    details = $"{result.Backend}; workers {result.WorkerCount}/{result.WorkerLimit}; "
                        + $"{(result.IsFileBacked ? "disk" : "memory")}; result {result.StoredBytes / 1048576d:F2} MiB; "
                        + $"workspace reserve {result.WorkspaceLimitBytes / 1048576d:F2} MiB";
                }
                Array.Sort(samples);
                Console.WriteLine($"Benchmark nonuniform {size}x{size}, {mode.Item3}: {samples[1]:F2} ms; {details}.");
            }
        }
    }

    private sealed class MatrixProgress(Action<double> action) : IProgress<double>
    { public void Report(double value) => action(value); }
}
