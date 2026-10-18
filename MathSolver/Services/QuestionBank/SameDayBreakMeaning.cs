using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

/// <summary>Each clock, included break, day scope and active-time target must be proved separately.</summary>
internal static class SameDayBreakMeaning
{
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string start=d.Facts![0].Text,end=d.Facts[1].Text,pause=d.Facts[2].Text,day=d.Facts[3].Text;
        string owner=vi?@"\{v1\} của \{v0\}":@"(?:\{v0\}['’]s \{v1\}|(?:the )?\{v1\} for \{v0\})";
        // Retry only complete clauses whose contradictory meaning is proved.
        // Keywords in a negation, reference or comparison are review-only.
        bool badCondition=Match(day,vi?@"buổi hoạt động (?:kết thúc|tiếp tục) (?:vào )?ngày hôm sau|một buổi thứ hai diễn ra sau đó|buổi hoạt động (?:diễn ra )?liên tục trong cùng (?:một )?ngày"
            :@"the session ends (?:the next day|on the following day)|a second session follows|the session runs continuously on the same day");
        // A break before the END may be inside the session; a break before its
        // START is outside. Unknown temporal wording is review-only, not a retry.
        bool badPause=Match(pause,vi?@"sau khi "+owner+@" (?:kết thúc|hoàn tất), có một lần nghỉ dài \{f4\} phút"
                +@"|trong "+owner+@", có (?:hai|nhiều) lần nghỉ dài \{f4\} phút"
            :@"after "+owner+@" (?:ends|finishes), there is one break of \{f4\} minutes"
                +@"|during "+owner+@", there are multiple breaks of \{f4\} minutes");
        // An end clause may refer to the start while explaining same-day scope.
        // Reject a proved wrong clock predicate, not an incidental role word.
        bool swapped=Match(start,Clock(false).Replace(@"\{f2\}",@"\{f0\}").Replace(@"\{f3\}",@"\{f1\}"))
            ||Match(end,Clock(true).Replace(@"\{f0\}",@"\{f2\}").Replace(@"\{f1\}",@"\{f3\}"));
        bool wrongTarget=Match(d.Question,vi?@"tổng thời gian "+owner+@" là bao nhiêu phút"
                +@"|"+owner+@" kết thúc lúc mấy giờ|"+owner+@" bao gồm thời gian nghỉ là bao nhiêu phút"
            :@"what is the total elapsed duration of "+owner+@" in minutes|what time does "+owner+@" end"
                +@"|how many minutes does "+owner+@" last, including the break");
        if(badCondition||badPause||swapped||wrongTarget)
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Keep one included break, the same-day start/end roles and active minutes excluding rest; add no session or condition.");
        if(OpenQuestionProse.Fields(d).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one pupil-facing sentence per field.");
        string Clock(bool first)
        {
            string h=first?"f0":"f2",m=first?"f1":"f3";
            string time=vi?@"\{"+h+@"\} giờ \{"+m+@"\} phút":@"\{"+h+@"\} hours (?:and )?\{"+m+@"\} minutes";
            string verb=vi?(first?"(?:bắt đầu|khởi đầu)":"(?:kết thúc|hoàn tất)"):(first?"(?:starts|begins|started|began)":"(?:ends|finishes|concludes|ended|finished|concluded)");
            return vi?owner+" "+verb+" (?:vào )?lúc "+time+"|(?:vào )?lúc "+time+", "+owner+" "+verb
                :owner+" "+verb+" at "+time+"|at "+time+", "+owner+" "+verb
                    +@"|\{v0\} "+verb+@" the \{v1\} at "+time;
        }
        string included=vi
            ?@"trong (?:suốt |quá trình thực hiện |khoảng thời gian (?:diễn ra )?)?"+owner+@",? (?:có|chỉ có) (?:đúng )?một lần nghỉ (?:dài|kéo dài) \{f4\} phút"
                +@"|"+owner+@" có đúng một lần nghỉ (?:dài|kéo dài) \{f4\} phút trong buổi"
                +@"|\{v0\} nghỉ đúng một lần (?:trong|giữa) \{v1\}, (?:kéo dài|trong) \{f4\} phút"
            :@"during "+owner+@",? there is (?:(?:exactly|only) )?one break (?:lasting|of) \{f4\} minutes"
                +@"|"+owner+@" includes (?:exactly|only) one break (?:of|lasting) \{f4\} minutes"
                +@"|\{v0\} takes (?:exactly|only) one break (?:of|lasting) \{f4\} minutes during (?:the )?\{v1\}";
        if(!vi)included+=@"|there is (?:only )?(?:one|a single) break of \{f4\} minutes within "+owner
            +@"|(?:exactly one|a single) (?:break|pause) of \{f4\} minutes occurs within the span of "+owner
            +@"|one break of \{f4\} minutes occurred while \{v0\} was engaged in the \{v1\}";
        string sameDay=vi?@"(?:hai mốc giờ|buổi hoạt động bắt đầu và kết thúc) (?:đều )?(?:thuộc|trong) cùng (?:một )?ngày"
            +@"|(?:cả )?hai mốc thời gian(?: này)? (?:đều )?(?:nằm|diễn ra|xảy ra) trong cùng (?:một )?ngày"
            +@"|(?:cả )?hai thời điểm(?: này)? (?:đều )?diễn ra trong cùng (?:một )?ngày"
            +@"|toàn bộ (?:sự việc|buổi hoạt động) diễn ra trong cùng (?:một )?ngày"
            +@"|buổi hoạt động bắt đầu và kết thúc trong cùng (?:một )?ngày"
            :@"(?:both clock times|the start and end times) are on the same day|the session starts and ends on the same day"
                +@"|the (?:start and (?:end|finish)|beginning and end) times(?: for this session)? (?:both )?(?:occur|fall) on (?:the same (?:calendar )?day|a single day)"
                +@"|the session (?:remains|remained) within a single day";
        // Scope the exclusion to the requested duration, never match the word
        // 'break' or 'not' in isolation. Missing minutes stays review-only.
        string exclude=vi?@"(?:không tính|không kể) (?:thời gian|phần|lần) nghỉ":@"excluding the (?:only |single )?break";
        string question=vi?@"(?:không tính|không kể) thời gian nghỉ, "+owner+@" (?:kéo dài|diễn ra(?: trong)?|có) bao nhiêu phút"
                +@"|thời gian (?:thực hiện thực tế|hoạt động thực tế) (?:của )?"+owner+@", (?:không tính|không kể) nghỉ, là bao nhiêu phút"
                +@"|thời gian thực hiện "+owner+@" (?:không tính|không kể) thời gian nghỉ là bao nhiêu phút"
                +@"|thời gian thực tế mà \{v0\} dành cho \{v1\}(?:, (?:không tính|không kể) thời gian nghỉ,| \((?:không tính|không kể) thời gian nghỉ\)) là bao nhiêu phút"
                +@"|nếu loại bỏ thời gian nghỉ, "+owner+@" (?:đã )?diễn ra trong bao nhiêu phút"
                +@"|thời gian (?:thực hiện|hoạt động)(?: thực tế)? (?:của )?"+owner+@"(?: "+exclude+@"| \("+exclude+@"\)|, "+exclude+@",) là bao nhiêu phút"
            :@"excluding the break, how many minutes does "+owner+@" last"
                +@"|how many active minutes does "+owner+@" (?:last|have), excluding the break"
                +@"|what is the active duration of "+owner+@" in minutes, excluding the break"
                +@"|"+exclude+@", what is (?:the (?:active )?duration of "+owner+@"|"+owner+@" duration) in minutes"
                +@"|what is the (?:active )?duration of "+owner+@" in minutes(?:, "+exclude+@"| \("+exclude+@"\))";
        string lead=vi?@"(?:thời gian|thời lượng) (?:hoạt động|thực hiện)(?: thực tế)? (?:không tính|không kể) (?:thời gian )?(?:nghỉ|giải lao)|thời lượng hoạt động không nghỉ"
            :@"(?:the )?active (?:time|duration|minutes) excluding the break";
        bool[] checks=[Match(start,Clock(true)),Match(end,Clock(false)),Match(pause,included),Match(day,sameDay),Match(d.Question,question),Match(d.SolutionLeads![0].Text,lead),
            d.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&d.Question.TrimEnd().EndsWith('?')];
        if(checks.All(b=>b))return new(d,null,c);
        string[] roles=["start clock","end clock","one break inside session","same day","active-minute target excluding rest","solution lead","sentence punctuation"];
        return new(d,"SemanticReviewRequired",c,"Could not prove: "+string.Join(", ",roles.Where((_,i)=>!checks[i]))+". Review before use.");
    }
}
