using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>C# owns chart data/roles; optional reviewed AI clauses may change presentation.</summary>
public static class DataChartQuestionValidator
{
    public static void ValidateProfile(DataChartProfile profile)
    {
        if (profile is null || !Enum.IsDefined(profile.Language) || !Enum.IsDefined(profile.Tier)
            || !ElementaryQuizGenerator.DataChartTypes.Contains(profile.Type)) Fail("Invalid chart profile.");
        var context = DataChartStoryContextCatalog.GetProfile(profile.Language)
            .SingleOrDefault(c => c.ContextId == profile.ContextId);
        if (context is null || profile.CategoryIds is null || profile.CategoryIds.Count != 3
            || !profile.CategoryIds.ToHashSet(StringComparer.Ordinal).SetEquals(context.CategoryIds!))
            Fail("Changed chart context or categories.");
        var ids = profile.CategoryIds;
        var targets = profile.TargetCategoryIds;
        if (targets is null || targets.Count == 0 || targets.Distinct(StringComparer.Ordinal).Count() != targets.Count
            || targets.Any(id => !ids.Contains(id))) Fail("Invalid chart target.");
        if (ElementaryQuizGenerator.IsAdditionalDataType(profile.Type))
        {
            int level = (int)profile.Tier;
            if (profile.Type == ElementaryQuizType.ReadPictograph)
            {
                var kind = level <= 2 ? DataChartQuestionKind.CategoryValue : level <= 4
                    ? DataChartQuestionKind.Total : DataChartQuestionKind.CombinedDifference;
                if (profile.QuestionKind != kind || (level <= 2 ? targets.Count != 1
                    : !targets.SequenceEqual(ids.Take(level == 3 ? 2 : 3)))
                    || profile.HiddenCategoryIds is null || profile.HiddenCategoryIds.Count != 0)
                    Fail("Changed pictograph key or target.");
            }
            else if (profile.Type == ElementaryQuizType.SortData)
            {
                if (profile.QuestionKind != DataChartQuestionKind.CategoryCounts || !targets.SequenceEqual(ids)
                    || profile.HiddenCategoryIds is null || !profile.HiddenCategoryIds.SequenceEqual(ids))
                    Fail("Changed classification target.");
            }
            else if (profile.QuestionKind != DataChartQuestionKind.CategoryValue || targets.Count != 1
                || profile.HiddenCategoryIds is null || !profile.HiddenCategoryIds.SequenceEqual(targets))
                Fail("Changed missing bar target.");
            return;
        }
        bool difference = profile.Type == ElementaryQuizType.ChartDifference;
        if (difference)
        {
            string[] pair = (int)profile.Tier == 5 ? [ids[0], ids[1]] : [ids[1], ids[2]];
            if (profile.QuestionKind is not (DataChartQuestionKind.AbsoluteDifference or DataChartQuestionKind.MoreThan)
                || !targets.ToHashSet(StringComparer.Ordinal).SetEquals(pair)) Fail("Changed comparison subjects or direction.");
        }
        else if (profile.Type == ElementaryQuizType.ChartTotal)
        {
            if (profile.QuestionKind != DataChartQuestionKind.Total || !targets.SequenceEqual(ids)) Fail("Changed chart total target.");
        }
        else if (profile.QuestionKind != DataChartQuestionKind.CategoryValue || targets.Count != 1
            || (profile.Type != ElementaryQuizType.ReadPieChart && (int)profile.Tier >= 3 && targets[0] != ids[2]))
            Fail("Changed category target.");
        string[] hidden = profile.Type == ElementaryQuizType.ReadPieChart
            ? (int)profile.Tier is 3 or 5 ? [targets[0]] : []
            : (int)profile.Tier < 3 ? []
            : (difference || profile.Type == ElementaryQuizType.ChartTotal) && (int)profile.Tier < 5 ? [ids[2]]
            : (int)profile.Tier == 3 ? [ids[2]] : [ids[1], ids[2]];
        if (profile.HiddenCategoryIds is null || !profile.HiddenCategoryIds.SequenceEqual(hidden))
            Fail("Changed hidden chart categories.");
    }

