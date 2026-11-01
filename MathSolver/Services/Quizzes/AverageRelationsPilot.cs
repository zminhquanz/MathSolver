using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Finite member-count relations; a mean never implies equal observations.</summary>
internal static class AverageRelationsPilot
{
    internal const string Prefix="average-relations-open/";
    internal sealed record Context(string Id,string GroupA,string GroupB,string Member,string Members,string Unit,string Verb,string BaseVerb);
    // Typed bilingual lists intentionally isolate reviewed bindings from model-written prose.
    private static readonly Context[] English=
    [
        new("reading","the Oak reading group","the Pine reading group","pupil","pupils","pages","read","read"),
        new("collecting","the River team","the Hill team","volunteer","volunteers","bottles","collected","collect")
    ];
    private static readonly Context[] Vietnamese=
    [
        new("reading","nhóm đọc sách Hoa Mai","nhóm đọc sách Hoa Lan","học sinh","học sinh","trang sách","đọc","đọc"),
        new("collecting","đội Hoa Sen","đội Hoa Hồng","tình nguyện viên","tình nguyện viên","chai nhựa","thu gom được","thu gom")
    ];
    internal static bool Supports(int variant)=>(AverageQuizType)variant is AverageQuizType.AverageToTotal or AverageQuizType.TotalToAverage or AverageQuizType.TwoGroups;
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>language switch
    {AppLanguage.English=>English,AppLanguage.Vietnamese=>Vietnamese,_=>throw new ArgumentException("InvalidAverageRelationsLanguage")};
    private static string Relation(int variant)=>(AverageQuizType)variant switch
    {AverageQuizType.AverageToTotal=>"mean-total",AverageQuizType.TotalToAverage=>"total-mean",AverageQuizType.TwoGroups=>"pooled",_=>throw new ArgumentException("InvalidAverageRelationsVariant")};
    internal static IEnumerable<string> Profiles(AppLanguage language,int variant)=>Contexts(language).Select(c=>Prefix+Relation(variant)+"-"+c.Id);
    internal sealed record Data(Context Context,string Profile,AverageQuizType Type,AppLanguage Language,int CountA,int MeanA,int CountB,int MeanB)
    {
        internal bool Pooled=>Type==AverageQuizType.TwoGroups;
        internal int Total=>checked(CountA*MeanA+CountB*MeanB);
        internal int Count=>CountA+CountB;
        internal int Answer=>Type==AverageQuizType.AverageToTotal?Total:Total/Count;
        internal int[] Facts=>Pooled?[CountA,MeanA,CountB,MeanB]:[CountA,Type==AverageQuizType.AverageToTotal?MeanA:Total];
        internal string Expression=>Pooled?$"({CountA} × {MeanA} + {CountB} × {MeanB}) ÷ ({CountA} + {CountB})":Type==AverageQuizType.AverageToTotal?$"{MeanA} × {CountA}":$"{Total} ÷ {CountA}";
        internal Dictionary<string,string> Values=>Pooled?new()
        {
            ["f0"]=N(CountA),["f1"]=N(MeanA),["f2"]=N(CountB),["f3"]=N(MeanB),
            ["v0"]=Context.GroupA,["v1"]=Context.GroupB,["v2"]=Context.Member,["v3"]=Context.Unit,
            ["v4"]=Context.Verb,["v5"]=Context.BaseVerb,["v6"]=Context.Members
        }:new()
        {
            ["f0"]=N(Type==AverageQuizType.AverageToTotal?MeanA:Total),["f1"]=N(CountA),
            ["v0"]=Context.GroupA,["v1"]=Context.Member,["v2"]=Context.Members,["v3"]=Context.Unit,["v4"]=Context.Verb,["v5"]=Context.BaseVerb
        };
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return text;}
        internal NarrativeText Text=>new(Language==AppLanguage.Vietnamese,Pooled,Type==AverageQuizType.AverageToTotal);
        internal string Problem=>Render(string.Join(" ",Text.Facts.Append(Text.Question)));
    }
    internal sealed record NarrativeText(bool Vi,bool Pooled,bool FindTotal)
    {
        internal string[] Facts=>Pooled?(Vi?
            ["Có {f0} {v6} trong {v0}.","Trung bình mỗi {v2} trong {v0} {v4} {f1} {v3}.","Có {f2} {v6} trong {v1}.","Trung bình mỗi {v2} trong {v1} {v4} {f3} {v3}.","Các {v6} trong {v0} và {v1} không trùng nhau."]:
            ["There are {f0} {v6} in {v0}.","On average, each {v2} in {v0} {v4} {f1} {v3}.","There are {f2} {v6} in {v1}.","On average, each {v2} in {v1} {v4} {f3} {v3}.","The {v6} in {v0} and {v1} are distinct."]):
            [Vi?"Có {f1} {v2} trong {v0}.":"There are {f1} {v2} in {v0}.",FindTotal?
                (Vi?"Trung bình mỗi {v1} trong {v0} {v4} {f0} {v3}.":"On average, each {v1} in {v0} {v4} {f0} {v3}."):
                (Vi?"Tổng số {v3} do tất cả các {v1} trong {v0} {v4} là {f0}.":"Across every {v1} in {v0}, the total number of {v3} {v4} was {f0}.")];
        internal string Question=>Pooled?(Vi?"Trung bình mỗi {v2} của cả {v0} và {v1} {v5} bao nhiêu {v3}?":"How many {v3} did each {v2} across {v0} and {v1} {v5} on average?"):
            FindTotal?(Vi?"Tất cả các {v2} trong {v0} {v5} tổng cộng bao nhiêu {v3}?":"How many {v3} did all the {v2} in {v0} {v5} in total?"):
            (Vi?"Trung bình mỗi {v1} trong {v0} {v5} bao nhiêu {v3}?":"How many {v3} did each {v1} in {v0} {v5} on average?");
        internal string Lead=>Pooled?(Vi?"Số {v3} trung bình mỗi {v2} của cả {v0} và {v1}":"The average number of {v3} per {v2} across {v0} and {v1}"):
            FindTotal?(Vi?"Tổng số {v3} của tất cả các {v2} trong {v0}":"The total number of {v3} for all the {v2} in {v0}"):
            (Vi?"Số {v3} trung bình mỗi {v1} trong {v0}":"The average number of {v3} per {v1} in {v0}");
    }
    internal static string N(int value)=>value.ToString(CultureInfo.InvariantCulture);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,int variant,Random random)
    {
        if(!Enum.IsDefined(tier)||!Supports(variant))throw new ArgumentException("InvalidAverageRelationsProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+Relation(variant)+"-"+c.Id==profile)??throw new ArgumentException("InvalidAverageRelationsContext");
        int level=(int)tier;
        if((AverageQuizType)variant==AverageQuizType.TwoGroups)
        {
            // Counts 2k and 3k, with opposite 3d and 2d deviations, make an
            // integral pooled mean while always defeating the unweighted shortcut.
            int k=random.Next(1,level+3),delta=random.Next(1,level+1),mean=random.Next(3*delta+2,15*level+1);
            int a=2*k,b=3*k,ma=mean+3*delta,mb=mean-2*delta;
            if(random.Next(2)==0)(a,b,ma,mb)=(b,a,mb,ma);
            return new(context,profile,(AverageQuizType)variant,language,a,ma,b,mb);
        }
        return new(context,profile,(AverageQuizType)variant,language,random.Next(3,4+2*level),random.Next(3,12*level+1),0,0);
    }
}

public sealed partial class AverageQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenRelations(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile,int variant)
    {
        var d=AverageRelationsPilot.Create(profile,language,tier,variant,_random);
        string equation=d.Expression+" = "+d.Answer,lead=d.Render(d.Text.Lead);
        var contract=new AverageQuizContract(d.Type,d.Facts,d.Answer,d.Context.Unit,lead,d.Problem,equation,
            FormatSolution(lead+":",equation,d.Context.Unit,d.Answer,language),
            d.Type==AverageQuizType.AverageToTotal?d.MeanA:d.Total,
            d.Type==AverageQuizType.AverageToTotal?ArithmeticOperation.Multiply:ArithmeticOperation.Divide,
            d.Count){StoryContextId=profile};
        return CreateQuestion(mode,contract,includeWordProblem:false);
    }
}
