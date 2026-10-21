using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class AverageMissingOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Average
        &&c.Story?.NarrativeId.StartsWith(AverageMissingPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)AverageQuizType.MissingValue)throw new ArgumentException("InvalidAverageMissingRelation");
        var d=AverageMissingPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new AverageQuizGenerator(new(c.Story.Seed)).GenerateOpenMissing(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["known-group-observation-0","known-group-observation-1","mean-of-all-three-groups","independent-group-count",
            "known-owner-0","known-owner-1","unknown-owner","observation-unit","singular-group-unit","completed-action","base-action","plural-group-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Fact(0)),new("fact_1",d.Fact(1)),new("fact_2",d.Context.Mean),new("fact_3",d.Context.Scope)];
        NarrativeStep[] steps=[new("step_0",d.Context.Lead,q.AverageProblem!.EquationText+" "+d.Context.Unit)];
        string signature=string.Join("\n",d.Profile,c.Language,c.Tier,string.Join("/",roles),string.Join("/",d.Context.Groups),
            d.Context.Unit,d.Context.GroupUnit,d.Context.GroupPlural,d.Context.Verb,d.Context.BaseVerb,d.Context.Capacity,
            d.Context.Fact,d.Context.Mean,d.Context.Scope,d.Context.Question,d.Context.Lead);
        return new(q,d.Profile,d.Render(d.Context.Lead),d.Context.Unit,AverageOpenPilot.N(d.Missing)+" "+d.Context.Unit,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))),quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal const string Relation="relation=three-independent-groups-find-missing-observation; {f0} is the actual amount for {v0}; {f1} is the actual amount for {v1}; {f2} is the arithmetic mean of ALL {f3} groups, including {v2}; {f3}=three groups exactly. {v2}'s amount is UNKNOWN. {v3}=common observation unit, {v4}=singular group noun, {v7}=plural group noun. {v5}=completed action, {v6}=base form of that action. Ask ONLY the actual amount for {v2}, not the mean or total. Independent observations, no transfer, weighting, extra groups or conditions. C# computes mean times three minus both known amounts. Never reveal the missing amount or supply calculations.";
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Bạn là giáo viên viết bài toán tìm số thiếu khi biết trung bình cộng của ba nhóm độc lập.
        Chỉ có số liệu của hai nhóm và trung bình của cả ba nhóm. Hỏi số lượng thực tế của nhóm còn lại.
        Ví dụ là tài liệu tham khảo; được đổi cách mở đầu, cấu trúc câu, chủ động/bị động và từ nối.
        Dữ kiện hai nhóm: có thể viết 'Số {v3} do {v0} {v5} là {f0}.'; nhóm thứ hai dùng đúng biến tương ứng.
        Dữ kiện trung bình phải nói rõ trung bình mỗi {v4}, không phải tổng số hoặc số lượng của riêng một nhóm.
        Dữ kiện trung bình có thể viết 'Trung bình mỗi {v4} {v5} {f2} {v3}.' hoặc 'Mỗi {v4} {v5} trung bình {f2} {v3}.'. Đây là ví dụ ngữ pháp; được viết cách khác nếu giữ rõ vai trò.
        Không ghép 'Mức trung bình của mỗi' với động từ hành động. Không thêm 'đạt' sau biến động từ đã đầy đủ.
        Câu hỏi phải gọi đúng {v2}, chẳng hạn 'Hỏi {v2} {v6} bao nhiêu {v3}?'. Không hỏi lại trung bình hay tổng số.
        Các biến hành động đã đầy đủ; không lặp hành động, không thêm chữ 'được' nếu biến đã chứa chữ đó.
        Giữ từng biến đúng trường, đúng một lần và nguyên role/step. Không điền tên hoặc số vào biến.
        Viết mới ít nhất một câu số liệu, không chỉ thay từ trong câu hỏi hoặc câu số nhóm. Mỗi trường là một câu tự nhiên, tối đa 24 từ; không định nghĩa biến, không thêm lời hướng dẫn kỹ thuật.
        Câu dẫn lời giải chỉ gọi số lượng của nhóm cần tìm. Không tính toán hay tiết lộ đáp án. Chỉ trả JSON.
        """:"""
        You are a teacher writing a missing-observation arithmetic-mean problem for primary pupils.
        There are exactly three independent groups. Two observations and the mean of all three are known; ask for the third observation.
        Examples are references. Write fresh openings and sentence structures, including active or passive observations, without adding conditions.
        For example, '{f0} {v3} were {v5} by {v0}.' uses the known amount and completed action. Use corresponding variables for the second group.
        State the average per {v4}; do not turn it into a total or an observation for one group.
        For the mean fact, a complete grammatical sentence is 'The average number of {v3} {v5} by each {v4} was {f2}.' or 'On average, each {v4} {v5} {f2} {v3}.'. These illustrate noun and verb roles, not an exhaustive list.
        Do not write 'The average per {v4} {v5} ...': the average is not an actor performing the action. Either make each group the subject, or use 'the average number ... was'.
        The question must name {v2}, for example 'How many {v3} did {v2} {v6}?'. Ask for that actual amount, not another mean or total.
        {v5} is a past-tense verb; {v6} is the base verb after 'did'. Never repeat the action or use it as a unit.
        Keep every placeholder in its original field exactly once and preserve role/step. Do not insert actual names or numbers.
        Write at least one fresh observation sentence, not just a changed question or group-count sentence. Use one natural sentence per field, at most 24 words; do not define placeholders or include technical instructions.
        The solution heading names only the unknown group's amount. Never calculate or reveal answers. Return only JSON.
        """;
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d=AverageMissingPilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed));
        return Relation+"\nContext bindings for understanding only: {v3}="+d.Context.Unit+"; {v4}="+d.Context.GroupUnit
            +"; {v7}="+d.Context.GroupPlural+"; {v5}="+d.Context.Verb+"; {v6}="+d.Context.BaseVerb
            +". Keep placeholders in the output. The third group is included in the mean, but its observation is not provided.";
    }
}
