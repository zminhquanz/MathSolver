using MathSolver.Controls;
using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixLargeDimensions()
    {
        var tall = new MatrixDraft(16384, 1);
        var wide = new MatrixDraft(1, 16384);
        TestAssert.That(tall.TrySetCell(16383, 0, "19") && wide.TrySetCell(0, 16383, "23"),
            "The final row and column at the supported maximum must be editable.");
        TestAssert.That(tall.GetCellText(16383, 0) == "19" && wide.GetCellText(0, 16383) == "23",
            "Tall and wide matrices must preserve their final cell values.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new IntegerMatrix(16385, 1), "Rows above 16384 must be rejected before allocating.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new IntegerMatrix(1, 16385), "Columns above 16384 must be rejected before allocating.");
    }

    public static void CheckMatrixLargeViewport()
    {
        var viewport = new MatrixViewportState();
        viewport.SetSource(16384, 16384);
        viewport.Arrange(720, 400, 96, 36, 20);
        viewport.Select(16383, 16383);
        var bounds = viewport.Bounds(16383, 16383);
        TestAssert.That(viewport.SelectedRow == 16383 && viewport.SelectedColumn == 16383
            && bounds.X >= MatrixViewportState.HeaderWidth && bounds.Y >= viewport.CellHeight
            && bounds.X + bounds.Width <= viewport.Width + 0.001 && bounds.Y + bounds.Height <= viewport.Height + 0.001,
            "Virtual scrolling must reveal the final cell of a 16384-square matrix without allocating its cells.");
        viewport.SelectAll();
        TestAssert.That(viewport.Selection.Rows == 16384 && viewport.Selection.Columns == 16384,
            "Select-all must cover every supported row and column.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => viewport.SetSource(16385, 1), "The viewport must reject dimensions above the model limit.");
    }

    public static void CheckMatrixLargeImportAndClipboard()
    {
        string tall = string.Concat(Enumerable.Repeat("7\n", 16384));
        string wide = string.Join('\t', Enumerable.Repeat("8", 16384));
        using var tallReader = new StringReader(tall);
        using var wideReader = new StringReader(wide);
        TestAssert.That(MatrixTextCodec.Read(tallReader).Rows == 16384 && MatrixTextCodec.Read(wideReader).Columns == 16384,
            "Streaming table imports must accept both full-height and full-width matrices.");
        TestAssert.That(MatrixClipboardCodec.Read(tall).Rows == 16384 && MatrixClipboardCodec.Read(wide).Columns == 16384,
            "Clipboard parsing must share the model's dimension limit.");
        TestAssert.That(MatrixClipboardCodec.Create(1, 16384, (_, _) => "8")!.Text == wide + "\r\n",
            "Clipboard writing must include all 16384 columns.");
        TestAssert.Throws<MatrixInputException>(() => MatrixClipboardCodec.Read(tall + "7\n"), "Clipboard rows above the limit must be rejected.");
        TestAssert.Throws<MatrixInputException>(() => MatrixClipboardCodec.Read(wide + "\t8"), "Clipboard columns above the limit must be rejected.");
        using var tooTallReader = new StringReader(tall + "7\n");
        using var tooWideReader = new StringReader(wide + "\t8");
        TestAssert.Throws<MatrixInputException>(() => MatrixTextCodec.Read(tooTallReader), "CSV rows above the limit must be rejected.");
        TestAssert.Throws<MatrixInputException>(() => MatrixTextCodec.Read(tooWideReader), "CSV columns above the limit must be rejected.");
        int reads = 0;
        TestAssert.That(MatrixClipboardCodec.Create(16384, 16384, (_, _) => { reads++; return ""; }) is null,
            "A table above clipboard capacity must return an ordinary unsuccessful result without throwing.");
        TestAssert.That(reads == 0, "Impossible clipboard writes must be rejected before visiting cells.");
        string longCell = new('9', 2048);
        TestAssert.That(MatrixClipboardCodec.Create(1, 16384, (_, _) => longCell) is null,
            "Content growth beyond clipboard capacity must also return without throwing or publishing partial data.");
        string escapedCell = new('&', 2048);
        TestAssert.That(MatrixClipboardCodec.Create(1, 16384, (_, _) => escapedCell, includeHtml: true) is null,
            "HTML encoding expansion must return an ordinary capacity result independently of plain text.");
    }

    public static void CheckMatrixSelectionCsvExport()
    {
        var draft = new MatrixDraft(3, 4);
        draft.TrySetCell(1, 1, "9223372036854775807");
        draft.TrySetCell(2, 1, "-42"); draft.TrySetCell(2, 2, "0");
        using var writer = new StringWriter { NewLine = "\n" };
        MatrixTextCodec.Write(writer, 2, 2, (r, c) => draft.GetCellText(r + 1, c + 1));
        TestAssert.That(writer.ToString() == "9223372036854775807,\n-42,0\n",
            "Selected CSV export must preserve offsets, blank cells, zeros, signs and every Int64 digit.");

        using var cancellation = new CancellationTokenSource();
        using var largeWriter = new StringWriter { NewLine = "\n" };
        int reads = 0;
        TestAssert.Throws<OperationCanceledException>(() => MatrixTextCodec.Write(largeWriter, 16384, 16384, (r, c) =>
        {
            reads++;
            if (r == 1 && c == 16383) cancellation.Cancel();
            return "1";
        }, cancellation.Token), "Huge CSV exports must stream rows and stop promptly when cancelled.");
        TestAssert.That(reads == 32768 && largeWriter.ToString().Count(c => c == '\n') == 2,
            "Export must write rows incrementally instead of staging the full 16384-square table in memory.");
    }

    public static void CheckMatrixMemoryBudget()
    {
        TestAssert.That(MatrixMemoryGuard.IsMemoryFailure(new AggregateException(new OutOfMemoryException(),
                new AggregateException(new MatrixMemoryException(100, 10)))),
            "Parallel worker allocation failures must receive the same memory message as sequential allocations.");
        TestAssert.That(!MatrixMemoryGuard.IsMemoryFailure(new AggregateException(new OutOfMemoryException(), new InvalidOperationException())),
            "Mixed parallel failures must not hide unrelated errors behind a memory message.");
        MatrixMemoryGuard.EnsureAvailable(128L * 1024 * 1024, 256L * 1024 * 1024, 0, true);
        TestAssert.Throws<MatrixMemoryException>(() => MatrixMemoryGuard.EnsureAvailable(20L * 1024 * 1024, 16L * 1024 * 1024, 0, true),
            "Allocations beyond available memory must fail before touching the managed heap.");
        TestAssert.Throws<MatrixMemoryException>(() => MatrixMemoryGuard.EnsureAvailable(256L * 1024 * 1024, 4L * 1024 * 1024 * 1024, 1400L * 1024 * 1024, false),
            "A 32-bit process must retain address-space headroom even on a machine with plentiful RAM.");
        try { MatrixMemoryGuard.EnsureAvailable(123, 100, 0, true); }
        catch (MatrixMemoryException error)
        {
            TestAssert.That(error.RequiredBytes == 123 && error.AvailableBytes <= 100,
                "Memory failures must expose the requested bytes and usable budget.");
            return;
        }
        throw new InvalidOperationException("An allocation beyond the deterministic budget must fail.");
    }

    public static void CheckMatrixCalculationMemoryPreflight()
    {
        // Keep the non-throwing policy regression for the original reported 18 GiB request.
        // The block planner separately proves that calculations no longer reserve that dense workspace.
        long required = 19327352832L;
        int memoryExceptions = 0;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> observer = (_, args) =>
        {
            if (args.Exception is MatrixMemoryException) memoryExceptions++;
        };
        AppDomain.CurrentDomain.FirstChanceException += observer;
        try
        {
            var denied = MatrixMemoryGuard.CheckAvailable(required, 3007239414L, 0, true);
            TestAssert.That(!denied.CanAllocate && denied.RequiredBytes == required && denied.AvailableBytes == 2738803958L,
                "Insufficient RAM must return a normal outcome with the required bytes and usable budget.");
            TestAssert.That(MatrixMemoryGuard.CheckAvailable(128L * 1024 * 1024, 256L * 1024 * 1024, 0, true).CanAllocate,
                "A calculation within its memory budget must remain available.");
            TestAssert.That(!MatrixMemoryGuard.CheckAvailable(256L * 1024 * 1024, 4L * 1024 * 1024 * 1024,
                1400L * 1024 * 1024, false).CanAllocate, "The non-throwing preflight must retain 32-bit address-space protection.");
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= observer; }
        TestAssert.That(memoryExceptions == 0,
            "Expected memory refusal must not throw even a caught first-chance exception that pauses the debugger.");

        var a = new IntegerMatrix(2, 2, [2, 2, 2, 2]);
        var attempt = MatrixEngine.TryCalculate(a, a, MatrixOperation.Multiply, MatrixBackend.Scalar);
        TestAssert.That(attempt.Result is { } result && result.GetValue(0, 0) == 8 && result.GetValue(1, 1) == 8,
            "The application calculation API must still publish the exact product when memory is sufficient.");
        TestAssert.That(a.Data.All(value => value == 2), "Preflight and multiplication must retain the input values.");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var canceledAttempt = MatrixEngine.TryCalculate(a, a, MatrixOperation.Multiply, cancellationToken: cancelled.Token);
        TestAssert.That(canceledAttempt.IsCanceled && canceledAttempt.Result is null,
            "Cancellation must take precedence over starting a calculation and return a normal stopped outcome.");
    }
}
