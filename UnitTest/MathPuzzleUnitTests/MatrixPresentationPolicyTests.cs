using MathSolver.Services;

namespace MathSolver.Tests;

internal static class MatrixPresentationPolicyTests
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        var input = new MatrixPresentationInput { HasInputs = true, HasResult = true, SelectedMatrix = 2, SideBySide = true };
        var state = MatrixPresentationPolicy.Resolve(input);
        Check(state.Panel == MatrixPanel.Cell && state.ShowSolution && state.EditorEnabled, "Wide C auto shows solution alongside editable table");
        state = MatrixPresentationPolicy.Resolve(input with { SideBySide = false });
        Check(state.Panel == MatrixPanel.None && state.EditorEnabled, "Narrow C does not open details automatically");
        state = MatrixPresentationPolicy.Resolve(input with { DetailsRequested = true, SideBySide = false });
        Check(state.Panel == MatrixPanel.Cell && !state.EditorEnabled, "Explicit narrow details own pointer input");
        state = MatrixPresentationPolicy.Resolve(input with { IsWindows = true, SetupRequested = true, ToolsRequested = true, TechnicalRequested = true });
        Check(state.Panel == MatrixPanel.Setup && !state.ShowSolution && !state.ShowTechnical, "Setup wins conflicting Windows requests");
        state = MatrixPresentationPolicy.Resolve(input with { Busy = true, DetailsRequested = true, TechnicalRequested = true });
        Check(state.Panel == MatrixPanel.None && !state.DetailsRequested && !state.TechnicalRequested, "Busy clears ordinary detail intent");
        state = MatrixPresentationPolicy.Resolve(input with { HasInputs = false, TableExpanded = true, ToolsRequested = true, StatusDetailsAvailable = true });
        Check(state.Panel == MatrixPanel.Status && !state.TableExpanded && state.ToolsRequested, "Recreation error remains readable without matrices");
        state = MatrixPresentationPolicy.Resolve(input with { TechnicalRequested = true, DetailsRequested = true, HasResult = false });
        Check(state.Panel == MatrixPanel.None && !state.TechnicalRequested, "No stale technical panel without result");
        state = MatrixPresentationPolicy.Resolve(input with { ToolsRequested = true, MoreToolsVisible = true, DetailsRequested = true });
        Check(state.Panel == MatrixPanel.Tools && !state.ShowSolution, "Tools win cell request");
        state = MatrixPresentationPolicy.Resolve(input with { DetailsRequested = true, TechnicalRequested = true, SelectionChanged = true });
        Check(!state.DetailsRequested && !state.TechnicalRequested && state.ShowSolution, "Matrix selection resets explicit details then projects C auto view");
        state = MatrixPresentationPolicy.Resolve(input with { DetailsDismissed = true });
        Check(state.Panel == MatrixPanel.None, "Dismissed C remains closed until selection changes");
        state = MatrixPresentationPolicy.Resolve(input with { DetailsDismissed = true, CellChanged = true });
        Check(state.Panel == MatrixPanel.Cell && !state.DetailsDismissed, "New cell restores auto detail intent");
        state = MatrixPresentationPolicy.Resolve(input with { SelectedMatrix = 0, DetailsRequested = true });
        Check(state.ValueOnly && !state.ShowSolution, "A/B inspector hugs one value");
    }
}
