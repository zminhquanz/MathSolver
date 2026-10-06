#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NativeScrollViewer = Microsoft.UI.Xaml.Controls.ScrollViewer;
#endif

namespace MathSolver.Controls;

/// <summary>
/// A horizontal scroll view that also accepts left-mouse drags on Windows.
/// Touch, keyboard input and other platforms use the standard ScrollView behavior.
/// </summary>
public sealed class MouseDragScrollView : ScrollView
{
    /// <summary>
    /// Uses scrollbar-style mouse dragging: moving right increases the horizontal
    /// offset. The default keeps tab-strip content following the pointer.
    /// Native touch scrolling is unaffected.
    /// </summary>
    public bool ScrollInPointerDirection { get; set; }

    private bool _isUpdatingTabs;
    private double _lastTabViewportWidth = -1d;

    /// <summary>Fits one row when there is room, otherwise keeps its readable width for horizontal scrolling.</summary>
    public bool UpdateTabLayout(Grid tabs)
    {
        if (_isUpdatingTabs || Width <= 0d || tabs.Children.Count == 0)
            return false;
        _isUpdatingTabs = true;
        try
        {
            double available = Math.Max(1d, Width - Padding.HorizontalThickness);
            bool changed = Math.Abs(_lastTabViewportWidth - available) >= 0.5d;
            _lastTabViewportWidth = available;
            double minimum = 170d * MathSolver.Services.ResponsiveLayoutPolicy.TextScale;
            double[] widths = tabs.Children.Select(child => Math.Max(minimum,
                child.Measure(double.PositiveInfinity, double.PositiveInfinity).Width)).ToArray();
            double natural = widths.Sum() + tabs.ColumnSpacing * (widths.Length - 1);
            double extra = Math.Max(0d, available - natural) / widths.Length;

            if (tabs.RowDefinitions.Count != 1)
            {
                changed = true;
                tabs.RowDefinitions.Clear();
                tabs.RowDefinitions.Add(new RowDefinition(Microsoft.Maui.GridLength.Auto));
            }
            if (tabs.ColumnDefinitions.Count != widths.Length)
            {
                changed = true;
                tabs.ColumnDefinitions.Clear();
                for (int i = 0; i < widths.Length; i++)
                    tabs.ColumnDefinitions.Add(new ColumnDefinition());
            }
            for (int i = 0; i < widths.Length; i++)
            {
                double width = widths[i] + extra;
                Microsoft.Maui.GridLength current = tabs.ColumnDefinitions[i].Width;
                if (!current.IsAbsolute || Math.Abs(current.Value - width) >= 0.5d)
                {
                    changed = true;
                    tabs.ColumnDefinitions[i].Width = new Microsoft.Maui.GridLength(width);
                }
                tabs.SetRow(tabs.Children[i], 0);
                tabs.SetColumn(tabs.Children[i], i);
            }
            double contentWidth = Math.Max(available, natural);
            if (Math.Abs(tabs.WidthRequest - contentWidth) >= 0.5d)
            {
                changed = true;
                tabs.WidthRequest = contentWidth;
            }
            return changed;
        }
        finally
        {
            _isUpdatingTabs = false;
        }
    }

#if WINDOWS
    private const double DragThreshold = 6d;
    private NativeScrollViewer? _viewer;
    private Pointer? _pointer;
    private double _pressX;
    private double _pressOffset;
    private bool _dragging;
    private bool _transferringCapture;

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        Detach();
        base.OnHandlerChanging(args);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler?.PlatformView is not NativeScrollViewer viewer)
            return;

        _viewer = viewer;
        // Observe events even when the native tab Button has handled them.
        viewer.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPressed), true);
        viewer.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnMoved), true);
        viewer.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnReleased), true);
        viewer.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnCanceled), true);
        viewer.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnCaptureLost), true);
        viewer.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler(OnExited), true);
        viewer.Unloaded += OnUnloaded;
    }

    private void OnPressed(object sender, PointerRoutedEventArgs args)
    {
        if (_viewer is null || _pointer is not null || _viewer.ScrollableWidth <= 0 ||
            args.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            return;

        // Native scrollbars and inline editors own their pointer gestures.
        // Capturing an editor drag here would interfere with text selection.
        for (DependencyObject? element = args.OriginalSource as DependencyObject;
             element is not null && !ReferenceEquals(element, _viewer);
             element = VisualTreeHelper.GetParent(element))
            if (element is Microsoft.UI.Xaml.Controls.Primitives.ScrollBar
                or Microsoft.UI.Xaml.Controls.TextBox or Microsoft.UI.Xaml.Controls.PasswordBox
                or Microsoft.UI.Xaml.Controls.RichEditBox or Microsoft.UI.Xaml.Controls.CheckBox)
                return;

        var point = args.GetCurrentPoint(_viewer);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _pointer = args.Pointer;
        _pressX = point.Position.X;
        _pressOffset = _viewer.HorizontalOffset;
        // Do not consume the press: an ordinary click must still select the tab.
    }

    private void OnMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_viewer is null || _pointer?.PointerId != args.Pointer.PointerId)
            return;

        var point = args.GetCurrentPoint(_viewer);
        if (!point.Properties.IsLeftButtonPressed)
        {
            Reset();
            return;
        }

        double delta = point.Position.X - _pressX;
        if (!_dragging)
        {
            if (Math.Abs(delta) < DragThreshold)
                return;

            _transferringCapture = true;
            try
            {
                // A native Button may already own this pointer. Release that
                // capture to cancel its pending click before capturing the drag.
                for (DependencyObject? element = args.OriginalSource as DependencyObject;
                     element is not null && !ReferenceEquals(element, _viewer);
                     element = VisualTreeHelper.GetParent(element))
                {
                    if (element is UIElement control &&
                        control.PointerCaptures?.Any(p => p.PointerId == args.Pointer.PointerId) == true)
                    {
                        control.ReleasePointerCapture(args.Pointer);
                    }
                }

                _dragging = _viewer.CapturePointer(args.Pointer);
            }
            finally
            {
                _transferringCapture = false;
            }

            if (!_dragging)
            {
                Reset();
                return;
            }
        }

        _viewer.ChangeView(
            Math.Clamp(_pressOffset + (ScrollInPointerDirection ? delta : -delta), 0d, _viewer.ScrollableWidth),
            null, null, disableAnimation: true);
        args.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_pointer?.PointerId != args.Pointer.PointerId)
            return;

        if (_dragging)
            args.Handled = true;
        Reset();
    }

    private void OnCanceled(object sender, PointerRoutedEventArgs args)
    {
        if (!_transferringCapture && _pointer?.PointerId == args.Pointer.PointerId)
            Reset();
    }

    private void OnCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        if (_transferringCapture || _pointer?.PointerId != args.Pointer.PointerId)
            return;

        // The child Button's capture loss can bubble through the scroll viewer.
        if (!_dragging || ReferenceEquals(args.OriginalSource, _viewer))
            Reset();
    }

    private void OnExited(object sender, PointerRoutedEventArgs args)
    {
        if (_viewer is null || _dragging || _pointer?.PointerId != args.Pointer.PointerId)
            return;

        var point = args.GetCurrentPoint(_viewer);
        // A press in an empty gap has no capture until dragging starts. Forget
        // it when leaving the strip so a release elsewhere cannot leave it stuck.
        if (point.Position.X < 0 || point.Position.X > _viewer.ActualWidth ||
            point.Position.Y < 0 || point.Position.Y > _viewer.ActualHeight)
            Reset();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => Reset();

    private void Reset()
    {
        Pointer? captured = _dragging ? _pointer : null;
        _pointer = null;
        _dragging = false;
        if (captured is not null)
            _viewer?.ReleasePointerCapture(captured);
    }

    private void Detach()
    {
        Reset();
        if (_viewer is not { } viewer)
            return;

        viewer.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPressed));
        viewer.RemoveHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnMoved));
        viewer.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnReleased));
        viewer.RemoveHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnCanceled));
        viewer.RemoveHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnCaptureLost));
        viewer.RemoveHandler(UIElement.PointerExitedEvent, new PointerEventHandler(OnExited));
        viewer.Unloaded -= OnUnloaded;
        _viewer = null;
    }
#endif
}
