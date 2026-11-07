namespace MathSolver.Services;

internal static class MotionPreferences
{
#if WINDOWS
    private static readonly global::Windows.UI.ViewManagement.UISettings WindowsSettings = new();
#endif
    public static bool AllowIllustrationMotion()
    {
        if (!IllustrationMotionSettings.IsEnabled) return false;
        if (Battery.Default.EnergySaverStatus == EnergySaverStatus.On) return false;
#if WINDOWS
        return WindowsSettings.AnimationsEnabled;
#elif ANDROID
        return !OperatingSystem.IsAndroidVersionAtLeast(26) || global::Android.Animation.ValueAnimator.AreAnimatorsEnabled();
#else
        return false; // Static fallback until the platform preference is explicitly supported.
#endif
    }
}
