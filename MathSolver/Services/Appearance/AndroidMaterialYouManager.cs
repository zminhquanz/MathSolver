#if ANDROID
using Android.App;
using Android.OS;
using Android.Util;
using Google.Android.Material.Color;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace MathSolver.Services;

/// <summary>
/// Android-only Material You integration. Dynamic Color is deliberately opt-in
/// so the Math Solver accent remains the default. When enabled on Android 12+
/// the Material dynamic-color overlay is applied before MAUI inflates the
/// activity, then AppThemeManager reads the semantic Material colors back into
/// the shared MAUI resource palette.
/// </summary>
public static class AndroidMaterialYouManager
{
    private const string DynamicColorPreferenceKey =
        "android_material_dynamic_color";

    public static bool IsDynamicColorSupported =>
        Build.VERSION.SdkInt >= BuildVersionCodes.S;

    public static bool IsDynamicColorEnabled =>
        IsDynamicColorSupported &&
        Preferences.Default.Get(
            DynamicColorPreferenceKey,
            false);

    public static void ApplyDynamicColorsIfEnabled(
        Activity activity)
    {
        if (!IsDynamicColorEnabled)
        {
            return;
        }

        DynamicColors.ApplyToActivityIfAvailable(activity);
    }

    public static void SetDynamicColorEnabled(
        bool enabled)
    {
        bool effectiveValue =
            enabled && IsDynamicColorSupported;
        bool previousValue =
            IsDynamicColorEnabled;

        Preferences.Default.Set(
            DynamicColorPreferenceKey,
            effectiveValue);

        if (previousValue == effectiveValue)
        {
            return;
        }

        // DynamicColors must be installed before the Android view hierarchy is
        // inflated. Recreate only the Android activity; the Windows path never
        // references this service.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Platform.CurrentActivity is Activity activity)
            {
                activity.Recreate();
            }
        });
    }

    public static void ResetToDefault()
    {
        Preferences.Default.Set(
            DynamicColorPreferenceKey,
            false);
    }

    public static bool TryGetCurrentColorScheme(
        out AndroidMaterialColorScheme scheme)
    {
        scheme = default!;

        if (!IsDynamicColorEnabled ||
            Platform.CurrentActivity is not Activity activity)
        {
            return false;
        }

        if (!TryResolveThemeColor(activity, "colorPrimary", out Color primary) ||
            !TryResolveThemeColor(activity, "colorOnPrimary", out Color onPrimary) ||
            !TryResolveThemeColor(activity, "colorSurface", out Color surface) ||
            !TryResolveThemeColor(activity, "colorOnSurface", out Color onSurface))
        {
            return false;
        }

        TryResolveThemeColor(activity, "colorPrimaryContainer", out Color primaryContainer);
        TryResolveThemeColor(activity, "colorOnSurfaceVariant", out Color onSurfaceVariant);
        TryResolveThemeColor(activity, "colorSurfaceVariant", out Color surfaceVariant);
        TryResolveThemeColor(activity, "colorSurfaceContainerLow", out Color surfaceContainerLow);
        TryResolveThemeColor(activity, "colorSurfaceContainer", out Color surfaceContainer);
        TryResolveThemeColor(activity, "colorSurfaceContainerHigh", out Color surfaceContainerHigh);
        TryResolveThemeColor(activity, "colorOutline", out Color outline);
        TryResolveThemeColor(activity, "colorOutlineVariant", out Color outlineVariant);
        TryResolveThemeColor(activity, "colorError", out Color error);
        TryResolveThemeColor(activity, "colorErrorContainer", out Color errorContainer);

        primaryContainer = Fallback(primaryContainer, primary);
        onSurfaceVariant = Fallback(onSurfaceVariant, onSurface);
        surfaceVariant = Fallback(surfaceVariant, surface);
        surfaceContainerLow = Fallback(surfaceContainerLow, surface);
        surfaceContainer = Fallback(surfaceContainer, surfaceVariant);
        surfaceContainerHigh = Fallback(surfaceContainerHigh, surfaceContainer);
        outline = Fallback(outline, onSurfaceVariant);
        outlineVariant = Fallback(outlineVariant, outline);
        error = Fallback(error, Color.FromArgb("#B3261E"));
        errorContainer = Fallback(errorContainer, Color.FromArgb("#F9DEDC"));

        scheme = new AndroidMaterialColorScheme(
            Primary: primary,
            OnPrimary: onPrimary,
            PrimaryContainer: primaryContainer,
            Surface: surface,
            SurfaceContainerLow: surfaceContainerLow,
            SurfaceContainer: surfaceContainer,
            SurfaceContainerHigh: surfaceContainerHigh,
            SurfaceVariant: surfaceVariant,
            OnSurface: onSurface,
            OnSurfaceVariant: onSurfaceVariant,
            Outline: outline,
            OutlineVariant: outlineVariant,
            Error: error,
            ErrorContainer: errorContainer);

        return true;
    }

    private static bool TryResolveThemeColor(
        Activity activity,
        string attributeName,
        out Color color)
    {
        color = Colors.Transparent;

        int attributeId =
            activity.Resources?.GetIdentifier(
                attributeName,
                "attr",
                activity.PackageName) ?? 0;

        if (attributeId == 0)
        {
            return false;
        }

        var typedValue = new TypedValue();

        if (!activity.Theme.ResolveAttribute(
                attributeId,
                typedValue,
                true))
        {
            return false;
        }

        int argb;

        try
        {
            argb = typedValue.ResourceId != 0
                ? activity.Resources!.GetColor(
                    typedValue.ResourceId,
                    activity.Theme).ToArgb()
                : typedValue.Data;
        }
        catch
        {
            argb = typedValue.Data;
        }

        byte alpha = (byte)((argb >> 24) & 0xFF);
        byte red = (byte)((argb >> 16) & 0xFF);
        byte green = (byte)((argb >> 8) & 0xFF);
        byte blue = (byte)(argb & 0xFF);

        color = Color.FromRgba(red, green, blue, alpha);
        return true;
    }

    private static Color Fallback(
        Color candidate,
        Color fallback)
    {
        return candidate.Alpha <= 0f
            ? fallback
            : candidate;
    }
}

public sealed record AndroidMaterialColorScheme(
    Color Primary,
    Color OnPrimary,
    Color PrimaryContainer,
    Color Surface,
    Color SurfaceContainerLow,
    Color SurfaceContainer,
    Color SurfaceContainerHigh,
    Color SurfaceVariant,
    Color OnSurface,
    Color OnSurfaceVariant,
    Color Outline,
    Color OutlineVariant,
    Color Error,
    Color ErrorContainer);
#endif
