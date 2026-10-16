using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckFractionPartLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(FractionPartPilot.ListName,out var contexts))return;
        if(contexts.GetArrayLength()!=3)Fail("Missing fraction part contexts.");
        var ids=new HashSet<string>();
        foreach(var row in contexts.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            if(!ids.Add(id)||id is not ("books" or "pens" or "notebooks"))Fail("Invalid fraction part context.");
            _=RequiredString(row,"Actor");_=RequiredString(row,"Unit");
            int capacity=row.GetProperty("Capacity").GetInt32();
            if(capacity is <20 or >10000)Fail("Invalid counted fraction capacity.");
            foreach(var (field,expected) in new (string,string[])[]{("Whole",["{f0}","{v0}","{v1}"]),
                ("Share",["{f1}","{v0}","{v1}"]),("Question",["{v0}","{v1}"]),("Lead",["{v1}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))
                    Fail("Changed fraction part roles: "+field);
        }
    }
}
