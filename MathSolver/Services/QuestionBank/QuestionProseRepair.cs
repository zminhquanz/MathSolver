namespace MathSolver.Services.QuestionBank;

/// <summary>Field-specific repair instructions from C# anchors, never from model prose.</summary>
internal static class QuestionProseRepair
{
    internal static string Instructions(BasicQuestionContract c, string? code, string? details)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(details)) return "";
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (c.Version == ReasoningStoryCatalogue.Version)
        {
            var lesson = ReasoningStoryCatalogue.Lesson(c);
            foreach (var fact in lesson.Facts) fields["facts." + fact.Role] = fact.Text;
            foreach (var step in lesson.Steps) fields["solution_leads." + step.Id] = step.Lead;
            fields["facts.role"] = string.Join(",", lesson.Facts.Select(f => f.Role));
            fields["solution_leads.step"] = string.Join(",", lesson.Steps.Select(s => s.Id));
            fields["question"] = lesson.Question;
        }
        else
        {
            var draft = c.Version == FindXQuestionCatalogue.Version ? FindXQuestionCatalogue.Draft(c)
                : c.Version == FractionQuestionCatalogue.Version ? FractionQuestionCatalogue.Draft(c)
                : c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.Draft(c)
                : BasicQuestionTemplates.Example(c);
            fields["given_a"] = draft.GivenA;
            fields["given_b"] = draft.GivenB;
            fields["question"] = draft.Question;
            fields["solution_lead"] = draft.SolutionLead ?? "";
            fields["unit_id"] = draft.UnitId ?? "";
        }
        var rejected = details.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal).Where(fields.ContainsKey).ToArray();
        if (rejected.Length == 0) return "";
        bool vi = c.Language == AppLanguage.Vietnamese;
        // Bound repair tokens. The full example is already present in the prompt;
        // identify all rejected fields, show at most two short semantic anchors.
        return "\n" + (vi ? "Sửa các trường: " : "Repair fields: ") + string.Join(", ", rejected)
            + (vi ? ". Khôi phục đúng vai trò, đơn vị và mục tiêu như các mốc sau; được diễn đạt tương đương:\n"
                : ". Restore roles, units and target using these anchors; equivalent wording is allowed:\n")
            + string.Join("\n", rejected.Take(2).Select(field => field + ": "
                + System.Text.Json.JsonSerializer.Serialize(Anchor(fields[field]),
                    new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })))
            + (vi ? "\nTrả lại toàn bộ JSON, không chỉ trường sửa." : "\nReturn the complete JSON, not only repaired fields.");
    }

    private static string Anchor(string text) => text.Length <= 220 ? text
        : text[..Math.Max(0, text.LastIndexOf(' ', 220))] + "...";
}
