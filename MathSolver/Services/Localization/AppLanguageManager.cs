using System.Globalization;

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

    private const string SelectedCulturePreferenceKey =
        "Localization.SelectedCulture";

    private static bool _initialized;

    public static event EventHandler? LanguageChanged;

    public static AppLanguage CurrentLanguage { get; private set; } =
        AppLanguage.English;

    public static bool HasStoredLanguage =>
        Preferences.Default.ContainsKey(LanguagePreferenceKey);

    private static AppLanguage SystemDefaultLanguage =>
        string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            "vi",
            StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Vietnamese
            : AppLanguage.English;

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
                SystemDefaultLanguage.ToString());

        // The JSON pack is the active UI language. Keep older screens that
        // still use AppLanguage in sync with a saved pack selection.
        if (Preferences.Default.ContainsKey(SelectedCulturePreferenceKey))
        {
            string selectedCulture = Preferences.Default.Get(
                SelectedCulturePreferenceKey,
                string.Empty);

            try
            {
                storedValue = CultureInfo.GetCultureInfo(selectedCulture)
                    .TwoLetterISOLanguageName switch
                {
                    "vi" => AppLanguage.Vietnamese.ToString(),
                    "en" => AppLanguage.English.ToString(),
                    _ => storedValue
                };
            }
            catch (CultureNotFoundException)
            {
                // Fall back to the legacy preference if the saved culture
                // is no longer valid.
            }
        }

        if (!Enum.TryParse(
                storedValue,
                ignoreCase: true,
                out AppLanguage language))
        {
            language =
                SystemDefaultLanguage;
        }

        CurrentLanguage =
            language;
    }

    public static bool SetLanguage(
        AppLanguage language)
    {
        Initialize();

        bool changed = CurrentLanguage != language;

        CurrentLanguage =
            language;

        Preferences.Default.Set(
            LanguagePreferenceKey,
            language.ToString());

        LanguageChanged?.Invoke(
            null,
            EventArgs.Empty);

        return changed;
    }

    public static void ResetToDefault()
    {
        SetLanguage(
            SystemDefaultLanguage);
    }
}
