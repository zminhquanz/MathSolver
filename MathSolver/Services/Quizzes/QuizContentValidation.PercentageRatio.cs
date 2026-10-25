using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckPercentageRatioLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(PercentageRatioPilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=2)Fail("Missing percentage ratio contexts.");var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            string id=RequiredString(row,"Id");if(id is not ("library" or "nursery")||!ids.Add(id))Fail("Invalid ratio context ID.");
            if(RequiredString(row,"Relation")!=PercentageRatioPilot.Relation||row.GetProperty("Capacity").GetInt32()!=2000)Fail("Invalid ratio role/capacity.");
            foreach(string field in new[]{"Owner","WholeUnit","WholeNoun","PartUnit","PartNoun"})_=RequiredString(row,field);
            foreach(var (field,slots) in new (string,string[])[]{("Whole",["{v0}","{f0}","{v1}"]),
                ("Part",["{v2}","{f1}","{v3}"]),("Question",["{v0}","{v2}","{v4}"]),("Lead",["{v2}","{v4}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(slots.Order()))Fail("Changed ratio slots: "+field);
            var language=pack.Culture=="vi-VN"?AppLanguage.Vietnamese:AppLanguage.English;
            var proof=new BasicQuestionContract(ReasoningStoryCatalogue.Version,ArithmeticOperation.Add,CurriculumTier.OneStar,language,1,1,"","","");
            var reference=new BasicQuestionDraft("","",RequiredString(row,"Question"),"","story")
            {Facts=[new("fact_0",RequiredString(row,"Whole")),new("fact_1",RequiredString(row,"Part"))],
                SolutionLeads=[new("step_0",RequiredString(row,"Lead"))]};
            if(!PercentageRatioOpenMeaning.Validate(proof,reference).IsValid)Fail("Unproved percentage-ratio reference relation.");
        }
    }
}
