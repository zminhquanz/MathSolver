namespace MathSolver.Controls;

internal enum MatrixCellMove { Left, Right, Up, Down, Next, Previous }
internal enum MatrixCellAction { Selected, Editing, Rejected }
internal enum MatrixResizeAxis { None, Column, Row }
internal readonly record struct MatrixCellBounds(double X, double Y, double Width, double Height);

/// <summary>Pixel scrolling, hit testing and guarded selection without native UI dependencies.</summary>
internal sealed class MatrixViewportState
{
    public const double HeaderWidth = 58;
    public const double DefaultColumnWidth = 96, DefaultRowHeight = 36;
    public const double MinColumnWidth = 60, MaxColumnWidth = 1000, MinRowHeight = 32, MaxRowHeight = 500;
    private readonly double[] _columnWidths = new double[1024], _rowHeights = new double[1024];
    private readonly double[] _columnStarts = new double[1025], _rowStarts = new double[1025];
    public MatrixViewportState() => RebuildDimensions();
    public int Rows { get; private set; } = 1;
    public int Columns { get; private set; } = 1;
    public int SelectedRow { get; private set; }
    public int SelectedColumn { get; private set; }
    public bool IsEditing { get; private set; }
    private bool _hasSelection;
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    public double Width { get; private set; } = 1;
    public double Height { get; private set; } = 1;
    public double CellWidth { get; private set; } = DefaultColumnWidth;
    public double CellHeight { get; private set; } = DefaultRowHeight;
    public bool ShowHorizontalScrollBar { get; private set; }
    public bool ShowVerticalScrollBar { get; private set; }
    public double ContentWidth => _columnStarts[Columns];
    public double ContentHeight => _rowStarts[Rows];
    public void Arrange(double width, double height, double cellWidth, double cellHeight, double scrollBarThickness)
    {
        SetDefaultDimensions(cellWidth, cellHeight);
        // Start without rails each time: an exact-fit table must not retain a
        // self-sustaining pair of scrollbars from its previous, smaller viewport.
        bool horizontal = false, vertical = false;
        for (int pass = 0; pass < 3; pass++)
        {
            horizontal = ContentWidth > Math.Max(1, width - HeaderWidth - (vertical ? scrollBarThickness : 0)) + 0.01;
            vertical = ContentHeight > Math.Max(1, height - CellHeight - (horizontal ? scrollBarThickness : 0)) + 0.01;
        }
        ShowHorizontalScrollBar = horizontal;
        ShowVerticalScrollBar = vertical;
        Width = Math.Max(1, width - (vertical ? scrollBarThickness : 0));
        Height = Math.Max(1, height - (horizontal ? scrollBarThickness : 0));
        ScrollTo(OffsetX, OffsetY);
    }
    public int FirstRow => IndexAt(_rowStarts, Rows, OffsetY);
    public int FirstColumn => IndexAt(_columnStarts, Columns, OffsetX);
    public double MaxX => Math.Max(0, ContentWidth - Math.Max(1, Width - HeaderWidth));
    public double MaxY => Math.Max(0, ContentHeight - Math.Max(1, Height - CellHeight));
    public void SetSource(int rows, int columns)
    {
        if (rows is < 1 or > 1024 || columns is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(rows));
        Rows = rows; Columns = columns;
        IsEditing = false;
        _hasSelection = false;
        SelectedRow = Math.Min(SelectedRow, rows - 1);
        SelectedColumn = Math.Min(SelectedColumn, columns - 1);
        RebuildDimensions();
        ScrollTo(OffsetX, OffsetY);
    }
    public void Resize(double width, double height, double cellWidth, double cellHeight)
    {
        Width = Math.Max(1, width); Height = Math.Max(1, height);
        SetDefaultDimensions(cellWidth, cellHeight);
        ShowHorizontalScrollBar = MaxX > 0.01; ShowVerticalScrollBar = MaxY > 0.01;
        ScrollTo(OffsetX, OffsetY);
    }
    private void SetDefaultDimensions(double cellWidth, double cellHeight)
    {
        cellWidth = Math.Max(1, cellWidth); cellHeight = Math.Max(1, cellHeight);
        if (CellWidth == cellWidth && CellHeight == cellHeight) return;
        CellWidth = cellWidth; CellHeight = cellHeight;
        RebuildDimensions();
    }
    private void RebuildDimensions()
    {
        for (int i = 0; i < Columns; i++) _columnStarts[i + 1] = _columnStarts[i] + GetColumnWidth(i);
        for (int i = 0; i < Rows; i++) _rowStarts[i + 1] = _rowStarts[i] + GetRowHeight(i);
    }
    public double GetColumnWidth(int column) => _columnWidths[column] > 0 ? _columnWidths[column] : CellWidth;
    public double GetRowHeight(int row) => _rowHeights[row] > 0 ? _rowHeights[row] : CellHeight;
    public void SetColumnWidth(int column, double width)
    {
        if (column < 0 || column >= Columns || !double.IsFinite(width)) throw new ArgumentOutOfRangeException(nameof(column));
        _columnWidths[column] = Math.Clamp(width, MinColumnWidth, MaxColumnWidth);
        RebuildDimensions(); ScrollTo(OffsetX, OffsetY);
    }
    public void SetRowHeight(int row, double height)
    {
        if (row < 0 || row >= Rows || !double.IsFinite(height)) throw new ArgumentOutOfRangeException(nameof(row));
        _rowHeights[row] = Math.Clamp(height, MinRowHeight, MaxRowHeight);
        RebuildDimensions(); ScrollTo(OffsetX, OffsetY);
    }
    public void ResetSelectedDimensions()
    {
        _columnWidths[SelectedColumn] = 0;
        _rowHeights[SelectedRow] = 0;
        RebuildDimensions(); ScrollTo(OffsetX, OffsetY);
    }
    private static int IndexAt(double[] starts, int count, double position)
    {
        int index = Array.BinarySearch(starts, 0, count + 1, position);
        return Math.Clamp(index >= 0 ? index : ~index - 1, 0, count - 1);
    }
    public bool TryHitResizeHandle(double x, double y, double tolerance, out MatrixResizeAxis axis, out int index)
    {
        axis = MatrixResizeAxis.None; index = -1;
        bool column = y >= 0 && y < CellHeight && x >= HeaderWidth && x <= Width;
        bool row = x >= 0 && x < HeaderWidth && y >= CellHeight && y <= Height;
        if (!column && !row) return false;
        double position = column ? x - HeaderWidth + OffsetX : y - CellHeight + OffsetY;
        var starts = column ? _columnStarts : _rowStarts;
        int count = column ? Columns : Rows;
        int current = IndexAt(starts, count, position);
        int boundary = current + 1;
        if (current > 0 && Math.Abs(position - starts[current]) < Math.Abs(position - starts[boundary])) boundary = current;
        if (Math.Abs(position - starts[boundary]) > tolerance) return false;
        axis = column ? MatrixResizeAxis.Column : MatrixResizeAxis.Row;
        index = boundary - 1;
        return true;
    }
    public bool ScrollTo(double x, double y)
    {
        double previousX = OffsetX, previousY = OffsetY;
        OffsetX = Math.Clamp(x, 0, MaxX); OffsetY = Math.Clamp(y, 0, MaxY);
        return OffsetX != previousX || OffsetY != previousY;
    }
    public bool ScrollBy(double x, double y) => ScrollTo(OffsetX + x, OffsetY + y);
    public MatrixCellAction TapCell(int row, int column, bool canEdit, Func<bool>? commit = null)
    {
        if (_hasSelection && row == SelectedRow && column == SelectedColumn && canEdit)
        {
            BeginEdit(canEdit);
            return MatrixCellAction.Editing;
        }
        return Select(row, column, commit) ? MatrixCellAction.Selected : MatrixCellAction.Rejected;
    }
    public bool BeginEdit(bool canEdit)
    {
        if (!canEdit) return false;
        _hasSelection = true;
        IsEditing = true;
        RevealSelection();
        return true;
    }
    public bool EndEdit(Func<bool>? commit = null)
    {
        if (IsEditing && commit?.Invoke() == false) return false;
        IsEditing = false;
        return true;
    }
    public void CancelEdit() => IsEditing = false;
    public bool Select(int row, int column, Func<bool>? commit = null)
    {
        // Validate/commit the old editor before changing either selection or scroll.
        if (!EndEdit(commit)) return false;
        _hasSelection = true;
        SelectedRow = Math.Clamp(row, 0, Rows - 1);
        SelectedColumn = Math.Clamp(column, 0, Columns - 1);
        RevealSelection();
        return true;
    }
    public void RevealSelection()
    {
        double availableWidth = Math.Max(1, Width - HeaderWidth), availableHeight = Math.Max(1, Height - CellHeight);
        double x = OffsetX, y = OffsetY;
        double left = _columnStarts[SelectedColumn], top = _rowStarts[SelectedRow];
        double cellWidth = GetColumnWidth(SelectedColumn), cellHeight = GetRowHeight(SelectedRow);
        if (left < x || cellWidth > availableWidth) x = left;
        else if (left + cellWidth > x + availableWidth) x = left + cellWidth - availableWidth;
        if (top < y || cellHeight > availableHeight) y = top;
        else if (top + cellHeight > y + availableHeight) y = top + cellHeight - availableHeight;
        ScrollTo(x, y);
    }
    public bool Navigate(MatrixCellMove move, Func<bool>? commit = null)
    {
        if (move is MatrixCellMove.Next or MatrixCellMove.Previous)
        {
            int index = Math.Clamp(SelectedRow * Columns + SelectedColumn + (move == MatrixCellMove.Next ? 1 : -1), 0, Rows * Columns - 1);
            return Select(index / Columns, index % Columns, commit);
        }
        return Select(SelectedRow + (move == MatrixCellMove.Down ? 1 : move == MatrixCellMove.Up ? -1 : 0),
            SelectedColumn + (move == MatrixCellMove.Right ? 1 : move == MatrixCellMove.Left ? -1 : 0), commit);
    }
    public MatrixCellBounds Bounds(int row, int column) => new(
        HeaderWidth + _columnStarts[column] - OffsetX, CellHeight + _rowStarts[row] - OffsetY,
        GetColumnWidth(column), GetRowHeight(row));
    public bool TryHitCell(double x, double y, out int row, out int column)
    {
        row = column = -1;
        if (x < HeaderWidth || y < CellHeight || x >= Width || y >= Height) return false;
        double px = x - HeaderWidth + OffsetX, py = y - CellHeight + OffsetY;
        if (px >= ContentWidth || py >= ContentHeight) return false;
        row = IndexAt(_rowStarts, Rows, py); column = IndexAt(_columnStarts, Columns, px);
        return true;
    }
}
