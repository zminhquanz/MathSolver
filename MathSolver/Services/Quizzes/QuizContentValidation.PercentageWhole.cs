using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckPercentageWholeLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(PercentageWholePilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=3)Fail("Missing percentage whole contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            if(!ids.Add(id)||id is not ("books" or "pens" or "notebooks"))Fail("Invalid percentage whole context.");
            if(RequiredString(row,"Relation")!=PercentageWholePilot.Relation||RequiredString(row,"Action")!="donate"
                ||RequiredString(row,"KnownRole")!="donated-count"||RequiredString(row,"BaseRole")!="original-whole"
                ||RequiredString(row,"TargetRole")!="original-whole")Fail("Invalid percentage whole roles.");
            if(RequiredString(row,"Donor")==RequiredString(row,"Recipient"))Fail("Donation requires distinct parties.");
            _=RequiredString(row,"Unit");
            if(row.GetProperty("Capacity").GetInt32() is <100 or >10000)Fail("Invalid percentage capacity.");
            foreach(var (field,expected) in new (string,string[])[]{("Gift",["{f0}","{v0}","{v1}","{v2}"]),
                ("Share",["{f1}","{v0}","{v1}"]),("Question",["{v0}","{v1}"]),("Lead",["{v1}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))Fail("Changed percentage whole slots: "+field);
        }
    }
}
