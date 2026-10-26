using MathSolver.Controls;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckDisplayDensity()
    {
        Require(DisplayDensityPolicy.Resolve(DisplayDensityMode.Automatic, true) == DisplayDensityMode.Touch,
            "A touch tablet or hybrid laptop must get Touch without an OS-name heuristic.");
        Require(DisplayDensityPolicy.Resolve(DisplayDensityMode.Automatic, false) == DisplayDensityMode.Compact,
            "Pointer-only devices must get Compact automatically.");
        Require(DisplayDensityPolicy.Resolve(DisplayDensityMode.Automatic, null) == DisplayDensityMode.Touch,
            "Unknown capabilities must retain a usable touch target size.");
        foreach (bool? touch in new bool?[] { true, false, null })
        {
            Require(DisplayDensityPolicy.Resolve(DisplayDensityMode.Compact, touch) == DisplayDensityMode.Compact,
                "Manual Compact must override device capability.");
            Require(DisplayDensityPolicy.Resolve(DisplayDensityMode.Touch, touch) == DisplayDensityMode.Touch,
                "Manual Touch must override device capability.");
        }
        foreach (string? stored in new[] { null, "", "bogus", "999", "-1" })
            Require(DisplayDensityPolicy.Parse(stored) == DisplayDensityMode.Automatic,
                "An invalid saved preference must safely return to Automatic.");
        foreach (var mode in Enum.GetValues<DisplayDensityMode>())
            Require(DisplayDensityPolicy.Parse(mode.ToString()) == mode, "The requested mode must round-trip through storage.");
        var compact = DisplayDensityPolicy.Metrics(DisplayDensityMode.Compact);
        var roomy = DisplayDensityPolicy.Metrics(DisplayDensityMode.Touch);
        Require(compact.ControlHeight >= 40 && roomy.ControlHeight >= 48 && compact.ControlHeight < roomy.ControlHeight,
            "Touch targets must be larger while Compact retains a usable pointer target.");
        Require(compact.CardPadding < roomy.CardPadding && compact.Spacing < roomy.Spacing,
            "Density must also change surrounding space instead of only renaming a setting.");
        var viewport = new MatrixViewportState();
        viewport.SetSource(8, 8);
        viewport.Arrange(600, 300, compact.MatrixColumnWidth, compact.MatrixRowHeight, 20);
        viewport.SetColumnWidth(1, 210); viewport.SetRowHeight(1, 77);
        viewport.Select(3, 4);
        viewport.Arrange(600, 300, roomy.MatrixColumnWidth, roomy.MatrixRowHeight, 20);
        Require(viewport.GetColumnWidth(1) == 210 && viewport.GetRowHeight(1) == 77,
            "Changing density must preserve custom matrix cell dimensions.");
        Require(viewport.GetRowHeight(0) == roomy.MatrixRowHeight && viewport.SelectedRow == 3 && viewport.SelectedColumn == 4,
            "Unmodified matrix rows should adopt density while retaining selection.");
    }
}
