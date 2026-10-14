using MathSolver.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Bounded pilot: open surface language, immutable C# facts, conservative verification.</summary>
public static class OpenQuestionProse
{
    private static readonly Regex Slots = QuestionTextRegex.Create(@"\{[a-zA-Z0-9_.-]+\}");
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static bool Supports(BasicQuestionContract c) => c.IsTemplate && (
        c.Version == ExpandedArithmeticCatalogue.Version && c.Practice?.StepCount == 1
        || c.Version is AdditionQuestionCatalogue.Version or ArithmeticQuestionCatalogue.Version
        || c.Version == FindXQuestionCatalogue.Version
        || c.Version == AppliedQuestionCatalogue.Version && AppliedQuestionCatalogue.Reasoning(c) is null
        || c.Version == ReasoningStoryCatalogue.Version && c.Family == BankQuestionFamily.Measurement
            && (ElementaryQuizType)c.BankVariant is ElementaryQuizType.LengthConversion or ElementaryQuizType.MassConversion
                or ElementaryQuizType.CapacityConversion or ElementaryQuizType.AreaConversion or ElementaryQuizType.VolumeConversion);

    internal static BasicQuestionDraft Example(BasicQuestionContract c) => c.Version == ExpandedArithmeticCatalogue.Version
        ? ExpandedArithmeticCatalogue.Draft(c) : c.Version == ReasoningStoryCatalogue.Version ? ReasoningStoryCatalogue.Draft(c)
        : c.Version == FindXQuestionCatalogue.Version ? FindXQuestionCatalogue.Draft(c)
        : c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.Draft(c) : OneStepQuestionCatalogue.Draft(c);

    internal static IReadOnlyList<BasicQuestionDraft> Examples(BasicQuestionContract c) => c.Version == ExpandedArithmeticCatalogue.Version
        ? ExpandedArithmeticCatalogue.Drafts(c) : c.Version == ReasoningStoryCatalogue.Version
            ? [Example(c),..ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Take(7)]
            : [Example(c), c.Version == FindXQuestionCatalogue.Version ? FindXQuestionCatalogue.Draft(c, 1)
                : c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.Draft(c, 1) : OneStepQuestionCatalogue.Draft(c, 1)];

    internal static (string Id, string Text)[] Fields(BasicQuestionDraft draft) => UserQuestionTemplateValidator.Fields(draft).ToArray();

