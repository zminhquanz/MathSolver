namespace MathSolver.Services.Appearance;

/// <summary>Keep four short steps readable without an orphaned third column or clipped large text.</summary>
internal static class AiGuideLayoutPolicy
{
    internal static int Columns(double width, double textScale = 1)
    {
        if (!double.IsFinite(width) || width <= 0) return 1;
        double scale = double.IsFinite(textScale) ? Math.Max(1, textScale) : 1;
        const double minimumWidth = 144, gap = 16;
        if (width >= minimumWidth * scale * 4 + gap * 3) return 4;
        return width >= minimumWidth * scale * 2 + gap ? 2 : 1;
    }
}
