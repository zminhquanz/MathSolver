using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Bounded, role-preserving sentence transformations, shared by prompts,
/// grammar and validation. This is not a general semantic or word-order checker.</summary>
internal static class EquivalentProseStructures
{
    private const string Slot = @"\{[A-Za-z0-9_.-]+\}";
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

    // The model cannot choose singular/plural verbs because C# supplies fresh
    // quantities later. Apply agreement while the role slots are still intact.
    internal static string WithEnglishAgreement(string text, Func<string, bool> isOne)
    {
        text = Regex.Replace(text, $@"(?<amount>{Slot}) (?<unit>{Slot}) (?<verb>belong to|were sold by)", m =>
            !isOne(m.Groups["amount"].Value) ? m.Value : m.Groups["amount"].Value + " " + m.Groups["unit"].Value
                + (m.Groups["verb"].Value == "belong to" ? " belongs to" : " was sold by"), RegexOptions.CultureInvariant, Timeout);
        return Regex.Replace(text, $@"There are (?<amount>{Slot}) (?<unit>{Slot})", m =>
            !isOne(m.Groups["amount"].Value) ? m.Value : "There is " + m.Groups["amount"].Value + " " + m.Groups["unit"].Value,
            RegexOptions.CultureInvariant, Timeout);
    }

