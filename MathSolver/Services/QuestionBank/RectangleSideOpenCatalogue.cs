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
    internal static string WritingInstructions(BasicQuestionContract c)
    {
        var context=RectangleSidePilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed)).Context;
        return WritingInstructions(c.Language,context.KnownRole,context.TargetRole);
    }
    internal static string WritingInstructions(AppLanguage language)=>WritingInstructions(language,"length or width","specified unknown dimension");
    private static string WritingInstructions(AppLanguage language,string known,string target)
    {
        bool vi=language==AppLanguage.Vietnamese;
        if(vi)
        {
            known=known=="length"?"chiều dài":known=="width"?"chiều rộng":"chiều đã biết";
            target=target=="length"?"chiều dài":target=="width"?"chiều rộng":"chiều cần tìm";
        }
        return vi?$$"""
            Viết ngắn, tự nhiên; được đổi câu chữ. Giữ mỗi biến đúng trường, đúng một lần; chỉ trả JSON.
            {v0} là đồ vật đầy đủ; {v1} đã là hình chữ nhật: không viết hình {v1}. Không thêm danh từ trước {v0}; không đặt {v0} sau đơn vị; không thêm người, địa điểm.
            fact_0: hình dạng; fact_1: toàn bộ chu vi {f0} {v2}; fact_2: đã biết {{known}} {f1} {v2}; question và step_1: tìm {{target}} của {v0}, không gọi chung cạnh còn lại.
            Dữ kiện kết thúc dấu chấm; câu hỏi theo {v2}, kết thúc dấu hỏi. Hai lời dẫn là cụm danh từ: step_0 nửa chu vi của {v0}; step_1 gọi đúng {{target}}.
            Không thêm phủ định, giả thiết, điều kiện, sự kiện, phép tính, đáp án, diện tích hoặc đổi đơn vị.
            """:$$"""
            Write concise natural prose; vary wording. Keep each slot once in its own field; return only JSON.
            {v0} includes its article: do not add an article before {v0}; do not append {v0} after the unit; do not add actors, locations. {v1} is the full shape name: do not write rectangle {v1}.
            fact_0: shape; fact_1: entire perimeter {f0} {v2}; fact_2: known {{known}} {f1} {v2}; question and step_1: find {{target}} of {v0}. Name the dimension explicitly, never just the other side.
            Facts end with periods; ask an interrogative in {v2} ending with a question mark. Leads are noun phrases: step_0 half the perimeter of {v0}; step_1 the {{target}} of {v0}.
            Add no negation, conditions, events, calculations, answers, area or unit conversions.
            """;
    }
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d=RectangleSidePilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed));
        return d.Context.Relation+"; f0=entire perimeter, never half; f1=known "+d.Context.KnownRole
            +"; target="+d.Context.TargetRole+"; v2=linear unit; step_0=half perimeter; step_1="+d.Context.TargetRole+"; no extra conditions.";
    }
}
