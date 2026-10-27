namespace MathSolver.Controls;

public sealed partial class MatrixViewport
{
    public bool IsRangeSelectionMode => _state.IsRangeSelectionMode;
    public bool IsAwaitingRangeEnd => _state.IsAwaitingRangeEnd;
    public event Action? RangeSelectionModeChanged;

    public bool BeginRangeSelection()
    {
        if (!CanUseInput) return false;
        if (!_state.BeginRangeSelection(CommitEdit)) return RestoreInvalidEditor();
        CancelPointerGesture();
        Refresh(); FocusSelection();
        RangeSelectionModeChanged?.Invoke();
        return true;
    }

    public void CancelRangeSelection()
    {
        if (!_state.IsRangeSelectionMode) return;
        _state.CancelRangeSelection();
        RangeSelectionModeChanged?.Invoke();
    }

    private void PublishSelectionChanged()
    {
        CellSelected?.Invoke(SelectedRow, SelectedColumn);
        RangeSelectionModeChanged?.Invoke();
    }
}
