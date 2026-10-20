using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

/// <summary>Proves event/date ownership and the counting convention independently.</summary>
internal static class SameMonthCalendarMeaning
{
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese,inclusive=SameMonthCalendarPilot.IsInclusive(c.SceneId),adjacent=SameMonthCalendarPilot.IsAdjacent(c.SceneId);
        var facts=d.Facts!;
        if(SameMonthCalendarPilot.IsFebruaryLeap(c.SceneId)&&FebruaryLeapCalendarLanguage.HasContradiction(d,c.Language))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"February has 29 days in the supplied leap year; keep that year, both dates, and the inclusive day-count target.");
        if(SameMonthCalendarPilot.IsFebruaryCommon(c.SceneId)&&FebruaryCommonCalendarLanguage.HasContradiction(d,c.Language))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"February has 28 days in the supplied common year; keep that year, both dates, and the inclusive day-count target.");
        int ruleIndex=adjacent?3:2;
        string Date(string day)
        {
            string month=adjacent&&day=="f1"?"f4":"f2";
            return vi?@"ngày \{"+day+@"\} tháng \{"+month+@"\} năm \{f3\}"
                :@"day \{"+day+@"\} of month \{"+month+@"\} in year \{f3\}";
        }
        string Event(string action)=>@"\{v0\} "+(vi?"(?:đã )?":"")+@"\{"+action+@"\} \{v1\}";
        string Fact(string action,string day)=>vi
            ?@"(?:theo ghi chép, )?"+Event(action)+@" (?:vào )?"+Date(day)+@"|(?:vào )?"+Date(day)+@", "+Event(action)
                +@"|ngày "+Event(action)+@" là "+Date(day)
                +(adjacent?@"|ngày "+Event(action)+@" rơi vào "+Date(day):"")
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
        excludedRule+="|"+CalendarCountingLanguage.ExcludeStart(c.Language);
        if(adjacent)excludedRule+="|"+AdjacentMonthCalendarLanguage.CountingRule(c.Language);
        includedRule+="|"+CalendarCountingLanguage.IncludeBoth(c.Language);
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
        bool contraryRule=Match(facts[ruleIndex].Text,inclusive?excludedRule:includedRule);
        if(inclusive)badRule=vi
            ?@"không tính ngày bắt đầu, không tính ngày kết thúc|chỉ tính ngày làm việc|chỉ tính ngày kết thúc|không tính ngày bắt đầu và các ngày ở giữa, chỉ tính ngày kết thúc"
                +@"|tính cả ngày bắt đầu và ngày kết thúc nhưng bỏ qua (?:cuối tuần|ngày nghỉ)|tính ngày bắt đầu, không tính ngày kết thúc"
            :@"exclude both the start date and the end date|count only working days|count only the end date"
                +@"|include both the start date and the end date, excluding (?:weekends|holidays)|include the start date and exclude the end date"
                +@"|count all days from the (?:second|end) date (?:up to and including it|through that same date)";
        bool swappedDates=facts.Take(2).Any(f=>Has(f.Text,vi?@"ngày \{f2\} tháng \{f[01]\}":@"day \{f2\} of month \{f[01]\}"));
        if(adjacent)swappedDates|=Has(facts[1].Text,vi?@"ngày \{f4\} tháng \{f1\}":@"day \{f4\} of month \{f1\}");
        string outsideScope=adjacent
            ?vi?"(?:cùng tháng|năm sau|hai tháng sau)":"(?:in the same month|next year|two months later)"
            :vi?"(?:tháng sau|năm sau|khác tháng)":"(?:next month|the following month|next year)";
        bool outsideMonth=Match(facts[1].Text,"(?:"+Fact("v3","f1")+") "+outsideScope);
        if(contraryRule||Match(facts[ruleIndex].Text,badRule)||Match(facts[ruleIndex].Text,CalendarCountingLanguage.WrongScope(c.Language))
            ||adjacent&&AdjacentMonthCalendarLanguage.CountingContradiction(facts[ruleIndex].Text,c.Language)
            ||Match(d.Question,wrongTarget)||swappedDates||outsideMonth)
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
                +@"|how many (?:calendar )?days are in "+owner
                +@"|how many (?:calendar )?days (?:does|did) "+owner+@" last";
        // Endpoint convention is proved separately; the same clear day-count
        // question may therefore serve either relation without changing its rule.
        question+=vi?@"|(?:hỏi )?số ngày (?:của )?"+owner+@" là bao nhiêu"
            :@"|what (?:is|was) the (?:duration|length) of "+owner+@"(?: in days)?"
                +@"|what (?:is|was) the duration in days of "+owner
                +@"|what is the (?:total )?(?:count|number) of (?:calendar )?days (?:for|in) "+owner;
        if(vi)
        {
            var context=SameMonthCalendarPilot.Contexts(c.Language,inclusive,adjacent,SameMonthCalendarPilot.IsFebruaryCommon(c.SceneId),SameMonthCalendarPilot.IsFebruaryLeap(c.SceneId)).Single(row=>
                SameMonthCalendarPilot.ProfilePrefix(inclusive,adjacent,SameMonthCalendarPilot.IsFebruaryCommon(c.SceneId),SameMonthCalendarPilot.IsFebruaryLeap(c.SceneId))+row.Id==c.SceneId);
            // "Thời gian đợt đọc sách" names a duration. Prefixing a loan
            // already named "thời gian mượn ..." repeats its interval label.
            if(!context.Interval.StartsWith("thời gian",StringComparison.OrdinalIgnoreCase)
                &&!context.Interval.StartsWith("khoảng thời gian",StringComparison.OrdinalIgnoreCase))
                question+=@"|thời gian "+owner+@" là bao nhiêu ngày";
        }
        string lead=vi?@"(?:tổng )?số ngày(?: (?:trôi qua|đã trôi qua|được tính))?|(?:khoảng )?thời gian (?:trôi qua|đã trôi qua)"
            :@"(?:the )?(?:elapsed days|number of elapsed days|total number of days|elapsed time|duration)";
        if(!inclusive&&!vi&&(c.SceneId.EndsWith("/library",StringComparison.Ordinal)||c.SceneId.EndsWith("/equipment",StringComparison.Ordinal)))lead+=@"|loan duration|duration of (?:the )?loan";
        if(inclusive)lead=vi?@"(?:tổng )?số ngày(?: được tính)?":@"(?:the )?(?:number of (?:calendar )?days|calendar days|total number of days)";
        if(!vi)lead+=@"|(?:the )?(?:(?:total )?(?:calendar )?days|(?:total )?number of (?:calendar )?days)(?: in the period)?|duration in days";
        bool[] checks=[Match(facts[0].Text,Fact("v2","f0")),Match(facts[1].Text,Fact("v3","f1")),
            Match(facts[ruleIndex].Text,rule),Match(d.Question,question)||adjacent&&!vi&&AdjacentMonthCalendarLanguage.ProvesTarget(d.Question),Match(d.SolutionLeads![0].Text,lead),
            facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&d.Question.TrimEnd().EndsWith('?')];
        if(adjacent)
        {
            string length=vi?@"tháng \{f2\} có \{f5\} ngày|số ngày của tháng \{f2\} là \{f5\}"
                :@"month \{f2\} (?:has|contains) \{f5\} days|(?:the )?number of days in month \{f2\} is \{f5\}|there are \{f5\} days in month \{f2\}";
            if(Match(facts[2].Text,vi?@"tháng \{f2\} không có \{f5\} ngày|tháng \{f5\} có \{f2\} ngày":@"month \{f2\} does not (?:have|contain) \{f5\} days|month \{f5\} has \{f2\} days"))
                return new(null,"ChangedRelationOrTarget",ErrorDetails:"The start month must have the C# month length.");
            checks=[..checks,Match(facts[2].Text,length)];
        }
        if(checks.All(b=>b))return new(d,null,c);
        string[] roles=["start event/date",adjacent?"end event/date in the next month of the same year":"end event/date in the same month/year",inclusive?"include-both-endpoints rule":"exclude-start/include-end rule","calendar-day target","day-count lead","sentence punctuation",..adjacent?new[]{"start-month length"}:Array.Empty<string>()];
        return new(d,"SemanticReviewRequired",c,"Could not prove: "+string.Join(", ",roles.Where((_,i)=>!checks[i]))+". Review before use.");
    }
}
