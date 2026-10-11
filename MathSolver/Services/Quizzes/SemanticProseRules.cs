using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Checks ordered semantic anchors rather than exact approved sentences.
/// Only neutral linking language and reviewed synonyms may change around the anchors.</summary>
internal static class SemanticProseRules
{
    private static readonly Regex Tokens = new(@"\{[A-Za-z0-9_.-]+\}|[/%=+*\u00f7\u00d7<>]|[\p{L}\p{M}\p{N}]+", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly HashSet<string> VietnameseLinks = ["hỏi", "vậy", "thì", "là", "được", "các", "những", "này",
        "đó", "rằng", "theo", "bản", "dữ", "liệu", "bối", "cảnh", "ghi", "chép", "báo", "cáo", "kết", "quả", "trong", "hoạt", "động", "hôm", "nay"];
    private static readonly HashSet<string> EnglishLinks = ["the", "a", "an", "please", "find", "calculate", "is", "are",
        "was", "were", "these", "those", "according", "to", "records", "report", "during", "this", "activity", "today"];
    private static string[][] Synonyms(AppLanguage language) => language == AppLanguage.Vietnamese
        ? [["có", "sở hữu"], ["tổng", "tổng cộng", "tất cả"], ["trung bình", "trung bình cộng"],
            ["số", "số lượng"], ["sau đó", "tiếp theo"], ["xét", "xem xét"], ["dùng", "áp dụng"],
            ["quyên góp", "đóng góp"], ["thu gom", "gom"], ["tiết kiệm", "để dành"], ["chứa", "đựng"],
            ["nhận thêm", "được bổ sung thêm"]]
        : [["has", "owns", "possesses"], ["total", "altogether", "in all", "in total"],
            ["average", "mean"], ["each", "every"], ["contributes", "donates"], ["contains", "holds"]];
    private static string Normalize(string text, AppLanguage language)
    {
        text = text.ToLowerInvariant();
        var aliases = Synonyms(language).SelectMany(group => group.Skip(1).Select(value => (value, group[0])))
            .OrderByDescending(alias => alias.value.Length);
        foreach (var (from, to) in aliases)
            text = Regex.Replace(text, @"(?<!\p{L})" + Regex.Escape(from) + @"(?!\p{L})", to);
        return text;
    }

    internal static bool Matches(string? candidate, IEnumerable<string> examples, AppLanguage language)
        => MatchesCore(candidate, examples, language, false);

    // Used only after C# has substituted the immutable chart data. Native model
    // templates still reject every literal number through Matches above.
    internal static bool MatchesRendered(string? candidate, IEnumerable<string> examples, AppLanguage language)
        => MatchesCore(candidate, examples, language, true);

    private static bool MatchesCore(string? candidate, IEnumerable<string> examples, AppLanguage language, bool rendered)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 1400 || candidate.Any(char.IsControl)) return false;
        string withoutSlots = Regex.Replace(candidate, @"\{[A-Za-z0-9_.-]+\}", "");
        if (withoutSlots.IndexOfAny(['{', '}']) >= 0) return false;
        // Template quantities and computations always belong to C#.
        if (!rendered && withoutSlots.Any(char.IsDigit)) return false;
        var links = language == AppLanguage.Vietnamese ? VietnameseLinks : EnglishLinks;
        string[] Signature(string text) => Tokens.Matches(Normalize(text, language)).Select(m => m.Value)
            .Where(word => !links.Contains(word)).ToArray();
        string[] actual = Signature(candidate);
        string[] Slots(string text) => Regex.Matches(text, @"\{[A-Za-z0-9_.-]+\}").Select(m => m.Value).ToArray();
        string[] slots = Slots(candidate);
        // Exact role/quantity/unit order is intentional: changing the order of
        // two equal values must never swap owner, total, part, rate or group count.
        // Variable names are case-sensitive in the renderer. Lowercasing them
        // with the surrounding prose could otherwise accept an unrenderable {F0}.
        string[] Numbers(string text) => Regex.Matches(text, @"(?<![\p{L}\p{N}])[-+]?\d+(?:[.,]\d+)?").Select(m => m.Value).ToArray();
        return (rendered ? examples : EquivalentProseStructures.Expand(examples, language)).Any(example => (!rendered || Numbers(example).SequenceEqual(Numbers(candidate))) && Slots(example).SequenceEqual(slots)
            && Signature(example).SequenceEqual(actual));
    }

    internal static string MismatchedFields(AppLanguage language,
        params (string Field, string? Text, IEnumerable<string> Examples)[] fields)
        => string.Join(", ", fields.Where(field => !Matches(field.Text, field.Examples, language)).Select(field => field.Field));

    internal static string PromptGuidance(AppLanguage language) => language == AppLanguage.Vietnamese
        ? "Bất biến: chủ thể, vai trò tổng/lượng mỗi nhóm/số nhóm, đơn vị, thứ tự sự việc, lượng làm gốc và đại lượng hỏi. Được đổi từ tương đương trong cùng hoạt động, cấu trúc chủ động/bị động và vị trí trạng ngữ theo grammar; thêm lời nối ngắn trung tính. Không đổi nhận thêm thành mượn thêm, gộp hai lượng thành lượng lũy kế, thêm điều kiện, điền biến hay tiết lộ đáp án. Câu dẫn gọi đúng đại lượng hỏi. Ví dụ:"
        : "Fixed: owners, total/per-group/group-count roles, units, chronology, reference amount and target. Vary equivalent activity verbs, active/passive structures and adverbial position allowed by the grammar; use short neutral links. Never change receiving into borrowing, separate amounts into cumulative amounts, add conditions, fill placeholders or reveal answers. Leads name the requested quantity. Example:";

    internal static string ClauseRule(string name, IEnumerable<string> examples, AppLanguage language)
    {
        static string L(string text) => System.Text.Json.JsonSerializer.Serialize(text);
        // Generate from the SAME equivalence policy as validation. Free letter*
        // between variables allowed JSON with invented actors/dimensions that
        // inevitably failed validation. Compose safe phrase substitutions instead.
        var synonyms = Synonyms(language);
        string pattern = @"\{[A-Za-z0-9_.-]+\}|(?<!\p{L})(?:"
            + string.Join("|", synonyms.SelectMany(group => group).OrderByDescending(value => value.Length).Select(Regex.Escape))
            + @")(?!\p{L})";
        string Branch(string example)
        {
            var parts = new List<string>();
            int offset = 0;
            foreach (Match match in Regex.Matches(example, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (match.Index > offset) parts.Add(L(example[offset..match.Index]));
                var group = match.Value.StartsWith('{') ? null : synonyms.First(values => values.Contains(match.Value, StringComparer.OrdinalIgnoreCase));
                if (group is null) parts.Add(L(match.Value));
                else parts.Add("(" + string.Join(" | ", group.Select(value =>
                    L(char.IsUpper(match.Value[0]) ? char.ToUpperInvariant(value[0]) + value[1..] : value))) + ")");
                offset = match.Index + match.Length;
            }
            if (offset < example.Length) parts.Add(L(example[offset..]));
            return string.Join(" ", parts);
        }
        string prefix = language == AppLanguage.Vietnamese
            ? "(\"\" | " + L("Theo bản ghi, ") + " | " + L("Theo báo cáo, ") + ")"
            : "(\"\" | " + L("According to the report, ") + ")";
        return name.Replace('_', '-') + " ::= \"\\\"\" " + prefix + " ("
            + string.Join(" | ", EquivalentProseStructures.Expand(examples, language).Select(Branch).Distinct()) + ") \"\\\"\"\n";
    }

}
