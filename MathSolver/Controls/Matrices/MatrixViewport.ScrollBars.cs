using Microsoft.Maui.Graphics;

namespace MathSolver.Controls;

public sealed partial class MatrixViewport
{
    // These rails belong to the bounded viewport, not to a million-cell native layout.
    private const double ScrollBarThickness = 20;
    private enum ScrollAxis { None, Horizontal, Vertical }
    private ScrollAxis _scrollAxis;
    private bool _barInteraction, _panBlocked;
    private double _thumbGrab;

    private MatrixScrollBarState HorizontalBar => MatrixScrollBarState.Create(
        MatrixViewportState.HeaderWidth, Math.Max(0, _state.Width - MatrixViewportState.HeaderWidth),
        Math.Max(1, _state.Width - MatrixViewportState.HeaderWidth), _state.ContentWidth, _state.OffsetX);
    private MatrixScrollBarState VerticalBar => MatrixScrollBarState.Create(
        _state.CellHeight, Math.Max(0, _state.Height - _state.CellHeight),
        Math.Max(1, _state.Height - _state.CellHeight), _state.ContentHeight, _state.OffsetY);

    private bool StartScrollBar(PointF point)
    {
        _scrollAxis = ScrollAxis.None;
        bool vertical = _state.ShowVerticalScrollBar && point.X >= _state.Width;
        bool horizontal = _state.ShowHorizontalScrollBar && point.Y >= _state.Height;
        if (!vertical && !horizontal) return false;
        if (vertical && horizontal) return true; // Corner between the two rails.
        var bar = vertical ? VerticalBar : HorizontalBar;
        double position = vertical ? point.Y : point.X;
        if (!bar.CanScroll || position < bar.Start || position > bar.Start + bar.Length) return true;
        if (!_state.EndEdit(CommitEdit)) { RestoreInvalidEditor(); return true; }
        if (bar.ContainsThumb(position))
        {
            _scrollAxis = vertical ? ScrollAxis.Vertical : ScrollAxis.Horizontal;
            _thumbGrab = position - bar.ThumbStart;
        }
        else
        {
            double page = bar.Length * (position < bar.ThumbStart ? -1 : 1);
            if (vertical) _state.ScrollBy(0, page);
            else _state.ScrollBy(page, 0);
        }
        Scrolled();
        return true;
    }

    private void DragScrollBar(PointF point)
    {
        if (_scrollAxis == ScrollAxis.Horizontal)
            _state.ScrollTo(HorizontalBar.OffsetForThumbPosition(point.X - _thumbGrab), _state.OffsetY);
        else if (_scrollAxis == ScrollAxis.Vertical)
            _state.ScrollTo(_state.OffsetX, VerticalBar.OffsetForThumbPosition(point.Y - _thumbGrab));
        else return;
        Scrolled();
    }

    private void DrawScrollBars(ICanvas canvas)
    {
        var horizontal = HorizontalBar;
        var vertical = VerticalBar;
        float right = (float)_state.Width, bottom = (float)_state.Height;
        float thickness = (float)ScrollBarThickness;
        if (_state.ShowHorizontalScrollBar)
        {
            canvas.FillColor = Line.WithAlpha(0.12f);
            canvas.FillRectangle((float)horizontal.Start, bottom, (float)horizontal.Length, thickness);
            canvas.FillColor = Ink.WithAlpha(0.5f);
            canvas.FillRoundedRectangle((float)horizontal.ThumbStart, bottom + 5, (float)horizontal.ThumbLength, thickness - 10, 5);
        }
        if (_state.ShowVerticalScrollBar)
        {
            canvas.FillColor = Line.WithAlpha(0.12f);
            canvas.FillRectangle(right, (float)vertical.Start, thickness, (float)vertical.Length);
            canvas.FillColor = Ink.WithAlpha(0.5f);
            canvas.FillRoundedRectangle(right + 5, (float)vertical.ThumbStart, thickness - 10, (float)vertical.ThumbLength, 5);
        }
    }
}
