using MathSolver.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MathSolver.Views;

public partial class CalculationPage
{
    private double _tabClipWidth = -1, _tabClipHeight = -1;

    private void UpdateCalculationViewportClip()
    {
        if (OperatingSystem.IsAndroid() || !CanUseTabNavigation || !CalculationTabStripHost.IsVisible) return;
        double width = CalculationTabStripHost.Width, height = CalculationTabStripHost.Height;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return;
        if (Math.Abs(width - _tabClipWidth) < .5 && Math.Abs(height - _tabClipHeight) < .5
            && CalculationTabStripHost.Clip is not null) return;
        double radius = Math.Min(10, Math.Min(width, height) / 2);
        // Clip the viewport, not individual tabs: partially visible cells keep
        // smooth corners while horizontal panning and press feedback stay native.
        string path = FormattableString.Invariant($"M {radius},0 L {width - radius},0 A {radius},{radius} 0 0 1 {width},{radius} L {width},{height - radius} A {radius},{radius} 0 0 1 {width - radius},{height} L {radius},{height} A {radius},{radius} 0 0 1 0,{height - radius} L 0,{radius} A {radius},{radius} 0 0 1 {radius},0 Z");
        CalculationTabStripHost.Clip = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(path)!;
        _tabClipWidth = width; _tabClipHeight = height;
    }

}
