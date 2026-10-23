using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckDifferenceRatioLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(DifferenceRatioPilot.ListName,out var contexts))return;
        if(contexts.GetArrayLength()!=3)Fail("Expected three direct difference/ratio contexts.");
        var ids=new HashSet<string>();
        foreach(var row in contexts.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate difference/ratio context.");
            if(RequiredString(row,"SmallerOwner")==RequiredString(row,"LargerOwner"))Fail("Difference/ratio owners must differ.");
            _=RequiredString(row,"Unit");
            if(row.GetProperty("Capacity").GetInt32() is <20 or >100000)Fail("Invalid difference/ratio capacity.");
            foreach(var (field,slots) in new (string,string[])[]{("Difference",["{v0}","{v1}","{f0}","{v2}"]),
                ("Ratio",["{v0}","{v1}","{f1}","{v2}","{v2}"]),("Question",["{v0}","{v1}","{v2}"]),
                ("PartLead",["{v2}"]),("SmallerLead",["{v2}","{v0}"]),("LargerLead",["{v2}","{v1}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(slots.Order()))
                    Fail("Changed difference/ratio role slots: "+field);
        }
    }
}
