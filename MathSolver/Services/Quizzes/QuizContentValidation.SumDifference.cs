using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckSumDifferenceLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(SumDifferencePilot.ListName,out var contexts))return;
        if(contexts.GetArrayLength()!=3)Fail("Expected three direct sum/difference contexts.");
        var ids=new HashSet<string>();
        foreach(var row in contexts.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate sum/difference context.");
            if(RequiredString(row,"LargerOwner")==RequiredString(row,"SmallerOwner"))Fail("Sum/difference owners must differ.");
            _=RequiredString(row,"Unit");
            if(row.GetProperty("Capacity").GetInt32() is <20 or >100000)Fail("Invalid sum/difference capacity.");
            foreach(var (field,slots) in new (string,string[])[]{("Total",["{v0}","{v1}","{f0}","{v2}"]),
                ("Difference",["{v0}","{v1}","{f1}","{v2}"]),("Question",["{v0}","{v1}","{v2}"]),
                ("SmallerLead",["{v2}","{v1}"]),("LargerLead",["{v2}","{v0}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(slots.Order()))
                    Fail("Changed sum/difference role slots: "+field);
        }
    }
}
