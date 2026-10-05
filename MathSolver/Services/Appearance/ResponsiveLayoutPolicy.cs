namespace MathSolver.Services;

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

    internal static int Columns(double width, double minimumColumnWidth, int maximumColumns, double spacing = 12d) =>
        Math.Clamp((int)Math.Floor((Math.Max(0d, width) + spacing) /
            (minimumColumnWidth * TextScale + spacing)), 1, maximumColumns);

    internal static bool UseStackedLayout(double width, double threshold) =>
        width < threshold * TextScale;

    internal static double SubTabWidth(double pageWidth) =>
        Math.Max(1d, Math.Min(pageWidth, 1320d) - 40d);

}
