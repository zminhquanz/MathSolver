using MathSolver.Numerics;
using System.Numerics;

namespace MathSolver.Models;

public enum ArithmeticExpressionError
{
    Empty,
    TooLong,
    InvalidCharacter,
    InvalidNumber,
    NumberOutOfRange,
    MissingOperand,
    MissingOperator,
    MismatchedBracket,
    InvalidBracketOrder,
    DivisionByZero,
    NonIntegralDivision
}

public sealed class ArithmeticExpressionException : FormatException
{
    public ArithmeticExpressionException(
        ArithmeticExpressionError error)
        : base(error.ToString())
    {
        Error = error;
    }

    public ArithmeticExpressionError Error { get; }
}

public sealed record IntegerExpressionResult(
    string NormalizedExpression,
    BigInteger ResultNumerator,
    BigInteger ResultDenominator,
    IReadOnlyList<string> Steps);

public sealed record DecimalExpressionResult(
    string NormalizedExpression,
    OctoDouble Result,
    IReadOnlyList<string> Steps);
