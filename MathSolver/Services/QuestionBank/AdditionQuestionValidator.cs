using MathSolver.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Validates bounded one-step addition templates against C# scene and relationship roles.</summary>
public static class AdditionQuestionValidator
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private static readonly string[] Fields = ["given_a", "given_b", "question", "solution_lead", "unit_id"];
    private static readonly string[] Tokens = ["{name}", "{other}", "{a}", "{b}", "{unit}", "{part_a}", "{part_b}"];
    private const string Between = @"(?:[^{}]|\{unit\})*";
    private static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
    private static int Count(string text, string token) => (text.Length - text.Replace(token, "").Length) / token.Length;

    public static BasicDraftValidation Validate(string rawJson, BasicQuestionContract c)
    {
        if (!c.IsValid || c.Version != AdditionQuestionCatalogue.Version) return new(null, "InvalidContract");
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
            if (properties.Length != Fields.Length || properties.Any(p => !Fields.Contains(p.Name) || p.Value.ValueKind != JsonValueKind.String)
                || properties.Select(p => p.Name).Distinct().Count() != Fields.Length) return new(null, "InvalidFields");
            string Field(string key) => document.RootElement.GetProperty(key).GetString()!;
            var d = new BasicQuestionDraft(Field("given_a"), Field("given_b"), Field("question"), Field("solution_lead"), Field("unit_id"));
            var languageError = QuestionProseLanguage.ValidateAndNormalize(d, c.Language, out d);
            if (languageError is not null) return new(null, languageError);
            var scene = AdditionQuestionCatalogue.Find(c.SceneId)!;
            var scale = scene.Scale(c.Tier)!;
            var unit = QuestionUnits.Find(d.UnitId!);
            if (unit is null || !scale.UnitIds.Contains(unit.Id)) return new(null, "ChangedUnits");
            c = BasicQuestionTemplates.ApplyUnit(c, unit);
            if (!c.IsValid) return new(null, "InvalidContract");
            string[] text = [d.GivenA, d.GivenB, d.Question, d.SolutionLead!];
            if (Has(d.GivenA + " " + d.GivenB, c.Language == AppLanguage.Vietnamese
                ? @"\b(?:lũy kế|bao gồm|đã tính|tính cả|trong đó)\b"
                : @"\b(?:cumulative|including|includes?|already counted|of which|so far)\b"))
                return new(null, "ExtraRelations");
            foreach (string s in text)
            {
                if (s.Length is < 10 or > 700 || s.Any(char.IsControl)) return new(null, "InvalidText");
                var slots = Regex.Matches(s, @"\{[^{}]*\}", RegexOptions.CultureInvariant, Timeout);
                if (slots.Any(m => !Tokens.Contains(m.Value))
                    || Regex.Replace(s, @"\{[^{}]*\}", "", RegexOptions.CultureInvariant, Timeout).IndexOfAny(['{', '}']) >= 0)
                    return new(null, "InvalidPlaceholders");
                if (Has(s, @"\d|[=<>%+×÷]|\b(?:minus|plus|multiplied by|divided by)\b")) return new(null, "ChangedQuantities");
                // A given is one fact, the question only asks, and the lead does
                // not include a worked solution. Keep this independent of native
                // grammar: imports and alternative runtimes use the same validator.
                if (Has(s.Trim().TrimEnd('.', '?', '!', ':'), @"[.?!:;]")) return new(null, "InvalidText");
                if (Has(s, @"\b(?:and|but|while|when|because|và|nhưng|vì|khi)\s*[,.:?!]*\s*$"))
                    return new(null, "InvalidText");
                // Supported stock verbs/adverbs are words, not a lone letter
                // before the count. A live GGUF returned 'có ộ {a} {unit}'.
                if (scene.Kind == AdditionSceneKind.Stock && Has(s, @"(?<!\p{L})\p{L}\s+\{[ab]\}"))
                    return new(null, "InvalidText");
                if (Count(s, "{name}") > 1 || Count(s, "{other}") > 1
                    || Count(s, "{unit}") > (s == d.GivenB && c.Structure is
                        BasicQuestionStructure.AddComparisonMore or BasicQuestionStructure.AddComparisonInverse ? 2 : 1))
                    return new(null, "InvalidPlaceholders");
                string unitWords = string.Join("|", QuestionUnits.All.Concat(AdditionQuestionCatalogue.ExtraUnits)
                    .Select(u => c.Language == AppLanguage.Vietnamese ? u.Vietnamese : u.Plural)
                    .Append(c.Language == AppLanguage.English ? unit.Singular : unit.Vietnamese)
                    .Distinct().OrderByDescending(s => s.Length).Select(Regex.Escape));
                if (Has(s, @"\{unit\}\s+(?:" + unitWords + @")\b")) return new(null, "ChangedUnits");
                string prose = Regex.Replace(s, @"\b(?:một hôm|một ngày nọ|one day)\b", "", RegexOptions.IgnoreCase, Timeout);
                // 'Cả hai nhóm' describes the already-bound operands, not a third numeric fact.
                if (c.Structure == BasicQuestionStructure.Combine)
                    prose = Regex.Replace(prose, @"\b(?:cả hai|hai nhóm|hai buổi|hai luống|hai thư viện|both|two groups|two periods|two rows|two teams)\b", "", RegexOptions.IgnoreCase, Timeout);
                if (c.Structure == BasicQuestionStructure.Combine && scene.Kind is AdditionSceneKind.Periods or AdditionSceneKind.Parts)
                    prose = Regex.Replace(prose, @"\b(?:hai ngày|hai tuần|hai tháng|hai khu|two days|two weeks|two months|two sections)\b", "", RegexOptions.IgnoreCase, Timeout);
                if (Has(prose, @"\b(?:một|hai|ba|bốn|năm|sáu|bảy|tám|chín|mười|one|two|three|four|five|six|seven|eight|nine|ten|half|nửa|gấp đôi)\b"))
                    return new(null, "ChangedQuantities");
                // English contractions still negate the fact even when a valid
                // action verb follows them (e.g. "don't bring {a} {unit}").
                if (Has(s, @"\b(?:không|chẳng|chưa|not|never|cannot|no|without|[a-z]+n['’]t|except|trừ khi|mỗi|each|every|times)\b|gấp\s+(?:\{[ab]\}|đôi|\S+\s+lần)")) return new(null, "ExtraRelations");
                if (Has(s, @"\b(?:bạn|bé|ông|bà|cô|chú|bác|anh|chị|mẹ|cha|dì|cậu|mợ|học sinh|Grandma|Grandpa|Uncle|Aunt|pupil|child)\s+\{(?:name|other)\}"))
                    return new(null, "InvalidContext");
                if (c.Language == AppLanguage.English && Has(s,
                    @"\{(?:name|other)\}['’]s\s+(?:(?:craft|wholesale|notebook|seedling|book|volunteer|aid|collection|recycling)\s+){0,2}(?:group|team|family|school|library|bookshop|shop|store|warehouse|workshop|factory|nursery|cooperative|organisation|organization)\b"))
                    return new(null, "InvalidContext");
            }
            if (Count(d.GivenA, "{a}") != 1 || Count(d.GivenB, "{b}") != 1
                || Count(d.GivenA, "{b}") != 0 || Count(d.GivenB, "{a}") != 0
                || Has(d.Question + d.SolutionLead, @"\{[ab]\}")) return new(null, "InvalidPlaceholders");
            if (!Has(d.GivenA, @"\{a\}\s+\{unit\}") || !Has(d.GivenB, @"\{b\}\s+(?:(?:more|fewer|additional|new)\s+)?\{unit\}")
                || !d.Question.Contains("{unit}") || !d.SolutionLead!.Contains("{unit}")) return new(null, "ChangedUnits");
            bool vi = c.Language == AppLanguage.Vietnamese;
            if (!Has(d.Question, vi ? @"\b(?:hỏi|bao nhiêu|tính|tìm|cho biết|xác định)\b" : @"\b(?:how many|what|find|calculate|determine)\b")
                || vi && !d.ProblemText.Any(ch => ch > 127)) return new(null, "WrongLanguage");
            bool parts = scene.Kind is AdditionSceneKind.Periods or AdditionSceneKind.Parts;
            if (parts ? Count(d.GivenA, "{part_a}") != 1 || Count(d.GivenB, "{part_b}") != 1
                    || d.GivenA.Contains("{part_b}") || d.GivenB.Contains("{part_a}")
                : text.Any(s => s.Contains("{part_a}") || s.Contains("{part_b}"))) return new(null, "InvalidPlaceholders");
            if (parts && !MatchesPartsScale(text, scene, scale, vi)) return new(null, "InvalidContext");
            if (!RolesMatch(d, c, scene, vi)) return new(null, "ChangedRelationOrTarget");
            return new(d, null, c);
        }
        catch (JsonException) { return new(null, "InvalidJson"); }
        catch (InvalidOperationException) { return new(null, "InvalidJson"); }
        catch (RegexMatchTimeoutException) { return new(null, "InvalidText"); }
    }

    private static bool MatchesPartsScale(string[] text, AdditionScene scene, AdditionQuestionScale scale, bool vi)
    {
        string prose = Regex.Replace(string.Join(" ", text), @"\{[^{}]*\}", "", RegexOptions.CultureInvariant, Timeout);
        // The named periods/sections are bound by C#. Literal changes to their
        // kind would shrink a monthly factory result to one morning, for example.
        string[] kinds = scene.Kind == AdditionSceneKind.Periods
            ? vi ? ["buổi", "ngày", "tuần", "tháng", "hiệp"] : ["morning", "afternoon", "day", "days", "week", "weeks", "month", "months", "half", "halves"]
            : vi ? ["luống", "khu"] : ["row", "rows", "section", "sections"];
        string allowed = string.Join(" ", new[] { scale.Parts(vi ? AppLanguage.Vietnamese : AppLanguage.English).A,
            scale.Parts(vi ? AppLanguage.Vietnamese : AppLanguage.English).B, scale.Span(vi ? AppLanguage.Vietnamese : AppLanguage.English) });
        return kinds.All(kind => !Has(prose, @"\b" + kind + @"\b") || Has(allowed, @"\b" + kind + @"\b"));
    }

    private static bool RolesMatch(BasicQuestionDraft d, BasicQuestionContract c, AdditionScene scene, bool vi)
    {
        string name = @"\{name\}", other = @"\{other\}";
        string verbs = vi ? scene.VietnameseVerbs + (scene.Kind == AdditionSceneKind.Stock ? "|còn lại|còn|sở hữu" : "")
            : scene.EnglishVerbs + (scene.Kind == AdditionSceneKind.Stock ? "|owns" : "");
        // Reaping is appropriate for rice, not mangoes. Accept the natural
        // activity synonym only for the matching object chosen by C#.
        if (vi && scene.Id == "crop-harvest" && QuestionUnits.Find(c)?.Id == "rice-sacks") verbs += "|gặt";
        bool Fact(string s, string actor, string quantity) => Has(s, actor + Between + @"\b(?:" + verbs + @")\b" + Between + quantity)
            || Has(s, quantity + Between + @"\b(?:" + verbs + @")\b" + Between + actor);
        bool Both(string s) => s.Contains("{name}") && s.Contains("{other}");
        bool OnlyName(string s) => s.Contains("{name}") && !s.Contains("{other}");
        bool total(string s) => Has(s, vi ? @"\b(?:tổng|tất cả|cộng lại|cả hai)\b" : @"\b(?:total|altogether|in all|together|combined)\b")
            || scene.Kind == AdditionSceneKind.Periods && Has(s, vi
                ? @"\b(?:qua|trong|của)\s+(?:các|cả hai|hai)\s+(?:buổi|ngày|tuần|tháng|hiệp)\b"
                : @"\b(?:over|across|during|in)\s+(?:these|both|the two)\s+(?:periods|days|weeks|months|halves)\b");
        bool original(string s) => Has(s, vi ? @"\b(?:ban đầu|lúc đầu|trước khi)\b" : @"\b(?:original|originally|initial|initially|before|at the start|at first|in the beginning|to begin with|(?:start(?:ed)?|begin|began)(?: off| out)? with)\b");
        bool remaining(string s) => Has(s, vi ? @"\bcòn(?: lại)?\b" : @"\b(?:left|remaining|remain)\b");
        bool comparison(string s) => Has(s, vi ? @"\b(?:hơn|kém|chênh lệch|thiếu|cần thêm)\b" : @"\b(?:more|fewer|difference|short|additional)\b");
        bool loss(string s) => Has(Regex.Replace(s, @"\b(?:được tặng|được cho|is given|is gifted)\b", "", RegexOptions.IgnoreCase, Timeout),
            vi ? @"\b(?:cho đi|cho tặng|tặng|bán|mất|bớt|dùng|ăn|lấy ra|chuyển đi)\b" : @"\b(?:gives?|gave|given away|sells?|sold|loses?|lost|removes?|removed|uses?|used|eats?|ate|eaten|donates?|donated)\b");
        bool neutral = !original(d.Question) && !remaining(d.Question) && !comparison(d.Question);
        string lead = d.SolutionLead!;
        bool correctLead = c.Structure == BasicQuestionStructure.Combine && scene.Kind is not (AdditionSceneKind.Periods or AdditionSceneKind.Parts)
            ? lead.Contains("{name}") == lead.Contains("{other}") : !lead.Contains("{other}");
        if (!correctLead || Has(d.GivenA + d.GivenB, vi ? @"\b(?:chênh lệch|cần thêm|thiếu)\b" : @"\b(?:difference|needs? more|short)\b")) return false;
        // An explicit partial-period/row question changes the target from the combined amount.
        if (scene.Kind is AdditionSceneKind.Periods or AdditionSceneKind.Parts)
        {
            bool paired(string s) => s.Contains("{part_a}") == s.Contains("{part_b}");
            if (!paired(d.Question) || !paired(lead)) return false;
        }
        switch (c.Structure)
        {
            case BasicQuestionStructure.Increase:
                if (!OnlyName(d.Question) || !neutral || !total(d.Question) || !total(lead) || original(lead) || remaining(lead) || comparison(lead)) return false;
                if (scene.Kind == AdditionSceneKind.Arrivals)
                {
                    string atPlace = vi ? @"\b(?:ở|tại|trong|đến|tới|vào|tham gia)\s+" + name : @"\b(?:at|in|into|to|join)\s+" + name;
                    bool PlaceOwnsAmount(string s, string quantity) => Has(s, name + Between
                        + (vi ? @"\b(?:có|chứa)\b" : @"\b(?:has|contains)\b") + Between + quantity);
                    return OnlyName(d.GivenA) && Has(d.GivenA, vi ? "có|chứa|đang đậu|đang tham gia" : "there are|has|contains|are at")
                        && (Has(d.GivenA, atPlace) || PlaceOwnsAmount(d.GivenA, @"\{a\}"))
                        && OnlyName(d.GivenB) && Has(d.GivenB, @"\b(?:" + verbs + @")\b") && !loss(d.GivenB)
                        && (Has(d.GivenB, atPlace) || PlaceOwnsAmount(d.GivenB, @"\{b\}"));
                }
                return Fact(d.GivenA, name, @"\{a\}") && !d.GivenA.Contains("{other}") && !remaining(d.GivenA)
                    && Has(d.GivenB, name + Between + (vi ? @"\b(?:nhận|mua|nhập|được tặng|được cho|bổ sung|thêm)\b" : @"(?:receives?|received|gets?|got|buys?|bought|adds?|added|is given)") + Between + @"\{b\}")
                    && !d.GivenB.Contains("{other}") && !loss(d.GivenB)
                    && !Has(d.GivenB, vi ? @"\b(?:hơn|kém)\b" : @"\bthan\b");
            case BasicQuestionStructure.Combine:
                if (!neutral || !total(d.Question) || !total(lead) || original(lead) || remaining(lead) || comparison(lead)) return false;
                if (scene.Kind is AdditionSceneKind.Periods or AdditionSceneKind.Parts)
                    return OnlyName(d.GivenA) && OnlyName(d.GivenB) && OnlyName(d.Question)
                        && (scene.Kind == AdditionSceneKind.Periods
                            ? Has(d.GivenA, name + Between + @"\b(?:" + verbs + @")\b" + Between + @"\{a\}")
                                && Has(d.GivenB, name + Between + @"\b(?:" + verbs + @")\b" + Between + @"\{b\}")
                            : SpatialPart(d.GivenA, "{part_a}", "{a}") && SpatialPart(d.GivenB, "{part_b}", "{b}"))
                        && !comparison(d.GivenA + d.GivenB) && !remaining(d.GivenA + d.GivenB)
                        && (scene.Kind != AdditionSceneKind.Periods || !Has(d.Question, vi ? "có|còn" : @"\bhave|has|left\b"));
                return Fact(d.GivenA, name, @"\{a\}") && !d.GivenA.Contains("{other}")
                    && Fact(d.GivenB, other, @"\{b\}") && !d.GivenB.Contains("{name}") && Both(d.Question)
                    && !comparison(d.GivenA + d.GivenB) && !remaining(d.GivenA + d.GivenB)
                    && (scene.Kind != AdditionSceneKind.Contributions || Has(d.Question, @"\b(?:" + verbs + @")\b"));
            case BasicQuestionStructure.RecoverInitial:
                return OnlyName(d.GivenA) && remaining(d.GivenA) && Fact(d.GivenA, name, @"\{a\}")
                    && OnlyName(d.GivenB) && loss(d.GivenB) && Has(d.GivenB, name + Between + @"\{b\}")
                    && !comparison(d.GivenA + d.GivenB)
                    && OnlyName(d.Question) && original(d.Question) && !remaining(d.Question) && !comparison(d.Question)
                    && original(lead) && !remaining(lead) && !comparison(lead);
            case BasicQuestionStructure.AddComparisonMore:
            case BasicQuestionStructure.AddComparisonInverse:
                string first = c.Structure == BasicQuestionStructure.AddComparisonMore ? name : other;
                string second = c.Structure == BasicQuestionStructure.AddComparisonMore ? other : name;
                string cue = vi ? c.Structure == BasicQuestionStructure.AddComparisonMore ? @"(?:nhiều hơn|hơn)" : @"(?:ít hơn|kém)"
                    : c.Structure == BasicQuestionStructure.AddComparisonMore ? "more" : "fewer";
                bool direction = vi ? Has(d.GivenB, first + Between + cue + Between + second)
                    : Has(d.GivenB, first + Between + @"\{b\}" + Between + cue + Between + @"\bthan\b" + Between + second);
                if (c.Structure == BasicQuestionStructure.AddComparisonMore && Has(d.GivenB, vi ? @"\b(?:ít hơn|kém)\b" : @"\bfewer\b")) direction = false;
                return Fact(d.GivenA, other, @"\{a\}") && !d.GivenA.Contains("{name}") && Both(d.GivenB) && direction
                    && (scene.Kind != AdditionSceneKind.Contributions || Has(d.GivenB, first + Between + @"\b(?:" + verbs + @")\b"))
                    && OnlyName(d.Question) && neutral
                    && !original(lead) && !remaining(lead) && !comparison(lead)
                    && !Has(d.Question + lead, vi ? @"\b(?:các nhóm|cả hai|cộng lại)\b" : @"\b(?:both|combined|together)\b")
                    && (scene.Kind != AdditionSceneKind.Contributions || Has(d.Question, @"\b(?:" + verbs + @")\b"));
            default: return false;
        }
    }

    private static bool SpatialPart(string text, string part, string quantity)
    {
        string name = @"\{name\}", p = Regex.Escape(part), q = Regex.Escape(quantity);
        // The garden is the location of the row, not a counted object placed
        // after 'has'. Also allow the natural garden-first sentence order.
        return Has(text, p + Between + @"\b(?:trong|của|thuộc|in|of)\s+" + name + Between
            + @"\b(?:có|trồng|mọc|has|contains|grows|planted)\b" + Between + q)
            || Has(text, name + Between + p + Between + @"\b(?:có|trồng|mọc|has|contains|grows|planted)\b" + Between + q);
    }
}
