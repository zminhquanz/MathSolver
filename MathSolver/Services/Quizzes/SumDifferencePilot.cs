using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Two stocks in one unit, with their total and oriented difference given directly.</summary>
internal static class SumDifferencePilot
{
    internal const string Prefix="sum-difference-open/", ListName="SumDifferenceOpen.Contexts", Relation="direct-total-oriented-difference-find-both";
    internal sealed record Context(string Id,string LargerOwner,string SmallerOwner,string Unit,int Capacity,
        string Total,string Difference,string Question,string SmallerLead,string LargerLead);
    internal sealed record Data(Context Context,string Profile,int Smaller,int Larger)
    {
        internal int Total=>Smaller+Larger;
        internal int Difference=>Larger-Smaller;
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=Total.ToString(CultureInfo.InvariantCulture),["f1"]=Difference.ToString(CultureInfo.InvariantCulture),
            ["v0"]=Context.LargerOwner,["v1"]=Context.SmallerOwner,["v2"]=Context.Unit
        };
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return FractionRemainingPilot.CapitalizeOpenings(text);}
        internal string Problem=>Render(string.Join(" ",Context.Total,Context.Difference,Context.Question));
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidSumDifferenceProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidSumDifferenceContext");
        if(context.LargerOwner==context.SmallerOwner||context.Capacity<20)throw new InvalidDataException("InvalidSumDifferenceOwners");
        int max=Math.Min(context.Capacity,30*(int)tier);
        int smaller=random.Next(2,max/2+1),larger=random.Next(smaller+1,max+1);
        return new(context,profile,smaller,larger);
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenSumDifference(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=SumDifferencePilot.Create(profile,language,tier,_random);
        var b=new DifficultyBuilder(QuizProblemKind.TwoNumbers,ElementaryQuizType.SumDifference,language,tier);
        b.Constant(2);
        string total=b.Given("combined-total",d.Total,d.Context.Unit),difference=b.Given("larger-minus-smaller",d.Difference,d.Context.Unit);
        // Keep the existing two-number convention: the smaller answer comes first.
        b.Answer(d.Context.SmallerOwner,$"({total}-{difference})/2",d.Context.Unit);
        b.Answer(d.Context.LargerOwner,$"({total}+{difference})/2",d.Context.Unit);
        var diagram=new QuizDiagram("bars",d.Render(d.Context.Total+" "+d.Context.Difference),
            [new(d.Context.SmallerOwner,[new("?",2)]),new(d.Context.LargerOwner,[new("?",2),new(d.Difference.ToString(CultureInfo.InvariantCulture),Highlight:true)])]);
        var contract=b.Build(SumDifferencePilot.Relation,d.Problem,diagram:diagram) with {StoryContextId=d.Profile};
        return CompleteQuestion(mode,contract,[],null);
    }
}
