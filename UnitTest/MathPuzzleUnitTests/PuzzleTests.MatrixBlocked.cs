using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixBlockedExact()
    {
        var a = new IntegerMatrix(35, 37);
        var b = new IntegerMatrix(37, 29);
        for (int i = 0; i < a.Data.Length; i++)
            a.Data[i] = (i % 7) switch { 0 => long.MinValue, 1 => long.MaxValue, _ => i - 670L };
        for (int i = 0; i < b.Data.Length; i++)
            b.Data[i] = (i % 9) switch { 0 => long.MaxValue, 1 => long.MinValue, _ => 453L - i };
        string directory = Path.Combine(Path.GetTempPath(), "matrix-block-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (int size in new[] { 16, 17, 32 })
            foreach (var backend in new[] { MatrixBackend.Scalar, MatrixBackend.Sse2, MatrixBackend.Avx2, MatrixBackend.Avx512 })
            {
                using var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, backend,
                    executionOptions: new() { BlockSize = size, InMemoryResultLimitBytes = 0,
                        ResultCacheBytes = 1024, TemporaryDirectory = directory });
                TestAssert.That(result.IsFileBacked && result.BigValues is null && result.WideValues is null,
                    "A disk result must never materialize the whole matrix in a dense result array.");
                for (int r = 0; r < a.Rows; r++)
                for (int c = 0; c < b.Columns; c++)
                {
                    BigInteger expected = 0;
                    for (int k = 0; k < a.Columns; k++) expected += (BigInteger)a.Data[r * a.Columns + k] * b.Data[k * b.Columns + c];
                    TestAssert.That(result.GetValue(r, c) == expected,
                        $"Blocked signed products differ at [{r},{c}], tile {size}, backend {backend}.");
                }
                TestAssert.That(result.CachedBytes <= result.CacheLimitBytes && result.VectorBatches >= 0,
                    "Random reads must retain the cache limit and truthful vector accounting.");
            }
            var left = new IntegerMatrix(35, 37, a.Data.ToArray());
            var right = new IntegerMatrix(35, 37, a.Data.Reverse().ToArray());
            foreach (var operation in new[] { MatrixOperation.Add, MatrixOperation.Subtract })
            {
                using var result = MatrixEngine.Calculate(left, right, operation, executionOptions:
                    new() { BlockSize = 17, InMemoryResultLimitBytes = 0, TemporaryDirectory = directory });
                for (int r = 0; r < left.Rows; r++)
                for (int c = 0; c < left.Columns; c++)
                {
                    int index = r * left.Columns + c;
                    BigInteger expected = operation == MatrixOperation.Add
                        ? (BigInteger)left.Data[index] + right.Data[index] : (BigInteger)left.Data[index] - right.Data[index];
                    TestAssert.That(result.GetValue(r, c) == expected, "Blocked add/subtract must widen before computing.");
                }
            }
            TestAssert.That(!Directory.EnumerateFiles(directory).Any(), "Disposed computations must remove their own result files.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory); }
    }

    public static void CheckMatrixBlockedBudget()
    {
        var plan = MatrixWorkPlan.Create(16384, 16384, 16384, MatrixOperation.Multiply, 24,
            new(), 3L * 1024 * 1024 * 1024);
        TestAssert.That(plan.Memory.CanAllocate && plan.FileBacked && plan.WorkspaceBytes <= 64L * 1024 * 1024
            && plan.Memory.RequiredBytes < 128L * 1024 * 1024,
            "A maximum-size product must budget bounded workers/cache/metadata instead of an 18 GiB transpose/result allocation.");
        var constrained = MatrixWorkPlan.Create(1024, 1024, 1024, MatrixOperation.Multiply, 8,
            new() { BlockSize = 64, WorkspaceBudgetBytes = 1024 * 1024, ResultCacheBytes = 64 * 1024,
                InMemoryResultLimitBytes = 0 }, 512L * 1024 * 1024);
        TestAssert.That(constrained.Memory.CanAllocate && constrained.WorkerLimit is >= 1 and < 8
            && constrained.BlockSize <= 64
            && constrained.WorkspaceBytes <= 1024 * 1024,
            "Worker count must shrink to respect the total workspace allowance, even when more CPU threads are available.");
        var impossible = MatrixWorkPlan.Create(16384, 16384, 16384, MatrixOperation.Multiply, 24,
            new() { WorkspaceBudgetBytes = 1024 }, 3L * 1024 * 1024 * 1024);
        TestAssert.That(!impossible.Memory.CanAllocate, "An unusably small workspace must be refused before allocating or opening files.");
        string unusedDirectory = Path.Combine(Path.GetTempPath(), "matrix-refused-" + Guid.NewGuid().ToString("N"));
        using var refused = MatrixEngine.TryCalculate(new(1, 1, [2]), new(1, 1, [2]), MatrixOperation.Multiply,
            executionOptions: new() { WorkspaceBudgetBytes = 1, TemporaryDirectory = unusedDirectory });
        TestAssert.That(refused.Result is null && !refused.Memory.CanAllocate && !Directory.Exists(unusedDirectory),
            "The public calculation path must return a resource refusal before creating temporary storage.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MatrixEngine.Calculate(new(1, 1), new(1, 1), MatrixOperation.Multiply,
            executionOptions: new() { BlockSize = 0 }), "Invalid block sizes must fail before execution.");
    }

    public static void CheckMatrixBlockedUniform()
    {
        var a = new IntegerMatrix(16, 16384);
        var b = new IntegerMatrix(16384, 16);
        foreach (long value in new[] { 2L, 1L << 32, long.MinValue })
        {
            Array.Fill(a.Data, value); Array.Fill(b.Data, value);
            using var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply);
            BigInteger expected = (BigInteger)value * value * 16384;
            TestAssert.That(result.UsedUniformShortcut && result.VectorBatches == 0 && result.WorkerCount == 1
                && result.Backend == MatrixBackend.Scalar && result.GetValue(15, 15) == expected,
                "Verified constant matrices must produce an exact compressed result and report only work actually executed.");
            var expectedStorage = value == 2 ? MatrixNumberStorage.Int64
                : value == 1L << 32 ? MatrixNumberStorage.Int128 : MatrixNumberStorage.Signed192;
            TestAssert.That(result.StorageKinds.Single() == expectedStorage && result.StoredBytes <= 24,
                "Constant tile values must use the smallest exact representation without allocating every repeated result.");
        }
        var nearUniform = new IntegerMatrix(16, 16); var right = new IntegerMatrix(16, 16);
        Array.Fill(nearUniform.Data, 2); Array.Fill(right.Data, 2); nearUniform.Data[^1] = 3;
        using var changed = MatrixEngine.Calculate(nearUniform, right, MatrixOperation.Multiply);
        TestAssert.That(!changed.UsedUniformShortcut && changed.GetValue(15, 15) == 66 && changed.GetValue(0, 0) == 64,
            "A single changed cell must prevent an incorrect uniform shortcut.");
        using var zero = MatrixEngine.Calculate(new(16, 16), nearUniform, MatrixOperation.Multiply);
        TestAssert.That(zero.UsedUniformShortcut && zero.GetValue(15, 15).IsZero,
            "A verified zero input must produce zero even when the other input is not uniform.");
    }

    public static void CheckMatrixBlockedCancellation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "matrix-cancel-test-" + Guid.NewGuid().ToString("N"));
        var a = new IntegerMatrix(96, 97); var b = new IntegerMatrix(97, 65);
        for (int i = 0; i < a.Data.Length; i++) a.Data[i] = i % 11 - 5;
        for (int i = 0; i < b.Data.Length; i++) b.Data[i] = 7 - i % 13;
        var options = new MatrixExecutionOptions { BlockSize = 16, InMemoryResultLimitBytes = 0, TemporaryDirectory = directory };
        try
        {
            using var cancelled = new CancellationTokenSource();
            var progress = new List<double>();
            TestAssert.Throws<OperationCanceledException>(() => MatrixEngine.Calculate(a, b, MatrixOperation.Multiply,
                cancellationToken: cancelled.Token, executionOptions: options,
                progress: new MatrixProgress(p => { progress.Add(p); if (p >= .1) cancelled.Cancel(); })),
                "Cancellation after writing tiles must abandon the result without publishing a partial matrix.");
            TestAssert.That(!Directory.Exists(directory) || !Directory.EnumerateFiles(directory).Any(),
                "Cancelled calculations must close and delete the partial file.");
            progress.Clear();
            using var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, executionOptions: options,
                progress: new MatrixProgress(p => progress.Add(p)));
            TestAssert.That(progress[^1] == 1 && result.GetValue(95, 64) != 0, "A fresh calculation must complete after cancellation.");
            TestAssert.That(progress[0] == 0 && progress.Zip(progress.Skip(1)).All(p => p.First <= p.Second),
                "Tile completion progress must stay monotonic.");
            BigInteger previousAnswer = result.GetValue(95, 64);
            var large = new IntegerMatrix(1024, 1024); var narrow = new IntegerMatrix(1024, 17);
            for (int i = 0; i < large.Data.Length; i++) large.Data[i] = i % 17 - 8;
            for (int i = 0; i < narrow.Data.Length; i++) narrow.Data[i] = i % 13 - 6;
            using var parallelCancellation = new CancellationTokenSource();
            TestAssert.Throws<OperationCanceledException>(() => MatrixEngine.Calculate(large, narrow, MatrixOperation.Multiply,
                cancellationToken: parallelCancellation.Token, maximumDegreeOfParallelism: 4, executionOptions: options,
                progress: new MatrixProgress(p => { if (p >= .05) parallelCancellation.Cancel(); })),
                "A cancelled parallel tile computation must join workers and release its partial file.");
            TestAssert.That(result.GetValue(95, 64) == previousAnswer && Directory.EnumerateFiles(directory).Count() == 1,
                "Cancellation must retain the prior published result and delete only the new partial file.");
            result.Dispose(); result.Dispose();
            TestAssert.Throws<ObjectDisposedException>(() => result.GetValue(0, 0), "Disposed result reads must be rejected.");
            TestAssert.That(!Directory.EnumerateFiles(directory).Any(), "Successful result disposal must release the file.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory); }
    }
}
