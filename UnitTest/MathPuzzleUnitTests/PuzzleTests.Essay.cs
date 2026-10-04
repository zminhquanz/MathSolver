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
            bool required = kind is not (
                QuizProblemKind.Arithmetic or QuizProblemKind.Fraction or
                QuizProblemKind.FindX);

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

        // word-problem word problems still require a sentence for arithmetic, fractions,
        // and Find X; the exception applies to algorithm-only expressions.
        foreach (QuizProblemKind kind in new[]
            { QuizProblemKind.Arithmetic, QuizProblemKind.Fraction,
              QuizProblemKind.FindX })
        {
            object subtype = kind == QuizProblemKind.Fraction
                ? FractionOperation.Add : ArithmeticOperation.Add;
            ArithmeticQuizQuestion question = Generate(
                kind, subtype, ArithmeticQuizMode.Essay, AppLanguage.English, 12000)
                with { WordProblem = new MathWordProblem(
                    "Emma has pens. How many pens?", "The number of pens is:",
                    "pens", "Emma") };
            Require(EssayAnswerValidator.RequiresSolution(question) &&
                    validator.Validate(question, null, null, null).SolutionError ==
                    EssayAnswerError.MissingSolution,
                $"word-problem {kind}: solution sentence should remain required.");
        }

        foreach (var (unit, lead) in new[] { ("tree", "The number of trees remaining is:"),
            ("stock lot", "The number of stock lots filled is:"), ("response batch", "The number of response batches is:") })
        {
            var q = new ArithmeticQuizQuestion(new(2, ArithmeticOperation.Subtract, 1), ArithmeticQuizMode.Essay, 1,
                null, null, [], new MathWordProblem("How many remain?", lead, unit, "Emma"));
            Require(validator.Validate(q, lead, $"2 - 1 = 1 {unit}", $"1 {unit}").IsCorrect,
                "Plural lead failed for singular answer: " + unit);
            Require(!validator.Validate(q, "The number of apples is:", $"2 - 1 = 1 {unit}", $"1 {unit}").SolutionIsCorrect,
                "Unrelated solution quantity passed: " + unit);
        }
        Console.WriteLine($"  Checked {count} algorithm essay contracts, word-problem basic exceptions and singular/plural solution units.");
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
                (GeometryMeasurement.LateralArea, true) => "lateral surface area",
                (GeometryMeasurement.Volume, true) => "volume",
                (GeometryMeasurement.Perimeter, false) => "chu vi",
                (GeometryMeasurement.Area, false) => "diện tích",
                (GeometryMeasurement.TotalArea, false) => "diện tích toàn phần",
                (GeometryMeasurement.LateralArea, false) => "diện tích xung quanh",
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
