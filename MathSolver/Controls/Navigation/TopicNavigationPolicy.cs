namespace MathSolver.Controls.Navigation;

/// <summary>Geometry and focus decisions shared by both topic choosers.</summary>
internal static class TopicNavigationPolicy
{
    public static int NextFocus(int current, int count, bool reverse) => count <= 0 ? -1
        : current < 0 ? (reverse ? count - 1 : 0)
        : (current + (reverse ? -1 : 1) + count) % count;

    public static double CardExtent(double available) => double.IsFinite(available)
        ? Math.Max(1, Math.Min(720, available - 24)) : 1;

    public static bool HasOverflow(double viewport, double content, bool visible, bool stacked) =>
        !stacked && visible && double.IsFinite(viewport) && double.IsFinite(content)
        && viewport > 0 && content - viewport > 1;
}
