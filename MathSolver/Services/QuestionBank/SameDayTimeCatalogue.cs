using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class SameDayTimeCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Time&&c.Story?.NarrativeId.StartsWith(SameDayTimePilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var d=SameDayTimePilot.Create(c.Story!.NarrativeId,(ElementaryQuizType)c.BankVariant,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateSameDayTime(ArithmeticQuizMode.Essay,(ElementaryQuizType)c.BankVariant,c.Language,c.Tier,d.Profile);
        var p=q.ElementaryProblem!;
        string[] roles=d.Relation.Id=="elapsed"?["start-hour","start-minute","end-hour","end-minute"]
            :[d.Relation.Id=="start"?"end-hour":"start-hour",d.Relation.Id=="start"?"end-minute":"start-minute","duration-minutes"];
        var quantities=roles.Select((role,i)=>new NarrativeQuantity("f"+i,role,d.Values["f"+i])).Concat(new[]{new NarrativeQuantity("v0","time_actor",d.Context.Actor),new NarrativeQuantity("v1","time_activity",d.Context.Activity)}).ToArray();
        string calculation=QuizMathExpressionFormatter.Format(d.Expression)+" = "+d.Result+(c.Language==AppLanguage.Vietnamese?" phút":" minutes");
        var facts=d.Relation.Facts.Select((text,i)=>new NarrativeClause("fact_"+i,text)).ToArray();
        string signature=string.Join("|",d.Profile,c.Language,c.Tier,string.Join("/",quantities.Select(v=>v.Role+(v.Id.StartsWith('v')?v.Value:""))),string.Join("/",d.Relation.Facts),d.Relation.Question,d.Relation.Lead);
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        return new(q,d.Profile,d.Context.Actor,c.Language==AppLanguage.Vietnamese?"phút":"minutes",p.AnswerText,schema,quantities,facts,d.Relation.Question,
            [new("step_0",d.Relation.Lead,calculation)],facts.Select((f,i)=>(f.Role,Choices:new[]{f.Text,d.Relation.AlternativeFacts[i]})).ToDictionary(x=>x.Role,x=>x.Choices),
            [d.Relation.Question,d.Relation.AlternativeQuestion],new Dictionary<string,string[]>{{"step_0",[d.Relation.Lead]}});
    }
    internal static string PromptRelation(BasicQuestionContract c)
    {
        string relation=c.SceneId.Split('/')[1];
        return "time_relation="+relation+"; SAME DAY; one uninterrupted activity; no breaks, waiting, second sessions or midnight crossing. "
            +string.Join("; ",ReasoningStoryCatalogue.Lesson(c).Quantities.Select(q=>"{"+q.Id+"}="+q.Role))
            +". "+(relation switch{"elapsed"=>"Known START and END clocks; target DURATION in minutes, not a clock time.",
                "start"=>"Known END clock and DURATION; target START clock = end minus duration, not end or duration.",
                _=>"Known START clock and DURATION; target END clock = start plus duration, not start or duration."});
    }
}
