using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    public static void CheckLists(QuizContentPack pack)
    {
        foreach (string key in new[] { "ProportionQuizGenerator.Templates", "ProportionQuizGenerator.Templates.Extensions" })
        if (pack.Lists.TryGetValue(key, out var proportionTemplates))
        {
            var texts = pack.Texts.ToDictionary(t => t.Id, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in proportionTemplates.EnumerateArray())
            {
                if (!row.TryGetProperty("NarrativeId", out _)) continue; // Legacy positional language packs.
                string id = RequiredString(row, "NarrativeId");
                if (!ids.Add(id) || !id.StartsWith("ProportionQuizGenerator.Narrative.", StringComparison.Ordinal)
                    || !texts.TryGetValue(id, out var source)) Fail("Invalid proportion narrative ID: " + id);
                var entry = texts[id];
                if (entry.Variables.Length != 3) Fail("Proportion requires three named roles: " + id);
                string named = RequiredString(row, "Template");
                for (int i = 0; i < 3; i++) named = named.Replace("{" + i + "}", "{" + entry.Variables[i] + "}");
                if (named != entry.Text) Fail("Named proportion template changed positional facts: " + id);
            }
        }
        if (pack.Lists.TryGetValue(ElementaryQuizGenerator.DecimalContextsList, out var decimalContexts))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (decimalContexts.GetArrayLength() == 0) Fail("Empty decimal story contexts.");
            var decimalRows = decimalContexts.EnumerateArray().AsEnumerable().Concat(
                pack.Lists.TryGetValue(ElementaryQuizGenerator.DecimalContextsList + ".Extensions", out var decimalExtra)
                    ? decimalExtra.EnumerateArray().AsEnumerable() : []);
            foreach (var row in decimalRows)
            {
                if (!ids.Add(RequiredString(row, "Id"))) Fail("Duplicate decimal story context.");
                _ = RequiredString(row, "Item");
                string unit = RequiredString(row, "Unit"), small = RequiredString(row, "SmallUnit");
                int factor = row.GetProperty("ConversionFactor").GetInt32();
                if (!((unit == "kg" && small == "g" && factor == 1000)
                    || (unit == "m" && small == "cm" && factor == 100)
                    || (unit is "l" or "litres" && small == "ml" && factor == 1000))
                    || row.GetProperty("MaximumQuantity").GetInt32() < 100)
                    Fail("Invalid decimal story dimension or capacity.");
                if (row.TryGetProperty("Activity", out var activity))
                {
                    string activityId = RequiredString(row, "Activity");
                    _ = RequiredString(row, "GroupUnit");
                    if (activityId != RequiredString(row, "Id") || !row.TryGetProperty("GroupMaximum", out var limit)
                        || !limit.TryGetDecimal(out decimal maximum) || maximum < 1
                        || maximum * 5 + 1 > row.GetProperty("MaximumQuantity").GetInt32())
                        Fail("Invalid decimal activity or per-group capacity.");
                    foreach (string clause in ElementaryQuizGenerator.DecimalActivityClauses)
                        if (!pack.Texts.Any(text => text.Id == "ElementaryQuizGenerator.DecimalStories.Activity." + activityId + "." + clause))
                            Fail("Incomplete decimal activity wording: " + activityId);
                }
            }
        }
        if (pack.Lists.TryGetValue(ElementaryQuizGenerator.MeasurementContextsList, out var measurementContexts))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var categories = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in measurementContexts.EnumerateArray())
            {
                if (!ids.Add(RequiredString(row, "Id"))) Fail("Duplicate measurement story context.");
                _ = RequiredString(row, "Subject");
                string category = RequiredString(row, "Category");
                if (category is not ("mass" or "capacity" or "length")) Fail("Invalid measurement story dimension.");
                categories.Add(category);
            }
            if (categories.Count != 3) Fail("Missing measurement story dimension.");
        }
        foreach (string listName in ReviewedNarrativePhrasings.ListNames)
        if (pack.Lists.TryGetValue(listName, out var phrasings))
        {
            var texts = pack.Texts.ToDictionary(text => text.Id, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (phrasings.GetArrayLength() == 0) Fail("Empty reviewed narrative list.");
            foreach (var row in phrasings.EnumerateArray())
            {
                string id = RequiredString(row, "Id");
                string prefix = listName == ReviewedNarrativePhrasings.MotionListName ? "MotionQuizGenerator."
                    : listName == ReviewedNarrativePhrasings.TwoNumbersListName ? "ElementaryQuizGenerator."
                    : listName == ReviewedNarrativePhrasings.AverageListName ? "AverageQuizGenerator."
                    : listName == ReviewedNarrativePhrasings.PercentageListName ? "PercentageQuizGenerator."
                    : listName == ReviewedNarrativePhrasings.GeometryListName ? "GeometryQuizGenerator.Difficulty."
                    : listName == ReviewedNarrativePhrasings.DataListName ? "ElementaryQuizGenerator.DataDifficulty."
                    : listName == ReviewedNarrativePhrasings.ProportionListName ? "ProportionQuizGenerator.Narrative."
                    : listName == ReviewedNarrativePhrasings.DecimalListName ? "ElementaryQuizGenerator.DecimalStories."
                    : listName == ReviewedNarrativePhrasings.MeasurementListName ? "ElementaryQuizGenerator.MeasurementStories."
                    : listName == ReviewedNarrativePhrasings.RemainderListName ? "ElementaryQuizGenerator.ContextStories.CreateRemainderStory."
                    : listName == ReviewedNarrativePhrasings.TimeListName ? "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty."
                    : listName == ReviewedNarrativePhrasings.FractionQuantityListName ? "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty."
                    : "ElementaryQuizGenerator.MultiStep.CreateMultiStep.";
                if (!ids.Add(id) || !id.StartsWith(prefix, StringComparison.Ordinal)
                    || !texts.TryGetValue(id, out var source)) Fail("Unknown or duplicate reviewed narrative: " + id);
                string[] parts = Regex.Split(texts[id].Text.Trim(), @"(?<=[.!?])\s+");
                var choiceGroups = new List<JsonElement> { RequiredArray(row, "Alternatives") };
                if (row.TryGetProperty("AlternativesByUnit", out var byUnit))
                {
                    if (byUnit.ValueKind != JsonValueKind.Object) Fail("Invalid unit phrasing map: " + id);
                    var units = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entry in byUnit.EnumerateObject())
                    {
                        if (string.IsNullOrWhiteSpace(entry.Name) || !units.Add(entry.Name)
                            || entry.Value.ValueKind != JsonValueKind.Array || entry.Value.GetArrayLength() == 0)
                            Fail("Invalid unit phrasing choices: " + id);
                        choiceGroups.Add(entry.Value);
                    }
                }
                foreach (var choices in choiceGroups)
                {
                    // Different units may share an equivalent sentence, but each
                    // unit's own alternatives must still be distinct.
                    var distinct = new HashSet<string>(StringComparer.Ordinal) { texts[id].Text.Trim() };
                    foreach (var choice in choices.EnumerateArray())
                    {
                        if (choice.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(choice.GetString())) Fail("Empty reviewed phrasing: " + id);
                        string text = choice.GetString()!;
                        string[] alternative = Regex.Split(text.Trim(), @"(?<=[.!?])\s+");
                        if (!distinct.Add(text.Trim()) || text.Any(char.IsControl) || text.Length > 700
                            || alternative.Length != parts.Length || Regex.Replace(text, @"\{[A-Za-z0-9_.-]+\}", "").Any(char.IsDigit))
                            Fail("Invalid reviewed phrasing: " + id);
                        for (int i = 0; i < parts.Length; i++)
                            if (!Placeholder().Matches(parts[i]).Select(m => m.Value)
                                    .SequenceEqual(Placeholder().Matches(alternative[i]).Select(m => m.Value))
                                || parts[i].EndsWith('?') != alternative[i].EndsWith('?'))
                                Fail("Changed ordered roles or target: " + id);
                    }
                }
            }
        }
        if (pack.Lists.TryGetValue("Foundation.NumberNames", out var numberNames))
        {
            var values = new HashSet<int>();
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (numberNames.GetArrayLength() < 4) Fail("Number vocabulary needs at least four distinct choices.");
            foreach (var name in numberNames.EnumerateArray())
            {
                if (!name.GetProperty("Value").TryGetInt32(out int value) || value < 0 || !values.Add(value))
                    Fail("Invalid or duplicate number vocabulary value.");
                if (!words.Add(RequiredString(name, "Words").Trim())) Fail("Duplicate number vocabulary wording.");
            }
            if (values.Count(value => value <= 100) < 4)
                Fail("Number vocabulary needs at least four choices within the introductory range 0-100.");
        }
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
        foreach (var entry in ContextFields.Where(entry => entry.Key.EndsWith(".Extensions", StringComparison.Ordinal)))
        {
            string baseKey = entry.Key[..^".Extensions".Length];
            if (!pack.Lists.TryGetValue(entry.Key, out var extensions) || !pack.Lists.TryGetValue(baseKey, out var originals)) continue;
            string idField = baseKey == "ProportionQuizGenerator.Templates" ? "NarrativeId" : "Id";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in originals.EnumerateArray().Concat(extensions.EnumerateArray()))
                if (row.TryGetProperty(idField, out var id) && !ids.Add(id.GetString()!))
                    Fail("Duplicate expanded context: " + baseKey + "." + id.GetString());
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
        {
            var contextIds = new HashSet<string>(StringComparer.Ordinal);
            var fractionRows = fractions.EnumerateArray().AsEnumerable().Concat(
                pack.Lists.TryGetValue("FractionQuantityContexts.Extensions", out var fractionExtra)
                    ? fractionExtra.EnumerateArray().AsEnumerable() : []).ToArray();
            foreach (var context in fractionRows)
            {
                string contextId = RequiredString(context, "ContextId");
                if (!Regex.IsMatch(contextId, @"^[a-z][a-z0-9-]*$") || !contextIds.Add(contextId))
                    Fail("Invalid or duplicate fraction context ID: " + contextId);
                if (RequiredString(context, "Quantity") is not ("Count" or "Mass" or "Distance" or "Capacity"))
                    Fail("Unsupported fraction quantity: " + contextId);
                foreach (string field in new[] { "Unit", "PartLabel", "WholeLabel" }) _ = RequiredString(context, field);
                foreach (string field in new[] { "PartProblemTemplate", "WholeProblemTemplate" })
                {
                    string template = RequiredString(context, field);
                    if (!Slots(template).SetEquals(["0", "1"])) Fail("Fraction quantity templates require exactly {0} and {1}.");
                    string alternativesField = field.Replace("Template", "Alternatives", StringComparison.Ordinal);
                    var alternatives = RequiredArray(context, alternativesField);
                    if (alternatives.GetArrayLength() < 2) Fail("Fraction quantity facts need reviewed alternatives.");
                    var seen = new HashSet<string>(StringComparer.Ordinal) { template.Trim() };
                    string[] original = Regex.Split(template.Trim(), @"(?<=[.!?])\s+");
                    foreach (var alternative in alternatives.EnumerateArray())
                    {
                        if (alternative.ValueKind != JsonValueKind.String) Fail("Invalid fraction quantity phrasing.");
                        string text = alternative.GetString()!;
                        string[] parts = Regex.Split(text.Trim(), @"(?<=[.!?])\s+");
                        if (!seen.Add(text.Trim()) || text.Any(char.IsControl) || text.Length > 700 || parts.Length != original.Length
                            || Regex.Replace(text, @"\{[01]\}", "").Any(char.IsDigit)) Fail("Invalid fraction quantity phrasing.");
                        for (int i = 0; i < parts.Length; i++)
                            if (!Placeholder().Matches(parts[i]).Select(m => m.Value).SequenceEqual(Placeholder().Matches(original[i]).Select(m => m.Value))
                                || parts[i].EndsWith('?') != original[i].EndsWith('?')) Fail("Changed fraction quantity roles or target.");
                    }
                }
                CheckCapacity(context);
            }
            if (pack.Lists.TryGetValue(FractionQuantityActivityCatalog.ListName, out var activities))
            {
                var activityIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var activity in activities.EnumerateArray())
                {
                    if (!activityIds.Add(RequiredString(activity, "ContextId"))) Fail("Duplicate fraction activity.");
                    foreach (string field in new[] { "PartSource", "WholeSource", "InitialAction", "PartAction" })
                    {
                        string text = RequiredString(activity, field);
                        if (Slots(text).Count != 0 || text.Any(char.IsDigit) || text.Any(char.IsControl))
                            Fail("Fraction activity cannot supply numerical facts.");
                    }
                }
                if (!activityIds.SetEquals(contextIds)) Fail("Missing or unknown fraction activity.");
            }
        }
        if (pack.Lists.TryGetValue("DataChartContexts", out var charts))
        {
            var contextIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var context in charts.EnumerateArray())
            {
                if (!contextIds.Add(RequiredString(context, "ContextId"))) Fail("Duplicate chart context ID.");
                foreach (string field in new[] { "Description", "Unit", "QuantityName" }) _ = RequiredString(context, field);
                var labels = RequiredArray(context, "Labels").EnumerateArray().Select(x => x.GetString()).ToArray();
                var categoryIds = RequiredArray(context, "CategoryIds").EnumerateArray().Select(x => x.GetString()).ToArray();
                if (labels.Length != 3 || labels.Any(string.IsNullOrWhiteSpace) || labels.Distinct(StringComparer.Ordinal).Count() != labels.Length
                    || categoryIds.Length != labels.Length || categoryIds.Any(string.IsNullOrWhiteSpace)
                    || categoryIds.Distinct(StringComparer.Ordinal).Count() != categoryIds.Length)
                    Fail("Chart categories need three distinct labels and stable IDs in the same order.");
                CheckCapacity(context);
            }
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
            if (list.Key == "Foundation.NumberNames")
                for (int i = 0; i < list.Value.GetArrayLength(); i++)
                    if (list.Value[i].GetProperty("Value").GetInt32() != translated[i].GetProperty("Value").GetInt32())
                        Fail("Changed number vocabulary value.");
            if (ReviewedNarrativePhrasings.ListNames.Contains(list.Key))
                for (int i = 0; i < list.Value.GetArrayLength(); i++)
                    if (RequiredString(list.Value[i], "Id") != RequiredString(translated[i], "Id"))
                        Fail("Changed reviewed narrative ID.");
            if (ContextFields.ContainsKey(list.Key) || list.Key is "FractionQuantityContexts" or "FractionQuantityContexts.Extensions"
                or "DecimalStoryContexts" or "DecimalStoryContexts.Extensions" or "FractionQuantityActivities" or "DataChartContexts")
                for (int i = 0; i < list.Value.GetArrayLength(); i++)
                {
                    var originalRow = list.Value[i]; var translatedRow = translated[i];
                    foreach (string field in FixedContextFields)
                        if (originalRow.TryGetProperty(field, out var expected) &&
                            (!translatedRow.TryGetProperty(field, out var actual) || expected.GetRawText() != actual.GetRawText()))
                            Fail("Changed mathematical metadata: " + list.Key + "." + field);
                    foreach (string field in new[] { "Activity", "ConversionFactor", "MaximumQuantity", "GroupMaximum", "SmallUnit" })
                        if (originalRow.TryGetProperty(field, out var expected) &&
                            (!translatedRow.TryGetProperty(field, out var actual) || expected.GetRawText() != actual.GetRawText()))
                            Fail("Changed quantity activity metadata: " + list.Key + "." + field);
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
        [ElementaryQuizGenerator.MeasurementContextsList] = ["Id", "Category", "Subject"],
        [ElementaryQuizGenerator.DecimalContextsList] = ["Id", "Item", "Unit", "SmallUnit"],
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
    private static readonly string[] FixedContextFields = ["Id", "ContextId", "CategoryIds", "ShapeId", "Measurement", "Type", "Scenario", "Category",
        "RateProfile", "Kind", "Capacity", "MaximumSize", "MaximumDimension", "DistanceScale", "TimeDivisor",
        "EnglishOnly", "Money", "AsksForAdditionalPeople", "ConversionFactor", "MaximumQuantity", "Quantity"];

    static QuizContentValidation()
    {
        foreach (string key in new[] { "AverageQuizGenerator.DirectContexts", "AverageQuizGenerator.DistributionContexts",
            "AverageQuizGenerator.TwoGroupContexts", "PercentageQuizGenerator.Contexts", "PercentageQuizGenerator.PercentageStories",
            "MotionQuizGenerator.MovingSubjects", "ProportionQuizGenerator.Templates" })
            ContextFields.Add(key + ".Extensions", ContextFields[key]);
    }

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
