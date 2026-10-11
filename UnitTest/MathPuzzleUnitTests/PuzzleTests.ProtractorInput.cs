using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckProtractorInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 20; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Measurement, ElementaryQuizType.ReadProtractor, language, tier);
            Check(question);
        }

        // Check the requested examples and the end points of a protractor scale.
        var sample = new ElementaryQuizGenerator(new Random(17)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.Measurement, ElementaryQuizType.ReadProtractor, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        foreach (int angle in new[] { 0, 90, 180 })
            Check(sample with { ElementaryProblem = sample.ElementaryProblem! with
            {
                Answers = [new ElementaryAnswer("Kết quả", new(angle, 1), "°", angle.ToString())]
            } });

        Console.WriteLine($"  Checked {count} protractor answers across both languages and five stars, including 0, 90 and 180 degrees; wrong values and temperature units rejected.");

        void Check(ArithmeticQuizQuestion question)
        {
            var value = question.ElementaryProblem!.Answers[0].Value;
            foreach (string unit in new[] { "độ", "ĐỘ", "do", "DO", "degree", "degrees", "°" })
            foreach (string prefix in new[] { "", "Đáp số: ", "Answer: " })
            {
                string input = prefix + value + " " + unit;
                Require(grader.ValidateSubmission(question, input).IsCorrect, "Keyboard angle answer rejected: " + input);
                count++;
            }
            foreach (string input in new[] { "", value.ToString(), (value.Numerator + 1) + " độ",
                value + " độ C", value + " °C", value + " degrees Celsius", value + " độ F",
                value + " rad", value + " cm", value + " kg °", value + " không phải độ",
                value + " độ cm", "Answer: " + value + " độ\nAnswer: 0 cm" })
                Require(!grader.ValidateSubmission(question, input).IsCorrect, "Wrong angle or unit accepted: " + input);
            Require(grader.ValidateSubmission(question, value + "độ").IsCorrect
                && grader.ValidateSubmission(question, value + "°").IsCorrect,
                "Angle units without a space should remain valid.");
        }
    }
}
