using MathSolver.Models;
using MathSolver.Numerics;
using MathSolver.Services;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services.Core;

/// <summary>
/// Nguồn sự thật dùng chung cho cộng, trừ, nhân và chia.
/// Lớp này không phụ thuộc vào MAUI hay control giao diện.
/// </summary>
public sealed class BasicArithmeticEngine
{
    private const int MaximumExpressionLength = 512;
    private const int MaximumTokenCount = 255;

    public IntegerArithmeticResult CalculateInteger(
        IntegerArithmeticExpression expression)
    {
        BigInteger result;
        BigInteger remainder =
            BigInteger.Zero;

        switch (expression.Operation)
        {
            case ArithmeticOperation.Add:
                result =
                    expression.LeftOperand +
                    expression.RightOperand;
                break;

            case ArithmeticOperation.Subtract:
                result =
                    expression.LeftOperand -
                    expression.RightOperand;
                break;

            case ArithmeticOperation.Multiply:
                result =
                    expression.LeftOperand *
                    expression.RightOperand;
                break;

            case ArithmeticOperation.Divide:
                if (expression.RightOperand.IsZero)
                {
                    throw new DivideByZeroException(
                        "The right operand of a division cannot be zero.");
                }

                result =
                    BigInteger.DivRem(
                        expression.LeftOperand,
                        expression.RightOperand,
                        out remainder);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(expression),
                    expression.Operation,
                    "Unsupported arithmetic operation.");
        }

