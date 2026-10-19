using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class SameMonthCalendarCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Time
        &&(c.BankVariant==(int)SameMonthCalendarPilot.Type
            &&c.Story?.NarrativeId.StartsWith(SameMonthCalendarPilot.Prefix,StringComparison.Ordinal)==true
        ||c.BankVariant==(int)SameMonthCalendarPilot.InclusiveType
            &&c.Story?.NarrativeId.StartsWith(SameMonthCalendarPilot.InclusivePrefix,StringComparison.Ordinal)==true
        ||c.BankVariant==(int)SameMonthCalendarPilot.AdjacentType
            &&c.Story?.NarrativeId.StartsWith(SameMonthCalendarPilot.AdjacentPrefix,StringComparison.Ordinal)==true);
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(SameMonthCalendarPilot.ProfileType(c.Story!.NarrativeId)!=(ElementaryQuizType)c.BankVariant)
            throw new ArgumentException("MismatchedCalendarCountingProfile");
        var d=SameMonthCalendarPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateSameMonthCalendar(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["start-day","end-day","month","year","calendar-actor","calendar-item","start-action","end-action","calendar-interval"];
        string[] slots=["f0","f1","f2","f3","v0","v1","v2","v3","v4"];
        if(SameMonthCalendarPilot.IsAdjacent(d.Profile))
        {
            roles=["start-day","end-day","start-month","year","end-month","start-month-length","calendar-actor","calendar-item","start-action","end-action","calendar-interval"];
            slots=["f0","f1","f2","f3","f4","f5","v0","v1","v2","v3","v4"];
        }
        var quantities=slots.Select((id,i)=>new NarrativeQuantity(id,roles[i],d.Values[id])).Concat(
            [new("include_start","include-start",d.IncludeStart?"1":"0"),new("include_end","include-end","1")]).ToArray();
        var facts=d.Context.Facts.Select((text,i)=>new NarrativeClause("fact_"+i,text)).ToArray();
        string signature=string.Join("|",SameMonthCalendarPilot.ProfileRelation(d.Profile),d.Profile,c.Language,c.Tier,
            System.Text.Json.JsonSerializer.Serialize(d.Context),$"include-start={(d.IncludeStart?1:0)};include-end=1",string.Join("/",roles));
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        string unit=c.Language==AppLanguage.Vietnamese?"ngày":"days";
        return new(q,d.Profile,d.Context.Actor,unit,q.ElementaryProblem!.AnswerText,schema,quantities,facts,d.Context.Question,
            [new("step_0",d.Context.Lead,QuizMathExpressionFormatter.Format(d.Expression)+" = "+d.Elapsed+" "+unit)],
            facts.Select((f,i)=>(f.Role,Texts:new[]{f.Text,d.Context.AlternativeFacts[i]})).ToDictionary(x=>x.Role,x=>x.Texts),
            [d.Context.Question,d.Context.AlternativeQuestion],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation(BasicQuestionContract c)=>SameMonthCalendarPilot.ProfileRelation(c.SceneId)
        +(SameMonthCalendarPilot.IsAdjacent(c.SceneId)?AdjacentMonthCalendarLanguage.Relation:"; f0=start day; f1=end day; f2=the SAME month in both dates; f3=the SAME year in both dates; v0=actor; v1=item; v2=start action; v3=end action; v4=interval noun phrase; "
        +(SameMonthCalendarPilot.IsInclusive(c.SceneId)?"INCLUDE both endpoints, count EVERY calendar day; equal dates count once; target=counted calendar DAYS.":"EXCLUDE start, INCLUDE end; target=elapsed DAYS."));
    internal static string WritingInstructions(BasicQuestionContract c)
    {
        if(SameMonthCalendarPilot.IsAdjacent(c.SceneId))return AdjacentMonthCalendarLanguage.WritingInstructions(c);
        bool vi=c.Language==AppLanguage.Vietnamese,inclusive=SameMonthCalendarPilot.IsInclusive(c.SceneId);
        string count=vi
            ?inclusive?"Tính mọi ngày, gồm cả ngày bắt đầu và ngày kết thúc.":"Không tính ngày bắt đầu, có tính ngày kết thúc; tính đủ các ngày từ ngày liền sau ngày bắt đầu đến hết ngày kết thúc."
            :inclusive?"Count every calendar day, including both the start date and the end date.":"Exclude the start date and include the end date; count every day from the day immediately after the start date through the end date.";
        return (vi?"""
        Bạn biên soạn đề toán tiểu học ngắn, tự nhiên. Được viết câu mới và đổi cách mở câu; ví dụ chỉ tham khảo.
        Mỗi biến là một cụm từ đã hoàn chỉnh. Nối trực tiếp {v0} {v2} {v1} với ngày {f0} tháng {f2} năm {f3} trong fact_0; nối {v0} {v3} {v1} với ngày {f1} tháng {f2} năm {f3} trong fact_1. Ngày có thể đứng đầu câu. Giữ đúng sự kiện và thứ tự, không thêm động từ vào cụm hành động.
        fact_2 nêu rõ quy ước đếm ngày bên dưới. Không dời ngày sự kiện hoặc bỏ ngày trung gian/ngày nghỉ.
        Trong câu hỏi, dùng {v4} của {v0} làm MỘT cụm danh từ duy nhất. Cụm này đã gọi đầy đủ khoảng cần hỏi; ghép trực tiếp với câu hỏi số ngày, không đặt thêm nhãn trước cụm. Hỏi số ngày của khoảng, không hỏi số đợt hoặc số ngày sử dụng thật của đồ vật.
        Câu dẫn lời giải chỉ là tên đại lượng, ví dụ Số ngày được tính. Mỗi trường một câu, dữ kiện kết thúc dấu chấm, câu hỏi dấu hỏi. Chỉ trả JSON đúng role/step và giữ biến trong đúng trường; không điền tên, số, đáp án hoặc chỉ dẫn kỹ thuật.
        """:"""
        Write a short, natural elementary word problem. Write new sentences and vary openings; examples are references.
        Each placeholder is already a complete phrase. Attach the date directly to {v0} {v2} {v1} in fact_0 and to {v0} {v3} {v1} in fact_1. The first date uses day {f0} of month {f2} in year {f3}; the last uses day {f1} of that same month and year. The date may come first. Preserve the completed actions and event order without adding verbs.
        fact_2 states the counting rule below in one plain sentence. Do not move events or skip intervening dates, weekends or holidays.
        In the question, {v0}'s {v4} is ONE complete noun phrase. Attach a clear question about its number of days directly to that phrase. Ask for days or duration, never an unspecified total count or actual days of use of a borrowed item. Use the complete phrase once, without extra interval labels.
        Use a short noun phrase such as Number of days as the solution lead. One grammatical sentence per field, periods for facts, question mark for the question. Return only JSON with the correct roles/step and placeholders in their own fields. Fill no names, numbers, answers or technical instructions.
        """)+"\n"+count+"\n"+(vi?"Quy ước trên là bắt buộc về ý nghĩa, được diễn đạt lại ngắn gọn.":"Preserve the counting convention above exactly in meaning; its wording may vary.")
            +(!vi&&!inclusive?" The day immediately after the start date is not the second supplied date, which is the END date.":"");
    }
    internal static string ContextNotes(BasicQuestionContract c)
    {
        if(SameMonthCalendarPilot.IsAdjacent(c.SceneId))return AdjacentMonthCalendarLanguage.ContextNotes(c);
        bool inclusive=SameMonthCalendarPilot.IsInclusive(c.SceneId),adjacent=SameMonthCalendarPilot.IsAdjacent(c.SceneId);
        string prefix=adjacent?SameMonthCalendarPilot.AdjacentPrefix:inclusive?SameMonthCalendarPilot.InclusivePrefix:SameMonthCalendarPilot.Prefix;
        var context=SameMonthCalendarPilot.Contexts(c.Language,inclusive,adjacent).Single(row=>prefix+row.Id==c.SceneId);
        bool vi=c.Language==AppLanguage.Vietnamese;
        string position=context.Id is "equipment" or "model" or "practice" or "reading"
            ?vi?"Ưu tiên mở một dữ kiện bằng ngày rồi đến người và hành động.":"Prefer opening one event with its date, then the actor and action."
            :vi?"Ưu tiên mở một dữ kiện bằng người và hành động rồi đến ngày.":"Prefer opening one event with the actor and action, then its date.";
        bool loan=context.Id is "library" or "equipment";
        string example=vi?loan?"{v4} của {v0} kéo dài bao nhiêu ngày?":"Số ngày của {v4} của {v0} là bao nhiêu?"
            :loan?"What is the duration of {v0}'s {v4} in days?":"How many days did {v0}'s {v4} last?";
        // Do not repeat the interval's literal name alongside its placeholder:
        // that glossary entry was priming redundant question prefixes.
        return "\n"+(vi?"Nghĩa hành động để hiểu bối cảnh, không chép vào JSON: ":"Action meanings for context only, not literal JSON wording: ")
            +$"v1={context.Item}; v2={context.StartAction}; v3={context.EndAction}. "+position
            +"\n"+(vi?"Câu hỏi minh họa cách dùng cụm biến hoàn chỉnh: ":"Question illustrating the complete placeholder phrase: ")+example
            +(vi?" Được viết câu khác nếu vẫn hỏi số ngày của cùng cụm, giữ đúng quy ước.":" Other wording is welcome if it asks for the days of the same interval under the same counting rule.");
    }
}
