using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Proof rules for a limited set of equivalent transformations, not a general language judge.</summary>
internal static class OpenProseMeaning
{
    private static readonly string[] VietnameseOpenings = ["Trong buổi chuẩn bị đồ dùng", "Trong lần kiểm kê", "Theo ghi chép", "Trong buổi học", "Trong hoạt động này", "Để chuẩn bị cho buổi học"];
    private static readonly string[] EnglishOpenings = ["During the activity", "In the inventory", "According to the records", "For the class activity", "While checking supplies"];
    internal static string Normalize(string text, AppLanguage language)
    {
        foreach (string opening in language == AppLanguage.Vietnamese ? VietnameseOpenings : EnglishOpenings)
            text = Regex.Replace(text, "^" + Regex.Escape(opening) + @"\s*[,.:]\s*", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        string Replace(string pattern, string replacement) => Regex.Replace(text, pattern, replacement,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        const string owner = @"(?<owner>\{(?:name|other|actor)\})", amount = @"(?<amount>\{(?:a|b|[a-z_]+)\})", unit = @"(?<unit>\{(?:unit|items|item)\})";
        if (language == AppLanguage.Vietnamese)
        {
            text = Replace(@"Số\s+" + unit + @"\s+của\s+" + owner + @"\s+là\s+" + amount, "${owner} có ${amount} ${unit}");
            text = Replace(amount + @"\s+" + unit + @"\s+thuộc về\s+" + owner, "${owner} có ${amount} ${unit}");
            text = Replace(@"\b(?:hiện có|đang sở hữu|đang giữ)\b", "có");
            text = Replace(@"\b(?:vừa nhận thêm|được gửi tặng thêm)\b", "nhận thêm");
            text = Replace(@"^(?:Hãy xác định|Em hãy tìm|Hãy cho biết)\s*", "Hỏi ");
        }
        else
        {
            text = Replace(amount + @"\s+" + unit + @"\s+(?:belong to|are held by)\s+" + owner, "${owner} has ${amount} ${unit}");
            text = Replace(@"\b(?:currently has|is holding|currently owns)\b", "has");
            text = Replace(@"\b(?:is given an additional|receives an additional)\b", "receives");
            text = Replace(@"^(?:Please determine|Work out)\s+", "Find ");
        }
        return text;
    }

    internal static bool Matches(string candidate, string reference, AppLanguage language) =>
        SemanticProseRules.Matches(Normalize(candidate, language), [Normalize(reference, language)], language);

    internal static bool HasContradiction(BasicQuestionDraft draft, BasicQuestionDraft reference, AppLanguage language)
    {
        bool Has(string text, string pattern) => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        string gain = language == AppLanguage.Vietnamese ? @"\b(?:nhận thêm|mua thêm|được tặng|bổ sung thêm)\b" : @"\b(?:receives?|gets?|gains?|is given|buys?)\b";
        string loss = language == AppLanguage.Vietnamese ? @"\b(?:cho đi|bán đi|lấy bớt|mất đi|đem tặng)\b|(?<!được )\btặng\b" : @"\b(?:gives? away|sells?|loses?|removes?|donates?)\b";
        foreach (var (actual, expected) in OpenQuestionProse.Fields(draft).Zip(OpenQuestionProse.Fields(reference)))
            if (Has(expected.Text, gain) && !Has(expected.Text, loss) && Has(actual.Text, loss)
                || Has(expected.Text, loss) && !Has(expected.Text, gain) && Has(actual.Text, gain)) return true;
        string condition = language == AppLanguage.Vietnamese ? @"\b(?:nếu|trừ khi|mượn thêm|gấp đôi|một nửa|không|chẳng|chưa)\b" : @"\b(?:unless|if|borrows?|twice|half|not|never|without)\b";
        return OpenQuestionProse.Fields(draft).Any(f => Has(f.Text, condition)) && !OpenQuestionProse.Fields(reference).Any(f => Has(f.Text, condition));
    }
}
