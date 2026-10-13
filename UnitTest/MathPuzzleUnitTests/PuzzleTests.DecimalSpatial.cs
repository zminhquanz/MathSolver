using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckDecimalSpatialSkills()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int cases = 0, leadingZeroes = 0;
        foreach (var type in ElementaryQuizGenerator.DecimalSkillTypes.Concat(ElementaryQuizGenerator.SpatialSkillTypes))
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 60; seed++)
        {
            var q = new ElementaryQuizGenerator(new Random(seed)).GenerateDecimalSpatialSkill(mode, type, language, tier);
            var c = q.ElementaryProblem!;
            foreach (string input in new[] { c.AnswerText, "Answer: " + c.AnswerText, c.SolutionText })
                Require(grader.ValidateSubmission(q, input).IsCorrect, $"Rejected {type}/{language}/{tier}: {input}");
            Require(c.ChoiceTexts!.Distinct().Count() == 4 && c.ChoiceTexts!.Count(s => ElementaryEssayValidator.CheckAnswers(q, s)) == 1,
                $"Ambiguous choices: {type}/{language}/{tier}/{seed}: " + string.Join(" | ",c.ChoiceTexts!) + " expected " + c.AnswerText);
            Require(ElementaryEssayValidator.CheckAnswers(q, c.PresentedText) == q.PresentedEquationIsCorrect, "Wrong true/false representation: " + type);
            Require(!grader.ValidateSubmission(q, "").IsCorrect && !grader.ValidateSubmission(q, "invalid").IsCorrect, "Invalid result accepted.");
            if (ElementaryQuizGenerator.SpatialSkillTypes.Contains(type))
            {
                int target = type == ElementaryQuizType.SpatialFront ? 0 : type == ElementaryQuizType.SpatialBehind ? 2 : 1;
                Require(c.AnswerText == c.Visual!.Labels[target] && c.Visual.Kind == "foundation-queue", "Queue answer does not match the facing direction.");
                Require(c.Visual.AccessibleDescription == c.ProblemText.Split('?')[0][..c.Visual.AccessibleDescription!.Length], "Accessible queue facts mismatch.");
            }
            else
            {
                decimal value = decimal.Parse(c.Reasoning!.Givens[0].Value, CultureInfo.InvariantCulture);
                string[] parts = value.ToString(CultureInfo.InvariantCulture).Split('.');
                if (type == ElementaryQuizType.ReadDecimal)
                {
                    string fractional = language == AppLanguage.Vietnamese
                        ? string.Concat(parts[1].TakeWhile(ch => ch == '0').Select(_ => "không ")) + PracticeNumberNames.Format(int.Parse(parts[1]),language)
                        : string.Join(" ", parts[1].Select(ch => PracticeNumberNames.Grammar(language).Digits[ch-'0']));
                    string expected = PracticeNumberNames.Format(int.Parse(parts[0]), language)
                        + (language == AppLanguage.Vietnamese ? " phẩy " : " point ")
                        + fractional;
                    Require(c.AnswerText == expected, "Incorrect decimal reading.");
                    foreach (string alias in c.Answers[0].Aliases!) Require(grader.ValidateSubmission(q, alias).IsCorrect, "Reading alias rejected.");
                }
                if (type == ElementaryQuizType.WriteDecimal || type == ElementaryQuizType.FractionToDecimal)
                {
                    Require((decimal)c.Answers[0].Value.Numerator / (decimal)c.Answers[0].Value.Denominator == value, "Decimal writing/conversion value mismatch.");
                    foreach (string separator in new[] { ".", "," })
                        Require(grader.ValidateSubmission(q, value.ToString(CultureInfo.InvariantCulture).Replace(".", separator)).IsCorrect, "Decimal keyboard separator rejected.");
                }
                if (type == ElementaryQuizType.DecimalPlace)
                {
                    int place = int.Parse(c.Reasoning.Givens.Single(g=>g.Role=="place-index").Value);
                    Require(parts[1][place].ToString() == c.AnswerText, "Incorrect decimal place digit.");
                }
                if (type == ElementaryQuizType.DecimalParts)
                {
                    Require(c.Answers[0].Text == parts[0] && c.Answers[1].Text == parts[1], "Decimal parts lost zeroes.");
                    Require(grader.ValidateSubmission(q, parts[0]+"; "+parts[1]).IsCorrect, "Unlabeled decimal parts rejected.");
                    if (parts[1].StartsWith('0'))
                    {
                        leadingZeroes++;
                        Require(!grader.ValidateSubmission(q, parts[0]+"; "+parts[1].TrimStart('0')).IsCorrect, "Lost leading fractional zero passed.");
                    }
                }
                if (type == ElementaryQuizType.DecimalToFraction)
                {
                    int denominator = (int)Math.Pow(10,parts[1].Length), numerator = (int)(value*denominator);
                    Require(c.AnswerText == numerator+"/"+denominator, "Decimal fraction was reduced or given the wrong denominator.");
                    Require(!grader.ValidateSubmission(q, numerator*2+"/"+denominator*2).IsCorrect, "Incorrect required denominator passed.");
                }
                if (type == ElementaryQuizType.OrderDecimals)
                {
                    decimal[] source = c.Reasoning.Givens.Where(g => g.Role.StartsWith("ordered-")).Select(g => decimal.Parse(g.Value, CultureInfo.InvariantCulture)).ToArray();
                    decimal[] sorted = source.Order().ToArray();
                    if (c.StoryContextId == "decimals-descending") Array.Reverse(sorted);
                    Require(c.AnswerText.Split(';').Select(s => decimal.Parse(s.Trim().Replace(',','.'), CultureInfo.InvariantCulture)).SequenceEqual(sorted), "Lexical decimal ordering error.");
                    foreach (string separator in new[] { ", ", " -> ", " - ", c.StoryContextId == "decimals-ascending" ? " < " : " > " })
                        Require(grader.ValidateSubmission(q, c.AnswerText.Replace("; ", separator)).IsCorrect, "Decimal order separator rejected.");
                    Require(!grader.ValidateSubmission(q, string.Join("; ",c.AnswerText.Split(';').Reverse())).IsCorrect, "Reversed decimal order passed.");
                    Require(!grader.ValidateSubmission(q, c.AnswerText.Replace("; ", c.StoryContextId == "decimals-ascending" ? " > " : " < ")).IsCorrect, "Contradictory inequality passed.");
                }
            }
            cases++;
        }
        Require(leadingZeroes > 0, "No leading-zero decimal coverage.");
        Console.WriteLine($"  {cases} decimal/spatial questions checked, including {leadingZeroes} leading-zero cases.");
    }
}