    public static BasicDraftValidation ValidateSyntax(string raw, BasicQuestionContract c)
    {
        if (!c.IsValid || !Supports(c)) return new(null, "InvalidContract");
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 12000) return new(null, "InvalidJson");
        try
        {
            var example = Example(c);
            using var json = JsonDocument.Parse(raw, new() { MaxDepth = 8 });
            bool structured = example.Facts is not null;
            string[] keys = structured ? ["facts", "question", "solution_leads"] : ["given_a", "given_b", "question", "solution_lead", "unit_id"];
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(keys.Order()))
                return new(null, "InvalidFields");
            string Text(JsonElement value, string key) => value.GetProperty(key).ValueKind == JsonValueKind.String
                ? value.GetProperty(key).GetString()! : throw new JsonException();
            NarrativeClause[] Clauses(string key, string role)
            {
                var value = root.GetProperty(key);
                if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 1 or > 32) throw new JsonException();
                return value.EnumerateArray().Select(item =>
                {
                    if (item.ValueKind != JsonValueKind.Object || !item.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { role, "text" }.Order())) throw new JsonException();
                    return new NarrativeClause(Text(item, role), Text(item, "text"));
                }).ToArray();
            }
            var draft = structured ? new BasicQuestionDraft("", "", Text(root, "question"), example.SolutionLead, example.UnitId)
                { Facts = Clauses("facts", "role"), SolutionLeads = Clauses("solution_leads", "step") }
                : new(Text(root, "given_a"), Text(root, "given_b"), Text(root, "question"), Text(root, "solution_lead"), Text(root, "unit_id"));
            if (structured) draft = draft with { SolutionLead = draft.SolutionLeads![^1].Text };
            if (draft.UnitId != example.UnitId) return new(null, "ChangedUnits");
            if (!Fields(draft).Select(f => f.Id).SequenceEqual(Fields(example).Select(f => f.Id))) return new(null, "InvalidFields");
            string? languageError = QuestionProseLanguage.ValidateAndNormalize(draft, c.Language, out draft);
            if (languageError is not null) return new(null, languageError);
            foreach (var (actual, expected) in Fields(draft).Zip(Fields(example)))
            {
                string text = actual.Text;
                if (string.IsNullOrWhiteSpace(text) || text.Length > 700 || text.Any(char.IsControl)) return new(null, "InvalidText", ErrorDetails: actual.Id);
                string bare = Slots.Replace(text, "");
                if (bare.IndexOfAny(['{', '}']) >= 0 || !Slots.Matches(text).Select(m => m.Value).Order()
                    .SequenceEqual(Slots.Matches(expected.Text).Select(m => m.Value).Order()))
                    return new(null, "InvalidPlaceholders", ErrorDetails: actual.Id);
                if (bare.Any(char.IsDigit) || bare.IndexOfAny(['=', '<', '>', '+', '*', '/', '×', '÷', '%']) >= 0)
                    return new(null, "ChangedQuantities", ErrorDetails: actual.Id);
                if(text.Count(ch=>ch=='$')!=expected.Text.Count(ch=>ch=='$'))return new(null,"ChangedUnits",ErrorDetails:actual.Id);
                if(Regex.IsMatch(bare,@"\b(?:given_a|given_b|solution_lead|unit_id|solution_leads)\b",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
                    return new(null,"InvalidText",ErrorDetails:actual.Id);
            }
            return new(draft, null, c);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException)
        { return new(null, "InvalidJson"); }
    }

    public static BasicDraftValidation Validate(string raw, BasicQuestionContract c)
    {
        var syntax = ValidateSyntax(raw, c);
        if (!syntax.IsValid) return syntax;
        // Voice markers such as Vietnamese "được tặng" carry mathematical
        // direction. Check them before legacy neutral-link normalization.
        if(OpenProseMeaning.HasContradiction(syntax.Draft!,Example(c),c.Language))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Event direction or condition changed.");
        var legacy = BasicQuestionValidator.Validate(raw, c with { OpenProse = false });
        if (legacy.IsValid) return syntax;
        var draft = syntax.Draft!;
        var examples = Examples(c);
        var mismatched = Fields(draft).Where(field => !examples.Any(example =>
            OpenProseMeaning.Matches(field.Text, Fields(example).Single(f => f.Id == field.Id).Text, c.Language))).Select(f => f.Id).ToArray();
        if (mismatched.Length == 0) return syntax;
        if (OpenProseMeaning.HasContradiction(draft, Example(c), c.Language))
            return new(null, "ChangedRelationOrTarget", ErrorDetails: string.Join(", ", mismatched));
        return new(draft, "SemanticReviewRequired", c,
            "Automatic meaning verification is inconclusive: " + string.Join(", ", mismatched));
    }

    public static string Prompt(BasicQuestionContract c, string? correction = null, IReadOnlyList<string>? duplicates = null, string? details = null)
    {
        if (!Supports(c)) throw new ArgumentException("Unsupported open prose profile.");
        bool vi = c.Language == AppLanguage.Vietnamese;
        string instruction = vi ? """
            Viết đề mới, tự nhiên, phù hợp tình huống. Ví dụ chỉ tham khảo, không bắt buộc sao chép câu chữ.
            Được đổi cấu trúc câu, cách mở đầu, từ nối, chủ động/bị động. Giữ nguyên chủ thể, vai trò các lượng, đơn vị, quan hệ, thứ tự sự việc và đại lượng cần tìm.
            Giữ nguyên từng biến trong đúng trường, kể cả biến đơn vị; không điền tên hay số, không thêm điều kiện, không tiết lộ đáp án. Câu dẫn lời giải chỉ gọi đại lượng cần tìm.
            Không đổi loại đồ vật hoặc thêm chủ thể. Không viết tên trường JSON hay chỉ dẫn vào câu văn. Mỗi trường chỉ một câu ngắn, tối đa 24 từ.
            Chỉ trả JSON theo cấu trúc ví dụ, giữ role/step/unit_id. Không giải bài.
            """ : """
            Write a NEW natural word problem for the supplied situation. Examples are references, not mandatory wording.
            Vary sentence structure, openings, links and active/passive voice. Preserve owners, quantity roles, units, relationships, chronology and the requested quantity.
            Preserve every placeholder in its field, including units. Do not fill names or numbers, add conditions, solve or reveal answers. Solution leads name only the target.
            Do not change the kind of objects or add actors. Never put JSON field names or instructions inside the prose. Use one short sentence per field, at most 24 words.
            Return only JSON with the example's schema and role/step/unit_id values.
            """;
        string relation = c.Family == BankQuestionFamily.FindX
            ? "unknown=" + c.UnknownRole + "; given_a="+FindXQuestionCatalogue.Find(c.SceneId)!.KnownARole+"; given_b="+FindXQuestionCatalogue.Find(c.SceneId)!.KnownBRole+"; target=" + FindXQuestionCatalogue.Find(c.SceneId)!.TargetRole
            : c.Version == ReasoningStoryCatalogue.Version ? string.Join(", ", ReasoningStoryCatalogue.Lesson(c).Quantities.Select(q => "{" + q.Id + "}=" + q.Role))
            : c.Version==ExpandedArithmeticCatalogue.Version ? "operation="+c.Operation+"; one-step target; use the reference's actual event and quantity roles"
            : "operation=" + c.Operation + "; structure=" + c.Structure+"; "+(c.Structure switch
            {
                BasicQuestionStructure.Combine => "a: separate stock owned by name; b: separate stock owned by other; target: combined stock. No transfer between owners.",
                BasicQuestionStructure.Increase => "a: initial stock of name; b: added to the same stock; target: final stock.",
                BasicQuestionStructure.Remaining => "a: initial stock of name; b: removed from that stock; target: stock left.",
                BasicQuestionStructure.EqualGroups => "a: items per equal group; b: number of groups; target: total items.",
                BasicQuestionStructure.EqualShare => "a: total items; b: group count; target: items per equal group.",
                BasicQuestionStructure.CountGroups => "a: total items; b: items per group; target: number of groups.",
                _ => "Preserve the reference's owners, event direction and target exactly."
            });
        return instruction + "\nC# relation: " + relation + "; context=" + c.SceneId
            + "\n"+(vi?"Chủ thể dùng biến, không điền tên. Đơn vị/đồ vật đã chọn (chỉ để hiểu bối cảnh): ":"Use actor placeholders, never actual names. Selected units/objects (context only): ")+c.Unit+"; "+c.GroupUnit
            + "\nReference JSON:\n" + QuestionBankStore.SerializeDraft(Example(c))
            + (correction is null ? "" : "\nPrevious attempt: " + correction + ". " + details + " Reword all fields while preserving their facts and relation.")
            + (duplicates is not { Count: > 0 } ? "" : "\nAvoid this wording: " + JsonSerializer.Serialize(duplicates.TakeLast(1), Json));
    }

    public static string Grammar(BasicQuestionContract c)
    {
        if (!Supports(c)) throw new ArgumentException("Unsupported open prose profile.");
        using var doc = JsonDocument.Parse(QuestionBankStore.SerializeDraft(Example(c)));
        var rules = new StringBuilder(); int index = 0;
        string Literal(string text) => JsonSerializer.Serialize(text);
        string Node(JsonElement node, string property = "")
        {
            if (node.ValueKind == JsonValueKind.Object) return Literal("{") + " ws " + string.Join(" ws "+Literal(",")+" ws ", node.EnumerateObject().Select(p => Literal(JsonSerializer.Serialize(p.Name)) + " ws "+Literal(":")+" ws " + Node(p.Value, p.Name))) + " ws " + Literal("}");
            if (node.ValueKind == JsonValueKind.Array) return Literal("[")+" ws "+string.Join(" ws "+Literal(",")+" ws ", node.EnumerateArray().Select(v => Node(v)))+" ws "+Literal("]");
            string value = node.GetString()!;
            if (property is "role" or "step" or "unit_id") return Literal(JsonSerializer.Serialize(value));
            string name = "field-" + index++;
            string[] slots = Slots.Matches(value).Select(m => m.Value).Distinct().ToArray();
            // Bind variables, not complete sentences. Quantity/unit pairs remain
            // together; actor placement may change for active/passive wording.
            var matches=Slots.Matches(value).Cast<Match>().ToArray();
            var blocks=new List<string>();
            for(int i=0;i<matches.Length;i++)
            {
                string block=Literal(matches[i].Value);
                if(i+1<matches.Length && Regex.IsMatch(matches[i].Value,@"^\{(?:a|b|f\d+)\}$")
                    && Regex.IsMatch(matches[i+1].Value,@"^\{(?:unit(?:_[ab])?|items?|group(?:_one)?|v\d+)\}$")
                    && string.IsNullOrWhiteSpace(value[(matches[i].Index+matches[i].Length)..matches[i+1].Index]))
                    block += " " + Literal(" ") + " " + Literal(matches[++i].Value);
                blocks.Add(block);
            }
            IEnumerable<string[]> Orders(string[] source)
            {
                if(source.Length==0){yield return [];yield break;}
                if(source.Length>3){yield return source;yield break;}
                for(int i=0;i<source.Length;i++)
                    foreach(var tail in Orders(source.Where((_,j)=>j!=i).ToArray()))yield return [source[i],..tail];
            }
            string branch=blocks.Count==0 ? "character{1,140}" : "("+string.Join(" | ",Orders(blocks.ToArray())
                .Select(order=>"character{0,32} "+string.Join(" character{0,48} ",order)+" character{0,40}").Distinct())+")";
            rules.AppendLine(name + " ::= " + Literal("\"") + " " + branch + " " + Literal("\""));
            return name;
        }
        string root = Node(doc.RootElement);
        return "root ::= " + root + " ws\n" + rules
            + "character ::= [" + QuestionProseLanguage.GrammarLetters(c.Language) + " ,.;:!?()'\\u2019-]\nws ::= [ \\t]{0,2}\n";
    }
}
