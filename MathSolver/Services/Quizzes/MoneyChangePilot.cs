using MathSolver.Models;
using MathSolver.Services.QuestionBank;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>One product, its direct unit price and quantity, and cash actually tendered.</summary>
internal static class MoneyChangePilot
{
    internal const string Prefix="money-change-open/",ListName="MoneyChangeOpen.Contexts",Relation="one-product-unit-price-count-cash-find-change";
    internal const int Variant=(int)ElementaryQuizType.MultiStepRemaining;
    internal sealed record Context(string Id,string Relation,string PriceRole,string CountRole,string PaidRole,string TargetRole,
        string Buyer,string Item,string Items,string MoneyUnit,int MinimumPrice,int MaximumPrice,int MaximumCount,
        string PriceFact,string CountFact,string PaidFact,string Question,string CostLead,string ChangeLead);
    internal sealed record Data(Context Context,string Profile,int UnitPrice,int Count,int CashTendered)
    {
        internal int Cost=>UnitPrice*Count;
        internal int Answer=>CashTendered-Cost;
        internal string PriceUnit=>Context.MoneyUnit+"/"+Context.Item;
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=N(UnitPrice),["f1"]=N(Count),["f2"]=N(CashTendered),
            ["v0"]=Context.Buyer,["v1"]=Context.Item,["v2"]=Context.Items,["v3"]=Context.MoneyUnit
        };
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return FractionRemainingPilot.CapitalizeOpenings(text);}
        internal string Problem=>Render(string.Join(" ",Context.PriceFact,Context.CountFact,Context.PaidFact,Context.Question));
    }
    internal static string N(int value)=>value.ToString(CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static bool ValidateContract(BasicQuestionContract c)
    {
        if(c.Story?.NarrativeId.StartsWith(Prefix,StringComparison.Ordinal)!=true)return false;
        if(c.Family!=BankQuestionFamily.MultiStep||c.BankVariant!=Variant||!Enum.IsDefined(c.Tier))return false;
        try{_=Create(c.Story.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));return true;}
        catch(Exception e) when(e is ArgumentException or InvalidDataException or InvalidOperationException){return false;}
    }
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidMoneyChangeProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidMoneyChangeContext");
        if(context.Relation!=Relation||context.PriceRole!="unit-price"||context.CountRole!="purchase-count"
            ||context.PaidRole!="cash-tendered"||context.TargetRole!="change"
            ||context.MoneyUnit!=(language==AppLanguage.Vietnamese?"đồng":"dong")
            ||context.MinimumPrice<1000||context.MaximumPrice>50000||context.MaximumPrice<context.MinimumPrice
            ||context.MinimumPrice%1000!=0||context.MaximumPrice%1000!=0||context.MaximumCount is <2 or >8
            ||new[]{context.Buyer,context.Item,context.Items}.Any(string.IsNullOrWhiteSpace)
            ||!MoneyChangeOpenMeaning.ProvesReference(context,language))throw new InvalidDataException("InvalidMoneyChangeRolesOrBounds");
        int maxPrice=Math.Min(context.MaximumPrice,context.MinimumPrice+new[]{2000,4000,8000,12000,16000}[(int)tier-1]);
        int price=1000*random.Next(context.MinimumPrice/1000,maxPrice/1000+1);
        int count=random.Next(2,Math.Min(context.MaximumCount,(int)tier+3)+1);
        int cost=price*count;
        int[] notes=[10000,20000,50000,100000,200000,500000];
        int tender=notes.First(n=>n>cost);
        return new(context,profile,price,count,tender);
    }
    internal static bool IsValidModel(ElementaryQuizContract contract)
    {
        if(contract.StoryContextId?.StartsWith(Prefix,StringComparison.Ordinal)!=true||contract.Kind!=QuizProblemKind.MultiStep
            ||contract.Type!=ElementaryQuizType.MultiStepRemaining||!contract.RequiresSolution||!contract.RequiresCalculation
            ||contract.Visual is not null||contract.ProbabilityScenario is not null||contract.DataChart is not null||contract.AreaMeasurement is not null
            ||contract.Reasoning is not {Givens.Count:3,Steps.Count:2} reasoning||contract.Answers.Count!=1||!Enum.IsDefined(reasoning.Tier))return false;
        try
        {
            var context=Contexts(contract.Language).SingleOrDefault(x=>Prefix+x.Id==contract.StoryContextId);
            if(context is null||reasoning.ScenarioId!=Relation||reasoning.SupportingDiagram is not null
                ||reasoning.Explanation is not null)return false;
            // Reuse the context/bounds checks without depending on the originating random seed.
            _=Create(contract.StoryContextId,contract.Language,reasoning.Tier,new(1));
            var givens=reasoning.Givens.ToDictionary(g=>g.Role);
            if(!givens.TryGetValue("unit-price",out var p)||!givens.TryGetValue("purchase-count",out var n)||!givens.TryGetValue("cash-tendered",out var paid)
                ||!int.TryParse(p.Value,NumberStyles.None,CultureInfo.InvariantCulture,out int price)
                ||!int.TryParse(n.Value,NumberStyles.None,CultureInfo.InvariantCulture,out int count)
                ||!int.TryParse(paid.Value,NumberStyles.None,CultureInfo.InvariantCulture,out int cash))return false;
            int maxPrice=Math.Min(context.MaximumPrice,context.MinimumPrice+new[]{2000,4000,8000,12000,16000}[(int)reasoning.Tier-1]);
            if(price<context.MinimumPrice||price>maxPrice||price%1000!=0||count<2||count>Math.Min(context.MaximumCount,(int)reasoning.Tier+3))return false;
            var data=new Data(context,contract.StoryContextId,price,count,cash);
            int[] notes=[10000,20000,50000,100000,200000,500000];
            if(data.Answer<=0||cash!=notes.First(note=>note>data.Cost)||p.Unit!=data.PriceUnit||n.Unit!=context.Items||paid.Unit!=context.MoneyUnit
                ||!contract.Facts.SequenceEqual(new[]{p.Value,n.Value,paid.Value})||!contract.Constants.ToHashSet().SetEquals(new[]{"0","1"})
                ||!reasoning.IntermediateUnits.ToHashSet().SetEquals(new[]{data.PriceUnit,context.Items,context.MoneyUnit}))return false;
            string product=$"{price}*{count}",expression=$"{cash}-({product})";
            var answer=contract.Answers[0];
            return !answer.IsText&&answer.Value==new ReducedFraction(data.Answer,1)&&answer.Unit==context.MoneyUnit&&answer.Expression==expression
                &&reasoning.Steps[0].Expression==product&&reasoning.Steps[0].Value==new ReducedFraction(data.Cost,1)
                &&reasoning.Steps[0].DisplayValue==N(data.Cost)&&reasoning.Steps[1].Expression==expression
                &&reasoning.Steps[1].Value==answer.Value&&reasoning.Steps[1].DisplayValue==N(data.Answer)
                &&reasoning.Steps.All(s=>s.Unit==context.MoneyUnit);
        }
        catch(Exception e) when(e is ArgumentException or InvalidDataException or InvalidOperationException){return false;}
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenMoneyChange(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=MoneyChangePilot.Create(profile,language,tier,_random);
        var b=new DifficultyBuilder(QuizProblemKind.MultiStep,ElementaryQuizType.MultiStepRemaining,language,tier);
        string price=b.Given("unit-price",d.UnitPrice,d.PriceUnit),count=b.Given("purchase-count",d.Count,d.Context.Items),cash=b.Given("cash-tendered",d.CashTendered,d.Context.MoneyUnit);
        string cost=$"{price}*{count}";
        b.Step(d.Render(d.Context.CostLead),cost,d.Context.MoneyUnit);
        b.Answer(d.Render(d.Context.ChangeLead),$"{cash}-({cost})",d.Context.MoneyUnit);
        var contract=b.Build(MoneyChangePilot.Relation,d.Problem) with {StoryContextId=d.Profile};
        return CompleteQuestion(mode,contract,[],null);
    }
}
