using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixCancellationOutcome()
    {
        string directory = Path.Combine(Path.GetTempPath(), "matrix-stop-outcome-" + Guid.NewGuid().ToString("N"));
        var disk = new MatrixExecutionOptions { BlockSize = 16, InMemoryResultLimitBytes = 0, TemporaryDirectory = directory };
        var original = new IntegerMatrix(2, 2, [1, 2, 3, 4]);
        using var previous = MatrixEngine.Calculate(original, original, MatrixOperation.Multiply, executionOptions: disk);
        int cancellationExceptions = 0, cases = 0;
        EventHandler<FirstChanceExceptionEventArgs> observer = (_, args) =>
        { if (args.Exception is OperationCanceledException) Interlocked.Increment(ref cancellationExceptions); };
        AppDomain.CurrentDomain.FirstChanceException += observer;
        try
        {
            void AssertStopped(MatrixCalculationAttempt attempt)
            {
                using (attempt)
                    TestAssert.That(attempt.Result is null && attempt.IsCancelled,
                        "Expected cancellation must be returned as a distinct cancelled outcome, without publishing a matrix.");
                TestAssert.That(previous.GetValue(1, 1) == 22 && Directory.EnumerateFiles(directory).Count() == 1
                    && original.Data.SequenceEqual(new long[] { 1, 2, 3, 4 }),
                    "Cancelled work must retain inputs and the previous result, and remove only its own partial file.");
                cases++;
            }
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                // Match the UI scheduling contract: cancellation is data, not a cancelled Task.
                var task = Task.Run(() => MatrixEngine.TryCalculate(original, original, MatrixOperation.Multiply,
                    cancellationToken: cancelled.Token, executionOptions: disk));
                AssertStopped(task.GetAwaiter().GetResult());
                TestAssert.That(task.IsCompletedSuccessfully, "A pre-cancelled UI calculation must complete normally with cancelled status.");
            }

            var nonuniform = new IntegerMatrix(1024, 1024);
            var narrow = new IntegerMatrix(1024, 32);
            for (int i = 0; i < nonuniform.Data.Length; i++) nonuniform.Data[i] = i % 17 - 8;
            for (int i = 0; i < narrow.Data.Length; i++) narrow.Data[i] = i % 13 - 6;
            var constant = new IntegerMatrix(1024, 1024); Array.Fill(constant.Data, 3);
            foreach (int workers in new[] { 1, 4 })
            {
                void CancelAt(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
                    MatrixExecutionOptions options, double threshold)
                {
                    using var cancellation = new CancellationTokenSource();
                    bool reached = false;
                    var attempt = MatrixEngine.TryCalculate(a, b, operation, maximumDegreeOfParallelism: workers,
                        cancellationToken: cancellation.Token, executionOptions: options,
                        progress: new MatrixProgress(p =>
                        {
                            if (p >= threshold) { reached = true; cancellation.Cancel(); }
                        }));
                    TestAssert.That(reached, "Cancellation fixture must reach the intended computation stage.");
                    AssertStopped(attempt);
                }
                CancelAt(nonuniform, narrow, MatrixOperation.Multiply, disk, 0); // Before workspace/files.
                CancelAt(constant, constant, MatrixOperation.Multiply, disk, .025); // Inside input verification.
                CancelAt(nonuniform, narrow, MatrixOperation.Multiply, disk, .21); // Workers writing computed tiles.
                CancelAt(constant, constant, MatrixOperation.Multiply, disk, .3); // Uniform tile shortcut.
                foreach (var operation in new[] { MatrixOperation.Add, MatrixOperation.Subtract })
                {
                    CancelAt(nonuniform, nonuniform, operation, new(), .05); // Dense rows.
                    CancelAt(nonuniform, nonuniform, operation, disk, .21); // Blocked elementwise.
                }
                CancelAt(nonuniform, narrow, MatrixOperation.Multiply, disk, 1); // Before final publication.
            }
            // Unlike a synchronous progress callback, the UI can cancel while
            // another thread is still running a tile's inner multiplication loop.
            var longLeft = new IntegerMatrix(128, 16384);
            var longRight = new IntegerMatrix(16384, 256);
            for (int i = 0; i < longLeft.Data.Length; i++) longLeft.Data[i] = i % 17 - 8;
            for (int i = 0; i < longRight.Data.Length; i++) longRight.Data[i] = i % 13 - 6;
            using (var computationPhase = new ManualResetEventSlim())
            using (var externalStop = new CancellationTokenSource())
            {
                var cancellingThread = new Thread(() =>
                {
                    if (!computationPhase.Wait(TimeSpan.FromSeconds(10))) return;
                    Thread.Sleep(50);
                    externalStop.Cancel();
                }) { IsBackground = true };
                var timer = Stopwatch.StartNew();
                cancellingThread.Start();
                try
                {
                    var interrupted = MatrixEngine.TryCalculate(longLeft, longRight, MatrixOperation.Multiply,
                        MatrixBackend.Scalar, externalStop.Token,
                        new MatrixProgress(p => { if (p >= .2) computationPhase.Set(); }),
                        maximumDegreeOfParallelism: 4, executionOptions: disk with { BlockSize = 128 });
                    TestAssert.That(computationPhase.IsSet && externalStop.IsCancellationRequested,
                        "Concurrent cancellation must reach the post-verification phase of nonuniform multiplication.");
                    AssertStopped(interrupted);
                }
                finally { computationPhase.Set(); cancellingThread.Join(); }
                Console.WriteLine($"Concurrent stop during 128x16384 times 16384x256 multiplication: {timer.Elapsed.TotalMilliseconds:F1} ms, including a 50 ms cancellation delay.");
            }
            TestAssert.That(Volatile.Read(ref cancellationExceptions) == 0,
                "Expected stop/tab/close cancellation must not throw first-chance cancellation exceptions from workers or Parallel.For.");
            TestAssert.That(nonuniform.Data[0] == -8 && nonuniform.Data[^1] == 7 && narrow.Data[0] == -6,
                "Cancellation must never modify either input.");

            var failure = new InvalidOperationException("Deliberate progress failure");
            TestAssert.That(ReferenceEquals(TestAssert.Throws<InvalidOperationException>(() => MatrixEngine.TryCalculate(original, original,
                MatrixOperation.Multiply, progress: new MatrixProgress(_ => throw failure)),
                "Unrelated computation errors must still propagate."), failure), "Do not turn real faults into cancellation.");
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= observer;
            previous.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
        using var legacyCancelled = new CancellationTokenSource(); legacyCancelled.Cancel();
        TestAssert.That(TestAssert.Throws<OperationCanceledException>(() => MatrixEngine.Calculate(original, original,
            MatrixOperation.Multiply, cancellationToken: legacyCancelled.Token),
            "The explicitly throwing calculation API must preserve its legacy cancellation contract.").CancellationToken == legacyCancelled.Token,
            "Legacy cancellation must retain the caller token.");
        using var completed = MatrixEngine.TryCalculate(original, original, MatrixOperation.Multiply);
        TestAssert.That(completed.Result?.GetValue(1, 1) == 22,
            "A fresh calculation must still work after repeatedly stopping workers.");
        Console.WriteLine($"Checked {cases} cancellation outcomes across early scheduling, uniform scan/shortcut, dense rows, blocked multiply/add/subtract and final publication; zero first-chance cancellation exceptions.");
    }
}
