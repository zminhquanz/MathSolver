using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>Direct area from supplied bases and corresponding perpendicular height only.</summary>
internal static class QuadrilateralAreaPilot
{
    internal const string ParallelogramPrefix="parallelogram-area-open/",TrapezoidPrefix="trapezoid-area-open/";
    internal const string ParallelogramListName="ParallelogramAreaOpen.Contexts",TrapezoidListName="TrapezoidAreaOpen.Contexts";
    internal const string ParallelogramRelation="parallelogram-direct-base-corresponding-perpendicular-height-find-area";
    internal const string TrapezoidRelation="trapezoid-direct-parallel-bases-corresponding-perpendicular-height-find-area";
    internal static int ParallelogramVariant=>QuestionBank.ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Parallelogram,GeometryMeasurement.Area);
    internal static int TrapezoidVariant=>QuestionBank.ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Trapezoid,GeometryMeasurement.Area);
    internal static bool IsVariant(int variant)=>variant==ParallelogramVariant||variant==TrapezoidVariant;
    internal static string Prefix(int variant)=>variant==ParallelogramVariant?ParallelogramPrefix:variant==TrapezoidVariant?TrapezoidPrefix:throw new ArgumentException("InvalidQuadrilateralVariant");
    internal static string ListName(int variant)=>variant==ParallelogramVariant?ParallelogramListName:variant==TrapezoidVariant?TrapezoidListName:throw new ArgumentException("InvalidQuadrilateralVariant");
    internal static string Relation(int variant)=>variant==ParallelogramVariant?ParallelogramRelation:variant==TrapezoidVariant?TrapezoidRelation:throw new ArgumentException("InvalidQuadrilateralVariant");
    internal static string ShapeId(int variant)=>variant==ParallelogramVariant?"parallelogram":variant==TrapezoidVariant?"trapezoid":throw new ArgumentException("InvalidQuadrilateralVariant");
    internal static string ShapeName(int variant,AppLanguage language)=>language==AppLanguage.Vietnamese
        ?variant==ParallelogramVariant?"hình bình hành":"hình thang":ShapeId(variant);
    internal sealed record Context(string Id,string Relation,string ShapeId,string BaseRole,string HeightRole,string TargetRole,
        string LengthUnitRole,string AreaUnitRole,string Object,string Shape,GeometryLengthUnit Unit,int MaximumLength,
        string ShapeFact,string BaseFact,string HeightFact,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Variant,int Base,int OtherBase,int Height)
    {
        internal bool IsTrapezoid=>Variant==TrapezoidVariant;
        internal BigInteger Area=>IsTrapezoid?((BigInteger)Base+OtherBase)*Height/2:(BigInteger)Base*Height;
        internal string LengthUnit=>GeometryUnitFormatter.GetLengthSymbol(Context.Unit);
        internal string AreaUnit=>GeometryUnitFormatter.GetMeasurementSymbol(Context.Unit,2);
        internal string Expression=>IsTrapezoid?$"(({Base} + {OtherBase}) × {Height}) ÷ 2":$"{Base} × {Height}";
        internal Dictionary<string,string> Values
        {
            get
            {
                var values=new Dictionary<string,string>{{"f0",Base.ToString(CultureInfo.InvariantCulture)},
                    {"f1",(IsTrapezoid?OtherBase:Height).ToString(CultureInfo.InvariantCulture)},
                    {"v0",Context.Object},{"v1",Context.Shape},{"v2",LengthUnit},{"v3",AreaUnit}};
                if(IsTrapezoid)values["f2"]=Height.ToString(CultureInfo.InvariantCulture);
                return values;
            }
        }
        internal string Render(string text)
        {
            foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);
            return FractionRemainingPilot.CapitalizeOpenings(text);
        }
        internal string Problem=>Render(Context.ShapeFact+" "+Context.BaseFact+" "+Context.HeightFact+" "+Context.Question);
    }
    internal static IReadOnlyList<Context> Contexts(int variant,AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName(variant),QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(int variant,AppLanguage language)=>IsVariant(variant)?Contexts(variant,language).Select(c=>Prefix(variant)+c.Id):[];
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidQuadrilateralAreaTier");
        int variant=profile.StartsWith(ParallelogramPrefix,StringComparison.Ordinal)?ParallelogramVariant:
            profile.StartsWith(TrapezoidPrefix,StringComparison.Ordinal)?TrapezoidVariant:throw new ArgumentException("InvalidQuadrilateralAreaProfile");
        var context=Contexts(variant,language).SingleOrDefault(c=>Prefix(variant)+c.Id==profile)??throw new ArgumentException("InvalidQuadrilateralAreaContext");
        if(context.Relation!=Relation(variant)||context.ShapeId!=ShapeId(variant)||context.BaseRole!=(variant==TrapezoidVariant?"parallel-bases":"base")
            ||context.HeightRole!="corresponding-perpendicular-height"||context.TargetRole!="area"||context.LengthUnitRole!="length-unit"||context.AreaUnitRole!="area-unit"
            ||!Enum.IsDefined(context.Unit)||context.MaximumLength is <4 or >1000||context.Shape!=ShapeName(variant,language))
            throw new InvalidDataException("InvalidQuadrilateralAreaRelation");
        int maximum=Math.Min(context.MaximumLength,new[]{10,20,30,50,100}[(int)tier-1]);
        int basis=random.Next(2,maximum+1),other=variant==TrapezoidVariant?random.Next(1,basis):0;
        int height=variant==TrapezoidVariant?random.Next(1,maximum/2+1)*2:random.Next(1,maximum+1);
        if(variant==ParallelogramVariant&&height==basis)height=height==maximum?height-1:height+1;
        return new(context,profile,variant,basis,other,height);
    }
    internal static bool ValidContract(GeometryQuizContract g)
    {
        bool trap=g.ShapeId=="trapezoid";
        if(g.ShapeId is not ("parallelogram" or "trapezoid")||g.Measurement!=GeometryMeasurement.Area||!Enum.IsDefined(g.LengthUnit)
            ||g.Dimensions.Count!=(trap?3:2)||!g.Dimensions.TryGetValue("a",out var a)||!g.Dimensions.TryGetValue("h",out var h)||a<=0||h<=0
            ||g.Reasoning is not {Steps.Count:0,HiddenDimensions.Count:0} r||!Enum.IsDefined(r.Tier)||!Enum.IsDefined(r.Language)
            ||r.ScenarioId!=(trap?"trapezoid-area-direct":"parallelogram-area-direct")||r.Givens.Count!=(trap?3:2)
            ||!r.Relations.SequenceEqual(new[]{trap?TrapezoidRelation:ParallelogramRelation}))return false;
        BigInteger b=0;
        if(trap&&(!g.Dimensions.TryGetValue("b",out b)||b<=0||a<=b||!((a+b)*h).IsEven))return false;
        foreach(var (id,value) in g.Dimensions)
            if(r.Givens.Count(x=>x.Id==id&&x.Value==value&&x.Unit==g.LengthUnitSymbol)!=1)return false;
        string expression=trap?$"(({a} + {b}) × {h}) ÷ 2":$"{a} × {h}";
        return g.ShapeName==ShapeName(trap?TrapezoidVariant:ParallelogramVariant,r.Language)
            &&g.CorrectAnswer==(trap?(a+b)*h/2:a*h)&&g.Formula==(trap?"S = ((a + b) × h) ÷ 2":"S = a × h")
            &&g.SubstitutionExpression==expression&&r.CombinedExpression==expression;
    }
}

