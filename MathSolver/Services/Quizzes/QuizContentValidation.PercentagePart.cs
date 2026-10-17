using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckPercentagePartLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(PercentagePartPilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=3)Fail("Missing percentage-part contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate percentage-part context.");
            if(RequiredString(row,"Relation")!=PercentagePartPilot.RelationId)Fail("Changed percentage-part relation.");
            foreach(string field in new[]{"Owner","WholeUnit","WholeNoun","PartNoun","PartUnit"})_=RequiredString(row,field);
            if(row.GetProperty("Capacity").GetInt32() is <100 or >10000)Fail("Invalid counted percentage capacity.");
            if(!RequiredString(row,"WholeUnit").Contains(RequiredString(row,"WholeNoun"),StringComparison.OrdinalIgnoreCase)
                ||!RequiredString(row,"PartUnit").Contains(RequiredString(row,"PartNoun"),StringComparison.OrdinalIgnoreCase)
                ||RequiredString(row,"WholeNoun")==RequiredString(row,"PartNoun"))Fail("Invalid percentage unit/subset bindings.");
            foreach(var (field,slots) in new (string,string[])[]{("Whole",["{v0}","{f0}","{v1}"]),
                ("Share",["{f1}","{v2}","{v3}"]),("Question",["{v0}","{v4}"]),("Lead",["{v4}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(slots.Order()))
                    Fail("Changed percentage-part slots: "+field);
        }
    }
}
