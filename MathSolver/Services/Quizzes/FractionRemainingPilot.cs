using MathSolver.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>One transfer from a known whole; the question always asks the donor's remainder.</summary>
internal static class FractionRemainingPilot
{
    internal const string Prefix = "fraction-remaining-open/";
    internal const string ListName = "FractionRemainingOpen.Contexts";
    internal const string Relation = "known-whole-donated-find-remaining";
    internal static string CapitalizeOpenings(string text) => Regex.Replace(text,@"(^\s*|[.!?]\s+)(\p{Ll})",
        m=>m.Groups[1].Value+m.Groups[2].Value.ToUpperInvariant(),RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal sealed record Context(string Id, string Relation, string Donor, string Recipient,
        string Action, string Unit, int Capacity, string Whole, string Share, string Question, string Lead);
    internal sealed record Data(Context Context, string Profile, int Whole, int Numerator, int Denominator)
    {
        internal int Donated => Whole / Denominator * Numerator;
        internal int Answer => Whole - Donated;
        internal string Expression => $"{Whole}-{Whole}*{Numerator}/{Denominator}";
        internal Dictionary<string,string> Values => new()
        {
            ["f0"] = Whole.ToString(CultureInfo.InvariantCulture),
            ["f1"] = $"{Numerator}/{Denominator}",
            ["v0"] = Context.Donor, ["v1"] = Context.Unit, ["v2"] = Context.Recipient
        };
        internal string Render(string text)
        {
            foreach(var (key,value) in Values) text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);
            return CapitalizeOpenings(text);
        }
        internal string Problem => Render(Context.Whole+" "+Context.Share+" "+Context.Question);
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language) =>
        QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language) => Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidFractionRemainingTier");
        var contexts=Contexts(language);
        var context=profile.Length==0 ? contexts[random.Next(contexts.Count)]
            : contexts.SingleOrDefault(c=>Prefix+c.Id==profile) ?? throw new ArgumentException("InvalidFractionRemainingProfile");
        if(context.Relation!=Relation||context.Action!="donate"||context.Donor==context.Recipient)
            throw new InvalidDataException("InvalidFractionRemainingRelation");
        int denominator=random.Next(2,Math.Min(10,3+(int)tier)+1);
        int numerator=(int)tier==1?1:random.Next(1,denominator);
        int whole=denominator*random.Next(2,Math.Min(context.Capacity/denominator,4+(int)tier*3)+1);
        return new(context,Prefix+context.Id,whole,numerator,denominator);
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateFractionRemaining(ArithmeticQuizMode mode,AppLanguage language,
        CurriculumTier tier,string profile) => CompleteQuestion(mode,CreateFractionRemaining(language,tier,profile),[],null);

    private ElementaryQuizContract CreateFractionRemaining(AppLanguage language,CurriculumTier tier,string profile="")
    {
        var d=FractionRemainingPilot.Create(profile,language,tier,_random);
        var b=new DifficultyBuilder(QuizProblemKind.FractionSkills,ElementaryQuizType.FractionRemaining,language,tier);
        b.RequiresSolution=true;
        b.Given("whole",d.Whole,d.Context.Unit);
        b.Given("donated-numerator",d.Numerator);
        b.Given("donated-denominator",d.Denominator);
        b.Answer(d.Render(d.Context.Lead),d.Expression,d.Context.Unit);
        return b.Build(FractionRemainingPilot.Relation,d.Problem) with {StoryContextId=d.Profile};
    }
}
