using MathSolver.Services;

namespace MathSolver.Views;

public partial class FormulaPage
{
    private bool _referenceLayoutQueued;

    private static IEnumerable<GraphicsView> ReferenceGraphics(IVisualTreeElement owner)
    {
        foreach (var child in owner.GetVisualChildren())
        {
            if (child is GraphicsView graphics) yield return graphics;
            foreach (var descendant in ReferenceGraphics(child)) yield return descendant;
        }
    }

    private void OnReferenceCardSizeChanged(object? sender, EventArgs e)
    {
        if (_referenceLayoutQueued || !CanUseTabNavigation) return;
        _referenceLayoutQueued = true;
        Dispatcher.Dispatch(() =>
        {
            _referenceLayoutQueued = false;
            if (!CanUseTabNavigation) return;
            if (_selectedSubTab == FormulaSubTab.UnknownComponent)
                UpdateUnknownComponentFlexHeight(ResponsiveLayoutPolicy.Columns(
                    UnknownComponentFlexLayout.Width - 6, 280, 3, 14));
            else if (_selectedSubTab == FormulaSubTab.Geometry)
            {
                // Reuse the established width policy and per-row measurement.
                _lastPlaneGeometryLayoutWidth = _lastSolidGeometryLayoutWidth = -1;
                UpdateGeometryCardWidthsIfNeeded();
            }
        });
    }
}
