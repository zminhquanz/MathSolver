using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckTimeFractionExtensions()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int cases = 0;
        foreach (var type in new[] { ElementaryQuizType.SecondsConversion, ElementaryQuizType.YearCentury,
            ElementaryQuizType.CalendarWeekday, ElementaryQuizType.OrderRelatedFractions })
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 100; seed++)
        {
            var q = new ElementaryQuizGenerator(new Random(seed)).GenerateTimeFractionExtension(mode, type, language, tier);
            var c = q.ElementaryProblem!;
            foreach (var input in new[] { c.AnswerText, "Answer: " + c.AnswerText, c.SolutionText })
                Require(grader.ValidateSubmission(q, input).IsCorrect, $"Rejected {type}/{language}/{tier}: {input}");
            Require(c.ChoiceTexts!.Distinct().Count() == 4 && c.ChoiceTexts!.Count(choice => ElementaryEssayValidator.CheckAnswers(q, choice)) == 1,
                "Ambiguous choices: " + type);
            Require(ElementaryEssayValidator.CheckAnswers(q, c.PresentedText) == q.PresentedEquationIsCorrect, "Incorrect true/false proposal.");
            Require(!grader.ValidateSubmission(q, "").IsCorrect && !grader.ValidateSubmission(q, "999999 unknown").IsCorrect, "Invalid extension result passed.");
            int Given(string role) => int.Parse(c.Reasoning!.Givens.Single(g => g.Role == role).Value, CultureInfo.InvariantCulture);
            if (type == ElementaryQuizType.SecondsConversion)
            {
                int expected = c.StoryContextId == "seconds-forward" ? Given("duration") * 60 : Given("duration") / 60;
                Require(c.Answers[0].Value == new ReducedFraction(expected, 1), "Incorrect seconds/minutes conversion.");
                Require(grader.ValidateSubmission(q, expected.ToString()).IsCorrect, "Bare duration rejected.");
                Require(!grader.ValidateSubmission(q, expected + (language == AppLanguage.Vietnamese ? " năm" : " years")).IsCorrect, "Wrong duration unit passed.");
            }
            else if (type == ElementaryQuizType.YearCentury)
            {
                int year = Given("year"), expected = (year + 99) / 100;
                Require(c.Answers[0].Value == new ReducedFraction(expected, 1), "Century boundary is incorrect.");
                foreach (string input in language == AppLanguage.Vietnamese ? new[] { "thế kỷ " + expected, "Thế kỉ: " + expected, expected + " thế kỷ" }
                    : new[] { "century " + expected, expected + "th century" })
                    Require(grader.ValidateSubmission(q, input).IsCorrect, "Century keyboard notation rejected: " + input);
            }
            else if (type == ElementaryQuizType.CalendarWeekday)
            {
                var date = new DateTime(Given("year"), Given("month"), Given("day"));
                int offset = Given("elapsed-days") * (c.StoryContextId == "weekday-forward" ? 1 : -1);
                int index = ((int)date.AddDays(offset).DayOfWeek + 6) % 7;
                string expected = QuizContentCatalog.Text(language, "TimeFraction.Weekday" + index);
                Require(c.AnswerText == expected, "Weekday disagrees with independent date arithmetic.");
                if (language == AppLanguage.Vietnamese)
                    Require(grader.ValidateSubmission(q, index < 6 ? "Thứ " + (index + 2) : "CN").IsCorrect, "Weekday keyboard alias rejected.");
            }
            else
            {
                var givens = c.Reasoning!.Givens;
                var source = Enumerable.Range(0, givens.Count / 2).Select(i => new ReducedFraction(Given("numerator-" + i), Given("denominator-" + i))).ToArray();
                Require(givens.Where(g => g.Role.StartsWith("denominator")).Select(g => g.Value).Distinct().Count() > 1, "Different-denominator question uses one denominator.");
                ReducedFraction[] actual = c.AnswerText.Split(" -> ").Select(s => { ReducedFraction.TryParse(s, out var f); return f; }).ToArray();
                Require(source.All(actual.Contains) && actual.Distinct().Count() == source.Length, "Order lost a supplied fraction.");
                foreach (string choice in c.ChoiceTexts!)
                {
                    string[] parts = choice.Split(" -> ");
                    Require(parts.Length == source.Length && parts.All(part => ReducedFraction.TryParse(part, out var f) && source.Contains(f)),
                        "A fraction-order distractor is not a permutation of the supplied fractions.");
                }
                for (int i = 1; i < actual.Length; i++)
                    Require((actual[i-1].Numerator * actual[i].Denominator < actual[i].Numerator * actual[i-1].Denominator)
                        == (c.StoryContextId == "fractions-ascending"), "Incorrect fraction order.");
                foreach (string separator in new[] { ", ", "; ", " - ", c.StoryContextId == "fractions-ascending" ? " < " : " > " })
                    Require(grader.ValidateSubmission(q, c.AnswerText.Replace(" -> ", separator)).IsCorrect, "Fraction keyboard separator rejected.");
                Require(!grader.ValidateSubmission(q, string.Join(" -> ", c.AnswerText.Split(" -> ").Reverse())).IsCorrect, "Reversed fraction order passed.");
                Require(!grader.ValidateSubmission(q, c.AnswerText.Replace(" -> ", c.StoryContextId == "fractions-ascending" ? " > " : " < ")).IsCorrect, "Wrong inequality sign passed.");
            }
            cases++;
        }
        foreach (var pair in new (int Year, int Century)[] { (1,1), (100,1), (101,2), (1900,19), (1901,20), (2000,20), (2001,21) })
            Require(ElementaryQuizGenerator.CenturyOfYear(pair.Year) == pair.Century, "Century boundary fixture failed.");
        Console.WriteLine($"  Checked {cases} time/fraction extensions, exact century/date/ordering math, three answer modes, keyboard aliases and incorrect submissions.");
    }
}
