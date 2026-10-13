using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMeasurementPractice()
    {
        // Independent metric scales, rather than reusing the conversion engine under test.
        var scales = new Dictionary<ElementaryQuizType, Dictionary<string, decimal>>
        {
            [ElementaryQuizType.LengthConversion] = new() { ["km"] = 1000, ["hm"] = 100, ["dam"] = 10, ["m"] = 1, ["dm"] = .1m, ["cm"] = .01m, ["mm"] = .001m },
            [ElementaryQuizType.MassConversion] = new() { ["t"] = 1000, ["q"] = 100, ["yến"] = 10, ["kg"] = 1, ["g"] = .001m, ["mg"] = .000001m },
            [ElementaryQuizType.CapacityConversion] = new() { ["L"] = 1, ["mL"] = .001m },
            [ElementaryQuizType.AreaConversion] = new() { ["km²"] = 1000000, ["ha"] = 10000, ["m²"] = 1, ["dm²"] = .01m, ["cm²"] = .0001m, ["mm²"] = .000001m },
            [ElementaryQuizType.VolumeConversion] = new() { ["m³"] = 1, ["dm³"] = .001m, ["cm³"] = .000001m, ["mm³"] = .000000001m }
        };
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        var seen = new HashSet<string>();
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        foreach (var (type, units) in scales)
        for (int seed = 0; seed < (mode == ArithmeticQuizMode.Essay ? 40 : 4); seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed))
                .GenerateMeasurementPractice(mode, type, language, tier);
            var c = question.ElementaryProblem!;
            var source = c.Reasoning!.Givens.Single();
            var answer = c.Answers.Single();
            decimal amount = decimal.Parse(source.Value, CultureInfo.InvariantCulture);
            decimal expected = amount * units[source.Unit] / units[answer.Unit];
            Require((decimal)answer.Value.Numerator / (decimal)answer.Value.Denominator == expected,
                "Wrong direct conversion factor: " + c.ProblemText);
            Require(decimal.Parse(answer.DisplayValue!, CultureInfo.InvariantCulture) == expected,
                "Displayed conversion was rounded or truncated.");
            Require(c.IsNumericMeasurementConversion && c.StoryContextId is null && c.AreaMeasurement is null
                && question.WordProblem is null && c.Visual is null && !c.RequiresSolution,
                "Numeric practice acquired a narrative or visual dependency.");
            Require(c.ProblemText == c.FormatMeasurementConversion("? " + answer.Unit), "Malformed conversion prompt.");
            Require(c.ChoiceTexts!.Count == 4 && c.ChoiceTexts.Distinct().Count() == 4
                && c.ChoiceTexts.Contains(c.AnswerText), "Invalid conversion choices.");
            Require((c.PresentedText == c.AnswerText) == question.PresentedEquationIsCorrect,
                "True/false conversion assertion was graded incorrectly.");
            if (tier == CurriculumTier.OneStar)
                Require(amount == decimal.Truncate(amount) && amount < 10 && expected > amount,
                    "One-star conversions must use small whole numbers and larger-to-smaller units.");
            if (tier == CurriculumTier.TwoStars)
                Require(expected == decimal.Truncate(expected), "Two-star whole-number result became fractional.");
            if (mode == ArithmeticQuizMode.Essay)
            {
                Require(grader.ValidateSubmission(question, c.AnswerText).IsCorrect,
                    "Correct numeric conversion submission rejected: " + c.AnswerText);
                Require(grader.ValidateSubmission(question, c.AnswerText.Replace('.', ',')).IsCorrect,
                    "Decimal-comma conversion submission rejected.");
                // The conversion prompt supplies the target unit; typing it again is optional.
                Require(grader.ValidateSubmission(question, answer.DisplayValue).IsCorrect, "Numeric answer with the supplied target unit rejected.");
                Require(!grader.ValidateSubmission(question, answer.DisplayValue + " " + source.Unit).IsCorrect,
                    "Wrong target unit accepted.");
                Require(!grader.ValidateSubmission(question, (expected + 1).ToString(CultureInfo.InvariantCulture) + " " + answer.Unit).IsCorrect,
                    "Wrong converted value accepted.");
            }
            seen.Add(type + ":" + source.Unit + ":" + answer.Unit);
            count++;
        }
        Require(seen.Contains("LengthConversion:m:mm") && seen.Contains("LengthConversion:km:m"),
            "Requested metre/millimetre and kilometre/metre examples were never generated.");
        foreach (var type in scales.Keys)
        {
            var words = new ElementaryQuizGenerator(new Random(123)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Measurement, type, AppLanguage.Vietnamese, CurriculumTier.FiveStars);
            Require(!words.ElementaryProblem!.IsNumericMeasurementConversion, "Word-problem branch became numeric.");
        }
        Console.WriteLine($"  Checked {count} numeric conversions, both languages, five stars and three answer modes; independent factors, unit checking and presentation.");
    }
}
