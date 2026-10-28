using MathSolver.Models;
using MathSolver.Services.QuestionBank;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>One original product price and one discount of that original price.</summary>
internal static class DiscountOpenPilot
{
    internal const string Prefix="discount-open/",ListName="DiscountOpen.Contexts",Relation="original-price-discount-percentage-find-payable-price";
    internal const int Variant=(int)ElementaryQuizType.MultiStepRemaining;
    internal sealed record Context(string Id,string Relation,string PriceRole,string RateRole,string TargetRole,
        string Product,string MoneyUnit,int MinimumPrice,int MaximumPrice,string PriceFact,string DiscountFact,
        string Question,string DiscountLead,string PayableLead);
    internal sealed record Data(Context Context,string Profile,int OriginalPrice,int Percentage)
    {
        internal int Discount=>OriginalPrice*Percentage/100;
        internal int Answer=>OriginalPrice-Discount;
        internal Dictionary<string,string> Values=>new()
        { ["f0"]=N(OriginalPrice),["f1"]=N(Percentage)+"%",["v0"]=Context.Product,["v1"]=Context.MoneyUnit };
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return FractionRemainingPilot.CapitalizeOpenings(text);}
        internal string Problem=>Render(string.Join(" ",Context.PriceFact,Context.DiscountFact,Context.Question));
    }
    internal static string N(int value)=>value.ToString(CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static bool ValidateContract(BasicQuestionContract c)
    {
        if(c.Family!=BankQuestionFamily.MultiStep||c.BankVariant!=Variant||c.Story?.NarrativeId.StartsWith(Prefix,StringComparison.Ordinal)!=true)return false;
        try{_=Create(c.Story.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));return true;}
        catch(Exception e) when(e is ArgumentException or InvalidDataException or InvalidOperationException){return false;}
    }
    private static int Maximum(Context context,CurriculumTier tier)=>Math.Min(context.MaximumPrice,context.MinimumPrice+new[]{4000,12000,24000,48000,96000}[(int)tier-1]);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!Enum.IsDefined(language)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidDiscountProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidDiscountContext");
        if(context.Relation!=Relation||context.PriceRole!="original-price"||context.RateRole!="discount-percentage-of-original"
            ||context.TargetRole!="payable-price"||context.MoneyUnit!=(language==AppLanguage.Vietnamese?"đồng":"dong")
            ||string.IsNullOrWhiteSpace(context.Product)||context.MinimumPrice<1000||context.MaximumPrice>500000
            ||context.MinimumPrice>context.MaximumPrice||context.MinimumPrice%1000!=0||context.MaximumPrice%1000!=0
            ||!DiscountOpenMeaning.ProvesReference(context,language))throw new InvalidDataException("InvalidDiscountRolesOrBounds");
        int[] rates=[10,20,25,50,75];
        return new(context,profile,1000*random.Next(context.MinimumPrice/1000,Maximum(context,tier)/1000+1),rates[random.Next(rates.Length)]);
    }
    internal static bool IsValidModel(ElementaryQuizContract contract)
    {
        if(contract.StoryContextId?.StartsWith(Prefix,StringComparison.Ordinal)!=true||contract.Kind!=QuizProblemKind.MultiStep
            ||contract.Type!=ElementaryQuizType.MultiStepRemaining||!contract.RequiresSolution||!contract.RequiresCalculation
            ||contract.Visual is not null||contract.ProbabilityScenario is not null||contract.DataChart is not null||contract.AreaMeasurement is not null
            ||contract.RoundingDecimalPlaces is not null
            ||contract.Reasoning is not {Givens.Count:2,Steps.Count:2} reasoning||contract.Answers.Count!=1||!Enum.IsDefined(reasoning.Tier))return false;
        try
        {
            _=Create(contract.StoryContextId,contract.Language,reasoning.Tier,new(1));
            var context=Contexts(contract.Language).Single(x=>Prefix+x.Id==contract.StoryContextId);
            if(reasoning.ScenarioId!=Relation||reasoning.SupportingDiagram is not null||reasoning.Explanation is not null)return false;
            var givens=reasoning.Givens.ToDictionary(g=>g.Role);
            if(!givens.TryGetValue("original-price",out var price)||!givens.TryGetValue("discount-percentage-of-original",out var rate)
                ||!int.TryParse(price.Value,NumberStyles.None,CultureInfo.InvariantCulture,out int original)
                ||!int.TryParse(rate.Value,NumberStyles.None,CultureInfo.InvariantCulture,out int percentage)
                ||original<context.MinimumPrice||original>Maximum(context,reasoning.Tier)||original%1000!=0
                ||percentage is not (10 or 20 or 25 or 50 or 75)||price.Unit!=context.MoneyUnit||rate.Unit!="%"
                ||!contract.Facts.SequenceEqual(new[]{price.Value,rate.Value})||!contract.Constants.ToHashSet().SetEquals(new[]{"0","1","100"})
                ||!reasoning.IntermediateUnits.ToHashSet().SetEquals(new[]{context.MoneyUnit,"%"}))return false;
            var data=new Data(context,contract.StoryContextId,original,percentage);
            string reduction=$"{original}*{percentage}/100",expression=$"{original}-({reduction})";
            var answer=contract.Answers[0];
            return data.Discount>0&&data.Answer>0&&!answer.IsText&&answer.Text is null&&answer.Aliases is null&&answer.DisplayValue is null
                &&!answer.RequireReduced&&answer.RequiredDenominator is null&&!answer.RequireMixedNumber&&answer.Value==new ReducedFraction(data.Answer,1)
                &&answer.Unit==context.MoneyUnit&&answer.Expression==expression
                &&reasoning.Steps[0].Expression==reduction&&reasoning.Steps[0].Value==new ReducedFraction(data.Discount,1)
                &&reasoning.Steps[0].DisplayValue==N(data.Discount)&&reasoning.Steps[1].Expression==expression
                &&reasoning.Steps[1].Value==answer.Value&&reasoning.Steps[1].DisplayValue==N(data.Answer)
                &&reasoning.Steps.All(s=>s.Unit==context.MoneyUnit);
        }
        catch(Exception e) when(e is ArgumentException or InvalidDataException or InvalidOperationException){return false;}
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenDiscount(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=DiscountOpenPilot.Create(profile,language,tier,_random);
        var b=new DifficultyBuilder(QuizProblemKind.MultiStep,ElementaryQuizType.MultiStepRemaining,language,tier);
        string price=b.Given("original-price",d.OriginalPrice,d.Context.MoneyUnit),rate=b.Given("discount-percentage-of-original",d.Percentage,"%");
        b.Constant(100);
        string discount=$"{price}*{rate}/100";
        b.Step(d.Render(d.Context.DiscountLead),discount,d.Context.MoneyUnit);
        b.Answer(d.Render(d.Context.PayableLead),$"{price}-({discount})",d.Context.MoneyUnit);
        var contract=b.Build(DiscountOpenPilot.Relation,d.Problem) with {StoryContextId=d.Profile};
        return CompleteQuestion(mode,contract,[],null);
    }
}
