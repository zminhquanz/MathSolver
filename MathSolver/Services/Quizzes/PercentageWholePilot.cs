using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>One gift: known donated count and its percentage of the unknown original stock.</summary>
internal static class PercentageWholePilot
{
    internal const string Prefix="percentage-whole-open/", ListName="PercentageWholeOpen.Contexts";
    internal const string Relation="known-donated-percentage-part-find-original-whole";
    internal sealed record Context(string Id,string Relation,string KnownRole,string BaseRole,string TargetRole,
        string Donor,string Recipient,string Unit,string Action,int Capacity,string Gift,string Share,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Whole,int Percentage)
    {
        internal int Donated=>Whole*Percentage/100;
        internal string Expression=>$"{Donated} × 100 ÷ {Percentage}";
        // Never expose the unknown original stock as a model input quantity.
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=Donated.ToString(CultureInfo.InvariantCulture),["f1"]=Percentage.ToString(CultureInfo.InvariantCulture)+"%",
            ["v0"]=Context.Donor,["v1"]=Context.Unit,["v2"]=Context.Recipient
        };
        internal string Render(string text)
        {
            foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);
            return FractionRemainingPilot.CapitalizeOpenings(text);
        }
        internal string Problem=>Render(Context.Gift+" "+Context.Share+" "+Context.Question);
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidPercentageWholeTier");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidPercentageWholeContext");
        if(context.Relation!=Relation||context.KnownRole!="donated-count"||context.BaseRole!="original-whole"
            ||context.TargetRole!="original-whole"||context.Action!="donate"||context.Donor==context.Recipient||context.Capacity<100)
            throw new InvalidDataException("InvalidPercentageWholeRelation");
        int percentage=PercentagePartPilot.Percentages[random.Next(PercentagePartPilot.Percentages.Length)];
        int step=100/(int)System.Numerics.BigInteger.GreatestCommonDivisor(100,percentage);
        int maximum=Math.Min(context.Capacity,new[]{100,200,500,1000,2000}[(int)tier-1]);
        int whole=step*random.Next((20+step-1)/step,maximum/step+1);
        return new(context,profile,whole,percentage);
    }
}

public sealed partial class PercentageQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenWhole(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=PercentageWholePilot.Create(profile,language,tier,_random);
        string equation=d.Expression+" = "+d.Whole;
        var contract=new PercentageQuizContract(PercentageQuizType.FindWholeFromPercentageValue,[d.Donated,d.Percentage],d.Whole,
            d.Context.Unit,d.Render(d.Context.Lead),d.Problem,equation,
            FormatSolution(d.Render(d.Context.Lead),equation,d.Context.Unit,d.Whole,language),
            d.Donated*100,ArithmeticOperation.Divide,d.Percentage) {StoryContextId=profile};
        return CreateQuestion(mode,contract,includeWordProblem:false);
    }
}
