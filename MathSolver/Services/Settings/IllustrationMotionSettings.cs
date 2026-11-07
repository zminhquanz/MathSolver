using Microsoft.Maui.Storage;

namespace MathSolver.Services;

/// <summary>Guide illustrations only; this never overrides the system's motion preference.</summary>
public static class IllustrationMotionSettings
{
    private const string PreferenceKey = "illustration_motion_enabled";
    private static int _disableVersion;
    internal static int DisableVersion => Volatile.Read(ref _disableVersion);
    public static bool IsEnabled => Preferences.Default.Get(PreferenceKey, true);
    public static event EventHandler? Changed;
    public static void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled) return;
        Preferences.Default.Set(PreferenceKey, enabled);
        if (!enabled) Interlocked.Increment(ref _disableVersion);
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
