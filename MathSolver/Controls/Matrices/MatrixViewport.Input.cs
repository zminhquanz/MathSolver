using MathSolver.Services;

namespace MathSolver.Controls;

public sealed partial class MatrixViewport
{
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
#endif
    }
    private void DetachNativeInput()
    {
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
        var properties = e.GetCurrentPoint(_inputSurface).Properties;
        double amount = properties.MouseWheelDelta / 120d * _state.CellHeight * 3;
        bool moved = properties.IsHorizontalMouseWheel ? _state.ScrollBy(amount, 0)
            : KeyDown(global::Windows.System.VirtualKey.Shift) ? _state.ScrollBy(-amount, 0) : _state.ScrollBy(0, -amount);
        if (moved) { Scrolled(); e.Handled = true; }
    }
    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (!CanUseInput || _editor?.IsFocused != true || KeyDown(global::Windows.System.VirtualKey.Control)
            || KeyDown(global::Windows.System.VirtualKey.Menu)) return;
        bool shift = KeyDown(global::Windows.System.VirtualKey.Shift);
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
        if (move is { } direction) { e.Handled = true; Navigate(direction); }
        else if (e.Key == global::Windows.System.VirtualKey.Escape)
        { e.Handled = true; CancelInlineEdit(); }
    }
#endif
}
