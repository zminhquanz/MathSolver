using System.Globalization;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckCurriculumExpansion()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        var shapes = new HashSet<string>();
        var complexities = new Dictionary<(ElementaryQuizType, CurriculumTier), int>();
        int tested = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var family in new[] { QuizProblemKind.MultiStep, QuizProblemKind.Measurement, QuizProblemKind.Data, QuizProblemKind.VisualGeometry })
        foreach (var type in ElementaryQuizGenerator.Types(family).Where(type => family == QuizProblemKind.MultiStep || type is ElementaryQuizType.MapScale or ElementaryQuizType.ReadPictograph or ElementaryQuizType.RecognizeShape))
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 12; seed++)
        {
            var q = new ElementaryQuizGenerator(new Random(seed)).Generate(mode, family, type, language, tier);
            var c = q.ElementaryProblem!;
            var given = c.Reasoning!.Givens.ToDictionary(x => x.Role, x => decimal.Parse(x.Value, CultureInfo.InvariantCulture));
            decimal G(string key) => given.GetValueOrDefault(key);
            decimal value = (decimal)c.Answers[0].Value.Numerator / (decimal)c.Answers[0].Value.Denominator;
            int level = (int)tier;
            decimal expected = 0;
            if (family == QuizProblemKind.MultiStep)
            {
                if (type == ElementaryQuizType.MultiStepAddSubtract)
                    expected = level <= 3 ? G("first") + G("second") + G("third") - G("used")
                        : G("remaining") + G("removed") - G("added") - G("each") * G("groups");
                else
                {
                    expected = G("each") * (G("groups") - G("unused-groups")) + G("second") + G("each-second") * G("groups-second") + G("extra") - G("used");
                    if (type == ElementaryQuizType.MultiStepShare) expected /= G("shares");
                }
                Require(expected > 0 && expected == decimal.Truncate(expected), "Multi-step quantities/shares must be positive whole items.");
                Require(value <= QuizStoryContextCatalog.Find(c.StoryContextId!).MaximumPerPeriod, "Multi-step result exceeds the context capacity.");
                complexities[(type, tier)] = c.Reasoning.Steps.Count;
                Require(c.RequiresSolution && c.Reasoning.Steps.Count >= 2, "Multi-step work must have intermediate reasoning.");
            }
            else if (type == ElementaryQuizType.MapScale)
            {
                expected = level == 5 ? (G("actual-km") * 1000 + G("actual-m")) * 100 / G("scale-denominator")
                    : (G("map-cm") + G("second-map-cm")) * G("scale-denominator") / (level <= 2 ? 100 : 100_000);
                Require(c.Answers[0].Unit == (level == 5 ? "cm" : level <= 2 ? "m" : "km"), "Map scale answer has the wrong dimension.");
            }
            else if (type == ElementaryQuizType.ReadPictograph)
            {
                var visual = c.Visual!;
                Require(visual.Kind == "pictograph" && visual.PictographKey == G("key"), "Missing pictograph key.");
                for (int row = 0; row < 3; row++)
                    Require(visual.Values[row] == G("icons-" + row) * G("key") && G("icons-" + row) <= 7, "Pictograph disagrees with the legend.");
                expected = level <= 2 ? visual.Values[c.DataChart!.Profile.CategoryIds.ToList().IndexOf(c.DataChart.Profile.TargetCategoryIds[0])] : level == 3 ? visual.Values[0] + visual.Values[1]
                    : level == 4 ? visual.Values.Sum() : visual.Values[0] + visual.Values[1] - visual.Values[2];
            }
            else
            {
                Require(c.Visual!.Kind == "shape" && c.Visual.Labels.Count == 0 && c.Answers[0].IsText && !c.RequiresSolution,
                    "Shape recognition must not reveal the name or require invented calculations.");
                shapes.Add(c.Visual.ScenarioId!);
                var direct = EssayCombinedInputParser.Parse(c.AnswerText, false, true, allowTextAnswer: true);
                Require(grader.Validate(q, direct.Solution, direct.Equation, direct.Answer).IsCorrect, "A direct shape name should be accepted.");
            }
            if (type != ElementaryQuizType.RecognizeShape)
            {
                Require(value == expected, $"Independent answer disagrees: {type}/{tier}/{seed}.");
                Require(!grader.Validate(q, c.Answers[0].Label, c.Answers[0].Expression + "=999999 " + c.Answers[0].Unit, c.AnswerText).IsCorrect,
                    "Correct answer cannot hide an incorrect calculation.");
                Require(!ElementaryEssayValidator.CheckAnswers(q, expected.ToString(CultureInfo.InvariantCulture) + " invalidunit"), "Wrong unit must be rejected.");
            }
            var parsed = EssayCombinedInputParser.Parse(c.SolutionText, c.RequiresSolution, true);
            var grade = grader.Validate(q, parsed.Solution, parsed.Equation, parsed.Answer);
            Require(grade.IsCorrect, $"Model solution rejected: {type}/{language}/{tier}/{seed}: {string.Join(" | ", grade.Details)}\n{c.SolutionText}");
            if (c.Reasoning.Steps.Count > 0)
            {
                var reversed = string.Join("\n", c.Reasoning.Steps.Reverse().Select(s => s.Expression + "=" + s.DisplayValue + (s.Unit.Length > 0 ? " " + s.Unit : "")));
                Require(grader.Validate(q, parsed.Solution, reversed, parsed.Answer).IsCorrect, $"Reordered steps rejected: {type}/{tier}.");
                Require(grader.Validate(q, parsed.Solution, c.Answers[0].Expression + "=" + c.AnswerText, parsed.Answer).IsCorrect, "Merged steps rejected.");
                var earlier = new List<ElementaryInferenceStep>();
                var split = new List<string>();
                foreach (var step in c.Reasoning.Steps)
                {
                    string expression = step.Expression;
                    foreach (var previous in earlier.OrderByDescending(previous => previous.Expression.Length))
                        expression = expression.Replace(previous.Expression, "(" + previous.DisplayValue + ")", StringComparison.Ordinal);
                    split.Add(expression + "=" + step.DisplayValue + (step.Unit.Length > 0 ? " " + step.Unit : ""));
                    earlier.Add(step);
                }
                var splitGrade = grader.Validate(q, parsed.Solution, string.Join("\n", split), parsed.Answer);
                Require(splitGrade.IsCorrect, $"Split steps rejected: {type}/{tier}/{seed}: {string.Join(" | ", splitGrade.Details)}\n{string.Join("\n", split)}");

            }
            tested++;
        }
        // At 1 : 100000, kilometres and map centimetres happen to have equal
        // numeric values. A kilometre subtotal is valid; it is not the final cm answer.
        for (int seed = 0; seed < 100; seed++)
        {
            var map = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Measurement, ElementaryQuizType.MapScale, AppLanguage.Vietnamese, CurriculumTier.FiveStars);
            var c = map.ElementaryProblem!;
            if (!c.Reasoning!.Givens.Any(g => g.Role == "scale-denominator" && g.Value == "100000")) continue;
            var parsed = EssayCombinedInputParser.Parse(c.SolutionText, true, true);
            Require(grader.Validate(map, parsed.Solution, parsed.Equation, parsed.Answer).IsCorrect, "Equal-valued units rejected a legitimate subtotal.");
            string final = c.Answers[0].Expression + "=" + c.Answers[0].DisplayValue;
            Require(!grader.Validate(map, parsed.Solution, final + " km", parsed.Answer).IsCorrect, "A km subtotal must not be accepted as the final cm calculation.");
            var subtotal = c.Reasoning.Steps.First(s => s.Label.Contains("Tổng quãng"));
            Require(!grader.Validate(map, parsed.Solution, subtotal.Expression + "=" + subtotal.DisplayValue + " km", parsed.Answer).IsCorrect,
                "A correct final answer cannot replace the requested conversion.");
        }
        Require(shapes.Count == 11, "Shape recognition did not cover all configured plane and solid shapes.");
        foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.MultiStep))
            Require(complexities[(type, CurriculumTier.FiveStars)] > complexities[(type, CurriculumTier.OneStar)], "Multi-step stars must increase inference depth.");
        int mixed = 0;
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var seen = new HashSet<(QuizProblemKind, ElementaryQuizType)>();
            for (int seed = 0; seed < 1800; seed++)
            {
                var request = QuizCurriculumLayer.ResolveMixedRequest(tier, new Random(seed));
                if (!ElementaryQuizGenerator.Supports(request.Kind)) continue;
                Require(request.ElementaryType.HasValue && QuizCurriculumLayer.GetMixedElementaryTypes(request.Kind, tier).Contains(request.ElementaryType.Value),
                    "Mixed chose an unfiltered elementary subtype.");
                seen.Add((request.Kind, request.ElementaryType!.Value));
                var q = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay, request.Kind, request.ElementaryType, AppLanguage.Vietnamese, tier);
                var c = q.ElementaryProblem!;
                var parsed = EssayCombinedInputParser.Parse(c.SolutionText, c.RequiresSolution, true, requireAnswerLabel: c.Type == ElementaryQuizType.ReadClock);
                var graded = grader.Validate(q, parsed.Solution, parsed.Equation, parsed.Answer);
                Require(graded.IsCorrect, $"Mixed example fails grading: {request.Kind}/{c.Type}/{tier}/{seed}: {string.Join(" | ", graded.Details)}\n{c.SolutionText}");
                mixed++;
            }
            foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
            foreach (var type in QuizCurriculumLayer.GetMixedElementaryTypes(kind, tier))
                Require(seen.Contains((kind, type)), $"An allowed subtype is unreachable in Mixed: {kind}/{type}/{tier}.");
        }
        Console.WriteLine($"  Checked {tested} new skill contracts with independent arithmetic and {mixed} filtered Mixed questions.");
    }
}
