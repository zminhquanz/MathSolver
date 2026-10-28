using MathSolver.Models;
using MathSolver.Services.QuestionBank;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckQuadrilateralAreaLists(QuizContentPack pack)
    {
        foreach(int variant in new[]{QuadrilateralAreaPilot.ParallelogramVariant,QuadrilateralAreaPilot.TrapezoidVariant})
        {
            if(!pack.Lists.TryGetValue(QuadrilateralAreaPilot.ListName(variant),out var rows))continue;
            if(rows.GetArrayLength()!=2)Fail("Missing quadrilateral area contexts.");
            bool trap=variant==QuadrilateralAreaPilot.TrapezoidVariant;
            var language=pack.Culture=="vi-VN"?AppLanguage.Vietnamese:AppLanguage.English;
            var ids=new HashSet<string>();
            foreach(var row in rows.EnumerateArray())
            {
                string id=RequiredString(row,"Id");
                if(!ids.Add(id)||id is not ("card" or "garden"))Fail("Invalid quadrilateral area context.");
                if(RequiredString(row,"Relation")!=QuadrilateralAreaPilot.Relation(variant)||RequiredString(row,"ShapeId")!=QuadrilateralAreaPilot.ShapeId(variant)
                    ||RequiredString(row,"BaseRole")!=(trap?"parallel-bases":"base")||RequiredString(row,"HeightRole")!="corresponding-perpendicular-height"
                    ||RequiredString(row,"TargetRole")!="area"||RequiredString(row,"LengthUnitRole")!="length-unit"||RequiredString(row,"AreaUnitRole")!="area-unit")
                    Fail("Changed quadrilateral area roles.");
                string objectName=RequiredString(row,"Object");
                if(objectName.Contains(QuadrilateralAreaPilot.ShapeName(variant,language),StringComparison.OrdinalIgnoreCase)
                    ||RequiredString(row,"Shape")!=QuadrilateralAreaPilot.ShapeName(variant,language))Fail("Quadrilateral shape/object label changed.");
                if(!Enum.TryParse<GeometryLengthUnit>(RequiredString(row,"Unit"),out var unit)||!Enum.IsDefined(unit)
                    ||row.GetProperty("MaximumLength").GetInt32() is <4 or >1000)Fail("Invalid quadrilateral bounds/unit.");
                foreach(var (field,expected) in new (string,string[])[]{("ShapeFact",["{v0}","{v1}"]),
                    ("BaseFact",trap?["{v0}","{f0}","{v2}","{f1}","{v2}"]:["{v0}","{f0}","{v2}"]),
                    ("HeightFact",["{v0}",trap?"{f2}":"{f1}","{v2}"]),("Question",["{v0}","{v3}"]),("Lead",["{v0}"])})
                    if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))Fail("Changed quadrilateral area slots: "+field);
                var contract=new BasicQuestionContract(ReasoningStoryCatalogue.Version,ArithmeticOperation.Add,CurriculumTier.OneStar,language,1,1,"","","")
                    {Story=new(1,BankQuestionFamily.Geometry,variant,"",QuadrilateralAreaPilot.Prefix(variant)+id)};
                var draft=new BasicQuestionDraft("","",RequiredString(row,"Question"),"","story")
                {Facts=[new("fact_0",RequiredString(row,"ShapeFact")),new("fact_1",RequiredString(row,"BaseFact")),new("fact_2",RequiredString(row,"HeightFact"))],
                    SolutionLeads=[new("step_0",RequiredString(row,"Lead"))]};
                if(!QuadrilateralAreaOpenMeaning.Validate(contract,draft).IsValid)
                    Fail("Quadrilateral reference must prove supplied bases, corresponding perpendicular height and entire area.");
            }
        }
    }
}
