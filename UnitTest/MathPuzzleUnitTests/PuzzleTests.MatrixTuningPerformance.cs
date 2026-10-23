using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixTuningPerformance()
    {
        Console.WriteLine($"Tuning runtime={Environment.Version}; logical CPUs={Environment.ProcessorCount}; SSE2={Sse2.IsSupported}; AVX2={Avx2.IsSupported}; AVX512F={Avx512F.IsSupported}");
        foreach (int n in new[] { 256, 512 })
        {
            var a = new IntegerMatrix(n, n); var b = new IntegerMatrix(n, n);
            for (int i = 0; i < a.Data.Length; i++) { a.Data[i] = i % 31 - 15; b.Data[i] = (i * 7 + i / n) % 37 - 18; }
            foreach (var cap in new[] { MatrixBackend.Scalar, MatrixBackend.Sse2, MatrixBackend.Avx2, MatrixBackend.Avx512 })
            {
                if (cap == MatrixBackend.Sse2 && !Sse2.IsSupported || cap == MatrixBackend.Avx2 && !Avx2.IsSupported || cap == MatrixBackend.Avx512 && !Avx512F.IsSupported) continue;
                foreach (int block in new[] { 64, 128, 256 })
                foreach (var kind in cap == MatrixBackend.Scalar ? new[] { MatrixMultiplyKernel.SingleOutput }
                    : new[] { MatrixMultiplyKernel.SingleOutput, MatrixMultiplyKernel.PairedOutputs })
                    Sample(a, b, cap, block, kind, true, 1);
            }
            if (n == 512)
            {
                Sample(a, b, MatrixBackend.Auto, 128, MatrixMultiplyKernel.SingleOutput, false, 1);
                Sample(a, b, MatrixBackend.Auto, 128, MatrixMultiplyKernel.PairedOutputs, false, 1);
            }
        }
        var left = new IntegerMatrix(1024, 1024); var right = new IntegerMatrix(1024, 1024);
        for (int i = 0; i < left.Data.Length; i++) { left.Data[i] = i % 29 - 14; right.Data[i] = (i * 5 + i / 1024) % 41 - 20; }
        foreach (int block in new[] { 64, 128, 256 })
        foreach (var kind in new[] { MatrixMultiplyKernel.SingleOutput, MatrixMultiplyKernel.PairedOutputs })
        foreach (int workers in new[] { 1, 4, 0 })
            Sample(left, right, MatrixBackend.Auto, block, kind, true, workers);

        static void Sample(IntegerMatrix a, IntegerMatrix b, MatrixBackend cap, int block, MatrixMultiplyKernel kind, bool reuse, int workers)
        {
            var samples = new double[3];
            string details = "";
            for (int sample = -1; sample < samples.Length; sample++)
            {
                var timer = Stopwatch.StartNew();
                using var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, cap,
                    maximumDegreeOfParallelism: workers, executionOptions: new() { BlockSize = block, MultiplyKernel = kind, ReusePackedPanels = reuse });
                timer.Stop();
                TestAssert.That(!result.UsedUniformShortcut, "Benchmark must exercise dot products.");
                foreach (var cell in new[] { (0, 0), (a.Rows / 2, b.Columns / 2), (a.Rows - 1, b.Columns - 1) })
                {
                    BigInteger expected = 0;
                    for (int k = 0; k < a.Columns; k++) expected += (BigInteger)a.Data[cell.Item1 * a.Columns + k] * b.Data[k * b.Columns + cell.Item2];
                    TestAssert.That(result.GetValue(cell.Item1, cell.Item2) == expected, "Timed result must match reference samples.");
                }
                if (sample >= 0) samples[sample] = timer.Elapsed.TotalMilliseconds;
                var x = result.Execution;
                details = $"backend={result.Backend}; workers={result.WorkerCount}/{result.WorkerLimit}; pack_ms={x.PackingTime.TotalMilliseconds:F3}; kernel_ms={x.KernelTime.TotalMilliseconds:F3}; convert_ms={x.ResultConversionTime.TotalMilliseconds:F3}; encode_ms={x.Storage.EncodingTime.TotalMilliseconds:F3}; wait_ms={x.Storage.LockWaitTime.TotalMilliseconds:F3}; write_ms={x.Storage.WriteTime.TotalMilliseconds:F3}; packed={x.PackedAElements + x.PackedBElements}; pairs={x.PairVectorBatches}; reserve_MiB={result.WorkspaceLimitBytes / 1048576d:F2}";
            }
            Array.Sort(samples);
            Console.WriteLine($"TUNING n={a.Rows}; cap={cap}; block={block}; kernel={kind}; reuse={reuse}; requested={workers}; median_ms={samples[1]:F3}; {details}");
        }
    }
}
