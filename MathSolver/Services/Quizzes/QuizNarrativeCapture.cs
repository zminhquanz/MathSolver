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
    internal readonly Dictionary<string, (string Role, string Value)> Slots = new(StringComparer.Ordinal);
    private static readonly Regex Numbers = new(@"(?<![\p{L}\p{N}])\d+(?:[.,]\d+)?(?![\p{L}\p{N}])");

    internal QuizNarrativeCapture() { _previous = Scope.Value; Scope.Value = this; }

    internal void Record(string template, IReadOnlyDictionary<string, string> arguments, string rendered)
    {
        if (!Numbers.IsMatch(rendered)) return;
        string projected = Regex.Replace(template, @"\{([A-Za-z0-9_.-]+)\}", m =>
        {
            if (!arguments.TryGetValue(m.Groups[1].Value, out string? value)) return m.Value;
            string nested = Project(value);
            return Numbers.Replace(nested, number => Slot(m.Groups[1].Value, number.Value));
        });
        projected = Numbers.Replace(projected, number => Slot("constant", number.Value));
        _fragments[rendered] = projected;
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

    internal string Finish(string text) => Numbers.Replace(Project(text), m => Slot("given", m.Value));
    public void Dispose() => Scope.Value = _previous;
}
