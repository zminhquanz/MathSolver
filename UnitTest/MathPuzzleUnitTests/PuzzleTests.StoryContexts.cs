using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckStoryContexts()
    {
        Require(QuizStoryContextCatalog.All.Select(c => c.Id).Distinct().Count() == 19, "Shared context identities are not unique.");
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        var seen = new HashSet<string>();
        int checkedCount = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 80; seed++)
        {
            var average = new AverageQuizGenerator(new Random(seed)).GenerateAlgorithm(
                ArithmeticQuizMode.Essay, AverageQuizType.Direct, language, new(tier, false)).AverageProblem!;
            var context = QuizStoryContextCatalog.Find(average.StoryContextId!);
            Require(average.Facts.Skip(1).All(n => n > 0 && n <= context.MaximumPerPeriod), "An average datum exceeds the context capacity.");
            Require(average.Facts.Skip(1).Sum() == (int)average.CorrectAnswer * average.Facts[0], "Contextual average is inconsistent.");
            Require(average.AnswerUnit == context.Unit(language) && !average.ProblemText.Contains("matchs"), "An average lost its unit or period.");
            seen.Add(context.Id);
            foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.Remainder))
            {
                var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay, QuizProblemKind.Remainder, type, language, tier);
                var c = question.ElementaryProblem!;
                var facts = c.Reasoning!.Givens.ToDictionary(g => g.Role, g => int.Parse(g.Value, CultureInfo.InvariantCulture));
                int F(string role) => facts.GetValueOrDefault(role);
                int amount = (int)tier <= 2 ? F("total") : F("first-batch") + F("second-batch") - F("already-accommodated");
                int size = (int)tier == 5 ? F("nominal-capacity") - F("reserved-capacity") : F("capacity");
                int quotient = Math.DivRem(amount, size, out int remainder);
                Require(size >= 2 && amount > 0 && remainder >= 0 && remainder < size, "Invalid packing facts.");
                if (type == ElementaryQuizType.MinimumGroups)
                    Require(c.Answers[0].Value.Numerator == quotient + (remainder == 0 ? 0 : 1), "Minimum containers must round up only when needed.");
                else Require(c.Answers[0].Value.Numerator == quotient && c.Answers[1].Value.Numerator == remainder, "Full containers must not round up.");
                CheckExample(question);
                var sample = EssayCombinedInputParser.Parse(c.SolutionText, c.RequiresSolution, true);
                string wrongUnits = string.Join("; ", c.Answers.Select(a => a.Label + ": " + a.Value + " kg"));
                Require(!grader.Validate(question, sample.Solution, sample.Equation, wrongUnits).IsCorrect, "Packing answers accepted a mass unit.");
                checkedCount++;
            }
            foreach (var type in new[] { ElementaryQuizType.DecimalAdd, ElementaryQuizType.DecimalSubtract, ElementaryQuizType.DecimalMultiply, ElementaryQuizType.DecimalDivide })
            {
                var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay, QuizProblemKind.Decimal, type, language, tier);
                var c = question.ElementaryProblem!;
                if (c.StoryContextId is null) {
                    Require(c.Answers[0].Unit.Length == 0, "Numeric decimal practice must remain available.");
                    continue;
                }
                decimal a = decimal.Parse(c.Facts[0], CultureInfo.InvariantCulture), b = decimal.Parse(c.Facts[1], CultureInfo.InvariantCulture);
                decimal expected = type switch { ElementaryQuizType.DecimalAdd => a + b, ElementaryQuizType.DecimalSubtract => a - b,
                    ElementaryQuizType.DecimalMultiply => a * b, _ => a / b };
                if ((int)tier >= 3)
                {
                    var g = c.Reasoning!.Givens.ToDictionary(given => given.Role, given => decimal.Parse(given.Value, CultureInfo.InvariantCulture));
                    decimal first = g["quantity"];
                    if (tier == CurriculumTier.FiveStars) first /= c.Answers[0].Unit == "m" ? 100 : 1000;
                    expected = type switch
                    {
                        ElementaryQuizType.DecimalAdd => first * 2 + g["difference"],
                        ElementaryQuizType.DecimalSubtract => first - g["used-first"] - g["used-second"],
                        ElementaryQuizType.DecimalMultiply => (first + g["extra-quantity"]) * g["portion-count"],
                        _ => (first + g["extra-quantity"]) / g["portion-count"]
                    };
                    expected += g.GetValueOrDefault("adjustment");
                }
                decimal value = (decimal)c.Answers[0].Value.Numerator / (decimal)c.Answers[0].Value.Denominator;
                Require(value == expected && value <= ElementaryQuizGenerator.DecimalContexts(language)
                    .Single(context => context.Id == c.StoryContextId).MaximumQuantity, "Decimal context scale or arithmetic is invalid.");
                CheckExample(question);
                checkedCount++;
            }
        }
        Require(QuizStoryContextCatalog.AverageContexts.All(c => seen.Contains(c.Id)), "Context rotation omitted an average setting.");
        Console.WriteLine($"  Checked {checkedCount} contextual decimal/packing contracts plus 800 bounded averages, both languages and all five stars.");
        void CheckExample(ArithmeticQuizQuestion question)
        {
            var c = question.ElementaryProblem!;
            var parts = EssayCombinedInputParser.Parse(c.SolutionText, c.RequiresSolution, true);
            var result = grader.Validate(question, parts.Solution, parts.Equation, parts.Answer);
            Require(result.IsCorrect, $"Contextual solution rejected: {c.Type}/{c.Language}/{c.Reasoning?.Tier}: {string.Join(" | ", result.Details)}\n{c.SolutionText}");
        }
    }
}
