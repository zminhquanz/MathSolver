using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Captures named numeric arguments while existing generators build their prose.
/// The returned text and all mathematical calculations remain unchanged.</summary>
internal sealed class QuizNarrativeCapture : IDisposable
{
    private static readonly AsyncLocal<QuizNarrativeCapture?> Scope = new();
    internal static QuizNarrativeCapture? Current => Scope.Value;
    private readonly QuizNarrativeCapture? _previous;
    private readonly Dictionary<string, string> _fragments = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, string[]>? _reviewed;
    private readonly Dictionary<string, string[]> _phrasingFragments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _deferredPhrasings = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, (string Role, string Value)> Slots = new(StringComparer.Ordinal);
    private static readonly Regex Numbers = new(@"(?<![\p{L}\p{N}])\d+(?:[.,]\d+)?(?![\p{L}\p{N}])");

    internal QuizNarrativeCapture(IReadOnlyDictionary<string, string[]>? reviewed = null)
    { _reviewed = reviewed; _previous = Scope.Value; Scope.Value = this; }

    internal void Record(string template, IReadOnlyDictionary<string, string> arguments, string rendered)
    {
        string[]? alternatives = null;
        _reviewed?.TryGetValue(template, out alternatives);
        if (!Numbers.IsMatch(rendered) && alternatives is null) return;
        var projectedArguments = new Dictionary<string, string>(StringComparer.Ordinal);
        string projected = Regex.Replace(template, @"\{([A-Za-z0-9_.-]+)\}", m =>
        {
            if (!arguments.TryGetValue(m.Groups[1].Value, out string? value)) return m.Value;
            string nested = Project(value);
            string argument = Numbers.Replace(nested, number => Slot(m.Groups[1].Value, number.Value));
            projectedArguments[m.Groups[1].Value] = argument;
            return argument;
        });
        var constants = new List<(string Value, string Slot)>();
        projected = Numbers.Replace(projected, number => {
            string slot = Slot("constant", number.Value);
            constants.Add((number.Value, slot));
            return slot;
        });
        _fragments[rendered] = projected;
        if (alternatives is null) return;
        string[] original = Sentences(projected);
        var choices = original.Select(_ => new List<string>()).ToArray();
        foreach (string alternative in alternatives)
        {
            string text = Regex.Replace(alternative, @"\{([A-Za-z0-9_.-]+)\}", m =>
                projectedArguments.GetValueOrDefault(m.Groups[1].Value, m.Value));
            int constantIndex = 0;
            text = Numbers.Replace(text, number => {
                if (constantIndex >= constants.Count || constants[constantIndex].Value != number.Value)
                    throw new InvalidDataException("Reviewed numeric constant changed.");
                return constants[constantIndex++].Slot;
            });
            if (constantIndex != constants.Count) throw new InvalidDataException("Reviewed numeric constant missing.");
            var parts = Sentences(text);
            if (parts.Length != original.Length) throw new InvalidDataException("Reviewed clause count changed.");
            for (int i = 0; i < parts.Length; i++) choices[i].Add(parts[i]);
        }
        for (int i = 0; i < original.Length; i++)
            _phrasingFragments[original[i]] = choices[i].ToArray();
    }

    private string Slot(string role, string value)
    {
        string id = "f" + Slots.Count;
        Slots[id] = (role, value);
        return "{" + id + "}";
    }

    internal string Project(string text)
    {
        // Replace complete translated fragments, never matching one numeric value
        // against all equal values: equal quantities can have different roles.
        foreach (var fragment in _fragments.OrderByDescending(pair => pair.Key.Length))
            text = text.Replace(fragment.Key, fragment.Value, StringComparison.Ordinal);
        return text;
    }

    // Some generators bind a supplied list only after translating the sentence.
    // Capture its reviewed alternatives without changing the existing fact slots
    // or schemas of templates already saved in SQLite.
    internal void BindAlternatives(string template, string variable, string value)
    {
        string source = template.Replace("{" + variable + "}", value, StringComparison.Ordinal);
        string Restore(string text) => Regex.Replace(text, @"\{(f\d+)\}", m => Slots[m.Groups[1].Value].Value);
        var choices = Phrasings(Project(template)).Select(text => Restore(text)
            .Replace("{" + variable + "}", value, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
        if (choices.Any(choice => choice != source)) _deferredPhrasings[source] = choices;
    }

    internal string Finish(string text)
    {
        string result = Numbers.Replace(Project(text), m => Slot("given", m.Value));
        if (_deferredPhrasings.TryGetValue(text, out var alternatives))
        {
            var ordered = Regex.Matches(result, @"\{f\d+\}").Select(m => m.Value).ToArray();
            string[] original = Sentences(result);
            var choices = original.Select(_ => new List<string>()).ToArray();
            foreach (string alternative in alternatives)
            {
                int index = 0;
                string projected = Numbers.Replace(alternative, _ => index < ordered.Length ? ordered[index++]
                    : throw new InvalidDataException("Deferred narrative quantity count changed."));
                var parts = Sentences(projected);
                if (index != ordered.Length || parts.Length != original.Length) throw new InvalidDataException("Deferred narrative roles changed.");
                for (int i = 0; i < parts.Length; i++) choices[i].Add(parts[i]);
            }
            for (int i = 0; i < original.Length; i++) _phrasingFragments[original[i]] = choices[i].ToArray();
        }
        return result;
    }

    private static string[] Sentences(string text) => Regex.Split(text.Trim(), @"(?<=[.!?])\s+");

    internal string[] Phrasings(string text)
    {
        var choices = new List<string> { text };
        foreach (var (original, alternatives) in _phrasingFragments.OrderByDescending(pair => pair.Key.Length))
        {
            if (!text.Contains(original, StringComparison.Ordinal)) continue;
            // Longer fragments carry the factual sentence; shorter fragments
            // are often just a context opening. Vary the former first, rather
            // than spending retries on "At the library" / "In the library".
            choices = new[] { original }.Concat(alternatives).SelectMany(alt => choices
                .Select(choice => choice.Replace(original, alt, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal).Take(64).ToList();
        }
        return choices.ToArray();
    }
    public void Dispose() => Scope.Value = _previous;
}
