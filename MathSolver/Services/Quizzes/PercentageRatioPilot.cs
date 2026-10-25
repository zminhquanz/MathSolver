using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

internal static class PercentageRatioPilot
{
    internal const string Prefix="percentage-ratio-open/",ListName="PercentageRatioOpen.Contexts",Relation="known-whole-subset-find-percentage-ratio";
    internal const int Variant=(int)PercentageQuizType.FindPercentageRatio;
    internal sealed record Context(string Id,string Relation,string Owner,string WholeUnit,string WholeNoun,string PartUnit,string PartNoun,int Capacity,
        string Whole,string Part,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int Whole,int Part)
    {
        internal int Answer=>checked(Part*100/Whole);
        internal string Expression=>$"{Part} ÷ {Whole} × 100";
        internal Dictionary<string,string> Values=>new(){["f0"]=N(Whole),["f1"]=N(Part),["v0"]=Context.Owner,["v1"]=Context.WholeUnit,
            ["v2"]=Context.WholeNoun,["v3"]=Context.PartUnit,["v4"]=Context.PartNoun};
        internal string Render(string text){foreach(var (k,v) in Values)text=text.Replace("{"+k+"}",v,StringComparison.Ordinal);return text;}
        internal string Problem=>Render(Context.Whole+" "+Context.Part+" "+Context.Question);
    }
    internal static string N(int x)=>x.ToString(CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(x=>Prefix+x.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier))throw new ArgumentException("InvalidPercentageRatioTier");
        var c=Contexts(language).SingleOrDefault(x=>Prefix+x.Id==profile&&x.Relation==Relation)??throw new ArgumentException("InvalidPercentageRatioProfile");
        int[] rates=[10,20,25,50,75];int rate=rates[random.Next(rates.Length)];
        int step=100/(int)System.Numerics.BigInteger.GreatestCommonDivisor(100,rate);
        int maximum=Math.Min(c.Capacity,new[]{100,200,500,1000,2000}[(int)tier-1]);
        int whole=step*random.Next((20+step-1)/step,maximum/step+1);
        return new(c,profile,whole,whole*rate/100);
    }
    internal static bool ValidContract(PercentageQuizContract p)
        =>p.Type==PercentageQuizType.FindPercentageRatio&&p.StoryContextId is Prefix+"library" or Prefix+"nursery"
        &&p.Facts.Count==2&&p.Facts[0]>p.Facts[1]&&p.Facts[1]>0&&p.Facts[0]<=2000&&p.Facts[1]*100%p.Facts[0]==0
        &&p.CorrectAnswer==p.Facts[1]*100/p.Facts[0]&&p.AnswerUnit=="%"&&p.RepresentativeOperation==ArithmeticOperation.Divide
        &&p.RepresentativeLeft==p.Facts[1]*100&&p.RepresentativeRight==p.Facts[0]
        &&p.EquationText==$"{p.Facts[1]} ÷ {p.Facts[0]} × 100 = {p.CorrectAnswer}";
}

public sealed partial class PercentageQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenRatio(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=PercentageRatioPilot.Create(profile,language,tier,_random);string equation=d.Expression+" = "+PercentageRatioPilot.N(d.Answer);
        var p=new PercentageQuizContract(PercentageQuizType.FindPercentageRatio,[d.Whole,d.Part],d.Answer,"%",d.Render(d.Context.Lead),d.Problem,equation,
            FormatSolution(d.Render(d.Context.Lead)+":",equation,"%",d.Answer,language),d.Part*100,ArithmeticOperation.Divide,d.Whole){StoryContextId=profile};
        return CreateQuestion(mode,p,includeWordProblem:false);
    }
}
