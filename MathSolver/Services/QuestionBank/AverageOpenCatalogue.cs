using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class AverageOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Average
        && c.Story?.NarrativeId.StartsWith(AverageOpenPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)AverageQuizType.Direct)throw new ArgumentException("InvalidAverageOpenRelation");
        var d=AverageOpenPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new AverageQuizGenerator(new(c.Story.Seed)).GenerateOpenDirect(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["group-observation-0","group-observation-1","group-observation-2","independent-group-count",
            "group-owner-0","group-owner-1","group-owner-2","observation-unit","group-unit","completed-action","base-action","plural-group-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        var facts=Enumerable.Range(0,3).Select(i=>new NarrativeClause("fact_"+i,d.Fact(i))).Append(new("fact_3",d.Context.Scope)).ToArray();
        NarrativeStep[] steps=[new("step_0",d.Context.Lead,q.AverageProblem!.EquationText+" "+d.Context.Unit)];
        string signature=string.Join("\n",d.Profile,c.Language,c.Tier,string.Join("/",roles),
            string.Join("/",d.Context.Groups),d.Context.Unit,d.Context.GroupUnit,d.Context.GroupPlural,d.Context.Verb,d.Context.BaseVerb,
            d.Context.Fact,d.Context.Scope,d.Context.Question,d.Context.Lead);
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        return new(q,d.Profile,d.Render(d.Context.Lead),d.Context.Unit,AverageOpenPilot.N(d.Average)+" "+d.Context.Unit,schema,quantities,
            facts,d.Context.Question,steps,facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],
            new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal const string Relation="relation=arithmetic-mean-of-complete-independent-group-observations; {f0}/{f1}/{f2} are the observed amounts for {v0}/{v1}/{v2} respectively, NOT group averages or member counts. {f3}=number of independent groups, {v3}=common observation unit, {v4}=singular group noun, {v7}=plural group noun, {v5}=completed action verb, {v6}=base form of the same action. Ask the arithmetic mean amount PER GROUP, not total, individual-person mean, missing amount or weighted mean. All groups and observations are provided, no overlap or transfer. Keep action placeholders as verbs, not units. C# sums all observations and divides by the group count; never supply the answer.";
    internal static string WritingInstructions(AppLanguage language) => language == AppLanguage.Vietnamese ? """
        Bạn là giáo viên viết một bài toán trung bình cộng ngắn, tự nhiên cho học sinh tiểu học.
        Các nhóm có số liệu riêng; hỏi trung bình mỗi nhóm, không hỏi số của từng nhóm hay tổng số.
        Ví dụ chỉ để tham khảo. Được thay cách mở đầu, cấu trúc câu, chủ động hoặc bị động; không thêm sự việc hay điều kiện.
        Hãy viết mới cả câu dữ kiện và câu hỏi, không chép lại nguyên văn ví dụ.
        Ví dụ về câu dữ kiện đúng vai trò: 'Số {v3} do {v0} {v5} là {f0}.' hoặc 'Có {f0} {v3} do {v0} {v5}.'. Với các nhóm khác, dùng đúng biến chủ thể và số lượng tương ứng.
        Trong cấu trúc 'Số ...', sau 'Số' là đơn vị {v3}, không phải tên nhóm. Đây chỉ là ví dụ ngữ pháp; được viết cách khác nếu giữ đúng quan hệ.
        Giữ từng biến trong đúng trường, đúng một lần; giữ nguyên role và step. Không thay biến bằng số, tên hay từ ngữ cụ thể.
        {v5} và {v6} đã chứa đầy đủ hành động, kể cả chữ 'được' nếu có. Không viết lại hành động hay thêm chữ 'được' lần nữa.
        Câu hỏi phải có 'trung bình', 'mỗi' và cụm 'bao nhiêu {v3}'. Đặt đơn vị sau 'bao nhiêu', không đặt trước từ hỏi.
        Chẳng hạn: 'Hỏi trung bình mỗi {v4} {v6} bao nhiêu {v3}?' hoặc 'Mỗi {v4} {v6} trung bình bao nhiêu {v3}?'. Đây là ví dụ về ngữ pháp, không phải danh sách câu bắt buộc.
        Mỗi trường là một câu ngắn. Đọc lại sau khi thay biến: không lặp từ, không đảo trật tự từ, không giải thích biến hay nhắc chỉ dẫn kỹ thuật.
        Câu dẫn lời giải chỉ gọi lượng trung bình cần tìm. Không tính toán, không nêu đáp án. Chỉ trả JSON theo cấu trúc ví dụ.
        """ : """
        You are a teacher writing a short, natural arithmetic-mean problem for primary pupils.
        The groups have separate observations. Ask for the average per group, not each individual observation or the total.
        Examples are references. Vary openings, sentence structure and active/passive voice without adding events or conditions.
        Write fresh observation sentences AND a fresh question, rather than copying the reference verbatim.
        Grammatical observation examples: '{f0} {v3} were {v5} by {v0}.' or 'The number of {v3} {v5} by {v0} was {f0}.'. Use the corresponding owner and quantity variables for the other groups.
        After 'The number of', place the observation unit {v3}, not a group name. These are grammar illustrations; other clear structures are allowed.
        Keep every placeholder in its original field exactly once, and keep role and step values. Never replace placeholders with names, numbers or literal words.
        {v5} is the past-tense action for observations. {v6} is the base verb: use it after 'did each {v4}', never as a past-tense relative-clause verb.
        The QUESTION itself must explicitly say 'on average', 'average' or 'mean'. A solution heading cannot supply this meaning.
        For example: 'On average, how many {v3} did each {v4} {v6}?' or 'How many {v3} did each {v4} {v6}, on average?'. These illustrate grammar, not an exhaustive list of allowed questions.
        Write one short sentence per field. Read the expanded sentences for correct verb tense and word order; do not repeat the action, define placeholders or mention technical instructions.
        The solution lead names only the per-group average. Do not calculate or reveal the answer. Return only JSON with the reference structure.
        """;
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var context=AverageOpenPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed)).Context;
        return Relation+"\nActivity bindings for understanding ONLY: {v3}="+context.Unit+"; {v4}="+context.GroupUnit
            +"; {v7}="+context.GroupPlural+"; {v5}="+context.Verb+"; {v6}="+context.BaseVerb+". "
            +(c.Language==AppLanguage.Vietnamese
                ?"Trong đầu ra vẫn dùng biến. {v5} là hành động đã thực hiện của nhóm; {v6} là hành động trong câu hỏi. Có thể đổi vị trí chủ thể trong câu chủ động hoặc bị động. Không giải thích biến, thêm hành động hay đổi số đã thực hiện thành số đã ghi chép."
                :"Keep placeholders in output. {v5} is the group's completed action; {v6} is the base verb in the question. Active or passive observations may put the owner first or last. Do not define placeholders, add actions or change actual observations into amounts written in a record.");
    }
}
