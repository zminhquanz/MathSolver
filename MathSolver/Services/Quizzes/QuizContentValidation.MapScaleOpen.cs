using MathSolver.Models;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckMapScaleOpenLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(MapScaleOpenPilot.ListName,out var rows))return;
        if(rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()!=2)Fail("Missing map scale open contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            if(!ids.Add(id)||id is not ("park" or "school"))Fail("Invalid map scale open context.");
            if(RequiredString(row,"Relation")!=MapScaleOpenPilot.Relation||RequiredString(row,"ScaleRole")!="scale-denominator"
                ||RequiredString(row,"MapLengthRole")!="map-cm"||RequiredString(row,"TargetRole")!="actual-m"
                ||RequiredString(row,"MapUnit")!="cm"||RequiredString(row,"ActualUnit")!="m")Fail("Changed map scale open roles or units.");
            _=RequiredString(row,"Map");_=RequiredString(row,"Segment");
            int maximum=row.GetProperty("MaximumScale").GetInt32();
            if(maximum is <100 or >100000||maximum%100!=0||row.GetProperty("MaximumMapLength").GetInt32() is <2 or >20)Fail("Invalid map scale open bounds.");
            foreach(var (field,slots) in new (string,string[])[]{("ScaleFact",["{v0}","{f0}"]),
                ("LengthFact",["{v0}","{v1}","{f1}","{v2}"]),("Question",["{v1}","{v3}"]),
                ("CentimetreLead",["{v1}"]),("ActualLead",["{v1}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(x=>x.Value).Order().SequenceEqual(slots.Order()))Fail("Changed map scale open slots: "+field);
            MapScaleOpenPilot.Context? context;
            try{context=row.Deserialize<MapScaleOpenPilot.Context>();}catch(JsonException){Fail("Invalid map scale open context schema.");return;}
            if(context is null||!MapScaleOpenMeaning.ProvesReference(context,pack.Culture=="vi-VN"?AppLanguage.Vietnamese:AppLanguage.English))
                Fail("Map scale reference must prove scale one to n, map centimetres and actual metres.");
        }
    }
}
