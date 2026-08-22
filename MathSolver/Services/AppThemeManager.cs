using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using System.Globalization;

namespace MathSolver.Services;

public enum AppThemeMode
{
    System,
    Light,
    Dark
}

public static class AppThemeManager
{
    private const string ThemeModePreferenceKey = "app_theme_mode";
    private const string AccentColorPreferenceKey = "app_accent_color";
    private const string DefaultAccentHex = "#6D28D9";

    private static bool _initialized;
    private static Application? _application;

    // Adaptive contrast for MP4 live wallpaper. null = follow the app theme,
    // true = use light text/dark glass, false = use dark text/light glass.
    private static bool? _liveWallpaperUseLightText;
    private const double LiveWallpaperLightTextThreshold = 0.46d;
    private const double LiveWallpaperDarkTextThreshold = 0.60d;
    private const double LiveWallpaperInitialThreshold = 0.53d;

    // Wallpaper resources can be refreshed by Switch/Picker callbacks, frame
    // contrast updates and import completion in very quick succession. Always
    // coalesce them onto a later UI turn so WinUI has time to disconnect the
    // outgoing MediaElement/GraphicsView handler before DynamicResource targets
    // are updated. This prevents transient COMException failures when turning
    // the animated background off.
    private static int _wallpaperVisualRefreshGeneration;
    private static int _themeVisualRefreshGeneration;

    private static readonly TimeSpan WallpaperVisualRefreshDelay =
        TimeSpan.FromMilliseconds(48);
    private static readonly TimeSpan WallpaperVisualRefreshRetryDelay =
        TimeSpan.FromMilliseconds(96);
    private const int WallpaperVisualRefreshMaxRetries = 3;

#if WINDOWS
    // DisconnectHandler() returns before every WinUI/Media Foundation object has
    // necessarily completed its native teardown. If the user turns wallpaper
    // off and immediately changes Light/Dark, DynamicResource propagation can
    // otherwise hit a stale native target and throw COMException. Hold all
    // ResourceDictionary mutations behind a short transition gate.
    private static long _nativeVisualTransitionNotBeforeTick;
    private static readonly TimeSpan NativeVisualTransitionGrace =
        TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan ThemeVisualRefreshRetryDelay =
        TimeSpan.FromMilliseconds(120);
    private const int ThemeVisualRefreshMaxRetries = 3;
#endif

    public static event EventHandler? ThemeChanged;

    public static AppThemeMode CurrentMode { get; private set; } =
        AppThemeMode.System;

    public static string CurrentAccentHex { get; private set; } =
        DefaultAccentHex;

    public static Color CurrentAccentColor =>
        TryParseHexColor(CurrentAccentHex, out Color color, out _)
            ? color
            : Color.FromArgb(DefaultAccentHex);

    // Màu icon đơn sắc được quyết định hoàn toàn bằng code:
    // Light  -> đen tuyệt đối
    // Dark   -> trắng tuyệt đối
    // System -> theo RequestedTheme hiện tại của hệ điều hành.
    public static bool IsDarkThemeEffective
    {
        get
        {
            if (CurrentMode == AppThemeMode.Dark)
            {
                return true;
            }

            if (CurrentMode == AppThemeMode.Light)
            {
                return false;
            }

            Application? application =
                _application ??
                Application.Current;

            return application?.RequestedTheme == AppTheme.Dark;
        }
    }

    public static Color MonochromeIconColor =>
        IsDarkThemeEffective
            ? Colors.White
            : Colors.Black;

    public static void Initialize(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _application = application;

        CurrentMode = ReadThemeMode(
            Preferences.Default.Get(
                ThemeModePreferenceKey,
                AppThemeMode.System.ToString()));

        string storedAccent = Preferences.Default.Get(
            AccentColorPreferenceKey,
            DefaultAccentHex);

        CurrentAccentHex =
            TryParseHexColor(storedAccent, out _, out string normalizedHex)
                ? normalizedHex
                : DefaultAccentHex;

        application.RequestedThemeChanged +=
            OnRequestedThemeChanged;

        ApplyCurrentTheme(savePreferences: false);
    }

    public static void SetThemeMode(AppThemeMode mode)
    {
        CurrentMode = mode;
        ApplyCurrentTheme(savePreferences: true);
    }

    public static bool SetAccentColor(string hexColor)
    {
        if (!TryParseHexColor(
                hexColor,
                out _,
                out string normalizedHex))
        {
            return false;
        }

        CurrentAccentHex = normalizedHex;
        ApplyCurrentTheme(savePreferences: true);
        return true;
    }

    public static void SetAccentColor(Color color)
    {
        CurrentAccentHex = ToHex(color);
        ApplyCurrentTheme(savePreferences: true);
    }

    public static void ResetToDefault()
    {
        CurrentMode = AppThemeMode.System;
        CurrentAccentHex = DefaultAccentHex;
        ApplyCurrentTheme(savePreferences: true);
    }

