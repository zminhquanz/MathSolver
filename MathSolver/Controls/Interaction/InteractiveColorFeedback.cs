using System.Runtime.CompilerServices;
using MathSolver.Services;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace MathSolver.Controls;

/// <summary>
/// Native interaction feedback leaves MAUI colors, DynamicResource bindings,
/// selection and answer states intact. Opt in tappable menu rows via IsEnabled.
/// </summary>
public static class InteractiveColorFeedback
{
    private static readonly ConditionalWeakTable<View, FeedbackState> States = new();

    public static readonly BindableProperty IsEnabledProperty = BindableProperty.CreateAttached(
        "IsEnabled", typeof(bool), typeof(InteractiveColorFeedback), false,
        propertyChanged: static (bindable, _, _) =>
        {
            if (bindable is View view) Refresh(view);
        });

    public static bool GetIsEnabled(BindableObject view) => (bool)view.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(BindableObject view, bool value) => view.SetValue(IsEnabledProperty, value);

    internal static void Configure()
    {
        ButtonHandler.Mapper.AppendToMapping("InteractiveColorFeedback", static (_, view) => Refresh(view));
        ButtonHandler.Mapper.AppendToMapping(nameof(IView.Background), static (_, view) => Refresh(view));
        ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.TextColor), static (_, view) => Refresh(view));
        ButtonHandler.Mapper.AppendToMapping(nameof(IButtonStroke.StrokeColor), static (_, view) => Refresh(view));
        ButtonHandler.Mapper.AppendToMapping(nameof(IButtonStroke.StrokeThickness), static (_, view) => Refresh(view));
        ButtonHandler.Mapper.AppendToMapping(nameof(IView.IsEnabled), static (_, view) => Refresh(view));
        ImageButtonHandler.Mapper.AppendToMapping("InteractiveColorFeedback", static (_, view) => Refresh(view));
        ImageButtonHandler.Mapper.AppendToMapping(nameof(IView.Background), static (_, view) => Refresh(view));
        ImageButtonHandler.Mapper.AppendToMapping(nameof(IButtonStroke.StrokeColor), static (_, view) => Refresh(view));
        ImageButtonHandler.Mapper.AppendToMapping(nameof(IButtonStroke.StrokeThickness), static (_, view) => Refresh(view));
        ImageButtonHandler.Mapper.AppendToMapping(nameof(IView.IsEnabled), static (_, view) => Refresh(view));
        BorderHandler.Mapper.AppendToMapping("InteractiveColorFeedback", static (_, view) => Refresh(view));
        BorderHandler.Mapper.AppendToMapping(nameof(IView.Background), static (_, view) => Refresh(view));
        BorderHandler.Mapper.AppendToMapping(nameof(IView.IsEnabled), static (_, view) => Refresh(view));
    }

    private static void Refresh(IElement element)
    {
        if (element is not View view || view is not (Button or ImageButton) && !GetIsEnabled(view)) return;
        States.GetValue(view, static target => new FeedbackState(target)).Refresh();
    }

    private sealed class FeedbackState
    {
        private readonly View _view;
        private object? _native;
#if WINDOWS
        private bool _hovered;
        private bool _pressed;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _brush;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _pointerOverBrush;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _pressedBrush;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _foregroundPointerOverBrush;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _foregroundPressedBrush;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _borderBrush;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _borderPointerOverBrush;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _borderPressedBrush;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _focusPrimaryBrush;
        private Microsoft.UI.Xaml.Media.SolidColorBrush? _focusSecondaryBrush;
        private Microsoft.UI.Xaml.Media.Animation.Storyboard? _animation;
#elif ANDROID
        private global::Android.Views.View? _androidInput;
#endif

        internal FeedbackState(View view)
        {
            _view = view;
            view.Loaded += (_, _) => Refresh();
            view.Unloaded += (_, _) => Detach();
            view.HandlerChanging += (_, _) => Detach();
        }

        private Color Foreground => (_view as Button)?.TextColor ?? ResourceColor("TextPrimaryColor", Colors.Black);
        private static Color ResourceColor(string key, Color fallback) =>
            Application.Current?.Resources.TryGetValue(key, out object value) == true && value is Color color ? color : fallback;
        private Color Background => (_view.Background as SolidColorBrush)?.Color ?? _view.BackgroundColor ?? Colors.Transparent;
        private Color Highlight(bool pressed) => InteractionColorPalette.Highlight(Background, Foreground,
            ResourceColor("PrimaryColor", Colors.Gray), AppThemeManager.IsDarkThemeEffective, pressed);

        internal void Refresh()
        {
            object? platform = _view.Handler?.PlatformView;
            if (platform is null) return;
            if (!ReferenceEquals(platform, _native))
            {
                Detach();
                _native = platform;
#if WINDOWS
                if (platform is Microsoft.UI.Xaml.UIElement element)
                {
                    // Listen without consuming input: button clicks and menu taps
                    // continue through their original MAUI handlers.
                    element.PointerEntered += OnEntered;
                    element.PointerExited += OnExited;
                    element.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
                        new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPressed), true);
                    element.AddHandler(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent,
                        new Microsoft.UI.Xaml.Input.PointerEventHandler(OnReleased), true);
                    element.PointerCanceled += OnCanceled;
                    element.PointerCaptureLost += OnCaptureLost;
                }
#endif
                AppThemeManager.ThemeChanged += OnThemeChanged;
            }
