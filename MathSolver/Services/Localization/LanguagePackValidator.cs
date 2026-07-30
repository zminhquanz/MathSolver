using MathSolver.Models.Localization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services.Localization;

public sealed class LanguagePackValidationResult
{
    public List<string> Errors { get; } =
        [];

    public List<string> Warnings { get; } =
        [];

    public bool IsValid =>
        Errors.Count == 0;
}

public sealed partial class LanguagePackValidator
{
    public const int SupportedSchemaVersion =
        1;

    public LanguagePackValidationResult Validate(
        LanguagePack candidate,
        LanguagePack fallback,
        LocalizationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(
            candidate);

        ArgumentNullException.ThrowIfNull(
            fallback);

        ArgumentNullException.ThrowIfNull(
            catalog);

        var result =
            new LanguagePackValidationResult();

        if (candidate.SchemaVersion !=
            SupportedSchemaVersion)
        {
            result.Errors.Add(
                $"Unsupported schemaVersion {candidate.SchemaVersion}. " +
                $"Expected {SupportedSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(
                candidate.Culture))
        {
            result.Errors.Add(
                "The culture field is required.");
        }
        else
        {
            try
            {
                _ =
                    CultureInfo.GetCultureInfo(
                        candidate.Culture);
            }
            catch (CultureNotFoundException)
            {
                result.Errors.Add(
                    $"'{candidate.Culture}' is not a valid culture name.");
            }
        }

        if (string.IsNullOrWhiteSpace(
                candidate.LanguageName))
        {
            result.Errors.Add(
                "The languageName field is required.");
        }

        if (string.IsNullOrWhiteSpace(
                candidate.NativeName))
        {
            result.Errors.Add(
                "The nativeName field is required.");
        }

        ValidateDictionary(
            "strings",
            candidate.Strings,
            fallback.Strings,
            result);

        ValidateDictionary(
            "templates",
            candidate.Templates,
            fallback.Templates,
            result);

        HashSet<string> catalogKeys =
            catalog.Entries
                .Select(
                    entry =>
                        entry.Key)
                .Concat(
                    catalog.CapturedTerms.Select(
                        term =>
                            term.Key))
                .ToHashSet(
                    StringComparer.Ordinal);

        foreach (string key
                 in candidate.Strings.Keys)
        {
            if (!catalogKeys.Contains(
                    key))
            {
                result.Warnings.Add(
                    $"Unknown string key: {key}");
            }
        }

        HashSet<string> templateKeys =
            catalog.DynamicRules
                .Select(
                    rule =>
                        rule.TemplateKey)
                .ToHashSet(
                    StringComparer.Ordinal);

        foreach (string key
                 in candidate.Templates.Keys)
        {
            if (!templateKeys.Contains(
                    key))
            {
                result.Warnings.Add(
                    $"Unknown template key: {key}");
            }
        }

        return result;
    }

    private static void ValidateDictionary(
        string sectionName,
        IReadOnlyDictionary<string, string> candidate,
        IReadOnlyDictionary<string, string> fallback,
        LanguagePackValidationResult result)
    {
        foreach ((string key, string sourceValue)
                 in fallback)
        {
            if (!candidate.TryGetValue(
                    key,
                    out string? translatedValue))
            {
                result.Warnings.Add(
                    $"Missing {sectionName} key: {key}");

                continue;
            }

            if (string.IsNullOrWhiteSpace(
                    translatedValue))
            {
                result.Warnings.Add(
                    $"Empty translation for {sectionName} key: {key}");

                continue;
            }

            HashSet<string> expectedPlaceholders =
                ExtractPlaceholders(
                    sourceValue);

            HashSet<string> actualPlaceholders =
                ExtractPlaceholders(
                    translatedValue);

            if (!expectedPlaceholders.SetEquals(
                    actualPlaceholders))
            {
                string expected =
                    string.Join(
                        ", ",
                        expectedPlaceholders.OrderBy(value => value));

                string actual =
                    string.Join(
                        ", ",
                        actualPlaceholders.OrderBy(value => value));

                result.Errors.Add(
                    $"Placeholder mismatch for {sectionName} key '{key}'. " +
                    $"Expected [{expected}], found [{actual}].");
            }
        }
    }

    private static HashSet<string> ExtractPlaceholders(
        string value)
    {
        var placeholders =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (Match match
                 in PlaceholderRegex().Matches(
                     value))
        {
            placeholders.Add(
                match.Groups["name"].Value);
        }

        return placeholders;
    }

    [GeneratedRegex(
        @"(?<!\{)\{(?<name>[A-Za-z0-9_.-]+)(?:\|translate)?(?::[^{}]+)?\}(?!\})",
        RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
