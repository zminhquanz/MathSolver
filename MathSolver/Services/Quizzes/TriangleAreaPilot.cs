using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>One supplied base and its corresponding perpendicular height, both lengths.</summary>
internal static class TriangleAreaPilot
{
    internal const string Prefix="triangle-area-open/",ListName="TriangleAreaOpen.Contexts";
    internal const string Relation="triangle-direct-base-corresponding-perpendicular-height-find-area";
    internal static int Variant=>QuestionBank.ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Triangle,GeometryMeasurement.Area);
    internal sealed record Context(string Id,string Relation,string ShapeId,string BaseRole,string HeightRole,string TargetRole,
        string LengthUnitRole,string AreaUnitRole,string Object,string Shape,GeometryLengthUnit Unit,int MaximumLength,
        string ShapeFact,string BaseFact,string HeightFact,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Base,int Height)
    {
        internal BigInteger Area=>(BigInteger)Base*Height/2;
        internal string LengthUnit=>GeometryUnitFormatter.GetLengthSymbol(Context.Unit);
        internal string AreaUnit=>GeometryUnitFormatter.GetMeasurementSymbol(Context.Unit,2);
        internal string Expression=>$"({Base} × {Height}) ÷ 2";
        internal Dictionary<string,string> Values=>new(){["f0"]=Base.ToString(CultureInfo.InvariantCulture),["f1"]=Height.ToString(CultureInfo.InvariantCulture),
            ["v0"]=Context.Object,["v1"]=Context.Shape,["v2"]=LengthUnit,["v3"]=AreaUnit};
        internal string Render(string text)
        {
            foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);
            return FractionRemainingPilot.CapitalizeOpenings(text);
        }
        internal string Problem=>Render(Context.ShapeFact+" "+Context.BaseFact+" "+Context.HeightFact+" "+Context.Question);
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidTriangleAreaTier");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidTriangleAreaContext");
        if(context.Relation!=Relation||context.ShapeId!="triangle"||context.BaseRole!="base"||context.HeightRole!="corresponding-perpendicular-height"
            ||context.TargetRole!="area"||context.LengthUnitRole!="length-unit"||context.AreaUnitRole!="area-unit"
            ||!Enum.IsDefined(context.Unit)||context.MaximumLength is <4 or >1000
            ||context.Shape!=(language==AppLanguage.Vietnamese?"hình tam giác":"triangle"))throw new InvalidDataException("InvalidTriangleAreaRelation");
        int maximum=Math.Min(context.MaximumLength,new[]{10,20,30,50,100}[(int)tier-1]);
        int basis=random.Next(1,maximum/2+1)*2,height=random.Next(1,maximum+1);
        if(height==basis)height=height==maximum?height-1:height+1;
        return new(context,profile,basis,height);
    }
    internal static bool ValidContract(GeometryQuizContract g)
    {
        if(g.ShapeId!="triangle"||g.Measurement!=GeometryMeasurement.Area||g.Dimensions.Count!=2
            ||!g.Dimensions.TryGetValue("a",out var a)||!g.Dimensions.TryGetValue("h",out var h)||a<=0||h<=0||!(a*h).IsEven
            ||g.Reasoning is not {ScenarioId:"triangle-area-direct",Givens.Count:2,Steps.Count:0,HiddenDimensions.Count:0} r)return false;
        return r.Givens.Count(x=>x.Id=="a"&&x.Value==a&&x.Unit==g.LengthUnitSymbol)==1
            &&r.Givens.Count(x=>x.Id=="h"&&x.Value==h&&x.Unit==g.LengthUnitSymbol)==1
            &&g.CorrectAnswer==a*h/2&&g.Formula=="S = (a × h) ÷ 2"
            &&g.SubstitutionExpression==$"({a} × {h}) ÷ 2"&&r.CombinedExpression==g.SubstitutionExpression;
    }
}

public sealed partial class GeometryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenTriangleArea(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=TriangleAreaPilot.Create(profile,language,tier,_random);
        var dimensions=new Dictionary<string,BigInteger>{{"a",d.Base},{"h",d.Height}};
        // The general triangle calculator also requires the other two sides.
        // A direct area question must not invent those unprovided dimensions.
        var contract=new GeometryQuizContract("triangle",GeometryMeasurement.Area,dimensions,d.Context.Unit,d.Context.Object,d.Context.Shape,
            d.Area,"S = (a × h) ÷ 2",BuildSubstitutionExpression("triangle",GeometryMeasurement.Area,dimensions))
        {Reasoning=new(tier,"triangle-area-direct",language,
            [new("a",d.Base,d.LengthUnit,d.Render(d.Context.BaseFact)),new("h",d.Height,d.LengthUnit,d.Render(d.Context.HeightFact))],
            [TriangleAreaPilot.Relation],[],new HashSet<string>(),d.Problem,d.Problem,d.Expression)};
        if(!TriangleAreaPilot.ValidContract(contract))throw new InvalidOperationException("InvalidTriangleAreaFacts");
        return CreateQuestion(mode,contract) with {WordProblem=new(d.Problem,d.Render(d.Context.Lead),d.AreaUnit,d.Context.Object)};
    }
}