#if WINDOWS
            if (!_view.IsEnabled) _hovered = _pressed = false;
            UpdateWindowsColors();
#elif ANDROID
            ConnectAndroidMenuInput();
            UpdateAndroidRipple();
#endif
        }

        private void OnThemeChanged(object? sender, EventArgs args) => Refresh();

        private void Detach()
        {
            AppThemeManager.ThemeChanged -= OnThemeChanged;
#if WINDOWS
            _animation?.Stop();
            _animation = null;
            if (_native is Microsoft.UI.Xaml.UIElement element)
            {
                element.PointerEntered -= OnEntered;
                element.PointerExited -= OnExited;
                element.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
                    new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPressed));
                element.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent,
                    new Microsoft.UI.Xaml.Input.PointerEventHandler(OnReleased));
                element.PointerCanceled -= OnCanceled;
                element.PointerCaptureLost -= OnCaptureLost;
            }
            _brush = null;
            _pointerOverBrush = null;
            _pressedBrush = null;
            _foregroundPointerOverBrush = _foregroundPressedBrush = null;
            _borderBrush = null;
            _borderPointerOverBrush = _borderPressedBrush = null;
            _focusPrimaryBrush = _focusSecondaryBrush = null;
#elif ANDROID
            if (_androidInput is { } input)
            {
                input.Touch -= OnTouch;
                input.Hover -= OnHover;
            }
            _androidInput = null;
#endif
            _native = null;
#if WINDOWS
            _hovered = _pressed = false;
#endif
        }

