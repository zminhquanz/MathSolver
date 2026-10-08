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
    PercentageQuizContract? PercentageProblem = null,
    ExpressionQuizContract? ExpressionProblem = null,
    ElementaryQuizContract? ElementaryProblem = null)
{
    public bool UsesFractionFormatting => FractionProblem is not null || ExpressionProblem?.UsesFractions == true
        || ElementaryProblem?.UsesFractionFormatting == true;

    /// <summary>Every requested result, in display/grading order. Includes both quotient and remainder.</summary>
    public IReadOnlyList<ReducedFraction> ExactAnswers => ElementaryProblem is { } elementary
        ? elementary.Answers.Select(answer => answer.Value).ToArray() : [ExactAnswer];

    /// <summary>Primary result for legacy scalar consumers; use ExactAnswers for multi-part questions.</summary>
    public ReducedFraction ExactAnswer => ElementaryProblem?.Answers[0].Value ?? ExpressionProblem?.CorrectAnswer ??
        FractionProblem?.CorrectAnswer ?? new ReducedFraction(CorrectAnswer, BigInteger.One);
}

public sealed record ArithmeticQuizValidationResult(
    bool IsValid,
    string? ErrorCode)
{
    public static ArithmeticQuizValidationResult Valid { get; } =
        new(true, null);
}
