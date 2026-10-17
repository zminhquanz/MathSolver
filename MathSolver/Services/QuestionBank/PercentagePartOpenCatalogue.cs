using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class PercentagePartOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Percentage
        &&c.Story?.NarrativeId.StartsWith(PercentagePartPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)PercentageQuizType.FindPercentageValue)throw new ArgumentException("InvalidPercentagePartVariant");
        var d=PercentagePartPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new PercentageQuizGenerator(new(c.Story.Seed)).GenerateOpenPart(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["whole-count-at-100-percent","subset-percentage-of-same-whole","whole-owner","whole-counted-unit",
            "whole-object-noun","subset-object-noun","subset-counted-answer-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.Whole),new("fact_1",d.Context.Share)];
        NarrativeStep[] steps=[new("step_0",d.Context.Lead,q.PercentageProblem!.EquationText+" "+d.Context.PartUnit)];
        string signature=string.Join("\n",d.Profile,c.Language,c.Tier,PercentagePartPilot.RelationId,string.Join("/",roles),d.Context);
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        return new(q,d.Profile,d.Context.PartNoun,d.Context.PartUnit,PercentagePartPilot.N(d.Answer)+" "+d.Context.PartUnit,
            schema,quantities,facts,d.Context.Question,steps,facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],
            new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d=PercentagePartPilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed)).Context;
        return "relation="+PercentagePartPilot.RelationId+"; {f0}=original whole count, the 100-percent base; "
            +"{f1}=percentage of that SAME whole, including its percent sign; {v0}=owner; {v1}=whole counted unit; "
            +"{v2}=whole noun; {v3}=subset noun; {v4}=subset counted answer unit. This is a STATIC classification, not a change or a transfer. "
            +"Ask the COUNT of the subset owned by {v0}, never a percentage, the remaining count or the original whole. "
            +"Do not invert the base/subset, add conditions, solve or supply the answer. "
            +"Context bindings for understanding only: {v0}="+d.Owner+"; {v1}="+d.WholeUnit+"; {v2}="+d.WholeNoun
            +"; {v3}="+d.PartNoun+"; {v4}="+d.PartUnit+". Keep placeholders in output.";
    }
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Bạn là giáo viên viết một bài toán phần trăm ngắn, tự nhiên cho học sinh tiểu học.
        Biết toàn bộ và tỉ lệ của nhóm con; hỏi số lượng nhóm con. Đây là phân loại đồ vật đang có, không phải nhận thêm, sử dụng hay tăng giảm.
        Hãy viết mới câu dữ kiện và câu hỏi. Ví dụ chỉ tham khảo; được thay cấu trúc, cách mở đầu, từ nối, giữ đúng chủ thể và vai trò các lượng.
        Giữ từng biến trong đúng trường, đúng một lần, giữ role và step. {f1} đã có dấu phần trăm do C# điền; không viết thêm ký hiệu hay số.
        Chỉ dùng chủ sở hữu, đồ vật và đơn vị đã cho. Không thêm người, tổ chức hay địa điểm mới. Có thể đổi câu sở hữu thành 'Trong kho của {v0} có {f0} {v1}.'.
        {v2} là nhóm toàn bộ, {v3} là nhóm con; không đổi chỗ. Câu 'Số {v3} chiếm {f1} tổng số {v2} đó.' là ví dụ ngữ pháp, không phải câu bắt buộc.
        Câu hỏi chỉ hỏi số {v4} của {v0}, không hỏi phần còn lại hoặc tỉ lệ. Không tiết lộ đáp án hay thêm sự việc.
        Viết một câu ngắn, trọn ý trong mỗi trường. Không giải thích biến hoặc lẫn chỉ dẫn kỹ thuật.
        Câu dẫn lời giải dùng tên đồ vật qua biến, chẳng hạn 'Số {v4}' hoặc 'Số lượng {v4}'; không viết thuật ngữ 'nhóm con' vào câu dẫn.
        Chỉ trả JSON theo cấu trúc ví dụ. Không giải bài.
        """:"""
        You are a teacher writing a short, natural percentage problem for primary pupils.
        Given the whole and a subset percentage, ask for the subset COUNT. Describe an existing inventory, not a gain, loss, use or change.
        Write fresh facts and a fresh question. Examples are references, not a list of permitted sentences. Vary openings, structures and links while preserving owners and quantity roles.
        Preserve each placeholder in its original field exactly once and keep role/step values. {f1} already includes its percent sign; add no symbol or number.
        Use only the supplied owner, objects and units. Invent no people, organizations or locations. For example, ownership can be phrased '{f0} {v1} belong to {v0}.'.
        {v2} is the whole noun and {v3} is its subset. Do not swap them. 'Of those {v2}, {f1} are {v3}.' illustrates grammar, not mandatory wording.
        Ask only how many {v4} {v0} has, never how many remain, the original whole or a percentage. Add no event or condition and reveal no answer.
        Describe type membership directly: the indicated percentage of the original objects ARE the specified type, not objects used to manufacture other objects.
        Use one short grammatical sentence per field. Do not define placeholders or include technical instructions.
        The solution lead uses the object binding, for example 'Number of {v4}' or 'Count of {v4}'; do not write the technical word 'subset' in it.
        Return only JSON with the reference structure. Do not solve the problem.
        """;
}
