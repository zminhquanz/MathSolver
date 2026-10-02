using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Evaluates a student's arithmetic without tying it to the generator's worked example.
/// Rational values keep intermediate divisions and fractions exact.
/// </summary>
internal static class EssayCalculationEvaluator
{
    internal static string Format(Value value) => value.Denominator.IsOne
        ? value.Numerator.ToString(CultureInfo.InvariantCulture)
        : $"{value.Numerator.ToString(CultureInfo.InvariantCulture)}/{value.Denominator.ToString(CultureInfo.InvariantCulture)}";

    internal readonly record struct Value(BigInteger Numerator, BigInteger Denominator)
    {
        internal static Value Create(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            BigInteger divisor = BigInteger.GreatestCommonDivisor(
                BigInteger.Abs(numerator), denominator);
            return new Value(numerator / divisor, denominator / divisor);
        }
    }

    internal static bool TryEvaluate(string text, out Value value, out bool hasOperation, bool preferDecimalNotation = false)
    {
        value = default;
        hasOperation = false;
        if (text.Length is 0 or > 256)
            return false;

        var parser = new Parser(text, preferDecimalNotation: preferDecimalNotation);
        if (!parser.TryReadExpression(out value) || !parser.AtEnd)
            return false;

        hasOperation = parser.HasOperation;
        return true;
    }

    internal static bool TryGetStructure(string text, out string structure, bool fractionLiterals, bool preferDecimalNotation = false)
    {
        structure = string.Empty;
        if (text.Length is 0 or > 256)
            return false;
        var parser = new Parser(text, trackStructure: true, fractionLiterals: fractionLiterals, preferDecimalNotation: preferDecimalNotation);
        if (!parser.TryReadExpression(out _) || !parser.AtEnd)
            return false;
        structure = parser.Structure;
        return true;
    }

    private sealed class Parser(string text, bool trackStructure = false, bool fractionLiterals = true, bool preferDecimalNotation = false)
    {
        private int _position;
        private int _depth;
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
                value = operation == '+'
                    ? Value.Create(value.Numerator * right.Denominator +
                                   right.Numerator * value.Denominator,
                        value.Denominator * right.Denominator)
                    : Value.Create(value.Numerator * right.Denominator -
                                   right.Numerator * value.Denominator,
                        value.Denominator * right.Denominator);
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
                if (operation is '÷' or '/' or ':')
                {
                    if (right.Numerator.IsZero)
                        return false;
                    value = Value.Create(value.Numerator * right.Denominator,
                        value.Denominator * right.Numerator);
                }
                else
                {
                    value = Value.Create(value.Numerator * right.Numerator,
                        value.Denominator * right.Denominator);
                }
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
                        value = Value.Create(value.Numerator,
                            value.Denominator * denominator.Numerator);
                        if (trackStructure)
                            Structure = $"/({numeratorStructure},{Structure})";
                    }
                }
            }

            if (negative)
            {
                value = value with { Numerator = -value.Numerator };
                if (trackStructure) Structure = $"neg({Structure})";
            }

            SkipSpaces();
            if (_position < text.Length && text[_position] is '%' or '²' or '³')
            {
                char suffix = text[_position++];
                if (trackStructure) Structure = $"{suffix}({Structure})";
                HasOperation = true;
                value = suffix switch
                {
                    '%' => Value.Create(value.Numerator, value.Denominator * 100),
                    '²' => Value.Create(value.Numerator * value.Numerator,
                        value.Denominator * value.Denominator),
                    _ => Value.Create(value.Numerator * value.Numerator * value.Numerator,
                        value.Denominator * value.Denominator * value.Denominator)
                };
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

            value = Value.Create(decimalDigits, BigInteger.Pow(10, groups[1].Length));
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
