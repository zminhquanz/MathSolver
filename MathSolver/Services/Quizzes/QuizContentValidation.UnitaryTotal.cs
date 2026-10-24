using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckUnitaryTotalLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(UnitaryTotalPilot.ListName,out var contexts))return;
        if(contexts.GetArrayLength()!=3)Fail("Expected three unitary-total contexts.");
        var ids=new HashSet<string>();
        foreach(var row in contexts.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate unitary-total context.");
            foreach(string field in new[]{"Group","Groups","Unit"})_=RequiredString(row,field);
            if(row.GetProperty("Capacity").GetInt32() is <20 or >100000)Fail("Invalid unitary-total capacity.");
            foreach(var (field,slots) in new (string,string[])[]{("Given",["{f0}","{v0}","{f1}","{v1}"]),
                ("Condition",["{v1}","{v2}"]),("Question",["{f2}","{v0}","{v1}"]),
                ("UnitLead",["{v1}","{v2}"]),("TotalLead",["{v1}","{v0}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(slots.Order()))
                    Fail("Changed unitary-total slots: "+field);
        }
    }
}
