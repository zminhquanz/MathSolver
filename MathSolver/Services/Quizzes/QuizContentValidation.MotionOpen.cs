using MathSolver.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckMotionOpenLists(QuizContentPack pack)
    {
        bool hasContexts=pack.Lists.TryGetValue("MotionOpen.Contexts",out var contexts),hasRelations=pack.Lists.TryGetValue("MotionOpen.Relations",out var relations);
        if(!hasContexts&&!hasRelations)return;
        if(!hasContexts||!hasRelations||contexts.GetArrayLength()!=3||relations.GetArrayLength()!=4)Fail("Invalid motion catalogue size.");
        var ids=new HashSet<string>();
        foreach(var c in contexts.EnumerateArray())
        {
            if(!ids.Add(RequiredString(c,"Id")))Fail("Duplicate motion context.");
            RequiredString(c,"Subject");
            int min=c.GetProperty("MinimumSpeed").GetInt32(),max=c.GetProperty("MaximumSpeed").GetInt32();
            if(min<1||max<min||max>120)Fail("Invalid motion speed range.");
        }
        if(!ids.SetEquals(new[]{"car","bus","bicycle"}))Fail("Unknown motion context.");
        ids.Clear();
        foreach(var r in relations.EnumerateArray())
        {
            string id=RequiredString(r,"Id");if(!ids.Add(id))Fail("Duplicate motion relation.");
            var expected=id switch {"distance"=>MotionQuestionKind.BasicDistance,"speed"=>MotionQuestionKind.BasicSpeed,
                "time"=>MotionQuestionKind.BasicTime,"rest"=>MotionQuestionKind.BasicRestDistance,_=>(MotionQuestionKind)(-1)};
            var kind=r.GetProperty("Kind");int actual=kind.ValueKind==JsonValueKind.Number?kind.GetInt32():Enum.TryParse<MotionQuestionKind>(kind.GetString(),out var parsed)?(int)parsed:-2;
            if(actual!=(int)expected||(int)expected<0)Fail("Changed motion relation kind.");
            string[] source=id is "speed" or "time"?["{v0}","{f0}","{v3}"]:["{v0}","{f0}","{v1}"];
            void Slots(string key,string[] expectedSlots)
            {if(!Regex.Matches(RequiredString(r,key),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expectedSlots.Order()))Fail("Changed motion slots: "+key);}
            Slots("Fact0",source);Slots("AlternateFact0",source);
            Slots("Fact1",id=="time"?["{f1}","{v1}"]:["{f1}","{v2}"]);
            Slots("Question",[id=="speed"?"{v1}":id=="time"?"{v2}":"{v3}"]);Slots("Lead",[]);
            if(id=="rest")Slots("Fact2",["{f2}","{v4}"]);
            else if(r.TryGetProperty("Fact2",out var extra)&&extra.ValueKind!=JsonValueKind.Null)Fail("Unexpected stop in continuous motion.");
        }
        if(!ids.SetEquals(new[]{"distance","speed","time","rest"}))Fail("Unknown motion relation.");
    }
}
