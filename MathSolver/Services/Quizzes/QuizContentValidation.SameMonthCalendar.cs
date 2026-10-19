namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckSameMonthCalendarLists(QuizContentPack pack)
    {
        foreach(bool inclusive in new[]{false,true})CheckSameMonthCalendarList(pack,inclusive);
    }
    private static void CheckSameMonthCalendarList(QuizContentPack pack,bool inclusive)
    {
        if(!pack.Lists.TryGetValue(inclusive?SameMonthCalendarPilot.InclusiveListName:SameMonthCalendarPilot.ListName,out var rows))return;
        if(rows.ValueKind!=System.Text.Json.JsonValueKind.Array||rows.GetArrayLength()==0)Fail("Missing same-month contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate same-month context.");
            foreach(string field in new[]{"Actor","Item","StartAction","EndAction","Interval"})_=RequiredString(row,field);
            if(RequiredString(row,"StartAction")==RequiredString(row,"EndAction"))Fail("Same-month events must differ.");
            if(RequiredString(row,"RelationId")!=(inclusive?SameMonthCalendarPilot.InclusiveRelation:SameMonthCalendarPilot.Relation)
                ||RequiredString(row,"CountRule")!=(inclusive?"include-start-include-end":"exclude-start-include-end")
                ||RequiredString(row,"TargetRole")!=(inclusive?"counted-calendar-days":"elapsed-days"))Fail("Changed same-month relation/counting rule.");
            if(!row.GetProperty("KnownRoles").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"start-day","end-day","month","year","include-start","include-end"}))Fail("Changed same-month roles.");
            string[][] expected=[["{f0}","{f2}","{f3}","{v0}","{v1}","{v2}"],["{f1}","{f2}","{f3}","{v0}","{v1}","{v3}"],[]];
            IEnumerable<string> Tokens(string s)=>System.Text.RegularExpressions.Regex.Matches(s,@"\{[^{}]+\}").Select(m=>m.Value).Order();
            foreach(string field in new[]{"Facts","AlternativeFacts"})
            {
                var facts=row.GetProperty(field);if(facts.GetArrayLength()!=3)Fail("Same-month requires start, end and explicit count-rule facts.");
                for(int i=0;i<3;i++)if(!Tokens(facts[i].GetString()!).SequenceEqual(expected[i].Order()))Fail("Changed same-month fact slots.");
            }
            foreach(string field in new[]{"Question","AlternativeQuestion"})if(!Tokens(RequiredString(row,field)).SequenceEqual(new[]{"{v0}","{v4}"}))Fail("Changed same-month target slots.");
            if(Tokens(RequiredString(row,"Lead")).Any())Fail("Unexpected same-month lead slots.");
        }
    }
}
