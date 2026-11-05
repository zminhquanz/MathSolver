namespace MathSolver.Services;

/// <summary>Captures local arranged bounds; never reads Window or screen geometry.</summary>
internal static class WorkspaceLayoutCapture
{
    internal static WorkspaceLayoutMetrics Capture(VisualElement viewport,
        double horizontalChrome = 0d, double verticalChrome = 0d) =>
        WorkspaceLayoutMetrics.FromAvailable(viewport.Width, viewport.Height,
            ResponsiveLayoutPolicy.TextScale, horizontalChrome, verticalChrome);

    // Only the grid owner may reserve its fixed final action row. A scrolling
    // viewport has already lost this row and must not call this method again.
    internal static double FixedFooterHeight(Grid root)
    {
        if (root.RowDefinitions.Count <= 2) return 0d;
        int lastRow = root.RowDefinitions.Count - 1;
        return root.Children.OfType<View>()
            .Where(view => view.IsVisible && Grid.GetRow(view) == lastRow && Grid.GetRowSpan(view) == 1)
            .Select(view => WorkspaceLayoutMetrics.NormalizeDimension(view.Height)
                + WorkspaceLayoutMetrics.NormalizeDimension(view.Margin.Top)
                + WorkspaceLayoutMetrics.NormalizeDimension(view.Margin.Bottom))
            .DefaultIfEmpty(0d).Max();
    }
}
