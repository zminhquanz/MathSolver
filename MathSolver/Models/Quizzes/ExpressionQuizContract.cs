using MathSolver.Services;

namespace MathSolver.Models;

public enum ExpressionQuizType
{
    Integer,
    IntegerWithBrackets,
    Fraction,
    FractionWithBrackets
}

/// <summary>Numeric expression owned by the algorithm, with an exact rational answer.</summary>
public sealed record ExpressionQuizContract(
    ExpressionQuizType Type,
    CurriculumTier Tier,
    string ExpressionText,
    int OperandCount,
    int BracketPairCount,
    ReducedFraction CorrectAnswer,
    ReducedFraction? PresentedAnswer,
    IReadOnlyList<ReducedFraction> Choices)
{
    public bool UsesFractions => Type is ExpressionQuizType.Fraction or ExpressionQuizType.FractionWithBrackets;
    /// <summary>C#-derived equivalent arrangements for a word problem, never model output.</summary>
    public IReadOnlyList<string> EquivalentExpressions { get; init; } = [];
}
