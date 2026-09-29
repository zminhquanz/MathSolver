using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    internal static void CheckCatalog()
    {
        var catalog = new QuizProblemTypeCatalog(new Random(17));
        QuizProblemKind[] registered = catalog.Options
            .Where(option => option.FixedRequest.HasValue)
            .Select(option => option.FixedRequest!.Value.Kind)
            .ToArray();
        Require(registered.Distinct().Count() == Enum.GetValues<QuizProblemKind>().Length,
            "Catalog does not register each problem kind exactly once.");
        Require(catalog.Options.Count(option => option.IsMixed) == 1,
            "Catalog must contain one mixed option.");
        foreach (QuizProblemKind kind in Enum.GetValues<QuizProblemKind>())
            Require(registered.Contains(kind), $"Missing catalog kind {kind}.");
    }

    private static IEnumerable<(QuizProblemKind Kind, object Subtype)> AllSubtypes()
    {
        foreach (ArithmeticOperation operation in Enum.GetValues<ArithmeticOperation>())
        {
            yield return (QuizProblemKind.Arithmetic, operation);
            yield return (QuizProblemKind.FindX, operation);
        }
        foreach (FractionOperation operation in new[]
            { FractionOperation.Add, FractionOperation.Subtract,
              FractionOperation.Multiply, FractionOperation.Divide })
            yield return (QuizProblemKind.Fraction, operation);
        foreach (GeometryQuizShape shape in Enum.GetValues<GeometryQuizShape>())
            yield return (QuizProblemKind.Geometry, shape);
        foreach (ProportionQuizType type in Enum.GetValues<ProportionQuizType>())
            yield return (QuizProblemKind.Proportion, type);
        foreach (MotionQuizType type in Enum.GetValues<MotionQuizType>())
            yield return (QuizProblemKind.Motion, type);
        foreach (AverageQuizType type in Enum.GetValues<AverageQuizType>())
            yield return (QuizProblemKind.Average, type);
        foreach (PercentageQuizType type in Enum.GetValues<PercentageQuizType>())
            yield return (QuizProblemKind.Percentage, type);
    }

    private static ArithmeticQuizQuestion Generate(
        QuizProblemKind kind, object subtype, ArithmeticQuizMode mode,
        AppLanguage language, int seed)
    {
        AppLanguageManager.CurrentLanguage = language;
        var curriculum = new QuizCurriculumContext(CurriculumTier.FiveStars, false);
        return kind switch
        {
            QuizProblemKind.Arithmetic =>
                new ArithmeticQuizGenerator(new BasicArithmeticEngine(), new Random(seed))
                    .Generate(mode, (ArithmeticOperation)subtype, curriculum),
            QuizProblemKind.Fraction =>
                new FractionQuizGenerator(new FractionCalculationEngine(), new Random(seed))
                    .Generate(mode, (FractionOperation)subtype, curriculum),
            QuizProblemKind.Geometry =>
                new GeometryQuizGenerator(new GeometryCalculationEngine(), new Random(seed))
                    .GenerateAlgorithm(mode, language, (GeometryQuizShape)subtype, curriculum),
            QuizProblemKind.FindX =>
                new FindXQuizGenerator(new FindXEngine(), new Random(seed))
                    .Generate(mode, (ArithmeticOperation)subtype, curriculum),
            QuizProblemKind.Proportion =>
                new ProportionQuizGenerator(new Random(seed))
                    .GenerateAlgorithm(mode, (ProportionQuizType)subtype, language, curriculum),
            QuizProblemKind.Motion =>
                new MotionQuizGenerator(new Random(seed))
                    .GenerateAlgorithm(mode, language, (MotionQuizType)subtype, curriculum),
            QuizProblemKind.Average =>
                new AverageQuizGenerator(new Random(seed))
                    .GenerateAlgorithm(mode, (AverageQuizType)subtype, language, curriculum),
            QuizProblemKind.Percentage =>
                new PercentageQuizGenerator(new Random(seed))
                    .GenerateAlgorithm(mode, (PercentageQuizType)subtype, language, curriculum),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static BigInteger Evaluate(
        BigInteger left, ArithmeticOperation operation, BigInteger right) =>
        operation switch
        {
            ArithmeticOperation.Add => left + right,
            ArithmeticOperation.Subtract => left - right,
            ArithmeticOperation.Multiply => left * right,
            ArithmeticOperation.Divide => left / right,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
}
