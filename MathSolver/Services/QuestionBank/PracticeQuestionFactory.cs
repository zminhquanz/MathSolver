using MathSolver.Models;
using MathSolver.Services.Core;

namespace MathSolver.Services.QuestionBank;

/// <summary>Shared by interactive practice and printable sets. Uses C# and stored prose, never inference.</summary>
public sealed class PracticeQuestionFactory(BasicPracticeQuestionProvider practice, Random? random = null)
{
    private readonly Random _groupRandom = random ?? Random.Shared;
    private readonly ArithmeticQuizGenerator _arithmetic = new(new BasicArithmeticEngine());
    private readonly FractionQuizGenerator _fraction = new(new FractionCalculationEngine());
    private readonly GeometryQuizGenerator _geometry = new(new GeometryCalculationEngine());
    private readonly FindXQuizGenerator _findX = new(new FindXEngine());
    private readonly ProportionQuizGenerator _proportion = new();
    private readonly MotionQuizGenerator _motion = new();
    private readonly AverageQuizGenerator _average = new();
    private readonly PercentageQuizGenerator _percentage = new();
    private readonly ExpressionQuizGenerator _expression = new();
    private readonly ElementaryQuizGenerator _elementary = new(expandedNumberPractice: true);

    public async Task<ArithmeticQuizQuestion> CreateAsync(QuizProblemRequest request, ArithmeticQuizMode mode,
        QuizCurriculumContext curriculum, AppLanguage language, QuestionLearningProfile? learning,
        PracticeQuestionFormat format, bool fractionWordProblems, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (request.Kind == QuizProblemKind.Arithmetic && !request.IsComparison && learning is not null
            && (request.ArithmeticOperation is not { } operation || !learning.Allows(operation)))
        {
            var allowed = Enum.GetValues<ArithmeticOperation>().Where(learning.Allows).ToArray();
            request = request with { ArithmeticOperation = allowed[Random.Shared.Next(allowed.Length)] };
        }
        var question = request.Kind switch
        {
            _ when request.IsComparison => _elementary.GenerateComparison(mode, request.Kind, language, curriculum.Tier),
            QuizProblemKind.Measurement when ElementaryQuizGenerator.SupportsMeasurementPractice(request.ElementaryType)
                && format == PracticeQuestionFormat.Numeric
                => _elementary.GenerateMeasurementPractice(mode, request.ElementaryType!.Value, language, curriculum.Tier),
            QuizProblemKind.Decimal when request.ElementaryType is (ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract
                or ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide) && format != PracticeQuestionFormat.Mixed
                => _elementary.GenerateDecimalArithmetic(mode, request.ElementaryType.Value, language, curriculum.Tier, format == PracticeQuestionFormat.WordProblem),
            _ when ElementaryQuizGenerator.Supports(request.Kind) => _elementary.Generate(mode, request.Kind, request.ElementaryType, language, curriculum.Tier),
            QuizProblemKind.Expression => _expression.Generate(mode, request.ExpressionType, curriculum.Tier),
            QuizProblemKind.Geometry => _geometry.GenerateAlgorithm(mode, language, request.GeometryShape, curriculum, request.GeometryMeasurement),
            QuizProblemKind.Arithmetic => _arithmetic.Generate(mode, request.ArithmeticOperation, curriculum, expandedPractice: true),
            QuizProblemKind.Fraction => _fraction.Generate(mode, request.FractionOperation, curriculum),
            QuizProblemKind.FindX => _findX.Generate(mode, request.FindXOperation, curriculum),
            QuizProblemKind.Proportion => _proportion.GenerateAlgorithm(mode, request.ProportionType, language, curriculum),
            QuizProblemKind.Motion => _motion.GenerateAlgorithm(mode, language, request.MotionType, curriculum),
            QuizProblemKind.Average => _average.GenerateAlgorithm(mode, request.AverageType, language, curriculum),
            QuizProblemKind.Percentage => _percentage.GenerateAlgorithm(mode, request.PercentageType, language, curriculum),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        if (learning is { IsMixed: true } && !request.IsComparison)
        {
            // Resolve each question separately, using its actual operation. A mixed
            // selection never reaches a prose contract or a SQLite group filter.
            ArithmeticOperation? mixedOperation = request.Kind switch
            {
                QuizProblemKind.Arithmetic when format != PracticeQuestionFormat.Numeric => question.Expression.Operation,
                QuizProblemKind.FindX when format != PracticeQuestionFormat.Numeric => question.FindXProblem!.Operation,
                QuizProblemKind.Fraction when fractionWordProblems => (ArithmeticOperation)question.FractionProblem!.Operation,
                _ => null
            };
            if (mixedOperation is { } selected)
            {
                var eligible = QuestionLearningProfile.Groups().Where(group =>
                {
                    var profile = new QuestionLearningProfile(group);
                    return request.Kind switch
                    {
                        QuizProblemKind.FindX => FindXQuestionCatalogue.Available(profile, selected, curriculum.Tier).Any(),
                        QuizProblemKind.Fraction => FractionQuestionCatalogue.Available(profile, selected, curriculum.Tier).Any(),
                        _ => AppliedQuestionCatalogue.Available(profile, selected, curriculum.Tier).Any()
                    };
                }).ToArray();
                if (eligible.Length == 0) throw new InvalidOperationException("No supported knowledge group for this question.");
                learning = new(eligible[_groupRandom.Next(eligible.Length)]);
            }
        }
        if (request.Kind == QuizProblemKind.Arithmetic && !request.IsComparison)
            question = await practice.SelectAsync(question, curriculum.Tier, language, cancellation, learning, format, expandedPractice: true);
        if (request.Kind == QuizProblemKind.FindX)
            question = await practice.SelectFindXAsync(question, curriculum.Tier, language, learning ?? new(QuestionKnowledgeGroup.Objects), cancellation, format);
        if (request.Kind == QuizProblemKind.Fraction && !request.IsComparison && fractionWordProblems)
            question = await practice.SelectFractionAsync(question, curriculum.Tier, language, learning ?? new(QuestionKnowledgeGroup.Objects), cancellation);
        if (request.Kind is QuizProblemKind.TwoNumbers or QuizProblemKind.Average or QuizProblemKind.Percentage or QuizProblemKind.MultiStep
            or QuizProblemKind.Motion or QuizProblemKind.Proportion or QuizProblemKind.Remainder or QuizProblemKind.Geometry or QuizProblemKind.Probability
            || question.ElementaryProblem is { Kind: QuizProblemKind.Data, DataChart: not null }
            || question.ElementaryProblem is { Kind: QuizProblemKind.Decimal, IsDecimalArithmetic: true, StoryContextId: not null }
            || question.ElementaryProblem is { Kind: QuizProblemKind.Time } time && ElementaryQuizGenerator.TimeStoryTypes.Contains(time.Type)
            || question.ElementaryProblem is { Kind: QuizProblemKind.FractionSkills } fraction && ElementaryQuizGenerator.FractionQuantityStoryTypes.Contains(fraction.Type)
            || question.ElementaryProblem is { Kind: QuizProblemKind.Measurement, IsNumericMeasurementConversion: false } measurement && ElementaryQuizGenerator.MeasurementBankTypes.Contains(measurement.Type))
            question = await practice.SelectReasoningAsync(question, curriculum.Tier, language, cancellation);
        cancellation.ThrowIfCancellationRequested();
        return question;
    }
}
