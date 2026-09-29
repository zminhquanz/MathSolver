using Android.Views;
using AndroidEditText = Android.Widget.EditText;

namespace MathSolver.Controls;

/// <summary>
/// Observes horizontal swipes within a sub-tab content area without taking
/// touch events away from Android's scroll views and input controls.
/// </summary>
internal sealed class AndroidSubTabSwipeNavigator(
    VisualElement contentHost,
    Func<int> selectedIndex,
    int lastIndex,
    Func<int, Task> switchToIndexAsync)
{
    private bool _isTracking;
    private float _startX;
    private float _startY;

    public void Attach()
    {
        MainActivity.TouchDispatched -= OnTouchDispatched;
        MainActivity.TouchDispatched += OnTouchDispatched;
    }

    public void Detach()
    {
        MainActivity.TouchDispatched -= OnTouchDispatched;
        _isTracking = false;
    }

    private void OnTouchDispatched(MotionEvent e)
    {
        float x = e.GetX();
        float y = e.GetY();

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _isTracking =
                    IsInsideContent(x, y) &&
                    !IsEditableOrHorizontallyScrollableAt(
                        Platform.CurrentActivity?.Window?.DecorView,
                        x,
                        y);
                _startX = x;
                _startY = y;
                break;

            case MotionEventActions.Move when _isTracking:
                float moveX = Math.Abs(x - _startX);
                float moveY = Math.Abs(y - _startY);
                if (moveY > 48 * DeviceDisplay.MainDisplayInfo.Density &&
                    moveY > moveX)
                {
                    _isTracking = false;
                }
                break;

            case MotionEventActions.Up:
                if (_isTracking && IsInsideContent(x, y))
                {
                    float deltaX = x - _startX;
                    float deltaY = y - _startY;
                    float minimumDistance =
                        80 * (float)DeviceDisplay.MainDisplayInfo.Density;

                    if (Math.Abs(deltaX) >= minimumDistance &&
                        Math.Abs(deltaX) > Math.Abs(deltaY) * 1.5f)
                    {
                        int nextIndex = selectedIndex() +
                            (deltaX < 0 ? 1 : -1);

                        if (nextIndex >= 0 && nextIndex <= lastIndex)
                        {
                            _ = switchToIndexAsync(nextIndex);
                        }
                    }
                }

                _isTracking = false;
                break;

            case MotionEventActions.Cancel:
            case MotionEventActions.PointerDown:
                _isTracking = false;
                break;
        }
    }

    private bool IsInsideContent(float x, float y)
    {
        if (contentHost.Handler?.PlatformView
                is not Android.Views.View nativeHost)
        {
            return false;
        }

        int[] location = new int[2];
        nativeHost.GetLocationInWindow(location);
        return x >= location[0] &&
               x < location[0] + nativeHost.Width &&
               y >= location[1] &&
               y < location[1] + nativeHost.Height;
    }

    private static bool IsEditableOrHorizontallyScrollableAt(
        Android.Views.View? view,
        float x,
        float y)
    {
        if (view is null || view.Visibility != ViewStates.Visible)
        {
            return false;
        }

        int[] location = new int[2];
        view.GetLocationInWindow(location);
        if (x < location[0] || x >= location[0] + view.Width ||
            y < location[1] || y >= location[1] + view.Height)
        {
            return false;
        }

        if (view is AndroidEditText ||
            view.CanScrollHorizontally(-1) ||
            view.CanScrollHorizontally(1))
        {
            return true;
        }

        if (view is ViewGroup group)
        {
            for (int index = group.ChildCount - 1; index >= 0; index--)
            {
                if (IsEditableOrHorizontallyScrollableAt(
                        group.GetChildAt(index), x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
