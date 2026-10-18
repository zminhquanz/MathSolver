using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>One included break, two same-day clocks, active minutes only.</summary>
internal static class SameDayBreakPilot
{
    internal const string Prefix="time-break-open/", ListName="TimeBreakOpen.Contexts";
    internal const string Relation="same-day-one-break-find-active-duration";
    internal const ElementaryQuizType Type=ElementaryQuizType.SameDayActiveDuration;
    internal sealed record Context(string Id,string Actor,string Activity,int EarliestHour,int LatestHour,int MaximumMinutes,
        string RelationId,string[] KnownRoles,string TargetRole,string[] Facts,string Question,string Lead,
        string[] AlternativeFacts,string AlternativeQuestion);
    internal sealed record Data(Context Context,string Profile,int Start,int End,int Break)
    {
        internal int Elapsed=>End-Start;
        internal int Active=>Elapsed-Break;
        internal string Expression=>$"({End/60}*60+{End%60})-({Start/60}*60+{Start%60})-{Break}";
        internal Dictionary<string,string> Values=>new(){["f0"]=N(Start/60),["f1"]=N(Start%60),["f2"]=N(End/60),
            ["f3"]=N(End%60),["f4"]=N(Break),["v0"]=Context.Actor,["v1"]=Context.Activity};
        private static string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
        internal string Render(string text)
        {
            foreach(var (id,value) in Values)text=text.Replace("{"+id+"}",value,StringComparison.Ordinal);
            return SameDayTimePilot.CapitalizeSentenceOpenings(text);
        }
        internal string Problem=>Render(string.Join(" ",Context.Facts.Append(Context.Question)));
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidSameDayBreakProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidSameDayBreakProfile");
        int quantum=(int)tier switch{1=>15,2=>5,_=>1};
        int cap=Math.Min(context.MaximumMinutes,new[]{60,90,120,180,240}[(int)tier-1]);
        int elapsed=random.Next(2,cap/quantum+1)*quantum;
        int pause=random.Next(1,Math.Min(45,elapsed-quantum)/quantum+1)*quantum;
        int start=random.Next(context.EarliestHour*60/quantum,(context.LatestHour*60-elapsed)/quantum+1)*quantum;
        return new(context,profile,start,start+elapsed,pause);
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateSameDayBreak(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
        =>CompleteQuestion(mode,CreateSameDayBreak(language,tier,profile),[],null);
    private ElementaryQuizContract CreateSameDayBreak(AppLanguage language,CurriculumTier tier,string profile="")
    {
        if(profile.Length==0)
        {
            var profiles=SameDayBreakPilot.Profiles(language).ToArray();
            profile=profiles[NextContextVariant(SameDayBreakPilot.Type,language,"same-day-one-break",profiles.Length)];
        }
        var d=SameDayBreakPilot.Create(profile,language,tier,_random);
        string unit=language==AppLanguage.Vietnamese?"phút":"minutes";
        var t=new DifficultyBuilder(QuizProblemKind.Time,SameDayBreakPilot.Type,language,tier){RequiresSolution=false};
        t.Constant(60);
        t.Given("start-hour",d.Start/60);t.Given("start-minute",d.Start%60);
        t.Given("end-hour",d.End/60);t.Given("end-minute",d.End%60);t.Given("included-break-minutes",d.Break,unit);
        t.Answer(d.Context.Lead,d.Expression,unit);
        return t.Build(SameDayBreakPilot.Relation,d.Problem) with {StoryContextId=profile,RequiresCalculation=false};
    }
}
