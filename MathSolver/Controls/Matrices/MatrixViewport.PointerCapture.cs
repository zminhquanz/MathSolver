namespace MathSolver.Controls;

public sealed partial class MatrixViewport
{
    private void CancelPointerGesture()
    {
        _resizeAxis = MatrixResizeAxis.None; _scrollAxis = ScrollAxis.None;
        _resizeInteraction = _barInteraction = false;
        // Ignore remaining moves/releases from the old source until a fresh press.
        _panBlocked = _dragged = true;
        SetParentPanLock(false);
#if WINDOWS
        var pointer = _dragPointer;
        _dragPointer = null;
        try
        {
            if (pointer is not null) _pointerCanvas?.ReleasePointerCapture(pointer);
        }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine($"Matrix pointer gesture disconnected: {error.Message}"); }
#endif
    }

    private bool HasPointerCapture =>
#if WINDOWS
        _dragPointer is not null;
#else
        false;
#endif

    private void AttachPointerInput()
    {
#if WINDOWS
        if (_canvas.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement canvas) return;
        _pointerCanvas = canvas;
        canvas.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerPressed), true);
        canvas.AddHandler(Microsoft.UI.Xaml.UIElement.PointerMovedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerMoved), true);
        canvas.AddHandler(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerReleased), true);
        canvas.AddHandler(Microsoft.UI.Xaml.UIElement.PointerCanceledEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerCanceled), true);
        canvas.AddHandler(Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerCaptureLost), true);
#endif
    }

    private void DetachPointerInput()
    {
#if WINDOWS
        if (_pointerCanvas is not { } canvas) return;
        var pointer = _dragPointer;
        _pointerCanvas = null; _dragPointer = null; _scrollAxis = ScrollAxis.None; _resizeAxis = MatrixResizeAxis.None;
        try
        {
            canvas.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerPressed));
            canvas.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerMovedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerMoved));
            canvas.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerReleased));
            canvas.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerCanceledEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerCanceled));
            canvas.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnScrollPointerCaptureLost));
            if (pointer is not null) canvas.ReleasePointerCapture(pointer);
        }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine($"Matrix scrollbar surface disconnected: {error.Message}"); }
#endif
    }

#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _pointerCanvas;
    private Microsoft.UI.Xaml.Input.Pointer? _dragPointer;

    private void OnScrollPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // MAUI has already dispatched StartInteraction before this bubbling handler.
        // Capture thumb and header-size drags; cell text selection stays native.
        if (!CanUseInput || _dragPointer is not null || (_scrollAxis == ScrollAxis.None && _resizeAxis == MatrixResizeAxis.None)
            || _pointerCanvas is not { } canvas) return;
        if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse
            && !e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed) return;
        if (!canvas.CapturePointer(e.Pointer)) return;
        _dragPointer = e.Pointer;
        e.Handled = true;
    }

    private void OnScrollPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!CanUseInput || _dragPointer?.PointerId != e.Pointer.PointerId) return;
        var point = e.GetCurrentPoint(_pointerCanvas).Position;
        // Native GraphicsView ends its own drag on PointerExited. Continue using
        // the captured pointer so a thumb still tracks outside the viewport.
        DragPointer(new((float)point.X, (float)point.Y));
        e.Handled = true;
    }

    private void OnScrollPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        EndScrollPointer(e, updatePosition: true, release: true);
    private void OnScrollPointerCanceled(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        EndScrollPointer(e, updatePosition: false, release: true);
    private void OnScrollPointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        EndScrollPointer(e, updatePosition: false, release: false);

    private void EndScrollPointer(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e, bool updatePosition, bool release)
    {
        if (_dragPointer?.PointerId != e.Pointer.PointerId) return;
        var canvas = _pointerCanvas;
        if (CanUseInput && updatePosition)
        {
            var point = e.GetCurrentPoint(canvas).Position;
            DragPointer(new((float)point.X, (float)point.Y));
        }
        _dragPointer = null; _scrollAxis = ScrollAxis.None; _resizeAxis = MatrixResizeAxis.None;
        if (CanUseInput) Refresh();
        SetParentPanLock(false);
        try
        {
            if (release) canvas?.ReleasePointerCapture(e.Pointer);
        }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine($"Matrix scrollbar capture disconnected: {error.Message}"); }
        if (CanUseInput && !_state.IsEditing) FocusSelection();
        e.Handled = true;
    }
#endif
}
