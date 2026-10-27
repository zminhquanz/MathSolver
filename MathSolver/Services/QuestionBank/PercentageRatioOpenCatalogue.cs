using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class PercentageRatioOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Percentage&&c.Story?.NarrativeId.StartsWith(PercentageRatioPilot.Prefix,StringComparison.Ordinal)==true;
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode)
        =>new PercentageQuizGenerator(new(c.Story!.Seed)).GenerateOpenRatio(mode,c.Language,c.Tier,c.Story.NarrativeId);
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=PercentageRatioPilot.Variant)throw new ArgumentException("InvalidPercentageRatioVariant");
        var d=PercentageRatioPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));var q=Generate(c,ArithmeticQuizMode.Essay);
        string[] roles=["whole-count-at-100-percent","subset-count-of-that-whole","whole-owner","whole-counted-objects","whole-noun","subset-counted-objects","subset-noun"];
        var quantities=d.Values.Select((x,i)=>new NarrativeQuantity(x.Key,roles[i],x.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.Whole),new("fact_1",d.Context.Part)];
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",d.Profile,c.Language,c.Tier,PercentageRatioPilot.Relation,d.Context,string.Join("/",roles)))));
        return new(q,d.Profile,d.Render(d.Context.Lead),"%",d.Answer+" %",schema,quantities,facts,d.Context.Question,
            [new("step_0",d.Context.Lead,q.PercentageProblem!.EquationText+" %")],facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),
            [d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation(BasicQuestionContract c)=>PercentageRatioPilot.Relation+"; f0=whole count (100-percent base); f1=count of a subset within f0, not an extra stock; v0=owner; v1/v2=whole items/noun; v3/v4=subset items/noun; target=part divided by whole, expressed as a percentage; static inventory, no events";
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết bài toán ngắn, tự nhiên về một nhóm đồ vật nằm trong toàn bộ đã biết.
        Ví dụ chỉ tham khảo; được đổi cách mở đầu, cấu trúc câu và từ nối, giữ nguyên vai trò và mỗi biến trong đúng trường.
        {f0} là tổng số {v1} của {v0}; {f1} là số {v3} đã nằm trong chính tổng đó, không phải lượng thêm vào.
        {v2} gọi toàn bộ đồ vật; {v4} gọi loại đồ vật thuộc nhóm con. Câu hỏi tìm tỉ số phần trăm số {v4} so với tổng số {v2} của {v0}.
        Dùng từ phần trăm trong câu hỏi, không thêm số hoặc kí hiệu. Không hỏi số lượng, phần còn lại, tăng giảm hay số nhóm.
        Câu hỏi dùng quan hệ chiếm phần trăm của toàn bộ thuộc {v0}; không đẩy chủ sở hữu ra đuôi câu bằng trong {v0}.
        Câu dẫn là cụm danh từ gọi tỉ số phần trăm nhóm con so với toàn bộ, không bắt đầu bằng Tính, Tìm hoặc Hãy. Dữ kiện kể thực tế, không chỉ dẫn tính toán.
        Mỗi trường một câu ngắn. Giữ biến, role, step; không tự thêm tên, đơn vị, điều kiện, sự kiện hoặc đáp án. Chỉ trả JSON.
        """:"""
        Write a short natural word problem about a subset already included within a known whole.
        Examples are references, not mandatory wording. Vary openings and structure; preserve each placeholder once in its original field.
        {f0} is the whole count owned by {v0}; {f1} counts the {v3} already within that SAME whole, never an additional collection.
        {v2} names all objects; {v4} names their subset. Ask what percentage of {v0}'s {v2} are {v4}; preserve the denominator as the original whole.
        Use percentage in the question, without adding numbers or symbols. Do not ask for a count, remainder, increase or number of groups.
        Ask what percentage of the owner's whole are the subset; avoid saying the whole objects consist of the subset objects.
        The solution heading is a noun phrase naming the subset percentage relative to the whole; never start with Calculate, Find or Determine. Facts describe inventory, not instructions.
        Use one short sentence per field. Preserve role/step and variables; invent no names, units, events, conditions or answers. Return only JSON.
        """;
}
