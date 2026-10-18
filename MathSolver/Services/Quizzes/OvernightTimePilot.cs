using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>One continuous activity; its end clock is on exactly the following day.</summary>
internal static class OvernightTimePilot
{
    internal const string Prefix="overnight-open/",ListName="OvernightTimeOpen.Contexts";
    internal const string Relation="overnight-continuous-find-duration";
    internal const ElementaryQuizType Type=ElementaryQuizType.OvernightElapsed;
    internal sealed record Context(string Id,string Actor,string Activity,int EarliestHour,int MaximumMinutes,
        int EndDayOffset,string Continuity,string RelationId,string[] KnownRoles,string TargetRole,
        string[] Facts,string Question,string Lead,string[] AlternativeFacts,string AlternativeQuestion);
    internal sealed record Data(Context Context,string Profile,int Start,int End)
    {
        internal int EndDayOffset=>1;
        internal int Elapsed=>EndDayOffset*24*60+End-Start;
        internal string Expression=>$"(24*60+{End/60}*60+{End%60})-({Start/60}*60+{Start%60})";
        internal Dictionary<string,string> Values=>new(){["f0"]=N(Start/60),["f1"]=N(Start%60),
            ["f2"]=N(End/60),["f3"]=N(End%60),["v0"]=Context.Actor,["v1"]=Context.Activity};
        private static string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
        internal string Render(string text)
        {
            foreach(var (id,value) in Values)text=text.Replace("{"+id+"}",value,StringComparison.Ordinal);
            return SameDayTimePilot.CapitalizeSentenceOpenings(text);
        }
        internal string Problem=>Render(string.Join(" ",Context.Facts.Append(Context.Question)));
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)
        =>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidOvernightProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidOvernightProfile");
        int quantum=(int)tier switch {1=>15,2=>5,_=>1};
        int cap=Math.Min(context.MaximumMinutes,new[]{120,180,240,360,480}[(int)tier-1]);
        // Always cross midnight, even at one star; an end at 00:00 is valid.
        int earliest=Math.Max(context.EarliestHour*60,24*60-cap);
        int start=random.Next(earliest/quantum,24*60/quantum)*quantum;
        int duration=random.Next((24*60-start)/quantum,cap/quantum+1)*quantum;
        return new(context,profile,start,start+duration-24*60);
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOvernightTime(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
        =>CompleteQuestion(mode,CreateOvernightTime(language,tier,profile),[],null);
    private ElementaryQuizContract CreateOvernightTime(AppLanguage language,CurriculumTier tier,string profile="")
    {
        if(profile.Length==0)
        {
            var profiles=OvernightTimePilot.Profiles(language).ToArray();
            profile=profiles[NextContextVariant(OvernightTimePilot.Type,language,OvernightTimePilot.Relation,profiles.Length)];
        }
        var d=OvernightTimePilot.Create(profile,language,tier,_random);
        string unit=language==AppLanguage.Vietnamese?"phút":"minutes";
        var t=new DifficultyBuilder(QuizProblemKind.Time,OvernightTimePilot.Type,language,tier){RequiresSolution=false};
        t.Constant(24);t.Constant(60);
        t.Given("start-hour",d.Start/60);t.Given("start-minute",d.Start%60);
        t.Given("end-hour",d.End/60);t.Given("end-minute",d.End%60);t.Given("end-day-offset",d.EndDayOffset);
        t.Answer(d.Context.Lead,d.Expression,unit);
        return t.Build(OvernightTimePilot.Relation,d.Problem) with {StoryContextId=profile,RequiresCalculation=false};
    }
}
