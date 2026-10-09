using System.Text.Json;
using MathSolver.Services;

namespace MathSolver.Services.QuestionBank;

public static class ReasoningStoryValidator
{
    public static BasicDraftValidation Validate(string raw, BasicQuestionContract c)
    {
        if (!c.IsValid) return new(null, "InvalidContract");
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 12000) return new(null, "InvalidJson");
        try
        {
            using var document = JsonDocument.Parse(raw, new() { MaxDepth = 8 });
            var root = document.RootElement;
            string[] fields = ["facts", "question", "solution_leads"];
            if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(fields.Order())
                || root.GetProperty("question").ValueKind != JsonValueKind.String) return new(null, "InvalidFields");
            NarrativeClause[] Read(string field, string id)
            {
                var array = root.GetProperty(field);
                if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 32) throw new JsonException();
                return array.EnumerateArray().Select(item =>
                {
                    if (item.ValueKind != JsonValueKind.Object || !item.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { id, "text" }.Order())
                        || item.GetProperty(id).ValueKind != JsonValueKind.String || item.GetProperty("text").ValueKind != JsonValueKind.String) throw new JsonException();
                    return new NarrativeClause(item.GetProperty(id).GetString()!, item.GetProperty("text").GetString()!);
                }).ToArray();
            }
            var draft = new BasicQuestionDraft("", "", root.GetProperty("question").GetString()!, "", "story")
            { Facts = Read("facts", "role"), SolutionLeads = Read("solution_leads", "step") };
            var lesson = ReasoningStoryCatalogue.Lesson(c);
            if (!draft.Facts!.Select(f => f.Role).SequenceEqual(lesson.Facts.Select(f => f.Role))
                || !draft.SolutionLeads!.Select(s => s.Role).SequenceEqual(lesson.Steps.Select(s => s.Id)))
                return new(null, "ChangedRelationOrTarget", ErrorDetails: "facts.role, solution_leads.step");
            string? languageError = QuestionProseLanguage.ValidateAndNormalize(draft, c.Language, out draft);
            if (languageError is not null) return new(null, languageError);
            bool Matches(string text, IEnumerable<string> examples) => examples.Any(example =>
                SemanticProseRules.Matches(text, [example], c.Language)
                // '$' is a currency unit in English price facts, not punctuation.
                && (c.Family != BankQuestionFamily.Proportion || text.Count(ch => ch == '$') == example.Count(ch => ch == '$')));
            var mismatches = draft.Facts!.Zip(lesson.Facts)
                .Where(pair => !Matches(pair.First.Text, lesson.FactPhrasings[pair.Second.Role]))
                .Select(pair => "facts." + pair.First.Role).ToList();
            if (!Matches(draft.Question, lesson.QuestionPhrasings)) mismatches.Add("question");
            mismatches.AddRange(draft.SolutionLeads!.Zip(lesson.Steps)
                .Where(pair => !Matches(pair.First.Text, lesson.LeadPhrasings[pair.Second.Id]))
                .Select(pair => "solution_leads." + pair.First.Role));
            if (mismatches.Count > 0) return new(null, "ChangedRelationOrTarget", ErrorDetails: string.Join(", ", mismatches));
            return new(draft, null, c);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        { return new(null, "InvalidJson"); }
    }

    public static string Prompt(BasicQuestionContract c, string? correction, IReadOnlySet<string>? excluded = null)
    {
        var lesson = ReasoningStoryCatalogue.Lesson(c);
        bool vi = c.Language == AppLanguage.Vietnamese;
        if (ReasoningStoryCatalogue.UsesReviewedPhrasings(c.Family))
        {
            var example = ReviewedReasoningProse.NovelDrafts(c, excluded ?? new HashSet<string>()).FirstOrDefault()
                ?? ReasoningStoryCatalogue.Draft(c);
            return (vi ? "Viết MẪU bài toán có lời văn bằng tiếng Việt, dùng cách diễn đạt tương đương đã duyệt. C# sinh số và tính đáp án. Giữ nguyên từng role, step, biến {f...}/{v...}, đơn vị, thứ tự sự việc và đối tượng hỏi. Không điền số hay tên vào biến; không thêm sự kiện, điều kiện hoặc phép tính. Chỉ trả JSON đầy đủ như ví dụ, không giải bài. Khi mẫu cũ đã có, đổi câu dữ kiện theo lựa chọn hợp lệ; chỉ đổi câu dẫn lời giải không tạo đề mới."
                : "Write a word-problem TEMPLATE in English using reviewed equivalent wording. C# generates numbers and calculates answers. Keep every role, step, literal {f...}/{v...} placeholder, unit, chronology and target. Do not fill variables, add events, conditions or formulas. Return only complete JSON as shown; do not solve. If wording already exists, choose different approved fact clauses; changing only solution leads is not novel.")
                + "\n" + c.Family + "; stars=" + (int)c.Tier + "; context=" + lesson.Context
                + (c.Family != BankQuestionFamily.FractionQuantity ? "" : vi
                    ? "\nKhông đổi tìm phần thành tìm toàn bộ. Phân số của lượng ban đầu khác phân số của phần còn lại sau lần dùng thứ nhất; giữ đúng lượng làm gốc ở từng câu."
                    : "\nDo not switch finding a part with finding the whole. A fraction of the initial amount differs from a fraction of the remainder after the first use; preserve the exact base of each fraction.")
                + "\n" + QuestionBankStore.SerializeDraft(example)
                + (correction is null ? "" : "\nRejected: " + correction + ". Return complete corrected JSON.");
        }
        return (vi ? "Viết lại MẪU đề và câu dẫn lời giải bằng tiếng Việt tự nhiên. C# chọn số và tính đáp án. Giữ nguyên biến, thứ tự vai trò, đơn vị, quan hệ, đối tượng hỏi và thứ tự sự việc. Được thay từ đồng nghĩa và từ nối trung tính; không thêm dữ kiện, nhân vật, điều kiện, số hay phép tính. Mỗi câu ngắn; không giải bài."
            : "Rewrite this word-problem TEMPLATE and its solution leads in natural English. C# owns numbers and calculations. Preserve placeholders, ordered fact roles, units, relationships, target and chronology. Use equivalent synonyms or neutral linking words; no extra facts, actors, conditions, numbers or formulas. Keep clauses short; do not solve.")
            + (vi ? "\nĐây là tình huống cụ thể, không viết thành mô tả trừu tượng: giữ nguyên tên nhóm và đơn vị trong ví dụ, không thay bằng ‘đối tượng’ hay ‘đơn vị’. Chỉ thêm từ nối trung tính hoặc dùng các từ tương đương: có/sở hữu, tổng/tổng cộng/tất cả, trung bình/trung bình cộng, sau đó/tiếp theo. Giữ nguyên các từ chính còn lại và thứ tự biến."
                : "\nThis is a concrete situation, not an abstract description: keep the exact group names and units from the example, never replace them with 'object' or 'unit'. Only add neutral linking words or use these equivalent terms: has/owns/possesses, total/in total/altogether, average/mean, each/every. Keep the remaining main words and placeholder order.")
            + "\n" + c.Family + "; stars=" + (int)c.Tier + "; context=" + lesson.Context
            + (vi ? "\nGiữ nguyên chữ và dấu ngoặc của mọi biến {f...}, {v...} trong JSON. Không điền tên, đơn vị hay số vào biến: C# sẽ thay chúng sau."
                : "\nKeep every {f...} and {v...} placeholder literally, including braces. Never fill in names, units or numbers: C# substitutes them later.")
            + "\nRoles: " + string.Join("; ", lesson.Quantities.Select(q => "{" + q.Id + "}=" + q.Role))
            + "\nReturn only JSON following this example:\n" + QuestionBankStore.SerializeDraft(ReasoningStoryCatalogue.Draft(c))
            + (correction is null ? "" : "\nRejected: " + correction + ". Rewrite the full JSON.");
    }

    internal static string Grammar(BasicQuestionContract c, IReadOnlySet<string>? excluded = null)
    {
        var lesson = ReasoningStoryCatalogue.Lesson(c);
        static string L(string text) => JsonSerializer.Serialize(text);
        string Object(string id, string role, string field) => "\"{\" ws " + L("\"" + id + "\"") + " ws \":\" ws "
            + L("\"" + role + "\"") + " ws \",\" ws " + L("\"text\"") + " ws \":\" ws " + field.Replace('_', '-') + " ws \"}\"";
        if (ReasoningStoryCatalogue.UsesReviewedPhrasings(c.Family))
        {
            // Bound the native grammar size. All branches are complete, novel
            // fact/question combinations, so a model cannot retry the old prose
            // by changing only punctuation, names, numbers or solution leads.
            var candidates = ReviewedReasoningProse.NovelDrafts(c, excluded ?? new HashSet<string>()).Take(8).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("DuplicateProseRetriesExhausted");
            // The inner string is JSON output, the outer string is GBNF. Emit
            // accented letters directly instead of forcing six-character JSON
            // escapes that waste the model's output budget on Vietnamese prose.
            var outputOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            string Text(string value) => L(JsonSerializer.Serialize(value, outputOptions));
            string heads = string.Join(" | ", candidates.Select((_, i) => "body-" + i));
            string bodies = string.Concat(candidates.Select((draft, i) => "body-" + i + " ::= "
                + L("\"facts\"") + " ws \":\" ws \"[\" "
                + string.Join(" ws \",\" ws ", draft.Facts!.Select(f => "\"{\" ws " + L("\"role\"")
                    + " ws \":\" ws " + L("\"" + f.Role + "\"") + " ws \",\" ws " + L("\"text\"")
                    + " ws \":\" ws " + Text(f.Text) + " ws \"}\""))
                + " ws \"]\" ws \",\" ws " + L("\"question\"") + " ws \":\" ws " + Text(draft.Question) + "\n"));
            return "root ::= \"{\" ws (" + heads + ") ws \",\" ws " + L("\"solution_leads\"") + " ws \":\" ws \"[\" "
                + string.Join(" ws \",\" ws ", lesson.Steps.Select(s => Object("step", s.Id, s.Id))) + " ws \"]\" ws \"}\" ws\n"
                + bodies + string.Concat(lesson.Steps.Select(s => SemanticProseRules.ClauseRule(s.Id, lesson.LeadPhrasings[s.Id], c.Language)))
                + "\nws ::= [ \\t\\n\\r]*\n";
        }
        return "root ::= \"{\" ws \"\\\"facts\\\"\" ws \":\" ws \"[\" "
            + string.Join(" ws \",\" ws ", lesson.Facts.Select(f => Object("role", f.Role, f.Role)))
            + " ws \"]\" ws \",\" ws \"\\\"question\\\"\" ws \":\" ws question ws \",\" ws \"\\\"solution_leads\\\"\" ws \":\" ws \"[\" "
            + string.Join(" ws \",\" ws ", lesson.Steps.Select(s => Object("step", s.Id, s.Id))) + " ws \"]\" ws \"}\" ws\n"
            + string.Concat(lesson.Facts.Select(f => SemanticProseRules.ClauseRule(f.Role, lesson.FactPhrasings[f.Role], c.Language)))
            + SemanticProseRules.ClauseRule("question", lesson.QuestionPhrasings, c.Language)
            + string.Concat(lesson.Steps.Select(s => SemanticProseRules.ClauseRule(s.Id, lesson.LeadPhrasings[s.Id], c.Language)))
            + "\nws ::= [ \\t\\n\\r]*\n";
    }
}
