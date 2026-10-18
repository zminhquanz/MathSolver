using System.Text.RegularExpressions;
using MathSolver.Models;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckRectanglePerimeterLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(RectanglePerimeterPilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=4)Fail("Missing rectangle perimeter contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            if(!ids.Add(id)||id is not ("garden" or "playground" or "card" or "photo"))Fail("Invalid rectangle context.");
            if(RequiredString(row,"Relation")!=RectanglePerimeterPilot.Relation||RequiredString(row,"ShapeId")!="rectangle"
                ||RequiredString(row,"LengthRole")!="length"||RequiredString(row,"WidthRole")!="width"||RequiredString(row,"TargetRole")!="perimeter")
                Fail("Changed rectangle roles.");
            _=RequiredString(row,"Object");
            if(RequiredString(row,"Shape")!=(pack.Culture=="vi-VN"?"hình chữ nhật":"rectangle"))Fail("Rectangle shape label changed.");
            if(!Enum.TryParse<GeometryLengthUnit>(RequiredString(row,"Unit"),out var unit)||!Enum.IsDefined(unit)
                ||row.GetProperty("MaximumLength").GetInt32() is <3 or >1000)Fail("Invalid rectangle length bounds/unit.");
            foreach(var (field,expected) in new (string,string[])[]{("ShapeFact",["{v0}","{v1}"]),
                ("LengthFact",["{v0}","{f0}","{v2}"]),("WidthFact",["{v0}","{f1}","{v2}"]),
                ("Question",["{v0}","{v2}"]),("Lead",["{v0}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))
                    Fail("Changed rectangle perimeter slots: "+field);
        }
    }
}
