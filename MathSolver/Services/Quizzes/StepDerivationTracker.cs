using System.Numerics;
using V = MathSolver.Services.EssayCalculationEvaluator.Value;

namespace MathSolver.Services;

/// <summary>Exact symbolic arithmetic preserves the origin of reusable step results.</summary>
internal sealed class StepDerivationTracker
{
    internal sealed record Polynomial(SortedDictionary<string, BigInteger> Terms)
    {
        internal static Polynomial Constant(BigInteger value) => new(new SortedDictionary<string, BigInteger>(StringComparer.Ordinal) { [""] = value });
        internal Polynomial Add(Polynomial other, int sign = 1)
        {
            var terms = new SortedDictionary<string, BigInteger>(Terms, StringComparer.Ordinal);
            foreach (var (key, coefficient) in other.Terms) terms[key] = terms.GetValueOrDefault(key) + sign * coefficient;
            return Normalize(terms);
        }
        internal Polynomial Multiply(Polynomial other)
        {
            var terms = new SortedDictionary<string, BigInteger>(StringComparer.Ordinal);
            foreach (var a in Terms)
            foreach (var b in other.Terms)
            {
                string key = string.Join('.', (a.Key + "." + b.Key).Split('.', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
                terms[key] = terms.GetValueOrDefault(key) + a.Value * b.Value;
                if (terms.Count > 128) throw new InvalidOperationException("Expression is too complex.");
            }
            return Normalize(terms);
        }
        private static Polynomial Normalize(SortedDictionary<string, BigInteger> terms)
        {
            foreach (string key in terms.Where(pair => pair.Value.IsZero).Select(pair => pair.Key).ToArray()) terms.Remove(key);
            return new(terms);
        }
        internal bool Same(Polynomial other) => Terms.Count == other.Terms.Count && Terms.All(pair => other.Terms.GetValueOrDefault(pair.Key) == pair.Value);
        internal bool IsZero => Terms.Count == 0 || Terms.Values.All(value => value.IsZero);
    }
    internal sealed record Origin(Polynomial Numerator, Polynomial Denominator)
    {
        internal static Origin Constant(V value) => new(Polynomial.Constant(value.Numerator), Polynomial.Constant(value.Denominator));
        internal bool Equivalent(Origin other)
        {
            try { return Numerator.Multiply(other.Denominator).Same(other.Numerator.Multiply(Denominator)); }
            catch (InvalidOperationException) { return false; }
        }
        internal Origin Apply(string operation, Origin other) => operation switch
        {
            "+" or "-" => new(Numerator.Multiply(other.Denominator).Add(other.Numerator.Multiply(Denominator), operation == "+" ? 1 : -1), Denominator.Multiply(other.Denominator)),
            "*" => new(Numerator.Multiply(other.Numerator), Denominator.Multiply(other.Denominator)),
            "/" when !other.Numerator.IsZero => new(Numerator.Multiply(other.Denominator), Denominator.Multiply(other.Numerator)),
            _ => throw new InvalidOperationException("Invalid symbolic operation.")
        };
    }
    private readonly Dictionary<V, List<Origin>> _known = [];
    private readonly bool _preferDecimalNotation;
    internal StepDerivationTracker(IEnumerable<string> facts, IEnumerable<string> constants, bool preferDecimalNotation = false)
    {
        _preferDecimalNotation = preferDecimalNotation;
        int index = 0;
        foreach (string text in facts.Distinct())
            if (EssayCalculationEvaluator.TryEvaluate(text, out var value, out _, preferDecimalNotation) && !_known.ContainsKey(value))
                Remember(value, [new(new Polynomial(new SortedDictionary<string, BigInteger>(StringComparer.Ordinal) { ["v" + index++] = 1 }), Polynomial.Constant(1))]);
        foreach (string text in constants.Distinct())
            if (EssayCalculationEvaluator.TryEvaluate(text, out var value, out _, preferDecimalNotation)) Remember(value, [Origin.Constant(value)]);
    }
    internal void Remember(V value, IEnumerable<Origin> origins)
    {
        if (!_known.TryGetValue(value, out var list)) _known[value] = list = [];
        foreach (Origin origin in origins)
            if (list.Count < 64 && !list.Any(old => old.Equivalent(origin))) list.Add(origin);
    }
    internal List<Origin> Read(string expression)
    {
        if (!EssayCalculationEvaluator.TryGetStructure(expression, out string tree, fractionLiterals: false, _preferDecimalNotation)) return [];
        try { return new Reader(tree, _known).Read(); }
        catch (InvalidOperationException) { return []; }
    }
    private sealed class Reader(string text, Dictionary<V, List<Origin>> known)
    {
        private int _position;
        internal List<Origin> Read()
        {
            var result = Node(); return _position == text.Length ? result : [];
        }
        private List<Origin> Node()
        {
            int opening = text.IndexOf('(', _position);
            if (opening < 0) return [];
            string kind = text[_position..opening]; _position = opening + 1;
            if (kind == "n")
            {
                int close = text.IndexOf(')', _position); if (close < 0) return [];
                string number = text[_position..close]; _position = close + 1;
                return EssayCalculationEvaluator.TryEvaluate(number, out V value, out _) && known.TryGetValue(value, out var sources) ? [.. sources] : [];
            }
            int leftStart = _position; var left = Node(); int leftEnd = _position;
            if (kind == "neg") return Take(')') ? left.Select(value => Origin.Constant(V.Create(-1, 1)).Apply("*", value)).ToList() : [];
            if (!Take(',')) return [];
            int rightStart = _position; var right = Node(); int rightEnd = _position;
            if (!Take(')')) return [];
            var result = new List<Origin>();
            if (kind == "/" && Number(text[leftStart..leftEnd], out V n) && Number(text[rightStart..rightEnd], out V d) && !d.Numerator.IsZero
                && known.TryGetValue(V.Create(n.Numerator * d.Denominator, n.Denominator * d.Numerator), out var references)) result.AddRange(references);
            foreach (Origin a in left)
            foreach (Origin b in right)
            {
                var origin = a.Apply(kind, b);
                if (!result.Any(old => old.Equivalent(origin))) result.Add(origin);
                if (result.Count > 64) throw new InvalidOperationException("Too many ambiguous interpretations.");
            }
            return result;
        }
        private static bool Number(string node, out V value)
        {
            value = default;
            return node.StartsWith("n(", StringComparison.Ordinal) && node.EndsWith(')') && EssayCalculationEvaluator.TryEvaluate(node[2..^1], out value, out _);
        }
        private bool Take(char character) => _position < text.Length && text[_position++] == character;
    }
}
