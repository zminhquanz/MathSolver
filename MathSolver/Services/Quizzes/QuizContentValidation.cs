using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    public static void CheckLists(QuizContentPack pack)
    {
        foreach (var (key, fields) in ContextFields)
            if (pack.Lists.TryGetValue(key, out var contexts))
            {
                if (contexts.GetArrayLength() == 0) Fail("Empty context list: " + key);
                foreach (var context in contexts.EnumerateArray())
                {
                    foreach (string field in fields) _ = RequiredString(context, field);
                    if (context.TryGetProperty("Capacity", out _)) CheckCapacity(context);
                    foreach (string field in new[] { "MaximumSize", "MaximumDimension", "DistanceScale", "TimeDivisor" })
                        if (context.TryGetProperty(field, out var number) && (!number.TryGetInt32(out int value) || value <= 0))
                            Fail("Invalid positive quantity: " + key + "." + field);
                }
            }
        if (pack.Lists.TryGetValue("MotionQuizGenerator.Watercraft", out var craft))
            foreach (var name in craft.EnumerateArray())
                if (name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())) Fail("Empty watercraft name.");
        if (pack.Lists.TryGetValue("Stories", out var stories))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var story in stories.EnumerateArray())
            {
                string id = RequiredString(story, "Id");
                string family = RequiredString(story, "Family");
                if (family is not ("Applied" or "Fraction") || !ids.Add(family + "." + id)) Fail("Invalid or duplicate story: " + id);
                var prose = story.GetProperty("Prose");
                string unitId = RequiredString(prose, "UnitId");
                foreach (string field in new[] { "GivenA", "GivenB", "Questions", "Leads" })
                    foreach (var value in RequiredArray(prose, field).EnumerateArray())
                        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) Fail("Empty prose: " + id + "." + field);
                foreach (var example in RequiredArray(story, "Examples").EnumerateArray())
                {
                    foreach (string field in new[] { "GivenA", "GivenB", "Question", "SolutionLead" }) _ = RequiredString(example, field);
                    if (RequiredString(example, "UnitId") != unitId) Fail("Changed story unit: " + id);
                }
            }
        }
        if (pack.Lists.TryGetValue("FractionQuantityContexts", out var fractions))
            foreach (var context in fractions.EnumerateArray())
            {
                foreach (string field in new[] { "Unit", "PartLabel", "WholeLabel" }) _ = RequiredString(context, field);
                foreach (string field in new[] { "PartProblemTemplate", "WholeProblemTemplate" })
                    if (!Slots(RequiredString(context, field)).SetEquals(["0", "1"])) Fail("Fraction quantity templates require exactly {0} and {1}.");
                CheckCapacity(context);
            }
        if (pack.Lists.TryGetValue("DataChartContexts", out var charts))
            foreach (var context in charts.EnumerateArray())
            {
                foreach (string field in new[] { "Description", "Unit", "QuantityName" }) _ = RequiredString(context, field);
                var labels = RequiredArray(context, "Labels").EnumerateArray().Select(x => x.GetString()).ToArray();
                if (labels.Length < 3 || labels.Any(string.IsNullOrWhiteSpace) || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length)
                    Fail("Chart categories must contain at least three distinct labels.");
                CheckCapacity(context);
            }
    }

    /// <summary>Author check: complete keys, stable value slots and the existing mathematical scenarios.</summary>
    public static void CheckTranslation(QuizContentPack pack, QuizContentPack reference)
    {
        QuizContentCatalog.Validate(pack);
        var texts = pack.Texts.ToDictionary(x => x.Id, StringComparer.Ordinal);
        foreach (var original in reference.Texts)
        {
            if (!texts.TryGetValue(original.Id, out var translated)) Fail("Missing text ID: " + original.Id);
            if (!original.Variables.ToHashSet(StringComparer.Ordinal).SetEquals(translated!.Variables)) Fail("Changed value slots: " + original.Id);
            // Descriptive slots differ between the original Vietnamese and English sentences.
            // All mathematical fact placeholders that remain for the second renderer must be preserved.
            var originalFacts = Slots(original.Text).Except(original.Variables).ToHashSet(StringComparer.Ordinal);
            var translatedFacts = Slots(translated.Text).Except(translated.Variables).ToHashSet(StringComparer.Ordinal);
            if (!originalFacts.SetEquals(translatedFacts)) Fail("Changed fact placeholders: " + original.Id);
        }
        foreach (var list in reference.Lists)
        {
            if (!pack.Lists.TryGetValue(list.Key, out var translated)) Fail("Missing list: " + list.Key);
            if (translated.GetArrayLength() != list.Value.GetArrayLength()) Fail("Changed list length: " + list.Key);
            if (ContextFields.ContainsKey(list.Key) || list.Key is "FractionQuantityContexts" or "DataChartContexts")
                for (int i = 0; i < list.Value.GetArrayLength(); i++)
                {
                    var originalRow = list.Value[i]; var translatedRow = translated[i];
                    foreach (string field in FixedContextFields)
                        if (originalRow.TryGetProperty(field, out var expected) &&
                            (!translatedRow.TryGetProperty(field, out var actual) || expected.GetRawText() != actual.GetRawText()))
                            Fail("Changed mathematical metadata: " + list.Key + "." + field);
                    foreach (string field in new[] { "Template", "PartProblemTemplate", "WholeProblemTemplate" })
                        if (originalRow.TryGetProperty(field, out var expected) &&
                            (!translatedRow.TryGetProperty(field, out var actual) || !Slots(expected.GetString()!).SetEquals(Slots(actual.GetString()!))))
                            Fail("Changed context placeholders: " + list.Key + "." + field);
                }
        }
        if (reference.Lists.TryGetValue("Stories", out var sourceStories))
        {
            var stories = pack.Lists["Stories"].EnumerateArray().ToDictionary(x => RequiredString(x, "Family") + "." + RequiredString(x, "Id"));
            foreach (var source in sourceStories.EnumerateArray())
            {
                string id = RequiredString(source, "Family") + "." + RequiredString(source, "Id");
                if (!stories.TryGetValue(id, out var translated)) Fail("Missing scene: " + id);
                if (RequiredString(translated.GetProperty("Prose"), "UnitId") != RequiredString(source.GetProperty("Prose"), "UnitId")) Fail("Changed dimension: " + id);
                var sourceProse = source.GetProperty("Prose"); var targetProse = translated.GetProperty("Prose");
                foreach (string field in new[] { "GivenA", "GivenB", "Questions", "Leads" })
                {
                    var expected = sourceProse.GetProperty(field).EnumerateArray().Select(value => Slots(value.GetString()!)).ToArray();
                    foreach (var value in targetProse.GetProperty(field).EnumerateArray())
                        if (!expected.Any(slots => slots.SetEquals(Slots(value.GetString()!)))) Fail("Changed story placeholders: " + id + "." + field);
                }
            }
        }
    }

    private static readonly Dictionary<string, string[]> ContextFields = new(StringComparer.Ordinal)
    {
        ["AverageQuizGenerator.DirectContexts"] = ["Id", "Action", "Unit", "Subject", "Period"],
        ["AverageQuizGenerator.DistributionContexts"] = ["Group", "Unit", "Subject"],
        ["AverageQuizGenerator.TwoGroupContexts"] = ["Member", "Unit", "Subject"],
        ["PercentageQuizGenerator.Contexts"] = ["Unit", "Subject", "RatioPart"],
        ["PercentageQuizGenerator.PercentageStories"] = ["Id", "Whole", "Part", "Unit"],
        ["GeometryQuizGenerator.Templates"] = ["ShapeId", "Measurement", "Unit", "Object", "Shape"],
        ["ProportionQuizGenerator.Templates"] = ["Type", "Scenario", "Template", "Unit", "Subject", "RateProfile"],
        ["ElementaryQuizGenerator.PackingStories"] = ["Id", "Item", "Container"],
        ["MotionQuizGenerator.UnitProfiles"] = ["Kind", "SpeedUnit", "TimeUnit", "DistanceUnit"],
        ["MotionQuizGenerator.MovingSubjects"] = ["Kind", "Name"]
    };
    private static readonly string[] FixedContextFields = ["Id", "ContextId", "ShapeId", "Measurement", "Type", "Scenario",
        "RateProfile", "Kind", "Capacity", "MaximumSize", "MaximumDimension", "DistanceScale", "TimeDivisor",
        "EnglishOnly", "Money", "AsksForAdditionalPeople"];

    private static HashSet<string> Slots(string text) => Placeholder().Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    private static string RequiredString(JsonElement element, string field)
    {
        if (!element.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            Fail("Missing string: " + field);
        return value.GetString()!;
    }
    private static JsonElement RequiredArray(JsonElement element, string field)
    {
        if (!element.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
            Fail("Missing nonempty list: " + field);
        return value;
    }
    private static void CheckCapacity(JsonElement context)
    {
        if (!context.TryGetProperty("Capacity", out var value) || !value.TryGetInt32(out int capacity) || capacity <= 0)
            Fail("Context capacity must be a positive integer.");
    }
    private static void Fail(string message) => throw new InvalidDataException(message);
    [GeneratedRegex(@"(?<!\{)\{([A-Za-z0-9_.-]+)\}(?!\})", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
