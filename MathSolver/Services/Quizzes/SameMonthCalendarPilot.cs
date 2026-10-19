using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Two independent same-month counting relations, sharing date generation.</summary>
internal static class SameMonthCalendarPilot
{
    internal const string Prefix="calendar-same-month/",ListName="SameMonthCalendarOpen.Contexts";
    internal const string Relation="same-month-exclude-start-include-end";
    internal const ElementaryQuizType Type=ElementaryQuizType.SameMonthElapsed;
    internal const string InclusivePrefix="calendar-same-month-inclusive/",InclusiveListName="SameMonthInclusiveOpen.Contexts";
    internal const string InclusiveRelation="same-month-include-start-include-end";
    internal const ElementaryQuizType InclusiveType=ElementaryQuizType.SameMonthInclusive;
    internal static bool IsInclusive(string profile)=>profile.StartsWith(InclusivePrefix,StringComparison.Ordinal);
    internal static ElementaryQuizType ProfileType(string profile)=>IsInclusive(profile)?InclusiveType:Type;
    internal static string ProfileRelation(string profile)=>IsInclusive(profile)?InclusiveRelation:Relation;
    internal sealed record Context(string Id,string Actor,string Item,string StartAction,string EndAction,string Interval,
        string RelationId,string CountRule,string[] KnownRoles,string TargetRole,
        string[] Facts,string Question,string Lead,string[] AlternativeFacts,string AlternativeQuestion);
    internal sealed record Data(Context Context,string Profile,int StartDay,int EndDay,int Month,int Year)
    {
        internal bool IncludeStart=>IsInclusive(Profile);
        internal int Elapsed=>EndDay-StartDay+(IncludeStart?1:0);
        internal string Expression=>$"{EndDay}-{StartDay}"+(IncludeStart?"+1":"");
        internal Dictionary<string,string> Values=>new(){["f0"]=N(StartDay),["f1"]=N(EndDay),["f2"]=N(Month),["f3"]=N(Year),
            ["v0"]=Context.Actor,["v1"]=Context.Item,["v2"]=Context.StartAction,["v3"]=Context.EndAction,["v4"]=Context.Interval};
        private static string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
        internal string Render(string text)
        {
            foreach(var (id,value) in Values)text=text.Replace("{"+id+"}",value,StringComparison.Ordinal);
            return FractionRemainingPilot.CapitalizeOpenings(text);
        }
        internal string Problem=>Render(string.Join(" ",Context.Facts.Append(Context.Question)));
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language,bool inclusive=false)
        =>QuizContentCatalog.LoadList<Context>(inclusive?InclusiveListName:ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language,bool inclusive=false)
        =>Contexts(language,inclusive).Select(c=>(inclusive?InclusivePrefix:Prefix)+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        bool inclusive=IsInclusive(profile);
        string prefix=inclusive?InclusivePrefix:Prefix;
        if(!Enum.IsDefined(tier)||!profile.StartsWith(prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidSameMonthProfile");
        var context=Contexts(language,inclusive).SingleOrDefault(c=>prefix+c.Id==profile)??throw new ArgumentException("InvalidSameMonthProfile");
        // Every tier retains the same counting relation. No pauses or month crossing.
        int[] months=[1,3,4,5,6,7,8,9,10,11,12];
        int year=random.Next(2025,2031),month=months[random.Next(months.Length)],length=DateTime.DaysInMonth(year,month);
        // Inclusive counting permits equal dates: one calendar day, never zero.
        int duration=random.Next(inclusive?0:1,Math.Min(length-1,new[]{7,12,18,24,30}[(int)tier-1])+1);
        int start=random.Next(1,length-duration+1);
        return new(context,profile,start,start+duration,month,year);
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateSameMonthCalendar(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
        =>CompleteQuestion(mode,CreateSameMonthCalendar(language,tier,profile),[],null);
    private ElementaryQuizContract CreateSameMonthCalendar(AppLanguage language,CurriculumTier tier,string profile="",bool inclusive=false)
    {
        if(profile.Length==0)
        {
            var profiles=SameMonthCalendarPilot.Profiles(language,inclusive).ToArray();
            profile=profiles[NextContextVariant(inclusive?SameMonthCalendarPilot.InclusiveType:SameMonthCalendarPilot.Type,language,
                inclusive?SameMonthCalendarPilot.InclusiveRelation:SameMonthCalendarPilot.Relation,profiles.Length)];
        }
        var d=SameMonthCalendarPilot.Create(profile,language,tier,_random);
        var t=new DifficultyBuilder(QuizProblemKind.Time,SameMonthCalendarPilot.ProfileType(profile),language,tier){RequiresSolution=false};
        t.Given("start-day",d.StartDay);t.Given("end-day",d.EndDay);t.Given("month",d.Month);t.Given("year",d.Year);
        t.Given("include-start",d.IncludeStart?1:0);t.Given("include-end",1);
        t.Answer(d.Context.Lead,d.Expression,language==AppLanguage.Vietnamese?"ngày":"days");
        return t.Build(SameMonthCalendarPilot.ProfileRelation(profile),d.Problem) with {StoryContextId=profile,RequiresCalculation=false};
    }
}
