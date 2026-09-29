using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckEssaySolutionRequirements()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;

        foreach ((QuizProblemKind kind, object subtype) in AllSubtypes())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            ArithmeticQuizQuestion question = Generate(
                kind, subtype, ArithmeticQuizMode.Essay, language, 11000 + count);
            string label = $"{kind}/{subtype}/{language}";
            bool required = kind is not (QuizProblemKind.Arithmetic or QuizProblemKind.Fraction);

            Require(EssayAnswerValidator.RequiresSolution(question) == required,
                $"{label}: wrong essay solution requirement.");

            EssayAnswerValidationResult missing = validator.Validate(question, null, null, null);
            Require(missing.SolutionIsCorrect == !required &&
                    missing.SolutionError == (required
                        ? EssayAnswerError.MissingSolution
                        : EssayAnswerError.None),
                $"{label}: blank solution was graded incorrectly.");

            if (required)
            {
                EssayAnswerValidationResult wrong = validator.Validate(
                    question, "Bài giải:", null, null);
                Require(!wrong.SolutionIsCorrect &&
                        wrong.SolutionError == EssayAnswerError.WrongSolutionContent,
                    $"{label}: generic solution passed without naming the quantity.");

                string correctText = BuildSolutionSentence(question, language);
                EssayAnswerValidationResult correct = validator.Validate(
                    question, correctText, null, null);
                Require(correct.SolutionIsCorrect,
                    $"{label}: valid solution sentence was rejected: {correctText}");
            }
            count++;
        }

        // AI word problems still require a sentence even for basic arithmetic
        // and fractions; the exception applies to algorithm-only expressions.
        foreach (QuizProblemKind kind in new[]
            { QuizProblemKind.Arithmetic, QuizProblemKind.Fraction })
        {
            object subtype = kind == QuizProblemKind.Arithmetic
                ? ArithmeticOperation.Add : FractionOperation.Add;
            ArithmeticQuizQuestion question = Generate(
                kind, subtype, ArithmeticQuizMode.Essay, AppLanguage.English, 12000)
                with { WordProblem = new MathWordProblem(
                    "Emma has pens. How many pens?", "The number of pens is:",
                    "pens", "Emma") };
            Require(EssayAnswerValidator.RequiresSolution(question) &&
                    validator.Validate(question, null, null, null).SolutionError ==
                    EssayAnswerError.MissingSolution,
                $"AI {kind}: solution sentence should remain required.");
        }

        Console.WriteLine($"  Checked {count} algorithm essay contracts and AI basic exceptions.");
    }

    private static string BuildSolutionSentence(
        ArithmeticQuizQuestion question, AppLanguage language)
    {
        bool english = language == AppLanguage.English;
        if (question.GeometryProblem is GeometryQuizContract geometry)
        {
            string quantity = (geometry.Measurement, english) switch
            {
                (GeometryMeasurement.Perimeter, true) => "perimeter",
                (GeometryMeasurement.Area, true) => "area",
                (GeometryMeasurement.TotalArea, true) => "total surface area",
                (GeometryMeasurement.Volume, true) => "volume",
                (GeometryMeasurement.Perimeter, false) => "chu vi",
                (GeometryMeasurement.Area, false) => "diện tích",
                (GeometryMeasurement.TotalArea, false) => "diện tích toàn phần",
                _ => "thể tích"
            };
            return english ? $"The {quantity} is:" : $"{quantity} cần tìm là:";
        }

        if (question.FindXProblem is not null)
            return english ? "The value of x is:" : "Giá trị của x là:";

        string unit = question.ProportionProblem?.AnswerUnit ??
                      question.MotionProblem?.AnswerUnit ??
                      question.AverageProblem?.AnswerUnit ??
                      question.PercentageProblem?.AnswerUnit ??
                      throw new InvalidOperationException("Missing answer unit.");
        if (unit == "%")
            return english ? "The percentage is:" : "Tỉ lệ phần trăm là:";

        return english ? $"The number of {unit} is:" : $"Số {unit} cần tìm là:";
    }
}
