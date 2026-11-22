using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text;

namespace MathSolver.Services;

public sealed record QuizContentText(string Id, string Text, string[] Variables,
    Dictionary<string, string>? TextByUnit = null)
{
    internal string ForUnit(string unit) => TextByUnit?.TryGetValue(unit, out var text) == true ? text : Text;
}

/// <summary>Language content only. Mathematical rules and accepted contracts remain in C#.</summary>
public sealed class QuizContentPack
{
    public int Version { get; init; }
    public string Culture { get; init; } = "";
    public List<QuizContentText> Texts { get; init; } = [];
    public Dictionary<string, JsonElement> Lists { get; init; } = [];
}

/// <summary>Cached JSON lists and keyed templates shared by generators on every platform.</summary>
public static partial class QuizContentCatalog
{
    public const string FallbackCulture = "en-US";
    private const string ResourcePrefix = "QuizContent.";
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static readonly ConcurrentDictionary<string, Lazy<QuizContentPack>> Packs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<(string Culture, string List, Type Type), object> Lists = new();
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, QuizContentText>> Texts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<JsonDocument> CatalogDocument = new(() =>
    {
        using var stream = typeof(QuizContentCatalog).Assembly.GetManifestResourceStream(ResourcePrefix + "catalogues.json")
            ?? throw new InvalidDataException("Missing quiz catalogues.");
        var document = JsonDocument.Parse(stream);
        if (document.RootElement.GetProperty("Version").GetInt32() != 1)
        {
            document.Dispose();
            throw new InvalidDataException("Unsupported quiz catalogue version.");
        }
        return document;
    }, LazyThreadSafetyMode.ExecutionAndPublication);
    [ThreadStatic] private static StringBuilder? s_renderBuilder;
    private static readonly HashSet<string> FactVariables = ["a", "b", "name", "other", "unit", "group", "group_one",
        "groups", "unit_a", "unit_b", "part_a", "part_b", "quantity", "person", "0", "1", "2", "3"];

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { MaxDepth = 32, PropertyNameCaseInsensitive = false };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }

    // Compatibility bridge for the existing two-language math contracts. The content API also accepts culture codes.
    public static string Culture(AppLanguage language) => language switch
    {
        AppLanguage.Vietnamese => "vi-VN",
        AppLanguage.English => "en-US",
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    internal static AppLanguage CompatibilityLanguage(bool vietnamese)
        => vietnamese ? AppLanguage.Vietnamese : AppLanguage.English;

    public static QuizContentPack ReadPack(Stream stream)
    {
        if (stream.CanSeek && stream.Length > 20 * 1024 * 1024)
            throw new InvalidDataException("Quiz content packs must not exceed 20 MB.");
        var pack = JsonSerializer.Deserialize<QuizContentPack>(stream, Options)
            ?? throw new InvalidDataException("Empty quiz content pack.");
        Validate(pack);
        return pack;
    }

    public static void Validate(QuizContentPack pack)
    {
        if (pack.Version != 1 || string.IsNullOrWhiteSpace(pack.Culture) || pack.Texts is null || pack.Lists is null)
            throw new InvalidDataException("Quiz content requires Version 1 and a Culture code.");
        _ = CultureInfo.GetCultureInfo(pack.Culture);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in pack.Texts)
        {
            if (text is null || string.IsNullOrWhiteSpace(text.Id) || text.Text is null || text.Variables is null || !ids.Add(text.Id))
                throw new InvalidDataException("Missing or duplicate quiz text ID.");
            var variables = new HashSet<string>(StringComparer.Ordinal);
            foreach (string variable in text.Variables)
            {
                // A language may omit a descriptive value used by another translation.
                // The values supplied by C# still use the same declared slot set.
                if (string.IsNullOrWhiteSpace(variable) || !variables.Add(variable)
                    || !Regex.IsMatch(variable, @"^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant))
                    throw new InvalidDataException($"Invalid variable '{variable}' in '{text.Id}'.");
            }
            foreach (Match match in Placeholders().Matches(text.Text))
                if (!variables.Contains(match.Groups[1].Value) && !FactVariables.Contains(match.Groups[1].Value))
                    throw new InvalidDataException($"Unknown placeholder '{match.Groups[1].Value}' in '{text.Id}'.");
            if (text.TextByUnit is not null)
                foreach (var (unit, variant) in text.TextByUnit)
                {
                    if (string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(variant)
                        || !Placeholders().Matches(text.Text).Select(m => m.Value)
                            .SequenceEqual(Placeholders().Matches(variant).Select(m => m.Value)))
                        throw new InvalidDataException($"Invalid unit-specific text in '{text.Id}'.");
                }
        }
        foreach (var list in pack.Lists)
            if (string.IsNullOrWhiteSpace(list.Key) || list.Value.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"'{list.Key}' must be a JSON array.");
        QuizContentValidation.CheckLists(pack);
    }

    private static QuizContentPack Pack(string culture)
    {
        string normalized = CultureInfo.GetCultureInfo(culture).Name;
        return Packs.GetOrAdd(normalized, name => new(() => {
            using var stream = typeof(QuizContentCatalog).Assembly.GetManifestResourceStream(ResourcePrefix + name + ".json");
            // Fall back as a complete pack, never silently accept an invalid installed pack.
            if (stream is null) return name == FallbackCulture
                ? throw new InvalidDataException("Missing fallback quiz content pack.") : Pack(FallbackCulture);
            var pack = ReadPack(stream);
            if (!string.Equals(pack.Culture, name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Quiz content culture does not match '{name}'.");
            return pack;
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public static IReadOnlyList<T> LoadList<T>(string name, string? culture = null)
    {
        string key = culture is null ? "catalogues" : CultureInfo.GetCultureInfo(culture).Name;
        return (IReadOnlyList<T>)Lists.GetOrAdd((key, name, typeof(T)), static (cacheKey, requestedCulture) => {
            string name = cacheKey.List;
            JsonElement data;
            if (requestedCulture is null)
            {
                data = CatalogDocument.Value.RootElement.GetProperty("Lists").GetProperty(name);
            }
            else
            {
                if (!Pack(requestedCulture).Lists.TryGetValue(name, out data) && !Pack(FallbackCulture).Lists.TryGetValue(name, out data))
                    throw new InvalidDataException($"Missing quiz list '{name}'.");
            }
            var values = data.Deserialize<List<T>>(Options) ?? throw new InvalidDataException($"Invalid quiz list '{name}'.");
            if (values.Count == 0 || values.Any(value => value is null)) throw new InvalidDataException($"Empty quiz list '{name}'.");
            return values.AsReadOnly();
        }, culture);
    }

    public static string Text(AppLanguage language, string id, params (string Name, string Value)[] values)
        => Text(Culture(language), id, values);

    public static string Text(string culture, string id, params (string Name, string Value)[] values)
        => Render(Entry(culture, id), "", values);

    public static string TextForUnit(AppLanguage language, string unit, string id,
        params (string Name, string Value)[] values)
        => Render(Entry(Culture(language), id), unit, values);

    private static string Render(QuizContentText entry, string unit, (string Name, string Value)[] values)
    {
        string id = entry.Id, template = entry.ForUnit(unit);
        ReadOnlySpan<(string Name, string Value)> supplied = values;
        // The packs are validated when loaded. Per-render slots are short, so
        // a contiguous scan avoids two temporary hash collections per question.
        for (int i = 0; i < supplied.Length; i++)
        {
            if (supplied[i].Name is null) throw new ArgumentNullException(nameof(values));
            for (int j = 0; j < i; j++)
                if (string.Equals(supplied[i].Name, supplied[j].Name, StringComparison.Ordinal))
                    throw new ArgumentException("Duplicate quiz text variable.", nameof(values));
        }
        bool valid = supplied.Length == entry.Variables.Length;
        foreach (string expected in entry.Variables)
        {
            bool found = false;
            foreach (var value in supplied)
                if (string.Equals(expected, value.Name, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            if (!found) valid = false;
        }
        if (!valid)
            throw new InvalidDataException($"Values do not match the variables of '{id}'.");
        // Enumerate matches over the original text, so substituted braces cannot
        // be interpreted as another placeholder. Keep the scratch buffer thread-local.
        StringBuilder? builder = null;
        string rendered;
        int copiedThrough = 0;
        try
        {
            foreach (var match in Placeholders().EnumerateMatches(template))
            {
                if (builder is null)
                {
                    builder = s_renderBuilder ?? new StringBuilder(128);
                    s_renderBuilder = null;
                }
                builder.Append(template.AsSpan(copiedThrough, match.Index - copiedThrough));
                ReadOnlySpan<char> name = template.AsSpan(match.Index + 1, match.Length - 2);
                bool substituted = false;
                foreach (var value in supplied)
                {
                    if (!name.SequenceEqual(value.Name.AsSpan())) continue;
                    builder.Append(value.Value);
                    substituted = true;
                    break;
                }
                if (!substituted) builder.Append(template.AsSpan(match.Index, match.Length));
                copiedThrough = match.Index + match.Length;
            }

            if (builder is null) rendered = template;
            else
            {
                builder.Append(template.AsSpan(copiedThrough));
                rendered = builder.ToString();
            }
        }
        finally
        {
            if (builder is not null)
            {
                builder.Clear();
                if (builder.Capacity <= 2048) s_renderBuilder = builder;
            }
        }
        if (QuizNarrativeCapture.Current is { } capture)
        {
            var arguments = new Dictionary<string, string>(values.Length, StringComparer.Ordinal);
            foreach (var (name, value) in values) arguments.Add(name, value);
            capture.Record(template, arguments, rendered);
        }
        return rendered;
    }

    internal static QuizContentText Entry(string culture, string id)
    {
        culture = CultureInfo.GetCultureInfo(culture).Name;
        IReadOnlyDictionary<string, QuizContentText> Entries(string code) => Texts.GetOrAdd(code,
            name => Pack(name).Texts.ToDictionary(t => t.Id, StringComparer.Ordinal));
        if (!Entries(culture).TryGetValue(id, out var entry) && !Entries(FallbackCulture).TryGetValue(id, out entry))
            throw new InvalidDataException($"Missing quiz text '{id}'.");
        return entry;
    }

    [GeneratedRegex(@"(?<!\{)\{([A-Za-z0-9_.-]+)\}(?!\})", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholders();
}
