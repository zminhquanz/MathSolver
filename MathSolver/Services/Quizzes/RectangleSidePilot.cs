using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Total perimeter and one known side; length is strictly greater than width.</summary>
internal static class RectangleSidePilot
{
    internal const string Prefix="rectangle-side-open/",ListName="RectangleSideOpen.Contexts";
    internal const string LengthRelation="rectangle-total-perimeter-known-width-find-length";
    internal const string WidthRelation="rectangle-total-perimeter-known-length-find-width";
    internal static int Variant=>(int)ElementaryQuizType.RectangleSide;
    internal sealed record Context(string Id,string Relation,string ShapeId,string PerimeterRole,string KnownRole,string TargetRole,
        string UnitRole,string Object,string Shape,GeometryLengthUnit Unit,int MaximumLength,string ShapeFact,string PerimeterFact,
        string KnownFact,string Question,string HalfLead,string Lead);
    internal sealed record Data(Context Context,string Profile,int Length,int Width)
    {
        internal int Perimeter=>2*(Length+Width);
        internal int Known=>Context.KnownRole=="width"?Width:Length;
        internal int Target=>Context.TargetRole=="length"?Length:Width;
        internal string Unit=>GeometryUnitFormatter.GetLengthSymbol(Context.Unit);
        internal string Expression=>$"{Perimeter}/2-{Known}";
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=Perimeter.ToString(CultureInfo.InvariantCulture),["f1"]=Known.ToString(CultureInfo.InvariantCulture),
            ["v0"]=Context.Object,["v1"]=Context.Shape,["v2"]=Unit
        };
        internal string Render(string text)
        {
            foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);
            return FractionRemainingPilot.CapitalizeOpenings(text);
        }
        internal string Problem=>Render(string.Join(" ",Context.ShapeFact,Context.PerimeterFact,Context.KnownFact,Context.Question));
    }
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static bool IsValidContext(Context x,AppLanguage language)
    {
        string target=x.Id.Split('/')[0];
        return x.Id is "length/garden" or "length/card" or "width/garden" or "width/card"
            &&x.TargetRole==target&&x.KnownRole==(target=="length"?"width":"length")
            &&x.Relation==(target=="length"?LengthRelation:WidthRelation)&&x.ShapeId=="rectangle"
            &&x.PerimeterRole=="total-perimeter"&&x.UnitRole=="common-length-unit"
            &&Enum.IsDefined(x.Unit)&&x.MaximumLength is >=3 and <=1000
            &&!string.IsNullOrWhiteSpace(x.Object)&&x.Shape==(language==AppLanguage.Vietnamese?"hình chữ nhật":"rectangle");
    }
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidRectangleSideTier");
        var context=Contexts(language).SingleOrDefault(x=>Prefix+x.Id==profile)??throw new ArgumentException("InvalidRectangleSideContext");
        if(!IsValidContext(context,language))throw new InvalidDataException("InvalidRectangleSideRelation");
        int maximum=Math.Min(context.MaximumLength,new[]{10,20,30,50,100}[(int)tier-1]);
        int width=random.Next(1,maximum);
        return new(context,profile,random.Next(width+1,maximum+1),width);
    }
    internal static bool IsValidModel(ElementaryQuizContract contract)
    {
        if(contract.StoryContextId?.StartsWith(Prefix,StringComparison.Ordinal)!=true)return false;
        if(contract.Kind!=QuizProblemKind.VisualGeometry||contract.Type!=ElementaryQuizType.RectangleSide
            ||contract.Reasoning is not {Givens.Count:2,Steps.Count:2} reasoning||contract.Answers.Count!=1||!contract.RequiresSolution)return false;
        try
        {
            var context=Contexts(contract.Language).SingleOrDefault(x=>Prefix+x.Id==contract.StoryContextId);
            if(context is null||!IsValidContext(context,contract.Language)||reasoning.ScenarioId!=context.Relation||!Enum.IsDefined(reasoning.Tier))return false;
            var givens=reasoning.Givens.ToDictionary(x=>x.Role);
            if(!givens.TryGetValue("rectangle-perimeter",out var p)||!givens.TryGetValue("rectangle-known-"+context.KnownRole,out var k)
                ||!int.TryParse(p.Value,NumberStyles.None,CultureInfo.InvariantCulture,out int perimeter)
                ||!int.TryParse(k.Value,NumberStyles.None,CultureInfo.InvariantCulture,out int known)
                ||perimeter<=0||perimeter%2!=0||known<=0)return false;
            int target=perimeter/2-known,length=context.TargetRole=="length"?target:known,width=context.TargetRole=="width"?target:known;
            if(width<=0||length<=width||length>Math.Min(context.MaximumLength,new[]{10,20,30,50,100}[(int)reasoning.Tier-1]))return false;
            string unit=GeometryUnitFormatter.GetLengthSymbol(context.Unit),expression=$"{perimeter}/2-{known}";
            var answer=contract.Answers[0];
            if(answer.IsText||answer.Value!=new ReducedFraction(target,1)||answer.Unit!=unit||answer.Expression!=expression
                ||!contract.Facts.SequenceEqual(new[]{p.Value,k.Value})||reasoning.Givens.Any(g=>g.Unit!=unit)
                ||!contract.Constants.ToHashSet().SetEquals(new[]{"0","1","2"})
                ||!reasoning.IntermediateUnits.ToHashSet().SetEquals(new[]{unit})||reasoning.Steps[0].Expression!=$"{perimeter}/2"
                ||reasoning.Steps[0].Value!=new ReducedFraction(perimeter/2,1)||reasoning.Steps[0].DisplayValue!=(perimeter/2).ToString(CultureInfo.InvariantCulture)
                ||reasoning.Steps[1].Expression!=expression||reasoning.Steps[1].Value!=answer.Value
                ||reasoning.Steps[1].DisplayValue!=target.ToString(CultureInfo.InvariantCulture)||reasoning.Steps.Any(s=>s.Unit!=unit))return false;
            var visual=contract.Visual;
            bool askLength=context.TargetRole=="length";
            return visual is {Kind:"missing-side",Polygons.Count:1,Annotations:not null}&&visual.Unit==unit&&visual.ScenarioId==context.Relation
                &&visual.Labels.Count==0&&visual.RotationDegrees==0&&visual.Lines is null
                &&visual.AccessibleDescription is null&&visual.HiddenValueIndices is null&&visual.PictographKey is null
                &&visual.Polygons[0].Label.Length==0&&!visual.Polygons[0].IsCutout
                &&visual.Polygons[0].Vertices.SequenceEqual(new QuizVisualPoint[]{new(0,0),new(6,0),new(6,4),new(0,4)})
                &&visual.Values.SequenceEqual(new decimal[]{perimeter,known})
                &&visual.Annotations.SequenceEqual(new QuizVisualAnnotation[]{new(askLength?"?":$"{known} {unit}",3,-.5m),
                    new(askLength?$"{known} {unit}":"?",6.8m,2),new($"P = {perimeter} {unit}",3,2)});
        }
        catch(ArgumentException){return false;}
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenRectangleSide(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=RectangleSidePilot.Create(profile,language,tier,_random);
        var b=new DifficultyBuilder(QuizProblemKind.VisualGeometry,ElementaryQuizType.RectangleSide,language,tier);
        b.Constant(2);
        string perimeter=b.Given("rectangle-perimeter",d.Perimeter,d.Unit),known=b.Given("rectangle-known-"+d.Context.KnownRole,d.Known,d.Unit);
        b.Step(d.Render(d.Context.HalfLead),$"{perimeter}/2",d.Unit);
        b.Answer(d.Render(d.Context.Lead),$"{perimeter}/2-{known}",d.Unit);
        bool askLength=d.Context.TargetRole=="length";
        var visual=new QuizVisualData("missing-side",[],[d.Perimeter,d.Known],d.Unit,
            Polygons:[ShapeRectangle(0,0,6,4)],Annotations:[new(askLength?"?":$"{d.Known} {d.Unit}",3,-.5m),
                new(askLength?$"{d.Known} {d.Unit}":"?",6.8m,2),new($"P = {d.Perimeter} {d.Unit}",3,2)],ScenarioId:d.Context.Relation);
        var contract=b.Build(d.Context.Relation,d.Problem,visual) with {StoryContextId=d.Profile};
        return CompleteQuestion(mode,contract,[],null);
    }
}
