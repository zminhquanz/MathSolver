using MathSolver.Models;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Services.Printing;

/// <summary>Owned by the worksheet page. No references to practice controls or mutable practice state.</summary>
public sealed record QuizWorksheetSettings(
    int ProblemIndex,
    QuizProblemRequest? Request,
    CurriculumTier Tier,
    AppLanguage Language,
    QuestionKnowledgeGroup Group = QuestionKnowledgeGroup.Objects,
    bool ArithmeticWords = false,
    bool FindXWords = false,
    bool FractionWords = false,
    bool DecimalWords = false,
    bool MeasurementWords = false)
{
    public QuizProblemKind? Kind => Request?.Kind;
    public bool UsesWords => Kind switch
    {
        QuizProblemKind.Arithmetic => ArithmeticWords,
        QuizProblemKind.FindX => FindXWords,
        QuizProblemKind.Fraction => FractionWords,
        QuizProblemKind.Decimal => DecimalWords,
        QuizProblemKind.Measurement => MeasurementWords,
        _ => false
    };
    public string FormatPrefix => Kind switch
    {
        QuizProblemKind.Arithmetic => "ArithmeticPractice",
        QuizProblemKind.FindX => "FindXPractice",
        QuizProblemKind.Fraction => "FractionBank",
        QuizProblemKind.Decimal => "DecimalPractice",
        QuizProblemKind.Measurement => "MeasurementPractice",
        _ => ""
    };
    public bool CanChooseFormat => Kind == QuizProblemKind.FindX
        || Kind is (QuizProblemKind.Arithmetic or QuizProblemKind.Fraction) && Request?.IsComparison != true
        || Kind == QuizProblemKind.Decimal && Request?.ElementaryType is (ElementaryQuizType.DecimalAdd
            or ElementaryQuizType.DecimalSubtract or ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide)
        || Kind == QuizProblemKind.Measurement && ElementaryQuizGenerator.SupportsMeasurementPractice(Request?.ElementaryType);
    public bool CanChooseGroup => CanChooseFormat && Kind is not (QuizProblemKind.Decimal or QuizProblemKind.Measurement) && UsesWords;

    public QuizWorksheetSettings SelectProblem(int index) => this with
    { ProblemIndex = index, Request = new QuizProblemTypeCatalog().GetFixedRequest(index) };

    public QuizWorksheetSettings SelectWords(bool words) => Kind switch
    {
        QuizProblemKind.Arithmetic => this with { ArithmeticWords = words },
        QuizProblemKind.FindX => this with { FindXWords = words },
        QuizProblemKind.Fraction => this with { FractionWords = words },
        QuizProblemKind.Decimal => this with { DecimalWords = words },
        QuizProblemKind.Measurement => this with { MeasurementWords = words },
        _ => this
    };

    public QuizWorksheetSettings SelectSubtype(QuizProblemRequest request)
    {
        if (request.Kind != Kind) throw new ArgumentException("Worksheet subtype belongs to another family.", nameof(request));
        if (request.Kind == QuizProblemKind.Geometry)
        {
            var measurement = Request?.GeometryMeasurement;
            request = request with { GeometryMeasurement = measurement.HasValue
                && GeometryQuizGenerator.GetAvailableMeasurements(request.GeometryShape).Contains(measurement.Value)
                    ? measurement : null };
        }
        return this with { Request = request };
    }

    public QuizProblemRequest Resolve(QuizProblemTypeCatalog catalog)
    {
        var r = Request;
        return catalog.Resolve(ProblemIndex, r?.ArithmeticOperation, r?.FractionOperation, r?.ProportionType,
            r?.AverageType, r?.PercentageType, r?.FindXOperation, r?.GeometryShape, r?.MotionType, Tier,
            expressionType: r?.ExpressionType, geometryMeasurement: r?.GeometryMeasurement, elementaryType: r?.ElementaryType,
            basicComparison: Kind == QuizProblemKind.Arithmetic && r?.IsComparison == true,
            fractionComparison: Kind == QuizProblemKind.Fraction && r?.IsComparison == true);
    }

    public Task<ArithmeticQuizQuestion> GenerateAsync(PracticeQuestionFactory factory, QuizProblemTypeCatalog catalog,
        CancellationToken cancellation = default) => factory.CreateAsync(Resolve(catalog), ArithmeticQuizMode.Essay,
            new(Tier, Request is null), Language, Request.HasValue ? new(Group) : null,
            Request.HasValue ? UsesWords ? PracticeQuestionFormat.WordProblem : PracticeQuestionFormat.Numeric
                : PracticeQuestionFormat.Mixed,
            Kind == QuizProblemKind.Fraction && FractionWords, cancellation);
}

