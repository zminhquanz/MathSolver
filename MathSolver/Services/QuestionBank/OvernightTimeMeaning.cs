using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

/// <summary>Proves bound clock roles, exactly the next day, no breaks and the minute target separately.</summary>
internal static class OvernightTimeMeaning
{
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string start=d.Facts![0].Text,end=d.Facts[1].Text,condition=d.Facts[2].Text;
        string owner=vi?@"\{v1\} của \{v0\}":@"(?:\{v0\}['’]s \{v1\}|the \{v1\} for \{v0\})";
        // A night watch is not a journey. A condition about another kind of
        // activity must not prove continuity of the named activity.
        string noun=c.SceneId.EndsWith("/watch",StringComparison.Ordinal)?"(?:session|activity)":"(?:session|activity|journey)";
        string nextDay=vi?@"(?:ngày hôm sau|ngày kế tiếp|ngày tiếp theo)":@"(?:the next day|the following day)";
        string Time(string h,string m)=>vi?@"\{"+h+@"\} giờ \{"+m+@"\} phút":@"\{"+h+@"\} hours (?:and )?\{"+m+@"\} minutes";
        string Verb(bool first)=>vi?(first?"(?:bắt đầu|khởi đầu)":"(?:kết thúc|hoàn tất)")
            :(first?"(?:starts|begins|commences|started|began|commenced)":"(?:ends|finishes|concludes|ended|finished|concluded)");
        string Clock(bool first,string h,string m)
        {
            string time=Time(h,m),verb=Verb(first);
            return vi?owner+" "+verb+" (?:vào lúc|lúc|vào|tại thời điểm) "+time
                    +"|(?:vào )?lúc "+time+", "+owner+" "+verb
                :owner+" "+verb+" at "+time+"|at "+time+", "+owner+" "+verb
                    +@"|\{v0\} "+verb+@" the \{v1\} at "+time;
        }
        string endClock="(?:"+Clock(false,"f2","f3")+")";
        string endNext=endClock+" "+(vi?"(?:(?:vào|của) )?":"(?:on )?")+nextDay
            +"|"+nextDay+", (?:"+Clock(false,"f2","f3")+")";
        // A proved contradiction retries. A missing day or unknown wording is
        // inconclusive and reviews once; never infer a day from clock numbers.
        bool wrongDay=Match(end,endClock+(vi?" (?:trong )?(?:cùng ngày|ngày hôm trước|hai ngày sau)"
            :" (?:on the same day|on the previous day|two days later)"));
        bool swapped=Match(start,Clock(false,"f0","f1"))
            ||Match(end,"(?:"+Clock(true,"f2","f3")+")(?: "+(vi?"":"(?:on )?")+nextDay+")?");
        bool badCondition=Match(condition,vi
            ?@"(?:buổi hoạt động|hoạt động này) có (?:một|hai|nhiều) lần nghỉ|một buổi thứ hai diễn ra sau đó|hoạt động kéo dài hai ngày"
            :@"the "+noun+@" (?:includes|has) (?:a break|two breaks)|a second session follows|the activity lasts two days");
        bool wrongTarget=Match(d.Question,vi
            ?owner+@" (?:kết thúc|bắt đầu) lúc mấy giờ|"+owner+@" kéo dài bao nhiêu giờ"
            :@"what time does "+owner+@" (?:end|start)|how many hours does "+owner+@" last");
        if(wrongDay||swapped||badCondition||wrongTarget)
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Keep start/end roles, exactly the following day, one continuous activity without breaks and elapsed minutes.");
        if(OpenQuestionProse.Fields(d).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one pupil-facing sentence per field.");
        string continuous=vi
            ?@"(?:buổi hoạt động|hoạt động(?: này)?) (?:diễn ra|được thực hiện) liên tục,? (?:và )?không (?:nghỉ|có thời gian nghỉ|có khoảng nghỉ)"
                +@"|(?:buổi hoạt động|hoạt động(?: này)?) diễn ra liên tục không có lần nghỉ nào"
                +@"|toàn bộ quá trình diễn ra không hề gián đoạn"
                +@"|hoạt động này được thực hiện (?:liên tục )?không (?:hề dừng lại|gián đoạn)"
            :@"the "+noun+@" (?:runs|continues|takes place|is carried out) continuously,? (?:and )?(?:without (?:a break|any breaks|rest)|with no breaks)"
                +@"|the "+noun+@" is continuous and has no breaks"
                +@"|the "+noun+@" is uninterrupted"
                +@"|(?:the|this) "+noun+@" proceeds without (?:interruption|any interruptions)";
        string question=vi
            ?@"(?:hỏi )?"+owner+@" (?:kéo dài|diễn ra trong) bao nhiêu phút"
                +@"|(?:thời gian|thời lượng) (?:của )?"+owner+@" là bao nhiêu phút"
                +@"|(?:hỏi )?"+owner+@" kéo dài tất cả bao nhiêu phút"
            :@"how many minutes (?:does|did) "+owner+@" last"
                +@"|what (?:is|was) the (?:elapsed |total )?duration of "+owner+@" in minutes"
                +@"|what (?:is|was) the (?:elapsed |total )?duration of "+owner+@", measured in minutes"
                +@"|in minutes, how long (?:does|did) "+owner+@" last";
        string lead=vi?@"(?:thời gian|thời lượng|tổng thời gian|khoảng thời gian) (?:hoạt động|diễn ra hoạt động|của hoạt động)"
            :@"(?:the )?(?:elapsed |total )?(?:time|duration)(?: of the "+noun+@")?";
        bool[] checks=[Match(start,Clock(true,"f0","f1")),Match(end,endNext),Match(condition,continuous),
            Match(d.Question,question),Match(d.SolutionLeads![0].Text,lead),
            d.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&d.Question.TrimEnd().EndsWith('?')];
        if(checks.All(b=>b))return new(d,null,c);
        string[] roles=["start clock","end clock on exactly the following day","continuous without breaks","elapsed-minute target","duration lead","sentence punctuation"];
        return new(d,"SemanticReviewRequired",c,"Could not prove: "+string.Join(", ",roles.Where((_,i)=>!checks[i]))+". Review before use.");
    }
}
