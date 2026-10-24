using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

internal static class UnitaryGroupsPilot
{
    internal const string Prefix="unitary-groups-open/",ListName="UnitaryGroupsOpen.Contexts",
        Relation="source-groups-and-total-new-item-total-find-groups";
    internal sealed record Context(string Id,string Group,string Groups,string Items,int Capacity,
        string Given,string GivenAlternate,string Condition,string Question,string RateLead,string CountLead);
    internal sealed record Data(Context Context,string Profile,int SourceGroups,int TargetGroups,int PerGroup)
    {
        internal int SourceTotal=>SourceGroups*PerGroup;
        internal int TargetTotal=>TargetGroups*PerGroup;
        internal UnitaryGroupUnits Units=>new(Context.Items,Context.Group,Context.Groups);
        internal Dictionary<string,string> Values=>new()
        { ["f0"]=N(SourceGroups),["f1"]=N(SourceTotal),["f2"]=N(TargetTotal),
          ["v0"]=Context.Groups,["v1"]=Context.Items,["v2"]=Context.Group };
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return text;}
        internal string Problem=>Render(string.Join(" ",Context.Given,Context.Condition,Context.Question));
    }
    private static string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidUnitaryGroupsProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidUnitaryGroupsContext");
        if(context.Capacity<20)throw new InvalidDataException("InvalidUnitaryGroupsCapacity");
        int maximum=Math.Min(4+2*(int)tier,context.Capacity/2);
        int source=random.Next(2,maximum+1),target=random.Next(2,maximum+1);
        if(source==target)target=target==maximum?2:target+1;
        int per=random.Next(2,Math.Min(10*(int)tier,context.Capacity/Math.Max(source,target))+1);
        return new(context,profile,source,target,per);
    }
    internal static bool ValidContract(ProportionQuizContract p,out int rate,out BigInteger answer)
    {
        rate=0;answer=0;var u=p.GroupCountUnits;
        if(p.Type!=ProportionQuizType.UnitaryGroupCount||u is null||p.Scenario!=ProportionScenarioKind.ProductionItems
            ||p.AsksForAdditionalPeople||p.A<=0||p.B<=0||p.C<=0||p.B%p.A!=0
            ||string.IsNullOrWhiteSpace(u.Items)||string.IsNullOrWhiteSpace(u.Group)||string.IsNullOrWhiteSpace(u.Groups)
            ||u.Items.Equals(u.Group,StringComparison.OrdinalIgnoreCase)||u.Items.Equals(u.Groups,StringComparison.OrdinalIgnoreCase)
            ||p.AnswerUnit!=u.Groups)return false;
        rate=p.B/p.A;if(rate<=0||p.C%rate!=0)return false;
        answer=p.C/rate;
        return answer>0&&p.CorrectAnswer==answer;
    }
}

public sealed partial class ProportionQuizGenerator
{
    private ProportionQuizContract CreateUnitaryGroupsContract(AppLanguage language,CurriculumTier tier,string? profile)
    {
        profile=string.IsNullOrEmpty(profile)?UnitaryGroupsPilot.Profiles(language).ElementAt(_random.Next(UnitaryGroupsPilot.Contexts(language).Count)):profile;
        var d=UnitaryGroupsPilot.Create(profile,language,tier,_random);
        string solution=d.Render(d.Context.RateLead)+":\n"+$"{d.SourceTotal} ÷ {d.SourceGroups} = {d.PerGroup} {d.Units.Rate}\n"
            +d.Render(d.Context.CountLead)+":\n"+$"{d.TargetTotal} ÷ {d.PerGroup} = {d.TargetGroups} {d.Units.Groups}\n"
            +(language==AppLanguage.Vietnamese?"Đáp số: ":"Answer: ")+$"{d.TargetGroups} {d.Units.Groups}";
        return new(ProportionQuizType.UnitaryGroupCount,ProportionScenarioKind.ProductionItems,d.SourceGroups,d.SourceTotal,
            d.TargetTotal,d.TargetGroups,d.Units.Groups,d.Units.Groups,d.Problem)
            {NarrativeId=profile,SolutionText=solution,GroupCountUnits=d.Units};
    }
}
