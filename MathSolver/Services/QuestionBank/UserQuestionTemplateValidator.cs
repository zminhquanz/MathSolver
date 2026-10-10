using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal sealed record TemplateSyntaxIssue(string Field, string ErrorCode, string? Variable = null);

/// <summary>Checks substitution syntax for user-authored prose, without evaluating its meaning.</summary>
public static class UserQuestionTemplateValidator
{
    private static readonly Regex Variables = new(@"\{[^{}]*\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    internal static IEnumerable<(string Id, string Text)> Fields(BasicQuestionDraft draft)
    {
        if (draft.Facts is null)
        {
            yield return ("given_a", draft.GivenA);
            yield return ("given_b", draft.GivenB);
        }
        else foreach (var fact in draft.Facts) yield return ("facts." + fact.Role, fact.Text);
        yield return ("question", draft.Question);
        if (draft.SolutionLeads is null) yield return ("solution_lead", draft.SolutionLead ?? "");
        else foreach (var lead in draft.SolutionLeads) yield return ("solution_leads." + lead.Role, lead.Text);
    }

    internal static IReadOnlyList<TemplateSyntaxIssue> Inspect(BasicQuestionDraft draft, BasicQuestionContract c)
    {
        if (!c.IsValid || !c.IsTemplate) return [new("", "InvalidContract")];
        var example = BasicQuestionTemplates.Example(c);
        if (draft.UnitId != example.UnitId
            || !SameRoles(draft.Facts, example.Facts) || !SameRoles(draft.SolutionLeads, example.SolutionLeads))
            return [new("", "InvalidFields")];
        var expected = Fields(example).ToDictionary(f => f.Id, f => f.Text);
        string[] candidates = c.Version == ReasoningStoryCatalogue.Version
            ? ReasoningStoryCatalogue.Lesson(c).Quantities.Select(q => "{" + q.Id + "}").ToArray()
            : ["{name}", "{other}", "{a}", "{b}", "{unit}", "{group}", "{group_one}", "{groups}", "{unit_a}", "{unit_b}", "{part_a}", "{part_b}"];
        // Each catalogue has its own renderer. Never accept a variable that stays unresolved.
        var allowed = candidates.Where(token => {
            string rendered = BasicQuestionTemplates.Render(token, c);
            return rendered.Length > 0 && rendered != token;
        }).ToHashSet(StringComparer.Ordinal);
        var issues = new List<TemplateSyntaxIssue>();
        foreach (var (id, text) in Fields(draft))
        {
            if (string.IsNullOrWhiteSpace(text)) { issues.Add(new(id, "ExcelRequiredCell")); continue; }
            if (text.Length > 4000 || text.Any(ch => char.IsControl(ch) && ch is not '\r' and not '\n' and not '\t'))
                issues.Add(new(id, "InvalidText"));
            var actual = Variables.Matches(text).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
            if (Variables.Replace(text, "").IndexOfAny(['{', '}']) >= 0)
                issues.Add(new(id, "ExcelInvalidVariableSyntax"));
            foreach (string token in actual.Except(allowed)) issues.Add(new(id, "ExcelUnexpectedVariable", token));
            // Numeric fact slots must remain present. Names, units and wording may be
            // repeated, moved or written explicitly by the author; no phrase whitelist.
            if (id is "given_a" or "given_b" || id.StartsWith("facts.", StringComparison.Ordinal))
                foreach (string token in Variables.Matches(expected[id]).Select(m => m.Value).Distinct()
                    .Where(t => t is "{a}" or "{b}" || Regex.IsMatch(t, @"^\{f\d+\}$")))
                    if (!actual.Contains(token)) issues.Add(new(id, "ExcelMissingVariable", token));
        }
        return issues;
    }

    private static bool SameRoles(IReadOnlyList<NarrativeClause>? actual, IReadOnlyList<NarrativeClause>? expected)
        => expected is null ? actual is null : actual is not null
            && actual.All(a => a is not null) && actual.Select(a => a.Role).SequenceEqual(expected.Select(e => e.Role));

    public static BasicDraftValidation Validate(string json, BasicQuestionContract c)
    {
        if (!c.IsValid || !c.IsTemplate) return new(null, "InvalidContract");
        if (string.IsNullOrWhiteSpace(json) || json.Length > 256 * 1024) return new(null, "InvalidJson");
        try
        {
            using var document = JsonDocument.Parse(json, new() { MaxDepth = 8 });
            var root = document.RootElement;
            bool story = c.Version == ReasoningStoryCatalogue.Version;
            string[] keys = story ? ["facts", "question", "solution_leads"] : ["given_a", "given_b", "question", "solution_lead", "unit_id"];
            if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(keys.Order()))
                return new(null, "InvalidFields");
            string Text(JsonElement item, string key) => item.GetProperty(key).GetString() ?? throw new JsonException();
            NarrativeClause[] Clauses(string key, string roleKey)
            {
                var array = root.GetProperty(key);
                if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 32) throw new JsonException();
                return array.EnumerateArray().Select(item => {
                    if (item.ValueKind != JsonValueKind.Object || !item.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { roleKey, "text" }.Order()))
                        throw new JsonException();
                    return new NarrativeClause(Text(item, roleKey), Text(item, "text"));
                }).ToArray();
            }
            var draft = story ? new BasicQuestionDraft("", "", Text(root, "question"), "", "story") {
                Facts = Clauses("facts", "role"), SolutionLeads = Clauses("solution_leads", "step") }
                : new BasicQuestionDraft(Text(root, "given_a"), Text(root, "given_b"), Text(root, "question"), Text(root, "solution_lead"), Text(root, "unit_id"));
            var issue = Inspect(draft, c).FirstOrDefault();
            return issue is null ? new(draft, null, c) : new(null, issue.ErrorCode, ErrorDetails: issue.Field);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or RegexMatchTimeoutException)
        { return new(null, "InvalidJson"); }
    }
}
