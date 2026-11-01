using MathSolver.Services;

namespace MathSolver.Views;

public partial class CalculationPage
{
    private void InitializeCalculationOverflow()
    {
        CalculationSubTabScrollView.Scrolled += (_, _) => RefreshCalculationOverflow();
    }

    private void RefreshCalculationOverflow()
    {
        if (!CanUseTabNavigation) return;
        if (AndroidCalculationSubTabBar.IsVisible)
        {
            SemanticProperties.SetHint(AndroidCalculationSubTabBar,
                LocalizationService.TranslateKey("Calculation.Navigation.ScrollHint"));
            return;
        }
        var scroll = CalculationSubTabScrollView;
        double remaining = Math.Max(0, scroll.ContentSize.Width - scroll.Width);
        bool available = !_tabNavigationStacked && CalculationTabStripHost.IsVisible && scroll.Width > 0 && remaining > 1;
        SemanticProperties.SetHint(scroll, available
            ? LocalizationService.TranslateKey("Calculation.Navigation.ScrollHint") : "");
    }
}
