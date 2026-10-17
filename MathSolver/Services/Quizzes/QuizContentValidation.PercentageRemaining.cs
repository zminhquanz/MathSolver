using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckPercentageRemainingLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(PercentageRemainingPilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=3)Fail("Missing percentage remaining contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            if(!ids.Add(id)||id is not ("books" or "pens" or "notebooks"))Fail("Invalid percentage remaining context.");
            if(RequiredString(row,"Relation")!=PercentageRemainingPilot.Relation||RequiredString(row,"Action")!="donate"
                ||RequiredString(row,"KnownRole")!="original-whole"||RequiredString(row,"RateRole")!="donated-percentage-of-original"
                ||RequiredString(row,"TargetRole")!="donor-remaining-count")Fail("Invalid percentage remaining roles.");
            if(RequiredString(row,"Donor")==RequiredString(row,"Recipient"))Fail("Donation requires distinct parties.");
            _=RequiredString(row,"Unit");
            if(row.GetProperty("Capacity").GetInt32() is <100 or >10000)Fail("Invalid percentage capacity.");
            foreach(var (field,expected) in new (string,string[])[]{("Whole",["{f0}","{v0}","{v1}"]),
                ("Share",["{f1}","{v0}","{v1}","{v2}"]),("Question",["{v0}","{v1}"]),("Lead",["{v1}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))Fail("Changed percentage remaining slots: "+field);
        }
    }
}
