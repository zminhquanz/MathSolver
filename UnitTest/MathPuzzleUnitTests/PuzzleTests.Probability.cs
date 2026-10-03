using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckProbabilityVariety()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.Probability))
        {
            bool vi = language == AppLanguage.Vietnamese;
            var generator = new ElementaryQuizGenerator(new Random(731));
            var contexts = new HashSet<string>();
            var cycle = new HashSet<string>();
            var problems = new HashSet<string>();
            var categories = new HashSet<string>();
            string? previous = null;
            bool sawZero = false, sawAll = false, sawComplement = false, sawDirect = false;
            for (int iteration = 0; iteration < 120; iteration++)
            {
                // Keep one generator as the app does, while changing answer modes.
                var mode = Enum.GetValues<ArithmeticQuizMode>()[iteration % 3];
                var question = generator.Generate(mode, QuizProblemKind.Probability, type, language, tier);
                var contract = question.ElementaryProblem!;
                var scenario = contract.ProbabilityScenario!;
                Require(scenario.TotalCount > 0 && scenario.EventCount >= 0 && scenario.EventCount <= scenario.TotalCount,
                    "Probability counts must describe a finite experiment.");
                Require(scenario.UsesObservedResults == (type == ElementaryQuizType.ExperimentalProbability),
                    "Observed frequency must not be treated as theoretical likelihood.");
                Require(scenario.ContextId != previous, "Adjacent probability questions repeated their context.");
                Require(cycle.Add(scenario.ContextId), "A probability context repeated before its rotation completed.");
                if (cycle.Count == 10) cycle.Clear();
                previous = scenario.ContextId;
                contexts.Add(scenario.ContextId);
                problems.Add(contract.ProblemText);
                Require(contract.ProblemText.Contains(scenario.EventText, StringComparison.Ordinal),
                    "The requested event is missing from the problem.");
                Require(!contract.RequiresSolution, "Keep the established short-answer probability requirements.");

                if (type == ElementaryQuizType.Likelihood)
                {
                    string expected = scenario.EventCount == 0 ? (vi ? "Không Thể" : "impossible")
                        : scenario.EventCount == scenario.TotalCount ? (vi ? "Chắc Chắn" : "certain")
                        : vi ? "Có Thể" : "possible";
                    Require(contract.Answers.Single().Text == expected, "Likelihood differs from the possible outcomes.");
                    categories.Add(expected);
                    var counts = contract.Reasoning!.Givens.ToDictionary(given => given.Role, given => int.Parse(given.Value));
                    int n = counts["outcomes"], k = counts["favorable"], level = (int)tier;
                    if (level == 5) { n -= counts["removed-favorable"]; k -= counts["removed-favorable"]; }
                    int independentTotal = level <= 2 ? n : level == 3 ? n * n : n * (n - 1);
                    int independentFavorable = level == 1 ? k : level == 2 ? n - k : level == 3 ? k * k : k * (k - 1);
                    Require(scenario.TotalCount == independentTotal && scenario.EventCount == independentFavorable,
                        "Joint or complement likelihood does not match its sampling rule.");

                    Require(contract.ProblemText.Contains(vi ? "có thể nhưng không chắc chắn" : "possible but not certain"),
                        "The possible category must be distinct from certainty.");
                    Require(contract.SolutionText.Contains(vi ? "kết quả" : "outcome"), "Classification needs an explanation.");
                    if ((int)tier <= 2 && scenario.ContextId == "die")
                    {
                        Require(scenario.TotalCount == 6, "A standard die must have six outcomes.");
                        if (expected == (vi ? "Có Thể" : "possible"))
                            Require(scenario.EventCount == 3, "Even/greater-than-three die events each have three outcomes.");
                    }
                    if ((int)tier <= 2 && scenario.ContextId == "coin") Require(scenario.TotalCount == 2, "The stated flat coin has two possible upper sides.");
                    if ((int)tier == 1 && scenario.ContextId == "number-cards" && expected == (vi ? "Có Thể" : "possible"))
                    {
                        bool even = scenario.EventText.Contains(vi ? "chẵn" : "even");
                        int independentCount = Enumerable.Range(1, scenario.TotalCount).Count(value => (value % 2 == 0) == even);
                        Require(scenario.EventCount == independentCount, "Number-card parity event count is incorrect.");
                    }
                    if (expected == (vi ? "Có Thể" : "possible"))
                        Require(ElementaryEssayValidator.CheckAnswers(question, vi ? "có thể nhưng không chắc chắn" : "possible but not certain"),
                            "The unambiguous possible label should be accepted.");
                }
                else
                {
                    // Compute the numerator independently from the recorded figures and wording.
                    var givens = contract.Reasoning!.Givens.ToDictionary(given => given.Role,
                        given => int.Parse(given.Value, CultureInfo.InvariantCulture));
                    int batches = givens.Keys.Count(key => key.StartsWith("total-", StringComparison.Ordinal));
                    int observed = 0, total = 0;
                    for (int batch = 0; batch < batches; batch++)
                    {
                        int trials = givens["total-" + batch];
                        total += trials;
                        observed += givens.TryGetValue("success-" + batch, out int success) ? success : trials - givens["failures-" + batch];
                    }
                    total -= givens.GetValueOrDefault("invalid-failures");
                    bool complement = scenario.EventText.Contains(vi ? "không xảy ra" : "does not occur");
                    int numerator = complement ? total - observed : observed;
                    Require(scenario.TotalCount == total && scenario.EventCount == numerator &&
                        contract.Answers.Single().Value == new ReducedFraction(numerator, total),
                        "Experimental fraction does not match the recorded direct/complement event.");
                    Require(contract.ProblemText.Contains(vi ? "đã ghi nhận" : "recorded"),
                        "An empirical fraction must be based on observed trials.");
                    sawZero |= numerator == 0;
                    sawAll |= numerator == total;
                    sawComplement |= complement;
                    sawDirect |= !complement;
                    Require(ElementaryEssayValidator.CheckAnswers(question, $"{numerator * 2}/{total * 2}"),
                        "Equivalent unreduced observed fractions must remain accepted.");
                    Require(!ElementaryEssayValidator.CheckAnswers(question, $"{(numerator + 1) % (total + 1)}/{total}"),
                        "A fraction with an incorrect event count was accepted.");
                    foreach (string choice in contract.ChoiceTexts!)
                    {
                        Require(EssayCalculationEvaluator.TryEvaluate(choice, out var value, out _) &&
                            value.Numerator >= 0 && value.Numerator <= value.Denominator,
                            "Probability distractors must lie in [0, 1].");
                    }
                    if (scenario.ContextId is "marbles" or "number-cards" or "tokens")
                        Require(contract.ProblemText.Contains(vi ? "sau mỗi lần rút" : "after every draw"),
                            "Repeated draws must explicitly replace the object.");
                }

                Require(contract.ChoiceTexts!.Count == 4 && contract.ChoiceTexts.Distinct().Count() == 4 &&
                    contract.ChoiceTexts.Count(choice => ElementaryEssayValidator.CheckAnswers(question, choice)) == 1,
                    "Probability multiple choice must have four choices and one correct answer.");
                Require(ElementaryEssayValidator.CheckAnswers(question, contract.PresentedText) == question.PresentedEquationIsCorrect,
                    "Probability true/false grading disagrees with the presented answer.");
                var parts = EssayCombinedInputParser.Parse(contract.SolutionText, contract.RequiresSolution, true);
                Require(grader.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                    "Probability sample solution was rejected: " + contract.SolutionText);

                count++;
            }
            Require(contexts.Count == 10 && problems.Count > 90, "The probability bank did not provide enough varied problems.");
            if (type == ElementaryQuizType.Likelihood) Require(categories.Count == 3, "Likelihood rotation missed an answer category.");
            else Require(sawZero && sawAll && ((int)tier == 1 ? sawDirect : (int)tier == 2 ? sawComplement : sawComplement && sawDirect),
                "Observed fractions missed a boundary or a tier-appropriate event/complement case.");
        }

        // Separate rotations retain their progress when changing subtype, language, or stars.
        var alternating = new ElementaryQuizGenerator(new Random(19));
        var rotations = new Dictionary<(AppLanguage, ElementaryQuizType), HashSet<string>>();
        for (int round = 0; round < 10; round++)
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.Probability))
        {
            var key = (language, type);
            if (!rotations.TryGetValue(key, out var seen)) rotations[key] = seen = [];
            var question = alternating.Generate(ArithmeticQuizMode.Essay, QuizProblemKind.Probability, type,
                language, Enum.GetValues<CurriculumTier>()[round % 5]);
            Require(seen.Add(question.ElementaryProblem!.ProbabilityScenario!.ContextId),
                "Changing language, mode, or difficulty reset a probability context rotation.");
        }
        Console.WriteLine($"  Checked {count} probability stories, rotations, boundaries, choices, grading and contracts.");
    }
}
