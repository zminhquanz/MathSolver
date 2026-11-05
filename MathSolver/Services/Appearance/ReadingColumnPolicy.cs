namespace MathSolver.Services;

/// <summary>Finite reading widths during initial or unbounded native measurement.</summary>
internal static class ReadingColumnPolicy
{
    internal static double Width(double viewport, double maximum, double inset, bool capBeforeInset)
    {
        double available = double.IsFinite(viewport) && viewport > 0 ? viewport : 320;
        return Math.Max(1, capBeforeInset
            ? Math.Min(maximum, available) - inset
            : Math.Min(maximum, available - inset));
    }
}
