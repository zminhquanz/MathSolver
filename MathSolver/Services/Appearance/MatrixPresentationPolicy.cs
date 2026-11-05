namespace MathSolver.Services;

internal enum MatrixPanel { None, Setup, Status, Coordinates, Tools, Technical, Cell }

/// <summary>Intent and data captured before changing any panel visibility.</summary>
internal readonly record struct MatrixPresentationInput
{
    internal bool HasInputs { get; init; }
    internal bool HasResult { get; init; }
    internal int SelectedMatrix { get; init; }
    internal bool SideBySide { get; init; }
    internal bool IsWindows { get; init; }
    internal bool SetupRequested { get; init; }
    internal bool DetailsRequested { get; init; }
    internal bool TechnicalRequested { get; init; }
    internal bool ToolsRequested { get; init; }
    internal bool DetailsDismissed { get; init; }
    internal bool TableExpanded { get; init; }
    internal bool Busy { get; init; }
    internal bool StatusDetailsAvailable { get; init; }
    internal bool MoreToolsVisible { get; init; }
    internal bool CoordinateVisible { get; init; }
    internal bool SelectionChanged { get; init; }
    internal bool CellChanged { get; init; }
}

internal readonly record struct MatrixPresentation(
    MatrixPanel Panel, bool DetailsRequested, bool TechnicalRequested,
    bool ToolsRequested, bool DetailsDismissed, bool TableExpanded,
    bool EditorEnabled, bool ShowSolution, bool ValueOnly)
{
    internal bool IsOpen => Panel != MatrixPanel.None;
    internal bool UsesTools => Panel is MatrixPanel.Status or MatrixPanel.Coordinates or MatrixPanel.Tools;
    internal bool ShowTechnical => Panel == MatrixPanel.Technical;
}

internal static class MatrixPresentationPolicy
{
    internal static MatrixPresentation Resolve(MatrixPresentationInput input)
    {
        bool details = input.DetailsRequested && !input.SelectionChanged;
        bool technical = input.TechnicalRequested && !input.SelectionChanged;
        bool tools = input.ToolsRequested && !input.SelectionChanged;
        bool dismissed = input.DetailsDismissed && !input.SelectionChanged && !input.CellChanged;
        bool status = tools && input.StatusDetailsAvailable && !input.MoreToolsVisible && !input.CoordinateVisible;
        if (input.Busy || !input.HasInputs)
        {
            details = technical = false;
            tools = status;
        }
        if (technical && !input.HasResult) details = technical = false;

        MatrixPanel panel = MatrixPanel.None;
        if (input.IsWindows && input.SetupRequested) panel = MatrixPanel.Setup;
        else if (status) panel = MatrixPanel.Status;
        else if (!input.Busy && input.HasInputs)
        {
            if (tools) panel = input.CoordinateVisible ? MatrixPanel.Coordinates : MatrixPanel.Tools;
            else if (details || (input.SideBySide && input.SelectedMatrix == 2 && input.HasResult && !dismissed))
                panel = technical ? MatrixPanel.Technical : MatrixPanel.Cell;
        }
        bool cell = panel == MatrixPanel.Cell;
        return new(panel, details, technical, tools, dismissed, input.HasInputs && input.TableExpanded,
            panel == MatrixPanel.None || input.SideBySide,
            cell && input.SelectedMatrix == 2 && input.HasResult, cell && input.SelectedMatrix != 2);
    }
}
