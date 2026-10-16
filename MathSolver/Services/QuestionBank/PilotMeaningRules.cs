using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Small, field-bound proofs. Unknown prose is not a failed proof of mathematics.</summary>
internal static class PilotMeaningRules
{
    internal static bool TechnicalText(string text)=>Regex.IsMatch(text,
        @"\b(?:solution[\s_-]*leads?|given[\s_-]*[ab]|unit[\s_-]*id|first[\s_-]*unit|second[\s_-]*unit|relation[\s_-]*id|JSON|GBNF|C#)\b|['""‘’]\s*[,;:]|['""](?:role|step|facts|question)['""]\s*:",
        RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));

    private static string Clean(string text,AppLanguage language)
    {
        text=OpenProseMeaning.Normalize(text,language).Trim();
        text=Regex.Replace(text,@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        return text.TrimEnd(' ','.',':','?','!').ToLowerInvariant();
    }
    internal static bool TimeContradiction(BasicQuestionDraft draft,AppLanguage language)
    {
        string all=draft.ProblemText;
        // "Without a break" preserves continuity. Do not treat its negated
        // noun as an affirmative break; unfamiliar negations still need review.
        all=Regex.Replace(all,language==AppLanguage.Vietnamese?@"\bkhông (?:(?:có )?(?:thời gian )?nghỉ(?: giữa giờ)?|(?:bị )?gián đoạn)\b":@"\b(?:without (?:a |any )?(?:breaks?|interruptions?|pauses?)|no (?:breaks?|pauses?)|not interrupted)\b","continuous",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        return Regex.IsMatch(all,language==AppLanguage.Vietnamese
            ?@"\b(?:hôm sau|ngày sau|ngày tiếp theo|qua đêm|nửa đêm|nghỉ|chờ|gián đoạn|không liên tục|không cùng ngày|trừ khi|nếu)\b"
            :@"\b(?:next day|following day|midnight|overnight|breaks?|pauses?|waits?|interrupted|not continuously|not on the same day|unless|if)\b",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    }
    internal static bool ProvesTime(BasicQuestionContract c,BasicQuestionDraft d)
        =>SameDayTimeMeaning.Proves(c,d);

    internal static bool ProvesDecimal(BasicQuestionContract c,BasicQuestionDraft d)
    {
        // Additional safe lexical transformations, scoped to decimal facts.
        // Roles/owners/quantity-unit pairs never move or disappear.
        var reference=OpenQuestionProse.Example(c);
        string Normalize(string s,string field)
        {
            s=Clean(s,c.Language);
            string Replace(string p,string r)=>Regex.Replace(s,p,r,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
            if(field.StartsWith("facts.",StringComparison.Ordinal))
            {
                if(c.Language==AppLanguage.Vietnamese)
                {
                    s=Replace(@"\b(?:sở hữu|giữ|đang có|đang giữ)\b","có");
                    s=Replace(@"\bban đầu có\b","có");
                    s=Replace(@"\bcó tổng cộng\b","có");
                    s=Replace(@"\b(?:được cấp thêm|vừa nhận thêm)\b","nhận thêm");
                    s=Replace(@"\bđã mua\b","mua");
                    s=Replace(@"\bđã sử dụng\b","dùng hết");
                }
                else
                {
                    s=Replace(@"\b(?:possesses|holds|currently has)\b","has");
                    s=Replace(@"\bhas (?:a total of|a quantity of)\b","has");
                    s=Replace(@"\b(?:acquires|purchases)\b","buys");
                }
            }
            return s;
        }
        return OpenQuestionProse.Fields(d).Zip(OpenQuestionProse.Fields(reference)).All(pair=>
            OpenProseMeaning.Matches(Normalize(pair.First.Text,pair.First.Id),Normalize(pair.Second.Text,pair.Second.Id),c.Language));
    }
}
