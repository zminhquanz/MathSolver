using System.Numerics;

namespace MathSolver.Models;

public enum ArithmeticQuizMode
{
    TrueFalse,
    MultipleChoice,
    Essay
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
    MathWordProblem? WordProblem = null,
    GeometryQuizContract? GeometryProblem = null,
    FindXQuizContract? FindXProblem = null,
    FractionQuizContract? FractionProblem = null,
    ProportionQuizContract? ProportionProblem = null,
    MotionQuizContract? MotionProblem = null,
    AverageQuizContract? AverageProblem = null,
    PercentageQuizContract? PercentageProblem = null);

public sealed record ArithmeticQuizValidationResult(
    bool IsValid,
    string? ErrorCode)
{
    public static ArithmeticQuizValidationResult Valid { get; } =
        new(true, null);
}
