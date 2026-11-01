using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class AverageRelationsOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Average&&c.Story?.NarrativeId.StartsWith(AverageRelationsPilot.Prefix,StringComparison.Ordinal)==true;
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode)
        =>new AverageQuizGenerator(new(c.Story!.Seed)).GenerateOpenRelations(mode,c.Language,c.Tier,c.Story.NarrativeId,c.BankVariant);
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var d=AverageRelationsPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,c.BankVariant,new(c.Story.Seed));
        var q=Generate(c,ArithmeticQuizMode.Essay);var text=d.Text;
        string[] roles=d.Pooled?
            ["member-count-group-a","mean-per-member-group-a","member-count-group-b","mean-per-member-group-b","group-a","group-b","singular-member","observation-unit","completed-action","base-action","plural-members"]:
            [d.Type==AverageQuizType.AverageToTotal?"known-mean-per-member":"known-combined-total","member-count","collection","singular-member","plural-members","observation-unit","completed-action","base-action"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        var facts=text.Facts.Select((f,i)=>new NarrativeClause("fact_"+i,f)).ToArray();
        NarrativeStep[] steps=[new("step_0",text.Lead,q.AverageProblem!.EquationText+" "+d.Context.Unit)];
        string signature=string.Join("\n",d.Profile,c.Language,c.Tier,string.Join("/",roles),d.Context.ToString(),string.Join("/",text.Facts),text.Question,text.Lead);
        return new(q,d.Profile,d.Render(text.Lead),d.Context.Unit,AverageRelationsPilot.N(d.Answer)+" "+d.Context.Unit,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))),quantities,facts,text.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[text.Question],new Dictionary<string,string[]>{{"step_0",[text.Lead]}});
    }
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết đề toán ngắn bằng tiếng Việt tự nhiên cho học sinh tiểu học.
        Giữ từng biến đúng trường và đúng số lần xuất hiện. Các nghĩa của biến chỉ giúp hiểu bối cảnh; trong JSON vẫn viết biến.
        Biến tên nhóm đã là tên đầy đủ, biến đồ vật đã có đơn vị. Biến hành động đã là động từ đầy đủ: không viết thêm hành động bên cạnh biến.
        Được đổi mở đầu, trật tự câu hoặc chủ động/bị động; viết mới ít nhất một câu dữ kiện. Mỗi trường là một câu ngắn, tối đa 24 từ.
        Câu hỏi chỉ hỏi đại lượng cần tìm; câu dẫn lời giải là cụm danh từ gọi đại lượng ấy. Không chép lời nhắc vào đề.
        Không thêm điều kiện, phủ định hay dữ kiện; không tính toán hoặc tiết lộ đáp án. Chỉ trả JSON.
        """:"""
        Write a short natural English word problem for primary pupils.
        Keep each placeholder in its original field with its original multiplicity. Bindings explain the context; output the placeholders themselves.
        A group-name slot is already a complete name; an item slot already names its unit. An action slot is a complete verb: do not repeat its action beside it.
        Vary openings, sentence structure or active/passive voice; write at least one fresh fact sentence. Use one short sentence per field, at most 24 words.
        Ask only for the requested quantity. The solution heading is a noun phrase naming that quantity. Keep author instructions out of the pupil's text.
        Add no conditions, negations or facts; never calculate or reveal answers. Return only JSON.
        """;
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d=AverageRelationsPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,c.BankVariant,new(c.Story.Seed));
        bool vi=c.Language==AppLanguage.Vietnamese;
        string relation=(d.Type,vi) switch
        {
            (AverageQuizType.AverageToTotal,true)=>"fact_0: {f1} là số {v2} trong {v0}. fact_1: {f0} là số {v3} trung bình mỗi {v1} trong {v0} {v4}. Hỏi tổng số {v3} của tất cả các {v2} trong {v0}, dùng {v5} làm động từ. Trung bình không khẳng định mọi người có số lượng bằng nhau; không đưa lời giải thích này vào đề.",
            (AverageQuizType.AverageToTotal,false)=>"fact_0: {f1} counts the {v2} in {v0}. fact_1: {f0} is the average number of {v3} {v4} per {v1} in {v0}. Ask for the combined total for all the {v2} in {v0}, using {v5} after 'did'. A mean does not assert equal individual amounts; keep this author note out of the problem.",
            (AverageQuizType.TotalToAverage,true)=>"fact_0: {f1} là số {v2} trong {v0}. fact_1: {f0} là tổng số {v3} do tất cả các {v1} trong {v0} {v4}. Hỏi trung bình mỗi {v1} trong {v0} {v5} bao nhiêu {v3}, không hỏi số lượng thực tế của từng người.",
            (AverageQuizType.TotalToAverage,false)=>"fact_0: {f1} counts the {v2} in {v0}. fact_1: {f0} is the combined total number of {v3} {v4} by every {v1} in {v0}. Ask for the average per {v1} in {v0}, using {v5} after 'did'; do not ask each person's actual amount.",
            (_,true)=>"fact_0 và fact_2: {f0}, {f2} là số {v6} của {v0}, {v1}. fact_1 và fact_3: {f1}, {f3} là số {v3} trung bình mỗi {v2} của nhóm tương ứng {v4}. fact_4: thành viên hai nhóm không trùng nhau. Hỏi trung bình mỗi {v2} của cả {v0} và {v1} {v5} bao nhiêu {v3}; không hỏi trung bình theo nhóm và không mô tả cách tính.",
            _=>"fact_0 and fact_2: {f0}, {f2} count the {v6} in {v0}, {v1}. fact_1 and fact_3: {f1}, {f3} are the average number of {v3} {v4} per {v2} in the corresponding group. fact_4: the groups have distinct members. Ask for the average per {v2} across both {v0} and {v1}, using {v5} after 'did'; not an average per group. Do not describe a calculation."
        };
        string[] roles=d.Pooled?(vi?
            ["tên đầy đủ nhóm thứ nhất","tên đầy đủ nhóm thứ hai","danh từ chỉ một người","đơn vị đồ vật","động từ đã hoàn chỉnh trong dữ kiện","động từ trong câu hỏi","danh từ chỉ nhiều người"]:
            ["complete first group name","complete second group name","singular person noun","item unit","complete past-tense action in facts","base action after did in questions","plural person noun"]):(vi?
            ["tên đầy đủ nhóm","danh từ chỉ một người","danh từ chỉ nhiều người","đơn vị đồ vật","động từ đã hoàn chỉnh trong dữ kiện","động từ trong câu hỏi"]:
            ["complete group name","singular person noun","plural person noun","item unit","complete past-tense action in facts","base action after did in questions"]);
        string bindings=string.Join("; ",d.Values.Where(v=>v.Key.StartsWith('v')).Select((v,i)=>"{"+v.Key+"}="+v.Value+" ("+roles[i]+")"));
        string action=vi?"{v4} và {v5} đứng ở vị trí động từ của người, không phải tên đồ vật hoặc đơn vị; không lặp hành động bằng chữ thường bên cạnh biến.":
            "{v4} is the actor's complete past-tense verb; {v5} is the verb after 'did'. Neither is an item or a unit. Never repeat the literal action beside its slot.";
        return relation+"\n"+bindings+".\n"+action;
    }
}
