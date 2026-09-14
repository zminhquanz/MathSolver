namespace MathSolver.Services;

/// <summary>
/// Quản lý các công cụ chẩn đoán dành cho nhà phát triển. Giá trị mặc định
/// phụ thuộc cấu hình biên dịch, còn lựa chọn của người dùng được lưu riêng.
/// </summary>
public static class DeveloperModeManager
{
    private const string PreferenceKey =
        "developer_mode_enabled";

#if DEBUG
    public const bool BuildDefaultIsEnabled = true;
#else
    public const bool BuildDefaultIsEnabled = false;
#endif

    private static bool _initialized;

    public static event EventHandler? DeveloperModeChanged;

    public static bool IsEnabled { get; private set; } =
        BuildDefaultIsEnabled;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsEnabled = Preferences.Default.Get(
            PreferenceKey,
            BuildDefaultIsEnabled);
    }

    public static bool SetEnabled(
        bool isEnabled)
    {
        Initialize();

        if (IsEnabled == isEnabled)
        {
            return false;
        }

        IsEnabled = isEnabled;
        Preferences.Default.Set(
            PreferenceKey,
            isEnabled);

        DeveloperModeChanged?.Invoke(
            null,
            EventArgs.Empty);

        return true;
    }

    public static void ResetToDefault()
    {
        Initialize();

        bool changed =
            IsEnabled != BuildDefaultIsEnabled;

        IsEnabled = BuildDefaultIsEnabled;
        Preferences.Default.Remove(
            PreferenceKey);

        if (changed)
        {
            DeveloperModeChanged?.Invoke(
                null,
                EventArgs.Empty);
        }
    }
}
