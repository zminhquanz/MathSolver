namespace MathSolver.Services;

/// <summary>Only an observed running batch can produce one terminal save acknowledgement.</summary>
internal sealed class BatchCompletionGate
{
    private bool _watching;
    public void Reset() => _watching = false;
    public bool Observe(bool running, bool successfulTerminal, int committedCount)
    {
        if (running) { _watching = true; return false; }
        bool acknowledge = _watching && successfulTerminal && committedCount > 0;
        _watching = false;
        return acknowledge;
    }
}

internal static class SaveFeedbackPolicy
{
    public static bool HasRowChanges(IReadOnlyDictionary<string, object?> original,
        IReadOnlyDictionary<string, string?> inputs, IReadOnlySet<string> integerColumns)
    {
        foreach (var (name, text) in inputs)
        {
            if (!original.TryGetValue(name, out var old)) return true;
            if (old is null || text is null) { if (old is not null || text is not null) return true; continue; }
            string previous = Convert.ToString(old, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (integerColumns.Contains(name)
                && long.TryParse(previous, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long before)
                && long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long after))
            { if (before != after) return true; }
            else if (!string.Equals(previous, text, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
