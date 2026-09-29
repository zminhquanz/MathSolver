using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckAlgorithmMatrix()
    {
        int count = 0;
        foreach ((QuizProblemKind kind, object subtype) in AllSubtypes())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        foreach (ArithmeticQuizMode mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            string label = $"{kind}/{subtype}/{language}/{mode}";
            ArithmeticQuizQuestion question = Generate(
                kind, subtype, mode, language, 1000 + count);
            Require(question.Mode == mode, $"{label}: answer mode changed.");

            if (kind == QuizProblemKind.Fraction)
            {
                FractionQuizContract fraction = question.FractionProblem
                    ?? throw new InvalidOperationException($"{label}: missing fraction contract.");
                Require(fraction.Operation == (FractionOperation)subtype,
                    $"{label}: wrong fraction operation.");
                Require(fraction.CorrectAnswer == ComputeFraction(fraction),
                    $"{label}: incorrect fraction answer.");
                CheckFractionAnswerMode(fraction, question, label);
            }
            else
            {
                CheckIntegerAnswerMode(question, label);
                CheckIntegerContract(kind, subtype, question, label);
            }
            count++;
        }
        Console.WriteLine($"  Checked {count} subtype/language/mode combinations.");
    }

    private static void CheckIntegerContract(
        QuizProblemKind kind, object subtype,
        ArithmeticQuizQuestion question, string label)
    {
        switch (kind)
        {
            case QuizProblemKind.Arithmetic:
                Require(question.Expression.Operation == (ArithmeticOperation)subtype,
                    $"{label}: wrong operation.");
                Require(new ArithmeticQuizValidator(new BasicArithmeticEngine())
                    .Validate(question).IsValid,
                    $"{label}: arithmetic validator rejected question.");
                Require(Evaluate(question.Expression.LeftOperand,
                        question.Expression.Operation,
                        question.Expression.RightOperand) == question.CorrectAnswer,
                    $"{label}: arithmetic answer is incorrect.");
                break;
            case QuizProblemKind.FindX:
                FindXQuizContract findX = question.FindXProblem!;
                Require(findX.Operation == (ArithmeticOperation)subtype,
                    $"{label}: wrong find-x operation.");
                BigInteger left = findX.UnknownIsLeftOperand
                    ? findX.CorrectAnswer : findX.KnownValue;
                BigInteger right = findX.UnknownIsLeftOperand
                    ? findX.KnownValue : findX.CorrectAnswer;
                Require(Evaluate(left, findX.Operation, right) == findX.ResultValue &&
                        findX.CorrectAnswer == question.CorrectAnswer,
                    $"{label}: x does not satisfy the equation.");
                break;
            case QuizProblemKind.Geometry:
                GeometryQuizContract geometry = question.GeometryProblem!;
                string expectedShapeId = (GeometryQuizShape)subtype switch
                {
                    GeometryQuizShape.Square => "square",
                    GeometryQuizShape.Rectangle => "rectangle",
                    GeometryQuizShape.Triangle => "triangle",
                    GeometryQuizShape.Trapezoid => "trapezoid",
                    GeometryQuizShape.Rhombus => "rhombus",
                    GeometryQuizShape.Parallelogram => "parallelogram",
                    GeometryQuizShape.Circle => "circle",
                    GeometryQuizShape.Cube => "cube",
                    GeometryQuizShape.RectangularPrism => "rectangular_prism",
                    _ => throw new ArgumentOutOfRangeException()
                };
                Require(geometry.CorrectAnswer == question.CorrectAnswer &&
                        geometry.ShapeId == expectedShapeId &&
                        geometry.Dimensions.Count > 0 && geometry.CorrectAnswer > 0,
                    $"{label}: invalid geometry dimensions or answer.");
                break;
            case QuizProblemKind.Proportion:
                ProportionQuizContract proportion = question.ProportionProblem!;
                Require(proportion.Type == (ProportionQuizType)subtype,
                    $"{label}: wrong proportion type.");
                BigInteger expectedProportion = proportion.Type == ProportionQuizType.Direct
                    ? (BigInteger)proportion.B * proportion.C / proportion.A
                    : (BigInteger)proportion.A * proportion.B / proportion.C
                      - (proportion.AsksForAdditionalPeople ? proportion.A : 0);
                Require(proportion.CorrectAnswer == expectedProportion &&
                        question.CorrectAnswer == expectedProportion,
                    $"{label}: incorrect proportion answer.");
                break;
            case QuizProblemKind.Motion:
                MotionQuizContract motion = question.MotionProblem!;
                Require(motion.Type == (MotionQuizType)subtype &&
                        motion.CorrectAnswer == question.CorrectAnswer &&
                        motion.Facts.Count > 0,
                    $"{label}: motion contract mismatch.");
                break;
            case QuizProblemKind.Average:
                AverageQuizContract average = question.AverageProblem!;
                Require(average.Type == (AverageQuizType)subtype &&
                        average.CorrectAnswer == question.CorrectAnswer &&
                        average.Facts.Count > 0,
                    $"{label}: average contract mismatch.");
                break;
            case QuizProblemKind.Percentage:
                PercentageQuizContract percentage = question.PercentageProblem!;
                Require(percentage.Type == (PercentageQuizType)subtype,
                    $"{label}: wrong percentage type.");
                BigInteger expectedPercentage = percentage.Type switch
                {
                    PercentageQuizType.FindPercentageRatio =>
                        percentage.Facts[1] * 100 / percentage.Facts[0],
                    PercentageQuizType.FindPercentageValue =>
                        percentage.Facts[0] * percentage.Facts[1] / 100,
                    _ => percentage.Facts[0] * 100 / percentage.Facts[1]
                };
                Require(percentage.CorrectAnswer == expectedPercentage &&
                        question.CorrectAnswer == expectedPercentage,
                    $"{label}: incorrect percentage answer.");
                break;
        }
    }

    private static ReducedFraction ComputeFraction(FractionQuizContract contract)
    {
        ReducedFraction a = contract.LeftOperand;
        ReducedFraction b = contract.RightOperand;
        return contract.Operation switch
        {
            FractionOperation.Add => new(
                a.Numerator * b.Denominator + b.Numerator * a.Denominator,
                a.Denominator * b.Denominator),
            FractionOperation.Subtract => new(
                a.Numerator * b.Denominator - b.Numerator * a.Denominator,
                a.Denominator * b.Denominator),
            FractionOperation.Multiply => new(
                a.Numerator * b.Numerator, a.Denominator * b.Denominator),
            FractionOperation.Divide => new(
                a.Numerator * b.Denominator, a.Denominator * b.Numerator),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static void CheckIntegerAnswerMode(ArithmeticQuizQuestion question, string label)
    {
        switch (question.Mode)
        {
            case ArithmeticQuizMode.TrueFalse:
                Require(question.PresentedAnswer.HasValue &&
                        question.PresentedEquationIsCorrect ==
                        (question.PresentedAnswer.Value == question.CorrectAnswer) &&
                        question.Choices.Count == 0,
                    $"{label}: malformed true/false answer.");
                break;
            case ArithmeticQuizMode.MultipleChoice:
                Require(question.PresentedAnswer is null &&
                        question.Choices.Count == 4 &&
                        question.Choices.Distinct().Count() == 4 &&
                        question.Choices.Count(value => value == question.CorrectAnswer) == 1,
                    $"{label}: malformed multiple-choice answer.");
                break;
            case ArithmeticQuizMode.Essay:
                Require(question.PresentedAnswer is null &&
                        question.PresentedEquationIsCorrect is null &&
                        question.Choices.Count == 0,
                    $"{label}: malformed essay answer.");
                break;
        }
    }

    private static void CheckFractionAnswerMode(
        FractionQuizContract fraction, ArithmeticQuizQuestion question, string label)
    {
        switch (question.Mode)
        {
            case ArithmeticQuizMode.TrueFalse:
                Require(fraction.PresentedAnswer.HasValue &&
                        question.PresentedEquationIsCorrect ==
                        (fraction.PresentedAnswer.Value == fraction.CorrectAnswer) &&
                        fraction.Choices.Count == 0,
                    $"{label}: malformed fraction true/false answer.");
                break;
            case ArithmeticQuizMode.MultipleChoice:
                Require(fraction.Choices.Count == 4 &&
                        fraction.Choices.Distinct().Count() == 4 &&
                        fraction.Choices.Count(value => value == fraction.CorrectAnswer) == 1,
                    $"{label}: malformed fraction choices.");
                break;
            case ArithmeticQuizMode.Essay:
                Require(fraction.PresentedAnswer is null &&
                        fraction.Choices.Count == 0,
                    $"{label}: malformed fraction essay answer.");
                break;
        }
    }
}
