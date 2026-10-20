namespace MathSolver.Controls;

internal enum MatrixCellMove { Left, Right, Up, Down, Next, Previous }
internal readonly record struct MatrixCellBounds(double X, double Y, double Width, double Height);

/// <summary>Pixel scrolling, hit testing and guarded selection without native UI dependencies.</summary>
internal sealed class MatrixViewportState
{
    public const double HeaderWidth = 58;
    public int Rows { get; private set; } = 1;
    public int Columns { get; private set; } = 1;
    public int SelectedRow { get; private set; }
    public int SelectedColumn { get; private set; }
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    public double Width { get; private set; } = 1;
    public double Height { get; private set; } = 1;
    public double CellWidth { get; private set; } = 184;
    public double CellHeight { get; private set; } = 52;
    public int FirstRow => (int)(OffsetY / CellHeight);
    public int FirstColumn => (int)(OffsetX / CellWidth);
    public double MaxX => Math.Max(0, Columns * CellWidth - Math.Max(1, Width - HeaderWidth));
    public double MaxY => Math.Max(0, Rows * CellHeight - Math.Max(1, Height - CellHeight));
    public void SetSource(int rows, int columns)
    {
        if (rows is < 1 or > 1024 || columns is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(rows));
        Rows = rows; Columns = columns;
        SelectedRow = Math.Min(SelectedRow, rows - 1);
        SelectedColumn = Math.Min(SelectedColumn, columns - 1);
        ScrollTo(OffsetX, OffsetY);
    }
    public void Resize(double width, double height, double cellWidth, double cellHeight)
    {
        Width = Math.Max(1, width); Height = Math.Max(1, height);
        CellWidth = Math.Max(1, cellWidth); CellHeight = Math.Max(1, cellHeight);
        ScrollTo(OffsetX, OffsetY);
    }
    public bool ScrollTo(double x, double y)
    {
        double previousX = OffsetX, previousY = OffsetY;
        OffsetX = Math.Clamp(x, 0, MaxX); OffsetY = Math.Clamp(y, 0, MaxY);
        return OffsetX != previousX || OffsetY != previousY;
    }
    public bool ScrollBy(double x, double y) => ScrollTo(OffsetX + x, OffsetY + y);
    public bool Select(int row, int column, Func<bool>? commit = null)
    {
        // Validate/commit the old editor before changing either selection or scroll.
        if (commit?.Invoke() == false) return false;
        SelectedRow = Math.Clamp(row, 0, Rows - 1);
        SelectedColumn = Math.Clamp(column, 0, Columns - 1);
        double availableWidth = Math.Max(1, Width - HeaderWidth), availableHeight = Math.Max(1, Height - CellHeight);
        double x = OffsetX, y = OffsetY;
        double left = SelectedColumn * CellWidth, top = SelectedRow * CellHeight;
        if (left < x || CellWidth > availableWidth) x = left;
        else if (left + CellWidth > x + availableWidth) x = left + CellWidth - availableWidth;
        if (top < y || CellHeight > availableHeight) y = top;
        else if (top + CellHeight > y + availableHeight) y = top + CellHeight - availableHeight;
        ScrollTo(x, y);
        return true;
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
        HeaderWidth + column * CellWidth - OffsetX, CellHeight + row * CellHeight - OffsetY, CellWidth, CellHeight);
    public bool TryHitCell(double x, double y, out int row, out int column)
    {
        row = column = -1;
        if (x < HeaderWidth || y < CellHeight || x >= Width || y >= Height) return false;
        int r = (int)((y - CellHeight + OffsetY) / CellHeight), c = (int)((x - HeaderWidth + OffsetX) / CellWidth);
        if (r >= Rows || c >= Columns) return false;
        row = r; column = c;
        return true;
    }
}
