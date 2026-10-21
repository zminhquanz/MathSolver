namespace MathSolver.Controls;

/// <summary>Scrollbar geometry in viewport pixels; independent of matrix dimensions or UI framework.</summary>
internal readonly record struct MatrixScrollBarState(
    double Start, double Length, double MaxOffset, double ThumbStart, double ThumbLength)
{
    public bool CanScroll => MaxOffset > 0 && Length > ThumbLength;
    public bool ContainsThumb(double position) => position >= ThumbStart && position <= ThumbStart + ThumbLength;

    public static MatrixScrollBarState Create(double start, double length, double viewport, double content, double offset)
    {
        length = Math.Max(0, length);
        double maxOffset = Math.Max(0, content - viewport);
        double thumb = Math.Min(length, Math.Max(24, length * Math.Min(1, viewport / Math.Max(1, content))));
        double travel = length - thumb;
        double position = maxOffset > 0 ? Math.Clamp(offset, 0, maxOffset) / maxOffset * travel : 0;
        return new(start, length, maxOffset, start + position, thumb);
    }

    public double OffsetForThumbPosition(double position) => CanScroll
        ? Math.Clamp((position - Start) / (Length - ThumbLength), 0, 1) * MaxOffset : 0;
}
