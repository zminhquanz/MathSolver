using MathSolver.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MathSolver.Views;

public partial class FormulaPage
{
    private double _tabClipWidth = -1, _tabClipHeight = -1;

    private void UpdateFormulaViewportClip()
    {
        if (!CanUseTabNavigation || !FormulaTabStripHost.IsVisible) return;
        double width = FormulaTabStripHost.Width, height = FormulaTabStripHost.Height;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return;
        if (Math.Abs(width - _tabClipWidth) < .5 && Math.Abs(height - _tabClipHeight) < .5
            && FormulaTabStripHost.Clip is not null) return;
        double radius = Math.Min(10, Math.Min(width, height) / 2);
        // Clip the viewport, not individual tabs: partially visible cells keep
        // smooth corners while horizontal panning and press feedback stay native.
        string path = FormattableString.Invariant($"M {radius},0 L {width - radius},0 A {radius},{radius} 0 0 1 {width},{radius} L {width},{height - radius} A {radius},{radius} 0 0 1 {width - radius},{height} L {radius},{height} A {radius},{radius} 0 0 1 0,{height - radius} L 0,{radius} A {radius},{radius} 0 0 1 {radius},0 Z");
        FormulaTabStripHost.Clip = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(path)!;
        _tabClipWidth = width; _tabClipHeight = height;
    }

    private void UpdateAndroidFormulaTabSizing()
    {
#if ANDROID
        if (AndroidFormulaSubTabScrollView.Content is not HorizontalStackLayout tabs || !tabs.IsLoaded) return;
        var cells = tabs.Children.OfType<Grid>().ToArray();
        var buttons = cells.Select(cell => cell.Children.OfType<Button>().First()).ToArray();
        if (buttons.Length == 0 || buttons.Any(button => !button.IsLoaded || button.Handler?.MauiContext is null)) return;
        // Measure the button rather than its previously sized parent, so locale
        // and text scaling can grow the common size without retaining old widths.
        var sizes = buttons.Select(button => button.Measure(double.PositiveInfinity, double.PositiveInfinity)).ToArray();
        double width = Math.Ceiling(Math.Max(170 * ResponsiveLayoutPolicy.TextScale, sizes.Max(size => size.Width)));
        double height = Math.Ceiling(Math.Max(DisplayDensityManager.Metrics.ControlHeight, sizes.Max(size => size.Height))) + 3;
        foreach (var cell in cells)
        {
            if (Math.Abs(cell.WidthRequest - width) > .5) cell.WidthRequest = width;
            if (Math.Abs(cell.HeightRequest - height) > .5) cell.HeightRequest = height;
        }
#endif
    }
}
