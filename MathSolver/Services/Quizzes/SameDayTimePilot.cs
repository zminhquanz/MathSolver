using MathSolver.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

internal static class SameDayTimePilot
{
    internal const string Prefix="time-open/", ContextList="TimeOpen.Contexts", RelationList="TimeOpen.Relations";
    internal static string CapitalizeSentenceOpenings(string text)=>Regex.Replace(text,
        @"(^\s*|[.!?]\s+)buổi(?=\s)","$1Buổi",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal sealed record Context(string Id,string Actor,string Activity,int EarliestHour,int LatestHour,int MaximumMinutes);
    internal sealed record Relation(string Id,ElementaryQuizType Type,string[] Facts,string Question,string Lead,
        string[] AlternativeFacts,string AlternativeQuestion);
    internal sealed record Data(Context Context,Relation Relation,string Profile,int Start,int End)
    {
        internal int Duration=>End-Start;
        internal int Known=>Relation.Id=="start"?End:Start;
        internal int Result=>Relation.Id switch {"elapsed"=>Duration,"start"=>Start,_=>End};
        internal string Expression=>Relation.Id=="elapsed"?$"({End/60}*60+{End%60})-({Start/60}*60+{Start%60})"
            :$"({Known/60}*60+{Known%60})"+(Relation.Id=="start"?"-":"+")+Duration;
        internal Dictionary<string,string> Values=>new(){["f0"]=(Known/60).ToString(CultureInfo.InvariantCulture),
            ["f1"]=(Known%60).ToString(CultureInfo.InvariantCulture),
            ["f2"]=(Relation.Id=="elapsed"?End/60:Duration).ToString(CultureInfo.InvariantCulture),
            ["f3"]=(End%60).ToString(CultureInfo.InvariantCulture),["v0"]=Context.Actor,["v1"]=Context.Activity};
        internal string Render(string text){foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return CapitalizeSentenceOpenings(text);}
        internal string Problem=>Render(string.Join(" ",Relation.Facts.Append(Relation.Question)));
    }
    internal static bool IsType(ElementaryQuizType type)=>type is ElementaryQuizType.SameDayElapsed or ElementaryQuizType.SameDayStart or ElementaryQuizType.SameDayEnd;
    internal static bool IsClock(ElementaryQuizType type)=>type is ElementaryQuizType.SameDayStart or ElementaryQuizType.SameDayEnd;
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ContextList,QuizContentCatalog.Culture(language));
    internal static IReadOnlyList<Relation> Relations(AppLanguage language)=>QuizContentCatalog.LoadList<Relation>(RelationList,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(ElementaryQuizType type,AppLanguage language)=>from r in Relations(language) where r.Type==type from c in Contexts(language) select Prefix+r.Id+"/"+c.Id;
    internal static Data Create(string profile,ElementaryQuizType type,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!IsType(type)||!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidSameDayProfile");
        var parts=profile[Prefix.Length..].Split('/');
        var relation=parts.Length==2?Relations(language).SingleOrDefault(r=>r.Id==parts[0]&&r.Type==type):null;
        var context=parts.Length==2?Contexts(language).SingleOrDefault(c=>c.Id==parts[1]):null;
        if(relation is null||context is null)throw new ArgumentException("InvalidSameDayProfile");
        int quantum=(int)tier switch{1=>15,2=>5,_=>1};
        int duration=random.Next(1,context.MaximumMinutes/quantum+1)*quantum;
        int start=random.Next(context.EarliestHour*60/quantum,(context.LatestHour*60-duration)/quantum+1)*quantum;
        return new(context,relation,profile,start,start+duration);
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateSameDayTime(ArithmeticQuizMode mode,ElementaryQuizType type,AppLanguage language,CurriculumTier tier,string profile)
        =>CompleteQuestion(mode,CreateSameDayTime(type,language,tier,profile),[],null);
    private ElementaryQuizContract CreateSameDayTime(ElementaryQuizType type,AppLanguage language,CurriculumTier tier,string profile="")
    {
        if(profile.Length==0){var profiles=SameDayTimePilot.Profiles(type,language).ToArray();profile=profiles[NextContextVariant(type,language,"same-day-time",profiles.Length)];}
        var d=SameDayTimePilot.Create(profile,type,language,tier,_random);
        bool vi=language==AppLanguage.Vietnamese;
        var t=new DifficultyBuilder(QuizProblemKind.Time,type,language,tier){RequiresSolution=false};
        t.Constant(60);
        t.Given(d.Relation.Id=="start"?"end-hour":"start-hour",d.Known/60);
        t.Given(d.Relation.Id=="start"?"end-minute":"start-minute",d.Known%60);
        if(d.Relation.Id=="elapsed"){t.Given("end-hour",d.End/60);t.Given("end-minute",d.End%60);}
        else t.Given("duration-minutes",d.Duration,vi?"phút":"minutes");
        string lead=d.Render(d.Relation.Lead),unit=vi?"phút":"minutes";
        if(d.Relation.Id=="elapsed")t.Answer(lead,d.Expression,unit);
        else
        {
            t.Step(lead,d.Expression,unit);
            t.Answers.Add(new(vi?"Giờ":"Hours",new(d.Result/60,1),"",(d.Result/60).ToString(CultureInfo.InvariantCulture)));
            t.Answers.Add(new(vi?"Phút":"Minutes",new(d.Result%60,1),"",(d.Result%60).ToString(CultureInfo.InvariantCulture)));
        }
        return t.Build(profile,d.Problem) with {StoryContextId=profile,RequiresCalculation=false};
    }
}