#if WINDOWS
        private static global::Windows.UI.Color NativeColor(Color color)
        {
            static byte Channel(float value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);
            return global::Windows.UI.Color.FromArgb(Channel(color.Alpha), Channel(color.Red), Channel(color.Green), Channel(color.Blue));
        }
        private void OnEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            if (args.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch || !_view.IsEnabled) return;
            _hovered = true;
            AnimateWindows();
        }
        private void OnExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        { _hovered = false; _pressed = false; AnimateWindows(); }
        private void OnPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        { if (_view.IsEnabled) { _pressed = true; AnimateWindows(); } }
        private void OnReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        { _pressed = false; AnimateWindows(); }
        private void OnCanceled(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        { _hovered = _pressed = false; AnimateWindows(); }
        private void OnCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        { _pressed = false; AnimateWindows(); }

        private void UpdateWindowsColors()
        {
            if (_native is Microsoft.UI.Xaml.Controls.Control focusControl)
            {
                // System focus visuals appear for keyboard navigation, independently
                // of hover/selection. Two outlines stay visible on light and dark fills.
                focusControl.UseSystemFocusVisuals = true;
                _focusPrimaryBrush ??= new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
                _focusSecondaryBrush ??= new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                focusControl.FocusVisualPrimaryBrush = _focusPrimaryBrush;
                focusControl.FocusVisualSecondaryBrush = _focusSecondaryBrush;
                focusControl.FocusVisualPrimaryThickness = new Microsoft.UI.Xaml.Thickness(2);
                focusControl.FocusVisualSecondaryThickness = new Microsoft.UI.Xaml.Thickness(1);
            }
            // Gradient backgrounds retain their original native rendering.
            if (_view.Background is GradientBrush)
            { _animation?.Stop(); _animation = null; _brush = null; return; }
            _animation?.Stop();
            _animation = null;
            _brush ??= new Microsoft.UI.Xaml.Media.SolidColorBrush();
            _brush.Color = NativeColor(CurrentColor());
            if (_native is Microsoft.UI.Xaml.Controls.Control control)
            {
                control.Background = _brush;
                // WinUI's native template switches these resources on hover/press.
                // Keep separate brushes and update them in place, synchronized
                // with Background so template transitions retain the animation.
                UpdateResourceBrush(control, "ButtonBackgroundPointerOver", ref _pointerOverBrush, _brush.Color);
                UpdateResourceBrush(control, "ButtonBackgroundPressed", ref _pressedBrush, _brush.Color);
                var foreground = NativeColor(Foreground);
                UpdateResourceBrush(control, "ButtonForegroundPointerOver", ref _foregroundPointerOverBrush, foreground);
                UpdateResourceBrush(control, "ButtonForegroundPressed", ref _foregroundPressedBrush, foreground);
                if (_view is IButtonStroke stroke)
                {
                    // MAUI maps the stroke to theme resources. Set the native
                    // properties too so the first template render has the same
                    // border as later selections, before any pointer interaction.
                    if (stroke.StrokeThickness >= 0)
                        control.BorderThickness = new Microsoft.UI.Xaml.Thickness(stroke.StrokeThickness);
                    if (stroke.StrokeColor is { } border)
                    {
                        _borderBrush ??= new Microsoft.UI.Xaml.Media.SolidColorBrush();
                        _borderBrush.Color = NativeColor(border);
                        control.BorderBrush = _borderBrush;
                        UpdateResourceBrush(control, "ButtonBorderBrushPointerOver", ref _borderPointerOverBrush, _borderBrush.Color);
                        UpdateResourceBrush(control, "ButtonBorderBrushPressed", ref _borderPressedBrush, _borderBrush.Color);
                    }
                    else
                    {
                        control.ClearValue(Microsoft.UI.Xaml.Controls.Control.BorderBrushProperty);
                        _borderBrush = null;
                    }
                }
            }
            else if (_native is Microsoft.UI.Xaml.Controls.Panel panel) panel.Background = _brush;
        }
        private static void UpdateResourceBrush(Microsoft.UI.Xaml.Controls.Control control, string key,
            ref Microsoft.UI.Xaml.Media.SolidColorBrush? brush, global::Windows.UI.Color color)
        {
            brush ??= new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
            if (!control.Resources.TryGetValue(key, out object current) || !ReferenceEquals(current, brush))
            {
                // MAUI may have replaced this key during another property map.
                // Remove the old entry before inserting our owned brush; WinUI
                // can reject replacing an existing native resource directly.
                control.Resources.Remove(key);
                control.Resources[key] = brush;
            }
            brush.Color = color;
        }
        private Color CurrentColor() => _view.IsEnabled && (_hovered || _pressed) ? Highlight(_pressed) : Background;
        private void AnimateWindows()
        {
            if (_brush is null || _native is null || _view.Handler?.PlatformView != _native) return;
            var from = _brush.Color;
            var to = NativeColor(CurrentColor());
            _animation?.Stop();
            _brush.Color = from;
            if (!new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled || !_view.IsEnabled)
            {
                _brush.Color = to;
                if (_pointerOverBrush is not null) _pointerOverBrush.Color = to;
                if (_pressedBrush is not null) _pressedBrush.Color = to;
                return;
            }
            _animation = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            AddColorAnimation(_brush, from, to);
            if (_pointerOverBrush is not null) AddColorAnimation(_pointerOverBrush, from, to);
            if (_pressedBrush is not null) AddColorAnimation(_pressedBrush, from, to);
            _animation.Begin();
        }

        private void AddColorAnimation(Microsoft.UI.Xaml.Media.SolidColorBrush brush,
            global::Windows.UI.Color from, global::Windows.UI.Color to)
        {
            brush.Color = from;
            var animation = new Microsoft.UI.Xaml.Media.Animation.ColorAnimation
            {
                From = from, To = to, Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(120)),
                EnableDependentAnimation = true
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, brush);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "Color");
            _animation!.Children.Add(animation);
        }
