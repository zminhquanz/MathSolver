using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class OvernightTimeCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Time
        &&c.BankVariant==(int)OvernightTimePilot.Type
        &&c.Story?.NarrativeId.StartsWith(OvernightTimePilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)OvernightTimePilot.Type)throw new ArgumentException("InvalidOvernightVariant");
        var d=OvernightTimePilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateOvernightTime(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["start-hour","start-minute","end-hour","end-minute"];
        var quantities=roles.Select((r,i)=>new NarrativeQuantity("f"+i,r,d.Values["f"+i])).Concat(
            [new("day_offset","end-day-offset","1"),new("v0","time-actor",d.Context.Actor),new("v1","time-activity",d.Context.Activity)]).ToArray();
        var facts=d.Context.Facts.Select((text,i)=>new NarrativeClause("fact_"+i,text)).ToArray();
        string signature=string.Join("|",OvernightTimePilot.Relation,d.Profile,c.Language,c.Tier,
            System.Text.Json.JsonSerializer.Serialize(d.Context),"end-day-offset=1",string.Join("/",roles));
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        string unit=c.Language==AppLanguage.Vietnamese?"phút":"minutes";
        return new(q,d.Profile,d.Context.Actor,unit,q.ElementaryProblem!.AnswerText,schema,quantities,facts,d.Context.Question,
            [new("step_0",d.Context.Lead,QuizMathExpressionFormatter.Format(d.Expression)+" = "+d.Elapsed+" "+unit)],
            facts.Select((f,i)=>(f.Role,Texts:new[]{f.Text,d.Context.AlternativeFacts[i]})).ToDictionary(x=>x.Role,x=>x.Texts),
            [d.Context.Question,d.Context.AlternativeQuestion],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation=>OvernightTimePilot.Relation
        +"; f0=start-hour; f1=start-minute; f2=end-hour; f3=end-minute; end-day-offset=ONE following day (immutable, not a slot); v0=person; v1=complete activity noun phrase; CONTINUOUS, NO BREAK; target=elapsed MINUTES.";
    internal static string WritingInstructions(BasicQuestionContract c)=>c.Language==AppLanguage.Vietnamese?"""
        Viết đề toán tiểu học mới, ngắn, tự nhiên. Ví dụ chỉ tham khảo; được thay cách mở đầu, cấu trúc câu và từ nối.
        {v1} đã là tên đầy đủ của hoạt động; gắn với người bằng {v1} của {v0}, không thêm tên người hoặc hoạt động khác.
        fact_0 nêu giờ bắt đầu. fact_1 nêu giờ kết thúc và PHẢI ghi rõ ngày hôm sau so với lúc bắt đầu. fact_2 nói hoạt động liên tục, không nghỉ.
        Đây là một hoạt động qua nửa đêm đúng một lần, kết thúc ngày liền sau. Không thêm buổi khác, khoảng nghỉ, lịch, buổi trưa hay ngày thứ hai tiếp nữa.
        Giữ từng biến đúng trường và một lần; không điền giá trị thật hoặc đáp án. Viết giờ, phút bên cạnh biến thời gian.
        Câu hỏi phải hỏi thời gian kéo dài bao nhiêu phút, không hỏi giờ bắt đầu/kết thúc hoặc chỉ hỏi bao lâu.
        Khi chủ ngữ là chuyến xe, chuyến tàu, hành trình hay buổi trực, hỏi hoạt động kéo dài bao nhiêu phút hoặc hỏi thời lượng của hoạt động; động từ trôi qua chỉ gắn với thời gian.
        solution_leads đặt tên cho đại lượng cần tính bằng một cụm danh từ ngắn, ví dụ: Thời lượng hoạt động. Không viết câu mệnh lệnh yêu cầu tính, hướng dẫn giải hoặc phép tính trong trường này.
        Mỗi dữ kiện một câu kết thúc bằng dấu chấm; câu hỏi kết thúc bằng dấu hỏi. Dùng câu đúng ngữ pháp, không có lời đệm hoặc hướng dẫn kỹ thuật. Chỉ trả JSON đúng cấu trúc, role và step.
        """:"""
        Write a new, short, natural elementary word problem. The example is a reference; vary sentence openings and phrasing.
        {v1} is an activity noun phrase without an article. Keep it with its owner as {v0}'s {v1} or the {v1} for {v0}. Add no other actor or activity.
        fact_0 states the start clock. fact_1 states the end clock and MUST explicitly say the next day relative to the start. fact_2 states continuous activity without breaks.
        There is one activity crossing midnight once, ending on the immediately following day. Add no break, second session, calendar rule, noon offset or extra day.
        Keep placeholders once in their own fields; fill no values or answers. Write hours and minutes beside the clock placeholders.
        Ask how many minutes the named activity lasts. Keep the activity noun phrase intact; minutes is the unit of the answer, not part of the activity name. For a question about duration, use duration of the activity, measured in minutes. Ask neither a start/end clock nor an unspecified how long.
        solution_leads is a short noun phrase naming the unknown, for example: Elapsed duration. Do not write an instruction to calculate, a worked method or an imperative in this field.
        Use a single grammatical sentence per field, periods for facts and a question mark for the question. Avoid filler and technical notes. Return only JSON with unchanged roles and step.
        """;
    internal static string WritingFocus(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        bool clockFirst=c.SceneId.EndsWith("train",StringComparison.Ordinal)||c.SceneId.EndsWith("watch",StringComparison.Ordinal);
        return "\n"+(clockFirst
            ?vi?"Có thể mở câu bằng mốc giờ; câu kết thúc vẫn phải ghi ngày hôm sau.":"You may put the clock first; explicitly keep the following-day marker in the end fact."
            :vi?"Có thể mở câu bằng tên hoạt động; câu hỏi nêu rõ số phút kéo dài.":"You may put the named activity first; explicitly ask how many minutes it lasts.");
    }
}
