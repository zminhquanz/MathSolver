using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public sealed record QuizContentText(string Id, string Text, string[] Variables);

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
        return (IReadOnlyList<T>)Lists.GetOrAdd((key, name, typeof(T)), _ => {
            JsonElement data;
            if (culture is null)
            {
                using var stream = typeof(QuizContentCatalog).Assembly.GetManifestResourceStream(ResourcePrefix + "catalogues.json")
                    ?? throw new InvalidDataException("Missing quiz catalogues.");
                using var document = JsonDocument.Parse(stream);
                if (document.RootElement.GetProperty("Version").GetInt32() != 1)
                    throw new InvalidDataException("Unsupported quiz catalogue version.");
                data = document.RootElement.GetProperty("Lists").GetProperty(name).Clone();
            }
            else
            {
                if (!Pack(culture).Lists.TryGetValue(name, out data) && !Pack(FallbackCulture).Lists.TryGetValue(name, out data))
                    throw new InvalidDataException($"Missing quiz list '{name}'.");
            }
            var values = data.Deserialize<List<T>>(Options) ?? throw new InvalidDataException($"Invalid quiz list '{name}'.");
            if (values.Count == 0 || values.Any(value => value is null)) throw new InvalidDataException($"Empty quiz list '{name}'.");
            return values.AsReadOnly();
        });
    }

    public static string Text(AppLanguage language, string id, params (string Name, string Value)[] values)
        => Text(Culture(language), id, values);

    public static string Text(string culture, string id, params (string Name, string Value)[] values)
    {
        culture = CultureInfo.GetCultureInfo(culture).Name;
        IReadOnlyDictionary<string, QuizContentText> Entries(string code) => Texts.GetOrAdd(code,
            name => Pack(name).Texts.ToDictionary(t => t.Id, StringComparer.Ordinal));
        if (!Entries(culture).TryGetValue(id, out var entry) && !Entries(FallbackCulture).TryGetValue(id, out entry))
            throw new InvalidDataException($"Missing quiz text '{id}'.");
        var supplied = values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
        if (!entry.Variables.ToHashSet(StringComparer.Ordinal).SetEquals(supplied.Keys))
            throw new InvalidDataException($"Values do not match the variables of '{id}'.");
        // A single replacement pass prevents data containing braces from becoming a second template.
        // Undeclared placeholders belong to the downstream C# fact renderer (e.g. AI prose's {a}, {unit}).
        return Placeholders().Replace(entry.Text, match => supplied.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
    }

    [GeneratedRegex(@"(?<!\{)\{([A-Za-z0-9_.-]+)\}(?!\})", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholders();
}
