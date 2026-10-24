using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class UnitaryTotalOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Proportion
        &&c.Story?.NarrativeId.StartsWith(UnitaryTotalPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)ProportionQuizType.Direct)throw new ArgumentException("InvalidUnitaryTotalRelation");
        var d=UnitaryTotalPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ProportionQuizGenerator(new(c.Story.Seed)).GenerateOpenUnitaryTotal(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["source-group-count","source-combined-total","target-group-count","plural-group-unit","counted-item-unit","singular-group-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.Given),new("fact_1",d.Context.Condition)];
        NarrativeStep[] steps=[new("step_0",d.Context.UnitLead,$"{d.SourceTotal} ÷ {d.SourceGroups} = {d.PerGroup} {d.Context.Unit}"),
            new("step_1",d.Context.TotalLead,$"{d.PerGroup} × {d.TargetGroups} = {d.Answer} {d.Context.Unit}")];
        string signature=string.Join("\n",UnitaryTotalPilot.Relation,d.Profile,c.Language,c.Tier,string.Join("/",roles),d.Context);
        return new(q,d.Profile,d.Context.Unit,d.Context.Unit,UnitaryTotalPilot.N(d.Answer)+" "+d.Context.Unit,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))),quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],steps.ToDictionary(s=>s.Id,s=>new[]{s.Lead}));
    }
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết bài rút về đơn vị ngắn, tự nhiên. Ví dụ chỉ tham khảo; được đổi cách mở đầu và cấu trúc câu.
        {f0} {v0} chứa tổng cộng {f1} {v1}. Đây là tổng chung của tất cả các nhóm ban đầu.
        Điều kiện có hai ý độc lập: các nhóm có số đồ vật bằng nhau, các nhóm mới giữ đúng số đồ vật trong mỗi nhóm ban đầu.
        Trong fact_1, dùng {v1} và {v2} để nói rõ cả hai ý. Chỉ nói không đổi thì chưa khẳng định các nhóm bằng nhau.
        Hỏi tổng số {v1} trong {f2} {v0} như vậy. Giữ rõ số nhóm ban đầu và số nhóm mới.
        Dùng biến cho mọi số, đơn vị nhóm và đồ vật; giữ số lần xuất hiện của mỗi biến trong đúng trường. Không điền giá trị vào biến.
        Dùng từ số cho đồ vật đếm được. Câu dẫn là cụm danh từ hoàn chỉnh, không phải lời yêu cầu tính.
        step_0 gọi số {v1} trong mỗi {v2}. step_1 gọi tổng số {v1} trong các {v0} mới, bao gồm tất cả các nhóm mới.
        {v0} đã là đơn vị nhóm số nhiều. Không thêm từ nhóm sau biến này. Giữ từ các ở câu dẫn tổng tiếng Việt.
        Trước khi trả JSON, đọc lại từng câu dẫn: phải trọn ý, đúng đơn vị và không lẫn tên vai trò hay chỉ dẫn kỹ thuật.
        Không viết phép tính hoặc đáp án.
        Mỗi trường một câu ngắn. Không thêm tặng, lấy bớt, nhóm không đầy hoặc lượng khác nhau. Chỉ trả JSON.
        """:"""
        Write a short, natural unitary-method problem. Examples are references; vary the opening and sentence structure.
        {f0} {v0} contain {f1} {v1} altogether. This is the combined total of all the original groups.
        State two independent ideas: the item counts are equal across groups, and the new groups keep the original count per group.
        In fact_1, use {v1} and {v2} to express both ideas. Constancy alone does not establish equality between groups.
        Ask for the total number of {v1} in {f2} {v0} like these. Keep the original and target group counts distinct.
        Use placeholders for all numbers, group units and items. Preserve their multiplicities in each field; the app fills values later.
        Use number or count for counted items. Headings must be complete noun phrases, not commands to calculate.
        step_0 names the number of {v1} in each {v2}. step_1 names the total number of {v1} in all the new {v0}.
        {v0} is already a plural group noun. Do not append another group noun after it. Keep every heading complete.
        Pair the group counts directly with their noun: {f0} {v0} and {f2} {v0} are complete count-and-container phrases.
        Before returning JSON, read each heading: it must name the right quantity in pupil-facing language without technical role names.
        No calculations or answers. One short sentence per field. No giving away items, losses, partially filled groups or unequal group sizes. Return only JSON.
        """;
    internal const string Relation="relation=source-groups-and-total-find-new-total; f0=source group count; f1=combined total in those source groups, NOT the per-group count; f2=target group count; v0=plural group noun; v1=item unit; v2=singular group noun. Every group has the same unchanged item count. Target is the TOTAL in the target groups. C# computes f1/f0*f2 and both solution steps.";
    internal static string ContextHint(AppLanguage language)=>language==AppLanguage.Vietnamese
        ? "Các nhóm chứa đồ vật đếm được. {v0} là đơn vị nhóm số nhiều, {v2} là đơn vị một nhóm, {v1} là đơn vị đồ vật. Ứng dụng sẽ điền các giá trị."
        : "Groups contain counted items. {v0} is the plural group noun, {v2} is the singular group noun, and {v1} is the item unit. The app fills their values.";

    internal static BasicQuestionDraft PromptExample(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        // Editorial guidance must not change the List<> lesson/schema used by
        // saved and pending questions. Clarify only the model's reference.
        string condition=(c.Language,c.SceneId.Split('/')[^1]) switch
        {
            (AppLanguage.Vietnamese,"pens")=>"Số {v1} trong mỗi {v2} bằng nhau và giữ nguyên khi xét các nhóm mới.",
            (AppLanguage.Vietnamese,"gifts")=>"Mỗi {v2} có cùng số {v1}, các nhóm mới cũng giữ nguyên số đó.",
            (AppLanguage.Vietnamese,_)=>"Trong mỗi {v2}, số {v1} đều bằng nhau và không thay đổi ở các nhóm mới.",
            (_,"pens")=>"The number of {v1} in each {v2} is the same, and the new groups keep that count.",
            (_,"gifts")=>"Each {v2} contains the same number of {v1}, and the new groups have that same count.",
            _=>"Every {v2} holds an equal number of {v1}, and this count remains unchanged for the new groups."
        };
        return draft with {Facts=[draft.Facts![0],draft.Facts[1] with {Text=condition}]};
    }
}
