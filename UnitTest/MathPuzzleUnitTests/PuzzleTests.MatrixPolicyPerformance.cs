using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Diagnostics;

namespace MathSolver.Tests;
internal static partial class PuzzleTests
{
    public static void CheckMatrixPolicyPerformance()
    {
        foreach (int n in new[] { 512, 1024 })
        {
            var a = new IntegerMatrix(n, n); var b = new IntegerMatrix(n, n);
            for (int i = 0; i < a.Data.Length; i++) { a.Data[i] = i % 29 - 14; b.Data[i] = (i * 5 + i / n) % 41 - 20; }
            long first = 0, last = 0;
            for (int k = 0; k < n; k++) { first += a.Data[k] * b.Data[k * n]; last += a.Data[(n - 1) * n + k] * b.Data[k * n + n - 1]; }
            foreach (var setting in new[] {
                (128, false, MatrixMultiplyKernel.SingleOutput), (128, false, MatrixMultiplyKernel.PairedOutputs),
                (64, true, MatrixMultiplyKernel.SingleOutput), (64, true, MatrixMultiplyKernel.PairedOutputs),
                (128, true, MatrixMultiplyKernel.SingleOutput), (128, true, MatrixMultiplyKernel.PairedOutputs),
                (256, true, MatrixMultiplyKernel.SingleOutput), (256, true, MatrixMultiplyKernel.PairedOutputs) })
            {
                var times = new double[7]; string details = "";
                for (int sample = -2; sample < times.Length; sample++)
                {
                    var timer = Stopwatch.StartNew();
                    using var r = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, maximumDegreeOfParallelism: n == 512 ? 1 : 0,
                        executionOptions: new() { BlockSize = setting.Item1, ReusePackedPanels = setting.Item2, MultiplyKernel = setting.Item3 });
                    timer.Stop();
                    TestAssert.That(!r.UsedUniformShortcut && r.GetValue(0, 0) == first && r.GetValue(n - 1, n - 1) == last, "Timed fixed checksum must match reference.");
                    if (sample >= 0) times[sample] = timer.Elapsed.TotalMilliseconds;
                    details = $"{r.Backend}; workers={r.WorkerCount}/{r.WorkerLimit}; packing={r.Execution.PackingTime.TotalMilliseconds:F2}; encode={r.Execution.Storage.EncodingTime.TotalMilliseconds:F2}; wait={r.Execution.Storage.LockWaitTime.TotalMilliseconds:F2}; write={r.Execution.Storage.WriteTime.TotalMilliseconds:F2}; packed={r.Execution.PackedAElements + r.Execution.PackedBElements}";
                }
                Array.Sort(times);
                Console.WriteLine($"POLICY n={n}; block={setting.Item1}; reuse={setting.Item2}; kernel={setting.Item3}; median_ms={times[3]:F3}; {details}");
            }
        }
    }
}
