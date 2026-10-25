using MathSolver.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckRectangleSideLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(RectangleSidePilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=4)Fail("Missing rectangle side contexts.");
        var options=new JsonSerializerOptions{Converters={new JsonStringEnumConverter()}};
        var language=pack.Culture=="vi-VN"?AppLanguage.Vietnamese:AppLanguage.English;
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            foreach(string field in new[]{"Id","Relation","ShapeId","PerimeterRole","KnownRole","TargetRole","UnitRole","Object","Shape","Unit",
                "ShapeFact","PerimeterFact","KnownFact","Question","HalfLead","Lead"})_=RequiredString(row,field);
            RectangleSidePilot.Context? context;
            try{context=row.Deserialize<RectangleSidePilot.Context>(options);}
            catch(JsonException){Fail("Invalid rectangle side context fields.");return;}
            if(context is null||!ids.Add(context.Id)||!RectangleSidePilot.IsValidContext(context,language)
                ||!QuestionBank.RectangleSideOpenMeaning.ProvesReference(context,language))Fail("Changed rectangle side relation, roles or reference prose.");
            foreach(var (field,expected) in new (string,string[])[]{("ShapeFact",["{v0}","{v1}"]),
                ("PerimeterFact",["{v0}","{f0}","{v2}"]),("KnownFact",["{v0}","{f1}","{v2}"]),
                ("Question",["{v0}","{v2}"]),("HalfLead",["{v0}"]),("Lead",["{v0}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))
                    Fail("Changed rectangle side slots: "+field);
        }
    }
}
