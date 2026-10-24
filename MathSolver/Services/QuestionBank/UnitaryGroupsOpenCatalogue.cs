using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class UnitaryGroupsOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Proportion
        &&c.Story?.NarrativeId.StartsWith(UnitaryGroupsPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)ProportionQuizType.UnitaryGroupCount)throw new ArgumentException("InvalidUnitaryGroupsRelation");
        var d=UnitaryGroupsPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ProportionQuizGenerator(new(c.Story.Seed)).GenerateNarrative(ArithmeticQuizMode.Essay,
            ProportionQuizType.UnitaryGroupCount,c.Language,new(c.Tier,false),d.Profile);
        string[] roles=["source-group-count","source-combined-item-total","target-item-total","plural-group-unit","counted-item-unit","singular-group-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.Given),new("fact_1",d.Context.Condition)];
        NarrativeStep[] steps=[new("step_0",d.Context.RateLead,$"{d.SourceTotal} ÷ {d.SourceGroups} = {d.PerGroup} {d.Units.Rate}"),
            new("step_1",d.Context.CountLead,$"{d.TargetTotal} ÷ {d.PerGroup} = {d.TargetGroups} {d.Units.Groups}")];
        string signature=string.Join("\n",UnitaryGroupsPilot.Relation,d.Profile,c.Language,c.Tier,string.Join("/",roles),d.Context);
        return new(q,d.Profile,d.Context.Groups,d.Context.Groups,$"{d.TargetGroups} {d.Units.Groups}",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))),quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>f.Role=="fact_0"?new[]{f.Text,d.Context.GivenAlternate}:new[]{f.Text}),
            [d.Context.Question],steps.ToDictionary(s=>s.Id,s=>new[]{s.Lead}));
    }
    internal static string Instructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết bài rút về đơn vị tìm số nhóm. Ví dụ chỉ tham khảo, được đổi cách mở đầu và cấu trúc câu.
        {f0} {v0} chứa tổng cộng {f1} {v1}, đây không phải số đồ vật trong một nhóm.
        Trường điều kiện cần hai ý độc lập: các nhóm ban đầu có số đồ vật bằng nhau; mỗi nhóm mới chứa đúng bằng số đồ vật trong một nhóm ban đầu.
        Nối rõ hai ý trong một câu, dùng {v1} và {v2} đúng một lần. Chỉ nói giữ nguyên chưa cho biết các nhóm ban đầu bằng nhau.
        Ví dụ tham khảo: Mỗi {v2} có cùng số {v1}, và số đó giữ nguyên khi đóng các nhóm mới. Được đổi cách diễn đạt nhưng giữ đủ cả hai ý.
        {f2} {v1} là tổng số đồ vật mới. Câu hỏi phải tìm số {v0} cần dùng để chứa hết tổng mới theo cách đó.
        Giữ nguyên vai trò số đồ vật và số nhóm. Số đồ vật mỗi nhóm và số nhóm cần tìm đều chưa được cho biết.
        Mỗi số/đơn vị dùng đúng biến, đúng số lần xuất hiện trong trường. Không điền giá trị vào biến.
        Câu dữ kiện và câu hỏi là câu hoàn chỉnh. Riêng câu dẫn lời giải là cụm danh từ: số {v1} trong mỗi {v2}, rồi số {v0} cần dùng.
        Không mở câu dẫn bằng Tìm, Tính, Xác định và không mô tả quy trình thực hiện. Không thêm nhãn tổng mới hoặc tên vai trò dữ kiện.
        Dùng từ số cho đồ vật đếm được. Câu viết dành cho học sinh, không có tên vai trò hoặc chỉ dẫn kỹ thuật.
        Không thêm hao hụt, tặng, nhóm không đầy, số dư, làm tròn hoặc điều kiện khác. Không tính toán hay tiết lộ đáp án. Chỉ trả JSON.
        """:"""
        Write a short unitary-method problem asking for the number of new groups. References do not limit sentence wording.
        {f0} {v0} hold {f1} {v1} in total, not per group.
        The condition needs two independent clauses: equal counts across ALL original groups; the SAME count in each new group as in ONE original group.
        Join both clauses into one sentence using {v1} and {v2} once each. As before or stays the same alone does not establish equality across original groups.
        Reference only: Every original {v2} has the same number of {v1}, and all new groups keep that count. Vary wording while retaining both clauses.
        {f2} {v1} is the new total number of items, never the number of groups.
        Ask how many {v0} like these are needed to hold the new item total. The per-group count and final group count are unknown.
        Pair numbers with their existing nouns: {f0} {v0} and {f2} {v1}. The app fills every placeholder later; preserve each field's multiplicities.
        Facts and the question are complete sentences. Solution headings are noun phrases: number of {v1} in each {v2}, then number of {v0} needed.
        Do not start headings with Find or Determine, or describe what is first found/then determined. No technical labels such as new item amount.
        Use number/count rather than amount for counted objects, and avoid wordy phrases such as every single or using these same arrangements.
        Keep pupil-facing language, no technical role names or instructions. No losses, giving away items, partial groups, remainders, rounding or extra conditions.
        No calculations or answers. Return only JSON; keep sentences and noun headings short and natural.
        """;
    internal const string Relation="relation=source-groups-and-total-new-item-total-find-groups; f0=known original group count; f1=combined original ITEM total; f2=new ITEM total; v0=plural group noun; v1=item unit; v2=singular group noun. Equal original groups and unchanged per-group count for new groups. TARGET=unknown group count, NOT items. C# computes rate=f1/f0, groups=f2/rate. Rate and group answers remain hidden.";
    internal static string ContextHint(AppLanguage language)=>language==AppLanguage.Vietnamese
        ? "{v0} gọi các nhóm, {v2} gọi một nhóm, {v1} gọi đồ vật trong nhóm. Giữ riêng số nhóm và số đồ vật."
        : "{v0} names plural containers, {v2} names one container, and {v1} names the counted contents. Keep container counts separate from item counts.";
}
