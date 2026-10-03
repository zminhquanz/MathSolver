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
    ClassifyAngle, ParallelLines, PerpendicularLines, CountSides, RectangleSide, CompositeArea,
    IntegerCompare
}

public sealed record ElementaryAnswer(string Label, ReducedFraction Value, string Unit, string Expression,
    string? Text = null, IReadOnlyList<string>? Aliases = null, bool RequireReduced = false,
    int? RequiredDenominator = null, bool RequireMixedNumber = false, string? DisplayValue = null)
{
    public bool IsText => Text is not null && !RequireMixedNumber && RequiredDenominator is null;
}

public sealed record QuizVisualLine(string Label, decimal DirectionDegrees, float OffsetRatio = 0);

/// <summary>Possible outcomes for classification, or observed trials for an experimental fraction.</summary>
public sealed record ProbabilityQuizScenario(string ContextId, string EventText,
    int EventCount, int TotalCount, bool UsesObservedResults);

public sealed record QuizVisualPoint(decimal X, decimal Y, string Label = "");
public sealed record QuizVisualPolygon(IReadOnlyList<QuizVisualPoint> Vertices, string Label = "",
    bool IsCutout = false);
public sealed record QuizVisualAnnotation(string Text, decimal X, decimal Y);

public sealed record QuizVisualData(string Kind, IReadOnlyList<string> Labels,
    IReadOnlyList<decimal> Values, string Unit, decimal RotationDegrees = 0,
    IReadOnlyList<QuizVisualLine>? Lines = null,
    IReadOnlyList<QuizVisualPolygon>? Polygons = null,
    IReadOnlyList<QuizVisualAnnotation>? Annotations = null, string? ScenarioId = null,
    IReadOnlySet<int>? HiddenValueIndices = null);

/// <summary>C# math puzzle data and rules.</summary>
public sealed record ElementaryQuizContract(QuizProblemKind Kind, ElementaryQuizType Type,
    AppLanguage Language, string ProblemText, string SolutionText,
    IReadOnlyList<string> Facts, IReadOnlyList<string> Constants,
    IReadOnlyList<ElementaryAnswer> Answers, bool RequiresSolution,
    QuizVisualData? Visual = null, string? PresentedText = null,
    IReadOnlyList<string>? ChoiceTexts = null)
{
    public ProbabilityQuizScenario? ProbabilityScenario { get; init; }
    public ElementaryQuizReasoning? Reasoning { get; init; }
    public string? ComparisonLeftExpression { get; init; }
    public string? ComparisonRightExpression { get; init; }

    public bool UsesFractionFormatting => Type is ElementaryQuizType.ReduceFraction or
        ElementaryQuizType.CompareFractions or ElementaryQuizType.MixedNumber or
        ElementaryQuizType.CommonDenominator or ElementaryQuizType.FractionOfNumber or
        ElementaryQuizType.WholeFromFraction or ElementaryQuizType.ExperimentalProbability;

    public bool IsComparison => Type is ElementaryQuizType.IntegerCompare or
        ElementaryQuizType.DecimalCompare or ElementaryQuizType.CompareFractions;

    public (string Left, string Right) ComparisonOperands => Type == ElementaryQuizType.CompareFractions
        ? (ComparisonLeftExpression ?? $"{Facts[0]}/{Facts[1]}", ComparisonRightExpression ?? $"{Facts[2]}/{Facts[3]}") : (Facts[0], Facts[1]);

    public string FormatComparison(string symbol)
    {
        var (left, right) = ComparisonOperands;
        return $"{left} {symbol} {right}";
    }

    public string AnswerText => string.Join("; ", Answers.Select(answer =>
        (Answers.Count > 1 ? answer.Label + ": " : "") + FormatAnswer(answer)));
    public static string FormatAnswer(ElementaryAnswer answer) =>
        (answer.DisplayValue ?? answer.Text ?? answer.Value.ToString()) + (answer.Unit.Length == 0 ? "" : " " + answer.Unit);
}
