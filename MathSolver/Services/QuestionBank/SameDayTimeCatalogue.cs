using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class SameDayTimeCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Time&&c.Story?.NarrativeId.StartsWith(SameDayTimePilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var d=SameDayTimePilot.Create(c.Story!.NarrativeId,(ElementaryQuizType)c.BankVariant,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateSameDayTime(ArithmeticQuizMode.Essay,(ElementaryQuizType)c.BankVariant,c.Language,c.Tier,d.Profile);
        var p=q.ElementaryProblem!;
        string[] roles=d.Relation.Id=="elapsed"?["start-hour","start-minute","end-hour","end-minute"]
            :[d.Relation.Id=="start"?"end-hour":"start-hour",d.Relation.Id=="start"?"end-minute":"start-minute","duration-minutes"];
        var quantities=roles.Select((role,i)=>new NarrativeQuantity("f"+i,role,d.Values["f"+i])).Concat(new[]{new NarrativeQuantity("v0","time_actor",d.Context.Actor),new NarrativeQuantity("v1","time_activity",d.Context.Activity)}).ToArray();
        string calculation=QuizMathExpressionFormatter.Format(d.Expression)+" = "+d.Result+(c.Language==AppLanguage.Vietnamese?" phút":" minutes");
        var facts=d.Relation.Facts.Select((text,i)=>new NarrativeClause("fact_"+i,text)).ToArray();
        string signature=string.Join("|",d.Profile,c.Language,c.Tier,string.Join("/",quantities.Select(v=>v.Role+(v.Id.StartsWith('v')?v.Value:""))),string.Join("/",d.Relation.Facts),d.Relation.Question,d.Relation.Lead);
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        return new(q,d.Profile,d.Context.Actor,c.Language==AppLanguage.Vietnamese?"phút":"minutes",p.AnswerText,schema,quantities,facts,d.Relation.Question,
            [new("step_0",d.Relation.Lead,calculation)],facts.Select((f,i)=>(f.Role,Choices:new[]{f.Text,d.Relation.AlternativeFacts[i]})).ToDictionary(x=>x.Role,x=>x.Choices),
            [d.Relation.Question,d.Relation.AlternativeQuestion],new Dictionary<string,string[]>{{"step_0",[d.Relation.Lead]}});
    }
    internal static string PromptRelation(BasicQuestionContract c)
    {
        string relation=c.SceneId.Split('/')[1];
        return "time_relation="+relation+"; SAME DAY; one uninterrupted activity; no breaks, waiting, second sessions or midnight crossing. "
            +string.Join("; ",ReasoningStoryCatalogue.Lesson(c).Quantities.Select(q=>"{"+q.Id+"}="+q.Role))
            +". "+(relation switch{"elapsed"=>"Known START and END clocks; target DURATION in minutes, not a clock time.",
                "start"=>"Known END clock and DURATION; target START clock = end minus duration, not end or duration.",
                _=>"Known START clock and DURATION; target END clock = start plus duration, not start or duration."});
    }

    internal static string OpenPrompt(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string relation=c.SceneId.Split('/')[1];
        string roles=relation switch
        {
            "elapsed"=>vi?"{f0} giờ {f1} phút là lúc bắt đầu; {f2} giờ {f3} phút là lúc kết thúc. Hỏi khoảng thời gian bằng phút."
                :"{f0} hours {f1} minutes is the start; {f2} hours {f3} minutes is the end. Ask for elapsed minutes.",
            "start"=>vi?"{f0} giờ {f1} phút là lúc kết thúc; {f2} phút là thời lượng. Hỏi thời điểm bắt đầu, không hỏi thời lượng."
                :"{f0} hours {f1} minutes is the end; {f2} minutes is the duration. Ask for the start time, not the duration.",
            _=>vi?"{f0} giờ {f1} phút là lúc bắt đầu; {f2} phút là thời lượng. Hỏi thời điểm kết thúc, không hỏi thời lượng."
                :"{f0} hours {f1} minutes is the start; {f2} minutes is the duration. Ask for the end time, not the duration."
        };
        return (vi?"""
            Viết lại bài toán thời gian bằng lời văn mới, tự nhiên cho học sinh tiểu học. Ví dụ chỉ để tham khảo.
            {v0} là tên người, {v1} là tên đầy đủ của hoạt động (cụm danh từ), không phải số hay đơn vị.
            Có thể mở đầu bằng mốc giờ hoặc hoạt động, đổi cấu trúc câu và từ nối. Giữ từng biến trong đúng trường như ví dụ; không thêm biến, số, người hay điều kiện.
            Hoạt động liên tục trong cùng một ngày, không nghỉ. Phải nói rõ điều này trong dữ kiện cuối.
            Câu hỏi chỉ hỏi đúng đại lượng yêu cầu. Câu dẫn lời giải chỉ gọi tên đại lượng đó, không tính toán.
            Mỗi trường là một câu ngắn, rõ nghĩa. Nội dung câu văn chỉ dành cho học sinh, không chứa tên trường hoặc hướng dẫn kỹ thuật.
            Chỉ trả JSON theo cấu trúc ví dụ, giữ nguyên role và step.
            """:"""
            Write a NEW, natural elementary time word problem. The example is a reference, not mandatory wording.
            {v0} is a person's name; {v1} is the full activity noun phrase, never a number or unit.
            You may start a sentence with the clock time or activity and vary sentence structure and links. Keep every placeholder in its example field; add no placeholders, values, actors or conditions.
            The activity is continuous on the same day, without breaks. State this explicitly in the last fact.
            Ask only for the requested quantity. The solution lead names that quantity without calculations.
            Use a short, complete sentence in each field. Prose is for pupils: no field names or technical instructions inside it.
            Return only JSON with the example's structure and unchanged role and step.
            """)+"\n"+roles+"\n"+(vi?"Ví dụ tham khảo:\n":"Reference example:\n")
            +QuestionBankStore.SerializeDraft(ReasoningStoryCatalogue.Draft(c));
    }
}
