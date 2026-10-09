using System.Globalization;
using System.Text.Json;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckDataChartContracts()
    {
        static void Reject(Action action, string message)
        {
            try { action(); } catch (InvalidDataException) { return; }
            throw new InvalidOperationException(message);
        }
        var vi = DataChartStoryContextCatalog.GetProfile(AppLanguage.Vietnamese);
        var en = DataChartStoryContextCatalog.GetProfile(AppLanguage.English);
        Require(vi.Count == 27 && vi.Select(c => c.ContextId).Distinct().Count() == 27
            && vi.All(c => !string.IsNullOrWhiteSpace(c.ContextId)), "Every chart needs a unique stable context ID.");
        Require(vi.Select(c => c.ContextId).SequenceEqual(en.Select(c => c.ContextId))
            && vi.Zip(en).All(pair => pair.First.CategoryIds!.SequenceEqual(pair.Second.CategoryIds!)),
            "Chart context/category identity changed in translation.");
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var context in DataChartStoryContextCatalog.GetProfile(language))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in ElementaryQuizGenerator.DataChartTypes)
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var question = new ElementaryQuizGenerator(new Random(91 + count)).GenerateDataChart(mode, type, language, tier, context.ContextId);
            var c = question.ElementaryProblem!;
            DataChartQuestionValidator.Validate(c);
            var profile = c.DataChart!.Profile;
            var visual = c.Visual!;
            int Row(string id) => profile.CategoryIds.ToList().IndexOf(id);
            var g = c.Reasoning!.Givens.ToDictionary(f => f.Role, f => decimal.Parse(f.Value, CultureInfo.InvariantCulture));
            decimal expected = profile.QuestionKind switch
            {
                DataChartQuestionKind.Total => visual.Values.Sum(),
                DataChartQuestionKind.AbsoluteDifference => Math.Abs(visual.Values[Row(profile.TargetCategoryIds[0])] - visual.Values[Row(profile.TargetCategoryIds[1])]),
                _ => type == ElementaryQuizType.ReadPieChart && tier > CurriculumTier.OneStar
                    ? visual.Values[Row(profile.TargetCategoryIds[0])] * (tier <= CurriculumTier.ThreeStars ? g["total"]
                        : tier == CurriculumTier.FourStars ? g["other-count"] * 100 / (100 - visual.Values[Row(profile.TargetCategoryIds[0])])
                        : g.Where(f => f.Key.StartsWith("count-", StringComparison.Ordinal)).Sum(f => f.Value) * 100
                            / visual.Values.Where((_, i) => !visual.HiddenValueIndices!.Contains(i)).Sum()) / 100
                    : visual.Values[Row(profile.TargetCategoryIds[0])]
            };
            expected += g.GetValueOrDefault("added-after-recording");
            Require((decimal)c.Answers[0].Value.Numerator / (decimal)c.Answers[0].Value.Denominator == expected,
                "Chart answer disagrees with its visual and selected groups.");
            Require(profile.CategoryIds.Select(id => context.Labels[context.CategoryIds!.ToList().IndexOf(id)]).SequenceEqual(visual.Labels)
                && profile.HiddenCategoryIds.Select(Row).ToHashSet().SetEquals(visual.HiddenValueIndices!), "Labels or missing data moved away from their IDs.");
            Require(c.ChoiceTexts!.Distinct().Count() == 4 && c.ChoiceTexts!.Count(text => ElementaryEssayValidator.CheckAnswers(question, text)) == 1
                && ElementaryEssayValidator.CheckAnswers(question, c.PresentedText) == question.PresentedEquationIsCorrect, "Chart grading modes disagree.");
            if (mode == ArithmeticQuizMode.Essay)
            {
                var input = EssayCombinedInputParser.Parse(c.SolutionText, true, true);
                Require(grader.Validate(question, input.Solution, input.Equation, input.Answer).IsCorrect, "Chart solution failed essay grading.");
            }
            var fresh = new ElementaryQuizGenerator(new Random(count + 1091)).GenerateDataChart(mode, profile).ElementaryProblem!;
            DataChartQuestionValidator.Validate(fresh);
            Require(fresh.DataChart!.Profile.CategoryIds.SequenceEqual(profile.CategoryIds)
                && fresh.DataChart.Profile.TargetCategoryIds.SequenceEqual(profile.TargetCategoryIds)
                && fresh.DataChart.Profile.HiddenCategoryIds.SequenceEqual(profile.HiddenCategoryIds)
                && fresh.Visual!.Labels.SequenceEqual(visual.Labels), "Fresh numbers shuffled the narrative/visual roles.");
            // Round-trip is a prerequisite for a future persisted AI contract.
            var restored = JsonSerializer.Deserialize<DataChartQuestionContract>(JsonSerializer.Serialize(c.DataChart))!;
            var replay = new ElementaryQuizGenerator(new Random(761)).RecreateDataChart(mode, restored).ElementaryProblem!;
            DataChartQuestionValidator.Validate(replay);
            Require(replay.ProblemText == c.ProblemText && replay.Visual!.Values.SequenceEqual(visual.Values)
                && replay.AnswerText == c.AnswerText, "A saved contract failed to replay its chart and question together.");
            if (count % 9 == 0)
            {
                Reject(() => DataChartQuestionValidator.Validate(c with { Visual = visual with { Labels = visual.Labels.Reverse().ToArray() } }), "Swapped chart labels accepted.");
                Reject(() => DataChartQuestionValidator.Validate(c with { Visual = visual with { Unit = "invalid-unit" } }), "Changed chart unit accepted.");
                Reject(() => DataChartQuestionValidator.Validate(c with { Visual = visual with { Values = visual.Values.Select(n => n + 1).ToArray() } }), "Detached chart numbers accepted.");
                Reject(() => DataChartQuestionValidator.Validate(c with { Visual = visual with { HiddenValueIndices = new HashSet<int> { 0, 1, 2 } } }), "Changed hidden cells accepted.");
                Reject(() => DataChartQuestionValidator.Validate(c with { Reasoning = c.Reasoning with { Explanation = "SECRET " + c.AnswerText } }), "Detached explanation accepted.");
                Reject(() => DataChartQuestionValidator.Validate(c with { DataChart = c.DataChart with { Version = 99 } }), "Unknown chart contract version accepted.");
                Require(!DataChartQuestionValidator.IsValidQuestionText(c, c.ProblemText + " Answer: " + c.AnswerText), "Prose leaking the answer accepted.");
            }
            count++;
        }

        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 24; seed++)
        {
            var generator = new ElementaryQuizGenerator(new Random(seed));
            var directed = generator.GenerateDataChart(ArithmeticQuizMode.Essay, ElementaryQuizType.ChartDifference,
                language, tier, "library-genres", DataChartQuestionKind.MoreThan).ElementaryProblem!;
            var profile = directed.DataChart!.Profile;
            int rowA = profile.CategoryIds.ToList().IndexOf(profile.TargetCategoryIds[0]);
            int rowB = profile.CategoryIds.ToList().IndexOf(profile.TargetCategoryIds[1]);
            Require(directed.Visual!.Values[rowA] > directed.Visual.Values[rowB], "Directed comparison reversed its subjects.");
            Require(directed.Answers[0].Value.Numerator > 0, "More-than question used a tied pair.");
            var fresh = generator.GenerateDataChart(ArithmeticQuizMode.Essay, profile).ElementaryProblem!;
            Require(fresh.Visual!.Values[rowA] > fresh.Visual.Values[rowB]
                && fresh.DataChart!.Profile.TargetCategoryIds.SequenceEqual(profile.TargetCategoryIds), "Fresh comparison reversed direction.");
            var swapped = profile with { TargetCategoryIds = profile.TargetCategoryIds.Reverse().ToArray() };
            Reject(() => DataChartQuestionValidator.Validate(directed with { DataChart = directed.DataChart with { Profile = swapped } }), "Reversed directed target accepted.");
            Reject(() => DataChartQuestionValidator.Validate(directed with { DataChart = directed.DataChart with { Profile = profile with { QuestionKind = DataChartQuestionKind.AbsoluteDifference } } }), "Directed/absolute comparison treated as equivalent.");
        }
        foreach (var kind in new[] { "table", "bar", "pie" })
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var visual = new QuizVisualData(kind, ["A", "B", "C"], [25, 35, 987654321], "%",
                HiddenValueIndices: new HashSet<int> { 2 }, AccessibleDescription: "SECRET 987654321",
                Annotations: [new("SECRET 987654321", 0, 0)]);
            var display = QuizChartPresentation.ForDisplay(visual, false);
            var changed = QuizChartPresentation.ForDisplay(visual with { Values = [25, 35, 111111111] }, false);
            Require(display.Values.SequenceEqual(changed.Values) && display.AccessibleDescription is null
                && display.Annotations is null, "Drawing/caption projection depends on a hidden value.");
            string description = QuizDiagramDescriptionFormatter.Format(null, visual, false, language);
            Require(!description.Contains("987654321") && !description.Contains("SECRET")
                && !QuizChartPresentation.DescribeValues(visual, false).Contains("987654321"), "Screen reader or caption leaked missing data.");
            Require(QuizDiagramDescriptionFormatter.Format(null, visual, true, language).Contains("987654321")
                && QuizChartPresentation.ForDisplay(visual, true).Values[2] == 987654321, "Solution reveal did not restore chart data.");
        }
        Console.WriteLine($"  Checked {count} bilingual chart/type/star/mode profiles, pinned fresh data, directed comparisons and hidden-data presentation.");
    }
}
