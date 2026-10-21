using Microsoft.Maui.Graphics;

namespace MathSolver.Controls;

public sealed partial class MatrixViewport
{
    private MatrixResizeAxis _resizeAxis;
    private int _resizeIndex;
    private bool _resizeInteraction;
    private double _resizeStartPosition, _resizeStartSize;

    public double SelectedColumnWidth => _state.GetColumnWidth(SelectedColumn);
    public double SelectedRowHeight => _state.GetRowHeight(SelectedRow);

    /// <summary>Accessible alternative to dragging header edges; applies to the selected row/column.</summary>
    public bool ResizeSelectedDimensions(double width, double height, bool reset = false)
    {
        if (!CanUseInput || !double.IsFinite(width) || !double.IsFinite(height)) return false;
        if (!_state.EndEdit(CommitEdit)) return RestoreInvalidEditor();
        CancelPointerGesture();
        if (reset) _state.ResetSelectedDimensions();
        else
        {
            if (width != SelectedColumnWidth) _state.SetColumnWidth(SelectedColumn, width);
            if (height != SelectedRowHeight) _state.SetRowHeight(SelectedRow, height);
        }
        _state.RevealSelection();
        Scrolled();
        return true;
    }

    private bool StartResize(PointF point)
    {
        _resizeAxis = MatrixResizeAxis.None;
        if (!_state.TryHitResizeHandle(point.X, point.Y, 7, out var axis, out int index)) return false;
        if (!_state.EndEdit(CommitEdit)) { RestoreInvalidEditor(); return true; }
        _resizeAxis = axis; _resizeIndex = index;
        _resizeStartPosition = axis == MatrixResizeAxis.Column ? point.X : point.Y;
        _resizeStartSize = axis == MatrixResizeAxis.Column ? _state.GetColumnWidth(index) : _state.GetRowHeight(index);
        SetParentPanLock(true);
        Refresh();
        return true;
    }

    private void DragResize(PointF point)
    {
        if (_resizeAxis == MatrixResizeAxis.None) return;
        double position = _resizeAxis == MatrixResizeAxis.Column ? point.X : point.Y;
        double size = _resizeStartSize + position - _resizeStartPosition;
        if (_resizeAxis == MatrixResizeAxis.Column) _state.SetColumnWidth(_resizeIndex, size);
        else _state.SetRowHeight(_resizeIndex, size);
        Scrolled();
    }

    private void DragPointer(PointF point)
    {
        if (_resizeAxis != MatrixResizeAxis.None) DragResize(point);
        else DragScrollBar(point);
    }

    private void DrawColumnGrip(ICanvas canvas, double right)
    {
        canvas.StrokeColor = Ink.WithAlpha(0.4f);
        float middle = (float)_state.CellHeight / 2;
        canvas.DrawLine((float)right - 5, middle - 6, (float)right - 5, middle + 6);
        canvas.DrawLine((float)right - 2, middle - 6, (float)right - 2, middle + 6);
        canvas.StrokeColor = Line;
    }

    private void DrawRowGrip(ICanvas canvas, double bottom)
    {
        canvas.StrokeColor = Ink.WithAlpha(0.4f);
        float middle = (float)MatrixViewportState.HeaderWidth / 2;
        canvas.DrawLine(middle - 6, (float)bottom - 5, middle + 6, (float)bottom - 5);
        canvas.DrawLine(middle - 6, (float)bottom - 2, middle + 6, (float)bottom - 2);
        canvas.StrokeColor = Line;
    }

    private void DrawResizeGuide(ICanvas canvas)
    {
        if (_resizeAxis == MatrixResizeAxis.None) return;
        canvas.SaveState();
        canvas.ClipRectangle(0, 0, (float)_state.Width, (float)_state.Height);
        canvas.StrokeColor = Accent; canvas.StrokeSize = 2;
        if (_resizeAxis == MatrixResizeAxis.Column)
        {
            var bounds = _state.Bounds(FirstRow, _resizeIndex);
            float x = (float)(bounds.X + bounds.Width);
            canvas.DrawLine(x, 0, x, (float)_state.Height);
        }
        else
        {
            var bounds = _state.Bounds(_resizeIndex, FirstColumn);
            float y = (float)(bounds.Y + bounds.Height);
            canvas.DrawLine(0, y, (float)_state.Width, y);
        }
        canvas.RestoreState();
    }
}
