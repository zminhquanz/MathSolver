using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Static subset of a known whole; never a change, remainder, ratio or inverse problem.</summary>
internal static class PercentagePartPilot
{
    internal const string Prefix="percentage-part-open/", ListName="PercentagePartOpen.Contexts";
    internal const string RelationId="known-whole-find-percentage-part";
    internal static ReadOnlySpan<int> Percentages => [10,20,25,50,75];
    internal sealed record Context(string Id,string Relation,string Owner,string WholeUnit,string WholeNoun,
        string PartNoun,string PartUnit,int Capacity,string Whole,string Share,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Whole,int Percentage)
    {
        internal int Answer=>Whole*Percentage/100;
        internal string Expression=>$"{Whole} × {Percentage} ÷ 100";
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=N(Whole),["f1"]=N(Percentage)+"%",["v0"]=Context.Owner,["v1"]=Context.WholeUnit,
            ["v2"]=Context.WholeNoun,["v3"]=Context.PartNoun,["v4"]=Context.PartUnit
        };
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return text;}
        internal string Problem=>Render(Context.Whole+" "+Context.Share+" "+Context.Question);
    }
    internal static string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidPercentagePartProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidPercentagePartContext");
        if(context.Relation!=RelationId||context.Capacity<100)throw new InvalidDataException("InvalidPercentagePartRelation");
        int percentage=Percentages[random.Next(Percentages.Length)];
        int step=100/(int)System.Numerics.BigInteger.GreatestCommonDivisor(100,percentage);
        int max=Math.Min(context.Capacity,new[]{100,200,500,1000,2000}[(int)tier-1]);
        int whole=step*random.Next((20+step-1)/step,max/step+1);
        return new(context,profile,whole,percentage);
    }
}

public sealed partial class PercentageQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenPart(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=PercentagePartPilot.Create(profile,language,tier,_random);
        string equation=d.Expression+" = "+PercentagePartPilot.N(d.Answer);
        var contract=new PercentageQuizContract(PercentageQuizType.FindPercentageValue,[d.Whole,d.Percentage],d.Answer,
            d.Context.PartUnit,d.Context.PartNoun,d.Problem,equation,
            FormatSolution(d.Render(d.Context.Lead)+":",equation,d.Context.PartUnit,d.Answer,language),
            d.Whole*d.Percentage,ArithmeticOperation.Divide,100){StoryContextId=profile};
        return CreateQuestion(mode,contract,includeWordProblem:false);
    }
}
