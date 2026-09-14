using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MathSolver.Controls;

/// <summary>
/// Phản hồi nhấn dùng chung cho Button trong các vùng giao diện được bật.
/// Animation chạy trên chính Button nên không làm thay đổi layout hay màu
/// trạng thái đang chọn của tab.
/// </summary>
public static class InteractiveButtonAnimation
{
#if ANDROID
    // Material 3 owns the Android ripple/state layer. Keep only a subtle scale
    // response so the MAUI animation does not fade out the native ripple.
    private const double PressedScale = 0.985d;
    private const double PressedOpacity = 1d;
    private const double ReleaseOvershoot = 1d;
#else
    private const double PressedScale = 0.96d;
    private const double PressedOpacity = 0.88d;
    private const double ReleaseOvershoot = 1.015d;
#endif

    private const uint PressDuration = 70;
    private const uint ReleaseDuration = 85;
    private const uint SettleDuration = 95;

    // Back navigation uses platform-specific feedback. On Android the native
    // MaterialButton ripple/state layer owns the press animation entirely.
    // On Windows keep a restrained Fluent-style press without spring overshoot.
    private const double WinBackPressedScale = 0.97d;
    private const double WinBackPressedOpacity = 0.94d;
    private const uint WinBackPressDuration = 55;
    private const uint WinBackReleaseDuration = 80;

    private static readonly ConditionalWeakTable<Button, AnimationState>
        States = new();

    public static readonly BindableProperty IsScopeEnabledProperty =
        BindableProperty.CreateAttached(
            "IsScopeEnabled",
            typeof(bool),
            typeof(InteractiveButtonAnimation),
            false);

    public static bool GetIsScopeEnabled(BindableObject bindable) =>
        (bool)bindable.GetValue(IsScopeEnabledProperty);

    public static void SetIsScopeEnabled(
        BindableObject bindable,
        bool value) =>
        bindable.SetValue(IsScopeEnabledProperty, value);

    public static readonly BindableProperty IsPlatformBackButtonProperty =
        BindableProperty.CreateAttached(
            "IsPlatformBackButton",
            typeof(bool),
            typeof(InteractiveButtonAnimation),
            false);

    public static bool GetIsPlatformBackButton(BindableObject bindable) =>
        (bool)bindable.GetValue(IsPlatformBackButtonProperty);

    public static void SetIsPlatformBackButton(
        BindableObject bindable,
        bool value) =>
        bindable.SetValue(IsPlatformBackButtonProperty, value);

    /// <summary>
    /// Được gọi từ ButtonHandler mapper. Có thể gọi nhiều lần an toàn khi
    /// native handler được tạo lại sau thay đổi cửa sổ hoặc vòng đời trang.
    /// </summary>
    public static void Attach(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);

        AnimationState state =
            States.GetValue(
                button,
                static target =>
                    new AnimationState(
                        target.Scale,
                        target.Opacity));

        if (state.IsAttached)
        {
            return;
        }

        state.IsAttached = true;

        button.Pressed += OnPressed;
        button.Released += OnReleased;
        button.Unloaded += OnUnloaded;
        button.PropertyChanged += OnButtonPropertyChanged;
    }

    private static async void OnPressed(
        object? sender,
        EventArgs e)
    {
        if (sender is not Button button ||
            !button.IsEnabled ||
            !IsInsideEnabledScope(button))
        {
            return;
        }

        if (!States.TryGetValue(
                button,
                out AnimationState? state))
        {
            return;
        }

        bool isPlatformBackButton =
            GetIsPlatformBackButton(button);

#if ANDROID
        // MaterialButton already provides the correct circular ripple/state layer.
        // Do not scale/fade it or the native feedback becomes visually muddy.
        if (isPlatformBackButton)
        {
            return;
        }
#endif

        state.Version++;

        state.IsPressed = true;
        button.CancelAnimations();

        double pressedScale =
            isPlatformBackButton
                ? WinBackPressedScale
                : PressedScale;

        double pressedOpacity =
            isPlatformBackButton
                ? WinBackPressedOpacity
                : PressedOpacity;

        uint pressDuration =
            isPlatformBackButton
                ? WinBackPressDuration
                : PressDuration;

        await Task.WhenAll(
            button.ScaleToAsync(
                state.RestingScale * pressedScale,
                pressDuration,
                Easing.CubicOut),

            button.FadeToAsync(
                state.RestingOpacity * pressedOpacity,
                pressDuration,
                Easing.CubicOut));

    }

    private static async void OnReleased(
        object? sender,
        EventArgs e)
    {
        if (sender is not Button button ||
            !States.TryGetValue(button, out AnimationState? state) ||
            !state.IsPressed)
        {
            return;
        }

        bool isPlatformBackButton =
            GetIsPlatformBackButton(button);

        state.IsPressed = false;
        int version = ++state.Version;

        button.CancelAnimations();

        if (isPlatformBackButton)
        {
            await Task.WhenAll(
                button.ScaleToAsync(
                    state.RestingScale,
                    WinBackReleaseDuration,
                    Easing.CubicOut),

                button.FadeToAsync(
                    state.RestingOpacity,
                    WinBackReleaseDuration,
                    Easing.CubicOut));

            return;
        }

        await Task.WhenAll(
            button.ScaleToAsync(
                state.RestingScale * ReleaseOvershoot,
                ReleaseDuration,
                Easing.CubicOut),

            button.FadeToAsync(
                state.RestingOpacity,
                ReleaseDuration,
                Easing.CubicOut));

        if (version != state.Version ||
            state.IsPressed)
        {
            return;
        }

        await button.ScaleToAsync(
            state.RestingScale,
            SettleDuration,
            Easing.SpringOut);
    }

    private static void OnUnloaded(
        object? sender,
        EventArgs e)
    {
        if (sender is Button button &&
            States.TryGetValue(button, out AnimationState? state))
        {
            Reset(button, state);
        }
    }

    private static void OnButtonPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (sender is Button button &&
            e.PropertyName == nameof(Button.IsEnabled) &&
            !button.IsEnabled &&
            States.TryGetValue(button, out AnimationState? state))
        {
            Reset(button, state);
        }
    }

    private static bool IsInsideEnabledScope(Element element)
    {
        Element? current = element;

        while (current is not null)
        {
            if (GetIsScopeEnabled(current))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static void Reset(
        Button button,
        AnimationState state)
    {
        state.IsPressed = false;
        state.Version++;

        button.CancelAnimations();
        button.Scale = state.RestingScale;
        button.Opacity = state.RestingOpacity;
    }

    private sealed class AnimationState(
        double restingScale,
        double restingOpacity)
    {
        public double RestingScale { get; } = restingScale;

        public double RestingOpacity { get; } = restingOpacity;

        public bool IsAttached { get; set; }

        public bool IsPressed { get; set; }

        public int Version { get; set; }
    }
}
