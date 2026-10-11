using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckNaturalNumberOrderSeparators()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int submissions = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 40; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.NumberSense, ElementaryQuizType.OrderNumbers, language, tier);
            var contract = question.ElementaryProblem!;
            Require(!contract.ProblemText.Contains('→') && contract.ProblemText.Contains("->"),
                "Ordering instructions must use a keyboard-friendly separator.");
            string[] numbers = contract.Answers[0].Text!.Split("->", StringSplitOptions.TrimEntries);
            foreach (string separator in new[] { "->", " -> ", "-", " - ", "\t->\t" })
            {
                string answer = string.Join(separator, numbers);
                foreach (string input in new[] { answer, "  " + answer + ".  ",
                    (language == AppLanguage.Vietnamese ? "Đáp số: " : "Answer: ") + answer })
                {
                    Require(validator.ValidateSubmission(question, input).IsCorrect,
                        $"Rejected correct order {tier}/{language}: {input}");
                    submissions++;
                }
            }
            foreach (string alias in contract.Answers[0].Aliases!)
                Require(validator.ValidateSubmission(question, alias).IsCorrect, "Existing ordering alias was lost.");
            foreach (string answer in new[] {
                string.Join("->", numbers.Reverse()), string.Join("-", numbers.Skip(1)),
                string.Join("-", numbers) + "-" + numbers[^1],
                string.Join("-", numbers.Take(numbers.Length - 1).Append(numbers[0])),
                "-" + string.Join("-", numbers), string.Join("->", numbers) + "->",
                string.Join("--", numbers), string.Join("+", numbers),
                string.Join("→", numbers), string.Join(" → ", numbers),
                string.Join("->", numbers) + " = 0", string.Join("->", numbers) + " invalid",
                string.Join("->", numbers) + "\n" + string.Join("->", numbers.Reverse()) })
                Require(!validator.ValidateSubmission(question, answer).IsCorrect,
                    "Accepted a wrong or malformed ordered list: " + answer);
            Require(contract.ChoiceTexts!.Count(answer => ElementaryEssayValidator.CheckAnswers(question, answer)) == 1,
                "Ordering choices became ambiguous.");
            Require(ElementaryEssayValidator.CheckAnswers(question, contract.PresentedText) == question.PresentedEquationIsCorrect,
                "True/false ordering grading changed.");
        }

        // Reproduce both screenshots through the same combined-input API as the UI.
        var fixture = new ElementaryQuizGenerator(new Random(1)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.NumberSense, ElementaryQuizType.OrderNumbers, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        foreach (var (expected, input) in new[] { ("2 -> 13 -> 16", "2-13-16"), ("16 -> 11 -> 8", "16->11->8"),
            ("0 -> 2 -> 13", "0-2->13") })
        {
            var question = fixture with { ElementaryProblem = fixture.ElementaryProblem! with {
                Answers = [fixture.ElementaryProblem!.Answers[0] with { Text = expected, Aliases = null }] } };
            Require(validator.ValidateSubmission(question, input).IsCorrect, "Screenshot regression: " + input);
        }

        // A hyphen is not a universal replacement for a textual answer or fraction.
        foreach (var (kind, type) in new[] { (QuizProblemKind.NumberSense, ElementaryQuizType.ReadNumber),
            (QuizProblemKind.FractionSkills, ElementaryQuizType.OrderFractions) })
        {
            var question = new ElementaryQuizGenerator(new Random(1)).Generate(ArithmeticQuizMode.Essay,
                kind, type, AppLanguage.Vietnamese, CurriculumTier.OneStar);
            Require(!validator.ValidateSubmission(question, "2-13-16").IsCorrect,
                "Natural-number separator handling leaked to another question type.");
        }
        Console.WriteLine($"  Checked {submissions} ordering submissions across both languages and all five difficulty levels.");
    }
}