    /// <summary>
    /// Re-read platform theme colors without changing saved preferences. On
    /// Android this is used after an Activity recreation installs a Material
    /// You dynamic-color overlay. Other platforms simply reapply the current
    /// palette.
    /// </summary>
    public static void RefreshFromPlatformTheme()
    {
        if (!_initialized)
        {
            return;
        }

        ApplyCurrentTheme(savePreferences: false);
    }

#if WINDOWS
    /// <summary>
    /// Marks a short WinUI native-surface transition window. Theme and wallpaper
    /// ResourceDictionary updates are deferred until this window expires.
    /// </summary>
    public static void NotifyLiveWallpaperNativeTransition()
    {
        long target =
            Environment.TickCount64 +
            (long)NativeVisualTransitionGrace.TotalMilliseconds;

        while (true)
        {
            long current = Volatile.Read(
                ref _nativeVisualTransitionNotBeforeTick);

            if (current >= target)
            {
                break;
            }

            if (Interlocked.CompareExchange(
                    ref _nativeVisualTransitionNotBeforeTick,
                    target,
                    current) == current)
            {
                break;
            }
        }

        // Invalidate a wallpaper refresh that may already be queued for an
        // earlier visual state. The caller will queue the current state again.
        Interlocked.Increment(
            ref _wallpaperVisualRefreshGeneration);
    }

    private static TimeSpan GetNativeVisualTransitionDelay(
        TimeSpan minimumDelay)
    {
        long remainingMilliseconds =
            Volatile.Read(ref _nativeVisualTransitionNotBeforeTick) -
            Environment.TickCount64;

        double delayMilliseconds = Math.Max(
            minimumDelay.TotalMilliseconds,
            remainingMilliseconds > 0
                ? remainingMilliseconds + 16d
                : 0d);

        return TimeSpan.FromMilliseconds(delayMilliseconds);
    }
#endif

    /// <summary>
    /// Rebuild dynamic visual resources after a presentation setting changes.
    /// LiveWallpaperManager calls this after enable/import/remove so all open
    /// learning pages switch between opaque and glass surfaces immediately.
    /// </summary>
    public static void RefreshVisualResources()
    {
        if (!_initialized)
        {
            return;
        }

        Application? application =
            _application ??
            Application.Current;

        if (application is null)
        {
            return;
        }

        int generation = Interlocked.Increment(
            ref _wallpaperVisualRefreshGeneration);

        TimeSpan delay = WallpaperVisualRefreshDelay;
#if WINDOWS
        delay = GetNativeVisualTransitionDelay(delay);
#endif

        QueueWallpaperVisualRefresh(
            application,
            generation,
            delay,
            retryCount: 0);
    }

    private static void QueueWallpaperVisualRefresh(
        Application application,
        int generation,
        TimeSpan delay,
        int retryCount)
    {
        application.Dispatcher.DispatchDelayed(
            delay,
            () =>
            {
                if (generation != Volatile.Read(
                        ref _wallpaperVisualRefreshGeneration))
                {
                    return;
                }

#if WINDOWS
                TimeSpan transitionDelay =
                    GetNativeVisualTransitionDelay(TimeSpan.Zero);

                if (transitionDelay > TimeSpan.Zero)
                {
                    QueueWallpaperVisualRefresh(
                        application,
                        generation,
                        transitionDelay,
                        retryCount);
                    return;
                }
#endif

                try
                {
                    ApplyWallpaperVisualResourcesCore(application);
                }
#if WINDOWS
                catch (System.Runtime.InteropServices.COMException exception)
                {
                    if (retryCount < WallpaperVisualRefreshMaxRetries)
                    {
                        QueueWallpaperVisualRefresh(
                            application,
                            generation,
                            GetNativeVisualTransitionDelay(
                                WallpaperVisualRefreshRetryDelay),
                            retryCount + 1);
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"Wallpaper resource refresh deferred: {exception}");
                }
#endif
            });
    }

    private static void ApplyWallpaperVisualResourcesCore(
        Application application)
    {
        AppTheme effectiveTheme =
            CurrentMode switch
            {
                AppThemeMode.Light => AppTheme.Light,
                AppThemeMode.Dark => AppTheme.Dark,
                _ => application.RequestedTheme == AppTheme.Dark
                    ? AppTheme.Dark
                    : AppTheme.Light
            };

        ThemePalette palette;

#if ANDROID
        if (AndroidMaterialYouManager.TryGetCurrentColorScheme(
                out AndroidMaterialColorScheme materialScheme))
        {
            palette = CreateMaterialYouPalette(
                effectiveTheme,
                materialScheme);
        }
        else
#endif
        {
            palette = CreatePalette(
                effectiveTheme,
                CurrentAccentColor);
        }

        // Wallpaper mode/enable changes only affect glass/scrim tokens.
        // Do not assign UserAppTheme, rebuild the whole palette, or raise
        // ThemeChanged while native video/drawing handlers are transitioning.
        ApplyWallpaperVisualPalette(
            application.Resources,
            palette,
            effectiveTheme);
    }

