using MathSolver.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

public sealed record BasicQuestionDraft(string GivenA, string GivenB, string Question, string? SolutionLead = null, string? UnitId = null)
{
    public string ProblemText => $"{GivenA.Trim()} {GivenB.Trim()} {Question.Trim()}";
    public MathWordProblem ToWordProblem(BasicQuestionContract c) => new(c.IsTemplate
        ? BasicQuestionTemplates.RenderProblem(GivenA, GivenB, Question, c) : ProblemText,
        c.IsTemplate ? BasicQuestionTemplates.Render(SolutionLead!, c) : c.SolutionLead, c.AnswerUnit, c.Subject,
        c.Version == FindXQuestionCatalogue.Version ? AppliedQuestionCatalogue.Quantity(FindXQuestionCatalogue.AsApplied(c))
            : c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.Quantity(c) : WordProblemQuantity.Unspecified,
        c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.ConversionStep(c) : null,
        c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.Reasoning(c) : null,
        c.Version == FindXQuestionCatalogue.Version ? AppliedQuestionCatalogue.FactTable(FindXQuestionCatalogue.AsApplied(c))
            : c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.FactTable(c) : null);
}

public sealed record BasicDraftValidation(BasicQuestionDraft? Draft, string? ErrorCode, BasicQuestionContract? Contract = null)
{
    public bool IsValid => Draft is not null && ErrorCode is null;
}

