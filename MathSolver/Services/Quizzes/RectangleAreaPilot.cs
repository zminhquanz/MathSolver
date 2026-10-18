using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>Two direct integer sides with distinct length and area unit roles.</summary>
internal static class RectangleAreaPilot
{
    internal const string Prefix="rectangle-area-open/", ListName="RectangleAreaOpen.Contexts";
    internal const string Relation="rectangle-direct-length-width-find-area";
    internal static int Variant=>QuestionBank.ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Rectangle,GeometryMeasurement.Area);
    internal sealed record Context(string Id,string Relation,string ShapeId,string LengthRole,string WidthRole,string TargetRole,
        string LengthUnitRole,string AreaUnitRole,string Object,string Shape,GeometryLengthUnit Unit,int MaximumLength,
        string ShapeFact,string LengthFact,string WidthFact,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Length,int Width)
    {
        internal int Area=>Length*Width;
        internal string LengthUnit=>GeometryUnitFormatter.GetLengthSymbol(Context.Unit);
        internal string AreaUnit=>GeometryUnitFormatter.GetMeasurementSymbol(Context.Unit,2);
        internal string Expression=>$"{Length} × {Width}";
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=Length.ToString(CultureInfo.InvariantCulture),["f1"]=Width.ToString(CultureInfo.InvariantCulture),
            ["v0"]=Context.Object,["v1"]=Context.Shape,["v2"]=LengthUnit,["v3"]=AreaUnit
        };
        internal string Render(string text)
        {
            foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);
            return FractionRemainingPilot.CapitalizeOpenings(text);
        }
        internal string Problem=>Render(Context.ShapeFact+" "+Context.LengthFact+" "+Context.WidthFact+" "+Context.Question);
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidRectangleAreaTier");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidRectangleAreaContext");
        if(context.Relation!=Relation||context.ShapeId!="rectangle"||context.LengthRole!="length"||context.WidthRole!="width"
            ||context.TargetRole!="area"||context.LengthUnitRole!="length-unit"||context.AreaUnitRole!="area-unit"
            ||!Enum.IsDefined(context.Unit)||context.MaximumLength is <3 or >1000
            ||context.Shape!=(language==AppLanguage.Vietnamese?"hình chữ nhật":"rectangle"))
            throw new InvalidDataException("InvalidRectangleAreaRelation");
        int maximum=Math.Min(context.MaximumLength,new[]{10,20,30,50,100}[(int)tier-1]);
        int width=random.Next(1,maximum);
        return new(context,profile,random.Next(width+1,maximum+1),width);
    }
}

public sealed partial class GeometryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenRectangleArea(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=RectangleAreaPilot.Create(profile,language,tier,_random);
        var dimensions=new Dictionary<string,BigInteger>{{"a",d.Length},{"b",d.Width}};
        var (answer,formula)=ResolveExactQuizAnswer("rectangle",GeometryMeasurement.Area,dimensions);
        var contract=new GeometryQuizContract("rectangle",GeometryMeasurement.Area,dimensions,d.Context.Unit,
            d.Context.Object,d.Context.Shape,answer,formula,BuildSubstitutionExpression("rectangle",GeometryMeasurement.Area,dimensions))
        {
            Reasoning=new(tier,"rectangle-area-direct",language,
                [new("a",d.Length,d.LengthUnit,d.Render(d.Context.LengthFact)),new("b",d.Width,d.LengthUnit,d.Render(d.Context.WidthFact))],
                [],[],new HashSet<string>(),d.Problem,d.Problem,d.Expression)
        };
        return CreateQuestion(mode,contract) with {WordProblem=new(d.Problem,d.Render(d.Context.Lead),d.AreaUnit,d.Context.Object)};
    }
}
