namespace MathSolver.Services;

public static class AppFontManager
{
    private const string FontPreferenceKey =
        "app_font_key";

    private static bool _initialized;
    private static Application? _application;

    public static event EventHandler? FontChanged;

    public static string CurrentFontKey { get; private set; } =
        AppFontCatalog.DefaultFontKey;

    public static AppFontOption CurrentFont =>
        AppFontCatalog.GetByKey(
            CurrentFontKey);

    public static void Initialize(
        Application application)
    {
        ArgumentNullException.ThrowIfNull(
            application);

        if (_initialized)
        {
            return;
        }

        _initialized =
            true;

        _application =
            application;

        string storedKey =
            Preferences.Default.Get(
                FontPreferenceKey,
                AppFontCatalog.DefaultFontKey);

        CurrentFontKey =
            AppFontCatalog
                .GetByKey(storedKey)
                .Key;

        ApplyCurrentFont(
            savePreference: false);
    }

    public static bool SetFont(
        string? fontKey)
    {
        AppFontOption option =
            AppFontCatalog.GetByKey(
                fontKey);

        if (option.Key ==
            CurrentFontKey)
        {
            return false;
        }

        CurrentFontKey =
            option.Key;

        ApplyCurrentFont(
            savePreference: true);

        return true;
    }

    public static void ResetToDefault()
    {
        CurrentFontKey =
            AppFontCatalog.DefaultFontKey;

        ApplyCurrentFont(
            savePreference: true);
    }

    private static void ApplyCurrentFont(
        bool savePreference)
    {
        void Apply()
        {
            Application? application =
                _application ??
                Application.Current;

            if (application is null)
            {
                return;
            }

            AppFontOption font =
                CurrentFont;

            // Styles.xaml dùng DynamicResource AppFontFamily.
            // Khi key này thay đổi, Label, Button, Entry, Picker...
            // sẽ cập nhật FontFamily ngay trong lúc ứng dụng đang chạy.
            application.Resources["AppFontFamily"] =
                font.FontFamily;

            application.Resources["AppFontDisplayName"] =
                font.DisplayName;

            if (savePreference)
            {
                Preferences.Default.Set(
                    FontPreferenceKey,
                    CurrentFontKey);
            }

            FontChanged?.Invoke(
                null,
                EventArgs.Empty);
        }

        if (MainThread.IsMainThread)
        {
            Apply();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(Apply);
        }
    }
}
