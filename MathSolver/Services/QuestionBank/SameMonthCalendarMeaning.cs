using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

/// <summary>Proves event/date ownership and the counting convention independently.</summary>
internal static class SameMonthCalendarMeaning
{
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese,inclusive=SameMonthCalendarPilot.IsInclusive(c.SceneId);
        var facts=d.Facts!;
        string Date(string day)=>vi?@"ngày \{"+day+@"\} tháng \{f2\} năm \{f3\}"
            :@"day \{"+day+@"\} of month \{f2\} in year \{f3\}";
        string Event(string action)=>@"\{v0\} "+(vi?"(?:đã )?":"")+@"\{"+action+@"\} \{v1\}";
        string Fact(string action,string day)=>vi
            ?@"(?:theo ghi chép, )?"+Event(action)+@" (?:vào )?"+Date(day)+@"|(?:vào )?"+Date(day)+@", "+Event(action)
            :@"(?:according to the record, )?"+Event(action)+@" on "+Date(day)+@"|on "+Date(day)+@", "+Event(action);
        string owner=vi?@"\{v4\} của \{v0\}":@"(?:\{v0\}['’]s \{v4\}|the \{v4\} for \{v0\})";
        string excludedRule=vi
            ?@"không tính ngày (?:bắt đầu|đầu), (?:có )?tính ngày (?:kết thúc|cuối)"
                +@"|ngày bắt đầu không được tính, ngày kết thúc được tính"
                +@"|thời gian (?:này|tính) không (?:tính|bao gồm) ngày bắt đầu,? (?:mà|nhưng) (?:có )?tính (?:cả )?ngày kết thúc"
                +@"|trong khoảng thời gian này, ta không tính ngày bắt đầu mà tính cả ngày kết thúc"
                +@"|đếm (?:đủ |tất cả )?(?:các )?ngày từ ngày liền sau ngày bắt đầu đến hết ngày kết thúc"
            :@"the start date is excluded and the end date is included"
                +@"|exclude the start date and include the end date"
                +@"|do not count the start date, (?:but )?count the end date"
                +@"|the start date is not counted, (?:but )?the end date is counted"
                +@"|count (?:all (?:calendar )?days|every (?:calendar )?day) from the day (?:immediately )?after the start date (?:up to and including|through) the end date(?:, inclusive)?"
                +@"|the count begins on the day after the first date and ends on the second date, counting every calendar day in between";
        string includedRule=vi
            ?@"tính (?:cả ngày (?:bắt đầu|đầu) và ngày (?:kết thúc|cuối)|mọi ngày, gồm cả ngày bắt đầu và ngày kết thúc)"
                +@"|ngày bắt đầu và ngày kết thúc đều được tính"
                +@"|đếm (?:mọi|tất cả các) ngày từ ngày bắt đầu đến hết ngày kết thúc"
            :@"include both the start date and the end date"
                +@"|count every calendar day, including both the start date and the end date"
                +@"|count all calendar days from the start date through the end date, inclusive"
                +@"|both the start date and the end date are included";
        string rule=inclusive?includedRule:excludedRule;
        string badRule=vi
            ?@"tính cả ngày (?:bắt đầu|đầu) và ngày (?:kết thúc|cuối)|không tính ngày bắt đầu, không tính ngày kết thúc"
                +@"|chỉ tính ngày làm việc|không tính ngày bắt đầu, (?:có )?tính ngày kết thúc và bỏ qua (?:cuối tuần|ngày nghỉ)"
                +@"|chỉ tính ngày kết thúc|không tính ngày bắt đầu và các ngày ở giữa, chỉ tính ngày kết thúc"
            :@"include both the start date and the end date|exclude both the start date and the end date"
                +@"|count only working days|exclude the start date and include the end date, excluding (?:weekends|holidays)"
                +@"|(?:the (?:duration|count) counts|count) (?:all )?days from the (?:second|end) date (?:up to and including it|through that same date)(?:, but not the (?:first|start) date)?"
                +@"|count only the end date";
        string wrongTarget=vi?@"ngày kết thúc "+owner+@" là ngày nào"
            :@"what is the (?:start|end) date of "+owner;
        // Complete contrary counting claims are errors. Negation/qualification of
        // those claims is not evidence of an error or a proof of the expected rule.
        bool contraryRule=Match(facts[2].Text,inclusive?excludedRule:includedRule);
        if(inclusive)badRule=vi
            ?@"không tính ngày bắt đầu, không tính ngày kết thúc|chỉ tính ngày làm việc|chỉ tính ngày kết thúc|không tính ngày bắt đầu và các ngày ở giữa, chỉ tính ngày kết thúc"
                +@"|tính cả ngày bắt đầu và ngày kết thúc nhưng bỏ qua (?:cuối tuần|ngày nghỉ)|tính ngày bắt đầu, không tính ngày kết thúc"
            :@"exclude both the start date and the end date|count only working days|count only the end date"
                +@"|include both the start date and the end date, excluding (?:weekends|holidays)|include the start date and exclude the end date"
                +@"|count all days from the (?:second|end) date (?:up to and including it|through that same date)";
        bool swappedDates=facts.Take(2).Any(f=>Has(f.Text,vi?@"ngày \{f2\} tháng \{f[01]\}":@"day \{f2\} of month \{f[01]\}"));
        bool outsideMonth=Match(facts[1].Text,"(?:"+Fact("v3","f1")+") "+(vi?"(?:tháng sau|năm sau|khác tháng)":"(?:next month|the following month|next year)"));
        if(contraryRule||Match(facts[2].Text,badRule)||Match(d.Question,wrongTarget)||swappedDates||outsideMonth)
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Keep both date roles in the same month/year and the profile's endpoint-counting convention; ask the calendar-day count without holiday exclusions.");
        if(OpenQuestionProse.Fields(d).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one pupil-facing sentence per field.");
        string question=vi?@"(?:hỏi )?"+owner+@" (?:là|kéo dài) bao nhiêu ngày"
                +@"|(?:hỏi )?số ngày (?:của )?"+owner+@" là bao nhiêu"
            :@"how many days (?:does|did) "+owner+@" last|what (?:is|was) the (?:duration|length) of "+owner+@" in days"
                +@"|how many days did "+owner+@" cover"
                +@"|what (?:is|was) the duration(?: in days)? of "+owner;
        if(inclusive)question=vi
            ?@"(?:hỏi )?"+owner+@" được tính bao nhiêu ngày|(?:hỏi )?số ngày được tính (?:cho|của) "+owner+@" là bao nhiêu"
                +@"|(?:hỏi )?"+owner+@" (?:là|kéo dài) bao nhiêu ngày"
            :@"how many (?:calendar )?days are counted for "+owner+@"|what is the number of (?:calendar )?days in "+owner
                +@"|how many (?:calendar )?days (?:does|did) "+owner+@" last";
        string lead=vi?@"(?:tổng )?số ngày(?: (?:trôi qua|đã trôi qua))?|(?:khoảng )?thời gian (?:trôi qua|đã trôi qua)"
            :@"(?:the )?(?:elapsed days|number of elapsed days|total number of days|elapsed time|duration)";
        if(!inclusive&&!vi&&(c.SceneId.EndsWith("/library",StringComparison.Ordinal)||c.SceneId.EndsWith("/equipment",StringComparison.Ordinal)))lead+=@"|loan duration";
        if(inclusive)lead=vi?@"(?:tổng )?số ngày(?: được tính)?":@"(?:the )?(?:number of (?:calendar )?days|calendar days|total number of days)";
        bool[] checks=[Match(facts[0].Text,Fact("v2","f0")),Match(facts[1].Text,Fact("v3","f1")),
            Match(facts[2].Text,rule),Match(d.Question,question),Match(d.SolutionLeads![0].Text,lead),
            facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&d.Question.TrimEnd().EndsWith('?')];
        if(checks.All(b=>b))return new(d,null,c);
        string[] roles=["start event/date","end event/date in the same month/year",inclusive?"include-both-endpoints rule":"exclude-start/include-end rule","calendar-day target","day-count lead","sentence punctuation"];
        return new(d,"SemanticReviewRequired",c,"Could not prove: "+string.Join(", ",roles.Where((_,i)=>!checks[i]))+". Review before use.");
    }
}
