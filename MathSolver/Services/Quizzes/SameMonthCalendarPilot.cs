using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Separate counting profiles sharing calendar roles and rendering.</summary>
internal static class SameMonthCalendarPilot
{
    internal const string Prefix="calendar-same-month/",ListName="SameMonthCalendarOpen.Contexts";
    internal const string Relation="same-month-exclude-start-include-end";
    internal const ElementaryQuizType Type=ElementaryQuizType.SameMonthElapsed;
    internal const string InclusivePrefix="calendar-same-month-inclusive/",InclusiveListName="SameMonthInclusiveOpen.Contexts";
    internal const string InclusiveRelation="same-month-include-start-include-end";
    internal const ElementaryQuizType InclusiveType=ElementaryQuizType.SameMonthInclusive;
    internal const string AdjacentPrefix="calendar-adjacent-months/",AdjacentListName="AdjacentMonthCalendarOpen.Contexts";
    internal const string AdjacentRelation="adjacent-months-exclude-start-include-end";
    internal const ElementaryQuizType AdjacentType=ElementaryQuizType.AdjacentMonthElapsed;
    internal static bool IsAdjacent(string profile)=>profile.StartsWith(AdjacentPrefix,StringComparison.Ordinal);
    internal static bool IsInclusive(string profile)=>profile.StartsWith(InclusivePrefix,StringComparison.Ordinal);
    internal static ElementaryQuizType ProfileType(string profile)=>IsAdjacent(profile)?AdjacentType:IsInclusive(profile)?InclusiveType:Type;
    internal static string ProfileRelation(string profile)=>IsAdjacent(profile)?AdjacentRelation:IsInclusive(profile)?InclusiveRelation:Relation;
    internal sealed record Context(string Id,string Actor,string Item,string StartAction,string EndAction,string Interval,
        string RelationId,string CountRule,string[] KnownRoles,string TargetRole,
        string[] Facts,string Question,string Lead,string[] AlternativeFacts,string AlternativeQuestion);
    internal sealed record Data(Context Context,string Profile,int StartDay,int EndDay,int Month,int Year,int EndMonth=0)
    {
        internal bool IncludeStart=>IsInclusive(Profile);
        internal int MonthLength=>DateTime.DaysInMonth(Year,Month);
        internal int Elapsed=>IsAdjacent(Profile)?MonthLength-StartDay+EndDay:EndDay-StartDay+(IncludeStart?1:0);
        internal string Expression=>IsAdjacent(Profile)?$"({MonthLength}-{StartDay})+{EndDay}":$"{EndDay}-{StartDay}"+(IncludeStart?"+1":"");
        internal Dictionary<string,string> Values
        {
            get
            {
                Dictionary<string,string> values=new(){["f0"]=N(StartDay),["f1"]=N(EndDay),["f2"]=N(Month),["f3"]=N(Year),
                    ["v0"]=Context.Actor,["v1"]=Context.Item,["v2"]=Context.StartAction,["v3"]=Context.EndAction,["v4"]=Context.Interval};
                if(IsAdjacent(Profile)){values["f4"]=N(EndMonth);values["f5"]=N(MonthLength);}
                return values;
            }
        }
        private static string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
        internal string Render(string text)
        {
            foreach(var (id,value) in Values)text=text.Replace("{"+id+"}",value,StringComparison.Ordinal);
            return FractionRemainingPilot.CapitalizeOpenings(text);
        }
        internal string Problem=>Render(string.Join(" ",Context.Facts.Append(Context.Question)));
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language,bool inclusive=false,bool adjacent=false)
        =>QuizContentCatalog.LoadList<Context>(adjacent?AdjacentListName:inclusive?InclusiveListName:ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language,bool inclusive=false,bool adjacent=false)
        =>Contexts(language,inclusive,adjacent).Select(c=>(adjacent?AdjacentPrefix:inclusive?InclusivePrefix:Prefix)+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        bool inclusive=IsInclusive(profile),adjacent=IsAdjacent(profile);
        string prefix=adjacent?AdjacentPrefix:inclusive?InclusivePrefix:Prefix;
        if(!Enum.IsDefined(tier)||!profile.StartsWith(prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidSameMonthProfile");
        var context=Contexts(language,inclusive,adjacent).SingleOrDefault(c=>prefix+c.Id==profile)??throw new ArgumentException("InvalidSameMonthProfile");
        if(adjacent)
        {
            // Both months have 30/31 days, are consecutive, and stay in one year.
            int startMonth=random.Next(3,12),calendarYear=random.Next(2025,2031);
            int firstLength=DateTime.DaysInMonth(calendarYear,startMonth);
            int window=new[]{3,5,8,12,15}[(int)tier-1];
            return new(context,profile,random.Next(firstLength-window+1,firstLength+1),random.Next(1,window+1),startMonth,calendarYear,startMonth+1);
        }
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
    private ElementaryQuizContract CreateSameMonthCalendar(AppLanguage language,CurriculumTier tier,string profile="",bool inclusive=false,bool adjacent=false)
    {
        if(profile.Length==0)
        {
            var profiles=SameMonthCalendarPilot.Profiles(language,inclusive,adjacent).ToArray();
            profile=profiles[NextContextVariant(SameMonthCalendarPilot.ProfileType(profiles[0]),language,
                SameMonthCalendarPilot.ProfileRelation(profiles[0]),profiles.Length)];
        }
        var d=SameMonthCalendarPilot.Create(profile,language,tier,_random);
        var t=new DifficultyBuilder(QuizProblemKind.Time,SameMonthCalendarPilot.ProfileType(profile),language,tier){RequiresSolution=false};
        t.Given("start-day",d.StartDay);t.Given("end-day",d.EndDay);t.Given(SameMonthCalendarPilot.IsAdjacent(profile)?"start-month":"month",d.Month);t.Given("year",d.Year);
        t.Given("include-start",d.IncludeStart?1:0);t.Given("include-end",1);
        if(SameMonthCalendarPilot.IsAdjacent(profile)){t.Given("end-month",d.EndMonth);t.Given("start-month-length",d.MonthLength);}
        t.Answer(d.Context.Lead,d.Expression,language==AppLanguage.Vietnamese?"ngày":"days");
        return t.Build(SameMonthCalendarPilot.ProfileRelation(profile),d.Problem) with {StoryContextId=profile,RequiresCalculation=false};
    }
}
