namespace MathSolver.Models;

public enum RationalExpressionError
{
    Empty,
    TooLong,
    InvalidExpression,
    DivisionByZero,
    TooLarge
}

public sealed class RationalExpressionException(RationalExpressionError error) : FormatException(error.ToString())
{
    public RationalExpressionError Error { get; } = error;
}

public sealed record RationalExpressionResult(
    string Expression,
    ReducedFraction Result,
    bool HasOperation,
    IReadOnlyList<string> Steps);
