using MathSolver.Controls;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixPhoneCellLayout()
    {
        foreach (double scale in new[] { 1d, 1.6d })
        {
            var state = new MatrixViewportState(); state.SetSource(3, 3);
            var size = MatrixCellLayout.FitSmall(336, 360, 3, 3, 112 * scale, 48 * scale, scale, 14);
            state.Arrange(336, 360, size.Width, size.Height, 14);
            if (state.ShowHorizontalScrollBar || state.ShowVerticalScrollBar || size.Height < 48 * scale)
                throw new Exception("A3x3 matrix must fit both axes with usable touch row heights.");
            state.SetColumnWidth(0, 240);
            state.Arrange(336, 360, size.Width, size.Height, 14);
            if (state.GetColumnWidth(0) != 240 || !state.ShowHorizontalScrollBar)
                throw new Exception("Manual column sizing must win over automatic default fitting.");
        }
        var tiny = new MatrixViewportState(); tiny.SetSource(3, 3);
        var small = MatrixCellLayout.FitSmall(150, 110, 3, 3, 112, 48, 1, 14);
        tiny.Arrange(150, 110, small.Width, small.Height, 14);
        if (small.Width < MatrixViewportState.MinColumnWidth || small.Height < 48 || !tiny.ShowHorizontalScrollBar || !tiny.ShowVerticalScrollBar)
            throw new Exception("Tiny/keyboard viewports scroll instead of shrinking below usable cell sizes.");
        var large = MatrixCellLayout.FitSmall(336, 360, 100, 100, 112, 48, 1, 14);
        if (large != (112d, 48d)) throw new Exception("Large matrices retain their standard virtualized cell dimensions.");
    }
}
