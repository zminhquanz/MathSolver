using MathSolver.Controls;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixTouchRange()
    {
        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        static bool Begin(MatrixViewportState s, Func<bool>? commit = null) => s.BeginRangeSelection(commit);
        static bool Active(MatrixViewportState s) => s.IsRangeSelectionMode;
        static bool AwaitEnd(MatrixViewportState s) => s.IsAwaitingRangeEnd;
        var state = new MatrixViewportState();
        state.SetSource(100, 100); state.Resize(500, 300, 96, 36);
        state.Select(3, 4);
        Require(Begin(state) && Active(state) && !AwaitEnd(state), "Mode must await a fresh first tap, not reuse the previous selected cell.");
        Require(state.TapCell(3, 4, true) == MatrixCellAction.Selected && !state.IsEditing && AwaitEnd(state), "First tap on an already-selected cell must establish an anchor without editing.");
        state.ScrollTo(state.MaxX, state.MaxY);
        Require(Active(state) && AwaitEnd(state) && state.SelectedRow == 3 && state.SelectedColumn == 4, "Scrolling must retain the off-screen anchor and phase.");
        Require(state.TapCell(99, 99, true) == MatrixCellAction.Selected && !Active(state) && !state.IsEditing, "Last tap must finish the range and leave selection mode.");
        Require(state.Selection == new MatrixCellRange(3, 4, 97, 96), "Both endpoints and all off-screen cells must be included.");
        Require(Begin(state), "Restart mode");
        state.TapCell(90, 80, false); state.ScrollTo(0, 0); state.TapCell(1, 2, false);
        Require(state.Selection == new MatrixCellRange(1, 2, 90, 79) && !state.IsEditing && !Active(state), "Reverse range must also work for read-only result cells.");
        Begin(state); state.TapCell(1, 2, true); state.TapCell(1, 2, true);
        Require(state.Selection == new MatrixCellRange(1, 2, 1, 1) && !state.IsEditing && !Active(state), "Same endpoints select one cell without entering its editor.");
        state.TapCell(1, 2, true);
        Require(state.IsEditing, "Normal second-tap editing must resume after the mode finishes.");
        Require(!Begin(state, () => false) && state.IsEditing && !Active(state), "Invalid pending edits must reject entering mode.");
        Require(Begin(state, () => true) && !state.IsEditing, "Valid edit must commit before entering mode.");
        var previous = state.Selection;
        state.CancelRangeSelection();
        Require(!Active(state) && state.Selection == previous, "Cancelling before a first tap preserves the previous selection.");
        Begin(state); state.TapCell(6, 7, true); state.CancelRangeSelection();
        Require(!Active(state) && state.Selection == new MatrixCellRange(6, 7, 1, 1), "Cancelling after an anchor preserves that cell.");
        Begin(state); state.SetSource(4, 5);
        Require(!Active(state), "Changing matrix source clears range mode.");
        Begin(state); state.Navigate(MatrixCellMove.Down);
        Require(!Active(state), "Explicit navigation clears range mode.");
        Begin(state); state.SelectAll();
        Require(!Active(state) && state.AllSelected, "Select all exits range mode and retains whole-table semantics.");
        Begin(state); state.BeginEdit(true);
        Require(!Active(state) && state.IsEditing, "Explicit editing exits range mode.");
        state.CancelEdit(); state.Select(1, 1); state.ExtendSelection(3, 4);
        Require(state.Selection == new MatrixCellRange(1, 1, 3, 4), "Desktop Shift range extension remains unchanged.");
        Console.WriteLine("Checked two taps, off-screen/reverse/read-only regions, scrolling, same-cell endpoints, edit validation and source/navigation resets.");
    }
}
