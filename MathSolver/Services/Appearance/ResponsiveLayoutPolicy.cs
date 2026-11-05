namespace MathSolver.Services;

internal readonly record struct SolverLayoutState(bool CompactHeader, bool UseSideBySide);

/// <summary>Uses logical content width, so resizing, split-screen and enlarged text share a layout policy.</summary>
internal static class ResponsiveLayoutPolicy
{
    internal static double TextScale
    {
        get
        {
#if WINDOWS
            return Math.Max(1d, new Windows.UI.ViewManagement.UISettings().TextScaleFactor);
#elif ANDROID
            return Math.Max(1d, Android.App.Application.Context.Resources?.Configuration?.FontScale ?? 1f);
#else
            return 1d;
#endif
        }
    }

    internal static SolverLayoutState ForSolver(double width, double height, double textScale = 1d)
    {
        return ForSolver(WorkspaceLayoutMetrics.FromAvailable(width, height, textScale));
    }

    internal static SolverLayoutState ForSolver(WorkspaceLayoutMetrics space)
    {
        bool split = space.EffectiveWidth >= 1040d && space.EffectiveHeight >= 360d;
        return new(space.EffectiveWidth < 760d || space.EffectiveHeight < 640d, split);
    }
    internal static int Columns(double width, double minimumColumnWidth, int maximumColumns, double spacing = 12d) =>
        Columns(width, minimumColumnWidth, maximumColumns, spacing, TextScale);

    internal static int Columns(double width, double minimumColumnWidth, int maximumColumns, double spacing, double textScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumColumns);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minimumColumnWidth, 0d);
        spacing = NormalizeDimension(spacing);
        double count = Math.Floor((NormalizeDimension(width) + spacing) /
            (minimumColumnWidth * NormalizeScale(textScale) + spacing));
        return (int)Math.Clamp(count, 1d, maximumColumns);
    }

    internal static bool UseStackedLayout(double width, double threshold) =>
        UseStackedLayout(width, threshold, TextScale);

    internal static bool UseStackedLayout(double width, double threshold, double textScale) =>
        NormalizeDimension(width) < threshold * NormalizeScale(textScale);

    internal static double SubTabWidth(double pageWidth) =>
        Math.Max(1d, Math.Min(pageWidth, 1320d) - 40d);

    private static double NormalizeDimension(double value) => WorkspaceLayoutMetrics.NormalizeDimension(value);
    private static double NormalizeScale(double value) => WorkspaceLayoutMetrics.NormalizeScale(value);
}
