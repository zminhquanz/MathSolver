using MathSolver.Models;
using MathSolver.Services.Core;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class RectangleAreaOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Geometry
        &&c.Story?.NarrativeId.StartsWith(RectangleAreaPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=RectangleAreaPilot.Variant)throw new ArgumentException("InvalidRectangleAreaVariant");
        var d=RectangleAreaPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new GeometryQuizGenerator(new GeometryCalculationEngine(),new(c.Story.Seed))
            .GenerateOpenRectangleArea(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        NarrativeQuantity[] quantities=[new("f0","rectangle-length",d.Values["f0"]),new("f1","rectangle-width",d.Values["f1"]),
            new("v0","rectangle-object",d.Context.Object),new("v1","rectangle-shape",d.Context.Shape),
            new("v2",d.Context.LengthUnitRole,d.LengthUnit),new("v3",d.Context.AreaUnitRole,d.AreaUnit)];
        NarrativeClause[] facts=[new("fact_0",d.Context.ShapeFact),new("fact_1",d.Context.LengthFact),new("fact_2",d.Context.WidthFact)];
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",RectangleAreaPilot.Relation,
            d.Profile,c.Language,c.Tier,d.Context,string.Join("/",quantities.Select(x=>x.Role))))));
        return new(q,d.Profile,d.Render(d.Context.Lead),d.AreaUnit,d.Area+" "+d.AreaUnit,schema,quantities,facts,d.Context.Question,
            [new("step_0",d.Context.Lead,q.GeometryProblem!.EquationText+" "+d.AreaUnit)],
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Bạn viết đề ngắn, tự nhiên cho học sinh tiểu học. Ví dụ chỉ để tham khảo; được thay cấu trúc câu và cách mở đầu.
        Có thể giữ một dữ kiện đơn giản và đổi cấu trúc ở câu khác; không cần thay mọi từ. Dùng {v0} như cụm danh từ hoàn chỉnh, không thêm Hình hoặc Vật thể trước nó.
        {v1} đã chứa đầy đủ tên hình, chẳng hạn hình chữ nhật. Đặt biến đó sau có dạng hoặc là một, không thêm chữ hình ngay trước biến.
        Giữ mỗi biến trong đúng trường và chỉ xuất hiện một lần. Không điền tên, số, đơn vị thật hoặc kết quả vào biến.
        Câu hình nêu {v0} có dạng {v1}. Câu chiều dài cho trực tiếp {f0} {v2}; câu chiều rộng cho trực tiếp {f1} {v2}, đều thuộc {v0}.
        Hai cạnh dùng đơn vị độ dài {v2}. Câu hỏi gọi rõ diện tích của {v0}, theo đơn vị diện tích {v3}. Hai biến đơn vị có vai trò khác nhau; không tự thêm ký hiệu bình phương sau biến.
        Mỗi trường một câu ngắn: dữ kiện kết thúc bằng dấu chấm, câu hỏi là câu nghi vấn kết thúc bằng dấu hỏi. Câu dẫn lời giải chỉ gọi tên diện tích {v0}.
        Câu hỏi dùng là bao nhiêu hoặc bằng bao nhiêu; không ghép lệnh Hãy tính với dấu hỏi.
        Chỉ biết hai cạnh trực tiếp, không thêm cắt bỏ, ghép hình, lối đi, lát gạch, cổng hoặc chi phí. Không suy ra cạnh, không đổi thành chu vi hoặc thể tích.
        Chỉ trả JSON theo cấu trúc tham khảo. Không viết chỉ dẫn kỹ thuật, phép tính hoặc đáp án vào câu văn.
        """:"""
        Write a short natural elementary word problem. The example is a reference; other clear sentence constructions are welcome.
        Use familiar measurement words. A simple fact may stay unchanged while another sentence changes. {v0} is a complete noun phrase with its article.
        {v1} already contains the complete shape name, such as rectangle. Use it after is a or is shaped like a; add no extra shape noun.
        Preserve each placeholder once in its own field; fill no names, numbers, units or answers. Name {v0} directly as subject or owner in each fact.
        State the shape {v1}, length {f0} {v2}, and width {f1} {v2} in separate facts about {v0}. Use ordinary school wording: is, has, measures, long or wide. Avoid technical modifiers and amounts to for side measurements.
        When the dimension is the subject, use is or measures for its value. Use has with the object as subject and a length of or a width of, rather than making a dimension own a measurement.
        The side unit {v2} measures length. The question explicitly asks for the area of {v0} in the area unit {v3}. Preserve these distinct unit roles; add no square symbol after a placeholder.
        Use one grammatical sentence per field. Facts end with a period, and the question is an interrogative ending with a question mark. The solution lead only names the area of {v0}.
        Add no cutting, joined shapes, paths, tiling, gates, costs, inferred dimensions or conversions. Preserve the rectangle and its entire area.
        Return only JSON with the reference schema. Write no technical notes, calculations or answers.
        """;

    internal static string WritingFocus(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        // Encourage variety without constraining whole sentences in the grammar
        // or changing the schema of previously stored context records.
        string focus=c.Story!.NarrativeId[RectangleAreaPilot.Prefix.Length..] switch
        {
            "garden"=>vi?"Lượt này ưu tiên dùng đồ vật làm chủ ngữ, diễn đạt hai kích thước bằng dài và rộng; thay cách hỏi diện tích."
                :"For this request, favor the object as the subject of the side facts, using long and wide; vary the area question.",
            "playground"=>vi?"Lượt này ưu tiên mở hai câu số đo bằng chiều dài và chiều rộng của đồ vật; có thể đưa đơn vị diện tích lên trước phần hỏi giá trị."
                :"For this request, favor the dimension names as sentence subjects, explicitly naming their owner; vary the area question.",
            "card"=>vi?"Lượt này ưu tiên dùng đồ vật làm chủ ngữ với có chiều dài và có chiều rộng; thay cách mở câu hỏi diện tích."
                :"For this request, favor measures with in length and in width, naming the object once per fact; vary the area question.",
            _=>vi?"Lượt này ưu tiên gọi cạnh dài và cạnh rộng của đồ vật bằng hai câu ngắn; câu hỏi có thể dùng bằng bao nhiêu."
                :"For this request, favor possessive dimension phrases with the object's name and a possessive area question."
        };
        return "\n"+focus;
    }
    internal static string PromptRelation=>RectangleAreaPilot.Relation+"; f0=length; f1=width; v0=object; v1=rectangle shape; v2=side length unit; v3=area answer unit; target=area; direct sides only";
}
