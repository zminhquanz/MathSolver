using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckDecimalLitreUnits()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in ElementaryQuizGenerator.DecimalStoryTypes)
        foreach (bool expanded in new[] { false, true })
        foreach (var context in ElementaryQuizGenerator.DecimalContexts(language, expanded)
            .Where(context => context.Unit.Equals("l", StringComparison.OrdinalIgnoreCase)))
        {
            var question = new ElementaryQuizGenerator(new Random(47), expandNarratives: expanded)
                .GenerateDecimalStory(ArithmeticQuizMode.Essay, type, language, tier, context.Id);
            var contract = question.ElementaryProblem!;
            var worked = EssayCombinedInputParser.Parse(contract.SolutionText, true, true);
            foreach (string unit in new[] { "l", "L", "lít", "LÍT", "lit", "litre", "litres", "liter", "liters" })
            {
                string ReplaceUnit(string text) => Regex.Replace(text, @"(?<!\p{L})l(?!\p{L})", unit,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                foreach (bool commaDecimals in new[] { false, true })
                {
                    string equations = ReplaceUnit(worked.Equation);
                    string answer = ReplaceUnit(contract.AnswerText);
                    if (commaDecimals) { equations = equations.Replace('.', ','); answer = answer.Replace('.', ','); }
                    // Use only the written unit as a prose cue; the canonical answer
                    // label or the noun from the generated question must not hide a failure.
                    string lead = "Tính được " + unit + ":";
                    var result = grader.ValidateSubmission(question, lead + "\n" + equations + "\nĐáp số: " + answer);
                    Require(result.IsCorrect,
                        $"Decimal litre wording rejected: {language}/{tier}/{type}/{context.Id}/{unit}; {string.Join("; ", result.Details)}");
                    count++;
                }
            }
            foreach (string wrongUnit in new[] { "ml", "kg", "m³" })
            {
                string answer = Regex.Replace(contract.AnswerText, @"(?<!\p{L})l(?!\p{L})", wrongUnit,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                Require(!ElementaryEssayValidator.CheckAnswers(question, answer), "Litre aliases accepted a different unit: " + wrongUnit);
            }
            Require(!grader.Validate(question, "Tính được ml:", worked.Equation, worked.Answer).SolutionIsCorrect,
                "Millilitres incorrectly supplied a litre solution cue.");
            Require(!grader.Validate(question, "Bài giải:", worked.Equation, worked.Answer).SolutionIsCorrect,
                "Decimal word problems stopped requiring a meaningful solution sentence.");
            Require(!grader.Validate(question, "Tính được lúa:", worked.Equation, worked.Answer).SolutionIsCorrect,
                "A fragment of a Vietnamese word was mistaken for the litre symbol.");
            Require(!grader.Validate(question, "Tính được lít:", worked.Equation, "0 lít").AnswerIsCorrect,
                "A litre alias concealed a wrong numeric answer.");
        }
        Require(count > 0, "No decimal litre contexts were checked.");
        Console.WriteLine($"  Checked {count} decimal litre submissions across both languages, five stars, all four operations and legacy/expanded contexts; wrong units and quantities rejected.");
    }
}
