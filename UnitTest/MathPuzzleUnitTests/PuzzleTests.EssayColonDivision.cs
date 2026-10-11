using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckEssayColonDivision()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        var findX = new ArithmeticQuizQuestion(
            new IntegerArithmeticExpression(8, ArithmeticOperation.Divide, 2),
            ArithmeticQuizMode.Essay, 4, null, null, [])
        {
            FindXProblem = new(8, 2, ArithmeticOperation.Divide, false, 4,
                new(8, ArithmeticOperation.Divide, 2))
        };
        foreach (string symbol in new[] { ":", "÷", "/" })
        foreach (bool preserve in new[] { false, true })
        foreach (string lead in new[] { "", "Ta có: ", "Ta có:\n" })
        {
            string equation = $"x = 8 {symbol} 2 = 4";
            var parts = EssayCombinedInputParser.Parse($"{lead}{equation}\nĐáp số : 4", false, preserve);
            Require(parts.Equation == equation &&
                    validator.Validate(findX, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                $"Named division was truncated: {lead}{equation}; parsed={parts.Equation}");
            var wrong = EssayCombinedInputParser.Parse($"{lead}x = 8 {symbol} 2 = 5\nĐáp số: 4", false, preserve);
            Require(!validator.Validate(findX, wrong.Solution, wrong.Equation, wrong.Answer).EquationIsCorrect,
                "A correct final answer concealed incorrect arithmetic.");
            count++;
        }

        foreach (string equation in new[] { "x=8:2=4", "x = (8 : 2) = 4", "x = 24 : 3 : 2 = 4",
                     "8 x 2 : (2 + 2) = 4", "2 : (1/2) = 4" })
        foreach (bool preserve in new[] { false, true })
        {
            var parts = EssayCombinedInputParser.Parse(equation + "\nAnswer: 4", false, preserve);
            Require(parts.Equation == equation && validator.Validate(findX, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                $"Division with brackets, fractions or multiple operations was truncated: {equation}");
            count++;
        }

        // Exercise the same combined editor path as the page, including optional
        // headings that make an otherwise numeric submission contain letters.
        foreach ((QuizProblemKind kind, object subtype) in AllSubtypes())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            var question = Generate(kind, subtype, ArithmeticQuizMode.Essay, language, 27000 + count);
            string unit = EssayAnswerValidator.GetExpectedUnit(question);
            string suffix = unit.Length > 0 ? " " + unit : "";
            string solution = EssayAnswerValidator.RequiresSolution(question)
                ? BuildSolutionSentence(question, language) : "Ta có:";
            string work = question.AverageProblem?.Type == AverageQuizType.IndirectData
                ? question.AverageProblem.SolutionText
                : $"{solution}\n{BuildEquation(question)}{suffix}\nAnswer: {question.ExactAnswer}{suffix}";
            foreach (string symbol in new[] { ":", "÷", "/" })
            {
                string submission = work.Replace("÷", symbol, StringComparison.Ordinal);
                var parts = EssayCombinedInputParser.Parse(submission, EssayAnswerValidator.RequiresSolution(question),
                    question.GeometryProblem?.Reasoning is not null || question.AverageProblem?.Type == AverageQuizType.IndirectData);
                var result = validator.Validate(question, parts.Solution, parts.Equation, parts.Answer);
                Require(result.IsCorrect,
                    $"{kind}/{subtype}/{language}/{symbol}: {result}; {submission}");
                count++;
            }
        }

        foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
        foreach (var type in ElementaryQuizGenerator.Types(kind))
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
        {
            var question = new ElementaryQuizGenerator(new Random(31000 + count)).Generate(
                ArithmeticQuizMode.Essay, kind, type, language, tier);
            var contract = question.ElementaryProblem!;
            foreach (string symbol in new[] { ":", "÷", "/" })
            {
                var parts = EssayCombinedInputParser.Parse(contract.SolutionText.Replace("÷", symbol, StringComparison.Ordinal),
                    contract.RequiresSolution, true, requireAnswerLabel: type == ElementaryQuizType.ReadClock);
                var result = validator.Validate(question, parts.Solution, parts.Equation, parts.Answer);
                Require(result.IsCorrect,
                    $"{kind}/{type}/{language}/{tier}/{symbol}: {string.Join(" | ", result.Details)}; {parts.Equation}");
                count++;
            }
        }

        var clock = new ElementaryQuizGenerator(new Random(72)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.Time, ElementaryQuizType.ReadClock, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        var answers = clock.ElementaryProblem!.Answers;
        foreach (string value in new[] { $"{answers[0].Value}:{answers[1].Value}",
                     $"Giờ: {answers[0].Value}; Phút: {answers[1].Value}" })
        {
            var parts = EssayCombinedInputParser.Parse("Đáp số: " + value, false, true, requireAnswerLabel: true);
            Require(validator.Validate(clock, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                "Clock/answer label colons were mistaken for division.");
        }
        Console.WriteLine($"  Checked {count} submissions with division symbols across essay families and five elementary tiers.");
    }
}
