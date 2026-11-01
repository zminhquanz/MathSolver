using MathSolver.Services;

namespace MathSolver.Views;

public partial class FormulaPage
{
    private void InitializeFormulaOverflow()
    {
        FormulaSubTabScrollView.Scrolled += (_, _) => RefreshFormulaOverflow();
    }

    private void RefreshFormulaOverflow()
    {
        if (!CanUseTabNavigation) return;
        if (AndroidFormulaSubTabBar.IsVisible)
        {
            SemanticProperties.SetHint(AndroidFormulaSubTabBar,
                LocalizationService.TranslateKey("Formula.Navigation.ScrollHint"));
            return;
        }
        var scroll = FormulaSubTabScrollView;
        double remaining = Math.Max(0, scroll.ContentSize.Width - scroll.Width);
        bool available = !_tabNavigationStacked && FormulaTabStripHost.IsVisible && scroll.Width > 0 && remaining > 1;
        SemanticProperties.SetHint(scroll, available
            ? LocalizationService.TranslateKey("Formula.Navigation.ScrollHint") : "");
    }
}
