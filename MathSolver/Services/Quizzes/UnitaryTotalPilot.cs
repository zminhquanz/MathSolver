using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

internal static class UnitaryTotalPilot
{
    internal const string Prefix="unitary-total-open/",ListName="UnitaryTotalOpen.Contexts",Relation="source-groups-and-total-find-new-total";
    internal sealed record Context(string Id,string Group,string Groups,string Unit,int Capacity,
        string Given,string Condition,string Question,string UnitLead,string TotalLead);
    internal sealed record Data(Context Context,string Profile,int SourceGroups,int TargetGroups,int PerGroup)
    {
        internal int SourceTotal=>SourceGroups*PerGroup;
        internal int Answer=>TargetGroups*PerGroup;
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=N(SourceGroups),["f1"]=N(SourceTotal),["f2"]=N(TargetGroups),
            ["v0"]=Context.Groups,["v1"]=Context.Unit,["v2"]=Context.Group
        };
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return text;}
        internal string Problem=>Render(string.Join(" ",Context.Given,Context.Condition,Context.Question));
    }
    internal static string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidUnitaryTotalProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidUnitaryTotalContext");
        if(context.Capacity<20)throw new InvalidDataException("InvalidUnitaryTotalCapacity");
        int maximum=4+2*(int)tier;
        int source=random.Next(2,maximum+1),target=random.Next(2,maximum+1);
        if(target==source)target=target==maximum?2:target+1;
        int perGroup=random.Next(2,Math.Min(10*(int)tier,context.Capacity/Math.Max(source,target))+1);
        return new(context,profile,source,target,perGroup);
    }
}

public sealed partial class ProportionQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenUnitaryTotal(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=UnitaryTotalPilot.Create(profile,language,tier,_random);
        string solution=d.Render(d.Context.UnitLead)+":\n"+$"{d.SourceTotal} ÷ {d.SourceGroups} = {d.PerGroup} {d.Context.Unit}\n"
            +d.Render(d.Context.TotalLead)+":\n"+$"{d.PerGroup} × {d.TargetGroups} = {d.Answer} {d.Context.Unit}\n"
            +(language==AppLanguage.Vietnamese?"Đáp số: ":"Answer: ")+$"{d.Answer} {d.Context.Unit}";
        var contract=new ProportionQuizContract(ProportionQuizType.Direct,ProportionScenarioKind.ProductionItems,
            d.SourceGroups,d.SourceTotal,d.TargetGroups,d.Answer,d.Context.Unit,d.Context.Unit,d.Problem)
            {NarrativeId=profile,SolutionText=solution};
        return CreateQuestion(mode,contract);
    }
}
