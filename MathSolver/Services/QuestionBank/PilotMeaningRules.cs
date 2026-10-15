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
    // Every regex anchors the complete field, retaining each placeholder's location.
    // A correct verb somewhere in a sentence is insufficient evidence of its role.
    private static bool Fits(string text,string pattern,AppLanguage language)=>Regex.IsMatch(Clean(text,language),
        "\\A(?:"+pattern+")\\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));

    internal static bool TimeContradiction(BasicQuestionDraft draft,AppLanguage language)
    {
        string all=draft.ProblemText;
        // "Without a break" preserves continuity. Do not treat its negated
        // noun as an affirmative break; unfamiliar negations still need review.
        all=Regex.Replace(all,language==AppLanguage.Vietnamese?@"\bkhông (?:(?:có )?nghỉ(?: giữa giờ)?|(?:bị )?gián đoạn)\b":@"\b(?:without (?:a |any )?(?:breaks?|interruption)|no breaks?|not interrupted)\b","continuous",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        return Regex.IsMatch(all,language==AppLanguage.Vietnamese
            ?@"\b(?:hôm sau|ngày sau|ngày tiếp theo|qua đêm|nửa đêm|nghỉ|chờ|gián đoạn|không liên tục|không cùng ngày|trừ khi|nếu)\b"
            :@"\b(?:next day|following day|midnight|overnight|breaks?|pauses?|waits?|interrupted|not continuously|not on the same day|unless|if)\b",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    }
    internal static bool ProvesTime(BasicQuestionContract c,BasicQuestionDraft d)
    {
        var source=ReasoningStoryCatalogue.Draft(c);
        string relation=c.SceneId.Split('/')[1];
        bool vi=c.Language==AppLanguage.Vietnamese;
        string owner=vi?@"\{v1\} của \{v0\}":@"the \{v1\} for \{v0\}";
        string Clock(bool start,string h,string m)=>vi?owner+" "+(start?"(?:bắt đầu|khởi đầu)":"(?:kết thúc|hoàn tất)")+@" (?:vào )?lúc \{"+h+@"\} giờ \{"+m+@"\} phút"
            :owner+" "+(start?"(?:starts|begins)":"(?:ends|finishes)")+@" at \{"+h+@"\} hours \{"+m+@"\} minutes";
        string[] facts=[Clock(relation!="start","f0","f1"),relation=="elapsed"?Clock(false,"f2","f3"):
            vi?@"hoạt động đó (?:kéo dài|diễn ra trong) \{f2\} phút":@"the activity (?:lasts|continues for) \{f2\} minutes",
            vi?@"(?:hoạt động diễn ra liên tục trong cùng ngày|trong cùng ngày, hoạt động diễn ra liên tục|hoạt động diễn ra trong cùng ngày và không nghỉ)"
                :@"(?:the activity runs continuously on the same day|on the same day, the activity runs continuously|the activity runs on the same day without a break)"];
        string question=vi?@"(?:hỏi )?"+owner+(relation=="elapsed"?@" (?:kéo dài|diễn ra trong) bao nhiêu phút":
            " "+(relation=="start"?"(?:bắt đầu|khởi đầu)":"(?:kết thúc|hoàn tất)")+" lúc mấy giờ, mấy phút")
            :@"(?:please determine: )?"+(relation=="elapsed"?@"how many minutes does "+owner+" last":@"at what hour and minute does "+owner+(relation=="start"?" (?:start|begin)":" (?:end|finish)"));
        // Leads can vary through verified equivalences, but may not rename the target.
        return d.Facts!.Select((f,i)=>Fits(f.Text,facts[i],c.Language)).All(v=>v)
            && Fits(d.Question,question,c.Language)
            && OpenProseMeaning.Matches(d.SolutionLeads![0].Text,source.SolutionLeads![0].Text,c.Language);
    }

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