        return new IntegerArithmeticResult(
            expression,
            result,
            remainder);
    }

    public QuadDouble CalculateDecimal(
        decimal leftOperand,
        ArithmeticOperation operation,
        decimal rightOperand)
    {
        if (operation == ArithmeticOperation.Divide &&
            rightOperand == decimal.Zero)
        {
            throw new DivideByZeroException(
                "The right operand of a division cannot be zero.");
        }

        QuadDouble left =
            QuadDouble.FromDecimal(
                leftOperand);

        QuadDouble right =
            QuadDouble.FromDecimal(
                rightOperand);

        return CalculateDecimal(
            left,
            operation,
            right);
    }

    public IntegerExpressionResult EvaluateIntegerExpression(
        string expression)
    {
        IReadOnlyList<ExpressionToken> postfix =
            ConvertToPostfix(expression);

        // Integer-expression input still accepts integer literals only, but an
        // intermediate division is allowed to become an exact rational value.
        // This avoids forcing students to switch to Decimal just because a
        // division does not divide evenly.
        var values =
            new Stack<ExactExpressionRational>();

        var steps =
            new List<string>();

        foreach (ExpressionToken token in postfix)
        {
            if (token.Kind == ExpressionTokenKind.Number)
            {
                if (token.Text.Contains('.'))
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.InvalidNumber);
                }

                if (!BigInteger.TryParse(
                        token.Text,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out BigInteger number) ||
                    number < (BigInteger)Int128.MinValue ||
                    number > (BigInteger)Int128.MaxValue)
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.NumberOutOfRange);
                }

                values.Push(
                    ExactExpressionRational.FromInteger(
                        number));
                continue;
            }

            if (token.IsUnary)
            {
                ExactExpressionRational operand =
                    PopExpressionRational(values);

                values.Push(
                    token.Text == "u-"
                        ? operand.Negate()
                        : operand);
                continue;
            }

            ExactExpressionRational right =
                PopExpressionRational(values);

            ExactExpressionRational left =
                PopExpressionRational(values);

            ArithmeticOperation operation =
                ToOperation(token.Text);

            if (operation == ArithmeticOperation.Divide &&
                right.Numerator.IsZero)
            {
                throw new ArithmeticExpressionException(
                    ArithmeticExpressionError.DivisionByZero);
            }

            ExactExpressionRational result =
                CalculateExpressionRational(
                    left,
                    operation,
                    right);

            values.Push(result);
            steps.Add(
                $"{FormatExpressionRational(left)} {GetSymbol(operation)} " +
                $"{FormatExpressionRational(right)} = " +
                $"{FormatExpressionRational(result)}");
        }

        if (values.Count != 1)
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.MissingOperator);
        }

        ExactExpressionRational finalResult =
            values.Pop();

        return new IntegerExpressionResult(
            NormalizeExpressionForDisplay(expression),
            finalResult.Numerator,
            finalResult.Denominator,
            steps);
    }

    private static ExactExpressionRational CalculateExpressionRational(
        ExactExpressionRational left,
        ArithmeticOperation operation,
        ExactExpressionRational right)
    {
        return operation switch
        {
            ArithmeticOperation.Add =>
                ExactExpressionRational.Create(
                    left.Numerator * right.Denominator +
                    right.Numerator * left.Denominator,
                    left.Denominator * right.Denominator),

            ArithmeticOperation.Subtract =>
                ExactExpressionRational.Create(
                    left.Numerator * right.Denominator -
                    right.Numerator * left.Denominator,
                    left.Denominator * right.Denominator),

            ArithmeticOperation.Multiply =>
                ExactExpressionRational.Create(
                    left.Numerator * right.Numerator,
                    left.Denominator * right.Denominator),

            ArithmeticOperation.Divide =>
                ExactExpressionRational.Create(
                    left.Numerator * right.Denominator,
                    left.Denominator * right.Numerator),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation,
                    "Unsupported arithmetic operation.")
        };
    }

    private static ExactExpressionRational PopExpressionRational(
        Stack<ExactExpressionRational> values)
    {
        if (!values.TryPop(
                out ExactExpressionRational value))
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.MissingOperand);
        }

        return value;
    }

    private static string FormatExpressionRational(
        ExactExpressionRational value)
    {
        return RationalDecimalFormatter.Format(
            value.Numerator,
            value.Denominator,
            maxRepeatingDecimalPlaces: 10);
    }

    private readonly record struct ExactExpressionRational(
        BigInteger Numerator,
        BigInteger Denominator)
    {
        public static ExactExpressionRational FromInteger(
            BigInteger value) =>
            new(
                value,
                BigInteger.One);

        public static ExactExpressionRational Create(
            BigInteger numerator,
            BigInteger denominator)
        {
            if (denominator.IsZero)
            {
                throw new ArithmeticExpressionException(
                    ArithmeticExpressionError.DivisionByZero);
            }

            if (numerator.IsZero)
            {
                return new ExactExpressionRational(
                    BigInteger.Zero,
                    BigInteger.One);
            }

            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            BigInteger gcd =
                BigInteger.GreatestCommonDivisor(
                    BigInteger.Abs(numerator),
                    denominator);

            return new ExactExpressionRational(
                numerator / gcd,
                denominator / gcd);
        }

        public ExactExpressionRational Negate() =>
            new(
                -Numerator,
                Denominator);
    }

    public DecimalExpressionResult EvaluateDecimalExpression(
        string expression)
    {
        IReadOnlyList<ExpressionToken> postfix =
            ConvertToPostfix(expression);

        var values =
            new Stack<OctoDouble>();

        var steps =
            new List<string>();

        foreach (ExpressionToken token in postfix)
        {
            if (token.Kind == ExpressionTokenKind.Number)
            {
                int decimalSeparatorIndex =
                    token.Text.IndexOf('.');

                if (decimalSeparatorIndex >= 0 &&
                    token.Text.Length - decimalSeparatorIndex - 1 > 10)
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.InvalidNumber);
                }

                // Giới hạn dữ liệu nhập theo System.Decimal trước để tránh
                // tạo OctoDouble cho những literal vượt phạm vi hỗ trợ của UI.
                // OctoDouble chỉ được tạo sau khi token đã hợp lệ và dùng cho
                // các phép tính trung gian/kết quả có độ chính xác cao hơn.
                if (!decimal.TryParse(
                        token.Text,
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out decimal number))
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.NumberOutOfRange);
                }

                values.Push(
                    OctoDouble.FromDecimal(
                        number));
                continue;
            }

            if (token.IsUnary)
            {
                OctoDouble operand = PopDecimal(values);
                values.Push(token.Text == "u-" ? -operand : operand);
                continue;
            }

            OctoDouble right = PopDecimal(values);
            OctoDouble left = PopDecimal(values);
            ArithmeticOperation operation = ToOperation(token.Text);

            if (operation == ArithmeticOperation.Divide &&
                right.IsZero)
            {
                throw new ArithmeticExpressionException(
                    ArithmeticExpressionError.DivisionByZero);
            }

            OctoDouble result = CalculateExpressionDecimal(
                left,
                operation,
                right);

            values.Push(result);
            steps.Add(
                $"{FormatDecimalStep(left)} {GetSymbol(operation)} " +
                $"{FormatDecimalStep(right)} = {FormatDecimalStep(result)}");
        }

        if (values.Count != 1)
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.MissingOperator);
        }

        return new DecimalExpressionResult(
            NormalizeExpressionForDisplay(expression),
            values.Pop(),
            steps);
    }

    private static OctoDouble CalculateExpressionDecimal(
        OctoDouble left,
        ArithmeticOperation operation,
        OctoDouble right)
    {
        return operation switch
        {
            ArithmeticOperation.Add =>
                left + right,

            ArithmeticOperation.Subtract =>
                left - right,

            ArithmeticOperation.Multiply =>
                left * right,

            ArithmeticOperation.Divide =>
                left / right,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation,
                    "Unsupported arithmetic operation.")
        };
    }

    private static QuadDouble CalculateDecimal(
        QuadDouble left,
        ArithmeticOperation operation,
        QuadDouble right)
    {
        return operation switch
        {
            ArithmeticOperation.Add =>
                left + right,

            ArithmeticOperation.Subtract =>
                left - right,

            ArithmeticOperation.Multiply =>
                left * right,

            ArithmeticOperation.Divide =>
                left / right,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation,
                    "Unsupported arithmetic operation.")
        };
    }

    public bool IsEquationCorrect(
        IntegerArithmeticExpression expression,
        BigInteger proposedAnswer)
    {
        IntegerArithmeticResult result =
            CalculateInteger(
                expression);

        // Câu hỏi luyện tập hiện chỉ dùng phép chia hết. Một biểu thức chia
        // có số dư không thể được coi là bằng riêng phần thương.
        return (!result.IsDivision ||
                result.IsExactDivision) &&
               result.Result == proposedAnswer;
    }

    public static string GetSymbol(
        ArithmeticOperation operation)
    {
        return operation switch
        {
            ArithmeticOperation.Add => "+",
            ArithmeticOperation.Subtract => "−",
            ArithmeticOperation.Multiply => "×",
            ArithmeticOperation.Divide => "÷",
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Unsupported arithmetic operation.")
        };
    }

    private static IReadOnlyList<ExpressionToken> ConvertToPostfix(
        string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.Empty);
        }

        if (expression.Length > MaximumExpressionLength)
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.TooLong);
        }

        string normalized = NormalizeExpression(expression);
        var output = new List<ExpressionToken>();
        var operators = new Stack<ExpressionToken>();
        var brackets = new Stack<char>();
        bool expectsOperand = true;

        for (int index = 0; index < normalized.Length;)
        {
            char character = normalized[index];

            if (char.IsWhiteSpace(character))
            {
                index++;
                continue;
            }

            if (char.IsDigit(character) || character == '.')
            {
                if (!expectsOperand)
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.MissingOperator);
                }

                int start = index;
                int decimalPoints = 0;

                while (index < normalized.Length &&
                       (char.IsDigit(normalized[index]) ||
                        normalized[index] == '.'))
                {
                    if (normalized[index] == '.')
                    {
                        decimalPoints++;
                    }

                    index++;
                }

                string number = normalized[start..index];

                if (decimalPoints > 1 ||
                    number == "." ||
                    number.EndsWith('.'))
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.InvalidNumber);
                }

                output.Add(new(ExpressionTokenKind.Number, number));
                expectsOperand = false;
                EnsureTokenLimit(output.Count + operators.Count);
                continue;
            }

            if (IsOpeningBracket(character))
            {
                if (!expectsOperand)
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.MissingOperator);
                }

                if (brackets.TryPeek(out char parent) &&
                    BracketRank(character) > BracketRank(parent))
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.InvalidBracketOrder);
                }

                brackets.Push(character);
                operators.Push(new(ExpressionTokenKind.Bracket, character.ToString()));
                expectsOperand = true;
                index++;
                EnsureTokenLimit(output.Count + operators.Count);
                continue;
            }

            if (IsClosingBracket(character))
            {
                if (expectsOperand ||
                    !brackets.TryPop(out char opening) ||
                    !BracketsMatch(opening, character))
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.MismatchedBracket);
                }

                while (operators.Count > 0 &&
                       operators.Peek().Kind != ExpressionTokenKind.Bracket)
                {
                    output.Add(operators.Pop());
                }

                if (operators.Count == 0)
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.MismatchedBracket);
                }

                operators.Pop();
                expectsOperand = false;
                index++;
                continue;
            }

            if (IsOperator(character))
            {
                string operation;
                bool isUnary = expectsOperand && character is '+' or '-';

                if (expectsOperand && !isUnary)
                {
                    throw new ArithmeticExpressionException(
                        ArithmeticExpressionError.MissingOperand);
                }

                operation = isUnary
                    ? character == '-' ? "u-" : "u+"
                    : character.ToString();

                var token = new ExpressionToken(
                    ExpressionTokenKind.Operator,
                    operation);

                while (operators.Count > 0 &&
                       operators.Peek().Kind == ExpressionTokenKind.Operator &&
                       ShouldPopOperator(operators.Peek(), token))
                {
                    output.Add(operators.Pop());
                }

                operators.Push(token);
                expectsOperand = true;
                index++;
                EnsureTokenLimit(output.Count + operators.Count);
                continue;
            }

            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.InvalidCharacter);
        }

        if (expectsOperand)
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.MissingOperand);
        }

        if (brackets.Count > 0)
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.MismatchedBracket);
        }

        while (operators.Count > 0)
        {
            ExpressionToken token = operators.Pop();

            if (token.Kind == ExpressionTokenKind.Bracket)
            {
                throw new ArithmeticExpressionException(
                    ArithmeticExpressionError.MismatchedBracket);
            }

            output.Add(token);
        }

        return output;
    }

    private static string NormalizeExpression(string expression)
    {
        return expression
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace('−', '-')
            .Replace('–', '-')
            .Replace('×', '*')
            .Replace('x', '*')
            .Replace('X', '*')
            .Replace('÷', '/')
            .Replace(':', '/');
    }

    private static string NormalizeExpressionForDisplay(string expression)
    {
        return NormalizeExpression(expression)
            .Replace('*', '×')
            .Replace('/', '÷')
            .Trim();
    }

    private static bool ShouldPopOperator(
        ExpressionToken stacked,
        ExpressionToken incoming)
    {
        int stackedPrecedence = OperatorPrecedence(stacked.Text);
        int incomingPrecedence = OperatorPrecedence(incoming.Text);
        bool incomingIsRightAssociative = incoming.IsUnary;

        return incomingIsRightAssociative
            ? stackedPrecedence > incomingPrecedence
            : stackedPrecedence >= incomingPrecedence;
    }

    private static int OperatorPrecedence(string operation) =>
        operation switch
        {
            "u+" or "u-" => 3,
            "*" or "/" => 2,
            "+" or "-" => 1,
            _ => 0
        };

    private static ArithmeticOperation ToOperation(string operation) =>
        operation switch
        {
            "+" => ArithmeticOperation.Add,
            "-" => ArithmeticOperation.Subtract,
            "*" => ArithmeticOperation.Multiply,
            "/" => ArithmeticOperation.Divide,
            _ => throw new ArithmeticExpressionException(
                ArithmeticExpressionError.InvalidCharacter)
        };

    private static bool IsOperator(char value) =>
        value is '+' or '-' or '*' or '/';

    private static bool IsOpeningBracket(char value) =>
        value is '(' or '[' or '{';

    private static bool IsClosingBracket(char value) =>
        value is ')' or ']' or '}';

    private static int BracketRank(char value) =>
        value switch
        {
            '(' => 1,
            '[' => 2,
            '{' => 3,
            _ => 0
        };

    private static bool BracketsMatch(char opening, char closing) =>
        (opening, closing) is
            ('(', ')') or
            ('[', ']') or
            ('{', '}');

    private static BigInteger PopInteger(Stack<BigInteger> values)
    {
        if (!values.TryPop(out BigInteger value))
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.MissingOperand);
        }

        return value;
    }

    private static OctoDouble PopDecimal(Stack<OctoDouble> values)
    {
        if (!values.TryPop(out OctoDouble value))
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.MissingOperand);
        }

        return value;
    }

    private static void EnsureTokenLimit(int tokenCount)
    {
        if (tokenCount > MaximumTokenCount)
        {
            throw new ArithmeticExpressionException(
                ArithmeticExpressionError.TooLong);
        }
    }

    private static string FormatDecimalStep(OctoDouble value) =>
        value.ToGeneralString(
            significantDigits: 32,
            scientificUpperExponent: 18,
            scientificLowerExponent: -10);

    private enum ExpressionTokenKind
    {
        Number,
        Operator,
        Bracket
    }

    private readonly record struct ExpressionToken(
        ExpressionTokenKind Kind,
        string Text)
    {
        public bool IsUnary =>
            Kind == ExpressionTokenKind.Operator &&
            Text is "u+" or "u-";
    }
}
