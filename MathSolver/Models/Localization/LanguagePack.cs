using System.Text.Json.Serialization;

namespace MathSolver.Models.Localization;

public sealed class LanguagePack
{
    public int SchemaVersion { get; init; } = 1;

    public string Culture { get; init; } = string.Empty;

    public string LanguageName { get; init; } = string.Empty;

    public string NativeName { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public string AppVersion { get; init; } = string.Empty;

    public Dictionary<string, string> Strings { get; init; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, string> Templates { get; init; } =
        new(StringComparer.Ordinal);
}

public sealed class LocalizationCatalog
{
    public int SchemaVersion { get; init; } = 1;

    public string SourceCulture { get; init; } = "vi-VN";

    public List<LocalizationCatalogEntry> Entries { get; init; } =
        [];

    public List<LocalizationCapturedTerm> CapturedTerms { get; init; } =
        [];

    public List<LocalizationDynamicRule> DynamicRules { get; init; } =
        [];
}

public sealed class LocalizationCatalogEntry
{
    public string Key { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LocalizationEntryMode Mode { get; init; }
}

public enum LocalizationEntryMode
{
    Exact,
    Phrase
}

public sealed class LocalizationCapturedTerm
{
    public string Key { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;
}

public sealed class LocalizationDynamicRule
{
    public string Id { get; init; } = string.Empty;

    public string Pattern { get; init; } = string.Empty;

    public string TemplateKey { get; init; } = string.Empty;

    public bool IgnoreCase { get; init; }
}

public sealed class LocalizationManifest
{
    public int SchemaVersion { get; init; } = 1;

    public string DefaultCulture { get; init; } = "en-US";

    public List<LanguageManifestEntry> Languages { get; init; } =
        [];
}

public sealed class LanguageManifestEntry
{
    public string Culture { get; init; } = string.Empty;

    public string File { get; init; } = string.Empty;

    public bool BuiltIn { get; init; } = true;
}

public sealed record LanguageOption(
    string Culture,
    string LanguageName,
    string NativeName,
    string Author,
    bool IsBuiltIn);
