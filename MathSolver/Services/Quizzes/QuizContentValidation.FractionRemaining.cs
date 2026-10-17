using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckFractionRemainingLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(FractionRemainingPilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=3)Fail("Missing fraction remaining contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            if(!ids.Add(id)||id is not ("books" or "pens" or "notebooks"))Fail("Invalid fraction remaining context.");
            if(RequiredString(row,"Relation")!=FractionRemainingPilot.Relation||RequiredString(row,"Action")!="donate")Fail("Invalid remaining relation.");
            if(RequiredString(row,"Donor")==RequiredString(row,"Recipient"))Fail("Donation requires distinct parties.");
            _=RequiredString(row,"Unit");
            if(row.GetProperty("Capacity").GetInt32() is <20 or >10000)Fail("Invalid remaining capacity.");
            foreach(var (field,expected) in new (string,string[])[]{("Whole",["{f0}","{v0}","{v1}"]),
                ("Share",["{f1}","{v0}","{v1}","{v2}"]),("Question",["{v0}","{v1}"]),("Lead",["{v1}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))
                    Fail("Changed fraction remaining roles: "+field);
        }
    }
}
