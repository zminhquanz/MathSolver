namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckOvernightTimeLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(OvernightTimePilot.ListName,out var rows))return;
        if(rows.ValueKind!=System.Text.Json.JsonValueKind.Array||rows.GetArrayLength()==0)Fail("Missing overnight contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate overnight context.");
            _=RequiredString(row,"Actor");_=RequiredString(row,"Activity");
            if(RequiredString(row,"RelationId")!=OvernightTimePilot.Relation||RequiredString(row,"TargetRole")!="elapsed-minutes"
                ||RequiredString(row,"Continuity")!="continuous-no-break"||row.GetProperty("EndDayOffset").GetInt32()!=1)Fail("Changed overnight relation/day.");
            if(!row.GetProperty("KnownRoles").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"start-hour","start-minute","end-hour","end-minute","end-day-offset"}))Fail("Changed overnight roles.");
            int early=row.GetProperty("EarliestHour").GetInt32(),max=row.GetProperty("MaximumMinutes").GetInt32();
            if(early<18||early>22||max<120||max>480)Fail("Invalid overnight bounds.");
            string[][] expected=[["{f0}","{f1}","{v0}","{v1}"],["{f2}","{f3}","{v0}","{v1}"],[]];
            IEnumerable<string> Tokens(string s)=>System.Text.RegularExpressions.Regex.Matches(s,@"\{[^{}]+\}").Select(m=>m.Value).Order();
            foreach(string field in new[]{"Facts","AlternativeFacts"})
            {
                var facts=row.GetProperty(field);if(facts.GetArrayLength()!=3)Fail("Overnight requires start, next-day end and continuity facts.");
                for(int i=0;i<3;i++)if(!Tokens(facts[i].GetString()!).SequenceEqual(expected[i].Order()))Fail("Changed overnight fact slots.");
            }
            foreach(string field in new[]{"Question","AlternativeQuestion"})if(!Tokens(RequiredString(row,field)).SequenceEqual(new[]{"{v0}","{v1}"}))Fail("Changed overnight target slots.");
            if(Tokens(RequiredString(row,"Lead")).Any())Fail("Unexpected overnight lead slots.");
        }
    }
}
