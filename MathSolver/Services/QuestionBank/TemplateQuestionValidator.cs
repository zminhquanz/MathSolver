using MathSolver.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

public static class BasicQuestionValidator
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private static readonly string[] Fields = ["given_a", "given_b", "question", "solution_lead", "unit_id"];
    private static readonly string[] Tokens = ["{name}", "{other}", "{a}", "{b}", "{unit}", "{group}", "{group_one}"];
    public static string Normalize(string text) => LegacyBasicQuestionValidator.Normalize(text);

    public static BasicDraftValidation Validate(string rawJson, BasicQuestionContract c)
    {
        if (c.Version == FindXQuestionCatalogue.Version) return FindXQuestionCatalogue.Validate(rawJson, c);
        if (c.Version == AppliedQuestionCatalogue.Version) return AppliedQuestionCatalogue.Validate(rawJson, c);
        if (c.Version == AdditionQuestionCatalogue.Version) return AdditionQuestionValidator.Validate(rawJson, c);
        if (!c.IsTemplate) return LegacyBasicQuestionValidator.Validate(rawJson, c);
        if (!c.IsValid) return new(null, "InvalidContract");
        if (string.IsNullOrWhiteSpace(rawJson) || rawJson.Length > 12000) return new(null, "InvalidJson");
        try
        {
            string json = rawJson.Trim();
            if (json.StartsWith("```", StringComparison.Ordinal))
            {
                int start = json.IndexOf('\n'), end = json.LastIndexOf("```", StringComparison.Ordinal);
                if (start < 0 || end <= start) return new(null, "InvalidJson");
                json = json[(start + 1)..end].Trim();
            }
            using var document = JsonDocument.Parse(json, new() { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new(null, "InvalidJson");
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != Fields.Length || properties.Any(p => !Fields.Contains(p.Name)
                || p.Value.ValueKind != JsonValueKind.String) || properties.Select(p => p.Name).Distinct().Count() != Fields.Length)
                return new(null, "InvalidFields");
            string Field(string name) => document.RootElement.GetProperty(name).GetString()!;
            var draft = new BasicQuestionDraft(Field("given_a"), Field("given_b"), Field("question"), Field("solution_lead"), Field("unit_id"));
            var languageError = QuestionProseLanguage.ValidateAndNormalize(draft, c.Language, out draft);
            if (languageError is not null) return new(null, languageError);
            var unit = QuestionUnits.Find(draft.UnitId);
            if (unit is null) return new(null, "ChangedUnits");
            bool contextual = c.Version == ArithmeticQuestionCatalogue.Version;
            if (contextual && unit.Id != QuestionUnits.Find(c)!.Id) return new(null, "ChangedUnits");
            c = BasicQuestionTemplates.ApplyUnit(c, unit);
            if (!c.IsValid) return new(null, "ChangedUnits");
            string[] text = [draft.GivenA, draft.GivenB, draft.Question, draft.SolutionLead!];
            if (text.Any(s => s.Length is < 10 or > 700 || s.Any(char.IsControl))) return new(null, "InvalidText");
            foreach (string s in text)
            {
                var matches = Regex.Matches(s, @"\{[^{}]*\}", RegexOptions.CultureInvariant, Timeout);
                if (matches.Any(m => !Tokens.Contains(m.Value))
                    || Regex.Replace(s, @"\{[^{}]*\}", "", RegexOptions.CultureInvariant, Timeout).IndexOfAny(['{', '}']) >= 0)
                    return new(null, "InvalidPlaceholders");
                if (Regex.IsMatch(s, @"\d|[=<>%+×÷]|\b(?:minus|plus|multiplied by|divided by)\b", RegexOptions.IgnoreCase, Timeout))
                    return new(null, "ChangedQuantities");
                // Reject additional spelled-out quantities, while allowing a nonnumeric story opening.
                string withoutOpening = Regex.Replace(s, @"\b(?:một hôm|một ngày nọ|one day)\b", "", RegexOptions.IgnoreCase, Timeout);
                if (Regex.IsMatch(withoutOpening, @"\b(?:một|hai|ba|bốn|năm|sáu|bảy|tám|chín|mười|one|two|three|four|five|six|seven|eight|nine|ten|half|nửa|gấp đôi)\b",
                    RegexOptions.IgnoreCase, Timeout)) return new(null, "ChangedQuantities");
                if (Has(s, @"\b(?:không|chẳng|chưa|not|never|except|trừ khi)\b")) return new(null, "ExtraRelations");
                if (contextual && Has(s, @"\b[a-z]+n['’]t\b|\b(?:cannot|without|no)\b")) return new(null, "ExtraRelations");
            }
            if (Count(draft.GivenA, "{a}") != 1 || Count(draft.GivenB, "{b}") != 1
                || Count(draft.GivenA, "{b}") != 0 || Count(draft.GivenB, "{a}") != 0
                || Count(draft.Question + draft.SolutionLead, "{a}") + Count(draft.Question + draft.SolutionLead, "{b}") != 0)
                return new(null, "InvalidPlaceholders");
            if (contextual && OneStepRelationRules.IsExtended(c.Structure))
                return OneStepRelationRules.Validate(draft, c);
            string ownerA = c.Structure is BasicQuestionStructure.TimesAsMany or BasicQuestionStructure.TimesFewer ? "{other}" : "{name}";
            string ownerB = c.Structure is BasicQuestionStructure.Combine or BasicQuestionStructure.Difference ? "{other}" : "{name}";
            if ((c.Structure != BasicQuestionStructure.EqualGroups && !draft.GivenA.Contains(ownerA))
                || !draft.GivenB.Contains(ownerB)
                || (c.Structure is BasicQuestionStructure.TimesAsMany or BasicQuestionStructure.TimesFewer && !draft.GivenB.Contains("{other}"))
                || (c.Structure != BasicQuestionStructure.EqualShare && !draft.Question.Contains("{name}")))
                return new(null, "InvalidPlaceholders");
            // An actor can own a container; an actor is not the object packed inside it.
            if (text.Any(s => Has(s, @"\{group(?:_one)?\}[^{}]*(?:đựng|chứa|contains?|holds?)[^{}]*\{(?:name|other)\}"
                + @"|(?:đựng|chứa|bỏ|xếp|có|has|owns|holds|packs?|puts?)\s+\{(?:name|other)\}")))
                return new(null, "InvalidContext");
            if (text.Any(s => Has(s, @"\b(?:các|những|bạn|bé|ông|bà|cô|chú|bác|anh|chị|mẹ|cha|dì|cậu|mợ|these|those|each|every|Grandma|Grandpa|Uncle|Aunt)\s+\{(?:name|other)\}")))
                return new(null, "InvalidContext");
            if (text.Any(s => Has(s, @"(?:cửa hàng|kho hàng|kho sách|shop|warehouse)\s+\{(?:name|other)\}")))
                return new(null, "InvalidContext");
            bool factor = c.Structure is BasicQuestionStructure.TimesAsMany or BasicQuestionStructure.TimesFewer;
            bool groupB = c.Structure is BasicQuestionStructure.EqualGroups or BasicQuestionStructure.EqualShare;
            if (!Has(draft.GivenA, @"\{a\}\s+\{unit\}")
                || factor && !draft.GivenB.Contains("{unit}")
                || !Has(draft.GivenB, groupB ? @"\{b\}\s+(?:(?:identical|equal|such)\s+)?\{group\}"
                    : factor ? @"\{b\}\s+(?:lần|times)\b" : @"\{b\}\s+(?:(?:more|additional|new)\s+)?\{unit\}"))
                return new(null, "ChangedUnits");
            string answerToken = c.Structure == BasicQuestionStructure.CountGroups ? "{group}" : "{unit}";
            if (!draft.Question.Contains(answerToken) || !draft.SolutionLead!.Contains(answerToken)
                || c.Structure == BasicQuestionStructure.CountGroups && (draft.Question.Contains("{unit}") || draft.SolutionLead.Contains("{unit}")))
                return new(null, "ChangedUnits");
            bool vi = c.Language == AppLanguage.Vietnamese;
            if (!Has(draft.Question, vi ? @"\b(?:hỏi|bao nhiêu)\b" : @"\b(?:how many|what|find)\b")
                || vi && !draft.ProblemText.Any(ch => ch > 127)) return new(null, "WrongLanguage");
            if (!RolesAndRelation(draft, c.Structure, vi) || !SolutionActorsMatch(draft, c.Structure))
                return new(null, "ChangedRelationOrTarget");
            if (contextual && !ContextMatches(draft, c, unit, vi)) return new(null, "InvalidContext");
            // Stored contracts carry preview values only; template semantics cannot depend on those values.
            // Re-render with small, large and unit quantities to catch unsupported slots before insertion.
            foreach (var (a, b) in new[] { (8, 2), (12, 3), (100, 5) })
            {
                var probe = c with { Left = a, Right = b };
                if (BasicQuestionTemplates.Render(draft.ProblemText, probe).Contains('{')) return new(null, "InvalidPlaceholders");
            }
            return new(draft, null, c);
        }
        catch (JsonException) { return new(null, "InvalidJson"); }
        catch (RegexMatchTimeoutException) { return new(null, "InvalidText"); }
    }

    private static int Count(string text, string token) => (text.Length - text.Replace(token, "").Length) / token.Length;
    private static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
    private static bool SolutionActorsMatch(BasicQuestionDraft draft, BasicQuestionStructure structure)
    {
        bool name = draft.SolutionLead!.Contains("{name}"), other = draft.SolutionLead.Contains("{other}");
        // A generic lead can omit actors. Explicit ownership must describe the question's target.
        return structure switch
        {
            BasicQuestionStructure.Combine => name == other,
            BasicQuestionStructure.Difference => !other || name,
            _ => !other
        };
    }
    private static bool RolesAndRelation(BasicQuestionDraft d, BasicQuestionStructure s, bool vi)
    {
        const string name = @"\{name\}", other = @"\{other\}";
        // Bind a role to its fact, not just to an arbitrary occurrence elsewhere in the clause.
        const string between = @"(?:[^{}]|\{(?:unit|group|group_one)\})*";
        bool ActorBefore(string text, string actor, string quantity) => Has(text, actor + between + quantity);
        bool Owns(string text, string actor, string quantity) => Has(text, actor + (vi
            ? @"[^{}]*(?:có|giữ|sở hữu|còn lại|còn)[^{}]*" : @"[^{}]*(?:has|holds|owns|had)[^{}]*") + quantity);
        bool Both(string text) => text.Contains("{name}") && text.Contains("{other}");
        string gains = vi ? @"\b(?:nhận|mua|nhập|được tặng|được cho|được biếu|được thưởng|bổ sung|thêm vào|kiếm|thu gom|nhặt|hái)\b" : @"\b(?:receives?|received|gets?|got|buys?|bought|collects?|collected|adds?|added|gains?|earned|earns?|finds?|found|is given|is gifted)\b";
        string loses = vi ? @"\b(?:cho đi|cho tặng|tặng|bán|lấy ra|chuyển đi|cho mượn|mất|bớt|dùng|sử dụng|đã cho|đem cho|phát|chia cho|ăn|tiêu thụ)\b" : @"\b(?:gives?|gave|sells?|sold|removes?|removed|loses?|lost|uses?|used|donates?|donated|hands? out|handed out|eats?|ate|lends? out|lent out|sends? for|sent for|transfers? out|transferred out)\b";
        bool Gain(string text) => Has(text, gains) || Has(text, @"\bđược tặng\b");
        bool Loss(string text) => Has(Regex.Replace(text, @"\b(?:được tặng|được cho|được biếu|is gifted|is given)\b", "", RegexOptions.IgnoreCase, Timeout), loses);
        bool eachA = Has(d.GivenA, vi ? @"\bmỗi\s+\{group_one\}" : @"\b(?:each|every)\s+\{group_one\}");
        bool eachB = Has(d.GivenB, vi ? @"\bmỗi\s+\{group_one\}" : @"\b(?:each|every)\s+\{group_one\}");
        bool equalB = Has(d.GivenB, vi ? @"(?:chia|xếp|phân|sắp).*(?:đều|như nhau)" : @"\b(?:equally|evenly|equal)\b");
        bool equalA = Has(d.GivenA, vi ? @"(?:chia|xếp|phân|sắp).*(?:đều|như nhau)" : @"\b(?:equally|evenly|equal)\b");
        bool groupsB = Has(d.GivenB, vi ? @"\b(?:chia|xếp|phân|sắp)\b" : @"\b(?:distributes?|divides?|splits?|packs?)\b");
        bool remainingQ = Has(d.Question, vi ? @"\bcòn(?: lại)?\b" : @"\b(?:left|remaining|remain)\b");
        bool originalQ = Has(d.Question, vi ? @"\b(?:lúc đầu|ban đầu|trước khi)\b" : @"\b(?:originally|initially|at first|before)\b");
        bool eachQ = Has(d.Question, vi ? @"\bmỗi\b" : @"\b(?:each|every|per)\b");
        bool totalQ = Has(d.Question, vi ? @"\b(?:tổng|tất cả|cả|cộng lại)\b" : @"\b(?:total|altogether|in all|together)\b");
        bool moreQ = Has(d.Question, vi ? @"\b(?:nhiều hơn|hơn|chênh lệch)\b" : @"\b(?:more|difference)\b");
        bool needQ = Has(d.Question, vi ? @"\b(?:cần thêm|còn thiếu|phải mua thêm|thêm.*đủ)\b" : @"\b(?:more.*need|additional.*need|need.*more|short)\b");
        bool neutral = !remainingQ && !originalQ && !eachQ && !moreQ && !needQ;
        bool targetName = d.Question.Contains("{name}") && !d.Question.Contains("{other}");
        bool lead(string cue) => Has(d.SolutionLead!, cue);
        return s switch
        {
            BasicQuestionStructure.Increase => Owns(d.GivenA, name, @"\{a\}") && ActorBefore(d.GivenB, name, @"\{b\}")
                && Gain(d.GivenB) && !Loss(d.GivenB) && targetName && totalQ && neutral
                && lead(vi ? "tất cả|tổng" : "total|altogether"),
            BasicQuestionStructure.Combine => Owns(d.GivenA, name, @"\{a\}") && Owns(d.GivenB, other, @"\{b\}")
                && !Gain(d.GivenB) && !Loss(d.GivenB) && Both(d.Question) && totalQ && neutral
                && lead(vi ? "tổng|tất cả|cả" : "total|altogether"),
            BasicQuestionStructure.RecoverInitial => Owns(d.GivenA, name, @"\{a\}")
                && Has(d.GivenA, vi ? "còn" : "left|remaining") && ActorBefore(d.GivenB, name, @"\{b\}")
                && Loss(d.GivenB) && !Gain(d.GivenB) && targetName && originalQ && !remainingQ
                && lead(vi ? "lúc đầu|ban đầu" : "original|initial"),
            BasicQuestionStructure.Remaining => Owns(d.GivenA, name, @"\{a\}") && ActorBefore(d.GivenB, name, @"\{b\}")
                && Loss(d.GivenB) && !Gain(d.GivenB) && targetName && remainingQ && !originalQ
                && lead(vi ? "còn" : "left|remaining"),
            BasicQuestionStructure.Difference => Owns(d.GivenA, name, @"\{a\}") && Owns(d.GivenB, other, @"\{b\}")
                && Both(d.Question) && moreQ && !remainingQ && !originalQ && d.Question.IndexOf("{name}", StringComparison.Ordinal) < d.Question.IndexOf("{other}", StringComparison.Ordinal)
                && lead(vi ? "hơn|chênh lệch" : "difference|more"),
            BasicQuestionStructure.MissingPart => ActorBefore(d.GivenA, name, @"\{a\}") && Has(d.GivenA, vi ? "cần|dự định|muốn" : "needs?|plans?|wants?")
                && Owns(d.GivenB, name, @"\{b\}") && !Gain(d.GivenB) && !Loss(d.GivenB) && targetName && needQ
                && lead(vi ? "thêm|thiếu" : "additional|more|short"),
            BasicQuestionStructure.EqualGroups => eachA && Owns(d.GivenB, name, @"\{b\}") && !equalB
                && targetName && totalQ && neutral && lead(vi ? "tổng|tất cả" : "total|altogether"),
            BasicQuestionStructure.EqualShare => Owns(d.GivenA, name, @"\{a\}") && ActorBefore(d.GivenB, name, @"\{b\}") && groupsB && (equalB || equalA)
                && eachQ && d.Question.Contains("{group_one}") && !remainingQ && !originalQ && !d.Question.Contains("{other}")
                && lead(vi ? "mỗi" : "each|every|per"),
            BasicQuestionStructure.CountGroups => Owns(d.GivenA, name, @"\{a\}") && d.GivenB.Contains("{name}") && eachB
                && targetName && !eachQ && !remainingQ && !originalQ,
            BasicQuestionStructure.TimesAsMany => Owns(d.GivenA, other, @"\{a\}") && TimesRelation(d.GivenB, name, other, vi)
                && targetName && neutral && !Has(d.SolutionLead!, vi ? "hơn|còn|lúc đầu" : "difference|remaining|original"),
            BasicQuestionStructure.TimesFewer => Owns(d.GivenA, other, @"\{a\}") && TimesRelation(d.GivenB, other, name, vi)
                && targetName && neutral && !Has(d.SolutionLead!, vi ? "hơn|còn|lúc đầu" : "difference|remaining|original"),
            _ => false
        };
    }

    private static bool TimesRelation(string text, string larger, string smaller, bool vi)
    {
        const string between = @"(?:[^{}]|\{(?:unit|group|group_one)\})*";
        return Has(text, larger + between + (vi ? @"gấp\s+\{b\}\s+lần" : @"\{b\}\s+times") + between + smaller);
    }

    private static bool ContextMatches(BasicQuestionDraft draft, BasicQuestionContract c, QuestionUnit unit, bool vi)
    {
        string[] fields = [draft.GivenA, draft.GivenB, draft.Question, draft.SolutionLead!];
        bool comparison = c.Structure is BasicQuestionStructure.TimesAsMany or BasicQuestionStructure.TimesFewer;
        if (Has(draft.GivenA + " " + draft.GivenB, vi ? @"\b(?:hỏi|bao nhiêu)\b" : @"\b(?:how many|what|why)\b")) return false;
        if (Count(draft.GivenA, "{unit}") != 1 || Count(draft.Question, "{unit}") != (c.Structure == BasicQuestionStructure.CountGroups ? 0 : 1)) return false;
        if (comparison)
        {
            string larger = c.Structure == BasicQuestionStructure.TimesFewer ? @"\{other\}" : @"\{name\}";
            string smaller = c.Structure == BasicQuestionStructure.TimesFewer ? @"\{name\}" : @"\{other\}";
            // Match a complete comparison clause, not an isolated 'gấp b lần'
            // substring buried in an explanation of template placeholders.
            string relation = vi ? larger + @"\s+(?:có|sở hữu|giữ)\s+(?:số(?: lượng)?\s+)?\{unit\}\s+(?:nhiều\s+)?gấp\s+\{b\}\s+lần\s+(?:số(?: lượng)?\s+)?(?:\{unit\}\s+)?(?:của\s+)?" + smaller
                : larger + @"\s+(?:has|owns|holds)\s+(?:(?:currently|now)\s+)?\{b\}\s+times\s+as\s+many\s+\{unit\}\s+as\s+" + smaller;
            if (!Has(draft.GivenB.Trim(), @"\A" + relation + @"\s*[.]?\z")) return false;
        }
        string actorA = comparison ? "{other}" : "{name}";
        string actorB = c.Structure is BasicQuestionStructure.Difference or BasicQuestionStructure.TimesFewer ? "{other}" : "{name}";
        if (c.Structure == BasicQuestionStructure.EqualGroups
            ? Count(draft.GivenA, "{name}") + Count(draft.GivenA, "{other}") != 0
            : Count(draft.GivenA, actorA) != 1 || Count(draft.GivenA, actorA == "{name}" ? "{other}" : "{name}") != 0) return false;
        if (comparison ? Count(draft.GivenB, "{name}") != 1 || Count(draft.GivenB, "{other}") != 1
            : Count(draft.GivenB, actorB) != 1 || Count(draft.GivenB, actorB == "{name}" ? "{other}" : "{name}") != 0) return false;
        // Object words are bound through {unit}; importing a correctly labelled
        // template must not smuggle a second, incompatible object into the prose.
        foreach (var u in QuestionUnits.All.Concat(AdditionQuestionCatalogue.ExtraUnits))
            foreach (string noun in vi ? new[] { u.Vietnamese } : new[] { u.Singular, u.Plural })
                if (noun != "can" && fields.Any(s => Has(s, @"\b" + Regex.Escape(noun) + @"\b"))) return false;
        bool grouping = c.Structure is BasicQuestionStructure.EqualGroups or BasicQuestionStructure.EqualShare or BasicQuestionStructure.CountGroups;
        if (!grouping && fields.Any(s => s.Contains("{group}") || s.Contains("{group_one}"))) return false;
        if (c.Structure == BasicQuestionStructure.CountGroups && (!Has(draft.SolutionLead!, vi
            ? @"(?:số|tổng).*\{group\}" : @"(?:number|total).*\{group\}")
            || Has(draft.SolutionLead!, vi ? @"\bmỗi\b" : @"\b(?:each|per|every)\b"))) return false;
        if (c.Structure == BasicQuestionStructure.EqualGroups && !Has(draft.GivenA, vi
            ? @"(?:chứa|có|đựng|gồm).*\{a\}" : @"(?:holds?|contains?|has|includes?).*\{a\}")) return false;
        if (c.Structure == BasicQuestionStructure.Remaining)
        {
            bool food = c.SceneId is "family-gifts" or "food-supplies" or "shop-stock" or "bakery" or "harvest" or "crop-harvest";
            bool edible = unit.Id is "apples" or "oranges" or "mangoes" or "candies" or "cakes" or "bread-rolls";
            if ((!food || !edible) && Has(draft.GivenB, vi ? @"\b(?:ăn|uống)\b" : @"\b(?:eats?|ate|drinks?|drank)\b")) return false;
        }
        // Animal/plant groups are herds, rearing areas or nursery sections, never packed in boxes.
        if (unit.Id is "chickens" or "ducks" or "cows" or "fish" or "trees" or "seedlings"
            && grouping && Has(draft.GivenB, vi ? @"\b(?:đóng gói|đóng hộp)\b" : @"\b(?:packs?|packed|boxes?|boxed)\b")) return false;
        return true;
    }
}
