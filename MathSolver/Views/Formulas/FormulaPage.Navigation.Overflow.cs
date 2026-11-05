using MathSolver.Controls.Navigation;

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
        TopicNavigationOverflow.Refresh(AndroidFormulaSubTabBar, FormulaSubTabScrollView,
            _tabNavigationStacked, FormulaTabStripHost.IsVisible, "Formula.Navigation.ScrollHint");
    }
}
