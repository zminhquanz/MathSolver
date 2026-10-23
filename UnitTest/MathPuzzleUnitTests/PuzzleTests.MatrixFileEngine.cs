using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixFileEngine()
    {
        foreach (var operation in new[] { MatrixOperation.Add, MatrixOperation.Subtract, MatrixOperation.Multiply })
        foreach (var backend in new[] { MatrixBackend.Scalar, MatrixBackend.Sse2, MatrixBackend.Avx2, MatrixBackend.Avx512 })
        foreach (int storage in new[] { 0, 1, 2 })
        {
            const int rows = 17, inner = 19;
            int columns = operation == MatrixOperation.Multiply ? 18 : inner;
            using var a = new MatrixDraft(rows, inner, fileBacked: storage != 2);
            using var b = new MatrixDraft(operation == MatrixOperation.Multiply ? inner : rows, columns, fileBacked: storage != 1);
            for (int r = 0; r < a.Rows; r++) for (int c = 0; c < a.Columns; c++)
                a.TrySetCell(r, c, (r == 0 && c == 0 ? long.MinValue : (long)r * 13 - c * 7).ToString());
            for (int r = 0; r < b.Rows; r++) for (int c = 0; c < b.Columns; c++)
                b.TrySetCell(r, c, (r == 0 && c == 0 ? long.MaxValue : (long)c * 5 - r * 3).ToString());
            using var result = MatrixEngine.Calculate(a.Matrix, b.Matrix, operation, backend,
                executionOptions: new() { BlockSize = 16, InMemoryResultLimitBytes = 0, ResultCacheBytes = 2048 });
            for (int r = 0; r < rows; r++) for (int c = 0; c < columns; c++)
            {
                BigInteger expected;
                if (operation == MatrixOperation.Multiply)
                {
                    expected = 0;
                    for (int k = 0; k < inner; k++) expected += (BigInteger)a.Matrix.GetValue(r, k) * b.Matrix.GetValue(k, c);
                }
                else expected = operation == MatrixOperation.Add
                    ? (BigInteger)a.Matrix.GetValue(r, c) + b.Matrix.GetValue(r, c)
                    : (BigInteger)a.Matrix.GetValue(r, c) - b.Matrix.GetValue(r, c);
                TestAssert.That(result.GetValue(r, c) == expected, "Disk panels must be exact including Int64 extremes and partial tiles.");
            }
            TestAssert.That(a.Matrix.GetValue(0, 0) == long.MinValue && b.Matrix.GetValue(0, 0) == long.MaxValue,
                "Panel packing must not mutate file-backed inputs.");
        }
    }

    public static void CheckMatrixFileUniformCalculation()
    {
        using var empty = new MatrixDraft(1024, 1024);
        using var a = empty.WithPastedCells(0, 0, 1024, 1024, MatrixClipboardCodec.Read("3"));
        using var b = empty.WithPastedCells(0, 0, 1024, 1024, MatrixClipboardCodec.Read("-7"));
        using var result = MatrixEngine.Calculate(a.Matrix, b.Matrix, MatrixOperation.Multiply);
        TestAssert.That(result.GetValue(1023, 1023) == -21504 && result.UsedUniformShortcut,
            "A disk-backed uniform multiplication must stay exact and use the uniform result path.");
        TestAssert.That(result.UniformScan.CellsExamined == 2L * 1024 * 1024,
            "Input verification must scan real bounded panels without materializing full input arrays.");
        TestAssert.That(a.ResidentBytes + b.ResidentBytes < 12L * 1024 * 1024,
            "Input caches and metadata must remain bounded after SIMD/parallel scans.");
        using var stop = new CancellationTokenSource();
        using var canceled = MatrixEngine.TryCalculate(a.Matrix, b.Matrix, MatrixOperation.Multiply,
            cancellationToken: stop.Token, progress: new MatrixProgress(_ => stop.Cancel()));
        TestAssert.That(canceled.IsCanceled && canceled.Result is null,
            "Stopping a file-backed calculation must return an ordinary canceled outcome.");
        TestAssert.That(a.Matrix.GetValue(1023, 1023) == 3, "Stopping must retain usable input files.");
    }

    public static void CheckMatrixFileParallelPanels()
    {
        const int size = 1024;
        using var a = new MatrixDraft(size, size); using var b = new MatrixDraft(size, size);
        a.Matrix.InputStore!.Fill(0, a.CellCount, 3); b.Matrix.InputStore!.Fill(0, b.CellCount, 7);
        a.TrySetCell(size - 1, size - 1, long.MaxValue.ToString());
        b.TrySetCell(0, 0, long.MinValue.ToString());
        using var result = MatrixEngine.Calculate(a.Matrix, b.Matrix, MatrixOperation.Add);
        TestAssert.That(!result.UsedUniformShortcut && result.GetValue(0, 0) == (BigInteger)long.MinValue + 3
            && result.GetValue(size - 1, size - 1) == (BigInteger)long.MaxValue + 7 && result.GetValue(512, 512) == 10,
            "Nonuniform disk panels must use exact kernels and preserve overflow beyond Int64.");
        TestAssert.That((Environment.ProcessorCount == 1 || result.WorkerLimit > 1)
            && result.WorkerCount >= 1 && a.ResidentBytes + b.ResidentBytes < 12L * 1024 * 1024,
            "Large nonuniform disk inputs must plan parallel kernels while retaining bounded input caches.");
    }
}
