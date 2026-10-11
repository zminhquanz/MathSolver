using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMixedMeasurementInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 20; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Measurement, ElementaryQuizType.MixedLength, language, tier);
            var contract = question.ElementaryProblem!;
            Require(EssayAnswerValidator.IsResultOnlyPractice(question), "Mixed measurements still require a worked solution.");
            string answer = contract.AnswerText;
            foreach (string input in new[] { answer, answer.Replace('.', ','), "Đáp số: " + answer,
                "Answer: " + answer, contract.Answers[0].Expression + " = " + answer,
                "Kết quả = " + answer, contract.SolutionText })
            {
                Require(grader.ValidateSubmission(question, input).IsCorrect, "Mixed measurement result rejected: " + input);
                count++;
            }
            foreach (string input in new[] { "", contract.Answers[0].Expression, "x =",
                "0 " + contract.Answers[0].Unit, contract.Answers[0].DisplayValue!,
                contract.Answers[0].DisplayValue + " kg", "Answer: " + answer + "\nAnswer: 0 kg",
                contract.Answers[0].Expression + " = " + answer + "\nAnswer: 0 " + contract.Answers[0].Unit })
                Require(!grader.ValidateSubmission(question, input).IsCorrect, "Missing, incorrect or conflicting measurement result accepted: " + input);

            var parts = EssayCombinedInputParser.Parse(contract.SolutionText, false, true);
            Require(grader.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                "Worked mixed-measurement examples lost independent calculation validation.");
            Require(!grader.Validate(question, null, "1 = " + answer, answer).IsCorrect,
                "Independent validation stopped checking the conversion relation.");

            // Unit conversions use the same optional-work policy.
            var story = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Measurement, ElementaryQuizType.LengthConversion, language, tier);
            Require(EssayAnswerValidator.IsResultOnlyPractice(story)
                && grader.ValidateSubmission(story, story.ElementaryProblem!.AnswerText).IsCorrect,
                "A unit conversion still requires a worked solution.");
        }

        var example = new ElementaryQuizGenerator(new Random(17)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.Measurement, ElementaryQuizType.MixedLength, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        example = example with { ElementaryProblem = new(QuizProblemKind.Measurement, ElementaryQuizType.MixedLength,
            AppLanguage.Vietnamese, "8 m và 9 dm bằng bao nhiêu dm?", "", ["8", "9"], ["0", "1", "10"],
            [new ElementaryAnswer("Số đo sau khi đổi", new(89, 1), "dm", "8*10+9")], false) };
        foreach (string input in new[] { "89 dm", "Đáp số: 89 dm", "8 × 10 + 9 = 89 dm",
            "Số đo sau khi đổi là:\n8 × 10 = 80 dm\n80 + 9 = 89 dm\nĐáp số: 89 dm" })
            Require(grader.ValidateSubmission(example, input).IsCorrect, "The reported 8 m 9 dm example was rejected: " + input);
        Require(!grader.ValidateSubmission(example, "89 m").IsCorrect, "The requested target unit was ignored.");
        Console.WriteLine($"  Checked {count} direct and worked mixed-measurement submissions across both languages and five stars; requested units retained.");
    }
}
