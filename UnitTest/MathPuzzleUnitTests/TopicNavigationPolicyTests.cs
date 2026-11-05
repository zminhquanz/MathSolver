using MathSolver.Controls.Navigation;

namespace MathPuzzleUnitTests;

internal static class TopicNavigationPolicyTests
{
    public static void Run()
    {
        Check(TopicNavigationPolicy.NextFocus(-1, 2, false) == 0, "Initial forward focus");
        Check(TopicNavigationPolicy.NextFocus(-1, 2, true) == 1, "Initial reverse focus");
        Check(TopicNavigationPolicy.NextFocus(0, 2, true) == 1, "Reverse wraps with no matching topics");
        Check(TopicNavigationPolicy.NextFocus(3, 4, false) == 0, "Forward wraps");
        Check(TopicNavigationPolicy.NextFocus(0, 0, false) == -1, "Empty focus set");
        Check(TopicNavigationPolicy.CardExtent(1000) == 720, "Card maximum");
        Check(TopicNavigationPolicy.CardExtent(300) == 276, "Card inset");
        Check(TopicNavigationPolicy.CardExtent(double.NaN) == 1, "Unarranged width is finite");
        Check(!TopicNavigationPolicy.HasOverflow(100, 101, true, false), "Tolerance avoids false overflow");
        Check(TopicNavigationPolicy.HasOverflow(100, 102, true, false), "Overflow available");
        Check(!TopicNavigationPolicy.HasOverflow(100, 200, true, true), "Stacked hides overflow");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
