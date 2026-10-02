using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Localization;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckComparisons()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        var aiValidator = new LlmWordProblemValidator();
        var relations = new Dictionary<QuizProblemKind, HashSet<string>>
        {
            [QuizProblemKind.Arithmetic] = [], [QuizProblemKind.Fraction] = []
        };
        int count = 0;
        bool equivalentFractions = false;
        foreach (var kind in relations.Keys)
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 48; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).GenerateComparison(mode, kind, language, tier);
            var contract = question.ElementaryProblem!;
            Require(contract.Kind == kind && contract.IsComparison && !contract.RequiresSolution,
                "Comparison must stay in its selected group and need no invented solution sentence.");
            var facts = contract.Facts.Select(int.Parse).ToArray();
            Require(facts.All(value => value > 0 && value <= QuizCurriculumLayer.GetMaximumOperandValue(tier)),
                "Comparison facts exceed the star level.");
            BigInteger left = kind == QuizProblemKind.Fraction ? (BigInteger)facts[0] * facts[3] : facts[0];
            BigInteger right = kind == QuizProblemKind.Fraction ? (BigInteger)facts[2] * facts[1] : facts[1];
            string expected = left < right ? "<" : left > right ? ">" : "=";
            relations[kind].Add(expected);
            equivalentFractions |= kind == QuizProblemKind.Fraction && expected == "=" && facts[1] != facts[3];
            Require(contract.AnswerText == expected, "Exact independent comparison disagrees with the answer.");
            Require(ElementaryEssayValidator.CheckAnswers(question, contract.PresentedText) == question.PresentedEquationIsCorrect,
                "The presented true/false comparison is inconsistent.");
            var choices = contract.ChoiceTexts!;
            Require(choices.Distinct().Count() == choices.Count &&
                choices.Count(choice => ElementaryEssayValidator.CheckAnswers(question, choice)) == 1,
                "Comparison choices must contain exactly one correct answer.");
            foreach (string input in new[] { expected, "Answer: " + expected, contract.FormatComparison(expected), contract.SolutionText })
            {
                var parts = EssayCombinedInputParser.Parse(input, false, true);
                Require(validator.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                    "Valid sign, complete comparison or example was rejected: " + input);
            }
            string wrong = expected == ">" ? "<" : ">";
            var failure = validator.Validate(question, null, null, wrong);
            Require(!failure.IsCorrect && failure.Details.Count > 0, "A wrong sign needs explicit feedback.");
            Require(!validator.Validate(question, null, contract.FormatComparison(wrong), expected).IsCorrect,
                "A correct answer must not hide an incorrect comparison.");
            Require(!validator.Validate(question, null, null, null).IsCorrect &&
                !validator.Validate(question, null, null, ";;").IsCorrect,
                "Empty or separator-only answers must fail.");

            string prompt = LlmQuizPromptBuilder.BuildElementaryUserPrompt(contract);
            Require(LlmWordProblemParser.TryParse(prompt[prompt.IndexOf('{')..], out var draft,
                out _, out _, allowEmptyAnswerUnit: true), "The production AI schema must parse.");
            var ai = aiValidator.ValidateElementary(draft!, contract);
            Require(ai.IsValid && validator.Validate(question with { WordProblem = ai.WordProblem }, null, null, expected).IsCorrect,
                "AI and Algorithm comparison grading must agree.");
            var changedDraft = new LlmWordProblemDraft
            {
                ProblemText = contract.ProblemText.Replace(facts[0].ToString(), "123456789", StringComparison.Ordinal),
                AnswerUnit = contract.Answers[0].Unit
            };
            Require(!aiValidator.ValidateElementary(changedDraft, contract).IsValid, "AI cannot change comparison operands.");
            count++;
        }
        Require(relations.Values.All(values => values.SetEquals(["<", ">", "="])) && equivalentFractions,
            "Generate all relations, including equal fractions with different denominators.");

        foreach (var language in new[] { "vi", "en" })
        foreach (var key in new[] { "Quiz.ComparisonSubtype", "Quiz.ComparisonQuestionTitle", "Quiz.ComparisonTrueFalseTitle",
            "Quiz.ComparisonEssayHint", "Quiz.ComparisonEssayPlaceholder", "Quiz.Elementary.IntegerCompare" })
            Require(QuizLocalizationOverrides.TryGetValue(key, language, out var text) && !string.IsNullOrWhiteSpace(text),
                "Comparison controls need both translations: " + key);

        var fractionQuestion = new ElementaryQuizGenerator(new Random(1)).GenerateComparison(
            ArithmeticQuizMode.Essay, QuizProblemKind.Fraction, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        var equal = fractionQuestion with { ElementaryProblem = fractionQuestion.ElementaryProblem! with
        {
            Facts = ["1", "2", "2", "4"], Answers = [new("Dấu so sánh", new(0, 1), "", "", Text: "=")]
        } };
        Require(validator.Validate(equal, null, "1/2 = 2/4", null).IsCorrect &&
            validator.Validate(equal, null, null, "0.5 = 2/4").IsCorrect,
            "Equivalent fraction values must be accepted exactly.");
        Require(!validator.Validate(equal, null, null, "1/3 = 2/4").IsCorrect &&
            !validator.Validate(equal, null, null, "1 = 1").IsCorrect,
            "A true comparison of unrelated values must not replace the given values.");
        var different = equal with { ElementaryProblem = equal.ElementaryProblem! with
        {
            Facts = ["1", "3", "1", "2"], Answers = [new("Dấu so sánh", new(0, 1), "", "", Text: "<")]
        } };
        Require(validator.Validate(different, null, null, "1/2 > 1/3").IsCorrect,
            "A reversed comparison with the correctly reversed sign is valid.");

        var catalog = new QuizProblemTypeCatalog(new Random(23));
        foreach (var kind in relations.Keys)
        {
            int index = catalog.Options.ToList().FindIndex(option => option.FixedRequest?.Kind == kind);
            QuizProblemRequest Resolve(ArithmeticOperation? basic, FractionOperation? fraction, bool compare = false) =>
                catalog.Resolve(index, basic, fraction, null, null, null, null, null, null, CurriculumTier.OneStar,
                    basicComparison: kind == QuizProblemKind.Arithmetic && compare,
                    fractionComparison: kind == QuizProblemKind.Fraction && compare);
            Require(Resolve(null, null, true).IsComparison, "Fixed comparison selection must reach both generators.");
            Require(!Resolve(ArithmeticOperation.Add, FractionOperation.Add).IsComparison,
                "Explicit arithmetic selections must keep their operation.");
            bool mixedComparison = false, mixedArithmetic = false;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var request = Resolve(null, null);
                Require(request.Kind == kind, "Subtype mixing must preserve the selected family.");
                mixedComparison |= request.IsComparison;
                mixedArithmetic |= !request.IsComparison;
            }
            Require(mixedComparison && mixedArithmetic, "Subtype mixed must include comparisons and arithmetic.");
        }
        Console.WriteLine($"  Checked {count} bilingual integer/fraction comparison contracts, exact grading and AI responses.");
    }
}
