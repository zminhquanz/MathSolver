using MathSolver.Models;
using System.Globalization;
using System.Numerics;
using Value = MathSolver.Models.ReducedFraction;

namespace MathSolver.Services;

/// <summary>
/// Exact rational parser shared by fraction calculations and quiz generation/grading.
/// Each call owns its parser and optional worked-step trace; the shared instance is stateless.
/// </summary>
public sealed class RationalExpressionEngine
{
    public static RationalExpressionEngine Shared { get; } = new();
    public const int MaximumLength = 256;
    private const int MaximumValueBits = 16384;

    public RationalExpressionResult Evaluate(string? text, bool preferDecimalNotation = false)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new RationalExpressionException(RationalExpressionError.Empty);
        if (text.Length > MaximumLength) throw new RationalExpressionException(RationalExpressionError.TooLong);
        var steps = new List<string>();
        var parser = new Parser(text, preferDecimalNotation: preferDecimalNotation, steps: steps);
        if (!parser.TryReadExpression(out var value) || !parser.AtEnd)
            throw new RationalExpressionException(parser.Error);
        string normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Replace('*', '×').Replace('x', '×').Replace('X', '×').Replace('·', '×').Replace(':', '÷').Replace('-', '−');
        return new(normalized, value, parser.HasOperation, steps.AsReadOnly());
    }

    public bool TryEvaluate(string? text, out ReducedFraction value, out bool hasOperation, bool preferDecimalNotation = false)
    {
        value = default;
        hasOperation = false;
        if (string.IsNullOrEmpty(text) || text.Length > MaximumLength) return false;
        var parser = new Parser(text, preferDecimalNotation: preferDecimalNotation);
        if (!parser.TryReadExpression(out var result) || !parser.AtEnd) return false;
        value = result;
        hasOperation = parser.HasOperation;
        return true;
    }

    internal bool TryGetStructure(string text, out string structure, bool fractionLiterals, bool preferDecimalNotation = false)
    {
        structure = string.Empty;
        if (text.Length is 0 or > MaximumLength) return false;
        var parser = new Parser(text, trackStructure: true, fractionLiterals: fractionLiterals, preferDecimalNotation: preferDecimalNotation);
        if (!parser.TryReadExpression(out _) || !parser.AtEnd) return false;
        structure = parser.Structure;
        return true;
    }

    private sealed class Parser(string text, bool trackStructure = false, bool fractionLiterals = true, bool preferDecimalNotation = false, List<string>? steps = null)
    {
        private int _position;
        private int _depth;
        internal RationalExpressionError Error { get; private set; } = RationalExpressionError.InvalidExpression;

        private bool CheckSize(Value value)
        {
            if (value.Numerator.GetBitLength() <= MaximumValueBits &&
                value.Denominator.GetBitLength() <= MaximumValueBits) return true;
            Error = RationalExpressionError.TooLarge;
            return false;
        }

        private static string Operand(Value value) => value.Numerator.Sign < 0 ? "(" + value + ")" : value.ToString();
        internal bool HasOperation { get; private set; }
        internal string Structure { get; private set; } = string.Empty;

        internal bool AtEnd
        {
            get
            {
                SkipSpaces();
                return _position == text.Length;
            }
        }

        internal bool TryReadExpression(out Value value)
        {
            value = default;
            if (!TryReadTerm(out value))
                return false;

            while (true)
            {
                SkipSpaces();
                if (_position == text.Length || text[_position] is not ('+' or '-' or '−'))
                    return true;

                char operation = text[_position++];
                string leftStructure = Structure;
                if (!TryReadTerm(out Value right))
                    return false;

                if (trackStructure)
                    Structure = $"{(operation == '+' ? '+' : '-')}({leftStructure},{Structure})";

                HasOperation = true;
                Value left = value;
                value = operation == '+'
                    ? new Value(value.Numerator * right.Denominator +
                                   right.Numerator * value.Denominator,
                        value.Denominator * right.Denominator)
                    : new Value(value.Numerator * right.Denominator -
                                   right.Numerator * value.Denominator,
                        value.Denominator * right.Denominator);
                if (!CheckSize(value)) return false;
                steps?.Add($"{Operand(left)} {(operation == '+' ? "+" : "−")} {Operand(right)} = {value}");
            }
        }

        private bool TryReadTerm(out Value value)
        {
            value = default;
            if (!TryReadFactor(out value))
                return false;

            while (true)
            {
                SkipSpaces();
                if (_position == text.Length ||
                    text[_position] is not ('×' or '*' or 'x' or 'X' or '·' or '÷' or '/' or ':'))
                    return true;

                char operation = text[_position++];
                string leftStructure = Structure;
                if (!TryReadFactor(out Value right))
                    return false;

                if (trackStructure)
                    Structure = $"{(operation is '÷' or '/' or ':' ? '/' : '*')}({leftStructure},{Structure})";

                HasOperation = true;
                Value left = value;
                if (operation is '÷' or '/' or ':')
                {
                    if (right.Numerator.IsZero)
                    {
                        Error = RationalExpressionError.DivisionByZero;
                        return false;
                    }
                    value = new Value(value.Numerator * right.Denominator,
                        value.Denominator * right.Numerator);
                }
                else
                {
                    value = new Value(value.Numerator * right.Numerator,
                        value.Denominator * right.Denominator);
                }
                if (!CheckSize(value)) return false;
                steps?.Add($"{Operand(left)} {(operation is '÷' or '/' or ':' ? "÷" : "×")} {Operand(right)} = {value}");
            }
        }

        private bool TryReadFactor(out Value value)
        {
            value = default;
            SkipSpaces();
            if (_position == text.Length)
                return false;

            bool negative = false;
            if (text[_position] is '+' or '-' or '−')
            {
                negative = text[_position] is '-' or '−';
                _position++;
                SkipSpaces();
            }

            bool isNumber = false;
            if (_position < text.Length && text[_position] is '(' or '[' or '{')
            {
                char closing = text[_position] switch { '(' => ')', '[' => ']', _ => '}' };
                if (++_depth > 20)
                    return false;
                _position++;
                if (!TryReadExpression(out value))
                    return false;
                SkipSpaces();
                if (_position == text.Length || text[_position++] != closing)
                    return false;
                _depth--;
            }
            else if (!TryReadNumber(out value))
            {
                return false;
            }
            else
            {
                isNumber = true;
            }

            // A slash directly following a numeric value can represent a fraction
            // literal. Treating the first slash as part of the factor also makes
            // expressions such as 1/2 ÷ 3/4 unambiguous while retaining ordinary
            // left-to-right division semantics for 24/4/2.
            if (isNumber && fractionLiterals)
            {
                int slashPosition = _position;
                SkipSpaces();
                if (_position < text.Length && text[_position] == '/')
                {
                    _position++;
                    SkipSpaces();
                    string numeratorStructure = Structure;
                    if (!TryReadNumber(out Value denominator) ||
                        !denominator.Denominator.IsOne ||
                        denominator.Numerator.IsZero)
                    {
                        _position = slashPosition;
                        Structure = numeratorStructure;
                    }
                    else
                    {
                        HasOperation = true;
                        value = new Value(value.Numerator,
                            value.Denominator * denominator.Numerator);
                        if (trackStructure)
                            Structure = $"/({numeratorStructure},{Structure})";
                    }
                }
            }

            if (negative)
            {
                value = new Value(-value.Numerator, value.Denominator);
                if (trackStructure) Structure = $"neg({Structure})";
            }

            SkipSpaces();
            if (_position < text.Length && text[_position] is '%' or '²' or '³')
            {
                char suffix = text[_position++];
                Value operand = value;
                if (trackStructure) Structure = $"{suffix}({Structure})";
                HasOperation = true;
                value = suffix switch
                {
                    '%' => new Value(value.Numerator, value.Denominator * 100),
                    '²' => new Value(value.Numerator * value.Numerator,
                        value.Denominator * value.Denominator),
                    _ => new Value(value.Numerator * value.Numerator * value.Numerator,
                        value.Denominator * value.Denominator * value.Denominator)
                };
                if (!CheckSize(value)) return false;
                steps?.Add($"{Operand(operand)}{suffix} = {value}");
            }

            return true;
        }

        private bool TryReadNumber(out Value value)
        {
            value = default;
            int start = _position;
            while (_position < text.Length && char.IsAsciiDigit(text[_position]))
                _position++;
            if (_position == start)
                return false;

            while (_position < text.Length && text[_position] is '.' or ',')
            {
                int separator = _position++;
                int digitsStart = _position;
                while (_position < text.Length && char.IsAsciiDigit(text[_position]))
                    _position++;
                if (_position == digitsStart || separator == start)
                    return false;
            }

            string token = text[start.._position];
            if (token.Length > 60)
                return false;

            int separatorIndex = token.LastIndexOfAny('.', ',');
            if (separatorIndex < 0)
            {
                if (!BigInteger.TryParse(token, NumberStyles.None,
                        CultureInfo.InvariantCulture, out BigInteger integer))
                    return false;
                value = new Value(integer, BigInteger.One);
                if (trackStructure) Structure = $"n({integer.ToString(CultureInfo.InvariantCulture)})";
                return true;
            }

            string[] groups = token.Split(['.', ',']);
            bool groupedInteger = !preferDecimalNotation && groups[0].Length is >= 1 and <= 3 &&
                                  groups.Skip(1).All(group => group.Length == 3);
            if (groupedInteger)
            {
                if (!BigInteger.TryParse(string.Concat(groups), NumberStyles.None,
                        CultureInfo.InvariantCulture, out BigInteger integer))
                    return false;
                value = new Value(integer, BigInteger.One);
                if (trackStructure) Structure = $"n({integer.ToString(CultureInfo.InvariantCulture)})";
                return true;
            }

            if (groups.Length != 2 ||
                !BigInteger.TryParse(string.Concat(groups), NumberStyles.None,
                    CultureInfo.InvariantCulture, out BigInteger decimalDigits))
                return false;

            value = new Value(decimalDigits, BigInteger.Pow(10, groups[1].Length));
            if (trackStructure)
                Structure = $"n({value.Numerator.ToString(CultureInfo.InvariantCulture)}/{value.Denominator.ToString(CultureInfo.InvariantCulture)})";
            return true;
        }

        private void SkipSpaces()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position]))
                _position++;
        }
    }
}
