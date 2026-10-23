using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Collections.Concurrent;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixGroupedPacking()
    {
        var a = new IntegerMatrix(33, 67); var b = new IntegerMatrix(67, 35);
        for (int i = 0; i < a.Data.Length; i++) a.Data[i] = i % 7 == 0 ? long.MinValue : i % 11 - 5;
        for (int i = 0; i < b.Data.Length; i++) b.Data[i] = i % 9 == 0 ? long.MaxValue : 7 - i % 13;
        foreach (var backend in new[] { MatrixBackend.Scalar, MatrixBackend.Sse2, MatrixBackend.Avx2, MatrixBackend.Avx512 })
        foreach (var kernel in new[] { MatrixMultiplyKernel.SingleOutput, MatrixMultiplyKernel.PairedOutputs })
        foreach (bool reuse in new[] { false, true })
        {
            using var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, backend, executionOptions:
                new() { BlockSize = 16, MultiplyKernel = kernel, ReusePackedPanels = reuse });
            for (int r = 0; r < a.Rows; r++)
            for (int c = 0; c < b.Columns; c++)
            {
                BigInteger expected = 0;
                for (int k = 0; k < a.Columns; k++) expected += (BigInteger)a.Data[r * a.Columns + k] * b.Data[k * b.Columns + c];
                TestAssert.That(result.GetValue(r, c) == expected, "Grouped/paired odd edges and depth tails must match full-range BigInteger reference.");
            }
            TestAssert.That(result.Execution.PackedAElements == (reuse ? 4422 : 6633)
                && result.Execution.PackedBElements == (reuse ? 4690 : 7035), "Grouping must eliminate repeated A/B packing with exact element accounting.");
            TestAssert.That(result.Execution.Storage.BlocksWritten == 9 && result.Execution.PackingTime > TimeSpan.Zero
                && result.Execution.KernelTime > TimeSpan.Zero, "Execution profiling must describe actual packing/kernel/store work.");
            TestAssert.That(result.Execution.PairVectorBatches == 0 || result.Backend != MatrixBackend.Scalar && kernel == MatrixMultiplyKernel.PairedOutputs,
                "Paired-vector telemetry must reflect the kernel actually selected.");
        }
    }

    public static void CheckMatrixDetailedProgress()
    {
        var a = new IntegerMatrix(96, 97); var b = new IntegerMatrix(97, 65);
        for (int i = 0; i < a.Data.Length; i++) a.Data[i] = i % 11 - 5;
        for (int i = 0; i < b.Data.Length; i++) b.Data[i] = 3 - i % 7;
        var events = new ConcurrentQueue<MatrixCalculationProgress>();
        using (var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply,
            calculationProgress: new MatrixDetails(p => events.Enqueue(p))))
        {
            var values = events.ToArray();
            TestAssert.That(values[0] == new MatrixCalculationProgress(0, MatrixCalculationStage.Preparing)
                && values[^1] == new MatrixCalculationProgress(1, MatrixCalculationStage.Completed), "Detailed progress must start preparing and finish only with a complete result.");
            TestAssert.That(values.All(p => p.Fraction is >= 0 and <= 1)
                && values.Zip(values.Skip(1)).All(p => p.First.Fraction <= p.Second.Fraction), "Parallel stage reports must keep global percentage monotonic.");
            TestAssert.That(values.Any(p => p.Stage == MatrixCalculationStage.CheckingInputs)
                && values.Any(p => p.Stage == MatrixCalculationStage.Finalizing), "Progress must distinguish checking input from finalizing result storage.");
        }
        foreach (var stage in new[] { MatrixCalculationStage.Preparing, MatrixCalculationStage.CheckingInputs,
            MatrixCalculationStage.Packing, MatrixCalculationStage.Finalizing })
        {
            events.Clear();
            using var stop = new CancellationTokenSource();
            using var attempt = MatrixEngine.TryCalculate(a, b, MatrixOperation.Multiply, cancellationToken: stop.Token,
                calculationProgress: new MatrixDetails(p => { events.Enqueue(p); if (p.Stage == stage) stop.Cancel(); }));
            TestAssert.That(attempt.IsCanceled && attempt.Result is null && events.All(p => p.Stage != MatrixCalculationStage.Completed && p.Fraction < 1),
                "Stopping in any reported stage must not throw, publish partial data, or display completed100%.");
        }
    }

    public static void CheckMatrixParallelGroupedTuning()
    {
        var a = new IntegerMatrix(1024, 1025); var b = new IntegerMatrix(1025, 135);
        for (int i = 0; i < a.Data.Length; i++) a.Data[i] = i % 13 == 0 ? long.MinValue : i % 17 - 8;
        for (int i = 0; i < b.Data.Length; i++) b.Data[i] = i % 11 == 0 ? long.MaxValue : 4 - i % 9;
        var reports = new ConcurrentQueue<MatrixCalculationProgress>();
        using var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, maximumDegreeOfParallelism: 4,
            executionOptions: new() { BlockSize = 64, MultiplyKernel = MatrixMultiplyKernel.PairedOutputs,
                InMemoryResultLimitBytes = 0, ResultCacheBytes = 64 * 1024, WorkspaceBudgetBytes = 4 * 1024 * 1024 },
            calculationProgress: new MatrixDetails(p => reports.Enqueue(p)));
        TestAssert.That(result.WorkerLimit >= Math.Min(2, Environment.ProcessorCount)
            && result.WorkerLimit <= Math.Min(4, Environment.ProcessorCount) && result.WorkerCount <= result.WorkerLimit
            && result.WorkspaceLimitBytes <= 4 * 1024 * 1024, "Grouped workers must respect the shared allocation budget.");
        foreach (int r in new[] { 0, 127, 128, 511, 1023 })
        foreach (int c in new[] { 0, 63, 64, 127, 128, 134 })
        {
            BigInteger expected = 0;
            for (int k = 0; k < a.Columns; k++) expected += (BigInteger)a.Data[r * a.Columns + k] * b.Data[k * b.Columns + c];
            TestAssert.That(result.GetValue(r, c) == expected, "Parallel groups, tails and wide storage must remain exact.");
        }
        var events = reports.ToArray();
        TestAssert.That(events.Zip(events.Skip(1)).All(p => p.First.Fraction <= p.Second.Fraction)
            && events[^1].Stage == MatrixCalculationStage.Completed, "Parallel reporting must serialize monotonic updates.");
        var plan = MatrixWorkPlan.Create(512, 512, 512, MatrixOperation.Multiply, 1, new(), 128 * 1024 * 1024,
            MatrixBackend.Avx512);
        TestAssert.That(plan.BlockSize == 256, "Wide single-worker backend must use the measured larger panel policy.");
        var parallelPlan = MatrixWorkPlan.Create(1024, 1024, 1024, MatrixOperation.Multiply, 24, new(), 128 * 1024 * 1024,
            MatrixBackend.Avx512);
        TestAssert.That(parallelPlan.BlockSize == 64 && parallelPlan.WorkspaceBytes <= 64 * 1024 * 1024,
            "Parallel automatic policy must retain small bounded groups.");
    }

    public static void CheckMatrixProgressObserverFailureCleanup()
    {
        string directory = Path.Combine(Path.GetTempPath(), "matrix-observer-" + Guid.NewGuid().ToString("N"));
        try
        {
            TestAssert.Throws<InvalidOperationException>(() => MatrixEngine.TryCalculate(new(16, 16), new(16, 16),
                MatrixOperation.Multiply, executionOptions: new() { InMemoryResultLimitBytes = 0, TemporaryDirectory = directory },
                calculationProgress: new MatrixDetails(p => { if (p.Stage == MatrixCalculationStage.Completed) throw new InvalidOperationException("observer"); })),
                "A synchronous completion observer failure must propagate after owned storage is released.");
            TestAssert.That(!Directory.EnumerateFiles(directory).Any(), "An observer failure must not leak the completed temporary file.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory); }
    }

    private sealed class MatrixDetails(Action<MatrixCalculationProgress> action) : IProgress<MatrixCalculationProgress>
    { public void Report(MatrixCalculationProgress value) => action(value); }
}
