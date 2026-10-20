namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckSameMonthCalendarLists(QuizContentPack pack)
    {
        foreach(bool inclusive in new[]{false,true})CheckSameMonthCalendarList(pack,inclusive);
        CheckSameMonthCalendarList(pack,false,true);
        CheckSameMonthCalendarList(pack,true,true);
        CheckSameMonthCalendarList(pack,true,true,true);
        CheckSameMonthCalendarList(pack,true,true,leap:true);
    }
    private static void CheckSameMonthCalendarList(QuizContentPack pack,bool inclusive,bool adjacent=false,bool february=false,bool leap=false)
    {
        if(!pack.Lists.TryGetValue(leap?SameMonthCalendarPilot.FebruaryLeapListName:february?SameMonthCalendarPilot.FebruaryCommonListName:adjacent?(inclusive?SameMonthCalendarPilot.AdjacentInclusiveListName:SameMonthCalendarPilot.AdjacentListName):inclusive?SameMonthCalendarPilot.InclusiveListName:SameMonthCalendarPilot.ListName,out var rows))return;
        if(rows.ValueKind!=System.Text.Json.JsonValueKind.Array||rows.GetArrayLength()==0)Fail("Missing same-month contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate same-month context.");
            foreach(string field in new[]{"Actor","Item","StartAction","EndAction","Interval"})_=RequiredString(row,field);
            if(RequiredString(row,"StartAction")==RequiredString(row,"EndAction"))Fail("Same-month events must differ.");
            if(RequiredString(row,"RelationId")!=(leap?SameMonthCalendarPilot.FebruaryLeapRelation:february?SameMonthCalendarPilot.FebruaryCommonRelation:adjacent?(inclusive?SameMonthCalendarPilot.AdjacentInclusiveRelation:SameMonthCalendarPilot.AdjacentRelation):inclusive?SameMonthCalendarPilot.InclusiveRelation:SameMonthCalendarPilot.Relation)
                ||RequiredString(row,"CountRule")!=(inclusive?"include-start-include-end":"exclude-start-include-end")
                ||RequiredString(row,"TargetRole")!=(inclusive?"counted-calendar-days":"elapsed-days"))Fail("Changed same-month relation/counting rule.");
            string[] known=adjacent?["start-day","end-day","start-month","end-month","year","start-month-length","include-start","include-end"]:["start-day","end-day","month","year","include-start","include-end"];
            if(!row.GetProperty("KnownRoles").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(known))Fail("Changed calendar roles.");
            string[][] expected=[["{f0}","{f2}","{f3}","{v0}","{v1}","{v2}"],["{f1}","{f2}","{f3}","{v0}","{v1}","{v3}"],[]];
            if(adjacent)expected=[["{f0}","{f2}","{f3}","{v0}","{v1}","{v2}"],["{f1}","{f4}","{f3}","{v0}","{v1}","{v3}"],["{f2}","{f5}"],[]];
            IEnumerable<string> Tokens(string s)=>System.Text.RegularExpressions.Regex.Matches(s,@"\{[^{}]+\}").Select(m=>m.Value).Order();
            foreach(string field in new[]{"Facts","AlternativeFacts"})
            {
                var facts=row.GetProperty(field);if(facts.GetArrayLength()!=expected.Length)Fail("Calendar requires dates, month length where needed, and an explicit count rule.");
                for(int i=0;i<expected.Length;i++)if(!Tokens(facts[i].GetString()!).SequenceEqual(expected[i].Order()))Fail("Changed calendar fact slots.");
            }
            foreach(string field in new[]{"Question","AlternativeQuestion"})if(!Tokens(RequiredString(row,field)).SequenceEqual(new[]{"{v0}","{v4}"}))Fail("Changed same-month target slots.");
            if(Tokens(RequiredString(row,"Lead")).Any())Fail("Unexpected same-month lead slots.");
        }
    }
}
