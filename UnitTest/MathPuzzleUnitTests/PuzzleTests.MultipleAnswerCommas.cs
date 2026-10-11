using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMultipleAnswerCommas()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        var covered = new HashSet<ElementaryQuizType>();
        int cases = 0;
        foreach (var kind in Enum.GetValues<QuizProblemKind>())
        foreach (var type in ElementaryQuizGenerator.Types(kind))
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 4; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                kind, type, language, tier);
            var contract = question.ElementaryProblem!;
            if (contract.Answers.Count < 2) continue;
            covered.Add(type);
            var work = EssayCombinedInputParser.Parse(contract.SolutionText, contract.RequiresSolution, true,
                requireAnswerLabel: type == ElementaryQuizType.ReadClock);
            string prefix = language == AppLanguage.Vietnamese ? "Đáp số: " : "Answer: ";
            string calculations = work.Solution + "\n" + work.Equation + "\n";
            foreach (string separator in new[] { "; ", ", ", ",", "\n" })
            {
                string labeled = contract.AnswerText.Replace("; ", separator, StringComparison.Ordinal);
                Require(validator.ValidateSubmission(question, calculations + prefix + labeled.Replace("\n", "\n" + prefix, StringComparison.Ordinal)).IsCorrect,
                    $"Rejected labeled pair {type}/{language}/{tier}/{separator}: {labeled}");
                string bare = string.Join(separator, contract.Answers.Select(ElementaryQuizContract.FormatAnswer));
                Require(validator.ValidateSubmission(question, calculations + prefix + bare.Replace("\n", "\n" + prefix, StringComparison.Ordinal)).IsCorrect,
                    $"Rejected unlabeled pair {type}/{language}/{tier}/{separator}: {bare}");
                if (type != ElementaryQuizType.ReadClock && separator != "\n")
                    Require(validator.ValidateSubmission(question, calculations + bare).IsCorrect,
                        $"Rejected pair without an Answer prefix {type}/{tier}: {bare}");
                cases++;
            }
            string commaPair = contract.AnswerText.Replace("; ", ", ", StringComparison.Ordinal);
            Require(!validator.ValidateSubmission(question, calculations + prefix + commaPair + ", Invalid: 999999").IsCorrect,
                "An extra answer was discarded: " + type);
            Require(!validator.ValidateSubmission(question, calculations + prefix +
                contract.Answers[0].Label + ": " + ElementaryQuizContract.FormatAnswer(contract.Answers[0])).IsCorrect,
                "A missing answer passed: " + type);
            string duplicate = string.Join(", ", Enumerable.Repeat(contract.Answers[0].Label + ": " +
                ElementaryQuizContract.FormatAnswer(contract.Answers[0]), contract.Answers.Count));
            Require(!validator.ValidateSubmission(question, calculations + prefix + duplicate).IsCorrect,
                "Duplicate labels replaced a missing answer: " + type);
        }
        foreach (var type in new[] { ElementaryQuizType.AdjacentNumbers, ElementaryQuizType.FractionTerms,
            ElementaryQuizType.SumDifference, ElementaryQuizType.SumRatio, ElementaryQuizType.DifferenceRatio,
            ElementaryQuizType.QuotientRemainder, ElementaryQuizType.CommonDenominator,
            ElementaryQuizType.ReadClock, ElementaryQuizType.SortData })
            Require(covered.Contains(type), "Missing multi-answer type in audit: " + type);

        var adjacent = new ElementaryQuizGenerator(new Random(1)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.NumberSense, ElementaryQuizType.AdjacentNumbers, AppLanguage.Vietnamese, CurriculumTier.ThreeStars);
        var original = adjacent.ElementaryProblem!;
        adjacent = adjacent with { ElementaryProblem = original with { Answers = [
            original.Answers[0] with { Value = new(28217, 1), Expression = "28217" },
            original.Answers[1] with { Value = new(28219, 1), Expression = "28219" }] } };
        foreach (string input in new[] { "28217,28219", "28217, 28219", "28217;28219",
            "Đáp số: 28217, 28219", "Số liền trước: 28217, Số liền sau: 28219",
            "Đáp số: Số liền sau: 28219, Số liền trước: 28217", "28,217;28,219", "28,217, 28,219" })
            Require(validator.ValidateSubmission(adjacent, input).IsCorrect, "Screenshot regression: " + input);
        foreach (string input in new[] { "28219,28217", "28217,28218", "28217,28219,28220", "28217,,28219" })
            Require(!validator.ValidateSubmission(adjacent, input).IsCorrect, "Wrong adjacency accepted: " + input);

        var decimals = adjacent with { ElementaryProblem = original with {
            Kind = QuizProblemKind.Measurement, Type = ElementaryQuizType.LengthConversion,
            RequiresCalculation = false, Answers = [
                new("Thứ nhất", new(3, 2), "m", "1.5"), new("Thứ hai", new(5, 2), "m", "2.5")] } };
        foreach (string input in new[] { "Đáp số: 1,5 m; 2,5 m", "Đáp số: 1,5 m, 2,5 m",
            "Đáp số: Thứ nhất: 1,5 m,Thứ hai: 2,5 m", "Đáp số: 1,5 m,2,5 m" })
            Require(validator.ValidateSubmission(decimals, input).IsCorrect, "Decimal comma was split: " + input);
        var unitless = decimals with { ElementaryProblem = decimals.ElementaryProblem! with {
            Answers = decimals.ElementaryProblem!.Answers.Select(answer => answer with { Unit = "" }).ToArray() } };
        foreach (string input in new[] { "1,5;2,5", "1,5, 2,5", "1.5, 2.5" })
            Require(validator.ValidateSubmission(unitless, input).IsCorrect, "Decimal list notation rejected: " + input);
        Require(!validator.ValidateSubmission(unitless, "1,5,2,5").IsCorrect,
            "An ambiguous decimal list must not be guessed from the correct answer.");

        var singleDecimal = unitless with { ElementaryProblem = unitless.ElementaryProblem! with {
            Kind = QuizProblemKind.Decimal, Type = ElementaryQuizType.DecimalRound,
            Answers = [new("Kết quả", new(3, 2), "", "1.5")] } };
        Require(validator.ValidateSubmission(singleDecimal, "1,5").IsCorrect, "A single decimal was split into answers.");
        Require(!validator.ValidateSubmission(singleDecimal, "1,5;2,5").IsCorrect, "A conflicting single answer was hidden.");
        Console.WriteLine($"  Audited {cases} multi-answer submissions in both languages and all stars: {string.Join(", ", covered.Order())}.");
    }
}