/// <summary>Typed choices use the same registered families, subtypes and illustration keys as practice.</summary>
public static class QuizWorksheetChoices
{
    public sealed record Subtype(string Key, QuizProblemRequest Request);
    public static IReadOnlyList<Subtype> Subtypes(QuizProblemKind kind)
    {
        var result = new List<Subtype> { new("Quiz.SubtypeMixed", new(kind)) };
        void Add<T>(string[] keys, Func<T, QuizProblemRequest> request, Func<T, bool>? include = null) where T : struct, Enum
        {
            var values = Enum.GetValues<T>().Where(v => include?.Invoke(v) ?? true).ToArray();
            if (keys.Length != values.Length) throw new InvalidOperationException("Worksheet subtype keys out of sync.");
            for (int i = 0; i < values.Length; i++) result.Add(new(keys[i], request(values[i])));
        }
        switch (kind)
        {
            case QuizProblemKind.Arithmetic:
                result.Add(new("Quiz.ComparisonSubtype", new(kind, IsComparison: true)));
                Add<ArithmeticOperation>(["AiBank.Add", "AiBank.Subtract", "AiBank.Multiply", "AiBank.Divide"], v => new(kind, ArithmeticOperation: v));
                break;
            case QuizProblemKind.Fraction:
                result.Add(new("Quiz.ComparisonSubtype", new(kind, IsComparison: true)));
                Add<FractionOperation>(["AiBank.Add", "AiBank.Subtract", "AiBank.Multiply", "AiBank.Divide"], v => new(kind, FractionOperation: v),
                    v => v != FractionOperation.CommonDenominator);
                break;
            case QuizProblemKind.FindX:
                Add<ArithmeticOperation>(["Quiz.FindXSum", "Quiz.FindXDifference", "Quiz.FindXProduct", "Quiz.FindXQuotient"], v => new(kind, FindXOperation: v));
                break;
            case QuizProblemKind.Geometry:
                Add<GeometryQuizShape>(Enum.GetValues<GeometryQuizShape>().Select(v => "Quiz.Geometry" + v).ToArray(), v => new(kind, GeometryShape: v));
                break;
            case QuizProblemKind.Proportion:
                Add<ProportionQuizType>(["Quiz.ProportionDirect", "Quiz.ProportionInverse"], v => new(kind, ProportionType: v));
                break;
            case QuizProblemKind.Motion:
                Add<MotionQuizType>(["Quiz.MotionBasic", "Quiz.MotionChasing", "Quiz.MotionMeeting", "Quiz.MotionRiver"], v => new(kind, MotionType: v));
                break;
            case QuizProblemKind.Average:
                Add<AverageQuizType>(["Quiz.AverageDirect", "Quiz.AverageTotalToAverage", "Quiz.AverageAverageToTotal", "Quiz.AverageMissingValue", "Quiz.AverageIndirectData", "Quiz.AverageTwoGroups"], v => new(kind, AverageType: v));
                break;
            case QuizProblemKind.Percentage:
                Add<PercentageQuizType>(["Quiz.PercentageRatio", "Quiz.PercentageValue", "Quiz.PercentageWhole", "Quiz.PercentageRemaining"], v => new(kind, PercentageType: v));
                break;
            case QuizProblemKind.Expression:
                Add<ExpressionQuizType>(["Quiz.ExpressionInteger", "Quiz.ExpressionIntegerBrackets", "Quiz.ExpressionFraction", "Quiz.ExpressionFractionBrackets"], v => new(kind, ExpressionType: v));
                break;
            default:
                foreach (var type in ElementaryQuizGenerator.Types(kind))
                    result.Add(new("Quiz.Elementary." + type, new(kind, ElementaryType: type)));
                break;
        }
        return result.AsReadOnly();
    }
}