/// <summary>Bounded first-version arithmetic prose validation, independent of model facts.</summary>
public static class LegacyBasicQuestionValidator
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex Digits = new(@"\d+", RegexOptions.CultureInvariant, Timeout);

    public static BasicDraftValidation Validate(string rawJson, BasicQuestionContract contract)
    {
        if (!contract.IsValid) return new(null, "InvalidContract");
        if (string.IsNullOrWhiteSpace(rawJson) || rawJson.Length > 12_000) return new(null, "InvalidJson");
        try
        {
            string json = rawJson.Trim();
            if (json.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLine = json.IndexOf('\n');
                int fence = json.LastIndexOf("```", StringComparison.Ordinal);
                if (firstLine < 0 || fence <= firstLine) return new(null, "InvalidJson");
                json = json[(firstLine + 1)..fence].Trim();
            }
            using var document = JsonDocument.Parse(json, new() { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new(null, "InvalidJson");
            var fields = root.EnumerateObject().ToArray();
            string[] required = ["given_a", "given_b", "question"];
            if (fields.Length != 3 || fields.Any(p => !required.Contains(p.Name))
                || fields.Select(p => p.Name).Distinct().Count() != 3
                || fields.Any(p => p.Value.ValueKind != JsonValueKind.String)) return new(null, "InvalidFields");
            var draft = new BasicQuestionDraft(root.GetProperty("given_a").GetString()!,
                root.GetProperty("given_b").GetString()!, root.GetProperty("question").GetString()!);
            var languageError = QuestionProseLanguage.ValidateAndNormalize(draft, contract.Language, out draft);
            if (languageError is not null) return new(null, languageError);
            if (new[] { draft.GivenA, draft.GivenB, draft.Question }.Any(s => s.Length is < 10 or > 700
                || s.Any(c => char.IsControl(c) && c is not '\r' and not '\n'))) return new(null, "InvalidText");
            string a = Normalize(draft.GivenA), b = Normalize(draft.GivenB), q = Normalize(draft.Question);
            if (!OneQuantity(a, contract.Left) || !OneQuantity(b, contract.Right) || Digits.IsMatch(q))
                return new(null, "ChangedQuantities");
            if (new[] { a, b, q }.Any(s => s.Contains('=') || s.Contains('%') || s.Contains('<') || s.Contains('>')))
                return new(null, "ExtraRelations");
            bool vi = contract.Language == AppLanguage.Vietnamese;
            string unit = vi ? @"\b(?:quyen|cuon) sach\b" : @"\bbooks?\b";
            string group = vi ? @"\bthung(?: sach)?\b" : @"\bbox(?:es)?\b";
            string subject = Normalize(contract.Subject);
            bool subjectA = contract.Operation == ArithmeticOperation.Multiply || Has(a, Regex.Escape(subject));
            bool targetSubject = Has(q, Regex.Escape(subject)) || IsNeutralQuestion(q, contract.Operation, vi);
            if (!subjectA || !Has(b, Regex.Escape(subject)) || !targetSubject) return new(null, "ChangedSubject");
            string questionWithoutSubject = q.Replace(subject, "");
            if (!Has(a, unit) || !Has(questionWithoutSubject, vi ? @"\bsach\b" : unit) || !Has(b, contract.Operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract ? unit : group))
                return new(null, "ChangedUnits");
            string itemQuantity = vi ? unit : @"(?:more\s+|additional\s+|new\s+)?" + unit;
            string groupQuantity = vi ? group : @"(?:identical\s+|equal\s+|such\s+)?" + group;
            if (!Has(a, @"\b" + contract.Left + @"\s+" + itemQuantity)
                || !Has(b, @"\b" + contract.Right + @"\s+" + (contract.Operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract ? itemQuantity : groupQuantity)))
                return new(null, "ChangedUnits");
            // Preserve diacritics for relation words: bạn != bán, chứa != chưa,
            // gặp != gấp. Accent folding is only used for subjects/quantity units.
            string relationA = draft.GivenA.ToLowerInvariant(), relationB = draft.GivenB.ToLowerInvariant(), relationQ = draft.Question.ToLowerInvariant();
            string actor = @"\b" + Regex.Escape(contract.Subject.ToLowerInvariant()) + @"\b";
            string auxiliaries = vi ? @"\s+(?:(?:hiện|đang|vừa|lại|cũng|đã)\s+){0,3}" : @"\s+(?:(?:currently|now|already|also|then)\s+){0,3}";
            string owns = vi ? @"(?:có|giữ|sở hữu)\b" : @"(?:has|holds|owns)\b";
            // A name somewhere in the sentence is insufficient: bind quantities
            // to the subject who owns, gains, loses or groups the books.
            bool owner = contract.Operation == ArithmeticOperation.Multiply
                ? Has(relationA, vi ? @"\bmỗi\s+thùng\s+(?:sách\s+)?(?:chứa|có|đựng)\b" : @"\b(?:each|every)\s+box\s+(?:contains|holds|has)\b")
                : Has(relationA, actor + auxiliaries + owns);
            if (!owner) return new(null, "ChangedRelationOrTarget");
            string adds = vi ? @"\b(nhận(?: được)?(?: thêm)?|mua(?: thêm)?|được tặng|thêm vào|nhập thêm|bổ sung)\b" : @"\b(receives?|gets?|buys?|adds?|gains?|is given)\b";
            string removes = vi ? @"\b(cho đi|cho tặng|(?<!được )tặng|bán|lấy ra|chuyển đi|xuất|mất|bớt)\b" : @"\b(gives? away|gives?|sells?|removes?|loses?|sends?|donates?)\b";
            string each = vi ? @"\bmỗi\b" : @"\b(each|every|per)\b";
            string total = vi ? @"\b(tất cả|tổng|cả|bao nhiêu)\b" : @"\b(total|altogether|in all|how many)\b";
            string remaining = vi ? @"\bcòn(?: lại)?\b" : @"\b(left|remaining|remain(?:s|ing)?)\b";
            string equally = vi ? @"\b(chia đều|xếp đều|phân đều|chia.*như nhau)\b" : @"\b(equally|evenly|equal)\b";
            bool totalTarget = IsNeutralQuestion(q, contract.Operation, vi)
                || Has(relationQ, actor + auxiliaries + owns)
                || Has(relationQ, vi ? @"\b(?:tổng|tất cả)\s+(?:số\s+)?(?:(?:quyển|cuốn)\s+)?sách\s+(?:của\s+)?" + actor
                    : @"\b(?:total|all)\s+(?:number of\s+)?books\s+(?:of|belonging to)\s+" + actor)
                || !vi && Has(relationQ, @"\bhow many books\s+(?:does|has)\s+" + actor + @"\s+(?:have|got)\b");
            bool relation = contract.Operation switch
            {
                ArithmeticOperation.Add => Has(relationB, actor + auxiliaries + adds) && !Has(relationB, removes) && totalTarget && Has(relationQ, total) && !Has(relationQ, remaining),
                ArithmeticOperation.Subtract => Has(relationB, actor + auxiliaries + removes) && !Has(relationB, adds) && Has(relationQ, remaining),
                ArithmeticOperation.Multiply => Has(relationA, each) && Has(a, group) && Has(relationQ, total)
                    && totalTarget
                    && Has(relationB, actor + auxiliaries + owns)
                    && !Has(relationQ, remaining) && !Has(relationB, equally) && !Has(relationQ, each),
                _ => Has(relationB, actor + auxiliaries + (vi ? @"(?:chia|xếp|phân)\b" : @"(?:distributes|divides|splits|packs|puts)\b"))
                    && Has(relationB, equally) && Has(relationQ, each) && Has(q, group) && !Has(relationQ, remaining)
            };
            if (!relation) return new(null, "ChangedRelationOrTarget");
            if (vi && (!Has(q, @"\b(hoi|bao nhieu)\b") || !draft.ProblemText.Any(c => c > 127)))
                return new(null, "WrongLanguage");
            if (!vi && !Has(q, @"\b(how many|what|find)\b")) return new(null, "WrongLanguage");
            // Negation and additional comparison relationships change the contract.
            string negatives = vi ? @"\b(không|chưa|ít hơn|nhiều hơn|gấp|nửa|phần trăm)\b"
                : @"\b(not|never|fewer|less than|more than|twice|half|percent)\b";
            if (new[] { relationA, relationB, relationQ }.Any(s => Has(s, negatives))) return new(null, "ExtraRelations");
            string writtenQuantity = vi ? @"\b(?:một|hai|ba|bốn|năm|sáu|bảy|tám|chín|mười|vài)\s+(?:quyển|cuốn|thùng)\b"
                : @"\b(?:one|two|three|four|five|six|seven|eight|nine|ten|some)\s+(?:more\s+)?(?:books?|boxes?)\b";
            if (new[] { relationA, relationB, relationQ }.Any(s => Has(s, writtenQuantity))) return new(null, "ChangedQuantities");
            return new(draft, null);
        }
        catch (Exception error) when (error is JsonException or RegexMatchTimeoutException or InvalidOperationException)
        { return new(null, "InvalidJson"); }
    }

    private static bool OneQuantity(string text, int expected)
    {
        var values = Digits.Matches(text);
        return values.Count == 1 && int.TryParse(values[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int actual)
            && actual == expected && !Has(text, @"\d[.,/]\d");
    }

    private static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, Timeout);

    private static bool IsNeutralQuestion(string question, ArithmeticOperation operation, bool vi)
    {
        // With one C# owner and no other named actor, natural questions may refer
        // to the already described books/boxes without repeating that owner's name.
        string pattern = operation switch
        {
            ArithmeticOperation.Divide => vi
                ? @"^(?:hoi\s+)?moi thung(?: sach)?\s+(?:co|chua|dung)\s+bao nhieu\s+(?:(?:quyen|cuon)\s+)?sach[?!.]?$"
                : @"^how many books are (?:there )?in each box[?!.]?$",
            ArithmeticOperation.Subtract => vi
                ? @"^(?:hoi\s+)?(?:con(?: lai)?\s+bao nhieu\s+(?:(?:quyen|cuon)\s+)?sach|so\s+(?:(?:quyen|cuon)\s+)?sach con(?: lai)?\s+la bao nhieu)[?!.]?$"
                : @"^how many books (?:are left|remain|are remaining)[?!.]?$",
            _ => vi
                ? @"^(?:hoi\s+)?(?:(?:co\s+)?tat ca\s+(?:co\s+)?bao nhieu\s+(?:(?:quyen|cuon)\s+)?sach|tong(?: so)?\s+(?:(?:quyen|cuon)\s+)?sach\s+la bao nhieu)[?!.]?$"
                : @"^how many books are (?:there )?(?:in total|altogether|in all)[?!.]?$"
        };
        return Has(question, pattern);
    }
    public static string Normalize(string text)
    {
        var result = new StringBuilder();
        foreach (char c in text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(c);
        return Regex.Replace(result.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }
}
