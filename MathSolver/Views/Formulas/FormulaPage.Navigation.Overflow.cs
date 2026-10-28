using MathSolver.Services;

namespace MathSolver.Views;

public partial class FormulaPage
{
    private void InitializeFormulaOverflow()
    {
        FormulaSubTabScrollView.Scrolled += (_, _) => RefreshFormulaOverflow();
        AndroidFormulaSubTabScrollView.Scrolled += (_, _) => RefreshFormulaOverflow();
    }

    private void RefreshFormulaOverflow()
    {
        if (!CanUseTabNavigation) return;
        var scroll = AndroidFormulaSubTabBar.IsVisible
            ? (ScrollView)AndroidFormulaSubTabScrollView : FormulaSubTabScrollView;
        double remaining = Math.Max(0, scroll.ContentSize.Width - scroll.Width);
        bool available = !_tabNavigationStacked && FormulaTabStripHost.IsVisible && scroll.Width > 0 && remaining > 1;
        SemanticProperties.SetHint(scroll, available
            ? LocalizationService.TranslateKey("Formula.Navigation.ScrollHint") : "");
    }
}
