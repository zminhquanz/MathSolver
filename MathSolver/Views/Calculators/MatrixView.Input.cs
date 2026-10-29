using MathSolver.Services.Core;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private string? _pendingRestoredCellText;
    private int _cellTextVersion;

    private void OnCellValueTextChanged(object? sender, TextChangedEventArgs e)
    {
        string text = e.NewTextValue ?? string.Empty;
        // Native text restoration can raise a delayed event after the guard is
        // released. Ignore that event so it cannot erase the range warning.
        if (_pendingRestoredCellText is { } restored)
        {
            _pendingRestoredCellText = null;
            if (text == restored) return;
        }
        int editVersion = ++_cellTextVersion;
        if (_constructed && !CanTouchUi) return;
        if (_updatingEditor || _busy || _selected == 2)
        {
            RefreshSelectedValue();
            return;
        }
        var error = MatrixCellInput.Validate(text);
        if (error != MatrixCellTextError.None)
        {
            string previous = e.OldTextValue ?? string.Empty;
            int caret = 0;
            while (caret < previous.Length && caret < text.Length && previous[caret] == text[caret]) caret++;
            _pendingRestoredCellText = previous;
            _updatingEditor = true;
            try
            {
                _value.Text = previous;
                _value.SelectionLength = 0;
                _value.CursorPosition = caret;
            }
            finally { _updatingEditor = false; }
            RefreshSelectedValue();
            ReportError(error == MatrixCellTextError.OutOfRange ? "CellInputOutOfRange" : "InvalidCell",
                "AB"[_selected], _viewport.SelectedRow + 1, _viewport.SelectedColumn + 1);
            int matrix = _selected, row = _viewport.SelectedRow, column = _viewport.SelectedColumn;
            // WinUI may reset SelectionStart after the TextChanged callback. Restore
            // the caret once native text mapping settles, only for this same edit.
            Dispatcher.Dispatch(() =>
            {
                if (editVersion != _cellTextVersion || !CanTouchUi || !_active || _busy
                    || !_viewport.IsEditing || _selected != matrix || _viewport.SelectedRow != row
                    || _viewport.SelectedColumn != column || _value.Text != previous) return;
                _value.SelectionLength = 0;
                _value.CursorPosition = Math.Clamp(caret, 0, previous.Length);
            });
            return;
        }
        RefreshSelectedValue();
        InvalidateResult();
    }
}
