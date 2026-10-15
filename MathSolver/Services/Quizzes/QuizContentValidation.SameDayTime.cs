using MathSolver.Models;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckSameDayTimeLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(SameDayTimePilot.ContextList,out var contexts))return;
        if(!pack.Lists.TryGetValue(SameDayTimePilot.RelationList,out var relations)||contexts.GetArrayLength()==0||relations.GetArrayLength()!=3)Fail("Missing same-day time relations.");
        var ids=new HashSet<string>();
        foreach(var row in contexts.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate same-day context.");
            _=RequiredString(row,"Actor");_=RequiredString(row,"Activity");
            int early=row.GetProperty("EarliestHour").GetInt32(),late=row.GetProperty("LatestHour").GetInt32(),max=row.GetProperty("MaximumMinutes").GetInt32();
            if(early<0||late>23||early>=late||max<15||max>(late-early)*60)Fail("Invalid same-day context bounds.");
        }
        ids.Clear();
        foreach(var row in relations.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            string expected=id switch{"elapsed"=>nameof(ElementaryQuizType.SameDayElapsed),"start"=>nameof(ElementaryQuizType.SameDayStart),"end"=>nameof(ElementaryQuizType.SameDayEnd),_=>""};
            if(!ids.Add(id)||expected.Length==0||RequiredString(row,"Type")!=expected)Fail("Invalid same-day role binding.");
            var facts=row.GetProperty("Facts");var alternatives=row.GetProperty("AlternativeFacts");
            if(facts.GetArrayLength()!=3||alternatives.GetArrayLength()!=3)Fail("Same-day requires two facts and explicit conditions.");
            IEnumerable<string> Tokens(string s)=>System.Text.RegularExpressions.Regex.Matches(s,@"\{[^{}]+\}").Select(m=>m.Value).Order();
            string[][] required=[["{f0}","{f1}","{v0}","{v1}"],id=="elapsed"?["{f2}","{f3}","{v0}","{v1}"]:["{f2}"],[]];
            for(int i=0;i<3;i++)
                if(!Tokens(facts[i].GetString()!).SequenceEqual(required[i].Order())||!Tokens(alternatives[i].GetString()!).SequenceEqual(required[i].Order()))Fail("Changed same-day fact roles.");
            foreach(string field in new[]{"Question","AlternativeQuestion"})if(!Tokens(RequiredString(row,field)).SequenceEqual(new[]{"{v0}","{v1}"}))Fail("Changed same-day question roles.");
            if(Tokens(RequiredString(row,"Lead")).Any())Fail("Unexpected same-day lead quantities.");
        }
    }
}
