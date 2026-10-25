using MathSolver.Models;
using MathSolver.Services.QuestionBank;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckTriangleAreaLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(TriangleAreaPilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=2)Fail("Missing triangle area contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            if(!ids.Add(id)||id is not ("card" or "garden"))Fail("Invalid triangle area context.");
            if(RequiredString(row,"Relation")!=TriangleAreaPilot.Relation||RequiredString(row,"ShapeId")!="triangle"
                ||RequiredString(row,"BaseRole")!="base"||RequiredString(row,"HeightRole")!="corresponding-perpendicular-height"
                ||RequiredString(row,"TargetRole")!="area"||RequiredString(row,"LengthUnitRole")!="length-unit"||RequiredString(row,"AreaUnitRole")!="area-unit")
                Fail("Changed triangle area roles.");
            _=RequiredString(row,"Object");
            if(RequiredString(row,"Shape")!=(pack.Culture=="vi-VN"?"hình tam giác":"triangle"))Fail("Triangle shape label changed.");
            if(!Enum.TryParse<GeometryLengthUnit>(RequiredString(row,"Unit"),out var unit)||!Enum.IsDefined(unit)
                ||row.GetProperty("MaximumLength").GetInt32() is <4 or >1000)Fail("Invalid triangle bounds/unit.");
            foreach(var (field,expected) in new (string,string[])[]{("ShapeFact",["{v0}","{v1}"]),("BaseFact",["{v0}","{f0}","{v2}"]),
                ("HeightFact",["{v0}","{f1}","{v2}"]),("Question",["{v0}","{v3}"]),("Lead",["{v0}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))Fail("Changed triangle area slots: "+field);
            // Validate the complete supplied propositions, rather than seeing
            // relation keywords inside a denial or an unrelated height clause.
            // This minimal language contract does not read the pack again.
            var language=pack.Culture=="vi-VN"?AppLanguage.Vietnamese:AppLanguage.English;
            var proofContract=new BasicQuestionContract(ReasoningStoryCatalogue.Version,ArithmeticOperation.Add,CurriculumTier.OneStar,language,1,1,"","","");
            var reference=new BasicQuestionDraft("","",RequiredString(row,"Question"),"","story")
            {
                Facts=[new("fact_0",RequiredString(row,"ShapeFact")),new("fact_1",RequiredString(row,"BaseFact")),new("fact_2",RequiredString(row,"HeightFact"))],
                SolutionLeads=[new("step_0",RequiredString(row,"Lead"))]
            };
            if(!TriangleAreaOpenMeaning.Validate(proofContract,reference).IsValid)
                Fail("Triangle reference must prove the supplied base, its corresponding perpendicular height and the whole-area target.");
        }
    }
}
