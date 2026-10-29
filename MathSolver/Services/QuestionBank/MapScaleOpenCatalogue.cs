using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class MapScaleOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Measurement&&c.Story?.NarrativeId.StartsWith(MapScaleOpenPilot.Prefix,StringComparison.Ordinal)==true;
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode)
    {
        if(!MapScaleOpenPilot.ValidateContract(c))throw new ArgumentException("InvalidMapScaleOpenContract");
        return new ElementaryQuizGenerator(new(c.Story!.Seed)).GenerateOpenMapScale(mode,c.Language,c.Tier,c.Story.NarrativeId);
    }
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var q=Generate(c,ArithmeticQuizMode.Essay);var d=MapScaleOpenPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        string[] roles=["scale-denominator","map-cm","map-object","single-map-segment","map-length-unit","actual-length-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.ScaleFact),new("fact_1",d.Context.LengthFact)];
        string[] leads=[d.Context.CentimetreLead,d.Context.ActualLead];var p=q.ElementaryProblem!;
        var steps=p.Reasoning!.Steps.Select((s,i)=>new NarrativeStep("step_"+i,leads[i],
            QuizMathExpressionFormatter.Format(s.Expression)+" = "+s.DisplayValue+" "+s.Unit)).ToArray();
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",MapScaleOpenPilot.Relation,d.Profile,c.Language,c.Tier,d.Context,string.Join("/",roles)))));
        return new(q,d.Profile,d.Context.Map,"m",p.AnswerText,schema,quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(x=>x.Role,x=>new[]{x.Text}),[d.Context.Question],steps.ToDictionary(x=>x.Id,x=>new[]{x.Lead}));
    }
    internal static string PromptRelation(BasicQuestionContract c)=>"relation="+MapScaleOpenPilot.Relation+
        "; {f0}=positive scale denominator; scale is one to {f0}, never reversed. {f1}=length of a single {v1} on {v0} in {v2}; target=actual length of {v1} in {v3}. "+
        "{v0}=complete map noun phrase; {v1}=complete single segment noun phrase; {v2}=map-length unit slot; {v3}=actual-length unit slot. Both solution_leads name the actual length of {v1}; C# owns the conversion and step units.";
    internal static string WritingInstructions(BasicQuestionContract c)=>WritingInstructions(c.Language);
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết bài toán tiểu học ngắn, tự nhiên về một đoạn thẳng trên bản đồ và độ dài thực tế của chính đoạn đó.
        Hai dữ kiện riêng: tỉ lệ của {v0} là một trên {f0}; độ dài của {v1} đo trên {v0} là {f1} {v2}. Viết chữ một trong tỉ lệ.
        Câu hỏi hoàn chỉnh tìm độ dài thực tế của chính {v1}, biểu thị bằng {v3}.
        Hai câu dẫn step_0 và step_1 chỉ là cụm danh từ gọi tên đại lượng, ví dụ “Chiều dài thực tế của {v1}”. Đơn vị và phép tính đã thuộc từng bước lời giải; câu dẫn kết thúc ở tên đoạn đường.
        Viết khác ví dụ bằng cách đổi cấu trúc câu hoặc vị trí cụm chỉ bản đồ, thay vì thêm lời bình. {v0} và {v1} là tên đầy đủ; dùng nguyên biến, mỗi biến đúng số lần trong đúng trường.
        Đơn vị trong dữ kiện và câu hỏi dùng đúng các biến {v2}, {v3}; câu dẫn chỉ dùng biến tên đoạn đường như mẫu. Các trường là nội dung cho học sinh, không nhắc đến JSON, biến, bước hay chỉ dẫn viết bài.
        Mỗi trường một câu ngắn; dữ kiện cuối dấu chấm, câu hỏi cuối dấu hỏi. Giữ nguyên một đoạn, tỉ lệ và hướng tìm độ dài thực tế; không thêm sự kiện, điều kiện hoặc đáp án. Trả JSON theo cấu trúc mẫu.
        """:"""
        Write a short natural elementary problem about one segment on a map and its actual length.
        Two separate facts: the scale of {v0} is one to {f0}; the length of {v1} measured on {v0} is {f1} {v2}. Spell out one in the scale.
        Write a complete question asking the actual length of that same {v1}, expressed in {v3}.
        Both solution_leads are short noun phrases naming the quantity, for example “The actual distance represented by {v1}”. Step units and calculations belong to the solution, so each heading ends with the segment's name.
        Write differently from the example by changing sentence structure or placing the map phrase differently, rather than adding commentary. {v0} and {v1} are complete noun phrases; use the placeholders directly, in the same fields and counts.
        Unit words in facts and question use their slots {v2} and {v3}; headings use only the segment-name slot as in the reference. Every field is student-facing content, without JSON, placeholder, step or writing instructions.
        One short sentence per field; facts end in periods, question in a question mark. Preserve a single segment, its scale and the map-to-actual direction; add no events, conditions or answers. Return JSON in the reference structure.
        """;
}
