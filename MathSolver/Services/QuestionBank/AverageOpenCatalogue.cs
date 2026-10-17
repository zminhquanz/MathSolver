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
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var context=AverageOpenPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed)).Context;
        return Relation+"\nActivity bindings for understanding ONLY: {v3}="+context.Unit+"; {v4}="+context.GroupUnit
            +"; {v7}="+context.GroupPlural+"; {v5}="+context.Verb+"; {v6}="+context.BaseVerb+". "
            +(c.Language==AppLanguage.Vietnamese
                ?"Trong đầu ra vẫn dùng biến. Đặt {v5} làm động từ ngay sau chủ thể; {v6} là hành động trong câu hỏi. Không giải thích biến, không thêm câu định nghĩa hay hành động khác. Các câu nói về số lượng đã thực hiện của từng nhóm, không phải số đã ghi chép."
                :"Keep placeholders in output. Place {v5} as the verb immediately after its group owner; {v6} is the action in the question. Never define or explain placeholders or add a different action. State actual amounts produced/collected by each group, not amounts written in a record.");
    }
}
