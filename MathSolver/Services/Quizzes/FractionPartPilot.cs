using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Only known whole → donated part. Other fraction relations keep their existing profiles.</summary>
internal static class FractionPartPilot
{
    internal const string Prefix = "fraction-part-open/";
    internal const string ListName = "FractionPartOpen.Contexts";
    internal sealed record Context(string Id, string Actor, string Unit, int Capacity,
        string Whole, string Share, string Question, string Lead);
    internal sealed record Data(Context Context, string Profile, int Whole, int Numerator, int Denominator)
    {
        internal int Answer => Whole / Denominator * Numerator;
        internal string Expression => $"{Whole}*{Numerator}/{Denominator}";
        internal Dictionary<string,string> Values => new()
        {
            ["f0"] = Whole.ToString(CultureInfo.InvariantCulture),
            ["f1"] = $"{Numerator}/{Denominator}", ["v0"] = Context.Actor, ["v1"] = Context.Unit
        };
        internal string Render(string text)
        {
            foreach(var (key,value) in Values) text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);
            return text;
        }
        internal string Problem => Render(Context.Whole+" "+Context.Share+" "+Context.Question);
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language) =>
        QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language) => Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!profile.StartsWith(Prefix,StringComparison.Ordinal)||!Enum.IsDefined(tier))
            throw new ArgumentException("InvalidFractionPartProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)
            ?? throw new ArgumentException("InvalidFractionPartContext");
        if(context.Capacity<20) throw new InvalidDataException("FractionPartCapacity");
        int denominator=random.Next(2,Math.Min(10,3+(int)tier)+1);
        int numerator=(int)tier==1?1:random.Next(1,denominator);
        int whole=denominator*random.Next(2,Math.Min(context.Capacity/denominator,4+(int)tier*3)+1);
        return new(context,profile,whole,numerator,denominator);
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateFractionPartPilot(ArithmeticQuizMode mode,AppLanguage language,
        CurriculumTier tier,string profile)
    {
        var data=FractionPartPilot.Create(profile,language,tier,_random);
        var builder=new DifficultyBuilder(QuizProblemKind.FractionSkills,ElementaryQuizType.FractionOfNumber,language,tier);
        builder.RequiresSolution=true;
        builder.Given("whole",data.Whole,data.Context.Unit);
        builder.Given("part-numerator",data.Numerator);
        builder.Given("part-denominator",data.Denominator);
        builder.Answer(data.Render(data.Context.Lead),data.Expression,data.Context.Unit);
        return CompleteQuestion(mode,builder.Build(profile,data.Problem) with {StoryContextId=profile},[],null);
    }
}
