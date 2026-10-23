using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    // Explicit filter only: actual maximum block-backed models, not mocked shapes.
    public static void CheckMatrixMaximumCreation()
    {
        const int size = 16384;
        long required = 2 * MatrixDraft.EstimateResidentBytes(size, size);
        var check = MatrixMemoryGuard.CheckAvailable(required);
        var gc = GC.GetGCMemoryInfo();
        Console.WriteLine($"MAX CREATE 64bit={Environment.Is64BitProcess}; runtime={Environment.Version}; required={required}; budget={check.AvailableBytes}; heap={GC.GetTotalMemory(false)}; totalAvailable={gc.TotalAvailableMemoryBytes}; load={gc.MemoryLoadBytes}; threshold={gc.HighMemoryLoadThresholdBytes}");
        var timer = Stopwatch.StartNew();
        using var created = MatrixRecreation.TryCreate(size, size, size, size, 0,
            progress: new MatrixProgress(p => Console.WriteLine($"CREATE progress={p:P0}; elapsed={timer.Elapsed.TotalSeconds:F2}s; heap={GC.GetTotalMemory(false)}")));
        Console.WriteLine($"CREATE outcome A={created.A?.Rows}x{created.A?.Columns}; B={created.B?.Rows}x{created.B?.Columns}; canceled={created.IsCanceled}; budget={created.Memory.AvailableBytes}; elapsed={timer.Elapsed.TotalSeconds:F2}s");
        TestAssert.That(created.A is { Rows: size, Columns: size } && created.B is { Rows: size, Columns: size },
            "The actual 16384-square creation must produce both requested matrices, never the reset placeholder.");
        TestAssert.That(created.A!.CellCount == size * size && created.B!.MissingCount == size * size
            && created.A.Matrix.DenseData is null && created.B.Matrix.DenseData is null && required < 12L * 1024 * 1024,
            "Maximum inputs must have the exact shape with bounded disk/cache memory, not full arrays.");
        created.A.TrySetCell(0, 0, "-9223372036854775808");
        created.A.TrySetCell(size - 1, size - 1, "7");
        created.B!.TrySetCell(size - 1, size - 1, "9");
        TestAssert.That(created.A.GetCellText(size - 1, size - 1) == "7" && created.B.GetCellText(size - 1, size - 1) == "9",
            "The last requested cells must be editable without falling back to a 1x1 model.");
        MatrixDraft? a = null, b = null;
        TestAssert.That(created.TryPublish(ref a, ref b, size, size, size, size)
            && a is { Rows: size, Columns: size } && b is { Rows: size, Columns: size },
            "Publication must install both actual maximum-size input models.");
        created.Dispose();
        using var ownedA = a; using var ownedB = b;
        TestAssert.That(a?.GetCellText(size - 1, size - 1) == "7" && b?.GetCellText(size - 1, size - 1) == "9",
            "Consuming the async outcome must not reset published dimensions or data.");
    }

    public static void CheckMatrixMaximumRecreation()
    {
        const int size = 16384;
        var old = ReleasePopulatedMatrixSession();
        Console.WriteLine($"MAX RECREATE released={old.Bytes}; heapBefore={GC.GetTotalMemory(false)}");
        using var created = MatrixRecreation.TryCreate(size, size, size, size, old.Bytes);
        var memory = GC.GetGCMemoryInfo();
        Console.WriteLine($"RECREATE outcome A={created.A?.Rows}x{created.A?.Columns}; B={created.B?.Rows}x{created.B?.Columns}; required={created.Memory.RequiredBytes}; budget={created.Memory.AvailableBytes}; load={memory.MemoryLoadBytes}; threshold={memory.HighMemoryLoadThresholdBytes}; heap={GC.GetTotalMemory(false)}; committed={memory.TotalCommittedBytes}; heapSize={memory.HeapSizeBytes}; fragmented={memory.FragmentedBytes}; os={MatrixSystemMemory.Read()}");
        MatrixDraft? a = null, b = null;
        TestAssert.That(created.TryPublish(ref a, ref b, size, size, size, size)
            && a is { Rows: size, Columns: size } && b is { Rows: size, Columns: size },
            "Recreating a populated 8192-square session must publish both requested 16384-square inputs.");
        using var ownedA = a; using var ownedB = b;
        TestAssert.That(old.Paths.All(path => !File.Exists(path)), "Old input files must be deleted before new inputs publish.");
        a!.TrySetCell(size - 1, size - 1, "11"); b!.TrySetCell(size - 1, size - 1, "13");
        TestAssert.That(a.GetCellText(size - 1, size - 1) == "11" && b.GetCellText(size - 1, size - 1) == "13",
            "The recreated maximum-size inputs must expose real last cells.");
        Console.WriteLine($"MAX RECREATE published A={a.Rows}x{a.Columns}; B={b.Rows}x{b.Columns}; old files deleted; resident={a.ResidentBytes + b.ResidentBytes}; heap={GC.GetTotalMemory(false)}");
    }

    public static void CheckMatrixMaximumFileCalculation()
    {
        const int size = 16384;
        using var attempt = MatrixRecreation.TryCreate(size, size, size, size, 0, fileBacked: true);
        var a = attempt.A!; var b = attempt.B!;
        a.Matrix.InputStore!.Fill(0, a.CellCount, 3);
        b.Matrix.InputStore!.Fill(0, b.CellCount, 3);
        long heapBefore = GC.GetTotalMemory(false);
        var timer = Stopwatch.StartNew();
        using var result = MatrixEngine.Calculate(a.Matrix, b.Matrix, MatrixOperation.Multiply);
        TestAssert.That(result.GetValue(0, 0) == 147456 && result.GetValue(size - 1, size - 1) == 147456,
            "Full maximum uniform inputs must calculate the exact first/last product with file-backed A/B/C.");
        TestAssert.That(result.IsFileBacked && result.UsedUniformShortcut
            && result.UniformScan.CellsExamined == 2L * size * size
            && a.ResidentBytes + b.ResidentBytes < 12L * 1024 * 1024,
            "Maximum verification must scan bounded panels and retain bounded input caches.");
        Console.WriteLine($"MAX FILE COMPUTE C={result.Rows}x{result.Columns}; value={result.GetValue(0, 0)}; elapsed={timer.Elapsed.TotalSeconds:F3}s; inputResident={a.ResidentBytes + b.ResidentBytes}; heapBefore={heapBefore}; heapAfter={GC.GetTotalMemory(false)}; backend={result.UniformScan.Backend}; workers={result.UniformScan.WorkerCount}/{result.UniformScan.WorkerLimit}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (string[] Paths, long Bytes) ReleasePopulatedMatrixSession()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
        const int size = 8192;
        MatrixDraft? a = new(size, size, fileBacked: true), b = new(size, size, fileBacked: true);
        a.Matrix.InputStore!.Fill(0, a.CellCount, 1); b.Matrix.InputStore!.Fill(0, b.CellCount, 1);
        MatrixCalculation? c = MatrixEngine.Calculate(a.Matrix, b.Matrix, MatrixOperation.Multiply);
        TestAssert.That(c.GetValue(size - 1, size - 1) == size, "Old populated session must have a usable result/cache.");
        for (int block = 0; block < 20; block++)
        {
            int row = block * MatrixInputStore.BlockLength / size;
            a.TrySetCell(row, 0, "2"); b.TrySetCell(row, 0, "3");
        }
        a.Matrix.InputStore.Flush(); b.Matrix.InputStore.Flush();
        string[] paths = [a.Matrix.StoragePath!, b.Matrix.StoragePath!];
        long released = MatrixRecreation.Release(ref a, ref b, ref c);
        TestAssert.That(a is null && b is null && c is null, "Reset must have an empty session, not 1x1 inputs.");
        return (paths, released);
    }
}
