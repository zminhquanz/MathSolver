using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class RectangleSideOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Spatial
        &&c.Story?.NarrativeId.StartsWith(RectangleSidePilot.Prefix,StringComparison.Ordinal)==true;
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode)
    {
        if(c.BankVariant!=RectangleSidePilot.Variant)throw new ArgumentException("InvalidRectangleSideVariant");
        return new ElementaryQuizGenerator(new(c.Story!.Seed)).GenerateOpenRectangleSide(mode,c.Language,c.Tier,c.Story.NarrativeId);
    }
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var q=Generate(c,ArithmeticQuizMode.Essay);
        var d=RectangleSidePilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        NarrativeQuantity[] quantities=[new("f0","rectangle-perimeter",d.Values["f0"]),
            new("f1","rectangle-known-"+d.Context.KnownRole,d.Values["f1"]),new("v0","rectangle-object",d.Context.Object),
            new("v1","rectangle-shape",d.Context.Shape),new("v2",d.Context.UnitRole,d.Unit)];
        NarrativeClause[] facts=[new("fact_0",d.Context.ShapeFact),new("fact_1",d.Context.PerimeterFact),new("fact_2",d.Context.KnownFact)];
        NarrativeStep[] steps=[new("step_0",d.Context.HalfLead,$"{d.Perimeter} : 2 = {d.Perimeter/2} {d.Unit}"),
            new("step_1",d.Context.Lead,$"{d.Perimeter/2} − {d.Known} = {d.Target} {d.Unit}")];
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",d.Profile,c.Language,c.Tier,d.Context,
            string.Join("/",quantities.Select(x=>x.Role))))));
        return new(q,d.Profile,d.Render(d.Context.Lead),d.Unit,d.Target+" "+d.Unit,schema,quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],steps.ToDictionary(s=>s.Id,s=>new[]{s.Lead}));
    }
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết bài toán ngắn, tự nhiên cho học sinh tiểu học. Ví dụ chỉ để tham khảo; được đổi cấu trúc và cách mở câu.
        {v0} là cụm danh từ đầy đủ gọi đồ vật; {v1} là tên đầy đủ hình chữ nhật. Mỗi trường một câu, giữ từng biến đúng trường và đúng một lần.
        Ba dữ kiện lần lượt nêu hình dạng, toàn bộ chu vi {f0} {v2}, và cạnh đã biết {f1} {v2}. Chu vi không phải nửa chu vi.
        Giữ đúng chiều dài hoặc chiều rộng đã biết như quan hệ yêu cầu. Câu hỏi chỉ hỏi cạnh còn thiếu, theo {v2}; không hỏi lại cạnh đã biết.
        Chiều dài lớn hơn chiều rộng; không thêm điều kiện so sánh, tỉ số, diện tích, hình vuông, ghép hình, cổng, lối đi hay đổi đơn vị.
        Dữ kiện kết thúc bằng dấu chấm; câu hỏi dạng nghi vấn kết thúc bằng dấu hỏi. Không ghép lệnh Hãy tính với dấu hỏi.
        Hai câu dẫn lời giải lần lượt gọi nửa chu vi và cạnh cần tìm của {v0}. Chỉ câu dẫn đầu được gọi nửa chu vi.
        Được giữ một câu đơn giản và viết mới câu khác. Không thêm phủ định, giả thiết, sự kiện, phép tính, đáp án hoặc chỉ dẫn kỹ thuật. Chỉ trả JSON theo cấu trúc ví dụ.
        """:"""
        Write a short natural elementary word problem. The example is a reference; vary sentence openings and structures freely.
        {v0} is a complete object noun phrase with its article; {v1} is the complete shape name rectangle. Preserve each placeholder once in its own field.
        The three facts separately state the shape, the entire perimeter {f0} {v2}, and the known side {f1} {v2}. The perimeter is not a semiperimeter.
        Keep the given length or width role specified in the relation. Ask only for the other, unknown side in {v2}.
        The length exceeds the width. Add no comparison condition, ratio, area, square, joined shape, gate, path or unit conversion.
        Use one grammatical sentence per field. Facts end with a period and the question is an interrogative ending with a question mark.
        The two solution leads name half the perimeter and the requested side of {v0}, respectively. Half belongs only in the first solution lead.
        A simple fact may stay unchanged while another is rephrased. Add no negation, hypothetical condition, event, calculation, answer or technical note. Return only JSON in the example schema.
        """;
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d=RectangleSidePilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed));
        return d.Context.Relation+"; f0=actual entire rectangle perimeter, never half; f1=known "+d.Context.KnownRole
            +"; v0=object; v1=rectangle shape; v2=common length unit; target="+d.Context.TargetRole
            +"; step_0=half perimeter; step_1="+d.Context.TargetRole+"; no extra conditions; C# computes perimeter divided by two, then subtracts the known side.";
    }
}
