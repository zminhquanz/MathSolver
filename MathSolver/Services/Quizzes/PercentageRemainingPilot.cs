using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

internal static class PercentageRemainingPilot
{
    internal const string Prefix="percentage-remaining-open/",ListName="PercentageRemainingOpen.Contexts";
    internal const string Relation="known-original-whole-donated-percentage-find-remaining";
    internal sealed record Context(string Id,string Relation,string Donor,string Recipient,string Unit,string Action,
        string KnownRole,string RateRole,string TargetRole,int Capacity,string Whole,string Share,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Whole,int Percentage)
    {
        internal int Donated=>Whole*Percentage/100;
        internal int Answer=>Whole-Donated;
        internal string Expression=>$"{Whole} − {Whole} × {Percentage} ÷ 100";
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=Whole.ToString(CultureInfo.InvariantCulture),["f1"]=Percentage.ToString(CultureInfo.InvariantCulture)+"%",
            ["v0"]=Context.Donor,["v1"]=Context.Unit,["v2"]=Context.Recipient
        };
        internal string Render(string text)
        {
            foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);
            return FractionRemainingPilot.CapitalizeOpenings(text);
        }
        internal string Problem=>Render(Context.Whole+" "+Context.Share+" "+Context.Question);
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidPercentageRemainingTier");
        var contexts=Contexts(language);
        var context=profile.Length==0?contexts[random.Next(contexts.Count)]:contexts.SingleOrDefault(c=>Prefix+c.Id==profile)
            ??throw new ArgumentException("InvalidPercentageRemainingProfile");
        if(context.Relation!=Relation||context.Action!="donate"||context.KnownRole!="original-whole"
            ||context.RateRole!="donated-percentage-of-original"||context.TargetRole!="donor-remaining-count"
            ||context.Donor==context.Recipient||context.Capacity<100)throw new InvalidDataException("InvalidPercentageRemainingRelation");
        int percentage=PercentagePartPilot.Percentages[random.Next(PercentagePartPilot.Percentages.Length)];
        int step=100/(int)BigInteger.GreatestCommonDivisor(100,percentage);
        int maximum=Math.Min(context.Capacity,new[]{100,200,500,1000,2000}[(int)tier-1]);
        int whole=step*random.Next((20+step-1)/step,maximum/step+1);
        return new(context,Prefix+context.Id,whole,percentage);
    }
}

public sealed partial class PercentageQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenRemaining(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
        =>CreateQuestion(mode,CreateRemainingContract(language,tier,profile),includeWordProblem:false);
    private PercentageQuizContract CreateRemainingContract(AppLanguage language,CurriculumTier tier,string profile="")
    {
        var d=PercentageRemainingPilot.Create(profile,language,tier,_random);
        string equation=d.Expression+" = "+d.Answer.ToString(CultureInfo.InvariantCulture);
        return new(PercentageQuizType.FindRemainingAfterDonation,[d.Whole,d.Percentage],d.Answer,d.Context.Unit,
            d.Render(d.Context.Lead),d.Problem,equation,FormatSolution(d.Render(d.Context.Lead)+":",equation,d.Context.Unit,d.Answer,language),
            d.Whole,ArithmeticOperation.Subtract,d.Donated){StoryContextId=d.Profile};
    }
}
