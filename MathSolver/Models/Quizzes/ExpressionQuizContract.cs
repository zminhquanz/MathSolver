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
    IReadOnlyList<ReducedFraction> Choices,
    ExpressionStoryContract? Story = null)
{
    public bool UsesFractions => Type is ExpressionQuizType.Fraction or ExpressionQuizType.FractionWithBrackets;
}

/// <summary>A verbal calculation plan generated from the same tree as the exact expression.</summary>
public sealed record ExpressionStoryContract(
    AppLanguage Language, string ContextName, string CalculationPlan,
    MathWordProblem ReferenceProblem);
