using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckSolverLayoutPolicy()
    {
        Require(ResponsiveLayoutPolicy.ForSolver(1280, 720) is { CompactHeader: false, UseSideBySide: true },
            "A desktop with ample height may retain its full header while placing results beside inputs.");
        Require(ResponsiveLayoutPolicy.ForSolver(1280, 639).CompactHeader &&
            !ResponsiveLayoutPolicy.ForSolver(1280, 640).CompactHeader,
            "Header compaction must follow the actual scaled remaining height, not the split layout alone.");
        Require(ResponsiveLayoutPolicy.ForSolver(1100, 500).UseSideBySide,
            "A short laptop can keep readable side-by-side columns while compacting its header.");
        Require(ResponsiveLayoutPolicy.ForSolver(390, 740) is { CompactHeader: true, UseSideBySide: false },
            "A phone must use a short header and a single reading flow.");
        Require(ResponsiveLayoutPolicy.ForSolver(800, 1024) is { CompactHeader: false, UseSideBySide: false },
            "A portrait tablet can retain its full header without squeezing two columns.");
        Require(ResponsiveLayoutPolicy.ForSolver(1280, 250) is { CompactHeader: true, UseSideBySide: false },
            "A keyboard-height viewport must release header space and keep a single flow.");
        Require(ResponsiveLayoutPolicy.ForSolver(1280, 900, 1.5) is { CompactHeader: true, UseSideBySide: false },
            "Enlarged text must reserve readable width instead of keeping cramped desktop columns.");
        Require(ResponsiveLayoutPolicy.ForSolver(1040, 360).UseSideBySide &&
            !ResponsiveLayoutPolicy.ForSolver(1039, 360).UseSideBySide &&
            !ResponsiveLayoutPolicy.ForSolver(1040, 359).UseSideBySide,
            "Both width and remaining height must satisfy the workspace boundary.");
        Require(ResponsiveLayoutPolicy.ForSolver(1560, 540, 1.5).UseSideBySide,
            "All layout boundaries must scale together with text size.");
        foreach (double width in new[] { 0d, 320, 600, 760, 1024, 1040, 1366, 1920, 3840 })
        foreach (double height in new[] { 0d, 180, 359, 360, 640, 768, 1080 })
        foreach (double scale in new[] { 1d, 1.25, 1.5, 2, 3 })
        {
            var state = ResponsiveLayoutPolicy.ForSolver(width, height, scale);
            Require(state == ResponsiveLayoutPolicy.ForSolver(width, height, scale),
                "The same viewport must produce stable layout decisions across repeated measure passes.");
            if (state.UseSideBySide)
                Require(width / scale >= 1040 && height / scale >= 360,
                    "A split workspace must meet both scaled boundaries.");
            if (width / scale < 760 || height / scale < 640)
                Require(state.CompactHeader, "Constrained width or height must compact the header.");
        }
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d })
        {
            Require(ResponsiveLayoutPolicy.ForSolver(invalid, 800) is { CompactHeader: true, UseSideBySide: false },
                "Unallocated or invalid dimensions must never enable an invalid split layout.");
            Require(ResponsiveLayoutPolicy.ForSolver(1280, invalid) is { CompactHeader: true, UseSideBySide: false },
                "Invalid remaining height must use the safe single-column state.");
            Require(ResponsiveLayoutPolicy.ForSolver(1280, 720, invalid) == ResponsiveLayoutPolicy.ForSolver(1280, 720, 1),
                "Invalid text scale must fall back to the standard accessible scale.");
        }
        Require(ResponsiveLayoutPolicy.Columns(800, 280, 3, 12, 1) == 2 &&
            ResponsiveLayoutPolicy.Columns(800, 280, 3, 12, 1.5) == 1,
            "Column count must use the same text scale as the workspace.");
        Require(ResponsiveLayoutPolicy.UseStackedLayout(800, 700, 1.5) &&
            !ResponsiveLayoutPolicy.UseStackedLayout(800, 700, 1),
            "Component reflow must share the policy's scaling rules.");
        Require(ResponsiveLayoutPolicy.Columns(366, 140, 2, 10, 1) == 2 &&
            ResponsiveLayoutPolicy.Columns(366, 140, 2, 10, 1.5) == 1,
            "Fixed actions should share one phone row normally, but stack when enlarged text needs more width.");
    }
}
