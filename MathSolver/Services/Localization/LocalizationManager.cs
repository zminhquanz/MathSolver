using MathSolver.Models.Localization;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace MathSolver.Services.Localization;

public sealed class LocalizationManager :
    INotifyPropertyChanged
{
    private const string SelectedCulturePreferenceKey =
        "Localization.SelectedCulture";

    private sealed record CompiledDynamicRule(
        LocalizationDynamicRule Definition,
        Regex Regex,
        string[] GroupNames);

    private readonly SemaphoreSlim _gate =
        new(
            1,
            1);

    private readonly JsonLocalizationProvider _provider =
        new();

    private readonly LanguagePackValidator _validator =
        new();

    private LocalizationManifest? _manifest;
    private LocalizationCatalog? _catalog;
    private LanguagePack? _fallbackPack;
    private LanguagePack? _currentPack;

    private Dictionary<string, string> _exactSourceKeys =
        new(
            StringComparer.Ordinal);

    private Dictionary<string, string> _capturedTermKeys =
        new(
            StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<LocalizationCatalogEntry> _phraseEntries =
        [];

    private IReadOnlyList<CompiledDynamicRule> _dynamicRules =
        [];

    private bool _initialized;

    public static LocalizationManager Instance { get; } =
        new();

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public event EventHandler?
        CultureChanged;

    public string CurrentCulture =>
        _currentPack?.Culture ??
        _fallbackPack?.Culture ??
        DefaultCulture;

    /// <summary>
    /// Culture selected when the application has never stored a user choice.
    /// This is intentionally separate from SourceCulture.
    /// </summary>
    public string DefaultCulture =>
        _manifest?.DefaultCulture ??
        "en-US";

    /// <summary>
    /// Culture used by the literal source text in the current XAML/codebase.
    /// The compatibility translator must return source text unchanged only
    /// for this culture, not for the startup default culture.
    /// </summary>
    public string SourceCulture =>
        _catalog?.SourceCulture ??
        "vi-VN";

    public string this[string key] =>
        TranslateKey(
            key);

    private LocalizationManager()
    {
    }

    public async Task InitializeAsync(
        string? preferredCulture = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(
            cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (!_initialized)
            {
                _manifest =
                    await _provider
                        .LoadManifestAsync(
                            cancellationToken)
                        .ConfigureAwait(false);

                _catalog =
                    await _provider
                        .LoadCatalogAsync(
                            cancellationToken)
                        .ConfigureAwait(false);

                _fallbackPack =
                    await _provider
                        .LoadLanguagePackAsync(
                            _catalog.SourceCulture,
                            cancellationToken)
                        .ConfigureAwait(false);

                BuildIndexes();

                _initialized =
                    true;
            }

            string storedCulture =
                Preferences.Default.Get(
                    SelectedCulturePreferenceKey,
                    DefaultCulture);

            string culture =
                string.IsNullOrWhiteSpace(
                    preferredCulture)
                    ? storedCulture
                    : preferredCulture;

            await LoadCurrentPackCoreAsync(
                culture,
                cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetCultureAsync(
        string culture,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken)
            .ConfigureAwait(false);

        await _gate.WaitAsync(
            cancellationToken)
            .ConfigureAwait(false);

        bool changed =
            false;

        try
        {
            string normalizedCulture =
                JsonLocalizationProvider
                    .NormalizeCulture(
                        culture);

            changed =
                !string.Equals(
                    CurrentCulture,
                    normalizedCulture,
                    StringComparison.OrdinalIgnoreCase);

            await LoadCurrentPackCoreAsync(
                normalizedCulture,
                cancellationToken)
                .ConfigureAwait(false);

            if (string.Equals(
                    CurrentCulture,
                    normalizedCulture,
                    StringComparison.OrdinalIgnoreCase))
            {
                Preferences.Default.Set(
                    SelectedCulturePreferenceKey,
                    normalizedCulture);
            }
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
        {
            RaiseCultureChanged();
        }
    }

    public async Task<IReadOnlyList<LanguageOption>>
        GetAvailableLanguagesAsync(
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken)
            .ConfigureAwait(false);

        return await _provider
            .GetAvailableLanguagesAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LanguagePackValidationResult>
        ImportLanguagePackAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            cancellationToken)
            .ConfigureAwait(false);

        LanguagePack candidate =
            await _provider
                .DeserializeLanguagePackAsync(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);

        LanguagePackValidationResult validation =
            _validator.Validate(
                candidate,
                _fallbackPack!,
                _catalog!);

        if (!validation.IsValid)
        {
            return validation;
        }

        await _provider
            .SaveImportedPackAsync(
                candidate,
                cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(
                CurrentCulture,
                candidate.Culture,
                StringComparison.OrdinalIgnoreCase))
        {
            await _gate.WaitAsync(
                cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await LoadCurrentPackCoreAsync(
                    candidate.Culture,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            RaiseCultureChanged();
        }

        return validation;
    }

    public bool DeleteImportedLanguagePack(
        string culture)
    {
        return _provider.DeleteImportedPack(
            culture);
    }

    public LanguagePackValidationResult ValidatePack(
        LanguagePack pack)
    {
        EnsureInitialized();

        return _validator.Validate(
            pack,
            _fallbackPack!,
            _catalog!);
    }

    public string TranslateKey(
        string? key)
    {
        if (string.IsNullOrWhiteSpace(
                key))
        {
            return string.Empty;
        }

        EnsureInitialized();

        return GetStringValue(
            key);
    }

    public string TranslateSource(
        string? source)
    {
        if (string.IsNullOrEmpty(
                source))
        {
            return source ??
                   string.Empty;
        }

        EnsureInitialized();

        if (string.Equals(
                CurrentCulture,
                SourceCulture,
                StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        return TranslateSourceCore(
            source,
            includeDynamicRules: true);
    }

    public string FormatKey(
        string key,
        IReadOnlyDictionary<string, object?> values)
    {
        string template =
            TranslateKey(
                key);

        return LocalizedTemplateFormatter.Format(
            template,
            values,
            TranslateCapturedValue);
    }

    public string FormatTemplate(
        string templateKey,
        IReadOnlyDictionary<string, object?> values)
    {
        EnsureInitialized();

        string template =
            GetTemplateValue(
                templateKey);

        return LocalizedTemplateFormatter.Format(
            template,
            values,
            TranslateCapturedValue);
    }

    private async Task EnsureInitializedAsync(
        CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await InitializeAsync(
            cancellationToken:
                cancellationToken)
            .ConfigureAwait(false);
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        InitializeAsync()
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    private async Task LoadCurrentPackCoreAsync(
        string culture,
        CancellationToken cancellationToken)
    {
        string normalizedCulture =
            JsonLocalizationProvider
                .NormalizeCulture(
                    culture);

        try
        {
            _currentPack =
                await _provider
                    .LoadLanguagePackAsync(
                        normalizedCulture,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            _currentPack =
                _fallbackPack;
        }
        catch (InvalidDataException)
        {
            _currentPack =
                _fallbackPack;
        }
    }

    private void BuildIndexes()
    {
        LocalizationCatalog catalog =
            _catalog ??
            throw new InvalidOperationException(
                "The localization catalog has not been loaded.");

        _exactSourceKeys =
            catalog.Entries
                .Where(
                    entry =>
                        entry.Mode ==
                        LocalizationEntryMode.Exact)
                .GroupBy(
                    entry =>
                        entry.Source,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.Last().Key,
                    StringComparer.Ordinal);

        _phraseEntries =
            catalog.Entries
                .Where(
                    entry =>
                        entry.Mode ==
                        LocalizationEntryMode.Phrase)
                .GroupBy(
                    entry =>
                        entry.Source,
                    StringComparer.Ordinal)
                .Select(
                    group =>
                        group.First())
                .OrderByDescending(
                    entry =>
                        entry.Source.Length)
                .ToArray();

        _capturedTermKeys =
            catalog.CapturedTerms
                .GroupBy(
                    term =>
                        term.Source,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.Last().Key,
                    StringComparer.OrdinalIgnoreCase);

        _dynamicRules =
            catalog.DynamicRules
                .Select(
                    definition =>
                    {
                        RegexOptions options =
                            RegexOptions.CultureInvariant;

                        if (definition.IgnoreCase)
                        {
                            options |=
                                RegexOptions.IgnoreCase;
                        }

                        var regex =
                            new Regex(
                                definition.Pattern,
                                options);

                        return new CompiledDynamicRule(
                            definition,
                            regex,
                            regex.GetGroupNames());
                    })
                .ToArray();
    }

    private string TranslateSourceCore(
        string source,
        bool includeDynamicRules)
    {
        if (_exactSourceKeys.TryGetValue(
                source,
                out string? exactKey))
        {
            return GetStringValue(
                exactKey);
        }

        string translated =
            source;

        if (includeDynamicRules)
        {
            foreach (CompiledDynamicRule rule
                     in _dynamicRules)
            {
                string template =
                    GetTemplateValue(
                        rule.Definition.TemplateKey);

                translated =
                    rule.Regex.Replace(
                        translated,
                        match =>
                        {
                            var values =
                                new Dictionary<string, object?>(
                                    StringComparer.Ordinal);

                            foreach (string groupName
                                     in rule.GroupNames)
                            {
                                Group group =
                                    match.Groups[
                                        groupName];

                                if (group.Success)
                                {
                                    values[groupName] =
                                        group.Value;
                                }
                            }

                            return LocalizedTemplateFormatter.Format(
                                template,
                                values,
                                TranslateCapturedValue);
                        });
            }
        }

        foreach (LocalizationCatalogEntry phrase
                 in _phraseEntries)
        {
            if (!translated.Contains(
                    phrase.Source,
                    StringComparison.Ordinal))
            {
                continue;
            }

            translated =
                translated.Replace(
                    phrase.Source,
                    GetStringValue(
                        phrase.Key),
                    StringComparison.Ordinal);
        }

        return translated;
    }

    private string TranslateCapturedValue(
        string source)
    {
        string normalized =
            source.Trim();

        bool capitalizeResult =
            normalized.Length > 0 &&
            char.IsUpper(
                normalized[0]);

        string translated;

        if (_capturedTermKeys.TryGetValue(
                normalized,
                out string? termKey))
        {
            translated =
                GetStringValue(
                    termKey);
        }
        else
        {
            translated =
                TranslateSourceCore(
                    normalized,
                    includeDynamicRules: false);
        }

        if (!capitalizeResult ||
            string.IsNullOrEmpty(
                translated))
        {
            return translated;
        }

        return char.ToUpper(
                   translated[0],
                   CultureInfo.CurrentCulture) +
               translated[1..];
    }

    private string GetStringValue(
        string key)
    {
        if (_currentPack?.Strings.TryGetValue(
                key,
                out string? currentValue) == true &&
            !string.IsNullOrWhiteSpace(
                currentValue))
        {
            return currentValue;
        }

        // Stable quiz/LLM strings are kept as a code fallback so an older
        // imported language pack cannot surface raw [Quiz.*] keys when the
        // app adds new controls. A custom pack still wins when it contains
        // the key because _currentPack is checked first.
        if (QuizLocalizationOverrides.TryGetValue(
                key,
                CurrentCulture,
                out string overrideValue) &&
            !string.IsNullOrWhiteSpace(
                overrideValue))
        {
            return overrideValue;
        }

        if (_fallbackPack?.Strings.TryGetValue(
                key,
                out string? fallbackValue) == true)
        {
            return fallbackValue;
        }

        return $"[{key}]";
    }

    private string GetTemplateValue(
        string key)
    {
        if (_currentPack?.Templates.TryGetValue(
                key,
                out string? currentValue) == true &&
            !string.IsNullOrWhiteSpace(
                currentValue))
        {
            return currentValue;
        }

        if (_fallbackPack?.Templates.TryGetValue(
                key,
                out string? fallbackValue) == true)
        {
            return fallbackValue;
        }

        return $"[{key}]";
    }

    private void RaiseCultureChanged()
    {
        void Raise()
        {
            OnPropertyChanged(
                "Item[]");

            OnPropertyChanged(
                nameof(CurrentCulture));

            CultureChanged?.Invoke(
                this,
                EventArgs.Empty);
        }

        if (MainThread.IsMainThread)
        {
            Raise();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(
                Raise);
        }
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}
