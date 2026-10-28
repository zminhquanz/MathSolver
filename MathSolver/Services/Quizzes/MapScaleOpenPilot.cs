using MathSolver.Models;
using MathSolver.Services.QuestionBank;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>One map segment in cm, at scale one to n, to its actual length in metres.</summary>
internal static class MapScaleOpenPilot
{
    internal const string Prefix="map-scale-open/",ListName="MapScaleOpen.Contexts",Relation="map-cm-scale-one-to-denominator-find-actual-m";
    internal const int Variant=(int)ElementaryQuizType.MapScale;
    internal sealed record Context(string Id,string Relation,string ScaleRole,string MapLengthRole,string TargetRole,
        string Map,string Segment,string MapUnit,string ActualUnit,int MaximumScale,int MaximumMapLength,
        string ScaleFact,string LengthFact,string Question,string CentimetreLead,string ActualLead);
    internal sealed record Data(Context Context,string Profile,int Denominator,int MapLength)
    {
        internal int ActualCentimetres=>MapLength*Denominator;
        internal int Answer=>ActualCentimetres/100;
        internal Dictionary<string,string> Values=>new(){["f0"]=N(Denominator),["f1"]=N(MapLength),["v0"]=Context.Map,
            ["v1"]=Context.Segment,["v2"]=Context.MapUnit,["v3"]=Context.ActualUnit};
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return FractionRemainingPilot.CapitalizeOpenings(text);}
        internal string Problem=>Render(string.Join(" ",Context.ScaleFact,Context.LengthFact,Context.Question));
    }
    internal static string N(int n)=>n.ToString(CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(x=>Prefix+x.Id);
    internal static bool ValidateContract(BasicQuestionContract c)
    {
        if(c.Story?.NarrativeId.StartsWith(Prefix,StringComparison.Ordinal)!=true||c.Family!=BankQuestionFamily.Measurement||c.BankVariant!=Variant)return false;
        try{_=Create(c.Story.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));return true;}
        catch(Exception e) when(e is ArgumentException or InvalidDataException or InvalidOperationException){return false;}
    }
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!Enum.IsDefined(language))throw new ArgumentException("InvalidMapScaleOpenProfile");
        var context=Contexts(language).SingleOrDefault(x=>Prefix+x.Id==profile)??throw new ArgumentException("InvalidMapScaleOpenContext");
        if(context.Relation!=Relation||context.ScaleRole!="scale-denominator"||context.MapLengthRole!="map-cm"||context.TargetRole!="actual-m"
            ||context.MapUnit!="cm"||context.ActualUnit!="m"||context.MaximumScale is <100 or >100000||context.MaximumScale%100!=0
            ||context.MaximumMapLength is <2 or >20||string.IsNullOrWhiteSpace(context.Map)||string.IsNullOrWhiteSpace(context.Segment)
            ||!MapScaleOpenMeaning.ProvesReference(context,language))throw new InvalidDataException("InvalidMapScaleOpenRolesOrBounds");
        int maxScale=Math.Min(context.MaximumScale,new[]{500,2000,10000,20000,50000}[(int)tier-1]);
        return new(context,profile,random.Next(1,maxScale/100+1)*100,random.Next(1,Math.Min(context.MaximumMapLength,(int)tier+4)+1));
    }
    internal static bool IsValidModel(ElementaryQuizContract p)
    {
        if(p.StoryContextId?.StartsWith(Prefix,StringComparison.Ordinal)!=true||p.Kind!=QuizProblemKind.Measurement||p.Type!=ElementaryQuizType.MapScale
            ||!p.RequiresSolution||!p.RequiresCalculation||p.Visual is not null||p.ProbabilityScenario is not null||p.DataChart is not null
            ||p.AreaMeasurement is not null||p.RoundingDecimalPlaces is not null||p.Answers.Count!=1
            ||p.Reasoning is not {Givens.Count:2,Steps.Count:2} r||!Enum.IsDefined(r.Tier)||!Enum.IsDefined(p.Language))return false;
        try
        {
            _=Create(p.StoryContextId,p.Language,r.Tier,new(1));
            var context=Contexts(p.Language).Single(x=>Prefix+x.Id==p.StoryContextId);
            if(r.ScenarioId!=Relation||r.SupportingDiagram is not null||r.Explanation is not null)return false;
            var givens=r.Givens.ToDictionary(x=>x.Role);
            if(!givens.TryGetValue("scale-denominator",out var scale)||!givens.TryGetValue("map-cm",out var length)
                ||!int.TryParse(scale.Value,NumberStyles.None,CultureInfo.InvariantCulture,out int n)
                ||!int.TryParse(length.Value,NumberStyles.None,CultureInfo.InvariantCulture,out int cm))return false;
            int maxScale=Math.Min(context.MaximumScale,new[]{500,2000,10000,20000,50000}[(int)r.Tier-1]);
            if(n<100||n>maxScale||n%100!=0||cm<1||cm>Math.Min(context.MaximumMapLength,(int)r.Tier+4)
                ||scale.Unit!=""||length.Unit!="cm"||scale.Value!=N(n)||length.Value!=N(cm)
                ||!p.Facts.SequenceEqual(new[]{scale.Value,length.Value})||p.Constants.Count!=3||!p.Constants.ToHashSet().SetEquals(new[]{"0","1","100"})
                ||r.IntermediateUnits.Count!=2||!r.IntermediateUnits.ToHashSet().SetEquals(new[]{"cm","m"}))return false;
            var d=new Data(context,p.StoryContextId,n,cm);string product=$"{cm}*{n}",expression=product+"/100";var a=p.Answers[0];
            return !a.IsText&&a.Text is null&&a.Aliases is null&&!a.RequireReduced&&a.RequiredDenominator is null&&!a.RequireMixedNumber
                &&a.Value==new ReducedFraction(d.Answer,1)&&a.Unit=="m"&&a.Expression==expression&&a.DisplayValue==N(d.Answer)
                &&r.Steps[0].Expression==product&&r.Steps[0].Value==new ReducedFraction(d.ActualCentimetres,1)
                &&r.Steps[0].DisplayValue==N(d.ActualCentimetres)&&r.Steps[0].Unit=="cm"
                &&r.Steps[1].Expression==expression&&r.Steps[1].Value==a.Value&&r.Steps[1].DisplayValue==N(d.Answer)&&r.Steps[1].Unit=="m";
        }
        catch(Exception e) when(e is ArgumentException or InvalidDataException or InvalidOperationException){return false;}
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenMapScale(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=MapScaleOpenPilot.Create(profile,language,tier,_random);
        var b=new DifficultyBuilder(QuizProblemKind.Measurement,ElementaryQuizType.MapScale,language,tier);
        string scale=b.Given("scale-denominator",d.Denominator),cm=b.Given("map-cm",d.MapLength,"cm");b.Constant(100);
        b.Step(d.Render(d.Context.CentimetreLead),$"{cm}*{scale}","cm");
        b.Answer(d.Render(d.Context.ActualLead),$"{cm}*{scale}/100","m");
        var contract=b.Build(MapScaleOpenPilot.Relation,d.Problem) with {StoryContextId=d.Profile};
        if(!MapScaleOpenPilot.IsValidModel(contract))throw new InvalidOperationException("InvalidMapScaleOpenFacts");
        return CompleteQuestion(mode,contract,[],null);
    }
}
