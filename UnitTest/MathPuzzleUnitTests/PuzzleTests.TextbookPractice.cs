using System.Globalization;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckTextbookPractice()
    {
        var engine = new BasicArithmeticEngine();
        var grader = new EssayAnswerValidator(engine);
        bool roundHundredMillions = false, decimalScaleFactor = false, largePopulation = false, largeProduction = false, bulkBudget = false;
        foreach (var tier in new[] { CurriculumTier.FourStars, CurriculumTier.FiveStars })
        {
            int internalZeroRuns = 0;
            for (int seed = 0; seed < 400; seed++)
            {
                int value = PracticeNumberNames.Next(new Random(seed), tier);
                internalZeroRuns += value.ToString(CultureInfo.InvariantCulture).TrimEnd('0').Contains("00") ? 1 : 0;
                foreach (var style in Enum.GetValues<PracticeArithmeticStyle>())
                foreach (var operation in Enum.GetValues<ArithmeticOperation>())
                {
                    var q = new ArithmeticQuizGenerator(engine, new Random(seed)).Generate(ArithmeticQuizMode.Essay, operation,
                        new(tier, false), expandedPractice: true, requestedStyle: style);
                    var a = q.Expression.LeftOperand; var b = q.Expression.RightOperand;
                    var expected = operation switch { ArithmeticOperation.Add => a + b, ArithmeticOperation.Subtract => a - b,
                        ArithmeticOperation.Multiply => a * b, _ => a / b };
                    Require(q.ArithmeticStyle == style && q.CorrectAnswer == expected && expected >= 0
                        && expected <= PracticeArithmeticDifficulty.Maximum(tier, style), "Skill-specific arithmetic bounds or exact answer changed.");
                    Require(grader.ValidateSubmission(q, expected.ToString(CultureInfo.InvariantCulture)).IsCorrect,
                        "Numeric skill rejected a direct answer.");
                    if (style == PracticeArithmeticStyle.Mental)
                    {
                        var coefficient = operation == ArithmeticOperation.Divide ? expected : a;
                        while (coefficient > 0 && coefficient % 10 == 0) coefficient /= 10;
                        Require(coefficient <= 99, "Mental calculation acquired a long arbitrary coefficient.");
                        roundHundredMillions |= tier == CurriculumTier.FourStars && a >= 100_000_000;
                        decimalScaleFactor |= b == 100 || b == 1000;
                    }
                }
            }
            Require(internalZeroRuns >= 20, "Zero-class practice is missing at a high-star level.");
        }
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            for (int seed = 0; seed < 400; seed++)
            {
                foreach (var group in new[] { QuestionKnowledgeGroup.Data, QuestionKnowledgeGroup.Production })
                {
                    var q = new PracticeArithmeticStoryGenerator(new Random(seed)).Generate(ArithmeticOperation.Add,
                        CurriculumTier.FourStars, language, group, ArithmeticQuizMode.Essay);
                    var details = q.WordProblem!.PracticeDetails!;
                    largePopulation |= details.ContextId == "population-statistics" && q.CorrectAnswer >= 100_000_000;
                    largeProduction |= details.ContextId == "annual-paper-production" && q.CorrectAnswer >= 100_000_000;
                    if (details.ContextId is "population-statistics" or "annual-paper-production")
                    {
                        Require(details.Steps.Count == 1 && q.CorrectAnswer <= details.Capacity,
                            "Statistics/annual-production prose acquired an unrelated transfer step.");
                        Require(grader.ValidateSubmission(q, ElementaryWordProblemSolutionFormatter.Format(q, language,
                            CultureInfo.GetCultureInfo(QuizContentCatalog.Culture(language)))).IsCorrect, "Context-specific solution rejected.");
                    }
                }
                var shopping = new PracticeArithmeticStoryGenerator(new Random(seed)).Generate(ArithmeticOperation.Add,
                    CurriculumTier.FiveStars, language, QuestionKnowledgeGroup.Money, ArithmeticQuizMode.Essay);
                var purchases = shopping.WordProblem!.PracticeDetails!.Purchases;
                bulkBudget |= language == AppLanguage.Vietnamese && purchases[0].ItemId == "school-laptop"
                    && shopping.CorrectAnswer >= 100_000_000;
                foreach (var purchase in purchases)
                    Require(shopping.WordProblem.ProblemText.Contains(purchase.UnitPrice.ToString("N0",
                        CultureInfo.GetCultureInfo(QuizContentCatalog.Culture(language)))), "Shopping prose omitted the unit price.");
            }
            foreach (var style in Enum.GetValues<PracticeArithmeticStyle>())
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                Require(!PracticeArithmeticDifficulty.Prompt(style, mode, language).Contains("PracticeArithmetic."), "Skill heading missing from JSON.");
        }
        Require(roundHundredMillions && decimalScaleFactor && largePopulation && largeProduction && bulkBudget,
            "Textbook skill ranges, large-context stories or institutional purchases are unreachable.");
        Console.WriteLine("Textbook practice passed: round hundred millions at four stars, written calculations, internal zeros, population, annual output and bounded unit prices.");
    }
}
