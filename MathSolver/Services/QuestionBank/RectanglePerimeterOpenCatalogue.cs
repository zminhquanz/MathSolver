using MathSolver.Models;
using MathSolver.Services.Core;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class RectanglePerimeterOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Geometry
        &&c.Story?.NarrativeId.StartsWith(RectanglePerimeterPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=RectanglePerimeterPilot.Variant)throw new ArgumentException("InvalidRectanglePerimeterVariant");
        var d=RectanglePerimeterPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new GeometryQuizGenerator(new GeometryCalculationEngine(),new(c.Story.Seed))
            .GenerateOpenRectanglePerimeter(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        NarrativeQuantity[] quantities=[new("f0","rectangle-length",d.Values["f0"]),new("f1","rectangle-width",d.Values["f1"]),
            new("v0","rectangle-object",d.Context.Object),new("v1","rectangle-shape",d.Context.Shape),new("v2","common-length-unit",d.Unit)];
        NarrativeClause[] facts=[new("fact_0",d.Context.ShapeFact),new("fact_1",d.Context.LengthFact),new("fact_2",d.Context.WidthFact)];
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",RectanglePerimeterPilot.Relation,
            d.Profile,c.Language,c.Tier,d.Context,string.Join("/",quantities.Select(x=>x.Role))))));
        return new(q,d.Profile,d.Render(d.Context.Lead),d.Unit,d.Perimeter+" "+d.Unit,schema,quantities,facts,d.Context.Question,
            [new("step_0",d.Context.Lead,q.GeometryProblem!.EquationText+" "+d.Unit)],
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string WritingInstructions(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string instruction=vi?"""
        Viết một đề mới, tự nhiên cho học sinh tiểu học. Ví dụ là tài liệu tham khảo, không giới hạn câu được phép viết.
        Được đổi cách mở đầu, cấu trúc và từ nối. Có thể giữ nguyên một câu dữ kiện đơn giản rồi đổi cấu trúc ở câu khác; không cần thay mọi từ. Giữ mỗi biến đúng trường, không điền số hay tên.
        Dùng {v0} như một cụm danh từ hoàn chỉnh gọi đồ vật; không thêm chữ Hình hoặc Vật thể trước biến đó. Không lặp từ hay thêm lời nhắn cho người đọc.
        Câu hình nêu {v0} có dạng {v1}. Câu chiều dài nêu trực tiếp chiều dài {v0} bằng {f0} {v2}; câu chiều rộng nêu trực tiếp chiều rộng {v0} bằng {f1} {v2}.
        Không đổi vai trò chiều dài và chiều rộng. Hai cạnh dùng cùng đơn vị độ dài. Hỏi chu vi của chính đồ vật đó, theo {v2}.
        Mỗi trường một câu ngắn; dữ kiện kết thúc bằng dấu chấm, câu hỏi kết thúc bằng dấu hỏi. Câu dẫn lời giải chỉ gọi tên chu vi {v0}.
        Câu hỏi dùng dạng nghi vấn để hỏi giá trị chu vi, không ghép lệnh Hãy tính với dấu hỏi.
        Không thêm cổng, khoảng hở, lối đi, hàng rào, chi phí, đổi đơn vị hay điều kiện suy ra cạnh. Không đổi thành diện tích hoặc nửa chu vi.
        Không viết chỉ dẫn kỹ thuật vào đề. Chỉ trả JSON theo cấu trúc ví dụ. C# giữ số, công thức, lời giải và đáp án.
        """:"""
        Write a short natural word problem for elementary pupils. The example is a reference; other clear sentences are welcome.
        Create variety through sentence construction, using simple familiar words. A simple fact may stay unchanged while another sentence changes. Prefer is, has, measures or equals for measurements.
        Preserve every placeholder in its own field, without filling names or numbers. {v0} is a complete noun phrase with its article. In each fact, name {v0} once as the subject or owner; use its name directly instead of a pronoun.
        State the shape {v1}, the length {f0} {v2}, and the width {f1} {v2} in their separate facts. Both dimensions belong to {v0} and use the same length unit.
        The question explicitly names the perimeter of {v0} and asks for its value in {v2}. Keep the familiar terms length, width and perimeter.
        Use one grammatical sentence per field: facts end with a period and the question ends with a question mark. The solution lead only names the perimeter of {v0}.
        Give only these direct dimensions; add no conditions, events, gate, gap, path, cost or conversion. Preserve the rectangle and its full perimeter.
        Return only JSON with the reference schema. Write no technical notes, calculations or answers.
        """;
        return instruction;
    }

    internal static string WritingFocus(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        // Surface guidance varies by context, while the grammar and proof still
        // accept other clear sentences. Do not change the stored lesson schema.
        string focus=c.Story!.NarrativeId[RectanglePerimeterPilot.Prefix.Length..] switch
        {
            "garden"=>vi?"Lượt này ưu tiên mở câu dữ kiện bằng đồ vật, diễn đạt trực tiếp hai cạnh; thay cách hỏi giá trị chu vi."
                :"For this request, favor the object as the subject of the side facts, using long and wide; vary the perimeter question.",
            "playground"=>vi?"Lượt này ưu tiên đưa tên đại lượng chiều dài và chiều rộng lên đầu câu; câu hỏi có thể đặt đơn vị trước phần hỏi giá trị."
                :"For this request, favor the dimension names as sentence subjects, explicitly naming their owner; vary the perimeter question.",
            "card"=>vi?"Lượt này ưu tiên diễn đạt hai số đo bằng chiều dài và chiều rộng của đồ vật, dùng từ bằng khi phù hợp; đổi cách mở câu hỏi."
                :"For this request, favor the verb measures with in length and in width, naming the object once per fact; vary the perimeter question.",
            _=>vi?"Lượt này ưu tiên diễn đạt hình dáng rồi hai cạnh của đồ vật bằng câu ngắn tự nhiên; câu hỏi có thể dùng bằng bao nhiêu."
                :"For this request, favor possessive dimension phrases with the object's name and a possessive perimeter question."
        };
        return "\n"+focus;
    }
    internal static string PromptRelation=>RectanglePerimeterPilot.Relation+"; f0=length; f1=width; v0=object; v1=rectangle shape; v2=common length unit; target=perimeter; direct sides only";
}
