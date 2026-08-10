using System.Numerics;

namespace MathSolver.Models;

public enum ArithmeticQuizMode
{
    TrueFalse,
    MultipleChoice
}

/// <summary>
/// Câu hỏi đã hoàn thiện và có thể hiển thị trực tiếp.
/// </summary>
public sealed record ArithmeticQuizQuestion(
    IntegerArithmeticExpression Expression,
    ArithmeticQuizMode Mode,
    BigInteger CorrectAnswer,
    BigInteger? PresentedAnswer,
    bool? PresentedEquationIsCorrect,
    IReadOnlyList<BigInteger> Choices,
    MathWordProblem? WordProblem = null);

public sealed record ArithmeticQuizValidationResult(
    bool IsValid,
    string? ErrorCode)
{
    public static ArithmeticQuizValidationResult Valid { get; } =
        new(true, null);
}
