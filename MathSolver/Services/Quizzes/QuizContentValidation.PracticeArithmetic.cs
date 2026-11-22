using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static readonly string[] PracticeListKeys =
        ["PracticeArithmeticContexts", "PracticeMoneyItems", "PracticeArithmeticTemplates"];
    private static readonly string[] PurchaseStepRoles = ["batch", "unit"];

    private static readonly IReadOnlyDictionary<string, string[]> PracticeVariables = new Dictionary<string, string[]>
    {
        ["count-add"] = ["actor", "a", "b", "unit"],
        ["count-subtract"] = ["actor", "a", "b", "unit"],
        ["count-multiply"] = ["actor", "a", "b", "unit", "group"],
        ["count-divide"] = ["actor", "a", "b", "unit", "group"],
        ["count-add-two"] = ["actor", "initial", "change", "b", "unit"],
        ["count-subtract-two"] = ["actor", "initial", "change", "b", "unit"],
        ["money-add"] = ["actor", "items", "count", "price", "second_price", "unit"],
        ["money-subtract"] = ["actor", "item", "a", "b", "unit"],
        ["money-subtract-two"] = ["actor", "items", "a", "price", "unit"],
        ["money-subtract-batches"] = ["actor", "items", "count", "a", "price", "unit"],
        ["money-multiply"] = ["actor", "items", "count", "price", "unit"],
        ["money-divide"] = ["actor", "items", "count", "cost", "unit"],
        ["population-add"] = ["actor", "a", "b", "unit"],
        ["population-subtract"] = ["actor", "a", "b", "unit"],
        ["population-multiply"] = ["actor", "a", "b", "unit", "group"],
        ["population-divide"] = ["actor", "a", "b", "unit", "group"],
        ["production-add"] = ["actor", "a", "b", "unit"],
        ["production-subtract"] = ["actor", "a", "b", "unit"],
        ["production-multiply"] = ["actor", "a", "b", "unit", "group"],
        ["production-divide"] = ["actor", "a", "b", "unit", "group"],
        ["budget-subtract"] = ["actor", "item", "a", "b", "unit"],
        ["budget-subtract-two"] = ["actor", "items", "a", "price", "unit"],
        ["budget-subtract-batches"] = ["actor", "items", "count", "a", "price", "unit"]
    };
    private static readonly int MaxPracticeRoleCount = PracticeVariables.Values.Max(static roles => roles.Length);

    private static void CheckPracticeArithmeticLists(QuizContentPack pack)
    {
        if (!pack.Lists.ContainsKey("PracticeArithmeticContexts") && !pack.Lists.ContainsKey("PracticeMoneyItems")
            && !pack.Lists.ContainsKey("PracticeArithmeticTemplates")) return;
        Span<int> baselineCounts = stackalloc int[MaxPracticeRoleCount];
        Span<int> alternativeCounts = stackalloc int[MaxPracticeRoleCount];
        foreach (string key in PracticeListKeys)
        {
            if (!pack.Lists.TryGetValue(key, out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
                throw new InvalidDataException("Missing practice list: " + key);
            var ids = new HashSet<string>();
            foreach (var row in list.EnumerateArray())
            {
                string id = RequiredString(row, "Id");
                if (!ids.Add(id)) throw new InvalidDataException("Duplicate practice ID: " + id);
                if (key == "PracticeMoneyItems")
                {
                    PracticeArithmeticStoryGenerator.Validate(row.Deserialize<PracticeMoneyItem>()
                        ?? throw new InvalidDataException("Invalid practice item: " + id));
                }
                else if (key == "PracticeArithmeticContexts")
                {
                    _ = RequiredString(row, "Actor"); _ = RequiredString(row, "Unit"); _ = RequiredString(row, "GroupUnit");
                    if (!Enum.TryParse<MathSolver.Models.QuestionKnowledgeGroup>(RequiredString(row, "Group"), out var group)
                        || group == MathSolver.Models.QuestionKnowledgeGroup.Money || !PracticeArithmeticStoryGenerator.Supports(group)
                        || row.GetProperty("Capacity").GetInt32() is < 2 or > 999_999_999
                        || row.GetProperty("MaximumPerGroup").GetInt32() < 1
                        || row.GetProperty("MaximumPerGroup").GetInt32() > row.GetProperty("Capacity").GetInt32())
                        throw new InvalidDataException("Invalid practice context: " + id);
                    string prefix = row.TryGetProperty("TemplatePrefix", out var family) ? family.GetString() ?? "" : "count";
                    if (prefix is not ("count" or "population" or "production")
                        || prefix == "population" && group != MathSolver.Models.QuestionKnowledgeGroup.Data
                        || prefix == "production" && group != MathSolver.Models.QuestionKnowledgeGroup.Production)
                        throw new InvalidDataException("Practice context uses the wrong prose family: " + id);
                    if (row.TryGetProperty("AllowsMentalLargeNumbers", out var mental)) _ = mental.GetBoolean();
                }
                else
                {
                    string problem = RequiredString(row, "Problem"); _ = RequiredString(row, "Lead");
                    if (!PracticeVariables.TryGetValue(id, out var expected))
                        throw new InvalidDataException("Invalid practice template roles: " + id);
                    if (!CountPracticeRoles(problem, expected, baselineCounts[..expected.Length]))
                        throw new InvalidDataException("Invalid practice template roles: " + id);
                    if (row.TryGetProperty("AlternativeProblems", out var alternatives))
                    {
                        if (alternatives.ValueKind != JsonValueKind.Array || alternatives.GetArrayLength() is < 1 or > 8)
                            throw new InvalidDataException("Invalid reviewed practice alternatives: " + id);
                        foreach (var alternative in alternatives.EnumerateArray())
                        {
                            string text = alternative.GetString() ?? "";
                            bool valid = CountPracticeRoles(text, expected, alternativeCounts[..expected.Length]);
                            for (int index = 0; valid && index < expected.Length; index++)
                            {
                                if (expected[index] is not ("actor" or "unit" or "group" or "item" or "items")
                                    && baselineCounts[index] != alternativeCounts[index])
                                    valid = false;
                            }
                            if (!valid || !text.EndsWith('?') || !text.Contains(". ") || text.Length > 1200)
                                throw new InvalidDataException("Reviewed alternative changed roles: " + id);
                        }
                    }
                    if (id.EndsWith("-two", StringComparison.Ordinal) || id.EndsWith("-subtract-batches", StringComparison.Ordinal)) _ = RequiredString(row, "FirstLead");
                    if (id == "money-add")
                    {
                        string lead = RequiredString(row, "FirstLead");
                        if (!CountPracticeRoles(lead, PurchaseStepRoles, alternativeCounts[..PurchaseStepRoles.Length]))
                            throw new InvalidDataException("Invalid purchase-step roles: " + id);
                    }
                }
            }
            if (key == "PracticeArithmeticTemplates" && !ids.SetEquals(PracticeVariables.Keys))
                throw new InvalidDataException("Incomplete practice arithmetic templates.");
        }
    }

    // Regex captures are inspected directly. Each expected role must occur, and fact counts
    // can be compared between the base and alternative templates without temporary strings.
    private static bool CountPracticeRoles(ReadOnlySpan<char> text, ReadOnlySpan<string> expected, Span<int> counts)
    {
        counts.Clear();
        foreach (var match in PracticePlaceholderPattern().EnumerateMatches(text))
        {
            ReadOnlySpan<char> role = text.Slice(match.Index + 1, match.Length - 2);
            int index = 0;
            for (; index < expected.Length; index++)
                if (role.SequenceEqual(expected[index])) break;
            if (index == expected.Length) return false;
            counts[index]++;
        }
        foreach (int count in counts)
            if (count == 0) return false;
        return true;
    }

    [GeneratedRegex(@"\{([^{}]+)\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PracticePlaceholderPattern();
}
