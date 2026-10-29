using MathSolver.Models;
using MathSolver.Services.Core;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class QuadrilateralAreaOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Geometry&&QuadrilateralAreaPilot.IsVariant(c.BankVariant)
        &&c.Story?.NarrativeId.StartsWith(QuadrilateralAreaPilot.Prefix(c.BankVariant),StringComparison.Ordinal)==true;
    internal static IEnumerable<string> Profiles(BankQuestionFamily family,int variant,AppLanguage language)=>family==BankQuestionFamily.Geometry?QuadrilateralAreaPilot.Profiles(variant,language):[];
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode)
    {
        if(!IsProfile(c))throw new ArgumentException("InvalidQuadrilateralAreaVariant");
        return new GeometryQuizGenerator(new GeometryCalculationEngine(),new(c.Story!.Seed)).GenerateOpenQuadrilateralArea(mode,c.Language,c.Tier,c.Story.NarrativeId);
    }
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var q=Generate(c,ArithmeticQuizMode.Essay);
        var d=QuadrilateralAreaPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var quantities=new List<NarrativeQuantity>{new("f0",d.IsTrapezoid?"trapezoid-longer-parallel-base":"parallelogram-base",d.Values["f0"]),
            new("f1",d.IsTrapezoid?"trapezoid-shorter-parallel-base":"parallelogram-corresponding-perpendicular-height",d.Values["f1"])};
        if(d.IsTrapezoid)quantities.Add(new("f2","trapezoid-corresponding-perpendicular-height",d.Values["f2"]));
        quantities.AddRange([new("v0",d.Context.ShapeId+"-object",d.Context.Object),new("v1",d.Context.ShapeId+"-shape",d.Context.Shape),
            new("v2",d.Context.LengthUnitRole,d.LengthUnit),new("v3",d.Context.AreaUnitRole,d.AreaUnit)]);
        NarrativeClause[] facts=[new("fact_0",d.Context.ShapeFact),new("fact_1",d.Context.BaseFact),new("fact_2",d.Context.HeightFact)];
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",QuadrilateralAreaPilot.Relation(c.BankVariant),d.Profile,c.Language,c.Tier,d.Context,string.Join("/",quantities.Select(x=>x.Role))))));
        return new(q,d.Profile,d.Render(d.Context.Lead),d.AreaUnit,d.Area+" "+d.AreaUnit,schema,quantities.ToArray(),facts,d.Context.Question,
            [new("step_0",d.Context.Lead,q.GeometryProblem!.EquationText+" "+d.AreaUnit)],facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),
            [d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation(BasicQuestionContract c)=>QuadrilateralAreaPilot.Relation(c.BankVariant)
        +(c.BankVariant==QuadrilateralAreaPilot.TrapezoidVariant?"; f0=longer parallel base; f1=shorter parallel base; f2=perpendicular height between those bases":"; f0=supplied base; f1=perpendicular height corresponding to that base")
        +"; v0=complete object phrase; v1=complete shape name; v2=length unit; v3=area unit; target=entire area; direct supplied dimensions only";
    internal static string WritingInstructions(BasicQuestionContract c)
    {
        bool trap=c.BankVariant==QuadrilateralAreaPilot.TrapezoidVariant;
        string dimensions=c.Language==AppLanguage.Vietnamese
            ?trap?"{v0} có hai đáy song song, đáy lớn dài {f0} {v2} và đáy nhỏ dài {f1} {v2}. Chiều cao vuông góc giữa hai đáy đó của {v0} dài {f2} {v2}.":"Đáy của {v0} dài {f0} {v2}. Chiều cao vuông góc với chính đáy đó của {v0} dài {f1} {v2}."
            :trap?"{v0} has two parallel bases, the longer measuring {f0} {v2} and the shorter measuring {f1} {v2}. The perpendicular height of {v0} between those bases is {f2} {v2}.":"The supplied base of {v0} is {f0} {v2}. The perpendicular height of {v0} corresponding to that base is {f1} {v2}.";
        return c.Language==AppLanguage.Vietnamese?"""
            Viết đề toán tiểu học ngắn, tự nhiên; được đổi cấu trúc câu. Ví dụ chỉ để tham khảo.
            Giữ đúng các biến và số lần xuất hiện trong từng trường của ví dụ; đơn vị độ dài lặp lại bên mỗi đáy được cho. {v0} là cụm danh từ đầy đủ, không thêm tên đồ vật; {v1} là tên hình đầy đủ, không thêm chữ hình trước biến.
            Ba dữ kiện riêng: {v0} có dạng {v1}; các đáy đã cho; chiều cao vuông góc tương ứng với chính đáy đã cho, hoặc đo vuông góc giữa đúng hai đáy song song đã cho.
            """+"\n"+dimensions+"\n"+"""
            Viết câu hỏi đầy đủ có là bao nhiêu, chỉ hỏi diện tích toàn bộ {v0} theo {v3}; {v2} là đơn vị độ dài, {v3} đã là đơn vị diện tích, không thêm bình phương.
            step_0 là cụm danh từ gọi diện tích {v0}; không dùng động từ mệnh lệnh như Tính, Tìm, Hãy.
            Mỗi trường một câu; dữ kiện cuối dấu chấm, câu hỏi cuối dấu hỏi. Không thêm sự kiện, cắt ghép, điều kiện, phủ định, tên, số, đơn vị thật, phép tính hoặc đáp án. Chỉ trả JSON cùng cấu trúc.
            """:"""
            Write a short natural elementary problem; vary sentence structure. The example is a reference.
            Keep the reference placeholders and their occurrence counts in each field; repeat the length unit beside each supplied base. {v0} is the complete object noun phrase: add no article or object name. {v1} is the complete shape name; use a natural clause such as {v0} is a {v1}.
            Keep three separate facts: the object's shape; its supplied bases; its corresponding perpendicular height. The height is measured perpendicularly to that supplied base, or perpendicularly between those two supplied parallel bases.
            """+"\n"+dimensions+"\n"+"""
            Write a complete question beginning What is, asking only for the entire area of {v0} in {v3}. {v2} is a length unit; {v3} already is an area unit, so add no square symbol or literal units.
            step_0 is a noun phrase naming the area of {v0}, without an imperative such as Calculate, Find or Determine.
            One sentence per field: facts end with periods, the question with a question mark. Add no events, conditions, negation, names, numbers, calculations or answers. Return only JSON in the reference schema.
            """;
    }
}