    internal static string[] Expand(IEnumerable<string> examples, AppLanguage language)
    {
        var result = new List<string>();
        foreach (string example in examples)
        {
            result.Add(example);
            // Match a whole, single factual clause. Never move words across an
            // event, comparison, fraction base or a second quantity.
            var rules = language == AppLanguage.Vietnamese ? Vietnamese : English;
            foreach (var (pattern, replacement) in rules)
                if (Regex.IsMatch(example, pattern, RegexOptions.CultureInvariant, Timeout))
                    result.Add(Regex.Replace(example, pattern, match => {
                        string value = match.Result(replacement);
                        // A moved adverbial is no longer sentence-initial.
                        var time = match.Groups["time"];
                        if (time.Success) value = value.Replace(time.Value, time.Value.ToLowerInvariant(), StringComparison.Ordinal);
                        foreach (string group in new[] { "actor", "part", "whole" })
                        {
                            string noun = match.Groups[group].Value;
                            if (noun.Length > 0 && !noun.StartsWith('{'))
                                value = value.Replace(noun, char.ToLowerInvariant(noun[0]) + noun[1..], StringComparison.Ordinal);
                        }
                        return value;
                    }, RegexOptions.CultureInvariant, Timeout));
        }
        return result.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static readonly (string Pattern, string Replacement)[] Vietnamese =
    [
        ($@"^(?<actor>{Slot}) (?:đang )?có (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${amount} ${unit} thuộc về ${actor}${end}"),
        ($@"^(?<actor>{Slot}) nhận thêm (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${amount} ${unit} được bổ sung thêm cho ${actor}${end}"),
        ($@"^(?<actor>{Slot}) (?:đã )?bán (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${amount} ${unit} đã được ${actor} bán${end}"),
        ($@"^Mỗi (?<group>{Slot}) (?:chứa|có) (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "Trong mỗi ${group} có ${amount} ${unit}${end}"),
        ($@"^Mỗi (?<group>{Slot}) (?:chứa|có) (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${amount} ${unit} được chứa trong mỗi ${group}${end}"),
        ($@"^Hỏi mỗi (?<group>{Slot}) (?:có|chứa) bao nhiêu (?<unit>{Slot})\?$",
            "Hỏi số ${unit} trong mỗi ${group} là bao nhiêu?"),
        ($@"^(?<time>Ban đầu|Trước đó|Sau đó|Tiếp theo|ban đầu|trước đó|sau đó|tiếp theo),? (?<actor>{Slot}) (?<verb>có|còn lại|nhận thêm|đã bán|đã chuyển đi) (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${actor} ${verb} ${amount} ${unit} ${time}${end}"),
        ($@"^(?<time>Trong|Vào|trong|vào) (?<period>{Slot}), (?<actor>{Slot}) (?<verb>đọc được|làm được|thu hoạch được|thu gom được) (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${actor} ${verb} ${amount} ${unit} ${time} ${period}${end}"),
        ($@"^Trong (?<count>{Slot}) (?<period>ngày|buổi|ca|tuần|tháng|trận|lượt|chuyến), (?<actor>[\p{{L}}\p{{M}} ]+) (?<verb>nhận được|thu gom được|làm được|đọc được|thu hoạch được|ghi được|đạt được) lần lượt (?<amounts>{Slot}(?:, {Slot}){{1,7}}) (?<unit>{Slot})\.$",
            "${actor} ${verb} lần lượt ${amounts} ${unit} trong ${count} ${period}."),
        ($@"^Phần thứ nhất gồm (?<count>{Slot}) nhóm bằng nhau, mỗi nhóm (?<each>{Slot}) (?<unit>{Slot})\.$",
            "Trong phần thứ nhất, mỗi nhóm trong ${count} nhóm bằng nhau có ${each} ${unit}."),
        ($@"^Sau khi lấy ra (?<removed>{Slot}) (?<unit>{Slot}), còn (?<remaining>{Slot}) \k<unit>\.$",
            "Lượng còn lại sau khi lấy ra ${removed} ${unit} là ${remaining} ${unit}."),
        (@"^(?<part>[^?!]+) chiếm bao nhiêu phần trăm (?<whole>[^?!]+)\?$",
            "Tỉ lệ phần trăm của ${part} so với ${whole} là bao nhiêu?"),
        ($@"^Trung bình mỗi (?<period>ngày|buổi|ca|tuần|tháng|trận|lượt|chuyến) là bao nhiêu (?<unit>{Slot})\?$",
            "Hỏi số ${unit} trung bình mỗi ${period} là bao nhiêu?")
    ];

    private static readonly (string Pattern, string Replacement)[] English =
    [
        ($@"^(?<actor>{Slot}) (?:has|owns) (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${amount} ${unit} belong to ${actor}${end}"),
        ($@"^(?<actor>{Slot}) receives (?<amount>{Slot}) more (?<unit>{Slot})(?<end>[,.])$",
            "${actor} is given ${amount} more ${unit}${end}"),
        ($@"^(?<actor>{Slot}) (?:sells|sold) (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${amount} ${unit} were sold by ${actor}${end}"),
        ($@"^Each (?<group>{Slot}) (?:contains|has|holds) (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "There are ${amount} ${unit} in each ${group}${end}"),
        ($@"^(?<time>Initially|Previously|Then|Next|initially|previously|then|next),? (?<actor>{Slot}) (?<verb>has|had|receives|sold) (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${actor} ${verb} ${amount} ${unit} ${time}${end}"),
        ($@"^(?<time>During|In|during|in) (?<period>{Slot}), (?<actor>{Slot}) (?<verb>reads|read|makes|made|collects|collected) (?<amount>{Slot}) (?<unit>{Slot})(?<end>[,.])$",
            "${actor} ${verb} ${amount} ${unit} ${time} ${period}${end}"),
        ($@"^Over (?<count>{Slot}) (?<period>days|sessions|shifts|weeks|months|matches|rounds|trips), (?<actor>[A-Za-z ]+) (?<verb>receives|collects|makes|reads|harvests|scores) (?<amounts>{Slot}(?:(?:, |,? and ){Slot}){{1,7}}) (?<unit>{Slot})\.$",
            "${actor} ${verb} ${amounts} ${unit} over ${count} ${period}."),
        ($@"^The first portion has (?<count>{Slot}) equal groups of (?<each>{Slot}) (?<unit>{Slot})\.$",
            "Each of the ${count} equal groups in the first portion contains ${each} ${unit}."),
        ($@"^After removing (?<removed>{Slot}) (?<unit>{Slot}), (?<remaining>{Slot}) \k<unit> remain\.$",
            "The amount remaining after removing ${removed} ${unit} is ${remaining} ${unit}."),
        (@"^What percentage of (?<whole>[^?!]+) is (?<part>[^?!]+)\?$",
            "What is ${part} as a percentage of ${whole}?")
    ];
}
