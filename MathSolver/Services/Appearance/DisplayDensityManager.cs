namespace MathSolver.Services;

/// <summary>Requested preference and capability-based density; independent of window layout and font scale.</summary>
public static class DisplayDensityManager
{
    private const string PreferenceKey = "display_density_mode";
    private static Application? _application;
    private static bool? _touchPresent;
    private static int _generation;

    public static DisplayDensityMode RequestedMode { get; private set; } = DisplayDensityMode.Automatic;
    public static DisplayDensityMode EffectiveMode { get; private set; } = DisplayDensityMode.Compact;
    internal static DisplayDensityMetrics Metrics => DisplayDensityPolicy.Metrics(EffectiveMode);
    public static event EventHandler? Changed;

    public static void Initialize(Application application)
    {
        if (_application is not null) return;
        _application = application;
        _touchPresent = ReadTouchCapability();
        RequestedMode = DisplayDensityPolicy.Parse(Preferences.Default.Get(PreferenceKey, "Automatic"));
        Apply();
    }

    public static void SetMode(DisplayDensityMode mode) => SetMode(mode, persist: true);

    // A temporary, nonpersistent selection is also useful for in-app previews and
    // native verification without touching the user's saved preference.
    internal static void SetMode(DisplayDensityMode mode, bool persist)
    {
        RequestedMode = DisplayDensityPolicy.Parse(mode.ToString());
        if (RequestedMode == DisplayDensityMode.Automatic) _touchPresent = ReadTouchCapability();
        if (persist) Preferences.Default.Set(PreferenceKey, RequestedMode.ToString());
        Apply();
    }

    public static void ResetToDefault() => SetMode(DisplayDensityMode.Automatic);

    internal static void BindControl(VisualElement control, bool square = false)
    {
        // Constructors often set a local minimum/padding. Remove that value so
        // it cannot take precedence over the live resource-backed setting.
        control.ClearValue(VisualElement.MinimumHeightRequestProperty);
        control.SetDynamicResource(VisualElement.MinimumHeightRequestProperty, "Density.ControlHeight");
        if (square)
        {
            control.ClearValue(VisualElement.MinimumWidthRequestProperty);
            control.SetDynamicResource(VisualElement.MinimumWidthRequestProperty, "Density.ControlWidth");
        }
        if (control is Button && !square)
        {
            control.ClearValue(Button.PaddingProperty);
            control.SetDynamicResource(Button.PaddingProperty, "Density.ButtonPadding");
        }
    }

    private static void Apply()
    {
        var application = _application;
        if (application is null) return;
        int generation = ++_generation;
        void Update()
        {
            if (generation != _generation || (application.Windows.Count > 0
                && application.Windows.All(window => WindowUiLifetime.IsClosing(window)))) return;
            EffectiveMode = DisplayDensityPolicy.Resolve(RequestedMode, _touchPresent);
            var metrics = Metrics;
            void Set(string key, object value)
            {
                if (!application.Resources.TryGetValue(key, out var old) || !Equals(old, value))
                    application.Resources[key] = value;
            }
            Set("Density.ControlHeight", metrics.ControlHeight);
            Set("Density.ControlWidth", metrics.ControlHeight);
            Set("Density.InputFrameHeight", metrics.ControlHeight + 4);
            var buttonPadding = new Thickness(metrics.HorizontalPadding, metrics.VerticalPadding);
            Set("Density.ButtonPadding", buttonPadding);
#if ANDROID
            Set("Density.NumberTypePadding", new Thickness(metrics.HorizontalPadding, metrics.VerticalPadding + 2,
                metrics.HorizontalPadding, Math.Max(0, metrics.VerticalPadding - 2)));
#else
            Set("Density.NumberTypePadding", buttonPadding);
#endif
            Set("Density.InputPadding", new Thickness(metrics.HorizontalPadding, 0));
            Set("Density.CardPadding", new Thickness(metrics.CardPadding));
            Set("Density.PagePadding", new Thickness(metrics.PagePadding, metrics.Spacing, metrics.PagePadding, metrics.PagePadding + 4));
            Set("Density.ScrollPadding", new Thickness(metrics.PagePadding, 0, metrics.PagePadding, metrics.PagePadding + 4));
            Set("Density.HeaderPadding", new Thickness(metrics.PagePadding, metrics.Spacing));
            Set("Density.ActionPadding", new Thickness(metrics.PagePadding, metrics.VerticalPadding));
            Set("Density.Spacing", metrics.Spacing);
            Set("Density.SmallSpacing", metrics.Spacing / 2);
            Changed?.Invoke(null, EventArgs.Empty);
        }
        if (application.Dispatcher.IsDispatchRequired) application.Dispatcher.Dispatch(Update);
        else Update();
    }

    private static bool? ReadTouchCapability()
    {
        try
        {
#if WINDOWS
            return new global::Windows.Devices.Input.TouchCapabilities().TouchPresent > 0;
#elif ANDROID
            return Android.App.Application.Context.PackageManager?.HasSystemFeature(Android.Content.PM.PackageManager.FeatureTouchscreen);
#else
            var idiom = DeviceInfo.Current.Idiom;
            return idiom == DeviceIdiom.Phone || idiom == DeviceIdiom.Tablet ? true
                : idiom == DeviceIdiom.Desktop ? false : null;
#endif
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"Touch capability detection unavailable: {error.Message}");
            return null;
        }
    }
}
