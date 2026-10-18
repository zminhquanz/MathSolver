using MathSolver.Models;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckSameDayBreakLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(SameDayBreakPilot.ListName,out var rows))return;
        if(rows.ValueKind!=System.Text.Json.JsonValueKind.Array||rows.GetArrayLength()==0)Fail("Missing one-break contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate one-break context.");
            _=RequiredString(row,"Actor");_=RequiredString(row,"Activity");
            if(RequiredString(row,"RelationId")!=SameDayBreakPilot.Relation||RequiredString(row,"TargetRole")!="active-minutes-excluding-break")Fail("Changed one-break relation.");
            if(!row.GetProperty("KnownRoles").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"start-hour","start-minute","end-hour","end-minute","included-break-minutes"}))Fail("Changed one-break quantity roles.");
            int early=row.GetProperty("EarliestHour").GetInt32(),late=row.GetProperty("LatestHour").GetInt32(),max=row.GetProperty("MaximumMinutes").GetInt32();
            if(early<0||late>23||early>=late||max<30||max>(late-early)*60)Fail("Invalid one-break bounds.");
            string[][] expected=[["{f0}","{f1}","{v0}","{v1}"],["{f2}","{f3}","{v0}","{v1}"],["{f4}","{v0}","{v1}"],[]];
            IEnumerable<string> Tokens(string s)=>System.Text.RegularExpressions.Regex.Matches(s,@"\{[^{}]+\}").Select(m=>m.Value).Order();
            foreach(string field in new[]{"Facts","AlternativeFacts"})
            {
                var facts=row.GetProperty(field);if(facts.GetArrayLength()!=4)Fail("One-break requires four facts.");
                for(int i=0;i<4;i++)if(!Tokens(facts[i].GetString()!).SequenceEqual(expected[i].Order()))Fail("Changed one-break fact roles.");
            }
            foreach(string field in new[]{"Question","AlternativeQuestion"})if(!Tokens(RequiredString(row,field)).SequenceEqual(new[]{"{v0}","{v1}"}))Fail("Changed active-time target slots.");
            if(Tokens(RequiredString(row,"Lead")).Any())Fail("Unexpected one-break lead slots.");
        }
    }
}
