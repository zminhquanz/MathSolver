using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Numerics;
using System.Runtime.ExceptionServices;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    // Catches dense Data access in either input panel, wrong B transposition,
    // narrow arithmetic, and a paged small ADD/SUB entering the dense-only path.
    public static void CheckMatrixPagedEngineExact()
    {
        string directory = Path.Combine(Path.GetTempPath(), "matrix-paged-engine-" + Guid.NewGuid().ToString("N"));
        try
        {
            long[] left = Enumerable.Range(0, 35 * 37).Select(i => (i % 7) switch
                { 0 => long.MinValue, 1 => long.MaxValue, _ => i - 670L }).ToArray();
            long[] right = Enumerable.Range(0, 37 * 29).Select(i => (i % 9) switch
                { 0 => long.MaxValue, 1 => long.MinValue, _ => 453L - i }).ToArray();
            using var pagedA = BuildPagedEngineInput(35, 37, left, directory);
            using var pagedB = BuildPagedEngineInput(37, 29, right, directory);
            using var denseA = new IntegerMatrix(35, 37, left.ToArray());
            using var denseB = new IntegerMatrix(37, 29, right.ToArray());
            TestAssert.That(pagedA.DenseData is null && pagedB.DenseData is null && pagedA.IsFileBacked && pagedB.IsFileBacked,
                "The arithmetic fixture must use actual nonuniform file-backed inputs.");
            foreach (int blockSize in new[] { 16, 17, 32 })
            foreach (var cap in new[] { MatrixBackend.Scalar, MatrixBackend.Sse2, MatrixBackend.Avx2, MatrixBackend.Avx512 })
            {
                var options = new MatrixExecutionOptions { BlockSize = blockSize, InMemoryResultLimitBytes = 0,
                    TemporaryDirectory = directory, ResultCacheBytes = 1024 };
                using var actual = MatrixEngine.Calculate(pagedA, pagedB, MatrixOperation.Multiply, cap,
                    maximumDegreeOfParallelism: 4, executionOptions: options);
                using var dense = MatrixEngine.Calculate(denseA, denseB, MatrixOperation.Multiply, cap,
                    maximumDegreeOfParallelism: 4, executionOptions: options);
                TestAssert.That(!actual.UsedUniformShortcut && actual.Rows == 35 && actual.Columns == 29,
                    "A nonuniform file-backed product must retain its rectangular shape and run the exact kernels.");
                for (int r = 0; r < 35; r++)
                for (int c = 0; c < 29; c++)
                {
                    BigInteger expected = 0;
                    for (int k = 0; k < 37; k++) expected += (BigInteger)left[r * 37 + k] * right[k * 29 + c];
                    TestAssert.That(actual.GetValue(r, c) == expected && dense.GetValue(r, c) == expected,
                        $"Paged and dense exact products must match at [{r},{c}], block {blockSize}, cap {cap}.");
                }
                TestAssert.That(actual.WorkerLimit <= Math.Min(4, Environment.ProcessorCount),
                    "Paged panels must obey the caller's existing worker preference.");
                var worked = MatrixSolutionPresentation.ForCell(pagedA, pagedB, actual, 0, 0);
                TestAssert.That(worked.Terms.Count == 4 && worked.RemainingCount == 33
                    && worked.Terms[0].Value == (BigInteger)long.MinValue * long.MaxValue
                    && worked.Answer == actual.GetValue(0, 0),
                    "Worked multiplication steps must read bounded paged input cells exactly.");
            }
            using var pagedElementwise = BuildPagedEngineInput(35, 37, left.Reverse().ToArray(), directory);
            foreach (var operation in new[] { MatrixOperation.Add, MatrixOperation.Subtract })
            foreach (var other in new[] { denseA, pagedElementwise })
            {
                using var actual = MatrixEngine.Calculate(pagedA, other, operation,
                    executionOptions: new() { BlockSize = 17, TemporaryDirectory = directory });
                TestAssert.That(actual.Rows == 35 && actual.Columns == 37 && actual.WideValues is null
                    && actual.WorkspaceLimitBytes > 0, "Paged elementwise input must use a budgeted block plan even for a small result.");
                for (int r = 0; r < 35; r++)
                for (int c = 0; c < 37; c++)
                {
                    BigInteger expected = operation == MatrixOperation.Add
                        ? (BigInteger)left[r * 37 + c] + other.GetValue(r, c)
                        : (BigInteger)left[r * 37 + c] - other.GetValue(r, c);
                    TestAssert.That(actual.GetValue(r, c) == expected, "Mixed and paged ADD/SUB must widen extreme signed inputs exactly.");
                }
                TestAssert.That(MatrixSolutionPresentation.ForCell(pagedA, other, actual, 0, 0).Answer == actual.GetValue(0, 0),
                    "Worked elementwise steps must accept paged input cells.");
            }
            TestAssert.That(pagedA.GetValue(0, 0) == long.MinValue && pagedA.GetValue(0, 1) == long.MaxValue
                && pagedA.CachedBytes <= pagedA.CacheLimitBytes && pagedB.CachedBytes <= pagedB.CacheLimitBytes,
                "Calculation must preserve its inputs and their bounded caches.");
        }
        finally
        {
            TestAssert.That(!Directory.Exists(directory) || !Directory.EnumerateFiles(directory).Any(),
                "Disposing all inputs and results must release their temporary files.");
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    // Catches treating absent metadata proof as nonuniform, rescanning compact
    // constant metadata, or falsely attributing scanned/SIMD work to metadata.
    public static void CheckMatrixPagedEngineUniform()
    {
        string directory = Path.Combine(Path.GetTempPath(), "matrix-paged-uniform-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var left = BuildPagedEngineInput(16, 37, Enumerable.Repeat(long.MinValue, 16 * 37).ToArray(), directory);
            using var right = BuildPagedEngineInput(37, 19, Enumerable.Repeat(long.MinValue, 37 * 19).ToArray(), directory);
            TestAssert.That(left.TryGetUniformValue(out long value) && value == long.MinValue,
                "Uniform storage must supply a metadata proof for this fixture.");
            using var result = MatrixEngine.Calculate(left, right, MatrixOperation.Multiply,
                executionOptions: new() { TemporaryDirectory = directory, BlockSize = 16 });
            TestAssert.That(result.UsedUniformShortcut && result.GetValue(15, 18) == (BigInteger)long.MinValue * long.MinValue * 37
                && result.UniformScan.CellsExamined == 0 && result.UniformScan.VectorBatches == 0
                && result.UniformScan.Backend == MatrixBackend.Scalar && result.UniformScan.WorkerCount == 0,
                "Metadata-only uniform proofs must calculate exactly and report no input comparisons or workers.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory); }
    }

    // Catches cancellation thrown from a paged scan or panel worker, partial
    // result publication/leaks, and source mutation during cooperative stop.
    public static void CheckMatrixPagedEngineCancellation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "matrix-paged-stop-" + Guid.NewGuid().ToString("N"));
        int cancellationExceptions = 0;
        EventHandler<FirstChanceExceptionEventArgs> observer = (_, e) =>
        { if (e.Exception is OperationCanceledException) Interlocked.Increment(ref cancellationExceptions); };
        try
        {
            const int rows = 128, columns = 257;
            long[] values = Enumerable.Range(0, rows * columns).Select(i => i % 17 - 8L).ToArray();
            using var input = BuildPagedEngineInput(rows, columns, values, directory);
            using var right = BuildPagedEngineInput(columns, 65,
                Enumerable.Range(0, columns * 65).Select(i => i % 13 - 6L).ToArray(), directory);
            var baselineFiles = Directory.EnumerateFiles(directory).Order().ToArray();
            AppDomain.CurrentDomain.FirstChanceException += observer;
            foreach (int workers in new[] { 1, 4 })
            foreach (double threshold in new[] { .05, .21, 1d })
            {
                using var stop = new CancellationTokenSource();
                bool reached = false;
                using var attempt = MatrixEngine.TryCalculate(input, right, MatrixOperation.Multiply,
                    cancellationToken: stop.Token, maximumDegreeOfParallelism: workers,
                    executionOptions: new() { BlockSize = 16, InMemoryResultLimitBytes = 0, TemporaryDirectory = directory },
                    progress: new MatrixProgress(p => { if (p >= threshold) { reached = true; stop.Cancel(); } }));
                TestAssert.That(reached && attempt.IsCancelled && attempt.Result is null,
                    "Paged cancellation must reach the requested stage and return a cancelled outcome.");
                TestAssert.That(Directory.EnumerateFiles(directory).Order().SequenceEqual(baselineFiles)
                    && input.GetValue(0, 0) == -8 && input.GetValue(rows - 1, columns - 1) == values[^1],
                    "Canceled paged work must remove its result file while preserving source files and values.");
            }
            TestAssert.That(cancellationExceptions == 0, "Paged TryCalculate must produce zero first-chance cancellation exceptions.");
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= observer;
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private static IntegerMatrix BuildPagedEngineInput(int rows, int columns, long[] values, string directory)
    {
        using var builder = new MatrixInputBuilder(columns, temporaryDirectory: directory, cacheLimitBytes: 1024);
        for (int row = 0; row < rows; row++) builder.AddRow(values.AsSpan(row * columns, columns));
        return builder.Complete();
    }
}
