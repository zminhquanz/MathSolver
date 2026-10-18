using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>Two directly known sides, one rectangle, one perimeter target.</summary>
internal static class RectanglePerimeterPilot
{
    internal const string Prefix="rectangle-perimeter-open/", ListName="RectanglePerimeterOpen.Contexts";
    internal const string Relation="rectangle-direct-length-width-find-perimeter";
    internal static int Variant=>QuestionBank.ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Rectangle,GeometryMeasurement.Perimeter);
    internal sealed record Context(string Id,string Relation,string ShapeId,string LengthRole,string WidthRole,string TargetRole,
        string Object,string Shape,GeometryLengthUnit Unit,int MaximumLength,string ShapeFact,string LengthFact,string WidthFact,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Length,int Width)
    {
        internal int Perimeter=>2*(Length+Width);
        internal string Unit=>GeometryUnitFormatter.GetLengthSymbol(Context.Unit);
        internal string Expression=>$"({Length} + {Width}) × 2";
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=Length.ToString(CultureInfo.InvariantCulture),["f1"]=Width.ToString(CultureInfo.InvariantCulture),
            ["v0"]=Context.Object,["v1"]=Context.Shape,["v2"]=Unit
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
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidRectanglePerimeterTier");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidRectanglePerimeterContext");
        if(context.Relation!=Relation||context.ShapeId!="rectangle"||context.LengthRole!="length"||context.WidthRole!="width"
            ||context.TargetRole!="perimeter"||!Enum.IsDefined(context.Unit)||context.MaximumLength<3
            ||context.Shape!=(language==AppLanguage.Vietnamese?"hình chữ nhật":"rectangle"))
            throw new InvalidDataException("InvalidRectanglePerimeterRelation");
        int maximum=Math.Min(context.MaximumLength,new[]{10,20,30,50,100}[(int)tier-1]);
        int width=random.Next(1,maximum);
        return new(context,profile,random.Next(width+1,maximum+1),width);
    }
}

public sealed partial class GeometryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenRectanglePerimeter(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=RectanglePerimeterPilot.Create(profile,language,tier,_random);
        var dimensions=new Dictionary<string,BigInteger>{{"a",d.Length},{"b",d.Width}};
        var (answer,formula)=ResolveExactQuizAnswer("rectangle",GeometryMeasurement.Perimeter,dimensions);
        var contract=new GeometryQuizContract("rectangle",GeometryMeasurement.Perimeter,dimensions,d.Context.Unit,
            d.Context.Object,d.Context.Shape,answer,formula,BuildSubstitutionExpression("rectangle",GeometryMeasurement.Perimeter,dimensions))
        {
            Reasoning=new(tier,"rectangle-perimeter-direct",language,
                [new("a",d.Length,d.Unit,d.Render(d.Context.LengthFact)),new("b",d.Width,d.Unit,d.Render(d.Context.WidthFact))],
                [],[],new HashSet<string>(),d.Problem,d.Problem,d.Expression)
        };
        return CreateQuestion(mode,contract) with {WordProblem=new(d.Problem,d.Render(d.Context.Lead),d.Unit,d.Context.Object)};
    }
}
