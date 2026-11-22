using MathSolver.Services;
namespace MathSolver.Testing;

public static class WorkspaceLayoutMetricsTests
{
    public static void Run()
    {
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        var shellContent = WorkspaceLayoutMetrics.FromAvailable(1100, 460, 1, 60, 100);
        Check(shellContent.AvailableWidth == 1040 && shellContent.AvailableHeight == 360,
            "Owned chrome must be deducted once from arranged content.");
        Check(ResponsiveLayoutPolicy.ForSolver(shellContent).UseSideBySide, "Inclusive two-column boundary changed.");
        Check(!ResponsiveLayoutPolicy.ForSolver(WorkspaceLayoutMetrics.FromAvailable(1100, 459, 1, 60, 100)).UseSideBySide,
            "A short content viewport must stack even when the outer window is wide.");
        var scaled = WorkspaceLayoutMetrics.FromAvailable(1620, 640, 1.5, 60, 100);
        Check(scaled.EffectiveWidth == 1040 && scaled.EffectiveHeight == 360,
            "Chrome is deducted in DIPs before text normalization.");
        Check(ResponsiveLayoutPolicy.ForSolver(scaled).UseSideBySide, "Scaled boundary must match logical boundary.");
        Check(!ResponsiveLayoutPolicy.ForSolver(WorkspaceLayoutMetrics.FromAvailable(800, 700, 1, 40, 60)).CompactHeader,
            "Header threshold must include explicit content deductions.");
        Check(ResponsiveLayoutPolicy.ForSolver(WorkspaceLayoutMetrics.FromAvailable(800, 699, 1, 40, 60)).CompactHeader,
            "Header must compact below 640 effective DIPs.");
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d })
        {
            var size = WorkspaceLayoutMetrics.FromAvailable(invalid, invalid, invalid, invalid, invalid);
            Check(size.AvailableWidth == 0 && size.AvailableHeight == 0 && size.TextScale == 1,
                "Invalid dimensions, deductions and text scale must normalize safely.");
        }
        var exhausted = WorkspaceLayoutMetrics.FromAvailable(20, 30, 0.5, 100, 100);
        Check(exhausted.EffectiveWidth == 0 && exhausted.EffectiveHeight == 0 && exhausted.TextScale == 1,
            "Chrome cannot create negative available space or shrink the text scale.");
        // An Android resized content viewport already excludes the IME. No implicit keyboard reserve.
        Check(WorkspaceLayoutMetrics.FromAvailable(1080, 400).AvailableHeight == 400,
            "Already resized content must not lose IME height again.");
        Check(ResponsiveLayoutPolicy.ForSolver(default(WorkspaceLayoutMetrics)) is { CompactHeader: true, UseSideBySide: false },
            "The default value must have safe policy behavior.");
    }
}