#elif ANDROID
        private void ConnectAndroidMenuInput()
        {
            if (_view is not Border border) return;
            // Menu taps are registered on the content Grid. Observe that same
            // input surface and mirror only its drawable state to the row layer.
            var input = border.Content?.Handler?.PlatformView as global::Android.Views.View;
            if (ReferenceEquals(input, _androidInput)) return;
            if (_androidInput is { } previous)
            { previous.Touch -= OnTouch; previous.Hover -= OnHover; }
            _androidInput = input;
            if (input is not null)
            { input.Touch += OnTouch; input.Hover += OnHover; }
        }
        private void OnTouch(object? sender, global::Android.Views.View.TouchEventArgs args)
        {
            if (_native is not global::Android.Views.View element || args.Event is not { } motion) return;
            if (motion.ActionMasked == global::Android.Views.MotionEventActions.Down && _view.IsEnabled)
            {
                UpdateAndroidRipple();
                element.DrawableHotspotChanged(motion.GetX() + (_androidInput?.Left ?? 0), motion.GetY() + (_androidInput?.Top ?? 0));
                element.Pressed = true;
            }
            else if (motion.ActionMasked is global::Android.Views.MotionEventActions.Up or global::Android.Views.MotionEventActions.Cancel)
                element.Pressed = false;
            else if (motion.ActionMasked == global::Android.Views.MotionEventActions.Move && _androidInput is { } input &&
                (motion.GetX() < 0 || motion.GetY() < 0 || motion.GetX() > input.Width || motion.GetY() > input.Height))
                element.Pressed = false;
            // Do not set Handled: preserve MAUI's tap/scroll gesture decision.
        }
        private void OnHover(object? sender, global::Android.Views.View.HoverEventArgs args)
        {
            if (_native is not global::Android.Views.View element) return;
            if (args.Event?.ActionMasked == global::Android.Views.MotionEventActions.HoverEnter && _view.IsEnabled)
            { UpdateAndroidRipple(); element.Hovered = true; }
            else if (args.Event?.ActionMasked == global::Android.Views.MotionEventActions.HoverExit)
                element.Hovered = false;
        }
        private void UpdateAndroidRipple()
        {
            if (_native is not global::Android.Views.View element) return;
            if (!_view.IsEnabled) { element.Pressed = false; element.Hovered = false; }
            Color Layer(bool pressed) => InteractionColorPalette.StateLayer(Background, Foreground,
                ResourceColor("PrimaryColor", Colors.Gray), AppThemeManager.IsDarkThemeEffective, pressed);
            int hover = Layer(false).ToPlatform().ToArgb(), pressed = Layer(true).ToPlatform().ToArgb();
            using var colors = new global::Android.Content.Res.ColorStateList(
                [[-global::Android.Resource.Attribute.StateEnabled], [global::Android.Resource.Attribute.StatePressed],
                 [global::Android.Resource.Attribute.StateHovered], [global::Android.Resource.Attribute.StateFocused], []],
                // RippleDrawable only draws active/exiting ripples. A nonzero
                // default preserves the release fade after StatePressed clears.
                [0, pressed, hover, hover, pressed]);
            if (element.Background is global::Android.Graphics.Drawables.RippleDrawable ripple)
            {
                // Keep MAUI's rounded mask, stroke, tint and Material touch animation.
                ripple.SetColor(colors);
            }
            else if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                // Image buttons/menu rows use a foreground layer so their original
                // drawable and the MAUI resource binding are never replaced.
                if (element.Foreground is global::Android.Graphics.Drawables.RippleDrawable foreground)
                    foreground.SetColor(colors);
                else
                {
                    var mask = new global::Android.Graphics.Drawables.GradientDrawable();
                    mask.SetColor(global::Android.Graphics.Color.White);
                    float radius = (float)((_view as Button)?.CornerRadius ?? (_view as ImageButton)?.CornerRadius ?? 12);
                    mask.SetCornerRadius(Math.Max(0, radius) * (element.Resources?.DisplayMetrics?.Density ?? 1));
                    element.Foreground = new global::Android.Graphics.Drawables.RippleDrawable(colors, null, mask);
                }
            }
        }
#endif
    }
}
