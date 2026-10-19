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
            &&c.Story?.NarrativeId.StartsWith(SameMonthCalendarPilot.InclusivePrefix,StringComparison.Ordinal)==true);
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(SameMonthCalendarPilot.ProfileType(c.Story!.NarrativeId)!=(ElementaryQuizType)c.BankVariant)
            throw new ArgumentException("MismatchedCalendarCountingProfile");
        var d=SameMonthCalendarPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateSameMonthCalendar(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["start-day","end-day","month","year","calendar-actor","calendar-item","start-action","end-action","calendar-interval"];
        string[] slots=["f0","f1","f2","f3","v0","v1","v2","v3","v4"];
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
        +"; f0=start day; f1=end day; f2=the SAME month in both dates; f3=the SAME year in both dates; v0=actor; v1=item; v2=start action; v3=end action; v4=interval noun phrase; "
        +(SameMonthCalendarPilot.IsInclusive(c.SceneId)?"INCLUDE both endpoints, count EVERY calendar day; equal dates count once; target=counted calendar DAYS.":"EXCLUDE start, INCLUDE end; target=elapsed DAYS.");
    internal static string WritingInstructions(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese,inclusive=SameMonthCalendarPilot.IsInclusive(c.SceneId);
        string count=vi
            ?inclusive?"Tính mọi ngày, gồm cả ngày bắt đầu và ngày kết thúc.":"Không tính ngày bắt đầu, có tính ngày kết thúc; tính đủ các ngày từ ngày liền sau ngày bắt đầu đến hết ngày kết thúc."
            :inclusive?"Count every calendar day, including both the start date and the end date.":"Exclude the start date and include the end date; count every day from the day immediately after the start date through the end date.";
        return (vi?"""
        Viết đề toán tiểu học ngắn, tự nhiên. Ví dụ là tài liệu tham khảo; được đổi cách mở câu và cách nối từ.
        Chỉ dùng biến cho người, hành động, đối tượng và tên khoảng thời gian. Hai cụm {v0} {v2} {v1} và {v0} {v3} {v1} đã là câu chủ thể–hành động đầy đủ: nối trực tiếp mỗi cụm với ngày tương ứng. Không chèn thêm bắt đầu/kết thúc, không chép thêm từ ngữ trong tên biến.
        fact_0 giữ sự kiện đầu và ngày {f0} tháng {f2} năm {f3}; fact_1 giữ sự kiện cuối và ngày {f1} tháng {f2} năm {f3}. Được đặt ngày trước hoặc sau chủ thể.
        fact_2 chỉ nêu quy ước đếm ngày ngắn gọn, không dời ngày sự kiện, không bỏ ngày trung gian, cuối tuần hoặc ngày nghỉ.
        Câu hỏi dùng nguyên cụm {v4} của {v0} một lần và hỏi số ngày của khoảng đó. {v4} đã là tên ĐẦY ĐỦ; không thêm tên hoạt động, khoảng thời gian hoặc thời gian mượn bên ngoài biến. Không hỏi số ngày thực sự sử dụng đồ vật thay cho thời gian mượn.
        Câu dẫn là cụm danh từ ngắn chỉ số ngày, không ra lệnh tính. Mỗi trường một câu, dữ kiện kết thúc dấu chấm, câu hỏi kết thúc dấu hỏi. Giữ biến, role và step; không điền tên, số hoặc đáp án, không đưa chỉ dẫn kỹ thuật vào đề.
        """:"""
        Write a short, natural elementary word problem. Examples are references; vary sentence openings and wording.
        Use placeholders for actors, actions, items and interval names. Both {v0} {v2} {v1} and {v0} {v3} {v1} already form complete subject-action phrases in the past tense: attach the date directly. Add no extra start/finish verb or repeated context words.
        fact_0 reports the first event on day {f0} of month {f2} in year {f3}; fact_1 reports the last event on day {f1} of month {f2} in year {f3}. You may put the date before or after the actor.
        fact_2 states the counting convention in one short grammatical sentence. Keep actual event dates unchanged; exclude no intervening days, weekends or holidays.
        Ask for the number of days in {v0}'s {v4}, using that complete possessive phrase once. The interval name already comes from {v4}; add no other interval label or activity. For a loan ask its duration, not the number of days the item was actually used.
        Use a short noun phrase for the solution lead, never a command. Avoid circular wording such as yields the count or gives a total of days. One grammatical sentence per field, periods for facts, question mark for the question. Keep placeholders, roles and step; fill no names, numbers or answers, and add no technical notes.
        """)+"\n"+count+"\n"+(vi?"Quy ước trên là bắt buộc về ý nghĩa, được diễn đạt lại ngắn gọn.":"Preserve the counting convention above exactly in meaning; its wording may vary.")
            +(!vi&&!inclusive?" The day immediately after the start date is not the second supplied date, which is the END date.":"");
    }
    internal static string ContextNotes(BasicQuestionContract c)
    {
        bool inclusive=SameMonthCalendarPilot.IsInclusive(c.SceneId);
        string prefix=inclusive?SameMonthCalendarPilot.InclusivePrefix:SameMonthCalendarPilot.Prefix;
        var context=SameMonthCalendarPilot.Contexts(c.Language,inclusive).Single(row=>prefix+row.Id==c.SceneId);
        bool vi=c.Language==AppLanguage.Vietnamese;
        string position=context.Id is "equipment" or "model" or "practice" or "reading"
            ?vi?"Ưu tiên mở một dữ kiện bằng ngày rồi đến người và hành động.":"Prefer opening one event with its date, then the actor and action."
            :vi?"Ưu tiên mở một dữ kiện bằng người và hành động rồi đến ngày.":"Prefer opening one event with the actor and action, then its date.";
        return "\n"+(vi?"Ý nghĩa từ ngữ đã chọn, chỉ để hiểu bối cảnh; vẫn dùng biến trong JSON: ":"Selected wording for context only; keep placeholders in JSON: ")
            +$"v1={context.Item}; v2={context.StartAction}; v3={context.EndAction}; v4={context.Interval}. "
            +(vi?"Chỉ dùng các biến để gọi người, hành động, đối tượng và khoảng thời gian trong câu; không chép lại các tên trên bên ngoài biến. ":"Refer to the actor, actions, item and interval using their placeholders only; do not copy these context words elsewhere in the prose. ")+position;
    }
}