    public static bool IsValidQuestionText(ElementaryQuizContract contract, string candidate)
    {
        Validate(contract);
        // Adding a future reviewed phrasing must explicitly preserve these roles;
        // a free-form replacement is not accepted as a semantic equivalent.
        return string.Equals(candidate?.Trim(), contract.ProblemText.Trim(), StringComparison.Ordinal);
    }

    public static void Validate(ElementaryQuizContract contract)
    {
        if (contract.DataChart is not { } chart || chart.DataSeed < 0 || chart.Version != 1) Fail("Missing or unsupported chart data contract.");
        chart = contract.DataChart!;
        ValidateProfile(chart.Profile);
        if (contract.Kind != QuizProblemKind.Data || contract.Type != chart.Profile.Type
            || contract.Language != chart.Profile.Language || contract.StoryContextId != chart.Profile.ContextId
            || contract.Reasoning?.Tier != chart.Profile.Tier) Fail("Chart and question profile disagree.");
        var expected = ElementaryQuizGenerator.RebuildDataChart(chart);
        var visual = contract.Visual;
        var expectedVisual = expected.Visual!;
        if (visual is null || visual.Kind != expectedVisual.Kind || visual.Unit != expectedVisual.Unit
            || visual.ScenarioId != chart.Profile.ContextId || !visual.Labels.SequenceEqual(expectedVisual.Labels)
            || !visual.Values.SequenceEqual(expectedVisual.Values)
            || visual.HiddenValueIndices is null || !visual.HiddenValueIndices.SetEquals(expectedVisual.HiddenValueIndices!)
            || visual.AccessibleDescription is not null || visual.Annotations is not null || visual.Polygons is not null
            || visual.Lines is not null || visual.PictographKey != expectedVisual.PictographKey || visual.RotationDegrees != 0)
            Fail("Chart labels, data, units or hidden cells disagree.");
        var supplied = chart.Observations ?? [];
        var original = expected.DataChart!.Observations ?? [];
        if (supplied.Count != original.Count || supplied.Zip(original).Any(pair => pair.First.Role != pair.Second.Role
            || pair.First.Excluded != pair.Second.Excluded || !pair.First.CategoryIds.SequenceEqual(pair.Second.CategoryIds)))
            Fail("Changed observation lists or exclusions.");
        bool originalProse = contract.ProblemText.Equals(expected.ProblemText, StringComparison.Ordinal)
            && contract.Reasoning!.Steps.SequenceEqual(expected.Reasoning!.Steps);
        if (!contract.Facts.SequenceEqual(expected.Facts) || !contract.Constants.ToHashSet().SetEquals(expected.Constants)
            || !contract.Answers.SequenceEqual(expected.Answers) || contract.RequiresSolution != expected.RequiresSolution
            || contract.Reasoning!.ScenarioId != expected.Reasoning!.ScenarioId
            || !contract.Reasoning.Givens.SequenceEqual(expected.Reasoning.Givens)
            || !contract.Reasoning.Steps.Select((step, i) => step with { Label = expected.Reasoning.Steps.ElementAtOrDefault(i)?.Label ?? "" })
                .SequenceEqual(expected.Reasoning.Steps)
            || !contract.Reasoning.IntermediateUnits.ToHashSet().SetEquals(expected.Reasoning.IntermediateUnits)
            || contract.Reasoning.Explanation != expected.Reasoning.Explanation
            || contract.Reasoning.SupportingDiagram != expected.Reasoning.SupportingDiagram)
            Fail("Chart prose, relations or answer disagree with the C# data.");
        if (!originalProse && !ReviewedDataChartProse.Matches(contract))
            Fail("Chart wording changes facts, units or the requested category.");
        if (chart.Profile.QuestionKind == DataChartQuestionKind.MoreThan)
        {
            int first = chart.Profile.CategoryIds.ToList().IndexOf(chart.Profile.TargetCategoryIds[0]);
            int second = chart.Profile.CategoryIds.ToList().IndexOf(chart.Profile.TargetCategoryIds[1]);
            if (visual!.Values[first] <= visual.Values[second])
                Fail("The first category is not greater than the second category.");
        }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string reason) => throw new InvalidDataException(reason);
}
