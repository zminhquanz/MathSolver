using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckAreaMeasurementLists(QuizContentPack pack)
    {
        if (!pack.Lists.TryGetValue(AreaMeasurementTemplates.ContextList, out var contexts)) return;
        if (!pack.Lists.TryGetValue(AreaMeasurementTemplates.VocabularyList, out var vocabulary)
            || vocabulary.GetArrayLength() != 1) Fail("Missing area quantity vocabulary.");
        string[] Terms(string field) => vocabulary[0].GetProperty(field).EnumerateArray()
            .Select(t => t.GetString() ?? "").ToArray();
        var areaTerms = Terms("AreaTerms");
        var otherTerms = Terms("OtherQuantityTerms");
        if (areaTerms.Length == 0 || otherTerms.Length == 0 || areaTerms.Concat(otherTerms).Any(string.IsNullOrWhiteSpace))
            Fail("Empty area quantity vocabulary.");
        bool Has(string text, string term) => Regex.IsMatch(text.Normalize(), @"(?<![\p{L}\p{M}])"
            + Regex.Escape(term.Normalize()) + @"(?![\p{L}\p{M}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        void CheckQuantityText(string text)
        {
            if (!areaTerms.Any(term => Has(text, term)) || otherTerms.Any(term => Has(text, term))
                || Regex.IsMatch(text, @"\p{Nd}")) Fail("Area prose changed the physical quantity or supplied a number.");
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (contexts.GetArrayLength() == 0) Fail("Empty area measurement contexts.");
        foreach (var row in contexts.EnumerateArray())
        {
            string id = RequiredString(row, "Id");
            if (!id.StartsWith("area-", StringComparison.Ordinal) || !ids.Add(id)) Fail("Invalid area measurement context ID.");
            string subject = RequiredString(row, "Subject");
            if (otherTerms.Any(term => Has(subject, term))) Fail("Area context describes another physical quantity.");
            if (!row.TryGetProperty("MaximumQuantity", out var maximum) || !maximum.TryGetInt32(out int cap) || cap < 4)
                Fail("Invalid area measurement quantity bounds.");
            try
            {
                if (MeasurementQuantityRules.Convert(1, "area", RequiredString(row, "LargeUnitId"), "area",
                    RequiredString(row, "SmallUnitId")) <= 1) Fail("Area units must run from larger to smaller.");
            }
            catch (ArgumentException) { Fail("A measurement unit belongs to a different quantity."); }
        }
        if (!pack.Lists.TryGetValue(AreaMeasurementTemplates.PhrasingList, out var phrases)) Fail("Missing area measurement prose.");
        var entries = pack.Texts.ToDictionary(t => t.Id, StringComparer.Ordinal);
        string[] required = ["InitialLarge", "InitialSmall", "InitialMixed", "Added", "Removed", "QuestionSmall", "QuestionLarge",
            "InitialConversionLead", "AddedConversionLead", "CombinedLead", "ResultLead", "AnswerLabel"];
        ids.Clear();
        foreach (var row in phrases.EnumerateArray())
        {
            string id = RequiredString(row, "Id");
            if (!required.Select(s => AreaMeasurementTemplates.Prefix + s).Contains(id) || !ids.Add(id)
                || !entries.TryGetValue(id, out var entry)) Fail("Invalid area prose ID.");
            var source = entries[id];
            CheckQuantityText(source.Text);
            foreach (var alternative in row.GetProperty("Alternatives").EnumerateArray())
            {
                string text = alternative.GetString() ?? "";
                CheckQuantityText(text);
                var variables = Regex.Matches(text, @"\{([a-z_]+)\}").Select(m => m.Groups[1].Value).ToArray();
                var expected = Regex.Matches(source.Text, @"\{([a-z_]+)\}").Select(m => m.Groups[1].Value).ToArray();
                if (text.Length == 0 || !variables.Order().SequenceEqual(expected.Order())) Fail("Area prose changed a quantity role.");
            }
        }
        if (ids.Count != required.Length) Fail("Incomplete area measurement clauses.");
    }
}
