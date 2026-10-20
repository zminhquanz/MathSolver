using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

/// <summary>The adjacent-month relation has its own dates and month-length fact.</summary>
internal static class AdjacentMonthCalendarLanguage
{
    internal const string Relation="; f0=start day; f1=end day; f2=start month; f3=the SAME year for both dates; f4=end month, exactly f2+1; f5=number of days in the START month; v0=actor; v1=item; v2=start action; v3=end action; v4=complete interval noun phrase. Both months have 30/31 days. EXCLUDE start, INCLUDE end, count every intervening day; target=elapsed DAYS. No February, holidays, extra months or year crossing.";
    internal static string PromptRelation(BasicQuestionContract c)=>SameMonthCalendarPilot.IsFebruaryCommon(c.SceneId)?FebruaryCommonCalendarLanguage.Relation:SameMonthCalendarPilot.IsInclusive(c.SceneId)
        ?Relation.Replace("EXCLUDE start, INCLUDE end, count every intervening day; target=elapsed DAYS.","INCLUDE both start and end, count every intervening day; target=counted calendar DAYS.",StringComparison.Ordinal):Relation;
    internal static string WritingInstructions(BasicQuestionContract c)=>SameMonthCalendarPilot.IsInclusive(c.SceneId)
        ?AdjacentMonthInclusiveLanguage.WritingInstructions(c):ElapsedInstructions(c);
    private static string ElapsedInstructions(BasicQuestionContract c)=>c.Language==AppLanguage.Vietnamese?"""
        Bạn biên soạn toán đố tiểu học tự nhiên. Được viết câu mới, thay cách mở đầu và từ nối; ví dụ chỉ tham khảo.
        Mỗi biến đã là một cụm hoàn chỉnh. fact_0 gắn {v0} {v2} {v1} với ngày {f0} tháng {f2} năm {f3}. fact_1 gắn {v0} {v3} {v1} với ngày {f1} tháng {f4} năm {f3}. Chọn cách đặt ngày trước hoặc sau người và hành động theo gợi ý riêng của bối cảnh. Giữ nguyên các cụm hành động và thứ tự sự kiện.
        Hai tháng liền nhau, cùng năm, mỗi tháng có 30 hoặc 31 ngày. fact_2 chỉ cho biết tháng {f2} có {f5} ngày. fact_3 là QUY ƯỚC ĐẾM: bỏ ngày bắt đầu, tính ngày kết thúc và mọi ngày ở giữa. Chẳng hạn: Khi đếm ngày, không tính ngày đầu và có tính ngày cuối. Các sự kiện vẫn xảy ra đúng hai ngày đã cho; chỉ việc đếm ngày bắt đầu từ ngày liền sau ngày đầu. Không thêm điều kiện hoặc ngày nghỉ.
        Câu hỏi ghép trực tiếp MỘT cụm danh từ hoàn chỉnh {v4} của {v0} với vị ngữ hỏi số ngày. Ví dụ: Hỏi {v4} của {v0} kéo dài bao nhiêu ngày? Hoặc: Số ngày của {v4} của {v0} là bao nhiêu? Cụm biến đã gọi đầy đủ khoảng cần hỏi; dùng nguyên cụm một lần, không thêm nhãn thời gian, từ nối từ khi hoặc hành động sử dụng trước nó. Chỉ hỏi số ngày của khoảng này.
        Câu dẫn lời giải là tên đại lượng, ví dụ Số ngày trôi qua. Mỗi trường một câu, dữ kiện kết thúc dấu chấm, câu hỏi dấu hỏi. Chỉ trả JSON, giữ nguyên các biến trong đúng trường, không điền tên, số, đáp án hoặc chỉ dẫn kỹ thuật.
        """:"""
        Write a short, natural elementary word problem. Write new sentences and vary openings; examples are references.
        Each placeholder is already a complete phrase. fact_0 attaches {v0} {v2} {v1} to day {f0} of month {f2} in year {f3}. fact_1 attaches {v0} {v3} {v1} to day {f1} of month {f4} in year {f3}. Vary date-first and actor-first openings using the context-specific guidance. Preserve actors, completed actions and event order.
        The months are consecutive within one year, both with 30 or 31 days. fact_2 states only that month {f2} has {f5} days. fact_3 is a COUNTING RULE: exclude the start date and include the end date, counting every intervening day. For example: The day count begins on the day immediately after the start date and ends on the end date. Only counting begins later; the actual loan or activity still starts on the original date. The day following the start date is NOT the second supplied date, which is the end date. Add no other conditions.
        Use {v0}'s {v4} as ONE complete noun phrase, directly asking its duration in days. Examples: What is the total duration of {v0}'s {v4} in days? Or: How many days did {v0}'s {v4} last? That complete phrase already names the interval; use it once without any extra period labels or verbs about actual use. Other clear day-count questions are welcome.
        The solution lead is a short quantity name, such as Number of days. One sentence per field; periods for facts, question mark for the question. Return only JSON with correct roles and placeholders in their own fields. Fill no names, numbers, answers or technical instructions.
        """;

