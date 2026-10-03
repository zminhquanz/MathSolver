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
                !TryParse(text, out Node? node) || !ContainsFraction(node!)) continue;
            if (match.Index > plainStart) result.Add(new(line[plainStart..match.Index]));
            result.Add(new(text, node));
            plainStart = match.Index + text.Length;
        }
        if (plainStart < line.Length) result.Add(new(line[plainStart..]));
        if (result.Count == 0) result.Add(new(line));
        return result;
    }

    public static bool TryParse(string expression, out Node? node)
    {
        node = null;
        if (expression.Length > 4096) return false;
        try
        {
            var reader = new Reader(expression);
            node = reader.Relation();
            reader.SkipSpace();
            if (reader.Position != expression.Length) { node = null; return false; }
            return true;
        }
        catch (FormatException) { node = null; return false; }
    }

    private sealed class Reader(string text)
    {
        internal int Position;
        private int _depth;
        internal void SkipSpace() { while (Position < text.Length && char.IsWhiteSpace(text[Position])) Position++; }
        private char Peek() { SkipSpace(); return Position < text.Length ? text[Position] : '\0'; }
        private bool Take(char symbol) { if (Peek() != symbol) return false; Position++; return true; }
        internal Node Relation()
        {
            Node left = Sum();
            while (Peek() is '=' or '<' or '>' or '≈' or '?')
            { string op = text[Position++].ToString(); left = new Binary(op, left, Sum()); }
            return left;
        }
        private Node Sum()
        {
            Node left = Product();
            while (Peek() is '+' or '-' or '−')
            { string op = text[Position++].ToString(); left = new Binary(op, left, Product()); }
            return left;
        }
        private Node Product()
        {
            Node left = Primary();
            while (Peek() is '*' or '×' or '/' or '÷')
            { string op = text[Position++].ToString(); left = new Binary(op, left, Primary()); }
            return left;
        }
        private Node Primary()
        {
            if (++_depth > 32) throw new FormatException();
            try
            {
                if (Peek() is '+' or '-' or '−')
                { string op = text[Position++].ToString(); return new Unary(op, Primary()); }
                if (Take('('))
                { Node content = Relation(); if (!Take(')')) throw new FormatException(); return new Group(content); }
                if (Take('?')) return new Number("?");
                Number whole = ReadNumber();
                int afterWhole = Position;
                SkipSpace();
                // Mixed numbers are one item, including in choices and wrapped paragraphs.
                if (Position > afterWhole && Peek() is >= '0' and <= '9')
                {
                    Number numerator = ReadNumber();
                    if (Take('/'))
                    {
                        Number denominator = ReadNumber();
                        if (Integer(whole.Text, out _) && Integer(numerator.Text, out var n) &&
                            Integer(denominator.Text, out var d) && n > 0 && n < d)
                            return new Mixed(whole, numerator, denominator);
                    }
                    Position = afterWhole;
                }
                return whole;
            }
            finally { _depth--; }
        }
        private Number ReadNumber()
        {
            SkipSpace();
            int start = Position;
            while (Position < text.Length && text[Position] is >= '0' and <= '9') Position++;
            if (Position == start) throw new FormatException();
            while (Position + 1 < text.Length && text[Position] is '.' or ',' && text[Position + 1] is >= '0' and <= '9')
            {
                Position++;
                while (Position < text.Length && text[Position] is >= '0' and <= '9') Position++;
            }
            return new Number(text[start..Position]);
        }
        private static bool Integer(string value, out BigInteger number) => BigInteger.TryParse(value, out number);
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}_])(?:[+\-−(]*[0-9])[0-9\s.,()+\-−*/×÷=<>?≈]*", RegexOptions.CultureInvariant)]
    private static partial Regex MathRunRegex();
    [GeneratedRegex(@"^\d{1,2}/\d{1,2}/\d{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex DateRegex();
}
