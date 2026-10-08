using System.Globalization;
using System.Numerics;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckFractionQuantityContexts()
    {
        var vi = FractionQuantityStoryContextCatalog.GetProfile(AppLanguage.Vietnamese);
        var en = FractionQuantityStoryContextCatalog.GetProfile(AppLanguage.English);
        Require(vi.Count == 15 && vi.Select(c => c.ContextId).Distinct().Count() == 15
            && vi.Select(c => (c.ContextId, c.Quantity, c.Capacity)).SequenceEqual(en.Select(c => (c.ContextId, c.Quantity, c.Capacity))),
            "Fraction context IDs and quantity metadata must be stable across languages.");
        Require(vi.Count(c => c.Quantity == WordProblemQuantity.Count) == 12
            && vi.Count(c => c.Quantity is WordProblemQuantity.Mass or WordProblemQuantity.Distance or WordProblemQuantity.Capacity) == 3,
            "Counted objects and measured quantities must have distinct policies.");
        var fractionalResults = new HashSet<string>();
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var context in FractionQuantityStoryContextCatalog.GetProfile(language))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in new[] { ElementaryQuizType.FractionOfNumber, ElementaryQuizType.WholeFromFraction })
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 8; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).GenerateFractionQuantityStory(mode, type, language, tier, context.ContextId);
            var p = question.ElementaryProblem!;
            Require(p.StoryContextId == context.ContextId, "Pinned context was changed when generating a question.");
            var g = p.Reasoning!.Givens.ToDictionary(g => g.Role, g => BigInteger.Parse(g.Value, CultureInfo.InvariantCulture));
            BigInteger supplied = tier <= CurriculumTier.TwoStars || tier == CurriculumTier.FiveStars ? g["quantity"]
                : g["quantity-first"] + g["quantity-second"] - g.GetValueOrDefault("removed");
            var share = new ReducedFraction(g["numerator"] * (tier == CurriculumTier.FiveStars ? 3 : 1),
                g["denominator"] * (tier == CurriculumTier.FiveStars ? 4 : 1));
            var expected = type == ElementaryQuizType.FractionOfNumber ? new ReducedFraction(supplied * share.Numerator, share.Denominator)
                : new ReducedFraction(supplied * share.Denominator, share.Numerator);
            var whole = type == ElementaryQuizType.FractionOfNumber ? new ReducedFraction(supplied, 1) : expected;
            Require(p.Answers.Count == 1 && p.Answers[0].Value == expected && p.Answers[0].Unit == context.Unit
                && expected.Numerator > 0 && whole.Numerator <= context.Capacity * whole.Denominator,
                "Fraction part/whole value or physical capacity is incorrect.");
            if (context.Quantity == WordProblemQuantity.Count)
                Require(expected.Denominator.IsOne && p.Reasoning.Steps.Where(s => s.Unit.Length > 0).All(s => s.Value.Denominator.IsOne),
                    "Counted objects must not have fractional answers or intermediate quantities.");
            else if (!expected.Denominator.IsOne) fractionalResults.Add(context.ContextId + "/" + type + "/" + tier);
            Require(p.ChoiceTexts!.Count == 4 && p.ChoiceTexts.Distinct().Count() == 4
                && p.ChoiceTexts.Count(c => ElementaryEssayValidator.CheckAnswers(question, c)) == 1,
                "Fractional measured answers broke multiple-choice grading.");
            Require(ElementaryEssayValidator.CheckAnswers(question, p.PresentedText) == question.PresentedEquationIsCorrect,
                "Fractional measured answers broke true/false grading.");
            if (mode == ArithmeticQuizMode.Essay)
            {
                var input = EssayCombinedInputParser.Parse(p.SolutionText, true, true);
                Require(grader.Validate(question, input.Solution, input.Equation, input.Answer).IsCorrect,
                    "Exact fraction solution was rejected: " + context.ContextId + "/" + type + "/" + tier);
                Require(!ElementaryEssayValidator.CheckAnswers(question, expected + " invalidunit"), "Wrong dimensions were accepted.");
            }
            count++;
        }
        foreach (var context in vi.Where(c => c.Quantity != WordProblemQuantity.Count))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            Require(fractionalResults.Contains(context.ContextId + "/" + ElementaryQuizType.FractionOfNumber + "/" + tier),
                "Measured part results still forced to integers.");
            if (tier >= CurriculumTier.ThreeStars)
                Require(fractionalResults.Contains(context.ContextId + "/" + ElementaryQuizType.WholeFromFraction + "/" + tier),
                    "Measured whole results still forced to integers.");
        }
        try
        {
            new ElementaryQuizGenerator().GenerateFractionQuantityStory(ArithmeticQuizMode.Essay, ElementaryQuizType.FractionOfNumber,
                AppLanguage.Vietnamese, CurriculumTier.OneStar, "retired-context");
            throw new InvalidOperationException("Unknown context was silently substituted.");
        }
        catch (ArgumentException) { }
        Console.WriteLine($"  Checked {count} pinned bilingual fraction quantity profiles, exact math, integer object counts, capacity, fractional measurements and all grading modes.");
    }
}
