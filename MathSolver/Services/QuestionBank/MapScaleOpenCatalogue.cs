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
        "{v0}=complete map noun phrase; {v1}=complete single segment noun phrase; {v2}=map centimetres; {v3}=actual metres. step_0=actual length in centimetres; step_1=actual length in metres.";
    internal static string WritingInstructions(BasicQuestionContract c)=>WritingInstructions(c.Language);
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết bài toán tiểu học ngắn, tự nhiên về một đoạn thẳng trên bản đồ và độ dài thực tế của chính đoạn đó.
        Hai dữ kiện riêng: tỉ lệ của {v0} là một trên {f0}; {v1} trên {v0} dài {f1} {v2}. Viết chữ một, không viết chữ số hay kí hiệu tỉ lệ.
        Chỉ hỏi độ dài thực tế của {v1} theo {v3}. Không hỏi ngược độ dài trên bản đồ, không ghép đoạn hay đổi sang đơn vị khác.
        step_0 và step_1 là cụm danh từ gọi độ dài thực tế của {v1}; không nêu phép tính hay mệnh lệnh.
        Được đổi cấu trúc câu; ví dụ chỉ tham khảo. Giữ mỗi biến đúng số lần và đúng trường. {v0} và {v1} chứa đủ tên đồ vật, không thêm tên hay từ đếm quanh biến.
        Mỗi trường một câu; dữ kiện cuối dấu chấm, câu hỏi cuối dấu hỏi. Không điền số, tên, đơn vị, phép tính, đáp án; không thêm điều kiện, phủ định hay sự kiện. Chỉ trả JSON theo cấu trúc mẫu.
        """:"""
        Write a short natural elementary problem about one segment on a map and its actual length.
        Two separate facts: {v0} has a scale of one to {f0}; {v1} on {v0} measures {f1} {v2}. Spell out one; use no digit or ratio symbol.
        Ask only for the actual length of {v1} in {v3}. Preserve the direction from map length to actual length and a single segment.
        step_0 and step_1 are noun phrases naming the actual length of {v1}; do not mention calculations or use imperatives.
        Vary sentence structure naturally; the example is a reference. Keep every placeholder in its field with the same count. {v0} and {v1} are complete noun phrases; add no article or object name around them.
        One sentence per field; facts end in periods, question in a question mark. Add no names, numbers, literal units, calculations, answers, conditions, negation or events. Return only JSON in the reference structure.
        """;
}
