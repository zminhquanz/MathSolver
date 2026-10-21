using MathSolver.Controls;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixResizing()
    {
        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        var state = new MatrixViewportState();
        Require(state.CellWidth == 96 && state.CellHeight == 36,
            "Default matrix cells should be compact enough for ordinary small integers.");
        state.SetSource(3, 3);
        state.Arrange(700, 340, 184, 52, 20);
        state.SetColumnWidth(0, 250);
        state.SetRowHeight(1, 90);
        state.Arrange(700, 340, 184, 52, 20);
        var cell = state.Bounds(1, 1);
        Require(cell == new MatrixCellBounds(308, 104, 184, 90), "Resizing must move later columns and rows without changing their own sizes.");
        Require(state.ContentWidth == 618 && state.ContentHeight == 194, "Variable dimensions must contribute to the full scroll extent.");
        Require(state.TryHitCell(350, 150, out int row, out int column) && row == 1 && column == 1,
            "Clicking a resized table must use its variable cell boundaries.");
        Require(state.TryHitResizeHandle(305, 20, 6, out var axis, out int index) && axis == MatrixResizeAxis.Column && index == 0,
            "The column's header edge must be grabbable just before its boundary.");
        Require(state.TryHitResizeHandle(311, 20, 6, out axis, out index) && axis == MatrixResizeAxis.Column && index == 0,
            "The same column must resize when grabbing just after its boundary.");
        Require(state.TryHitResizeHandle(20, 194, 6, out axis, out index) && axis == MatrixResizeAxis.Row && index == 1,
            "A row edge must resize the row above it.");
        Require(!state.TryHitResizeHandle(308, 150, 6, out _, out _), "Dragging cell borders in the body must not resize a row or column.");
        state.SetColumnWidth(1, 600);
        state.SetRowHeight(2, 400);
        state.Arrange(700, 340, 184, 52, 20);
        Require(state.ShowHorizontalScrollBar && state.ShowVerticalScrollBar, "Growing cell sizes can require both scrollbars.");
        state.ScrollTo(260, 147);
        Require(state.FirstColumn == 1 && state.FirstRow == 2, "Visible indexes must follow resized prefixes rather than division by the default size.");
        state.ScrollTo(state.MaxX, state.MaxY);
        var last = state.Bounds(2, 2);
        Require(last.X + last.Width == state.Width && last.Y + last.Height == state.Height,
            "The last resized cell must remain reachable at the scroll limits.");
        var secondColumn = state.Bounds(0, 1);
        Require(state.TryHitResizeHandle(secondColumn.X + secondColumn.Width, 20, 6, out axis, out index)
            && axis == MatrixResizeAxis.Column && index == 1, "Resize hit testing must include the scroll offset.");
        state.Select(2, 1);
        state.BeginEdit(true);
        Require(state.Bounds(2, 1).X == MatrixViewportState.HeaderWidth && state.Bounds(2, 1).Y == state.CellHeight,
            "An oversized cell must reveal its beginning for inline editing.");
        state.CancelEdit();
        state.Arrange(700, 340, 200, 60, 20);
        Require(state.GetColumnWidth(0) == 250 && state.GetColumnWidth(2) == 200 && state.GetRowHeight(1) == 90,
            "A refresh or text-scale change must retain manual sizes and update untouched dimensions.");
        for (int i = 0; i < 3; i++) { state.SetColumnWidth(i, -100); state.SetRowHeight(i, 0); }
        state.Arrange(700, 340, 184, 52, 20);
        Require(state.GetColumnWidth(0) == MatrixViewportState.MinColumnWidth && state.GetRowHeight(0) == MatrixViewportState.MinRowHeight,
            "Resizing must keep a usable minimum cell size.");
        Require(!state.ShowHorizontalScrollBar && !state.ShowVerticalScrollBar && state.OffsetX == 0 && state.OffsetY == 0,
            "Shrinking cells to fit must hide both rails and clamp offsets.");
        state.SetSource(1024, 1024);
        state.SetColumnWidth(1023, 500); state.SetRowHeight(1023, 150);
        state.Arrange(700, 340, 184, 52, 20);
        state.ScrollTo(state.MaxX, state.MaxY);
        last = state.Bounds(1023, 1023);
        Require(last.X + last.Width == state.Width && last.Y + last.Height == state.Height,
            "The final resized cell of a 1024 by 1024 matrix must remain reachable.");
        Require(state.TryHitCell(state.Width - 1, state.Height - 1, out row, out column) && row == 1023 && column == 1023,
            "Large-grid hit testing must still reach its final cell.");
        state.SetSource(3, 3);
        state.Arrange(700, 340, 96, 36, 20);
        state.Select(1, 1);
        state.SetColumnWidth(0, 120); state.SetColumnWidth(1, 800); state.SetRowHeight(1, 400);
        state.Arrange(700, 340, 96, 36, 20);
        Require(state.ShowHorizontalScrollBar && state.ShowVerticalScrollBar, "Oversized selected dimensions must cause overflow.");
        state.ResetSelectedDimensions();
        state.Arrange(700, 340, 96, 36, 20);
        Require(state.GetColumnWidth(1) == 96 && state.GetRowHeight(1) == 36 && state.GetColumnWidth(0) == 120,
            "Reset must restore only the selected row/column and retain adjacent manual sizes.");
        Require(!state.ShowHorizontalScrollBar && !state.ShowVerticalScrollBar, "Resetting dimensions to fit must remove both rails.");
        state.Arrange(700, 340, 144, 54, 20);
        Require(state.GetColumnWidth(1) == 144 && state.GetRowHeight(1) == 54,
            "Reset dimensions must follow later system text scaling rather than retain a fixed override.");
        Console.WriteLine("Checked independent row/column sizes, resize handles, scrolling, minimum sizes, refresh and the final cell at 1024 × 1024.");
    }
    public static void CheckMatrixAutomaticScrollBars()
    {
        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        var state = new MatrixViewportState();
        state.SetSource(3, 3);
        state.Arrange(700, 340, 184, 52, 20);
        Require(!state.ShowHorizontalScrollBar && !state.ShowVerticalScrollBar && state.Width == 700 && state.Height == 340,
            "A small matrix must hide both scrollbars and reclaim their space.");
        state.SetSource(3, 10);
        state.Arrange(700, 340, 184, 52, 20);
        Require(state.ShowHorizontalScrollBar && !state.ShowVerticalScrollBar && state.Height == 320 && state.Width == 700,
            "A wide matrix must show only its horizontal scrollbar.");
        state.SetSource(20, 2);
        state.Arrange(700, 340, 184, 52, 20);
        Require(!state.ShowHorizontalScrollBar && state.ShowVerticalScrollBar && state.Width == 680 && state.Height == 340,
            "A tall matrix must show only its vertical scrollbar.");
        state.SetSource(5, 3);
        state.Arrange(610, 310, 184, 52, 20);
        Require(state.ShowHorizontalScrollBar && state.ShowVerticalScrollBar,
            "A vertical scrollbar can reduce width enough to require horizontal scrolling too.");
        state.ScrollTo(state.MaxX, state.MaxY);
        state.Arrange(1000, 800, 184, 52, 20);
        Require(!state.ShowHorizontalScrollBar && !state.ShowVerticalScrollBar && state.OffsetX == 0 && state.OffsetY == 0,
            "Growing the viewport must hide both bars and reset obsolete scroll offsets.");
        state.Arrange(610, 312, 184, 52, 20);
        Require(!state.ShowHorizontalScrollBar && !state.ShowVerticalScrollBar,
            "Exact-fit dimensions must choose no scrollbars rather than a self-sustaining pair.");
        Console.WriteLine("Checked no overflow, each single axis, coupled overflow, exact fit and growing the viewport.");
    }
    public static void CheckMatrixCellInteraction()
    {
        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        var state = new MatrixViewportState();
        state.Resize(600, 320, 184, 52);
        state.SetSource(1024, 1024);
        int commits = 0;
        bool Commit() { commits++; return true; }
        Require(state.TapCell(0, 0, true, Commit) == MatrixCellAction.Selected && !state.IsEditing,
            "The first click on the initial cell must only select it.");
        Require(commits == 0, "Selection must not save an unopened editor.");
        Require(state.TapCell(0, 0, true, Commit) == MatrixCellAction.Editing && state.IsEditing,
            "The second click on the selected cell must start editing.");
        Require(state.TapCell(1, 2, true, Commit) == MatrixCellAction.Selected && !state.IsEditing && commits == 1,
            "Clicking a different cell must save the old edit and select without editing.");
        state.TapCell(1, 2, true, Commit);
        Require(state.TapCell(900, 900, true, () => false) == MatrixCellAction.Rejected
            && state.IsEditing && state.SelectedRow == 1 && state.SelectedColumn == 2,
            "An invalid value must keep its editor and selection.");
        state.CancelEdit();
        Require(!state.IsEditing && commits == 1, "Cancel must end editing without saving.");
        state.TapCell(1, 2, true, Commit);
        Require(!state.EndEdit(() => false) && state.IsEditing, "A rejected commit must retain editing.");
        Require(state.EndEdit(Commit) && !state.IsEditing, "An accepted commit must end editing.");
        state.SetSource(3, 3);
        Require(state.TapCell(1, 2, true) == MatrixCellAction.Selected,
            "Changing matrices must reset the click sequence even at the same coordinate.");
        Require(state.TapCell(1, 2, false) == MatrixCellAction.Selected && !state.IsEditing,
            "A result cell must remain read-only after repeated clicks.");
        state.TapCell(1, 2, true);
        Require(state.Navigate(MatrixCellMove.Next, Commit) && !state.IsEditing
            && state.SelectedRow == 2 && state.SelectedColumn == 0,
            "Tab must save and move to the next row without opening another editor.");
        Require(!state.BeginEdit(false) && !state.IsEditing, "F2/Enter must respect read-only mode.");
        state.SetSource(1024, 1024);
        state.Select(1000, 1000);
        state.ScrollTo(0, 0);
        state.BeginEdit(true);
        var editingBounds = state.Bounds(1000, 1000);
        Require(editingBounds.X >= MatrixViewportState.HeaderWidth && editingBounds.Y >= state.CellHeight
            && editingBounds.X + editingBounds.Width <= state.Width && editingBounds.Y + editingBounds.Height <= state.Height,
            "Starting an edit after scrolling away must reveal the selected cell.");
        Console.WriteLine("Checked first/second clicks, matrix switching, read-only cells, Tab, cancel and invalid edits.");
    }

    public static void CheckMatrixScrollBars()
    {
        static void Close(double actual, double expected, string message)
        {
            if (Math.Abs(actual - expected) > 0.00001) throw new InvalidOperationException(message);
        }
        var state = new MatrixViewportState();
        state.Resize(600, 320, 184, 52);
        state.SetSource(1024, 1024);
        double bodyWidth = state.Width - MatrixViewportState.HeaderWidth;
        double bodyHeight = state.Height - state.CellHeight;
        var horizontal = MatrixScrollBarState.Create(58, bodyWidth, bodyWidth, 1024 * 184, 0);
        var vertical = MatrixScrollBarState.Create(52, bodyHeight, bodyHeight, 1024 * 52, 0);
        Close(horizontal.ThumbStart, 58, "The horizontal thumb must start at the first column.");
        Close(vertical.ThumbStart, 52, "The vertical thumb must start below the header.");
        Close(horizontal.OffsetForThumbPosition(horizontal.Start + horizontal.Length - horizontal.ThumbLength),
            state.MaxX, "Dragging right must reach the last column, including a minimum-size thumb.");
        Close(vertical.OffsetForThumbPosition(vertical.Start + vertical.Length - vertical.ThumbLength),
            state.MaxY, "Dragging down must reach the last row.");
        state.ScrollTo(state.MaxX, state.MaxY);
        var end = MatrixScrollBarState.Create(58, bodyWidth, bodyWidth, 1024 * 184, state.OffsetX);
        Close(end.ThumbStart + end.ThumbLength, 600, "The thumb must follow wheel/programmatic scrolling.");
        var bounds = state.Bounds(1023, 1023);
        Close(bounds.X + bounds.Width, 600, "The final column must fit before the scrollbar.");
        Close(bounds.Y + bounds.Height, 320, "The final row must fit above the scrollbar.");
        if (!state.TryHitCell(599, 319, out int row, out int column) || row != 1023 || column != 1023)
            throw new InvalidOperationException("Scrolled cell hit testing must still select the final cell.");
        if (state.TryHitCell(601, 319, out _, out _) || state.TryHitCell(599, 321, out _, out _))
            throw new InvalidOperationException("Scrollbar areas must never hit a cell.");
        var fits = MatrixScrollBarState.Create(58, 542, 542, 184, 0);
        Close(fits.ThumbLength, 542, "A fitting table must use a disabled full-length thumb.");
        Close(fits.OffsetForThumbPosition(500), 0, "A fitting table cannot scroll.");
        var small = MatrixScrollBarState.Create(0, 12, 12, 10000, 5000);
        if (small.ThumbLength > 12 || !double.IsFinite(small.ThumbStart)
            || !double.IsFinite(small.OffsetForThumbPosition(10)))
            throw new InvalidOperationException("Tiny viewports must have finite clamped scrollbar geometry.");
        Console.WriteLine("Checked both axes at 1024 × 1024, end positions, hit testing, fitting tables and tiny viewports.");
    }
}
