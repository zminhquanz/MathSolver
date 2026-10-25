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
        Viết đề toán ngắn, tự nhiên cho học sinh tiểu học. Có thể thay cấu trúc câu; ví dụ chỉ để tham khảo.
        Giữ mỗi biến đúng trường và đúng một lần. {v0} là cụm danh từ đầy đủ; {v1} đã là tên hình đầy đủ, không thêm chữ hình trước biến.
        Câu đầu nêu {v0} có dạng {v1}. Câu đáy cho trực tiếp độ dài đáy {f0} {v2} của {v0}.
        Câu chiều cao cho trực tiếp {f1} {v2} của {v0}, nói rõ chiều cao vuông góc với chính đáy đó. Phải nhắc đáy đó; không thay chiều cao bằng cạnh bên hoặc chiều cao của đáy khác.
        Câu hỏi chỉ hỏi diện tích toàn bộ {v0} theo {v3}. {v2} là đơn vị độ dài, {v3} đã là đơn vị diện tích; không thêm ký hiệu bình phương.
        Mỗi trường là một câu; dữ kiện kết thúc bằng dấu chấm, câu hỏi kết thúc bằng dấu hỏi. Câu dẫn lời giải chỉ gọi diện tích {v0}.
        Không thêm cắt ghép, lát gạch, lối đi, chi phí, suy ra cạnh, đổi đơn vị, điều kiện hoặc phủ định. Không điền số, tên, đơn vị thật, phép tính hoặc đáp án. Chỉ trả JSON theo cấu trúc mẫu.
        """:"""
        Write a short natural elementary word problem. The example is a reference; vary clear sentence constructions.
        Preserve every placeholder once in its own field. {v0} is the complete object noun phrase with its article; {v1} already contains the complete shape name.
        State that {v0} is a {v1}. The base fact supplies the base of {v0} directly as {f0} {v2}.
        The height fact supplies {f1} {v2} for {v0}, explicitly describing the perpendicular height corresponding to that base. Say that base to connect it to the supplied base. A side or a height to another base is not interchangeable.
        Ask only for the entire area of {v0} in {v3}. {v2} is the length unit; {v3} already is the area unit. Add no square symbol or literal unit words.
        Use one grammatical sentence per field. Facts end with a period; the question is an interrogative ending with a question mark. The solution lead only names the area of {v0}.
        Add no cutting, joined shapes, paths, tiling, costs, inferred sides, conversions, conditions or negation. Fill no actual names, numbers, units, calculations or answers. Return only JSON with the reference schema.
        """;
}
