using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixFileDraftEditing()
    {
        using var draft = new MatrixDraft(3, 4, fileBacked: true, cacheLimitBytes: 1);
        TestAssert.That(draft.Matrix.IsFileBacked && draft.MissingCount == 12 && !draft.HasAnyValues,
            "A forced disk draft must start blank without a dense values array.");
        TestAssert.Throws<InvalidOperationException>(() => _ = draft.Matrix.Data,
            "Dense data access must never materialize a file-backed matrix.");
        draft.TrySetCell(0, 0, "0"); draft.TrySetCell(2, 3, "-9223372036854775808");
        TestAssert.That(draft.MissingCount == 10 && draft.GetCellText(0, 0) == "0"
            && draft.GetCellText(0, 1) == "" && draft.Matrix.GetValue(2, 3) == long.MinValue,
            "Disk edits must distinguish blank from explicit zero and preserve all Int64 digits.");
        long[] row = new long[4]; draft.Matrix.ReadRow(2, 0, row);
        TestAssert.That(row.SequenceEqual(new long[] { 0, 0, 0, long.MinValue }), "Row reads must preserve exact values.");
        draft.FillMissingWithZero();
        TestAssert.That(draft.IsComplete && draft.GetCellText(1, 2) == "0"
            && draft.Matrix.GetValue(2, 3) == long.MinValue, "Missing fill must retain existing values.");
        TestAssert.That(draft.TryClearCells(1, 1, 2, 3) && draft.MissingCount == 6
            && draft.GetCellText(0, 0) == "0" && draft.GetCellText(2, 3) == "",
            "Disk rectangle clearing must retain values outside the rectangle and restore missing state.");
        var path = draft.Matrix.StoragePath!; draft.Dispose(); draft.Dispose();
        TestAssert.That(!File.Exists(path), "Disposing a draft must delete its owned temporary file.");
        TestAssert.Throws<ObjectDisposedException>(() => draft.Matrix.GetValue(0, 0), "Disposed inputs cannot be read.");
    }

    public static void CheckMatrixFileDraftPaste()
    {
        using var draft = new MatrixDraft(3, 4, fileBacked: true, cacheLimitBytes: 1);
        draft.TrySetCell(0, 0, "88");
        using var pasted = draft.WithPastedCells(1, 1, MatrixClipboardCodec.Read("1\t\r\n0\t-9"));
        TestAssert.That(pasted.Matrix.IsFileBacked && pasted.GetCellText(0, 0) == "88"
            && pasted.GetCellText(1, 1) == "1" && pasted.GetCellText(1, 2) == ""
            && pasted.GetCellText(2, 1) == "0" && pasted.GetCellText(2, 2) == "-9" && pasted.MissingCount == 8,
            "Disk paste must clone original values and missing cells atomically.");
        TestAssert.That(draft.MissingCount == 11 && draft.GetCellText(1, 1) == "", "Paste must preserve its source draft.");
        using var filled = draft.WithPastedCells(0, 1, 3, 3, MatrixClipboardCodec.Read("7"));
        TestAssert.That(filled.GetCellText(2, 3) == "7" && filled.MissingCount == 2,
            "A single clipboard value must broadcast across disk-backed selection rectangles.");
        using var stop = new CancellationTokenSource(); stop.Cancel();
        TestAssert.Throws<OperationCanceledException>(() => draft.WithPastedCells(0, 0, MatrixClipboardCodec.Read("3"), stop.Token),
            "Canceled paste must leave the source and its file intact.");
        TestAssert.That(draft.GetCellText(0, 0) == "88" && File.Exists(draft.Matrix.StoragePath), "Cancellation must preserve the source file.");
    }

    public static void CheckMatrixFileDraftImportAndCreationOwnership()
    {
        string directory = Path.Combine(Path.GetTempPath(), "matrix-draft-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = new IntegerMatrix(2, 2, new MatrixInputStore(4, directory, 1));
            input.InputStore!.Set(0, 42);
            using (var imported = new MatrixDraft(input))
                TestAssert.That(imported.IsComplete && imported.GetCellText(0, 0) == "42" && imported.GetCellText(1, 1) == "0",
                    "An imported disk input must mark implicit zero cells complete without a dense filled mask.");
            MatrixDraft? a = null, b = null;
            using (var created = MatrixRecreation.TryCreate(1024, 1024, 1024, 1024, 0, temporaryDirectory: directory))
            {
                TestAssert.That(created.TryPublish(ref a, ref b, 1024, 1024, 1024, 1024), "Matching drafts must publish.");
            }
            TestAssert.That(a!.Matrix.IsFileBacked && b!.Matrix.IsFileBacked && Directory.GetFiles(directory).Length == 2,
                "Publishing must transfer live files beyond attempt disposal.");
            MatrixCalculation? result = null;
            long released = MatrixRecreation.Release(ref a, ref b, ref result);
            TestAssert.That(a is null && b is null && released < 32L * 1024 * 1024 && Directory.GetFiles(directory).Length == 0,
                "Release must delete both input files and account resident memory instead of logical arrays.");
            using var stop = new CancellationTokenSource();
            using var canceled = MatrixRecreation.TryCreate(1024, 1024, 1024, 1024, 0, stop.Token,
                new MatrixProgress(p => { if (p >= .5) stop.Cancel(); }), directory);
            TestAssert.That(canceled.IsCanceled && Directory.GetFiles(directory).Length == 0,
                "Cancellation after A allocation must delete every unpublished input file.");
            using (var unpublished = MatrixRecreation.TryCreate(1024, 1024, 1024, 1024, 0, temporaryDirectory: directory)) { }
            TestAssert.That(Directory.GetFiles(directory).Length == 0, "Rejected attempts must dispose their unpublished drafts.");
            TestAssert.Throws<InvalidOperationException>(() => MatrixRecreation.TryCreate(1024, 1024, 1024, 1024, 0,
                progress: new MatrixProgress(p => { if (p >= 1) throw new InvalidOperationException("observer failed"); }),
                temporaryDirectory: directory), "Creation observer failures must propagate.");
            TestAssert.That(Directory.GetFiles(directory).Length == 0, "A failure after allocating B must delete both unpublished files.");
        }
        finally { Directory.Delete(directory); }
    }

    public static void CheckMatrixFileDraftFailedImportOwnership()
    {
        var store = new MatrixInputStore(131072, cacheLimitBytes: 1);
        string path = store.FilePath;
        try
        {
            // Force the first mixed block to disk and leave only the second in cache.
            store.Set(0, 7); store.Set(65536, 9); store.Flush();
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                file.SetLength(0);
            var matrix = new IntegerMatrix(128, 1024, store);
            TestAssert.Throws<EndOfStreamException>(() => new MatrixDraft(matrix), "Import must propagate truncated backing-store reads.");
            TestAssert.That(!File.Exists(path), "A failed imported-draft constructor must dispose its input and delete its temporary file.");
        }
        finally { store.Dispose(); }
    }
}
