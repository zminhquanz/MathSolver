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
        || c.Version == ReasoningStoryCatalogue.Version && (SameMonthCalendarCatalogue.IsProfile(c)||OvernightTimeCatalogue.IsProfile(c)||SameDayBreakCatalogue.IsProfile(c)||RectangleAreaOpenCatalogue.IsProfile(c)||RectanglePerimeterOpenCatalogue.IsProfile(c)||DecimalOpenStoryCatalogue.IsProfile(c)||SameDayTimeCatalogue.IsProfile(c)||FractionPartOpenCatalogue.IsProfile(c)||FractionRemainingOpenCatalogue.IsProfile(c)||FractionWholeOpenCatalogue.IsProfile(c)||SumRatioOpenCatalogue.IsProfile(c)||SumDifferenceOpenCatalogue.IsProfile(c)||AverageMissingOpenCatalogue.IsProfile(c)||AverageOpenCatalogue.IsProfile(c)||PercentagePartOpenCatalogue.IsProfile(c)||PercentageWholeOpenCatalogue.IsProfile(c)||PercentageRemainingOpenCatalogue.IsProfile(c))
        || c.Version == ReasoningStoryCatalogue.Version && c.Family == BankQuestionFamily.Measurement
            && (ElementaryQuizType)c.BankVariant is ElementaryQuizType.LengthConversion or ElementaryQuizType.MassConversion
                or ElementaryQuizType.CapacityConversion or ElementaryQuizType.AreaConversion or ElementaryQuizType.VolumeConversion);

    internal static BasicQuestionDraft Example(BasicQuestionContract c) => SameMonthCalendarCatalogue.IsProfile(c)&&SameMonthCalendarPilot.IsAdjacent(c.SceneId)
        ? AdjacentMonthCalendarLanguage.Reference(c) : c.Version == ExpandedArithmeticCatalogue.Version
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
                if(PilotMeaningRules.TechnicalText(bare))
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
        if(SameMonthCalendarCatalogue.IsProfile(c))return SameMonthCalendarMeaning.Validate(c,syntax.Draft!);
        if(OvernightTimeCatalogue.IsProfile(c))return OvernightTimeMeaning.Validate(c,syntax.Draft!);
        if(SameDayBreakCatalogue.IsProfile(c))return SameDayBreakMeaning.Validate(c,syntax.Draft!);
        if(RectangleAreaOpenCatalogue.IsProfile(c))return RectangleAreaOpenMeaning.Validate(c,syntax.Draft!);
        if(RectanglePerimeterOpenCatalogue.IsProfile(c))return RectanglePerimeterOpenMeaning.Validate(c,syntax.Draft!);
        if(PercentageWholeOpenCatalogue.IsProfile(c))return PercentageWholeOpenMeaning.Validate(c,syntax.Draft!);
        if(PercentageRemainingOpenCatalogue.IsProfile(c))return PercentageRemainingOpenMeaning.Validate(c,syntax.Draft!);
        if(PercentagePartOpenCatalogue.IsProfile(c))return PercentagePartOpenMeaning.Validate(c,syntax.Draft!);
        if(SumRatioOpenCatalogue.IsProfile(c))return SumRatioOpenMeaning.Validate(c,syntax.Draft!);
        if(SumDifferenceOpenCatalogue.IsProfile(c))return SumDifferenceOpenMeaning.Validate(c,syntax.Draft!);
        if(AverageMissingOpenCatalogue.IsProfile(c))return AverageMissingOpenMeaning.Validate(c,syntax.Draft!);
        if(AverageOpenCatalogue.IsProfile(c))return AverageOpenMeaning.Validate(c,syntax.Draft!);
        if(FractionWholeOpenCatalogue.IsProfile(c))return FractionWholeOpenMeaning.Validate(c,syntax.Draft!);
        if(FractionRemainingOpenCatalogue.IsProfile(c))return FractionRemainingOpenMeaning.Validate(c,syntax.Draft!);
        if(FractionPartOpenCatalogue.IsProfile(c))return FractionPartOpenCatalogue.Validate(c,syntax.Draft!);
        if(SameDayTimeCatalogue.IsProfile(c))
        {
            if(PilotMeaningRules.TimeContradiction(syntax.Draft!,c.Language))
                return new(null,"ChangedRelationOrTarget",ErrorDetails:"Same-day continuity or chronology changed.");
            return PilotMeaningRules.ProvesTime(c,syntax.Draft!)?syntax:
                new(syntax.Draft,"SemanticReviewRequired",c,"Time quantity roles, continuity or target could not be proved.");
        }
        if(DecimalOpenStoryCatalogue.IsProfile(c)&&DecimalOpenStoryCatalogue.HasContradiction(c,syntax.Draft!))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Decimal event direction or financial condition changed.");
        // Voice markers such as Vietnamese "được tặng" carry mathematical
        // direction. Check them before legacy neutral-link normalization.
        if(OpenProseMeaning.HasContradiction(syntax.Draft!,Example(c),c.Language))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Event direction or condition changed.");
        if(DecimalOpenStoryCatalogue.IsProfile(c)&&PilotMeaningRules.ProvesDecimal(c,syntax.Draft!))return syntax;
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
        if (SameDayTimeCatalogue.IsProfile(c))
            return SameDayTimeCatalogue.OpenPrompt(c)
                + (correction is null ? "" : c.Language==AppLanguage.Vietnamese
                    ? "\nLượt trước chưa đạt: " + correction + ". " + details + " Viết lại rõ vai trò thời điểm và thời lượng, giữ đúng câu hỏi."
                    : "\nPrevious attempt: " + correction + ". " + details + " Rewrite with clear clock and duration roles and the correct target.")
                + (duplicates is not { Count: > 0 } ? "" : "\nAvoid this wording: " + JsonSerializer.Serialize(duplicates.TakeLast(1), Json));
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
        if (SameMonthCalendarCatalogue.IsProfile(c)) instruction = SameMonthCalendarCatalogue.WritingInstructions(c);
        else if (OvernightTimeCatalogue.IsProfile(c)) instruction = OvernightTimeCatalogue.WritingInstructions(c);
        else if (SameDayBreakCatalogue.IsProfile(c)) instruction = SameDayBreakCatalogue.WritingInstructions(c);
        if (RectangleAreaOpenCatalogue.IsProfile(c)) instruction = RectangleAreaOpenCatalogue.WritingInstructions(c.Language);
        if (RectanglePerimeterOpenCatalogue.IsProfile(c)) instruction = RectanglePerimeterOpenCatalogue.WritingInstructions(c);
        if (SumRatioOpenCatalogue.IsProfile(c)) instruction = SumRatioOpenCatalogue.WritingInstructions(c.Language);
        if (SumDifferenceOpenCatalogue.IsProfile(c)) instruction = SumDifferenceOpenCatalogue.WritingInstructions(c.Language);
        if (AverageMissingOpenCatalogue.IsProfile(c)) instruction = AverageMissingOpenCatalogue.WritingInstructions(c.Language);
        if (AverageOpenCatalogue.IsProfile(c)) instruction = AverageOpenCatalogue.WritingInstructions(c.Language);
        if (FractionWholeOpenCatalogue.IsProfile(c)) instruction = FractionWholeOpenCatalogue.WritingInstructions(c.Language);
        if (FractionRemainingOpenCatalogue.IsProfile(c)) instruction = FractionRemainingOpenCatalogue.WritingInstructions(c.Language);
        if (PercentageWholeOpenCatalogue.IsProfile(c)) instruction = PercentageWholeOpenCatalogue.WritingInstructions(c.Language);
        if (PercentageRemainingOpenCatalogue.IsProfile(c)) instruction = PercentageRemainingOpenCatalogue.WritingInstructions(c.Language);
        if (PercentagePartOpenCatalogue.IsProfile(c)) instruction = PercentagePartOpenCatalogue.WritingInstructions(c.Language);
        string relation = SameMonthCalendarCatalogue.IsProfile(c)?SameMonthCalendarCatalogue.PromptRelation(c)
            :OvernightTimeCatalogue.IsProfile(c)?OvernightTimeCatalogue.PromptRelation
            :SameDayBreakCatalogue.IsProfile(c)?SameDayBreakCatalogue.PromptRelation
            : RectangleAreaOpenCatalogue.IsProfile(c)?RectangleAreaOpenCatalogue.PromptRelation
            : RectanglePerimeterOpenCatalogue.IsProfile(c)?RectanglePerimeterOpenCatalogue.PromptRelation
            : SameDayTimeCatalogue.IsProfile(c)?SameDayTimeCatalogue.PromptRelation(c)
            : SumRatioOpenCatalogue.IsProfile(c) ? SumRatioOpenCatalogue.PromptRelation(c)
            : SumDifferenceOpenCatalogue.IsProfile(c) ? SumDifferenceOpenCatalogue.PromptRelation(c)
            : AverageMissingOpenCatalogue.IsProfile(c) ? AverageMissingOpenCatalogue.PromptRelation(c)
            : AverageOpenCatalogue.IsProfile(c) ? AverageOpenCatalogue.PromptRelation(c)
            : PercentageWholeOpenCatalogue.IsProfile(c) ? PercentageWholeOpenCatalogue.PromptRelation(c.Language)
            : PercentageRemainingOpenCatalogue.IsProfile(c) ? PercentageRemainingOpenCatalogue.PromptRelation(c.Language)
            : PercentagePartOpenCatalogue.IsProfile(c) ? PercentagePartOpenCatalogue.PromptRelation(c)
            : FractionWholeOpenCatalogue.IsProfile(c) ? FractionWholeOpenCatalogue.PromptRelation(c.Language)
            : FractionRemainingOpenCatalogue.IsProfile(c) ? FractionRemainingOpenCatalogue.PromptRelation(c.Language)
            : FractionPartOpenCatalogue.IsProfile(c) ? FractionPartOpenCatalogue.Relation
            : DecimalOpenStoryCatalogue.IsProfile(c) ? DecimalOpenStoryCatalogue.PromptRelation(c)
            : c.Family == BankQuestionFamily.FindX
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
            + (SameMonthCalendarCatalogue.IsProfile(c)?SameMonthCalendarCatalogue.ContextNotes(c):"")
            + (SumDifferenceOpenCatalogue.IsProfile(c)?SumDifferenceOpenCatalogue.WritingFocus(c):"")
            + (RectanglePerimeterOpenCatalogue.IsProfile(c)?RectanglePerimeterOpenCatalogue.WritingFocus(c):"")
            + (RectangleAreaOpenCatalogue.IsProfile(c)?RectangleAreaOpenCatalogue.WritingFocus(c):"")
            + (OvernightTimeCatalogue.IsProfile(c)?OvernightTimeCatalogue.WritingFocus(c):"")
            + (SameDayBreakCatalogue.IsProfile(c)?SameDayBreakCatalogue.WritingFocus(c):"")
            + (correction is null ? "" : PercentageWholeOpenCatalogue.IsProfile(c)
                ? PercentageWholeOpenCatalogue.RetryInstructions(c.Language,correction,details)
                : PercentageRemainingOpenCatalogue.IsProfile(c)
                ? PercentageRemainingOpenCatalogue.RetryInstructions(c.Language,correction,details)
                : FractionWholeOpenCatalogue.IsProfile(c)
                ? FractionWholeOpenCatalogue.RetryInstructions(c.Language,correction,details)
                : FractionRemainingOpenCatalogue.IsProfile(c)
                ? FractionRemainingOpenCatalogue.RetryInstructions(c.Language,correction,details)
                : "\nPrevious attempt: " + correction + ". " + details + " Reword all fields while preserving their facts and relation.")
            + (duplicates is not { Count: > 0 } ? "" : "\nAvoid this wording: " + JsonSerializer.Serialize(duplicates.TakeLast(1), Json));
    }

    public static string Grammar(BasicQuestionContract c)
    {
        if (!Supports(c)) throw new ArgumentException("Unsupported open prose profile.");
        bool clockActivity=SameDayBreakCatalogue.IsProfile(c)||OvernightTimeCatalogue.IsProfile(c);
        bool calendar=SameMonthCalendarCatalogue.IsProfile(c);
        bool singleSentence=SumRatioOpenCatalogue.IsProfile(c)||calendar||clockActivity||RectangleAreaOpenCatalogue.IsProfile(c)||RectanglePerimeterOpenCatalogue.IsProfile(c)||SameDayTimeCatalogue.IsProfile(c)||PercentageWholeOpenCatalogue.IsProfile(c)||PercentageRemainingOpenCatalogue.IsProfile(c);
        using var doc = JsonDocument.Parse(QuestionBankStore.SerializeDraft(Example(c)));
        bool punctuatedClauses=SumRatioOpenCatalogue.IsProfile(c)||calendar||clockActivity||RectangleAreaOpenCatalogue.IsProfile(c)||RectanglePerimeterOpenCatalogue.IsProfile(c)||PercentageWholeOpenCatalogue.IsProfile(c)
            ||c.Language==AppLanguage.English&&PercentageRemainingOpenCatalogue.IsProfile(c);
        int factFields=punctuatedClauses?doc.RootElement.GetProperty("facts").GetArrayLength():0;
        var rules = new StringBuilder(); int index = 0;
        string Literal(string text) => JsonSerializer.Serialize(text);
        string Node(JsonElement node, string property = "")
        {
            if (node.ValueKind == JsonValueKind.Object) return Literal("{") + " ws " + string.Join(" ws "+Literal(",")+" ws ", node.EnumerateObject().Select(p => Literal(JsonSerializer.Serialize(p.Name)) + " ws "+Literal(":")+" ws " + Node(p.Value, p.Name))) + " ws " + Literal("}");
            if (node.ValueKind == JsonValueKind.Array) return Literal("[")+" ws "+string.Join(" ws "+Literal(",")+" ws ", node.EnumerateArray().Select(v => Node(v)))+" ws "+Literal("]");
            string value = node.GetString()!;
            if (property is "role" or "step" or "unit_id") return Literal(JsonSerializer.Serialize(value));
            int fieldIndex=index++;
            string name = "field-" + fieldIndex;
            string[] slots = Slots.Matches(value).Select(m => m.Value).Distinct().ToArray();
            // Bind variables, not complete sentences. Average observations also
            // allow unit-first clauses ("The number of ... was ..."), so their
            // single quantity and unit can move independently within the field.
            var matches=Slots.Matches(value).Cast<Match>().ToArray();
            var blocks=new List<string>();
            for(int i=0;i<matches.Length;i++)
            {
                string block=Literal(matches[i].Value);
                if(calendar&&i+2<matches.Length&&matches[i].Value=="{v0}"
                    &&matches[i+1].Value is "{v2}" or "{v3}"&&matches[i+2].Value=="{v1}")
                {
                    // Bind actor/action/object roles, not an entire event sentence.
                    blocks.Add(Literal(matches[i].Value+" "+matches[i+1].Value+" "+matches[i+2].Value));i+=2;continue;
                }
                if(calendar&&i+2<matches.Length&&matches[i].Value is "{f0}" or "{f1}"
                    &&matches[i+1].Value==(SameMonthCalendarPilot.IsAdjacent(c.SceneId)&&matches[i].Value=="{f1}"?"{f4}":"{f2}")&&matches[i+2].Value=="{f3}")
                {
                    blocks.Add(Literal(c.Language==AppLanguage.Vietnamese
                        ?"ngày "+matches[i].Value+" tháng "+matches[i+1].Value+" năm {f3}"
                        :"day "+matches[i].Value+" of month "+matches[i+1].Value+" in year {f3}"));i+=2;continue;
                }
                if(calendar&&property=="question"&&matches.Length==2)
                {
                    blocks.Add(c.Language==AppLanguage.Vietnamese?Literal("{v4} của {v0}")
                        :"("+Literal("{v0}'s {v4}")+" | "+Literal("the {v4} for {v0}")+")");i=matches.Length;continue;
                }
                if(AverageMissingOpenCatalogue.IsProfile(c)&&i+1<matches.Length
                    &&matches[i].Value=="{v5}"&&matches[i+1].Value=="{v4}")
                {
                    blocks.Add(c.Language==AppLanguage.English
                        ?"("+Literal("{v5} by each {v4}")+" | "+Literal("{v4} {v5}")+")"
                        :Literal("{v4} {v5}"));i++;continue;
                }
                if((AverageOpenCatalogue.IsProfile(c)||AverageMissingOpenCatalogue.IsProfile(c))&&i+1<matches.Length
                    &&(matches[i].Value is "{v0}" or "{v1}" or "{v2}" || AverageMissingOpenCatalogue.IsProfile(c)&&matches[i].Value=="{v4}")&&matches[i+1].Value=="{v5}")
                {
                    // Bind the action to its owner in either voice, not a whole
                    // sentence. Independent amount/unit placement still allows
                    // unit-first clauses and amount-first passive observations.
                    string owner=matches[i].Value,action=matches[++i].Value;
                    block=c.Language==AppLanguage.Vietnamese?Literal(owner+" "+action)
                        :"("+Literal(owner+" "+action)+" | "+Literal(action+(AverageMissingOpenCatalogue.IsProfile(c)&&owner=="{v4}"?" by each ":" by ")+owner)+")";
                    blocks.Add(block);continue;
                }
                if(AverageMissingOpenCatalogue.IsProfile(c)&&matches[i].Value=="{v2}"&&i+1<matches.Length&&matches[i+1].Value=="{v6}")
                {
                    blocks.Add(Literal("{v2} {v6}"));i++;continue;
                }
                if(AverageOpenCatalogue.IsProfile(c)&&matches[i].Value=="{v4}"&&i+1<matches.Length&&matches[i+1].Value=="{v6}")
                {
                    // The base action is a verb attached to its group, never a
                    // quantity or a dangling token at the end of the question.
                    blocks.Add(Literal(matches[i].Value+" "+matches[++i].Value));continue;
                }
                if(clockActivity&&i+1<matches.Length
                    &&(c.Language==AppLanguage.Vietnamese&&matches[i].Value=="{v1}"&&matches[i+1].Value=="{v0}"
                        ||c.Language==AppLanguage.English&&matches[i].Value=="{v0}"&&matches[i+1].Value=="{v1}"))
                {
                    // Bind a named activity to its owner, not a complete
                    // sentence. Free predicates/openings remain generated.
                    // This prevents dangling actors/activities and English
                    // bare session nouns without possessives or articles.
                    blocks.Add(c.Language==AppLanguage.Vietnamese?Literal("{v1} của {v0}")
                        :"("+Literal("{v0}'s {v1}")+" | "+Literal("the {v1} for {v0}")+" | "+Literal("The {v1} for {v0}")+")");
                    i++;continue;
                }
                if(SameDayTimeCatalogue.IsProfile(c) && matches[i].Value=="{v1}" && i+1<matches.Length && matches[i+1].Value=="{v0}")
                {
                    // Bind the owner/activity phrase, never an entire sentence.
                    // Neither a person's name nor an activity is a numeric unit.
                    block=c.Language==AppLanguage.Vietnamese ? Literal("{v1} của {v0}")
                        : "("+Literal("the {v1} for {v0}")+" | "+Literal("The {v1} for {v0}")+" | "+Literal("{v0}'s {v1}")+")";
                    blocks.Add(block); i++; continue;
                }
                if((SameDayTimeCatalogue.IsProfile(c)||clockActivity) && matches[i].Value is "{f0}" or "{f2}" && i+1<matches.Length
                    && matches[i+1].Value==(matches[i].Value=="{f0}"?"{f1}":"{f3}"))
                {
                    // Clock components travel together with their immutable units.
                    string hour=matches[i].Value,minute=matches[++i].Value;
                    block=c.Language==AppLanguage.Vietnamese?Literal(hour+" giờ "+minute+" phút")
                        :"("+Literal(hour+" hours "+minute+" minutes")+" | "+Literal(hour+" hours and "+minute+" minutes")+")";
                    blocks.Add(block); continue;
                }
                if(!RectangleAreaOpenCatalogue.IsProfile(c) && !RectanglePerimeterOpenCatalogue.IsProfile(c) && !SumRatioOpenCatalogue.IsProfile(c) && !SumDifferenceOpenCatalogue.IsProfile(c) && !AverageMissingOpenCatalogue.IsProfile(c) && !AverageOpenCatalogue.IsProfile(c) && !FractionRemainingOpenCatalogue.IsProfile(c) && !FractionWholeOpenCatalogue.IsProfile(c) && !PercentageWholeOpenCatalogue.IsProfile(c) && !PercentageRemainingOpenCatalogue.IsProfile(c) && i+1<matches.Length && Regex.IsMatch(matches[i].Value,@"^\{(?:a|b|f\d+)\}$")
                    && Regex.IsMatch(matches[i+1].Value,@"^\{(?:unit(?:_[ab])?|items?|group(?:_one)?|v\d+)\}$")
                    && string.IsNullOrWhiteSpace(value[(matches[i].Index+matches[i].Length)..matches[i+1].Index]))
                    block += " " + Literal(" ") + " " + Literal(matches[++i].Value);
                blocks.Add(block);
            }
            IEnumerable<string[]> Orders(string[] source)
            {
                if(source.Length==0){yield return [];yield break;}
                if(source.Length>(SumRatioOpenCatalogue.IsProfile(c)?5:(SumDifferenceOpenCatalogue.IsProfile(c)||clockActivity||SameDayTimeCatalogue.IsProfile(c)||FractionRemainingOpenCatalogue.IsProfile(c)||FractionWholeOpenCatalogue.IsProfile(c)||PercentageWholeOpenCatalogue.IsProfile(c)||PercentageRemainingOpenCatalogue.IsProfile(c)?4:3))){yield return source;yield break;}
                for(int i=0;i<source.Length;i++)
                    foreach(var tail in Orders(source.Where((_,j)=>j!=i).ToArray()))yield return [source[i],..tail];
            }
            bool time=SameDayTimeCatalogue.IsProfile(c);
            int prefix=time&&property!="question"?16:79;
            bool wordBoundaries=SumRatioOpenCatalogue.IsProfile(c)||calendar||clockActivity||PercentageWholeOpenCatalogue.IsProfile(c)||RectangleAreaOpenCatalogue.IsProfile(c)||RectanglePerimeterOpenCatalogue.IsProfile(c);
            string branch=blocks.Count==0 ? "character{1,140}" : "("+string.Join(" | ",Orders(blocks.ToArray())
                .Select(order=>wordBoundaries
                    // Slots are complete lexical tokens, not function names or
                    // letter aliases. Keep every order and all free prose; only
                    // a person's English possessive may touch its placeholder.
                    ? "(character{1,31} "+Literal(" ")+")? "+string.Join(" word-gap ",order.Select(block=>block
                        +(c.Language==AppLanguage.English&&(SumRatioOpenCatalogue.IsProfile(c)?block==Literal("{v0}")||block==Literal("{v1}"):block==Literal("{v0}")||block==Literal("{v2}")&&!RectangleAreaOpenCatalogue.IsProfile(c)&&!RectanglePerimeterOpenCatalogue.IsProfile(c))?" possessive?":"")))+" word-tail"
                    :time
                    ? "(character{0,"+prefix+"} "+Literal(" ")+")? "+string.Join(" "+Literal(" ")+" character{0,71} "+Literal(" ")+" ",order)+" ("+Literal(" ")+" character{0,79})?"
                    : "character{0,32} "+string.Join(" character{0,48} ",order)+" character{0,40}").Distinct())+")";
            if(calendar&&SameMonthCalendarPilot.IsAdjacent(c.SceneId))
            {
                if(fieldIndex<2)
                {
                    // A complete record introduction may precede the event.
                    // Keep complete actor/action/item and date groups intact; do
                    // not attach a dangling "Day" label to a person's name.
                    string actor=Literal(fieldIndex==0?"{v0} {v2} {v1}":"{v0} {v3} {v1}");
                    string day=fieldIndex==0?"{f0}":"{f1}",month=fieldIndex==0?"{f2}":"{f4}";
                    string date=Literal(c.Language==AppLanguage.Vietnamese?$"ngày {day} tháng {month} năm {{f3}}":$"day {day} of month {month} in year {{f3}}");
                    branch="(("+Literal(c.Language==AppLanguage.Vietnamese?"Theo ghi chép, ":"According to the record, ")+")? "+actor+" word-gap "+date+" word-tail | "
                        +Literal(c.Language==AppLanguage.Vietnamese?"Vào ":"On ")+" "+date+" "+Literal(", ")+" "+actor+" word-tail)";
                }
                if(property=="question"&&c.Language==AppLanguage.Vietnamese)
                {
                    // Bind the complete interval NP directly to the day-count
                    // predicate. Predicates remain free prose; no complete
                    // questions are enumerated. An arbitrary prefix, even with
                    // a comma, can duplicate or fragment the interval noun.
                    branch="("+Literal("Hỏi ")+" | "+Literal("Số ngày của ")+")? "
                        +Literal("{v4} của {v0}")+" "+Literal(" ")+" character{1,79}";
                }
                if(property=="question"&&c.Language==AppLanguage.English&&SameMonthCalendarPilot.IsAdjacentInclusive(c.SceneId))
                {
                    // Preserve the complete owner/interval NP and the requested
                    // day unit as lexical groups, in either order. All question
                    // wording between them stays generated, not enumerated.
                    string owner=blocks.Single();
                    branch="((character{1,31} "+Literal(" ")+")? calendar-days word-gap "+owner+" word-tail | "
                        +"(character{1,31} "+Literal(" ")+")? "+owner+" word-gap calendar-days word-tail)";
                }
            }
            if(time&&blocks.Count==2)
            {
                // Two clause structures bind the clock to its owner. A free
                // permutation could put the owner after an unrelated clause.
                // Predicates remain generated text, not enumerated sentences.
                string opening=c.Language==AppLanguage.Vietnamese?"Theo ghi chép, ":"According to the records, ";
                branch="("+Literal(opening)+")? "+blocks[0]+" "+Literal(" ")+" character{1,64} "+Literal(" ")+" "+blocks[1]+" ("+Literal(" ")+" character{0,40})?"
                    +" | "+Literal(c.Language==AppLanguage.Vietnamese?"Vào lúc ":"At ")+" "+blocks[1]+" "+Literal(", ")+" "+blocks[0]+" "+Literal(" ")+" character{1,64}";
                branch="("+branch+")";
            }
            string ending=punctuatedClauses&&fieldIndex<factFields?" "+Literal(".")+" "
                :punctuatedClauses&&property=="question"?" "+Literal("?")+" ":singleSentence?" ending ":" ";
            rules.AppendLine(name + " ::= " + Literal("\"") + " " + branch + ending + Literal("\""));
            return name;
        }
        string root = Node(doc.RootElement);
        return "root ::= " + root + " ws\n" + rules
            + "character ::= [" + QuestionProseLanguage.GrammarLetters(c.Language) + (singleSentence?" ,()'\\u2019-]":" ,.;:!?()'\\u2019-]")
            + (calendar||clockActivity?"\nword-gap ::= (\" \" | \", \") (character{1,46} \" \")?\nword-tail ::= (\" \" character{1,79})? | \", \" character{1,79}\npossessive ::= \"'s\" | \"\\u2019s\""
                :SumRatioOpenCatalogue.IsProfile(c)||PercentageWholeOpenCatalogue.IsProfile(c)||RectangleAreaOpenCatalogue.IsProfile(c)||RectanglePerimeterOpenCatalogue.IsProfile(c)?"\nword-gap ::= \" \" (character{1,46} \" \")?\nword-tail ::= (\" \" character{1,39})?\npossessive ::= \"'s\" | \"\\u2019s\"":"")
            + (calendar&&SameMonthCalendarPilot.IsAdjacentInclusive(c.SceneId)&&c.Language==AppLanguage.English?"\ncalendar-days ::= (\"calendar \")? \"days\"":"")
            + (singleSentence?"\nending ::= [.!?:]?":"")+"\nws ::= [ \\t]{0,2}\n";
    }
}
