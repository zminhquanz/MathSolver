using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Field-bound temporal clauses. Unrecognised prose still requires review.</summary>
internal static class SameDayTimeMeaning
{
    private static bool Fits(string text, string pattern, AppLanguage language)
    {
        text = OpenProseMeaning.Normalize(text, language).Trim();
        text = Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return Regex.IsMatch(text.TrimEnd(' ', '.', ':', '?', '!'), @"\A(?:" + pattern + @")\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    internal static bool Proves(BasicQuestionContract c, BasicQuestionDraft d)
    {
        bool vi = c.Language == AppLanguage.Vietnamese;
        string relation = c.SceneId.Split('/')[1];
        string owner = vi ? @"\{v1\} của \{v0\}" : @"(?:(?:the )?\{v1\} for \{v0\}|\{v0\}['’]s \{v1\})";
        string Clock(bool start, string h, string m)
        {
            string time = vi ? @"\{" + h + @"\} giờ \{" + m + @"\} phút"
                : @"\{" + h + @"\} hours (?:and )?\{" + m + @"\} minutes";
            string verb = vi ? start ? @"(?:bắt đầu|khởi đầu)" : @"(?:kết thúc|hoàn tất)"
                : start ? @"(?:starts|begins|commences)" : @"(?:ends|finishes|concludes)";
            string clock=vi
                ? owner + (start?@" (?:được thực hiện |được )?":@" (?:được )?") + verb + @" (?:vào )?" + (start?"(?:lúc|từ)":"lúc") + " " + time
                    + @"|(?:vào )?lúc " + time + @", " + owner + " " + verb
                    + @"|\{v0\} " + verb + @" \{v1\} (?:vào )?lúc " + time
                : owner + " " + verb + " at " + time
                    + @"|at " + time + @", " + owner + " " + verb
                    + @"|\{v0\} " + verb + @" (?:the )?\{v1\} at " + time;
            return vi?"(?:"+clock+@")(?: trong ngày| của ngày)?(?: nhé| đấy)?":clock;
        }

        string duration = vi
            ? @"(?:hoạt động (?:đó|này)|quá trình này|việc này) (?:kéo dài|diễn ra trong) (?:tổng cộng )?\{f2\} phút"
                + @"|(?:thời gian (?:thực hiện|diễn ra)(?: hoạt động(?: này)?)?|thời lượng của hoạt động)(?: là)? \{f2\} phút"
                + @"|khoảng thời gian này có độ dài \{f2\} phút"
                + @"|hoạt động (?:này|đó) có (?:tổng thời gian|thời lượng) (?:là )?\{f2\} phút"
                + @"|hoạt động (?:này|đó) tiếp diễn trong suốt \{f2\} phút"
                + @"|thời gian dành cho hoạt động (?:này|đó) là \{f2\} phút"
            : @"(?:the|this|that) activity (?:lasts|continues for|takes|takes up) \{f2\} minutes(?: of time)?"
                + @"|(?:the duration|the length) of (?:the|this) activity is \{f2\} minutes"
                + @"|a span of \{f2\} minutes is the length of (?:the|this) activity"
                + @"|the time spent on (?:the|this) activity is \{f2\} minutes"
                + @"|the activity is \{f2\} minutes long"
                + @"|the duration of \{f2\} minutes is how long the activity takes"
                + @"|the entire \{f2\} minutes is the length of the activity";
        string continuity = vi
            ? @"(?:(?:toàn bộ |cả )?hoạt động(?: này)?|(?:toàn bộ |cả )?quá trình(?: này)?) (?:được thực hiện|diễn ra) (?:liên tục|liền mạch|không bị gián đoạn|không nghỉ) trong (?:cùng (?:một )?ngày|một ngày)"
                + @"|trong cùng (?:một )?ngày, hoạt động diễn ra liên tục"
                + @"|hoạt động diễn ra trong cùng (?:một )?ngày và không nghỉ"
                + @"|hoạt động(?: này)? không bị gián đoạn trong cùng (?:một )?ngày"
                + @"|hoạt động này diễn ra hết trong một ngày không nghỉ"
            : @"(?:the|this|the entire|this entire|this single) (?:activity|session|event) (?:runs|occurs|proceeds|continues|takes place|happens) (?:continuously|without (?:a |any )?(?:break|interruption|stopping)) (?:on the same day|within one day|on one day)"
                + @"|on the same day, the activity runs continuously"
                + @"|the activity runs on the same day without a break"
                + @"|the activity is not interrupted on the same day"
                + @"|the entire activity takes place without interruption on one day";
        string question;
        if (vi)
        {
            string target = relation == "start" ? @"(?:bắt đầu|khởi đầu)" : @"(?:kết thúc|hoàn tất)";
            question = relation == "elapsed"
                ? @"(?:hỏi )?" + owner + @" (?:đã )?(?:kéo dài|diễn ra trong|mất|tiêu tốn) bao nhiêu phút"
                    + @"|thời gian (?:diễn ra|thực hiện) " + owner + @" là bao nhiêu phút"
                    + @"|khoảng thời gian mà " + owner + @" kéo dài là bao nhiêu phút"
                : @"(?:hỏi )?" + owner + @" (?:đã |sẽ )?" + target + @" (?:vào )?lúc mấy giờ(?:, mấy phút)?"
                    + @"|(?:hỏi )?" + owner + " " + target + @" vào thời điểm nào"
                    + @"|thời điểm " + target + " của " + owner + @" là (?:khi nào|mấy giờ, mấy phút)";
        }
        else
        {
            string target = relation == "start" ? @"(?:start|begin|commence)" : @"(?:end|finish|conclude)";
            question = relation == "elapsed"
                ? @"(?:please determine: )?how many minutes does " + owner + @" last"
                    + @"|what is the (?:total )?duration of " + owner + @" (?:in|measured in) minutes"
                    + @"|how long does " + owner + @" last in minutes"
                    + @"|what is the total number of (?:elapsed )?minutes for " + owner
                    + @"|what is the total number of minutes " + owner + @" takes"
                : @"(?:please determine: )?(?:at what hour and minute|what time|when) does " + owner + " " + target + @"(?: in hours and minutes)?"
                    + @"|what is the " + (relation=="start"?"(?:start|starting)":"(?:end|ending|finish)") + " time of " + owner;
        }
        string lead = vi ? relation switch
        {
            "elapsed" => @"(?:(?:khoảng )?thời gian(?: diễn ra| thực hiện)? hoạt động|tổng thời gian thực hiện|thời lượng của hoạt động)",
            "start" => @"(?:thời điểm (?:bắt đầu|khởi đầu)(?:(?: của)? hoạt động)?|giờ và phút bắt đầu hoạt động)",
            _ => @"(?:thời điểm kết thúc(?:(?: của)? hoạt động)?|giờ và phút kết thúc hoạt động)"
        } : relation switch
        {
            "elapsed" => @"(?:the )?(?:duration of the activity|total elapsed time|elapsed time|total duration|total elapsed minutes)(?: in minutes)?",
            "start" => @"(?:the )?(?:starting|start|beginning|commencement) time(?: of the activity)?",
            _ => @"(?:the )?(?:ending|end|concluding|final) (?:clock )?time(?: of the activity)?|(?:the )?final clock reading of the activity"
        };
        return d.Facts is { Count: 3 } && d.SolutionLeads is { Count: 1 }
            && Fits(d.Facts[0].Text, Clock(relation != "start", "f0", "f1"), c.Language)
            && Fits(d.Facts[1].Text, relation == "elapsed" ? Clock(false, "f2", "f3") : duration, c.Language)
            && Fits(d.Facts[2].Text, continuity, c.Language)
            && Fits(d.Question, question, c.Language)
            && Fits(d.SolutionLeads[0].Text, lead, c.Language);
    }
}