    public static void SetLiveWallpaperFrameLuminance(
        double luminance)
    {
        luminance = Math.Clamp(luminance, 0d, 1d);

        bool nextUseLightText =
            _liveWallpaperUseLightText switch
            {
                true => luminance < LiveWallpaperDarkTextThreshold,
                false => luminance <= LiveWallpaperLightTextThreshold,
                null => luminance < LiveWallpaperInitialThreshold
            };

        if (_liveWallpaperUseLightText == nextUseLightText)
        {
            return;
        }

        _liveWallpaperUseLightText = nextUseLightText;
        RefreshVisualResources();
    }

    public static void ResetLiveWallpaperAdaptiveContrast()
    {
        if (_liveWallpaperUseLightText is null)
        {
            return;
        }

        _liveWallpaperUseLightText = null;
        RefreshVisualResources();
    }

    public static bool TryParseHexColor(
        string? input,
        out Color color,
        out string normalizedHex)
    {
        color = Colors.Transparent;
        normalizedHex = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string value = input.Trim().TrimStart('#');

        if (value.Length == 3)
        {
            value = string.Concat(
                value.Select(character => new string(character, 2)));
        }

        if (value.Length != 6 ||
            !uint.TryParse(
                value,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out uint rgb))
        {
            return false;
        }

        byte red = (byte)((rgb >> 16) & 0xFF);
        byte green = (byte)((rgb >> 8) & 0xFF);
        byte blue = (byte)(rgb & 0xFF);

        color = Color.FromRgb(red, green, blue);
        normalizedHex = $"#{red:X2}{green:X2}{blue:X2}";
        return true;
    }

