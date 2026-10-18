using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class SameDayBreakCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Time
        &&c.Story?.NarrativeId.StartsWith(SameDayBreakPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)SameDayBreakPilot.Type)throw new ArgumentException("InvalidSameDayBreakVariant");
        var d=SameDayBreakPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateSameDayBreak(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["start-hour","start-minute","end-hour","end-minute","included-break-minutes"];
        var quantities=roles.Select((r,i)=>new NarrativeQuantity("f"+i,r,d.Values["f"+i])).Concat(
            [new("v0","time-actor",d.Context.Actor),new NarrativeQuantity("v1","time-activity",d.Context.Activity)]).ToArray();
        var facts=d.Context.Facts.Select((text,i)=>new NarrativeClause("fact_"+i,text)).ToArray();
        string signature=string.Join("|",SameDayBreakPilot.Relation,d.Profile,c.Language,c.Tier,
            System.Text.Json.JsonSerializer.Serialize(d.Context),string.Join("/",roles));
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        string unit=c.Language==AppLanguage.Vietnamese?"phút":"minutes";
        return new(q,d.Profile,d.Context.Actor,unit,q.ElementaryProblem!.AnswerText,schema,quantities,facts,d.Context.Question,
            [new("step_0",d.Context.Lead,QuizMathExpressionFormatter.Format(d.Expression)+" = "+d.Active+" "+unit)],
            facts.Select((f,i)=>(f.Role,Texts:new[]{f.Text,d.Context.AlternativeFacts[i]})).ToDictionary(x=>x.Role,x=>x.Texts),
            [d.Context.Question,d.Context.AlternativeQuestion],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation=>SameDayBreakPilot.Relation
        +"; f0=start-hour; f1=start-minute; f2=end-hour; f3=end-minute; f4=one break in minutes INCLUDED between those clocks; v0=person; v1=complete activity noun phrase; target=active minutes EXCLUDING that break; SAME DAY.";
    internal static string WritingInstructions(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        return vi?"""
        Viết đề mới, tự nhiên cho học sinh tiểu học. Ví dụ chỉ tham khảo; được đổi cách mở đầu, cấu trúc và từ nối.
        {v0} là tên người; {v1} đã là cụm danh từ đầy đủ gọi buổi hoạt động. Không đổi tên hoạt động hoặc tự thêm chữ buổi trước biến.
        Hai mốc giờ bắt đầu và kết thúc thuộc cùng một ngày. Có đúng một lần nghỉ nằm bên trong khoảng giữa hai mốc giờ ấy; thời gian nghỉ đã nằm trong tổng khoảng thời gian, không phải sau khi kết thúc.
        Giữ từng biến đúng trường và chỉ một lần; không thay biến bằng số hoặc tên thật, không tiết lộ đáp án. Viết rõ đơn vị giờ, phút bên cạnh biến thời gian. Không thêm buổi khác, lần nghỉ khác hoặc ngày hôm sau.
        Câu hỏi chỉ tìm số phút thực hiện không tính thời gian nghỉ, không hỏi tổng thời gian gồm nghỉ hay mốc bắt đầu/kết thúc. Không nói hoạt động liên tục không nghỉ.
        Mỗi trường một câu ngắn đúng ngữ pháp: dữ kiện kết thúc bằng dấu chấm, câu hỏi nghi vấn bằng dấu hỏi và phải có chữ phút. Dùng văn phong đề toán, gọi rõ hoạt động và người thực hiện; tránh lời đệm hội thoại, đại từ mơ hồ hoặc thông tin ghi chép ngoài đề.
        fact_0 chỉ nêu lúc bắt đầu, fact_1 chỉ nêu lúc kết thúc, fact_2 nói rõ một lần nghỉ trong buổi, fact_3 chỉ xác nhận cùng ngày. Không thay fact_3 bằng lời nhắc về cách tính.
        Câu dẫn lời giải là cụm danh từ ngắn gọi thời gian hoạt động thực tế, như Thời gian hoạt động không tính nghỉ. Chỉ gọi tên đại lượng, không mô tả cách tính. Đọc lại để tránh lặp danh từ hoặc dính chữ.
        Chỉ trả JSON theo cấu trúc tham khảo, giữ role và step. Không viết chỉ dẫn kỹ thuật, phép tính hoặc lời giải vào câu văn.
        """:"""
        Write a new, natural elementary word problem. The example is a reference; vary the opening and sentence construction.
        {v0} is a person's name; {v1} is an activity noun phrase WITHOUT an article, for example study session. Keep the activity and owner together as {v0}'s {v1} or the {v1} for {v0}. Do not change the activity.
        The start and end clocks are on the same day. There is exactly one break WITHIN that session: it is already included between those clocks, never after the end.
        Keep each placeholder once in its own field; do not replace it with a name or number or reveal the answer. Write the literal units hours and minutes beside clock placeholders. Add no second session, extra break or next day.
        Ask only for active minutes excluding the break, not total elapsed minutes including it and not a start/end clock. Do not call the activity uninterrupted.
        Use one short grammatical sentence per field. Facts end with a period; the question ends with a question mark and MUST explicitly say minutes and exclude the break. A minutes label in the solution lead is not enough.
        fact_0 states the start, fact_1 the end, fact_2 the only break inside the session, and fact_3 the same-day condition. Avoid invented records, initial/final sessions, vague references and conversational filler. Keep the same tense throughout.
        The solution lead is a short noun phrase naming the quantity, such as Active time excluding the break. Name the quantity rather than describing a calculation. Check for repeated nouns and merged words.
        Return only JSON with the reference schema and unchanged roles/step. Write no technical notes, calculations or answers inside the prose.
        """;
    }
    internal static string WritingFocus(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string id=c.SceneId[SameDayBreakPilot.Prefix.Length..];
        string focus=id switch
        {
            "study"=>vi?"Ưu tiên gọi tên buổi hoạt động ở đầu câu mốc giờ; câu hỏi mở bằng Không tính thời gian nghỉ."
                :"Favor the named session as the subject of the clock facts; start the question with Excluding the break.",
            "sport"=>vi?"Có thể mở câu mốc giờ bằng Vào lúc; nói rõ chỉ nghỉ một lần trong buổi."
                :"You may put the clock first in a fact; explicitly place the only break inside the session.",
            "craft"=>vi?"Ưu tiên cách nói thời gian thực hiện thực tế cho câu hỏi, vẫn nói rõ không tính nghỉ."
                :"Favor an active-duration question in minutes that explicitly excludes the break; keep side information short.",
            _=>vi?"Có thể thay cấu trúc câu giờ kết thúc và hỏi số phút hoạt động không kể thời gian nghỉ."
                :"Vary the clock sentence opening; ask for the duration in minutes excluding the break, not time remaining after the session."
        };
        return "\n"+focus;
    }
}
