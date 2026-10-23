using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Known direct difference and smaller/larger ratio; no inferred data or transfers.</summary>
internal static class DifferenceRatioPilot
{
    internal const string Prefix="difference-ratio-open/", ListName="DifferenceRatioOpen.Contexts", Relation="direct-difference-oriented-ratio-find-both";
    internal sealed record Context(string Id,string SmallerOwner,string LargerOwner,string Unit,int Capacity,
        string Difference,string Ratio,string Question,string PartLead,string SmallerLead,string LargerLead);
    internal sealed record Data(Context Context,string Profile,int SmallerParts,int LargerParts,int PartValue)
    {
        internal int Smaller=>SmallerParts*PartValue;
        internal int Larger=>LargerParts*PartValue;
        internal int Difference=>Larger-Smaller;
        // The fraction is one immutable C# value. AI must not supply a slash or
        // split numerator/denominator into independently interpreted quantities.
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=Difference.ToString(CultureInfo.InvariantCulture),["f1"]=$"{SmallerParts}/{LargerParts}",
            ["v0"]=Context.SmallerOwner,["v1"]=Context.LargerOwner,["v2"]=Context.Unit
        };
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return FractionRemainingPilot.CapitalizeOpenings(text);}
        internal string Problem=>Render(string.Join(" ",Context.Difference,Context.Ratio,Context.Question));
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidDifferenceRatioProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidDifferenceRatioContext");
        if(context.SmallerOwner==context.LargerOwner||context.Capacity<20)throw new InvalidDataException("InvalidDifferenceRatioOwners");
        (int Small,int Large)[] ratios=[(1,2),(1,3),(2,3),(2,5),(3,4),(3,5),(4,5)];
        var ratio=ratios[random.Next(Math.Min(ratios.Length,2+(int)tier))];
        int maximum=Math.Min(20*(int)tier,context.Capacity/ratio.Large);
        return new(context,profile,ratio.Small,ratio.Large,random.Next(2,maximum+1));
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenDifferenceRatio(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=DifferenceRatioPilot.Create(profile,language,tier,_random);
        var b=new DifficultyBuilder(QuizProblemKind.TwoNumbers,ElementaryQuizType.DifferenceRatio,language,tier);
        string difference=b.Given("larger-minus-smaller-difference",d.Difference,d.Context.Unit),small=b.Given("smaller-ratio-parts",d.SmallerParts),large=b.Given("larger-ratio-parts",d.LargerParts);
        string part=$"{difference}/({large}-{small})";
        b.Step(d.Render(d.Context.PartLead),part,d.Context.Unit);
        b.Answer(d.Context.SmallerOwner,$"{part}*{small}",d.Context.Unit);
        b.Answer(d.Context.LargerOwner,$"{part}*{large}",d.Context.Unit);
        QuizDiagramSegment[] Parts(int count)=>Enumerable.Range(0,count).Select(_=>new QuizDiagramSegment("?")).ToArray();
        var diagram=new QuizDiagram("bars",d.Render(d.Context.Difference),
            [new(d.Context.SmallerOwner,Parts(d.SmallerParts)),new(d.Context.LargerOwner,Parts(d.LargerParts))]);
        var contract=b.Build(DifferenceRatioPilot.Relation,d.Problem,diagram:diagram) with {StoryContextId=d.Profile};
        return CompleteQuestion(mode,contract,[],null);
    }
}