public sealed partial class GeometryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenQuadrilateralArea(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=QuadrilateralAreaPilot.Create(profile,language,tier,_random);
        var dimensions=new Dictionary<string,BigInteger>{{"a",d.Base},{"h",d.Height}};
        var givens=new List<GeometryGivenFact>{new("a",d.Base,d.LengthUnit,d.Render(d.Context.BaseFact))};
        if(d.IsTrapezoid){dimensions["b"]=d.OtherBase;givens.Add(new("b",d.OtherBase,d.LengthUnit,d.Render(d.Context.BaseFact)));}
        givens.Add(new("h",d.Height,d.LengthUnit,d.Render(d.Context.HeightFact)));
        var contract=new GeometryQuizContract(d.Context.ShapeId,GeometryMeasurement.Area,dimensions,d.Context.Unit,d.Context.Object,d.Context.Shape,
            d.Area,d.IsTrapezoid?"S = ((a + b) × h) ÷ 2":"S = a × h",d.Expression)
        {Reasoning=new(tier,d.IsTrapezoid?"trapezoid-area-direct":"parallelogram-area-direct",language,givens,
            [QuadrilateralAreaPilot.Relation(d.Variant)],[],new HashSet<string>(),d.Problem,d.Problem,d.Expression)};
        if(!QuadrilateralAreaPilot.ValidContract(contract))throw new InvalidOperationException("InvalidQuadrilateralAreaFacts");
        return CreateQuestion(mode,contract) with {WordProblem=new(d.Problem,d.Render(d.Context.Lead),d.AreaUnit,d.Context.Object)};
    }
}