    // References vary by context without changing the persisted List/schema.
    // They illustrate wording; GBNF still permits other sentence structures.
    internal static BasicQuestionDraft Reference(BasicQuestionContract c)
    {
        var d=ReasoningStoryCatalogue.Draft(c);bool vi=c.Language==AppLanguage.Vietnamese;
        var context=Context(c);
        var facts=d.Facts!.ToArray();
        bool dateFirstStart=context.Id is "equipment" or "garden";
        bool dateFirstEnd=context.Id is "library" or "garden";
        string Event(int i,bool dateFirst)
        {
            string actor=$"{{v0}} {{v{i+2}}} {{v1}}",day=i==0?"{f0}":"{f1}",month=i==0?"{f2}":"{f4}";
            string date=vi?$"ngày {day} tháng {month} năm {{f3}}":$"day {day} of month {month} in year {{f3}}";
            return dateFirst?(vi?$"Vào {date}, {actor}.":$"On {date}, {actor}.")
                :$"{actor} "+(vi?"vào ":"on ")+date+".";
        }
        facts[0]=new("fact_0",Event(0,dateFirstStart));facts[1]=new("fact_1",Event(1,dateFirstEnd));
        if(context.Id=="model")facts[0]=new("fact_0",(vi?"Theo ghi chép, ":"According to the record, ")+Event(0,false));
        return d with {Facts=facts,Question=SameMonthCalendarPilot.IsInclusive(c.SceneId)?d.Question:vi?"Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?":"What is the total duration of {v0}'s {v4} in days?"};
    }
    internal static string ContextNotes(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string focus=Context(c).Id switch
        {
            "library"=>vi?"Mở sự kiện đầu bằng người, sự kiện cuối bằng ngày.":"Use an actor-first opening for the first event and a date-first opening for the last event.",
            "equipment"=>vi?"Mở sự kiện đầu bằng ngày, sự kiện cuối bằng người.":"Use a date-first opening for the first event and an actor-first opening for the last event.",
            "garden"=>vi?"Dùng hai câu mở bằng ngày để kể hai mốc chăm sóc.":"Use date-first openings to describe both plant-care dates.",
            _=>vi?"Có thể mở bằng Theo ghi chép, rồi kể người và hành động; sự kiện cuối nối trực tiếp với ngày.":"You may start with According to the record, then the actor and action; attach the last date directly to its event."
        };
        return "\n"+(vi?"Gợi ý đổi cách mở câu cho bối cảnh này, không phải mẫu câu bắt buộc: ":"Context-specific variation guidance, not mandatory sentences: ")+focus
            +"\n"+(vi?"Người {v0} và cụm hành động {v2}/{v3} đã hoàn chỉnh. Câu hỏi chỉ cần một cụm {v4} của {v0}; nối trực tiếp với kéo dài bao nhiêu ngày hoặc là bao nhiêu ngày.":"The actor {v0} and action phrases {v2}/{v3} are complete. The question needs just the complete phrase {v0}'s {v4}, directly linked to duration in days or how many days it lasted.");
    }
    private static SameMonthCalendarPilot.Context Context(BasicQuestionContract c)=>SameMonthCalendarPilot.Contexts(c.Language,SameMonthCalendarPilot.IsInclusive(c.SceneId),adjacent:true,february:SameMonthCalendarPilot.IsFebruaryCommon(c.SceneId))
        .Single(row=>SameMonthCalendarPilot.ProfilePrefix(SameMonthCalendarPilot.IsInclusive(c.SceneId),true,SameMonthCalendarPilot.IsFebruaryCommon(c.SceneId))+row.Id==c.SceneId);

    internal static string CountingRule(AppLanguage language)=>language==AppLanguage.Vietnamese
        ?@"khi đếm ngày, không tính ngày (?:bắt đầu|đầu) và (?:có )?tính (?:cả )?ngày (?:kết thúc|cuối)"
            +@"|khi tính khoảng thời gian, không tính ngày bắt đầu mà tính cả ngày kết thúc"
            +@"|khi đếm khoảng thời gian, không tính ngày bắt đầu mà chỉ tính ngày kết thúc và tất cả các ngày nằm giữa"
            +@"|việc đếm ngày không bao gồm ngày bắt đầu mà (?:sẽ )?tính (?:cả )?ngày kết thúc(?:, đồng thời đếm tất cả các ngày ở giữa)?"
        :@"for counting, exclude the start date and include the end date"
            +@"|the day count (?:begins|starts) on the day (?:immediately )?(?:after|following) the start date and (?:ends|finishes) on the end date(?:, counting (?:all intervening days|every (?:intervening|calendar) day))?"
            +@"|counting begins the day after the start date and concludes on the final date"
            +@"|the day count begins after the initial date and concludes on the final date, counting all days in between";
    internal static bool CountingContradiction(string text,AppLanguage language)=>language==AppLanguage.Vietnamese
        &&Match(text,@"khi đếm khoảng thời gian, không tính ngày bắt đầu mà chỉ tính ngày kết thúc");
    internal static bool ProvesTarget(string text)=>Match(text,@"what (?:is|was) the total (?:duration|length) of (?:\{v0\}['’]s \{v4\}|the \{v4\} for \{v0\}) in days");
}
