#if ANDROID
using Android.Views;
using MathSolver.Services;

namespace MathSolver.Controls;

public sealed partial class MatrixViewport
{
    private Android.Views.View? _contextSurface;
    private GestureDetector? _contextGestures;
    private MatrixLongPressListener? _contextListener;
    private MatrixMenuAction? _selectAllAction, _copyAction;

    private void AttachAndroidContextMenu()
    {
        DetachAndroidContextMenu();
        if (!CanUseInput || _canvas.Handler?.PlatformView is not Android.Views.View surface) return;
        _contextSurface = surface;
        _contextListener = new MatrixLongPressListener(this);
        _contextGestures = new GestureDetector(surface.Context, _contextListener);
        _selectAllAction = new MatrixMenuAction(() => { if (CanUseInput) SelectAll(); });
        _copyAction = new MatrixMenuAction(RequestCopy);
        surface.Touch += OnAndroidContextTouch;
        surface.ContextMenuCreated += OnAndroidContextMenuCreated;
    }

    private void DetachAndroidContextMenu()
    {
        if (_contextSurface is { } surface)
        {
            surface.Touch -= OnAndroidContextTouch;
            surface.ContextMenuCreated -= OnAndroidContextMenuCreated;
        }
        _contextSurface = null;
        if (_contextGestures is { } gestures)
        {
            // Remove the pending long-press callback before releasing the native listener.
            using var cancel = MotionEvent.Obtain(0, 0, MotionEventActions.Cancel, 0, 0, 0);
            if (cancel is not null) gestures.OnTouchEvent(cancel);
        }
        _contextGestures?.Dispose();
        _contextGestures = null;
        _contextListener?.Dispose();
        _contextListener = null;
        _selectAllAction?.Dispose();
        _copyAction?.Dispose();
        _selectAllAction = _copyAction = null;
    }

    private void OnAndroidContextTouch(object? sender, Android.Views.View.TouchEventArgs e)
    {
        if (e.Event is { } motion) _contextGestures?.OnTouchEvent(motion);
        // Observe the gesture without consuming MAUI's cell taps, scrolling or resizing.
        e.Handled = false;
    }

    private void OpenAndroidContextMenu(MotionEvent motion)
    {
        if (!CanUseInput || _dragged || _resizeInteraction || _barInteraction
            || _readCell is null || _contextSurface is not { } surface) return;
        float density = surface.Resources?.DisplayMetrics?.Density ?? 1;
        float x = motion.GetX() / density, y = motion.GetY() / density;
        if (!_state.TryHitCell(x, y, out int row, out int column)) return;

        // Releasing a long press must never turn into a second tap that opens the editor.
        _dragged = _panBlocked = true;
        SetParentPanLock(false);
        if (!FinishEdit()) return;
        if (!_state.IsSelected(row, column) && !Select(row, column)) return;
        FocusSelection();
        surface.PerformHapticFeedback(FeedbackConstants.LongPress);
        if (OperatingSystem.IsAndroidVersionAtLeast(24)) surface.ShowContextMenu(motion.GetX(), motion.GetY());
        else surface.ShowContextMenu();
    }

    private void OnAndroidContextMenuCreated(object? sender, Android.Views.View.CreateContextMenuEventArgs e)
    {
        if (!CanUseInput || _state.IsEditing || e.Menu is not { } menu) return;
        menu.Add(LocalizationService.TranslateKey("Matrix.SelectAll"))?.SetOnMenuItemClickListener(_selectAllAction);
        menu.Add(LocalizationService.TranslateKey("Matrix.Workspace.Copy"))?.SetOnMenuItemClickListener(_copyAction);
    }

    private sealed class MatrixLongPressListener(MatrixViewport owner) : GestureDetector.SimpleOnGestureListener
    {
        public override bool OnDown(MotionEvent e) => true;
        public override void OnLongPress(MotionEvent e) => owner.OpenAndroidContextMenu(e);
    }

    private sealed class MatrixMenuAction(Action action) : Java.Lang.Object, IMenuItemOnMenuItemClickListener
    {
        public bool OnMenuItemClick(IMenuItem item) { action(); return true; }
    }
}
#endif
