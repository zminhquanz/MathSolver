using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Known donated count and fraction of the original stock; find that original stock.</summary>
internal static class FractionWholePilot
{
    internal const string Prefix="fraction-whole-open/";
    internal const string ListName="FractionWholeOpen.Contexts";
    internal const string Relation="known-donated-part-find-original-whole";
    internal sealed record Context(string Id,string Relation,string KnownRole,string BaseRole,string TargetRole,
        string Donor,string Recipient,string Unit,string Action,int Capacity,string Gift,string Share,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Whole,int Numerator,int Denominator)
    {
        internal int Donated=>Whole/Denominator*Numerator;
        internal string Expression=>$"{Donated}/{Numerator}*{Denominator}";
        // The unknown whole is deliberately absent from facts exposed to the model.
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=Donated.ToString(CultureInfo.InvariantCulture),["f1"]=$"{Numerator}/{Denominator}",
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
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidFractionWholeTier");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidFractionWholeContext");
        if(context.Relation!=Relation||context.KnownRole!="donated-count"||context.BaseRole!="original-whole"
            ||context.TargetRole!="original-whole"||context.Action!="donate"||context.Donor==context.Recipient)
            throw new InvalidDataException("InvalidFractionWholeRelation");
        int denominator=random.Next(2,Math.Min(10,3+(int)tier)+1);
        int numerator=(int)tier==1?1:random.Next(1,denominator);
        int whole=denominator*random.Next(2,Math.Min(context.Capacity/denominator,4+(int)tier*3)+1);
        return new(context,profile,whole,numerator,denominator);
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateFractionWholePilot(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=FractionWholePilot.Create(profile,language,tier,_random);
        var b=new DifficultyBuilder(QuizProblemKind.FractionSkills,ElementaryQuizType.WholeFromFraction,language,tier);
        b.RequiresSolution=true;
        b.Given("donated-count",d.Donated,d.Context.Unit);
        b.Given("numerator",d.Numerator);b.Given("denominator",d.Denominator);
        b.Answer(d.Render(d.Context.Lead),d.Expression,d.Context.Unit);
        return CompleteQuestion(mode,b.Build(FractionWholePilot.Relation,d.Problem) with {StoryContextId=profile},[],null);
    }
}
