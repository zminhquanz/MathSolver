using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Diagnostics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixInputMemoryEvidence()
    {
        const int dimension = 16384;
        var scalar = MatrixClipboardCodec.Read("2");
        long before = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        using var emptyA = new MatrixDraft(dimension, dimension);
        using var emptyB = new MatrixDraft(dimension, dimension);
        using var aFill = emptyA.TryWithPastedCells(0, 0, dimension, dimension, scalar);
        using var bFill = emptyB.TryWithPastedCells(0, 0, dimension, dimension, scalar);
        var a = aFill.Draft!; var b = bFill.Draft!;
        timer.Stop();
        long fillAllocation = GC.GetAllocatedBytesForCurrentThread() - before;
        TestAssert.That(a.IsComplete && b.IsComplete && a.Matrix.DenseData is null && b.Matrix.DenseData is null
            && a.GetCellText(dimension - 1, dimension - 1) == "2" && b.GetCellText(0, 0) == "2",
            "Both full desktop benchmark matrices must retain exact constant values without dense arrays.");
        TestAssert.That(fillAllocation < 1024 * 1024 && a.Matrix.StoredBytes == 0 && b.Matrix.StoredBytes == 0,
            $"Creating and filling constant A+B must stay under 1 MiB without input files; allocated {fillAllocation} bytes.");
        Console.WriteLine($"16384-square constant A+B: {fillAllocation:N0} managed bytes allocated, {timer.Elapsed.TotalMilliseconds:F2} ms, input files {a.Matrix.StoredBytes + b.Matrix.StoredBytes} bytes.");

        long editBefore = GC.GetAllocatedBytesForCurrentThread();
        using var edited = a.WithPastedCells(dimension - 1, dimension - 1, MatrixClipboardCodec.Read("3"));
        long editAllocation = GC.GetAllocatedBytesForCurrentThread() - editBefore;
        TestAssert.That(editAllocation < 1024 * 1024 && edited.GetCellText(dimension - 1, dimension - 1) == "3"
            && a.GetCellText(dimension - 1, dimension - 1) == "2" && edited.Matrix.StoredBytes < 128 * 1024,
            "One edited corner must copy one bounded block and retain its constant snapshot.");
        Console.WriteLine($"One corner edit: {editAllocation:N0} managed bytes allocated, {edited.Matrix.StoredBytes:N0} input file bytes; cache {edited.Matrix.CachedBytes:N0}/{edited.Matrix.CacheLimitBytes:N0}.");

        timer.Restart();
        using var result = MatrixEngine.Calculate(a.Matrix, b.Matrix, MatrixOperation.Multiply,
            MatrixBackend.Scalar, maximumDegreeOfParallelism: 1);
        timer.Stop();
        TestAssert.That(result.UsedUniformShortcut && result.GetValue(dimension - 1, dimension - 1) == 65536
            && result.UniformScan.CellsExamined == 0 && result.VectorBatches == 0,
            "Metadata-proven constants must use the exact shortcut without scanning all cells or claiming SIMD arithmetic.");
        Console.WriteLine($"Constant-only product shortcut: {timer.Elapsed.TotalMilliseconds:F2} ms; this is not an arbitrary-matrix speed benchmark.");
    }
}
