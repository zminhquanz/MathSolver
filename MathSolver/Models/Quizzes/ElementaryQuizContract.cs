using MathSolver.Services;

namespace MathSolver.Models;

public enum ElementaryQuizType
{
    SumDifference, SumRatio, DifferenceRatio,
    LengthConversion, MassConversion, CapacityConversion, AreaConversion, VolumeConversion, MixedLength,
    ElapsedTime, TimeAddition, ReadClock, Calendar,
    QuotientRemainder, MinimumGroups, Leftovers,
    DecimalAdd, DecimalSubtract, DecimalMultiply, DecimalDivide, DecimalRound, DecimalCompare,
    ReduceFraction, CompareFractions, MixedNumber, CommonDenominator, FractionOfNumber, WholeFromFraction,
    ReadTable, ReadBarChart, ReadPieChart, ChartTotal, ChartDifference,
    Likelihood, ExperimentalProbability,
    ClassifyAngle, ParallelLines, PerpendicularLines, CountSides, RectangleSide, CompositeArea
}

public sealed record ElementaryAnswer(string Label, ReducedFraction Value, string Unit, string Expression,
    string? Text = null, IReadOnlyList<string>? Aliases = null, bool RequireReduced = false,
    int? RequiredDenominator = null, bool RequireMixedNumber = false, string? DisplayValue = null)
{
    public bool IsText => Text is not null && !RequireMixedNumber && RequiredDenominator is null;
}

public sealed record QuizVisualData(string Kind, IReadOnlyList<string> Labels,
    IReadOnlyList<decimal> Values, string Unit);

/// <summary>C# owns every relation, answer and visual; AI may rewrite introductory wording only.</summary>
public sealed record ElementaryQuizContract(QuizProblemKind Kind, ElementaryQuizType Type,
    AppLanguage Language, string ProblemText, string SolutionText,
    IReadOnlyList<string> Facts, IReadOnlyList<string> Constants,
    IReadOnlyList<ElementaryAnswer> Answers, bool RequiresSolution,
    QuizVisualData? Visual = null, string? PresentedText = null,
    IReadOnlyList<string>? ChoiceTexts = null)
{
    public string AnswerText => string.Join("; ", Answers.Select(answer =>
        (Answers.Count > 1 ? answer.Label + ": " : "") + FormatAnswer(answer)));
    public static string FormatAnswer(ElementaryAnswer answer) =>
        (answer.DisplayValue ?? answer.Text ?? answer.Value.ToString()) + (answer.Unit.Length == 0 ? "" : " " + answer.Unit);
}
