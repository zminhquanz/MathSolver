namespace MathSolver.Services;

/// <summary>Bounds the choice dialog by its content and the available logical viewport.</summary>
internal readonly record struct QuizChoiceLayout(double Width, double Height, int Columns,
    double ItemHeight, double Padding)
{
    internal static QuizChoiceLayout Calculate(double viewportWidth, double viewportHeight,
        int itemCount, double textScale = 1, double scrollbarGutter = 0, double headerHeight = 0)
    {
        double scale = Math.Max(1, textScale);
        double availableWidth = Math.Max(1, viewportWidth - 24);
        double padding = availableWidth < 600 ? 12 : 16;
        double frame = 2 * padding + 2;
        double width = Math.Min(1100, availableWidth);
        int count = Math.Max(0, itemCount);
        int capacity = Math.Clamp((int)Math.Floor((width - frame - scrollbarGutter + 10)
            / (320 * scale + 10)), 1, 3);
        // Four options become 2 × 2 instead of leaving one card alone in a second row.
        int rows = Math.Max(1, (count + capacity - 1) / capacity);
        int columns = Math.Max(1, (count + rows - 1) / rows);
        width = Math.Min(width, Math.Max(560, columns * 320 * scale
            + (columns - 1) * 10 + frame + scrollbarGutter));
        double itemHeight = 136 * scale;
        double chrome = frame + 24 + (headerHeight > 0 ? headerHeight : 96 * scale);
        double contentHeight = count == 0 ? 96 * scale : rows * itemHeight + (rows - 1) * 10;
        double height = Math.Min(Math.Max(1, viewportHeight - 24), Math.Min(780, chrome + contentHeight));
        return new(width, height, columns, itemHeight, padding);
    }
}
