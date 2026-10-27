using MathSolver.Services;

namespace MathSolver.Controls;

public sealed partial class MatrixViewport
{
    private static bool IsRangeExtensionPressed() =>
#if WINDOWS
        KeyDown(global::Windows.System.VirtualKey.Shift);
#else
        false;
#endif
    private void FocusSelection()
    {
        if (!CanUseInput) return;
        int generation = ++_focusGeneration;
        Dispatcher.Dispatch(() =>
        {
            if (generation != _focusGeneration || !CanUseInput || _state.IsEditing) return;
            _editor?.Unfocus();
            _canvas.Focus();
#if WINDOWS
            if (_canvas.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control control)
            {
                control.IsTabStop = true;
                control.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
#endif
        });
    }
    private void SetParentPanLock(bool locked)
    {
#if ANDROID
        if (_canvas.Handler?.PlatformView is Android.Views.View native)
            native.Parent?.RequestDisallowInterceptTouchEvent(locked);
#endif
    }
    private void AttachNativeInput()
    {
#if WINDOWS
        DetachNativeInput();
        if (!CanUseInput || Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement surface) return;
        _inputSurface = surface;
        surface.AddHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnWheel), true);
        surface.AddHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnKeyDown), true);
        AttachPointerInput();
#endif
    }
    private void DetachNativeInput()
    {
        CancelPointerGesture();
        DetachPointerInput();
#if WINDOWS
        if (_inputSurface is not { } surface) return;
        _inputSurface = null;
        try
        {
            surface.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnWheel));
            surface.RemoveHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnKeyDown));
        }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine($"Matrix input surface disconnected: {error.Message}"); }
#endif
    }

#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _inputSurface;
    private static bool KeyDown(global::Windows.System.VirtualKey key) =>
        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

    private void OnWheel(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!CanUseInput) return;
        bool wasEditing = _state.IsEditing;
        if (!_state.EndEdit(CommitEdit)) { RestoreInvalidEditor(); e.Handled = true; return; }
        var properties = e.GetCurrentPoint(_inputSurface).Properties;
        double amount = properties.MouseWheelDelta / 120d * _state.CellHeight * 3;
        bool moved = properties.IsHorizontalMouseWheel ? _state.ScrollBy(amount, 0)
            : KeyDown(global::Windows.System.VirtualKey.Shift) ? _state.ScrollBy(-amount, 0) : _state.ScrollBy(0, -amount);
        if (moved) { Scrolled(); e.Handled = true; }
        else Refresh();
        if (wasEditing) FocusSelection();
    }
    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (!CanUseInput || KeyDown(global::Windows.System.VirtualKey.Menu)
            || KeyDown(global::Windows.System.VirtualKey.LeftWindows) || KeyDown(global::Windows.System.VirtualKey.RightWindows)) return;
        if (KeyDown(global::Windows.System.VirtualKey.Control))
        {
            // Let the native TextBox own Ctrl+A/C/V while editing its text.
            if (_state.IsEditing) return;
            if (e.Key == global::Windows.System.VirtualKey.A) { e.Handled = true; SelectAll(); }
            else if (e.Key == global::Windows.System.VirtualKey.C)
            { e.Handled = true; RequestCopy(); }
            else if (e.Key == global::Windows.System.VirtualKey.V)
            { e.Handled = true; RequestPaste(); }
            return;
        }
        if (!_state.IsEditing && e.Key == global::Windows.System.VirtualKey.Delete)
        { e.Handled = true; RequestDelete(); return; }
        bool shift = KeyDown(global::Windows.System.VirtualKey.Shift);
        if (MatrixTypingInput.TryGetText((int)e.Key, shift, false, out string text) && TypeIntoSelectedCell(text))
        { e.Handled = true; return; }
        if (!_state.IsEditing && e.Key is global::Windows.System.VirtualKey.F2 or global::Windows.System.VirtualKey.Enter)
        { e.Handled = true; BeginInlineEdit(); return; }
        // While editing, arrow keys belong to the native text caret.
        if (_state.IsEditing && e.Key is global::Windows.System.VirtualKey.Left or global::Windows.System.VirtualKey.Right
            or global::Windows.System.VirtualKey.Up or global::Windows.System.VirtualKey.Down) return;
        MatrixCellMove? move = e.Key switch
        {
            global::Windows.System.VirtualKey.Left => MatrixCellMove.Left,
            global::Windows.System.VirtualKey.Right => MatrixCellMove.Right,
            global::Windows.System.VirtualKey.Up => MatrixCellMove.Up,
            global::Windows.System.VirtualKey.Down => MatrixCellMove.Down,
            global::Windows.System.VirtualKey.Enter => shift ? MatrixCellMove.Up : MatrixCellMove.Down,
            global::Windows.System.VirtualKey.Tab => shift ? MatrixCellMove.Previous : MatrixCellMove.Next,
            _ => null
        };
        if (move is { } direction)
        {
            e.Handled = true;
            if (shift && !_state.IsEditing && e.Key is global::Windows.System.VirtualKey.Left or global::Windows.System.VirtualKey.Right
                or global::Windows.System.VirtualKey.Up or global::Windows.System.VirtualKey.Down)
            {
                // Keep the moving endpoint, including a reverse selection.
                int endRow = _state.SelectionEndRow, endColumn = _state.SelectionEndColumn;
                ExtendSelection(endRow + (direction == MatrixCellMove.Down ? 1 : direction == MatrixCellMove.Up ? -1 : 0),
                    endColumn + (direction == MatrixCellMove.Right ? 1 : direction == MatrixCellMove.Left ? -1 : 0));
            }
            else Navigate(direction);
        }
        else if (e.Key == global::Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            if (_state.IsRangeSelectionMode) CancelRangeSelection();
            else if (_state.HasRangeSelection) Select(SelectedRow, SelectedColumn);
            else CancelInlineEdit();
        }
    }
#endif
}
