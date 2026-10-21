using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>Three independent observations: two known quantities and their overall mean.</summary>
internal static class AverageMissingPilot
{
    internal const string Prefix="average-missing-open/", ListName="AverageMissingOpen.Contexts";
    internal sealed record Context(string Id,string[] Groups,string Unit,string GroupUnit,string GroupPlural,
        string Verb,string BaseVerb,int Capacity,string Fact,string Mean,string Scope,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Mean,int Known0,int Known1)
    {
        internal int Missing=>Mean*3-Known0-Known1;
        internal string Expression=>$"{Mean} × 3 − ({Known0} + {Known1})";
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=AverageOpenPilot.N(Known0),["f1"]=AverageOpenPilot.N(Known1),
            ["f2"]=AverageOpenPilot.N(Mean),["f3"]="3",
            ["v0"]=Context.Groups[0],["v1"]=Context.Groups[1],["v2"]=Context.Groups[2],
            ["v3"]=Context.Unit,["v4"]=Context.GroupUnit,["v5"]=Context.Verb,
            ["v6"]=Context.BaseVerb,["v7"]=Context.GroupPlural
        };
        internal string Fact(int i)=>Context.Fact.Replace("{owner}","{v"+i+"}").Replace("{count}","{f"+i+"}");
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return text;}
        internal string Problem=>Render(string.Join(" ",Context.Scope,Fact(0),Fact(1),Context.Mean,Context.Question));
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidAverageMissingProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidAverageMissingContext");
        if(context.Groups.Length!=3||context.Groups.Distinct().Count()!=3||context.Capacity<20)throw new InvalidDataException("InvalidAverageMissingGroups");
        int mean=random.Next(6,Math.Min(context.Capacity/2,15*(int)tier)+1);
        // Keep all three observations positive, distinct, and within capacity.
        // The unknown is deliberately not the mean or either known observation.
        int gap=random.Next(1,Math.Min((mean-1)/3,(context.Capacity-mean)/2)+1);
        int[] counts=[mean-3*gap,mean+gap,mean+2*gap];
        random.Shuffle(counts);
        return new(context,profile,mean,counts[0],counts[1]);
    }
}

public sealed partial class AverageQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenMissing(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=AverageMissingPilot.Create(profile,language,tier,_random);
        string equation=d.Expression+" = "+d.Missing;
        var contract=new AverageQuizContract(AverageQuizType.MissingValue,[3,d.Mean,d.Known0,d.Known1],d.Missing,d.Context.Unit,
            d.Render(d.Context.Lead),d.Problem,equation,FormatSolution(d.Render(d.Context.Lead)+":",equation,d.Context.Unit,d.Missing,language),
            d.Mean*3,ArithmeticOperation.Subtract,d.Known0+d.Known1,KnownScores:[d.Known0,d.Known1]){StoryContextId=profile};
        return CreateQuestion(mode,contract,includeWordProblem:false);
    }
}
