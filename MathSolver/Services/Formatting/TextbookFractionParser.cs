using System.Numerics;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Presentation-only syntax: preserves operands, grouping and unreduced fractions.</summary>
public static partial class TextbookFractionParser
{
    public abstract record Node;
    public sealed record Number(string Text) : Node;
    public sealed record Binary(string Operator, Node Left, Node Right) : Node;
    public sealed record Group(Node Content) : Node;
    public sealed record Unary(string Operator, Node Content) : Node;
    public sealed record Mixed(Number Whole, Number Numerator, Number Denominator) : Node;
    public sealed record Fragment(string Text, Node? Math = null);

    public static bool ContainsFraction(Node node) => node switch
    {
        Binary { Operator: "/" } or Mixed => true,
        Binary binary => ContainsFraction(binary.Left) || ContainsFraction(binary.Right),
        Group group => ContainsFraction(group.Content),
        Unary unary => ContainsFraction(unary.Content),
        _ => false
    };

    public static IReadOnlyList<Fragment> ParseLine(string line)
    {
        var result = new List<Fragment>();
        int plainStart = 0;
        foreach (Match match in MathRunRegex().Matches(line))
        {
            string text = match.Value.TrimEnd();
            // A period/comma at the end belongs to the sentence, not the denominator.
            text = text.TrimEnd('.', ',');
            // Closing prose punctuation can follow a fraction inside a parenthetical sentence.
            while (text.EndsWith(')') && text.Count(character => character == ')') > text.Count(character => character == '('))
                text = text[..^1].TrimEnd();
            if (!text.Contains('/') || DateRegex().IsMatch(text) ||
                !TryParseCore(text, bindFractionOperands: true, out Node? node) || !ContainsFraction(node!)) continue;
            if (match.Index > plainStart) result.Add(new(line[plainStart..match.Index]));
            result.Add(new(text, node));
            plainStart = match.Index + text.Length;
        }
        if (plainStart < line.Length) result.Add(new(line[plainStart..]));
        if (result.Count == 0) result.Add(new(line));
        return result;
    }

    public static bool TryParse(string expression, out Node? node)
        => TryParseCore(expression, bindFractionOperands: false, out node);

    private static bool TryParseCore(string expression, bool bindFractionOperands, out Node? node)
    {
        node = null;
        if (string.IsNullOrWhiteSpace(expression) || expression.Length > 4096) return false;
        var reader = new Reader(expression, bindFractionOperands);
        if (!reader.TryReadRelation(out var parsed)) return false;
        reader.SkipSpace();
        if (reader.Position != expression.Length) return false;
        node = parsed;
        return true;
    }

    // Incomplete input is normal while an Editor raises TextChanged. Every
    // grammar rule reports failure without throwing, including speculative
    // mixed-number reads and the nesting limit.
    private sealed class Reader(string text, bool bindFractionOperands)
    {
        internal int Position;
        private int _depth;
        internal void SkipSpace() { while (Position < text.Length && char.IsWhiteSpace(text[Position])) Position++; }
        private char Peek() { SkipSpace(); return Position < text.Length ? text[Position] : '\0'; }
        private bool Take(char symbol) { if (Peek() != symbol) return false; Position++; return true; }

        internal bool TryReadRelation(out Node? node)
        {
            node = null;
            if (!TryReadSum(out var left)) return false;
            while (Peek() is '=' or '<' or '>' or '≈' or '?')
            {
                string op = text[Position++].ToString();
                if (!TryReadSum(out var right)) return false;
                left = new Binary(op, left!, right!);
            }
            node = left;
            return true;
        }

        private bool TryReadSum(out Node? node)
        {
            node = null;
            if (!TryReadProduct(out var left)) return false;
            while (Peek() is '+' or '-' or '−')
            {
                string op = text[Position++].ToString();
                if (!TryReadProduct(out var right)) return false;
                left = new Binary(op, left!, right!);
            }
            node = left;
            return true;
        }

        private bool TryReadProduct(out Node? node)
        {
            node = null;
            if (!TryReadPrimary(out var left)) return false;
            while (Peek() is '*' or '×' or '/' or '÷')
            {
                string op = text[Position++].ToString();
                if (!TryReadPrimary(out var right)) return false;
                left = new Binary(op, left!, right!);
            }
            node = left;
            return true;
        }

        private bool TryReadPrimary(out Node? node)
        {
            node = null;
            if (++_depth > 32) { _depth--; return false; }
            try
            {
                if (Peek() is '+' or '-' or '−')
                {
                    string op = text[Position++].ToString();
                    if (!TryReadPrimary(out var operand)) return false;
                    node = new Unary(op, operand!);
                    return true;
                }
                if (Peek() is '(' or '[' or '{')
                {
                    char closing = text[Position++] switch { '(' => ')', '[' => ']', _ => '}' };
                    if (!TryReadRelation(out var content) || !Take(closing)) return false;
                    node = new Group(content!);
                    return true;
                }
                if (Take('?')) { node = new Number("?"); return true; }
                if (!TryReadNumber(out var whole)) return false;
                int afterWhole = Position;
                SkipSpace();
                // Mixed numbers remain one item in choices and wrapped paragraphs.
                if (Position > afterWhole && Peek() is >= '0' and <= '9')
                {
                    if (!TryReadNumber(out var numerator)) return false;
                    if (Take('/'))
                    {
                        if (!TryReadNumber(out var denominator)) return false;
                        if (Integer(whole!.Text, out _) && Integer(numerator!.Text, out var n) &&
                            Integer(denominator!.Text, out var d) && n > 0 && n < d)
                        {
                            node = new Mixed(whole, numerator, denominator);
                            return true;
                        }
                    }
                    Position = afterWhole;
                }
                // Display numeric a/b as one operand of × or ÷. Ordinary
                // TryParse callers retain the original left-associative grammar.
                if (bindFractionOperands)
                {
                    Position = afterWhole;
                    if (Take('/') && Peek() is >= '0' and <= '9')
                    {
                        if (!TryReadNumber(out var denominator)) return false;
                        node = new Binary("/", whole!, denominator!);
                        return true;
                    }
                    Position = afterWhole;
                }
                node = whole;
                return true;
            }
            finally { _depth--; }
        }

        private bool TryReadNumber(out Number? number)
        {
            number = null;
            SkipSpace();
            int start = Position;
            while (Position < text.Length && text[Position] is >= '0' and <= '9') Position++;
            if (Position == start) return false;
            while (Position + 1 < text.Length && text[Position] is '.' or ',' && text[Position + 1] is >= '0' and <= '9')
            {
                Position++;
                while (Position < text.Length && text[Position] is >= '0' and <= '9') Position++;
            }
            number = new Number(text[start..Position]);
            return true;
        }

        private static bool Integer(string value, out BigInteger number) => BigInteger.TryParse(value, out number);
    }

    // Commas followed by whitespace and keyboard arrows separate list items.
    // Keep -> in plain text, rather than parsing it as subtraction then comparison.
    [GeneratedRegex(@"(?<![\p{L}\p{N}_])(?:[+\-−(\[{]*[0-9])(?:[0-9\s.()\[\]{}+−*/×÷=<?≈]|-(?!>)|(?<!-)>|,(?=[0-9]))*", RegexOptions.CultureInvariant)]
    private static partial Regex MathRunRegex();
    [GeneratedRegex(@"^\d{1,2}/\d{1,2}/\d{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex DateRegex();
}
