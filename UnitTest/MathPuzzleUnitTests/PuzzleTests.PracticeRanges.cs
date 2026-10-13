using System.Globalization;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckPracticeNumberRanges()
    {
        var engine = new BasicArithmeticEngine();
        var validator = new ArithmeticQuizValidator(engine);
        var grader = new EssayAnswerValidator(engine);
        var seen = new HashSet<string>();
        bool largeFour = false, largeFive = false, smallFive = false, twoSteps = false, roundMillion = false;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 70; seed++)
        {
            var context = new QuizCurriculumContext(tier, false);
            var generator = new ArithmeticQuizGenerator(engine, new Random(seed));
            var numeric = generator.Generate(mode, operation, context, expandedPractice: true);
            Require(validator.Validate(numeric).IsValid, "Expanded numeric question failed validation.");
            Require(numeric.Expression.LeftOperand <= PracticeArithmeticDifficulty.Maximum(tier), "Practice operand exceeded its star cap.");
            if ((int)tier >= 4 && operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
                Require(numeric.Expression.RightOperand <= 99, "Large multiplication/division requires a one/two digit factor.");
            if (tier == CurriculumTier.FourStars) largeFour |= numeric.Expression.LeftOperand >= 100_000;
            if (tier == CurriculumTier.FiveStars) largeFive |= numeric.Expression.LeftOperand >= 1_000_000;
            if ((int)tier < 4) continue;
            foreach (var group in new[] { QuestionKnowledgeGroup.Objects, QuestionKnowledgeGroup.Money,
                QuestionKnowledgeGroup.Packaging, QuestionKnowledgeGroup.Production, QuestionKnowledgeGroup.Data })
            {
                var q = new PracticeArithmeticStoryGenerator(new Random(seed)).Generate(operation, tier, language, group, mode);
                var word = q.WordProblem!; var facts = word.PracticeDetails!;
                Require(validator.Validate(q).IsValid && q.CorrectAnswer == engine.CalculateInteger(q.Expression).Result, "Story mathematics changed.");
                Require(q.CorrectAnswer <= facts.Capacity && !word.ProblemText.Contains('{'), "Invalid capacity or unresolved prose variable.");
                Require(PracticeArithmeticDifficulty.HasAppropriateRegrouping((int)q.Expression.LeftOperand,
                    (int)q.Expression.RightOperand, operation, tier), "Four-star story requires excessive or consecutive regrouping.");
                foreach (var step in facts.Steps)
                    Require(step.Expression.LeftOperand >= 0 && step.Expression.LeftOperand <= facts.Capacity
                        && step.Answer >= 0 && step.Answer <= facts.Capacity, "An intermediate fact exceeded its context.");
                if (facts.ContextId == "classroom") Require(facts.Capacity == 40, "Million-person classroom.");
                smallFive |= tier == CurriculumTier.FiveStars && facts.Capacity == 40;
                twoSteps |= facts.Steps.Count == 2;
                roundMillion |= language == AppLanguage.Vietnamese && tier == CurriculumTier.FourStars && q.CorrectAnswer == 1_000_000;
                foreach (var purchase in facts.Purchases)
                {
                    Require(purchase.UnitPrice >= purchase.MinimumPrice && purchase.UnitPrice <= purchase.MaximumPrice,
                        "An item price escaped its reviewed range.");
                    var item = QuizContentCatalog.LoadList<PracticeMoneyItem>("PracticeMoneyItems", QuizContentCatalog.Culture(language)).Single(i => i.Id == purchase.ItemId);
                    int limit = tier == CurriculumTier.FourStars && item.MaximumCountFourStars > 0 ? item.MaximumCountFourStars : item.MaximumCount;
                    Require(purchase.Total <= facts.Capacity && purchase.Count >= 1 && purchase.Count <= limit, "Implausible shopping quantity.");
                    if (purchase.ItemId == "ballpoint-pen" && language == AppLanguage.Vietnamese)
                        Require(purchase.UnitPrice <= 10_000 && purchase.Total <= 100_000, "A pen purchase cost millions.");
                    if (purchase.ItemId == "child-bicycle") Require(tier == CurriculumTier.FiveStars, "Bicycle escaped five-star availability.");
                    seen.Add(purchase.ItemId);
                }
                if (mode == ArithmeticQuizMode.Essay)
                {
                    string solution = ElementaryWordProblemSolutionFormatter.Format(q, language, CultureInfo.InvariantCulture);
                    var graded = grader.ValidateSubmission(q, solution);
                    Require(graded.IsCorrect, $"Expanded story solution rejected: {facts.ContextId}/{operation}/{language}: {graded}; {solution}");
                    string localized = ElementaryWordProblemSolutionFormatter.Format(q, language,
                        CultureInfo.GetCultureInfo(QuizContentCatalog.Culture(language)));
                    Require(grader.ValidateSubmission(q, localized).IsCorrect, "Localized large-number solution rejected: " + localized);
                    string answer = q.CorrectAnswer.ToString(CultureInfo.InvariantCulture);
                    Require(!grader.Validate(q, word.SolutionLead, "1 + 1 = 0", answer + " " + word.AnswerUnit).IsCorrect, "Accepted incorrect calculation.");
                    if (facts.Steps.Count > 1 && facts.Steps[0].Answer != 2)
                        Require(!grader.Validate(q, word.SolutionLead, "1 + 1 = 2\n" + q.Expression.LeftOperand + " "
                            + BasicArithmeticEngine.GetSymbol(operation) + " " + q.Expression.RightOperand + " = " + answer,
                            answer + " " + word.AnswerUnit).IsCorrect, "Accepted a wrong intermediate quantity.");
                    Require(!grader.Validate(q, word.SolutionLead, q.Expression.LeftOperand + " " + BasicArithmeticEngine.GetSymbol(operation)
                        + " " + q.Expression.RightOperand + " = " + answer, answer + " km").IsCorrect, "Accepted incorrect answer unit.");
                }
            }
        }
        Require(largeFour && largeFive && smallFive && twoSteps, "Expanded ranges lost large facts, small contexts or two-step reasoning.");
        Require(seen.SetEquals(new[] { "ballpoint-pen", "cooking-oil", "fish-sauce", "child-bicycle", "school-backpack", "television", "school-laptop" }), "Some shopping items never occur.");
        // Practice opts in. The bank's default generator and profiles keep their previous ceilings.
        foreach (var tier in new[] { CurriculumTier.FourStars, CurriculumTier.FiveStars })
        {
            int oldCap = tier == CurriculumTier.FourStars ? 9_999 : 99_999;
            Require(new QuestionLearningProfile(QuestionKnowledgeGroup.Objects).ArithmeticCeiling(tier) == oldCap, "AI profile ceiling changed.");
            for (int seed = 0; seed < 50; seed++)
            {
                var old = new ArithmeticQuizGenerator(engine, new Random(seed)).Generate(ArithmeticQuizMode.Essay, ArithmeticOperation.Add, new(tier, false));
                Require(old.Expression.LeftOperand <= oldCap && old.Expression.RightOperand <= oldCap, "Default bank generation acquired practice ranges.");
            }
        }
        foreach (var language in Enum.GetValues<AppLanguage>())
            foreach (var item in QuizContentCatalog.LoadList<PracticeMoneyItem>("PracticeMoneyItems", QuizContentCatalog.Culture(language)))
                PracticeArithmeticStoryGenerator.Validate(item);
        var vietnamese = QuizContentCatalog.LoadList<PracticeCountContext>("PracticeArithmeticContexts", "vi-VN");
        var english = QuizContentCatalog.LoadList<PracticeCountContext>("PracticeArithmeticContexts", "en-US");
        Require(vietnamese.Select(c => (c.Id, c.Group, c.Capacity, c.MaximumPerGroup)).SequenceEqual(
            english.Select(c => (c.Id, c.Group, c.Capacity, c.MaximumPerGroup))), "Translations changed context identities or quantity limits.");
        Console.WriteLine($"Practice range/money matrix passed; round-million four-star sample: {roundMillion}.");
    }
}
