namespace MathSolver.Services;

public sealed record AppLanguageOption(
    AppLanguage Language,
    string LocalizationKey)
{
    public string LocalizedDisplayName =>
        LocalizationService.TranslateKey(
            LocalizationKey);

    public override string ToString() =>
        LocalizedDisplayName;
}

public static class AppLanguageCatalog
{
    public static IReadOnlyList<AppLanguageOption> Options { get; } =
    [
        new(
            AppLanguage.Vietnamese,
            "Language.Vietnamese"),

        new(
            AppLanguage.English,
            "Language.English")
    ];

    public static AppLanguageOption GetByLanguage(
        AppLanguage language) =>
        Options.First(
            option =>
                option.Language == language);
}


public enum AppLanguage
{
    Vietnamese,
    English
}

public static class AppLanguageManager
{
    private const string LanguagePreferenceKey =
        "app_language";

    private static bool _initialized;

    public static event EventHandler? LanguageChanged;

    public static AppLanguage CurrentLanguage { get; private set; } =
        AppLanguage.English;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        string storedValue =
            Preferences.Default.Get(
                LanguagePreferenceKey,
                AppLanguage.English.ToString());

        if (!Enum.TryParse(
                storedValue,
                ignoreCase: true,
                out AppLanguage language))
        {
            language =
                AppLanguage.English;
        }

        CurrentLanguage =
            language;
    }

    public static bool SetLanguage(
        AppLanguage language)
    {
        Initialize();

        if (CurrentLanguage == language)
        {
            return false;
        }

        CurrentLanguage =
            language;

        Preferences.Default.Set(
            LanguagePreferenceKey,
            language.ToString());

        LanguageChanged?.Invoke(
            null,
            EventArgs.Empty);

        return true;
    }

    public static void ResetToDefault()
    {
        SetLanguage(
            AppLanguage.English);
    }
}
