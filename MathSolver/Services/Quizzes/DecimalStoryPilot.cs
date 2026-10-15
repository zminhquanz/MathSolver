using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>One-step decimal facts. Wording is reference data, not the AI's sentence universe.</summary>
internal static class DecimalStoryPilot
{
    internal const string Prefix = "decimal-open/";
    internal const string ContextList = "DecimalOpen.Contexts", RelationList = "DecimalOpen.Relations";
    internal static string RelationSignature(string kind)=>kind switch
    {
        "combine"=>"DecimalAdd|separate-first-stock|separate-second-stock|combined-stock",
        "increase"=>"DecimalAdd|initial-stock|added-stock|final-stock",
        "remaining"=>"DecimalSubtract|initial-stock|removed-stock|remaining-stock",
        "groups"=>"DecimalMultiply|amount-per-group|group-count|total-amount",
        "price"=>"DecimalMultiply|unit-price|purchase-count|total-cost",
        "share"=>"DecimalDivide|total-amount|group-count|amount-per-group",
        "count"=>"DecimalDivide|total-amount|amount-per-group|group-count",
        _=>throw new InvalidDataException("Unknown decimal relation.")
    };
    internal sealed record Context(string Id, string Actor, string Other, string Item, string Unit, string Group,
        decimal MaximumPerGroup, decimal MaximumTotal, bool Money = false);
    internal sealed record Relation(string Id, ElementaryQuizType Type, string FirstRole, string SecondRole,
        string TargetRole, bool MoneyOnly, bool GoodsOnly, string[] Facts, string Question, string Lead,
        string[] AlternativeFacts, string AlternativeQuestion)
    { internal string Kind=>Id.EndsWith("-money",StringComparison.Ordinal)?Id[..^6]:Id; }
    internal sealed record Data(Context Context, Relation Relation, string Profile, decimal First, decimal Second,
        string FirstUnit, string SecondUnit, string AnswerUnit, string Group)
    {
        internal string Expression => N(First) + (Relation.Type switch { ElementaryQuizType.DecimalAdd => "+",
            ElementaryQuizType.DecimalSubtract => "-", ElementaryQuizType.DecimalMultiply => "*", _ => "/" }) + N(Second);
        internal Dictionary<string,string> Values => new() { ["f0"]=N(First),["f1"]=N(Second),["v0"]=AnswerUnit,
            ["v1"]=Context.Actor,["v2"]=Context.Other,["v3"]=Context.Item,["v4"]=FirstUnit,["v5"]=SecondUnit,["v6"]=Group };
        internal string Render(string text)
        { foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return text; }
        internal string Problem => Render(string.Join(" ",Relation.Facts.Append(Relation.Question)));
    }
    internal static string N(decimal number)=>number.ToString("0.################",CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ContextList,QuizContentCatalog.Culture(language));
    internal static IReadOnlyList<Relation> Relations(AppLanguage language)=>QuizContentCatalog.LoadList<Relation>(RelationList,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(ElementaryQuizType type,AppLanguage language) =>
        from relation in Relations(language) where relation.Type==type
        from context in Contexts(language) where Eligible(relation,context)
        select Prefix+relation.Id+"/"+context.Id;
    private static bool Eligible(Relation r,Context c)=>r.MoneyOnly==c.Money&&(!r.GoodsOnly||!c.Money);

    internal static Data Create(string profile,ElementaryQuizType type,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!profile.StartsWith(Prefix,StringComparison.Ordinal)||!Enum.IsDefined(tier)||!Enum.IsDefined(language))
            throw new ArgumentException("InvalidDecimalOpenProfile");
        var parts=profile[Prefix.Length..].Split('/');
        var relation=parts.Length==2?Relations(language).SingleOrDefault(r=>r.Id==parts[0]&&r.Type==type):null;
        var context=parts.Length==2?Contexts(language).SingleOrDefault(c=>c.Id==parts[1]):null;
        if(relation is null||context is null||!Eligible(relation,context))throw new ArgumentException("InvalidDecimalOpenProfile");
        int scale=(int)tier<=2?10:(int)tier<=4?100:1000;
        decimal amount=random.Next(Math.Max(1,(int)(context.MaximumPerGroup*scale/3)),(int)(context.MaximumPerGroup*scale)+1)/(decimal)scale;
        if(amount==decimal.Truncate(amount))amount-=1m/scale;
        int count=random.Next(2,Math.Min(20,3+(int)tier*2)+1);
        decimal first,second;string a=context.Unit,b=context.Unit,result=context.Unit;
        string group=context.Money&&relation.Kind=="share"?(language==AppLanguage.Vietnamese?"phong bì":"envelopes"):context.Group;
        switch(relation.Kind)
        {
            case "combine": case "increase":
                first=amount;second=decimal.Truncate(amount*scale/2m)/scale+1m/scale;break;
            case "remaining": first=amount+1m;second=decimal.Truncate(amount*scale/2m)/scale;break;
            case "groups": case "price": first=amount;second=count;b=group;break;
            case "share": first=amount*count;second=count;b=group;break;
            case "count": first=amount*count;second=amount;result=group;break;
            default:throw new ArgumentException("InvalidDecimalOpenRelation");
        }
        if(first<=0||second<=0||first>context.MaximumTotal||second>context.MaximumTotal)
            throw new InvalidDataException("DecimalOpenCapacityExceeded");
        return new(context,relation,profile,first,second,a,b,result,group);
    }
}
