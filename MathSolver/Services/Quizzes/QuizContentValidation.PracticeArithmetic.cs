using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
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

    private static void CheckPracticeArithmeticLists(QuizContentPack pack)
    {
        if (!pack.Lists.ContainsKey("PracticeArithmeticContexts") && !pack.Lists.ContainsKey("PracticeMoneyItems")
            && !pack.Lists.ContainsKey("PracticeArithmeticTemplates")) return;
        foreach (string key in new[] { "PracticeArithmeticContexts", "PracticeMoneyItems", "PracticeArithmeticTemplates" })
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
                    var variables = Regex.Matches(problem, @"\{([^{}]+)\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
                        .Select(m => m.Groups[1].Value).ToHashSet();
                    if (!PracticeVariables.TryGetValue(id, out var expected) || !variables.SetEquals(expected))
                        throw new InvalidDataException("Invalid practice template roles: " + id);
                    if (id.EndsWith("-two", StringComparison.Ordinal) || id.EndsWith("-subtract-batches", StringComparison.Ordinal)) _ = RequiredString(row, "FirstLead");
                    if (id == "money-add")
                    {
                        string lead = RequiredString(row, "FirstLead");
                        var leadRoles = Regex.Matches(lead, @"\{([^{}]+)\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
                            .Select(m => m.Groups[1].Value).ToHashSet();
                        if (!leadRoles.SetEquals(new[] { "batch", "unit" }))
                            throw new InvalidDataException("Invalid purchase-step roles: " + id);
                    }
                }
            }
            if (key == "PracticeArithmeticTemplates" && !ids.SetEquals(PracticeVariables.Keys))
                throw new InvalidDataException("Incomplete practice arithmetic templates.");
        }
    }
}
