using MathSolver.Services;

namespace MathSolver.Views;

public partial class CalculationPage
{
    private void UpdateAndroidCalculationTabSizing()
    {
#if ANDROID
        if (AndroidCalculationSubTabScrollView.Content is not HorizontalStackLayout tabs || !tabs.IsLoaded) return;
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
