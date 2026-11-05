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
        // Cached tabs can become inactive without unloading the native view.
        // End a floating long-press menu even when range-selection mode is off.
        CloseSelectionMenu();
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
