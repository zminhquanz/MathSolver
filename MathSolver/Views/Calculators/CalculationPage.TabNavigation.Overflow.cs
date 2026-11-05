using MathSolver.Controls.Navigation;

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
        TopicNavigationOverflow.Refresh(AndroidCalculationSubTabBar, CalculationSubTabScrollView,
            _tabNavigationStacked, CalculationTabStripHost.IsVisible, "Calculation.Navigation.ScrollHint");
    }
}
