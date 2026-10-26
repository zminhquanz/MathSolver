using MathSolver.Services;

namespace MathSolver.Controls;

public partial class SolverResultPlaceholder : Border
{
    public SolverResultPlaceholder()
    {
        InitializeComponent();
        LocalizationService.ExcludeSubtreeFromLegacyTracking(this);
    }
}
