using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    public static void CheckLists(QuizContentPack pack)
    {
        CheckMapScaleOpenLists(pack);
        CheckDiscountLists(pack);
        CheckQuadrilateralAreaLists(pack);
        CheckPercentageRatioLists(pack);
        CheckMoneyChangeLists(pack);
        CheckTriangleAreaLists(pack);
        CheckRectangleSideLists(pack);
        CheckRectangleAreaLists(pack);
        CheckRectanglePerimeterLists(pack);
        CheckDecimalOpenLists(pack);
        CheckFractionPartLists(pack);
        CheckFractionRemainingLists(pack);
        CheckFractionWholeLists(pack);
        CheckAverageOpenLists(pack);
        CheckAverageMissingLists(pack);
        CheckSumRatioLists(pack);
        CheckDifferenceRatioLists(pack);
        CheckUnitaryTotalLists(pack);
        CheckUnitaryGroupsLists(pack);
        CheckMotionOpenLists(pack);
        CheckMotionExtendedLists(pack);
        CheckSumDifferenceLists(pack);
        CheckPercentagePartLists(pack);
        CheckPercentageRemainingLists(pack);
        CheckPercentageWholeLists(pack);
        CheckSameDayTimeLists(pack);
        CheckSameDayBreakLists(pack);
        CheckOvernightTimeLists(pack);
        CheckSameMonthCalendarLists(pack);
        CheckTimeFractionLists(pack);
        CheckMixedTimeLists(pack);
        CheckDecimalSpatialLists(pack);
        CheckPracticeArithmeticLists(pack);
        CheckNumberNameLists(pack);
        CheckAreaMeasurementLists(pack);
        CheckContextActivityLists(pack);
        CheckReviewedNarrativeLists(pack);
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
        CheckFractionQuantityContextLists(pack);
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
        if (pack.Lists.TryGetValue("MapScaleContexts", out var maps))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var context in maps.EnumerateArray())
            {
                string id = RequiredString(context, "Id");
                if (!ids.Add(id) || context.GetProperty("MinimumStar").GetInt32() is < 1 or > 5
                    || context.GetProperty("MaximumStar").GetInt32() is < 1 or > 5
                    || context.GetProperty("MinimumStar").GetInt32() > context.GetProperty("MaximumStar").GetInt32()
                    || context.GetProperty("MaximumScale").GetInt32() < 100
                    || !pack.Texts.Any(t => t.Id == "ElementaryQuizGenerator.NewSkills.CreateMapScale.Context." + id))
                    Fail("Invalid map scale context or limits.");
            }
        }
    }

    private static readonly (string Template, string Alternatives)[] FractionQuantityTextFields =
    [
        ("PartProblemTemplate", "PartProblemAlternatives"),
        ("WholeProblemTemplate", "WholeProblemAlternatives")
    ];

    // Ignore only the two positional fact placeholders, as the original regex did.
    private static bool HasFractionQuantityDigits(ReadOnlySpan<char> text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (i + 2 < text.Length && text[i] == '{'
                && text[i + 1] is '0' or '1' && text[i + 2] == '}')
            {
                i += 2;
                continue;
            }
            if (char.IsDigit(text[i])) return true;
        }
        return false;
    }

    private static void CheckFractionQuantityContextLists(QuizContentPack pack)
    {
        if (pack.Lists.TryGetValue("FractionQuantityContexts", out var fractions))
        {
            var contextIds = new HashSet<string>(StringComparer.Ordinal);
            var fractionRows = fractions.EnumerateArray().AsEnumerable().Concat(
                pack.Lists.TryGetValue("FractionQuantityContexts.Extensions", out var fractionExtra)
                    ? fractionExtra.EnumerateArray().AsEnumerable() : []);
            foreach (var context in fractionRows)
            {
                string contextId = RequiredString(context, "ContextId");
                if (!Regex.IsMatch(contextId, @"^[a-z][a-z0-9-]*$") || !contextIds.Add(contextId))
                    Fail("Invalid or duplicate fraction context ID: " + contextId);
                if (RequiredString(context, "Quantity") is not ("Count" or "Mass" or "Distance" or "Capacity"))
                    Fail("Unsupported fraction quantity: " + contextId);
                foreach (string field in new[] { "Unit", "PartLabel", "WholeLabel" }) _ = RequiredString(context, field);
                foreach (var (field, alternativesField) in FractionQuantityTextFields)
                {
                    string template = RequiredString(context, field);
                    if (!Slots(template).SetEquals(["0", "1"])) Fail("Fraction quantity templates require exactly {0} and {1}.");
                    var alternatives = RequiredArray(context, alternativesField);
                    if (alternatives.GetArrayLength() < 2) Fail("Fraction quantity facts need reviewed alternatives.");
                    var seen = new HashSet<string>(StringComparer.Ordinal) { template.Trim() };
                    foreach (var alternative in alternatives.EnumerateArray())
                    {
                        if (alternative.ValueKind != JsonValueKind.String) Fail("Invalid fraction quantity phrasing.");
                        string text = alternative.GetString()!;
                        if (!seen.Add(text.Trim()) || ContainsControlCharacters(text) || text.Length > 700
                            || HasFractionQuantityDigits(text)) Fail("Invalid fraction quantity phrasing.");
                        if (!SameReviewedSentences(template, text)) Fail("Changed fraction quantity roles or target.");
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
    }

    private static void CheckContextActivityLists(QuizContentPack pack)
    {
        var textIds = new HashSet<string>(pack.Texts.Count, StringComparer.Ordinal);
        var proportionTexts = new Dictionary<string, QuizContentText>(64, StringComparer.Ordinal);
        foreach (var text in pack.Texts)
        {
            if (!textIds.Add(text.Id)) Fail("Duplicate content text ID: " + text.Id);
            if (text.Id.StartsWith("ProportionQuizGenerator.Narrative.", StringComparison.Ordinal))
                proportionTexts.Add(text.Id, text);
        }

        foreach (string key in new[] { "ProportionQuizGenerator.Templates", "ProportionQuizGenerator.Templates.Extensions" })
        if (pack.Lists.TryGetValue(key, out var proportionTemplates))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in proportionTemplates.EnumerateArray())
            {
                if (!row.TryGetProperty("NarrativeId", out _)) continue; // Legacy positional language packs.
                string id = RequiredString(row, "NarrativeId");
                if (!ids.Add(id) || !id.StartsWith("ProportionQuizGenerator.Narrative.", StringComparison.Ordinal)
                    || !proportionTexts.ContainsKey(id)) Fail("Invalid proportion narrative ID: " + id);
                var entry = proportionTexts[id];
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
                        if (!textIds.Contains("ElementaryQuizGenerator.DecimalStories.Activity." + activityId + "." + clause))
                            Fail("Incomplete decimal activity wording: " + activityId);
                }
            }
        }
        if (pack.Lists.TryGetValue(ElementaryQuizGenerator.MeasurementContextsList, out var measurementContexts))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var categories = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in measurementContexts.EnumerateArray().Concat(
                pack.Lists.TryGetValue(ElementaryQuizGenerator.MeasurementContextsList + ".Extensions", out var measuredExtra)
                    ? measuredExtra.EnumerateArray().AsEnumerable() : []))
            {
                if (!ids.Add(RequiredString(row, "Id"))) Fail("Duplicate measurement story context.");
                _ = RequiredString(row, "Subject");
                string category = RequiredString(row, "Category");
                if (category is not ("mass" or "capacity" or "length")) Fail("Invalid measurement story dimension.");
                categories.Add(category);
                if (row.TryGetProperty("Activity", out _))
                {
                    string activity = RequiredString(row, "Activity");
                    if (activity != RequiredString(row, "Id") || row.GetProperty("MaximumQuantity").GetInt32() < 20)
                        Fail("Invalid measurement activity limits.");
                    for (int clause = 1; clause <= 10; clause++)
                        if (!textIds.Contains("ElementaryQuizGenerator.MeasurementStories.Activity." + activity + "." + clause.ToString("000")))
                            Fail("Incomplete measurement activity: " + activity);
                }
            }
            if (categories.Count != 3) Fail("Missing measurement story dimension.");
        }
        foreach (string key in new[] { "ElementaryQuizGenerator.PackingStories", "ElementaryQuizGenerator.PackingStories.Extensions" })
        if (pack.Lists.TryGetValue(key, out var packing))
        foreach (var row in packing.EnumerateArray())
        {
            if (!row.TryGetProperty("Activity", out _)) continue;
            string id = RequiredString(row, "Id");
            int capacity = row.GetProperty("Capacity").GetInt32(), size = row.GetProperty("MaximumSize").GetInt32();
            if (RequiredString(row, "Activity") != id || size < 5 || capacity < size * 2 - 1)
                Fail("Invalid packing activity capacity.");
            foreach (int clause in Enumerable.Range(1, 11).Concat([18, 19]))
                if (!textIds.Contains("ElementaryQuizGenerator.ContextStories.CreateRemainderStory.Activity." + id + "." + clause.ToString("000")))
                    Fail("Incomplete packing activity: " + id);
        }
        if (pack.Lists.TryGetValue("TimeActivityContexts", out var timeActivities))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (timeActivities.GetArrayLength() == 0 || !timeActivities.EnumerateArray().Any(row => row.GetProperty("MinimumStar").GetInt32() == 1))
                Fail("Time activities need at least one introductory context.");
            foreach (var row in timeActivities.EnumerateArray())
            {
                string id = RequiredString(row, "Id");
                int minimum = row.GetProperty("MinimumStar").GetInt32(), first = row.GetProperty("StartHourMinimum").GetInt32(), last = row.GetProperty("StartHourMaximum").GetInt32();
                if (!ids.Add(id) || minimum is < 1 or > 5 || first < 0 || last > 23 || first > last
                    || last > 20 && minimum < 3 || row.GetProperty("MaximumHours").GetInt32() is < 1 or > 3)
                    Fail("Invalid time activity range.");
                foreach (string clause in ElementaryQuizGenerator.TimeActivityClauses)
                    if (!textIds.Contains("ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.Activity." + id + "." + clause))
                        Fail("Incomplete time activity: " + id);
            }
        }
    }

    private static void CheckReviewedNarrativeLists(QuizContentPack pack)
    {
        var texts = pack.Texts.ToDictionary(text => text.Id, StringComparer.Ordinal);
        foreach (string listName in ReviewedNarrativePhrasings.ListNames)
        if (pack.Lists.TryGetValue(listName, out var phrasings))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (phrasings.GetArrayLength() == 0) Fail("Empty reviewed narrative list.");
            foreach (var row in phrasings.EnumerateArray())
            {
                string id = RequiredString(row, "Id");
                string prefix = listName == ElementaryQuizGenerator.AppliedNarrativeList ? "AppliedNarrative."
                    : listName == ReviewedNarrativePhrasings.MotionListName ? "MotionQuizGenerator."
                    : listName == ReviewedNarrativePhrasings.DecimalSpatialListName ? "DecimalSpatial."
                    : listName == ReviewedNarrativePhrasings.TimeFractionListName ? "TimeFraction."
                    : listName == ReviewedNarrativePhrasings.TwoNumbersListName ? "ElementaryQuizGenerator."
                    : listName == ReviewedNarrativePhrasings.AverageListName ? "AverageQuizGenerator."
                    : listName == ReviewedNarrativePhrasings.PercentageListName ? "PercentageQuizGenerator."
                    : listName == ReviewedNarrativePhrasings.GeometryListName ? "GeometryQuizGenerator.Difficulty."
                    : listName == ReviewedNarrativePhrasings.DataListName ? "ElementaryQuizGenerator.DataDifficulty."
                    : listName == ReviewedNarrativePhrasings.MapScaleListName ? "ElementaryQuizGenerator.NewSkills.CreateMapScale."
                    : listName == ReviewedNarrativePhrasings.ProbabilityListName ? "ElementaryQuizGenerator.Probability"
                    : listName == ReviewedNarrativePhrasings.ProportionListName ? "ProportionQuizGenerator.Narrative."
                    : listName == ReviewedNarrativePhrasings.DecimalListName ? "ElementaryQuizGenerator.DecimalStories."
                    : listName == ReviewedNarrativePhrasings.MeasurementListName ? "ElementaryQuizGenerator.MeasurementStories."
                    : listName == ReviewedNarrativePhrasings.RemainderListName ? "ElementaryQuizGenerator.ContextStories.CreateRemainderStory."
                    : listName == ReviewedNarrativePhrasings.TimeListName ? "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty."
                    : listName == ReviewedNarrativePhrasings.FractionQuantityListName ? "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty."
                    : "ElementaryQuizGenerator.MultiStep.CreateMultiStep.";
                if (!ids.Add(id) || !id.StartsWith(prefix, StringComparison.Ordinal)
                    || !texts.ContainsKey(id)) Fail("Unknown or duplicate reviewed narrative: " + id);
                string originalText = texts[id].Text;
                string originalTrimmed = originalText.Trim();
                bool probability = listName == ReviewedNarrativePhrasings.ProbabilityListName;
                var distinct = new HashSet<string>(StringComparer.Ordinal);
                CheckReviewedChoices(RequiredArray(row, "Alternatives"), originalText, originalTrimmed, id, probability, distinct);
                if (row.TryGetProperty("AlternativesByUnit", out var byUnit))
                {
                    if (byUnit.ValueKind != JsonValueKind.Object) Fail("Invalid unit phrasing map: " + id);
                    var units = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entry in byUnit.EnumerateObject())
                    {
                        if (string.IsNullOrWhiteSpace(entry.Name) || !units.Add(entry.Name)
                            || entry.Value.ValueKind != JsonValueKind.Array || entry.Value.GetArrayLength() == 0)
                            Fail("Invalid unit phrasing choices: " + id);
                        // Each unit has its own uniqueness constraint; reuse the local set.
                        CheckReviewedChoices(entry.Value, originalText, originalTrimmed, id, probability, distinct);
                    }
                }
            }
        }
    }

    private static void CheckReviewedChoices(JsonElement choices, string original, string originalTrimmed,
        string id, bool probability, HashSet<string> distinct)
    {
        distinct.Clear();
        distinct.Add(originalTrimmed);
        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(choice.GetString()))
                Fail("Empty reviewed phrasing: " + id);
            string text = choice.GetString()!;
            if (!distinct.Add(text.Trim()) || ContainsControlCharacters(text) || text.Length > 700
                || (probability ? !SameReviewedNumbers(original, text) : HasVisibleDigits(text)))
                Fail("Invalid reviewed phrasing: " + id);
            if (!SameReviewedSentences(original.AsSpan(), text.AsSpan()))
                Fail("Changed ordered roles or target: " + id);
        }
    }

    private static bool ContainsControlCharacters(ReadOnlySpan<char> text)
    {
        foreach (char character in text)
            if (char.IsControl(character)) return true;
        return false;
    }

    // Regex.Split(original.Trim(), @"(?<=[.!?])\s+") without temporary arrays or strings.
    private static bool SameReviewedSentences(ReadOnlySpan<char> original, ReadOnlySpan<char> alternative)
    {
        original = original.Trim();
        alternative = alternative.Trim();
        while (true)
        {
            bool originalHasMore = NextReviewedSentence(ref original, out var originalSentence);
            bool alternativeHasMore = NextReviewedSentence(ref alternative, out var alternativeSentence);
            if (originalSentence.EndsWith('?') != alternativeSentence.EndsWith('?')
                || !SameReviewedSlots(originalSentence, alternativeSentence)) return false;
            if (originalHasMore != alternativeHasMore) return false;
            if (!originalHasMore) return true;
        }
    }

    private static bool NextReviewedSentence(ref ReadOnlySpan<char> remaining, out ReadOnlySpan<char> sentence)
    {
        for (int index = 1; index < remaining.Length; index++)
        {
            if (remaining[index - 1] is not ('.' or '!' or '?') || !char.IsWhiteSpace(remaining[index]))
                continue;
            int next = index + 1;
            while (next < remaining.Length && char.IsWhiteSpace(remaining[next])) next++;
            sentence = remaining[..index];
            remaining = remaining[next..];
            return true;
        }
        sentence = remaining;
        remaining = default;
        return false;
    }

    private static bool SameReviewedSlots(ReadOnlySpan<char> original, ReadOnlySpan<char> alternative)
    {
        var originalMatches = Placeholder().EnumerateMatches(original);
        var alternativeMatches = Placeholder().EnumerateMatches(alternative);
        while (true)
        {
            bool originalHasMatch = originalMatches.MoveNext();
            bool alternativeHasMatch = alternativeMatches.MoveNext();
            if (originalHasMatch != alternativeHasMatch) return false;
            if (!originalHasMatch) return true;
            var first = originalMatches.Current;
            var second = alternativeMatches.Current;
            if (!original.Slice(first.Index, first.Length).SequenceEqual(alternative.Slice(second.Index, second.Length)))
                return false;
        }
    }

    // Scans the characters visible after Regex.Replace(text, @"\{[A-Za-z0-9_.-]+\}", "").
    private static bool NextUnmaskedCharacter(ReadOnlySpan<char> text, ref int index, out char character)
    {
        while (index < text.Length)
        {
            if (text[index] == '{')
            {
                int end = index + 1;
                while (end < text.Length && IsReviewedSlotCharacter(text[end])) end++;
                if (end > index + 1 && end < text.Length && text[end] == '}')
                {
                    index = end + 1;
                    continue;
                }
            }
            character = text[index++];
            return true;
        }
        character = default;
        return false;
    }

    private static bool IsReviewedSlotCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '.' or '-';

    private static bool HasVisibleDigits(ReadOnlySpan<char> text)
    {
        int index = 0;
        while (NextUnmaskedCharacter(text, ref index, out char character))
            if (char.IsDigit(character)) return true;
        return false;
    }

    // The separator token distinguishes "12" from "1 2", including when a slot is removed between digits.
    private static int NextReviewedNumberToken(ReadOnlySpan<char> text, ref int index, ref bool inNumber)
    {
        while (NextUnmaskedCharacter(text, ref index, out char character))
        {
            if (char.IsDigit(character))
            {
                inNumber = true;
                return character;
            }
            if (inNumber)
            {
                inNumber = false;
                return -2;
            }
        }
        if (inNumber)
        {
            inNumber = false;
            return -2;
        }
        return -1;
    }

    private static bool SameReviewedNumbers(ReadOnlySpan<char> original, ReadOnlySpan<char> alternative)
    {
        int originalIndex = 0, alternativeIndex = 0;
        bool originalInNumber = false, alternativeInNumber = false;
        while (true)
        {
            int first = NextReviewedNumberToken(original, ref originalIndex, ref originalInNumber);
            int second = NextReviewedNumberToken(alternative, ref alternativeIndex, ref alternativeInNumber);
            if (first != second) return false;
            if (first == -1) return true;
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
                or "DecimalStoryContexts" or "DecimalStoryContexts.Extensions" or "FractionQuantityActivities" or "DataChartContexts" or "TimeActivityContexts" or "MapScaleContexts")
                for (int i = 0; i < list.Value.GetArrayLength(); i++)
                {
                    var originalRow = list.Value[i]; var translatedRow = translated[i];
                    foreach (string field in FixedContextFields)
                        if (originalRow.TryGetProperty(field, out var expected) &&
                            (!translatedRow.TryGetProperty(field, out var actual) || expected.GetRawText() != actual.GetRawText()))
                            Fail("Changed mathematical metadata: " + list.Key + "." + field);
                    foreach (string field in new[] { "Activity", "ConversionFactor", "MaximumQuantity", "GroupMaximum", "SmallUnit", "MinimumStar", "StartHourMinimum", "StartHourMaximum", "MaximumHours" })
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
        "EnglishOnly", "Money", "AsksForAdditionalPeople", "ConversionFactor", "MaximumQuantity", "Quantity",
        "RequiresObservationUnit", "MinimumStar", "MaximumStar", "MaximumScale"];

    static QuizContentValidation()
    {
        foreach (string key in new[] { "AverageQuizGenerator.DirectContexts", "AverageQuizGenerator.DistributionContexts",
            "AverageQuizGenerator.TwoGroupContexts", "PercentageQuizGenerator.Contexts", "PercentageQuizGenerator.PercentageStories",
            "MotionQuizGenerator.MovingSubjects", "ProportionQuizGenerator.Templates", "ElementaryQuizGenerator.PackingStories", ElementaryQuizGenerator.MeasurementContextsList })
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
