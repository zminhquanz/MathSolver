using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckAverageMissingLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(AverageMissingPilot.ListName,out var contexts))return;
        if(contexts.GetArrayLength()!=3)Fail("Expected three missing-value contexts.");
        var ids=new HashSet<string>();
        foreach(var row in contexts.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate missing-value context.");
            var groups=row.GetProperty("Groups").EnumerateArray().Select(g=>g.GetString()).ToArray();
            if(groups.Length!=3||groups.Any(string.IsNullOrWhiteSpace)||groups.Distinct().Count()!=3)Fail("Missing-value groups must be distinct.");
            foreach(string field in new[]{"Unit","GroupUnit","GroupPlural","Verb","BaseVerb"})_=RequiredString(row,field);
            if(row.GetProperty("Capacity").GetInt32() is <20 or >100000)Fail("Invalid missing-value capacity.");
            foreach(var (field,slots) in new (string,string[])[]{("Fact",["{owner}","{count}","{v3}","{v5}"]),
                ("Mean",["{v4}","{v5}","{f2}","{v3}"]),("Scope",["{f3}","{v7}"]),
                ("Question",["{v2}","{v6}","{v3}"]),("Lead",["{v3}","{v2}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(slots.Order()))
                    Fail("Changed missing-value role slots: "+field);
        }
    }
}
