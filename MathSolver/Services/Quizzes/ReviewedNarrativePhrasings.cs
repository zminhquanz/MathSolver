namespace MathSolver.Services;

/// <summary>Human-reviewed, equivalent clauses from the same language pack as the C# lesson.</summary>
internal sealed record ReviewedNarrativePhrasings(string Id, string[] Alternatives,
    Dictionary<string, string[]>? AlternativesByUnit = null)
{
    internal const string ListName = "Narrative.MultiStep.Phrasings";
    internal const string MotionListName = "Narrative.Motion.Phrasings";
    internal const string ProportionListName = "Narrative.Proportion.Phrasings";
    internal const string DecimalListName = "Narrative.Decimal.Phrasings";
    internal const string MeasurementListName = "Narrative.Measurement.Phrasings";
    internal const string RemainderListName = "Narrative.Remainder.Phrasings";
    internal const string TimeListName = "Narrative.Time.Phrasings";
    internal const string FractionQuantityListName = "Narrative.FractionQuantity.Phrasings";

    internal static IReadOnlyDictionary<string, string[]> ForFractionQuantity(AppLanguage language)
    {
        var result = new Dictionary<string, string[]>(For(language, FractionQuantityListName), StringComparer.Ordinal);
        foreach (var context in FractionQuantityStoryContextCatalog.GetProfile(language))
        {
            result.Add(context.PartProblemTemplate, context.PartProblemAlternatives!);
            result.Add(context.WholeProblemTemplate, context.WholeProblemAlternatives!);
        }
        return result;
    }

    internal static IReadOnlyDictionary<string, string[]> For(AppLanguage language, string listName = ListName,
        string unit = "")
    {
        string culture = QuizContentCatalog.Culture(language);
        return QuizContentCatalog.LoadList<ReviewedNarrativePhrasings>(listName, culture)
            .GroupBy(row => QuizContentCatalog.Entry(culture, row.Id).ForUnit(unit), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.SelectMany(row =>
                row.AlternativesByUnit?.TryGetValue(unit, out var choices) == true ? choices : row.Alternatives)
                .Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
    }
}
