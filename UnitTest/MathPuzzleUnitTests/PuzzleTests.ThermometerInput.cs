using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckThermometerInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 20; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Measurement, ElementaryQuizType.ReadThermometer, language, tier);
            var temperature = question.ElementaryProblem!.Answers[0].Value;
            foreach (string unit in new[] { "độ C", "độ c", "ĐỘ C", "độ   C", "độC", "do C", "do c",
                "degrees C", "degree C", "degrees Celsius", "Celsius", "°C", "° C", "℃" })
            foreach (string prefix in new[] { "", "Đáp số: ", "Answer: " })
            {
                string input = prefix + temperature + " " + unit;
                Require(grader.ValidateSubmission(question, input).IsCorrect, "Temperature answer rejected: " + input);
                count++;
            }
            foreach (string input in new[] { "", temperature.ToString(), temperature + " độ F",
                temperature + " °F", temperature + " K", temperature + " độ", temperature + " C",
                temperature + " kg", temperature + " kg °C", temperature + " độ C kg",
                temperature + " không phải độ C", (temperature.Numerator + 1) + " độ C",
                "Answer: " + temperature + " độ C\nAnswer: 0 K" })
                Require(!grader.ValidateSubmission(question, input).IsCorrect, "Missing/wrong temperature or unit accepted: " + input);
        }

        var sample = new ElementaryQuizGenerator(new Random(17)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.Measurement, ElementaryQuizType.ReadThermometer, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        sample = sample with { ElementaryProblem = sample.ElementaryProblem! with
        {
            Answers = [new ElementaryAnswer("Kết quả", new(34, 1), "°C", "34")]
        } };
        Require(grader.ValidateSubmission(sample, "34 độ C").IsCorrect, "The reported thermometer answer was rejected.");
        var mass = sample with { ElementaryProblem = sample.ElementaryProblem with
        {
            Answers = [new ElementaryAnswer("Kết quả", new(34, 1), "kg", "34")]
        } };
        Require(!grader.ValidateSubmission(mass, "34 độ C").IsCorrect, "Celsius aliases were incorrectly treated as another measurement unit.");
        Console.WriteLine($"  Checked {count} thermometer answers across both languages and all five stars; keyboard Celsius aliases, symbols, wrong values and units.");
    }
}