    public static string ToHex(Color color)
    {
        byte red = ToByte(color.Red);
        byte green = ToByte(color.Green);
        byte blue = ToByte(color.Blue);

        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static void ApplyCurrentTheme(bool savePreferences)
    {
        Application? application =
            _application ??
            Application.Current;

        if (application is null)
        {
            return;
        }

        // Persist the user's choice immediately even when visual application
        // must wait for a native wallpaper teardown to finish.
        if (savePreferences)
        {
            Preferences.Default.Set(
                ThemeModePreferenceKey,
                CurrentMode.ToString());

            Preferences.Default.Set(
                AccentColorPreferenceKey,
                CurrentAccentHex);
        }

        int generation = Interlocked.Increment(
            ref _themeVisualRefreshGeneration);

#if WINDOWS
        TimeSpan transitionDelay =
            GetNativeVisualTransitionDelay(TimeSpan.Zero);

        if (transitionDelay > TimeSpan.Zero)
        {
            QueueThemeVisualRefresh(
                application,
                generation,
                transitionDelay,
                retryCount: 0);
            return;
        }
#endif

        void ApplyNow()
        {
            if (generation != Volatile.Read(
                    ref _themeVisualRefreshGeneration))
            {
                return;
            }

            TryApplyCurrentThemeCore(
                application,
                generation,
                retryCount: 0);
        }

        if (MainThread.IsMainThread)
        {
            ApplyNow();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(ApplyNow);
        }
    }

    private static void ApplyCurrentThemeCore(
        Application application)
    {
        application.UserAppTheme =
            CurrentMode switch
            {
                AppThemeMode.Light => AppTheme.Light,
                AppThemeMode.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };

        AppTheme effectiveTheme =
            CurrentMode switch
            {
                AppThemeMode.Light => AppTheme.Light,
                AppThemeMode.Dark => AppTheme.Dark,
                _ => application.RequestedTheme == AppTheme.Dark
                    ? AppTheme.Dark
                    : AppTheme.Light
            };

        ThemePalette palette;

#if ANDROID
        if (AndroidMaterialYouManager.TryGetCurrentColorScheme(
                out AndroidMaterialColorScheme materialScheme))
        {
            palette = CreateMaterialYouPalette(
                effectiveTheme,
                materialScheme);
        }
        else
#endif
        {
            palette = CreatePalette(
                effectiveTheme,
                CurrentAccentColor);
        }

        ApplyPalette(application.Resources, palette);
        ApplyWallpaperVisualPalette(
            application.Resources,
            palette,
            effectiveTheme);

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void TryApplyCurrentThemeCore(
        Application application,
        int generation,
        int retryCount)
    {
#if WINDOWS
        TimeSpan transitionDelay =
            GetNativeVisualTransitionDelay(TimeSpan.Zero);

        if (transitionDelay > TimeSpan.Zero)
        {
            QueueThemeVisualRefresh(
                application,
                generation,
                transitionDelay,
                retryCount);
            return;
        }
#endif

        try
        {
            ApplyCurrentThemeCore(application);
        }
#if WINDOWS
        catch (System.Runtime.InteropServices.COMException exception)
        {
            if (retryCount < ThemeVisualRefreshMaxRetries)
            {
                QueueThemeVisualRefresh(
                    application,
                    generation,
                    GetNativeVisualTransitionDelay(
                        ThemeVisualRefreshRetryDelay),
                    retryCount + 1);
                return;
            }

            // A resource update must never terminate the app. The next normal
            // theme/wallpaper refresh will reconcile any token that WinUI could
            // not update while a stale native target was being destroyed.
            System.Diagnostics.Debug.WriteLine(
                $"Theme resource refresh deferred: {exception}");
        }
#endif
    }

#if WINDOWS
    private static void QueueThemeVisualRefresh(
        Application application,
        int generation,
        TimeSpan delay,
        int retryCount)
    {
        application.Dispatcher.DispatchDelayed(
            delay,
            () =>
            {
                if (generation != Volatile.Read(
                        ref _themeVisualRefreshGeneration))
                {
                    return;
                }

                TryApplyCurrentThemeCore(
                    application,
                    generation,
                    retryCount);
            });
    }
#endif

    private static void OnRequestedThemeChanged(
        object? sender,
        AppThemeChangedEventArgs e)
    {
        if (CurrentMode == AppThemeMode.System)
        {
            ApplyCurrentTheme(savePreferences: false);
        }
    }

#if ANDROID
    private static ThemePalette CreateMaterialYouPalette(
        AppTheme theme,
        AndroidMaterialColorScheme material)
    {
        bool dark = theme == AppTheme.Dark;

        Color success = dark
            ? Color.FromArgb("#6DD58C")
            : Color.FromArgb("#146C2E");
        Color successSoft = dark
            ? Color.FromArgb("#0A3818")
            : Color.FromArgb("#C4EED0");
        Color warning = dark
            ? Color.FromArgb("#FFB77C")
            : Color.FromArgb("#8A4D00");
        Color warningSoft = dark
            ? Color.FromArgb("#472A00")
            : Color.FromArgb("#FFDDBD");
        Color info = material.Primary;
        Color infoSoft = material.PrimaryContainer;

        return new ThemePalette(
            Accent: material.Primary,
            AccentDark: Mix(material.Primary, Colors.Black, dark ? 0.10 : 0.18),
            AccentSoft: material.PrimaryContainer,
            AccentBorder: material.OutlineVariant,
            OnAccent: material.OnPrimary,
            PageBackground: material.Surface,
            Surface: material.Surface,
            SurfaceAlt: material.SurfaceContainerLow,
            InputBackground: material.SurfaceContainerHigh,
            TextPrimary: material.OnSurface,
            TextSecondary: material.OnSurfaceVariant,
            Border: material.OutlineVariant,
            Divider: material.OutlineVariant,
            Success: success,
            SuccessSoft: successSoft,
            SuccessBorder: Mix(success, material.Surface, 0.50),
            Warning: warning,
            WarningSoft: warningSoft,
            WarningBorder: Mix(warning, material.Surface, 0.50),
            Danger: material.Error,
            DangerSoft: material.ErrorContainer,
            DangerBorder: Mix(material.Error, material.Surface, 0.48),
            Info: info,
            InfoSoft: infoSoft,
            InfoBorder: material.OutlineVariant,
            ShellBackground: material.SurfaceContainer,
            ShellForeground: material.Primary,
            ShellUnselected: material.OnSurfaceVariant);
    }
#endif

    private static ThemePalette CreatePalette(
        AppTheme theme,
        Color accent)
    {
        Color accentDark = Mix(accent, Colors.Black, 0.18);

        Color onAccent =
            GetRelativeLuminance(accent) > 0.56
                ? Color.FromArgb("#111827")
                : Colors.White;

        if (theme == AppTheme.Dark)
        {
            Color surface = Color.FromArgb("#111827");
            Color shellAccent = EnsureVisibleOnSurface(
                accent,
                Color.FromArgb("#0F172A"),
                preferLighter: true);

            return new ThemePalette(
                Accent: accent,
                AccentDark: accentDark,
                AccentSoft: Mix(surface, accent, 0.22),
                AccentBorder: Mix(Color.FromArgb("#334155"), accent, 0.38),
                OnAccent: onAccent,
                PageBackground: Color.FromArgb("#0B1120"),
                Surface: surface,
                SurfaceAlt: Color.FromArgb("#172033"),
                InputBackground: Color.FromArgb("#0F172A"),
                TextPrimary: Color.FromArgb("#F8FAFC"),
                TextSecondary: Color.FromArgb("#CBD5E1"),
                Border: Color.FromArgb("#334155"),
                Divider: Color.FromArgb("#293548"),
                Success: Color.FromArgb("#4ADE80"),
                SuccessSoft: Color.FromArgb("#12351F"),
                SuccessBorder: Color.FromArgb("#287A46"),
                Warning: Color.FromArgb("#FDBA74"),
                WarningSoft: Color.FromArgb("#3A2412"),
                WarningBorder: Color.FromArgb("#9A5B28"),
                Danger: Color.FromArgb("#FDA4AF"),
                DangerSoft: Color.FromArgb("#3B1720"),
                DangerBorder: Color.FromArgb("#9F3A4B"),
                Info: Color.FromArgb("#93C5FD"),
                InfoSoft: Color.FromArgb("#132A4A"),
                InfoBorder: Color.FromArgb("#315D96"),
                ShellBackground: Color.FromArgb("#0F172A"),
                ShellForeground: shellAccent,
                ShellUnselected: Color.FromArgb("#94A3B8"));
        }

        Color lightShellAccent = EnsureVisibleOnSurface(
            accent,
            Colors.White,
            preferLighter: false);

        return new ThemePalette(
            Accent: accent,
            AccentDark: accentDark,
            AccentSoft: Mix(accent, Colors.White, 0.88),
            AccentBorder: Mix(accent, Colors.White, 0.62),
            OnAccent: onAccent,
            PageBackground: Color.FromArgb("#F4F7FB"),
            Surface: Colors.White,
            SurfaceAlt: Color.FromArgb("#F8FAFC"),
            InputBackground: Colors.White,
            TextPrimary: Color.FromArgb("#1E293B"),
            TextSecondary: Color.FromArgb("#64748B"),
            Border: Color.FromArgb("#D8E2EF"),
            Divider: Color.FromArgb("#E2E8F0"),
            Success: Color.FromArgb("#15803D"),
            SuccessSoft: Color.FromArgb("#F0FDF4"),
            SuccessBorder: Color.FromArgb("#BBF7D0"),
            Warning: Color.FromArgb("#C2410C"),
            WarningSoft: Color.FromArgb("#FFF7ED"),
            WarningBorder: Color.FromArgb("#FDBA74"),
            Danger: Color.FromArgb("#B91C1C"),
            DangerSoft: Color.FromArgb("#FEF2F2"),
            DangerBorder: Color.FromArgb("#FCA5A5"),
            Info: Color.FromArgb("#2563EB"),
            InfoSoft: Color.FromArgb("#EFF6FF"),
            InfoBorder: Color.FromArgb("#BFDBFE"),
            ShellBackground: Colors.White,
            ShellForeground: lightShellAccent,
            ShellUnselected: Color.FromArgb("#64748B"));
    }

    private static void ApplyPalette(
        ResourceDictionary resources,
        ThemePalette palette)
    {
        SetColorAndBrush(resources, "PrimaryColor", "PrimaryBrush", palette.Accent);
        SetColorAndBrush(resources, "PrimaryDarkColor", "PrimaryDarkBrush", palette.AccentDark);
        SetColorAndBrush(resources, "PrimarySoftColor", "PrimarySoftBrush", palette.AccentSoft);
        SetColorAndBrush(resources, "PrimaryBorderColor", "PrimaryBorderBrush", palette.AccentBorder);
        SetColorAndBrush(resources, "OnPrimaryColor", "OnPrimaryBrush", palette.OnAccent);
        SetColorAndBrush(resources, "PageBackgroundColor", "PageBackgroundBrush", palette.PageBackground);
        SetColorAndBrush(resources, "SurfaceColor", "SurfaceBrush", palette.Surface);
        SetColorAndBrush(resources, "SurfaceAltColor", "SurfaceAltBrush", palette.SurfaceAlt);
#if ANDROID
        SetColorAndBrush(resources, "SurfaceContainerLowColor", "SurfaceContainerLowBrush", palette.SurfaceAlt);
        SetColorAndBrush(resources, "SurfaceContainerColor", "SurfaceContainerBrush", palette.ShellBackground);
        SetColorAndBrush(resources, "SurfaceContainerHighColor", "SurfaceContainerHighBrush", palette.InputBackground);
#else
        // Keep the established WinUI/iOS/Mac surface appearance exactly as it
        // was before the Android Material You migration.
        SetColorAndBrush(resources, "SurfaceContainerLowColor", "SurfaceContainerLowBrush", palette.Surface);
        SetColorAndBrush(resources, "SurfaceContainerColor", "SurfaceContainerBrush", palette.Surface);
        SetColorAndBrush(resources, "SurfaceContainerHighColor", "SurfaceContainerHighBrush", palette.Surface);
#endif
        SetColorAndBrush(resources, "InputBackgroundColor", "InputBackgroundBrush", palette.InputBackground);
        SetColorAndBrush(resources, "TextPrimaryColor", "TextPrimaryBrush", palette.TextPrimary);
        SetColorAndBrush(resources, "TextSecondaryColor", "TextSecondaryBrush", palette.TextSecondary);
        SetColorAndBrush(resources, "BorderColor", "BorderBrush", palette.Border);
        SetColorAndBrush(resources, "DividerColor", "DividerBrush", palette.Divider);
        SetColorAndBrush(resources, "SuccessColor", "SuccessBrush", palette.Success);
        SetColorAndBrush(resources, "SuccessSoftColor", "SuccessSoftBrush", palette.SuccessSoft);
        SetColorAndBrush(resources, "SuccessBorderColor", "SuccessBorderBrush", palette.SuccessBorder);
        SetColorAndBrush(resources, "WarningColor", "WarningBrush", palette.Warning);
        SetColorAndBrush(resources, "WarningSoftColor", "WarningSoftBrush", palette.WarningSoft);
        SetColorAndBrush(resources, "WarningBorderColor", "WarningBorderBrush", palette.WarningBorder);
        SetColorAndBrush(resources, "DangerColor", "DangerBrush", palette.Danger);
        SetColorAndBrush(resources, "DangerSoftColor", "DangerSoftBrush", palette.DangerSoft);
        SetColorAndBrush(resources, "DangerBorderColor", "DangerBorderBrush", palette.DangerBorder);
        SetColorAndBrush(resources, "InfoColor", "InfoBrush", palette.Info);
        SetColorAndBrush(resources, "InfoSoftColor", "InfoSoftBrush", palette.InfoSoft);
        SetColorAndBrush(resources, "InfoBorderColor", "InfoBorderBrush", palette.InfoBorder);
        SetColorAndBrush(resources, "ShellBackgroundColor", "ShellBackgroundBrush", palette.ShellBackground);
        SetColorAndBrush(resources, "ShellForegroundColor", "ShellForegroundBrush", palette.ShellForeground);
        SetColorAndBrush(resources, "ShellUnselectedColor", "ShellUnselectedBrush", palette.ShellUnselected);

        // Đồng bộ các key cũ của template MAUI.
        resources["Primary"] = palette.Accent;
        resources["PrimaryDark"] = palette.AccentDark;
        resources["PrimaryDarkText"] = palette.TextPrimary;
        resources["Secondary"] = palette.AccentSoft;
        resources["SecondaryDarkText"] = palette.TextSecondary;
        resources["Tertiary"] = palette.AccentDark;
        resources["Magenta"] = palette.Accent;
        resources["MidnightBlue"] = palette.TextPrimary;
    }

    private static void ApplyWallpaperVisualPalette(
        ResourceDictionary resources,
        ThemePalette palette,
        AppTheme effectiveTheme)
    {
        bool wallpaperEnabled = LiveWallpaperManager.IsEnabled;

        if (!wallpaperEnabled)
        {
            // Exact opaque mapping: no visual regression when wallpaper is off.
            SetColorAndBrush(resources, "WallpaperSurfaceStrongColor", "WallpaperSurfaceStrongBrush", palette.Surface);
            SetColorAndBrush(resources, "WallpaperSurfaceColor", "WallpaperSurfaceBrush", palette.Surface);
            SetColorAndBrush(resources, "WallpaperSurfaceAltColor", "WallpaperSurfaceAltBrush", palette.SurfaceAlt);
            SetColorAndBrush(resources, "WallpaperInputBackgroundColor", "WallpaperInputBackgroundBrush", palette.InputBackground);
            SetColorAndBrush(resources, "WallpaperBorderColor", "WallpaperBorderBrush", palette.Border);
            SetColorAndBrush(resources, "WallpaperDividerColor", "WallpaperDividerBrush", palette.Divider);
            SetColorAndBrush(resources, "WallpaperPrimarySoftColor", "WallpaperPrimarySoftBrush", palette.AccentSoft);
            SetColorAndBrush(resources, "WallpaperPrimaryBorderColor", "WallpaperPrimaryBorderBrush", palette.AccentBorder);
            SetColorAndBrush(resources, "WallpaperSuccessSoftColor", "WallpaperSuccessSoftBrush", palette.SuccessSoft);
            SetColorAndBrush(resources, "WallpaperWarningSoftColor", "WallpaperWarningSoftBrush", palette.WarningSoft);
            SetColorAndBrush(resources, "WallpaperDangerSoftColor", "WallpaperDangerSoftBrush", palette.DangerSoft);
            SetColorAndBrush(resources, "WallpaperInfoSoftColor", "WallpaperInfoSoftBrush", palette.InfoSoft);
            SetColorAndBrush(resources, "WallpaperTextPrimaryColor", "WallpaperTextPrimaryBrush", palette.TextPrimary);
            SetColorAndBrush(resources, "WallpaperTextSecondaryColor", "WallpaperTextSecondaryBrush", palette.TextSecondary);
            SetColorAndBrush(resources, "LiveWallpaperScrimColor", "LiveWallpaperScrimBrush", Colors.Transparent);
            return;
        }

        bool mathAnimation =
            LiveWallpaperManager.Mode ==
            LiveWallpaperMode.MathAnimation;

        bool themeIsDark =
            effectiveTheme == AppTheme.Dark;

        // Math Animation deliberately follows the selected app theme. MP4 can
        // override the learning-area glass/text polarity from its precomputed
        // frame-brightness timeline without changing the app-wide theme.
        bool darkGlass;

        if (mathAnimation ||
            _liveWallpaperUseLightText is null)
        {
            darkGlass = themeIsDark;
        }
        else
        {
            darkGlass = _liveWallpaperUseLightText.Value;
        }

        Color surfaceBase = darkGlass
            ? Color.FromArgb("#101827")
            : Colors.White;
        Color surfaceAltBase = darkGlass
            ? Color.FromArgb("#182235")
            : Color.FromArgb("#F6F8FC");
        Color inputBase = darkGlass
            ? Color.FromArgb("#1B263A")
            : Colors.White;
        Color borderBase = darkGlass
            ? Color.FromArgb("#A9B8CF")
            : Color.FromArgb("#7B8BA5");
        Color dividerBase = darkGlass
            ? Color.FromArgb("#8392AA")
            : Color.FromArgb("#A7B2C3");

        // Wallpaper text uses a hard polarity rather than inheriting the app
        // theme. A dark video frame must always produce light text and a
        // bright frame must always produce dark text. This keeps titles,
        // neutral buttons, labels and placeholders readable even when the
        // wallpaper theme is the opposite of the selected app theme.
        Color textPrimary = darkGlass
            ? Colors.White
            : Colors.Black;
        Color textSecondary = darkGlass
            ? Color.FromArgb("#F1F5F9")
            : Color.FromArgb("#1F2937");

        Color surfaceStrong =
            WithAlpha(surfaceBase, darkGlass ? 0.86 : 0.84);
        Color surface =
            WithAlpha(surfaceBase, darkGlass ? 0.76 : 0.74);
        Color surfaceAlt =
            WithAlpha(surfaceAltBase, darkGlass ? 0.66 : 0.62);
        Color input =
            WithAlpha(inputBase, darkGlass ? 0.90 : 0.88);

        SetColorAndBrush(resources, "WallpaperSurfaceStrongColor", "WallpaperSurfaceStrongBrush", surfaceStrong);
        SetColorAndBrush(resources, "WallpaperSurfaceColor", "WallpaperSurfaceBrush", surface);
        SetColorAndBrush(resources, "WallpaperSurfaceAltColor", "WallpaperSurfaceAltBrush", surfaceAlt);
        SetColorAndBrush(resources, "WallpaperInputBackgroundColor", "WallpaperInputBackgroundBrush", input);
        SetColorAndBrush(resources, "WallpaperBorderColor", "WallpaperBorderBrush", WithAlpha(borderBase, darkGlass ? 0.46 : 0.42));
        SetColorAndBrush(resources, "WallpaperDividerColor", "WallpaperDividerBrush", WithAlpha(dividerBase, darkGlass ? 0.34 : 0.38));
        SetColorAndBrush(resources, "WallpaperPrimarySoftColor", "WallpaperPrimarySoftBrush", WithAlpha(palette.AccentSoft, darkGlass ? 0.70 : 0.76));
        SetColorAndBrush(resources, "WallpaperPrimaryBorderColor", "WallpaperPrimaryBorderBrush", WithAlpha(palette.AccentBorder, 0.90));
        SetColorAndBrush(resources, "WallpaperSuccessSoftColor", "WallpaperSuccessSoftBrush", WithAlpha(palette.SuccessSoft, darkGlass ? 0.74 : 0.80));
        SetColorAndBrush(resources, "WallpaperWarningSoftColor", "WallpaperWarningSoftBrush", WithAlpha(palette.WarningSoft, darkGlass ? 0.74 : 0.80));
        SetColorAndBrush(resources, "WallpaperDangerSoftColor", "WallpaperDangerSoftBrush", WithAlpha(palette.DangerSoft, darkGlass ? 0.74 : 0.80));
        SetColorAndBrush(resources, "WallpaperInfoSoftColor", "WallpaperInfoSoftBrush", WithAlpha(palette.InfoSoft, darkGlass ? 0.74 : 0.80));
        SetColorAndBrush(resources, "WallpaperTextPrimaryColor", "WallpaperTextPrimaryBrush", textPrimary);
        SetColorAndBrush(resources, "WallpaperTextSecondaryColor", "WallpaperTextSecondaryBrush", textSecondary);

        // The built-in animation already uses restrained theme colors, so it
        // only needs a light veil. MP4 uses a veil matched to the current frame
        // polarity. Runtime only changes resources when hysteresis crosses a
        // threshold, avoiding flicker and per-frame layout churn.
        Color scrim = mathAnimation
            ? (darkGlass
                ? new Color(0.015f, 0.027f, 0.055f, 0.18f)
                : new Color(1f, 1f, 1f, 0.12f))
            : (darkGlass
                ? new Color(0.015f, 0.027f, 0.055f, 0.28f)
                : new Color(1f, 1f, 1f, 0.22f));

        SetColorAndBrush(resources, "LiveWallpaperScrimColor", "LiveWallpaperScrimBrush", scrim);
    }

    private static Color WithAlpha(Color color, double alpha)
    {
        return new Color(
            color.Red,
            color.Green,
            color.Blue,
            (float)Math.Clamp(alpha, 0d, 1d));
    }

    private static void SetColorAndBrush(
        ResourceDictionary resources,
        string colorKey,
        string brushKey,
        Color color)
    {
        // Avoid replacing resource objects when nothing changed. Adaptive MP4
        // contrast can touch these tokens many times during a session; keeping
        // the existing SolidColorBrush removes needless allocations and reduces
        // DynamicResource churn on WinUI/Android.
        if (!resources.TryGetValue(
                colorKey,
                out object? currentColorResource) ||
            currentColorResource is not Color currentColor ||
            !AreColorsEquivalent(currentColor, color))
        {
            resources[colorKey] = color;
        }

        if (resources.TryGetValue(
                brushKey,
                out object? currentBrushResource) &&
            currentBrushResource is SolidColorBrush currentBrush)
        {
            if (!AreColorsEquivalent(currentBrush.Color, color))
            {
                currentBrush.Color = color;
            }

            return;
        }

        resources[brushKey] = new SolidColorBrush(color);
    }

    private static bool AreColorsEquivalent(
        Color left,
        Color right)
    {
        const float epsilon = 0.0001f;

        return
            Math.Abs(left.Red - right.Red) <= epsilon &&
            Math.Abs(left.Green - right.Green) <= epsilon &&
            Math.Abs(left.Blue - right.Blue) <= epsilon &&
            Math.Abs(left.Alpha - right.Alpha) <= epsilon;
    }

    private static AppThemeMode ReadThemeMode(string? value)
    {
        return Enum.TryParse(
            value,
            ignoreCase: true,
            out AppThemeMode mode)
                ? mode
                : AppThemeMode.System;
    }

    private static Color EnsureVisibleOnSurface(
        Color color,
        Color surface,
        bool preferLighter)
    {
        if (GetContrastRatio(color, surface) >= 3.0)
        {
            return color;
        }

        Color target = preferLighter ? Colors.White : Colors.Black;

        for (double amount = 0.12; amount <= 0.72; amount += 0.12)
        {
            Color candidate = Mix(color, target, amount);

            if (GetContrastRatio(candidate, surface) >= 3.0)
            {
                return candidate;
            }
        }

        return target;
    }

    private static double GetContrastRatio(Color first, Color second)
    {
        double firstLuminance = GetRelativeLuminance(first);
        double secondLuminance = GetRelativeLuminance(second);
        double lighter = Math.Max(firstLuminance, secondLuminance);
        double darker = Math.Min(firstLuminance, secondLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static Color Mix(
        Color first,
        Color second,
        double amount)
    {
        double value = Math.Clamp(amount, 0, 1);

        return new Color(
            (float)(first.Red + (second.Red - first.Red) * value),
            (float)(first.Green + (second.Green - first.Green) * value),
            (float)(first.Blue + (second.Blue - first.Blue) * value),
            1f);
    }

    private static double GetRelativeLuminance(Color color)
    {
        static double ConvertChannel(double channel)
        {
            return channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return
            0.2126 * ConvertChannel(color.Red) +
            0.7152 * ConvertChannel(color.Green) +
            0.0722 * ConvertChannel(color.Blue);
    }

    private static byte ToByte(float component)
    {
        return (byte)Math.Round(
            Math.Clamp(component, 0f, 1f) * 255f);
    }

    private sealed record ThemePalette(
        Color Accent,
        Color AccentDark,
        Color AccentSoft,
        Color AccentBorder,
        Color OnAccent,
        Color PageBackground,
        Color Surface,
        Color SurfaceAlt,
        Color InputBackground,
        Color TextPrimary,
        Color TextSecondary,
        Color Border,
        Color Divider,
        Color Success,
        Color SuccessSoft,
        Color SuccessBorder,
        Color Warning,
        Color WarningSoft,
        Color WarningBorder,
        Color Danger,
        Color DangerSoft,
        Color DangerBorder,
        Color Info,
        Color InfoSoft,
        Color InfoBorder,
        Color ShellBackground,
        Color ShellForeground,
        Color ShellUnselected);
}
