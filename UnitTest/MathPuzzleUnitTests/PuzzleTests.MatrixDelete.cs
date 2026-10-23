using MathSolver.Controls;
using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixDeleteCells()
    {
        var input = new IntegerMatrix(100, 100);
        for (int i = 0; i < input.Data.Length; i++) input.Data[i] = i;
        var draft = new MatrixDraft(input);
        var selection = new MatrixViewportState(); selection.SetSource(100, 100);
        selection.Select(7, 9);
        TestAssert.That(selection.TryGetDeleteRange(true, out var cell) && draft.TryClearCells(cell.Row, cell.Column, cell.Rows, cell.Columns),
            "Delete on a single selected cell must clear it.");
        TestAssert.That(draft.GetCellText(7, 9) == "" && draft.MissingCount == 1 && draft.Matrix.Data[709] == 0
            && draft.GetCellText(7, 8) == "708" && draft.GetCellText(7, 10) == "710" && !draft.IsComplete,
            "Delete must make the chosen cell missing, zero its stored value, and retain neighbors.");

        selection.Select(80, 90); selection.ExtendSelection(10, 20);
        TestAssert.That(selection.TryGetDeleteRange(true, out var range) && range == new MatrixCellRange(10, 20, 71, 71),
            "Reverse Shift selection must target the full normalized rectangle, including off-screen cells.");
        TestAssert.That(draft.TryClearCells(range.Row, range.Column, range.Rows, range.Columns), "A valid selected rectangle must clear.");
        for (int row = 0; row < 100; row++)
        for (int column = 0; column < 100; column++)
        {
            bool empty = row == 7 && column == 9 || row >= 10 && row <= 80 && column >= 20 && column <= 90;
            TestAssert.That(draft.GetCellText(row, column) == (empty ? "" : (row * 100 + column).ToString()),
                "Only cells inside the Shift-selected rectangle may be deleted.");
        }
        TestAssert.That(draft.MissingCount == 5042 && draft.GetCellText(0, 0) == "0", "Explicit zero outside the selection must remain filled.");
        draft.TryClearCells(range.Row, range.Column, range.Rows, range.Columns);
        TestAssert.That(draft.MissingCount == 5042, "Deleting already-empty cells must not increment the missing count again.");
        foreach (var invalid in new[] { new MatrixCellRange(-1, 0, 1, 1), new(99, 99, 2, 1), new(0, 0, 0, 1), new(0, 100, 1, 1), new(0, 0, int.MaxValue, 1) })
            TestAssert.That(!draft.TryClearCells(invalid.Row, invalid.Column, invalid.Rows, invalid.Columns)
                && draft.MissingCount == 5042 && draft.GetCellText(99, 99) == "9999", "Invalid clear requests must be rejected without mutation.");

        selection.SelectAll();
        TestAssert.That(selection.TryGetDeleteRange(true, out var all) && all == new MatrixCellRange(0, 0, 100, 100)
            && draft.TryClearCells(all.Row, all.Column, all.Rows, all.Columns), "Ctrl+A Delete must clear every cell.");
        TestAssert.That(draft.MissingCount == 10000 && !draft.HasAnyValues && draft.Matrix.Data.All(v => v == 0),
            "Deleting all must clear values and filled markers throughout the draft.");
        draft.TrySetCell(99, 99, "-9223372036854775808");
        TestAssert.That(draft.MissingCount == 9999 && draft.GetCellText(99, 99) == "-9223372036854775808", "Deleted cells must accept new exact Int64 values.");
        draft.FillMissingWithZero();
        TestAssert.That(draft.IsComplete && draft.GetCellText(0, 0) == "0", "Filling empty cells must still work after Delete.");

        foreach (var shape in new[] { (1, 16384), (16384, 1) })
        {
            var edge = new MatrixDraft(shape.Item1, shape.Item2);
            edge.FillMissingWithZero();
            edge.TryClearCells(shape.Item1 - 1, shape.Item2 - 1, 1, 1);
            TestAssert.That(edge.MissingCount == 1 && edge.GetCellText(shape.Item1 - 1, shape.Item2 - 1) == "",
                "Delete must reach the final off-screen cell at the maximum supported row/column boundary.");
            edge.TryClearCells(0, 0, shape.Item1, shape.Item2);
            TestAssert.That(edge.MissingCount == 16384 && !edge.HasAnyValues, "Full-row and full-column clearing must retain correct counts.");
        }
    }

    public static void CheckMatrixDeleteInteraction()
    {
        var state = new MatrixViewportState(); state.SetSource(20, 30);
        state.Select(12, 18); state.ExtendSelection(3, 4);
        var range = state.Selection;
        TestAssert.That(!state.TryGetDeleteRange(false, out _) && state.Selection == range,
            "Read-only results or locked input must not emit a grid deletion.");
        TestAssert.That(state.TryGetDeleteRange(true, out var selected) && selected == range && state.Selection == range && !state.IsEditing,
            "Delete must preserve the selected rectangle and stay out of edit mode.");
        state.BeginEdit(true);
        TestAssert.That(!state.TryGetDeleteRange(true, out _) && state.IsEditing,
            "While editing, Delete must belong to the native text caret instead of clearing a grid selection.");
        state.CancelEdit();
        TestAssert.That(state.TryGetDeleteRange(true, out selected) && selected == new MatrixCellRange(12, 18, 1, 1),
            "After text editing ends, grid Delete must target the active cell.");
    }
}
