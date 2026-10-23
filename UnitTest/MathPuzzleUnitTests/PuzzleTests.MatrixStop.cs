using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Runtime.ExceptionServices;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixStopWithoutExceptions()
    {
        var small = new IntegerMatrix(16, 16);
        var large = new IntegerMatrix(1024, 1024);
        var narrow = new IntegerMatrix(1024, 17);
        for (int i = 0; i < small.Data.Length; i++) small.Data[i] = i % 7 - 3;
        for (int i = 0; i < large.Data.Length; i++) large.Data[i] = i % 13 - 6;
        for (int i = 0; i < narrow.Data.Length; i++) narrow.Data[i] = i % 11 - 5;
        foreach (var backend in new[] { MatrixBackend.Scalar, MatrixBackend.Auto })
        {
            AssertMatrixStop(small, small, MatrixOperation.Add, backend, 1, 0, preCanceled: true);
            AssertMatrixStop(small, small, MatrixOperation.Add, backend, 1, .05);
            AssertMatrixStop(large, large, MatrixOperation.Subtract, backend, 4, .05);
            AssertMatrixStop(large, narrow, MatrixOperation.Multiply, backend, 1, .2001);
            AssertMatrixStop(large, narrow, MatrixOperation.Multiply, backend, 4, .2001);
        }
        Array.Fill(large.Data, 3);
        AssertMatrixStop(large, large, MatrixOperation.Multiply, MatrixBackend.Auto, 4, .025);
    }

    public static void CheckMatrixStopStorage()
    {
        string directory = Path.Combine(Path.GetTempPath(), "matrix-stop-" + Guid.NewGuid().ToString("N"));
        var options = new MatrixExecutionOptions { BlockSize = 16, InMemoryResultLimitBytes = 0, TemporaryDirectory = directory };
        try
        {
            using var previous = MatrixEngine.Calculate(new(1, 1, [7]), new(1, 1, [6]), MatrixOperation.Multiply, executionOptions: options);
            var a = new IntegerMatrix(96, 80); var b = new IntegerMatrix(80, 65);
            for (int i = 0; i < a.Data.Length; i++) a.Data[i] = i % 7;
            for (int i = 0; i < b.Data.Length; i++) b.Data[i] = i % 11;
            AssertMatrixStop(a, b, MatrixOperation.Multiply, MatrixBackend.Auto, 4, .2001, options: options);
            AssertMatrixStop(a, a, MatrixOperation.Add, MatrixBackend.Auto, 4, .2001, options: options);
            Array.Fill(a.Data, 2); Array.Fill(b.Data, 3);
            AssertMatrixStop(a, b, MatrixOperation.Multiply, MatrixBackend.Auto, 4, .2001, options: options);
            AssertMatrixStop(a, b, MatrixOperation.Multiply, MatrixBackend.Auto, 4, 1, options: options);
            var large = new IntegerMatrix(256, 4096);
            var wide = new IntegerMatrix(4096, 512);
            for (int i = 0; i < large.Data.Length; i++) large.Data[i] = i % 7 - 3;
            for (int i = 0; i < wide.Data.Length; i++) wide.Data[i] = i % 11 - 5;
            foreach (var backend in new[] { MatrixBackend.Scalar, MatrixBackend.Auto })
            {
                // Unlike the small fixtures, this reaches the parallel threshold.
                // A timer issues stop after verification returns, while kernels run,
                // independently of the next tile/progress callback.
                AssertMatrixStop(large, wide, MatrixOperation.Multiply, backend, 4, .2,
                    options: options with { BlockSize = 128 }, asynchronousStop: true);
                // Also stop after workers have started publishing completed tiles.
                AssertMatrixStop(large, wide, MatrixOperation.Multiply, backend, 4, .2001, options: options);
            }
            TestAssert.That(previous.GetValue(0, 0) == 42 && Directory.EnumerateFiles(directory).Count() == 1,
                "Stopping must retain the prior published result and delete only the unfinished result file.");
            using var fresh = MatrixEngine.TryCalculate(new(2, 2, [1, 2, 3, 4]), new(2, 2, [5, 6, 7, 8]),
                MatrixOperation.Multiply, executionOptions: options);
            TestAssert.That(fresh.Result is { } result && result.GetValue(0, 0) == 19 && result.GetValue(1, 1) == 50,
                "A new computation must succeed after stopping without reusing partial output.");
        }
        finally
        {
            TestAssert.That(!Directory.Exists(directory) || !Directory.EnumerateFiles(directory).Any(), "Every result handle must be closed before cleanup.");
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private static void AssertMatrixStop(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation, MatrixBackend backend,
        int workers, double stopAt, bool preCanceled = false, MatrixExecutionOptions? options = null, bool asynchronousStop = false)
    {
        using var cancel = new CancellationTokenSource();
        if (preCanceled) cancel.Cancel();
        int firstChanceExceptions = 0, stopScheduled = 0;
        void OnException(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is OperationCanceledException) Interlocked.Increment(ref firstChanceExceptions);
        }
        Exception? failure = null;
        AppDomain.CurrentDomain.FirstChanceException += OnException;
        try
        {
            using var attempt = MatrixEngine.TryCalculate(a, b, operation, backend, cancel.Token,
                new MatrixProgress(p =>
                {
                    if (p < stopAt || Interlocked.Exchange(ref stopScheduled, 1) != 0) return;
                    if (asynchronousStop) cancel.CancelAfter(1);
                    else cancel.Cancel();
                }), workers, options);
            TestAssert.That(cancel.IsCancellationRequested && attempt.IsCanceled && attempt.Result is null,
                "A canceled UI calculation must return without publishing a partial result.");
        }
        catch (Exception error) { failure = error; }
        finally { AppDomain.CurrentDomain.FirstChanceException -= OnException; }
        TestAssert.That(failure is null && firstChanceExceptions == 0,
            $"Normal stop must never throw, including first-chance debugger exceptions; operation={operation}, backend={backend}, workers={workers}, stopAt={stopAt}, thrown={firstChanceExceptions}, error={failure?.GetType().Name}.");
    }
}
