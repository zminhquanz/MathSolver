using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckUnitaryGroupsLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(UnitaryGroupsPilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=3)Fail("Expected three unitary-group contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate unitary-group context.");
            string item=RequiredString(row,"Items"),group=RequiredString(row,"Group"),groups=RequiredString(row,"Groups");
            if(item.Equals(group,StringComparison.OrdinalIgnoreCase)||item.Equals(groups,StringComparison.OrdinalIgnoreCase))Fail("Group/item units must differ.");
            if(row.GetProperty("Capacity").GetInt32() is <20 or >100000)Fail("Invalid unitary-group capacity.");
            foreach(var (field,slots) in new (string,string[])[]{("Given",["{f0}","{v0}","{f1}","{v1}"]),
                ("GivenAlternate",["{f0}","{v0}","{f1}","{v1}"]),
                ("Condition",["{v1}","{v2}"]),("Question",["{f2}","{v1}","{v0}"]),
                ("RateLead",["{v1}","{v2}"]),("CountLead",["{v0}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(slots.Order()))
                    Fail("Changed unitary-group slots: "+field);
        }
        if(!ids.SetEquals(new[]{"pens","gifts","notebooks"}))Fail("Unsupported unitary-group context.");
    }
}
