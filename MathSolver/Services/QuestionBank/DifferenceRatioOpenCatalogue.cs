using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class DifferenceRatioOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.TwoNumbers
        &&c.Story?.NarrativeId.StartsWith(DifferenceRatioPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)ElementaryQuizType.DifferenceRatio)throw new ArgumentException("InvalidDifferenceRatioRelation");
        var d=DifferenceRatioPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateOpenDifferenceRatio(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["larger-minus-smaller-difference","smaller-to-larger-ratio","smaller-owner","larger-owner","common-counted-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.Difference),new("fact_1",d.Context.Ratio)];
        string[] leads=[d.Context.PartLead,d.Context.SmallerLead,d.Context.LargerLead];
        var p=q.ElementaryProblem!;
        var steps=p.Reasoning!.Steps.Select((s,i)=>new NarrativeStep("step_"+i,leads[i],
            QuizMathExpressionFormatter.Format(s.Expression)+" = "+s.DisplayValue+" "+s.Unit)).ToArray();
        string signature=string.Join("\n",DifferenceRatioPilot.Relation,d.Profile,c.Language,c.Tier,string.Join("/",roles),d.Context);
        return new(q,d.Profile,d.Context.SmallerOwner+" / "+d.Context.LargerOwner,d.Context.Unit,p.AnswerText,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))),quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],steps.ToDictionary(s=>s.Id,s=>new[]{s.Lead}));
    }
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết đề hiệu–tỉ ngắn, tự nhiên cho học sinh tiểu học. Ví dụ chỉ tham khảo; được đổi cách mở đầu và cấu trúc câu.
        Hai người có cùng loại đồ vật. {v1} có nhiều hơn {v0} một lượng {f0} {v2}; đây là hiệu, không phải tổng.
        Số {v2} của {v0} bằng {f1} số {v2} của {v1}. Phân số thuộc lượng của {v1}, không thuộc tổng hay hiệu.
        Hỏi rõ số đồ vật riêng của từng người. Không chỉ hỏi chung cả hai, hỏi tổng hoặc hỏi hiệu.
        Giữ nguyên các biến và số lần xuất hiện trong từng trường. {f1} đã chứa toàn bộ phân số. Không điền tên, số hoặc đồ vật cụ thể.
        Câu dẫn chỉ gọi số đồ vật ứng với mỗi phần, rồi số của {v0}, rồi số của {v1}; không viết phép tính hay đáp án.
        Không thêm tặng, nhận, thay đổi số lượng hoặc điều kiện. Mỗi trường một câu ngắn; dữ kiện kết thúc bằng dấu chấm, câu hỏi bằng dấu hỏi. Chỉ trả JSON.
        """:"""
        Write a short, natural difference-and-ratio word problem for primary pupils. The example is a reference; vary the opening and sentence structure.
        {v1} has {f0} more {v2} than {v0}. This is the difference, not a combined total.
        The number of {v2} owned by {v0} equals {f1} of the number of {v2} owned by {v1}. Keep the ratio direction and its base owner.
        Ask explicitly for each owner's separate count, using each/respectively or two separate questions. Do not ask only for a count for both, a total or a difference.
        Preserve each field's placeholders and multiplicities. {f1} already contains the whole fraction. All names, counts and item names come from placeholders; do not fill them in.
        Headings name the count per equal ratio part, then {v0}'s count, then {v1}'s count. No calculation or answer.
        No transfers, changes or extra conditions. One short sentence per field; facts end in periods, the question in a question mark. Return only JSON.
        """;
    internal static string PromptRelation(BasicQuestionContract c)=>
        "relation="+DifferenceRatioPilot.Relation+"; f0=larger-minus-smaller difference; f1=smaller/larger fraction; v0=smaller owner; v1=larger owner; v2=common counted unit. Find both counts, labeled by owner. C# owns all numbers, arithmetic and answers. Write new prose; keep these roles.";
}
