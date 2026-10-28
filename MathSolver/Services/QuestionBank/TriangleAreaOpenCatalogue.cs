using MathSolver.Models;
using MathSolver.Services.Core;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class TriangleAreaOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Geometry&&c.Story?.NarrativeId.StartsWith(TriangleAreaPilot.Prefix,StringComparison.Ordinal)==true;
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode)
    {
        if(c.BankVariant!=TriangleAreaPilot.Variant)throw new ArgumentException("InvalidTriangleAreaVariant");
        return new GeometryQuizGenerator(new GeometryCalculationEngine(),new(c.Story!.Seed)).GenerateOpenTriangleArea(mode,c.Language,c.Tier,c.Story.NarrativeId);
    }
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var q=Generate(c,ArithmeticQuizMode.Essay);
        var d=TriangleAreaPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        NarrativeQuantity[] quantities=[new("f0","triangle-base",d.Values["f0"]),new("f1","triangle-corresponding-perpendicular-height",d.Values["f1"]),
            new("v0","triangle-object",d.Context.Object),new("v1","triangle-shape",d.Context.Shape),
            new("v2",d.Context.LengthUnitRole,d.LengthUnit),new("v3",d.Context.AreaUnitRole,d.AreaUnit)];
        NarrativeClause[] facts=[new("fact_0",d.Context.ShapeFact),new("fact_1",d.Context.BaseFact),new("fact_2",d.Context.HeightFact)];
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",TriangleAreaPilot.Relation,d.Profile,c.Language,c.Tier,d.Context,string.Join("/",quantities.Select(x=>x.Role))))));
        return new(q,d.Profile,d.Render(d.Context.Lead),d.AreaUnit,d.Area+" "+d.AreaUnit,schema,quantities,facts,d.Context.Question,
            [new("step_0",d.Context.Lead,q.GeometryProblem!.EquationText+" "+d.AreaUnit)],facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),
            [d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation(BasicQuestionContract c)=>TriangleAreaPilot.Relation+"; f0=base; f1=perpendicular height corresponding to that supplied base; v0=one object; v1=triangle; v2=length unit; v3=area unit; target=entire triangle area; direct given dimensions only";
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết đề toán tiểu học ngắn, tự nhiên. Được đổi cấu trúc câu; ví dụ chỉ tham khảo.
        Mỗi biến đúng trường, đúng một lần. {v0} là cụm danh từ đầy đủ; không thêm tên hay từ chỉ đồ vật. {v1} là tên hình đầy đủ: không thêm chữ hình trước biến.
        Ba dữ kiện riêng: {v0} có dạng {v1}; đáy của {v0} dài {f0} {v2}; chiều cao của {v0} dài {f1} {v2}, đo vuông góc với chính đáy đó. Nêu rõ đáy đó, không dùng cạnh bên hay đáy khác.
        Chỉ hỏi diện tích toàn bộ {v0} theo {v3}. {v2} là đơn vị độ dài; {v3} đã là đơn vị diện tích, không thêm bình phương.
        step_0 là cụm danh từ gọi diện tích {v0}; không dùng động từ mệnh lệnh như Tính, Tìm, Hãy.
        Mỗi trường một câu; dữ kiện cuối dấu chấm, câu hỏi cuối dấu hỏi. Không thêm sự kiện, cắt ghép, điều kiện, phủ định, tên, số, đơn vị thật, phép tính hay đáp án. Chỉ trả JSON cùng cấu trúc.
        """:"""
        Write a short natural elementary problem; vary sentence structure. The example is a reference.
        Keep each placeholder once in its field. {v0} is the complete object noun phrase: add no article, object name or actor before it. {v1} already contains the complete shape name; add no extra shape word.
        Three separate facts state {v0}'s shape {v1}, its supplied base {f0} {v2}, and its perpendicular height {f1} {v2} corresponding to that base. Explicitly connect the height to that supplied base, never another base or a side length.
        Ask only for the entire area of {v0} in {v3}. {v2} is a length unit; {v3} already is an area unit, so add no square symbol or literal units.
        step_0 is a noun phrase naming the area of {v0}, without an imperative such as Calculate, Find or Determine.
        One sentence per field: facts end with periods, the question with a question mark. Add no events, conditions, negation, names, numbers, calculations or answers. Return only JSON in the reference schema.
        """;
}
