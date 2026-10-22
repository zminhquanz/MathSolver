using MathSolver.Controls;
using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixRangeSelection()
    {
        var state = new MatrixViewportState();
        state.SetSource(1024, 1024); state.Arrange(600, 340, 96, 36, 20);
        state.Select(5, 6); state.ExtendSelection(2, 3);
        TestAssert.That(state.Selection == new MatrixCellRange(2, 3, 4, 4), "Shift click must normalize a reverse rectangle.");
        TestAssert.That(state.SelectedRow == 5 && state.SelectedColumn == 6 && !state.IsEditing,
            "Shift selection must retain its anchor and must never edit a cell.");
        TestAssert.That(state.IsSelected(2, 3) && state.IsSelected(5, 6) && !state.IsSelected(1, 3), "Only the selected rectangle should be shaded.");
        state.ExtendSelection(8, 9);
        TestAssert.That(state.Selection == new MatrixCellRange(5, 6, 4, 4), "Repeated Shift clicks must keep the original anchor.");
        state.ExtendSelection(1023, 1023);
        TestAssert.That(state.FirstRow > 0 && state.FirstColumn > 0, "Extending a range must reveal its far endpoint.");
        TestAssert.That(state.TapCell(5, 6, true) == MatrixCellAction.Selected && !state.HasRangeSelection && !state.IsEditing,
            "A regular click after range selection must collapse to one cell before editing.");
        state.BeginEdit(true);
        TestAssert.That(!state.ExtendSelection(7, 8, () => false) && state.IsEditing && !state.HasRangeSelection,
            "An invalid edit must prevent a range change.");
        state.CancelEdit(); state.SelectAll(); state.ExtendSelection(7, 8);
        TestAssert.That(!state.AllSelected && state.Selection == new MatrixCellRange(5, 6, 3, 3), "Shift after select-all must start from the active cell.");
        state.SetSource(2, 2);
        TestAssert.That(!state.HasRangeSelection && state.Selection.Rows == 1 && state.Selection.Columns == 1, "Switching matrices must clear the rectangle.");
        state.SetSource(10, 10); state.Select(3, 4); state.ExtendSelection(2, 3);
        var range = state.Selection;
        var copied = MatrixClipboardCodec.Create(range.Rows, range.Columns, (r, c) => ((range.Row + r) * 10 + range.Column + c).ToString());
        TestAssert.That(copied!.Text == "23\t24\r\n33\t34\r\n", "Copy must start at the normalized rectangle's top-left, including reverse selections.");
        Console.WriteLine("Checked forward/reverse Shift rectangles, stable anchors, off-screen endpoints, rejected edits and source changes.");
    }

    public static void CheckMatrixPasteCells()
    {
        var a = new MatrixDraft(2, 2);
        a.TrySetCell(0, 0, "-12"); a.TrySetCell(0, 1, "9223372036854775807");
        a.TrySetCell(1, 1, "0");
        var b = new MatrixDraft(4, 4); b.FillMissingWithZero(); b.TrySetCell(3, 3, "99");
        var text = MatrixClipboardCodec.Create(2, 2, a.GetCellText)!.Text;
        var block = MatrixClipboardCodec.Read(text);
        var pasted = b.WithPastedCells(1, 1, block);
        TestAssert.That(pasted.Rows == 4 && pasted.Columns == 4 && pasted.GetCellText(3, 3) == "99" && b.GetCellText(1, 1) == "0",
            "Pasting must stage a new draft, retain dimensions and preserve values outside the target rectangle.");
        TestAssert.That(pasted.GetCellText(1, 1) == "-12" && pasted.GetCellText(1, 2) == "9223372036854775807"
            && pasted.GetCellText(2, 1) == "" && pasted.GetCellText(2, 2) == "0" && pasted.MissingCount == 1,
            "A-to-B paste must preserve Int64 precision, empty cells and explicit zero.");
        var one = MatrixClipboardCodec.Read("42\r\n");
        var cell = b.WithPastedCells(2, 3, one);
        TestAssert.That(cell.GetCellText(2, 3) == "42" && cell.GetCellText(3, 3) == "99", "A single copied cell must paste into exactly the destination cell.");
        var blank = b.WithPastedCells(0, 0, MatrixClipboardCodec.Read("\r\n"));
        TestAssert.That(blank.GetCellText(0, 0) == "" && blank.MissingCount == 1, "An empty copied cell must clear its destination.");
        TestAssert.Throws<MatrixInputException>(() => b.WithPastedCells(3, 3, block), "Out-of-bounds paste must be rejected atomically.");
        foreach (string invalid in new[] { "1\t2\r\n3", "1\tnope", "9223372036854775808", "1.5", "2\0" })
            TestAssert.Throws<MatrixInputException>(() => MatrixClipboardCodec.Read(invalid), "Invalid pasted cells must not silently change values.");
        TestAssert.That(b.GetCellText(3, 3) == "99" && b.MissingCount == 0, "Rejected pastes must retain the original draft.");
        foreach (string invalid in new[] { "+", "1.2", "9223372036854775808", "12\0" })
            TestAssert.That(!b.TrySetCell(3, 3, invalid) && b.GetCellText(3, 3) == "99", "Cell editing must retain its strict Int64 validation after sharing the paste parser.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        TestAssert.Throws<OperationCanceledException>(() => b.WithPastedCells(0, 0, block, cancelled.Token), "Cancelled paste must not publish a new draft.");
        var edge = new MatrixDraft(1024, 1024).WithPastedCells(1023, 1023, one);
        TestAssert.That(edge.GetCellText(1023, 1023) == "42" && edge.MissingCount == 1024 * 1024 - 1, "Pasting into the final cell must work at the maximum matrix size.");
        Console.WriteLine("Checked single-cell A-to-B paste, partial rectangles, blanks, Int64 precision, bounds, validation, cancellation and 1024x1024 edges.");
    }

    public static void CheckMatrixPasteFillSelection()
    {
        var original = new MatrixDraft(100, 100);
        original.TrySetCell(0, 0, "99");
        original.TrySetCell(99, 99, "-7");
        var selection = new MatrixViewportState();
        selection.SetSource(100, 100);
        selection.Select(70, 80); selection.ExtendSelection(10, 20);
        var range = selection.Selection;
        var one = MatrixClipboardCodec.Read("2\r\n");
        var filled = original.WithPastedCells(range.Row, range.Column, range.Rows, range.Columns, one);
        for (int row = 0; row < 100; row++)
            for (int column = 0; column < 100; column++)
                TestAssert.That(filled.GetCellText(row, column) == (selection.IsSelected(row, column) ? "2" : original.GetCellText(row, column)),
                    "Pasting one value must fill the entire Shift-selected rectangle and retain every cell outside it.");
        TestAssert.That(filled.MissingCount == 10000 - range.Rows * range.Columns - 2 && original.MissingCount == 9998,
            "Filling must update the entered-cell count without mutating the original draft.");

        var all = original.WithPastedCells(0, 0, 100, 100, MatrixClipboardCodec.Read("-9223372036854775808\r\n"));
        TestAssert.That(all.MissingCount == 0 && all.GetCellText(0, 0) == "-9223372036854775808" && all.GetCellText(99, 99) == "-9223372036854775808",
            "Select-all paste must fill all 10,000 cells exactly, including Int64 minimum.");
        var cleared = all.WithPastedCells(range.Row, range.Column, range.Rows, range.Columns, MatrixClipboardCodec.Read("\r\n"));
        TestAssert.That(cleared.MissingCount == range.Rows * range.Columns && cleared.GetCellText(70, 80) == "" && cleared.GetCellText(0, 0) == "-9223372036854775808",
            "Pasting a blank cell must clear the selected rectangle without changing surrounding cells.");
        var zeros = original.WithPastedCells(0, 0, 1, 100, MatrixClipboardCodec.Read("0\r\n"));
        TestAssert.That(zeros.GetCellText(0, 99) == "0" && zeros.MissingCount == 9899,
            "A one-row range must be filled with explicit zeros, not left empty.");
        var columnFill = original.WithPastedCells(0, 99, 100, 1, one);
        TestAssert.That(columnFill.GetCellText(99, 99) == "2" && columnFill.GetCellText(0, 99) == "2", "One-column filling must reach the final row.");
        TestAssert.Throws<MatrixInputException>(() => original.WithPastedCells(99, 99, 2, 2, one), "Out-of-bounds fills must be rejected atomically.");
        TestAssert.Throws<MatrixInputException>(() => original.WithPastedCells(0, 0, 0, 1, one), "Empty destination shapes must be rejected.");
        TestAssert.Throws<MatrixInputException>(() => original.WithPastedCells(0, 0, 3, 3, MatrixClipboardCodec.Read("1\t2\r\n3\t4\r\n")),
            "Multi-cell blocks must not silently repeat or expand into an unrelated destination shape.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        TestAssert.Throws<OperationCanceledException>(() => original.WithPastedCells(0, 0, 100, 100, one, cancelled.Token), "Cancelled fills must not publish changes.");
        TestAssert.That(original.GetCellText(99, 99) == "-7" && original.MissingCount == 9998, "Rejected and cancelled fills must retain all original values.");
        Console.WriteLine("Checked scalar fill across reversed ranges, 10,000 cells, blanks, zeros, rows/columns, exact Int64, invalid bounds and cancellation.");
    }
}
