using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMixedNumberInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 20; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.FractionSkills, ElementaryQuizType.MixedNumber, language, tier);
            var contract = question.ElementaryProblem!;
            Require(EssayAnswerValidator.IsResultOnlyPractice(question), "Mixed-number practice still requires worked calculations.");
            var value = contract.Answers[0].Value;
            var whole = BigInteger.DivRem(value.Numerator, value.Denominator, out var numerator);
            string answer = $"{whole} {numerator}/{value.Denominator}";
            foreach (string input in new[] { answer, $"{whole}   {numerator} / {value.Denominator}",
                $"{whole} và {numerator}/{value.Denominator}", $"{whole} VA {numerator} / {value.Denominator}",
                $"{whole} and {numerator}/{value.Denominator}", $"{whole}\u00a0{numerator}/{value.Denominator}",
                $"{whole}\u2009{numerator}/{value.Denominator}",
                "Đáp số: " + answer, "Answer: " + answer, "Kết quả = " + answer,
                $"Đáp số: {whole} và {numerator}/{value.Denominator}",
                $"Kết quả = {whole} và {numerator}/{value.Denominator}",
                contract.Answers[0].Expression + $" = {whole} và {numerator}/{value.Denominator}",
                contract.Answers[0].Expression + " = " + answer, contract.SolutionText })
            {
                Require(grader.ValidateSubmission(question, input).IsCorrect, "Keyboard mixed-number answer rejected: " + input);
                count++;
            }
            foreach (string input in new[] { "", value.ToString(), $"{whole} x {numerator}/{value.Denominator}",
                $"{whole} * {numerator}/{value.Denominator}", $"{whole} + {numerator}/{value.Denominator}",
                $"{whole + 1} {numerator}/{value.Denominator}", $"{whole} {2 * numerator}/{2 * value.Denominator}",
                $"{whole - 1} {numerator + value.Denominator}/{value.Denominator}", answer + " kg",
                $"{whole} và {2 * numerator}/{2 * value.Denominator}",
                $"{whole} và {numerator}/{value.Denominator} kg", $"{whole} và {numerator}/0",
                $"{whole} và {numerator}/{value.Denominator} và 1/3",
                "Answer: " + answer + "\nAnswer: 0" })
                Require(!grader.ValidateSubmission(question, input).IsCorrect, "Wrong or non-mixed answer accepted: " + input);

            // The independent validator still verifies worked examples and the source relation.
            var parts = EssayCombinedInputParser.Parse(contract.SolutionText, false, true);
            Require(grader.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                "Mixed-number examples lost calculation validation.");
        }

        var example = new ElementaryQuizGenerator(new Random(17)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.FractionSkills, ElementaryQuizType.MixedNumber, AppLanguage.Vietnamese, CurriculumTier.ThreeStars);
        example = example with { ElementaryProblem = example.ElementaryProblem! with
        {
            Answers = [new ElementaryAnswer("Hỗn số", new(12, 5), "", "11/5+1/5", Text: "2 2/5", RequireMixedNumber: true)]
        } };
        Require(grader.ValidateSubmission(example, "2 2/5").IsCorrect
            && grader.ValidateSubmission(example, "11/5 + 1/5 = 2 2/5").IsCorrect
            && !grader.ValidateSubmission(example, "2 x 2/5").IsCorrect,
            "The reported 2 2/5 example is not correctly distinguished from multiplication.");
        example = example with { ElementaryProblem = example.ElementaryProblem! with
        {
            Answers = [new ElementaryAnswer("Hỗn số", new(11, 2), "", "11/2", Text: "5 1/2", RequireMixedNumber: true)]
        } };
        foreach (string input in new[] { "5 1/2", "5 và 1/2", "5\u00a01/2", "5\u20091/2",
            "Đáp số: 5 và 1/2", "11/2 = 5 và 1/2", "5 and 1/2" })
            Require(grader.ValidateSubmission(example, input).IsCorrect,
                "The reported 11/2 example rejected keyboard input: " + input);
        Console.WriteLine($"  Checked {count} keyboard mixed-number submissions, both languages and five stars; reduced form, wrong values and multiplication remain distinct.");
    }
}
