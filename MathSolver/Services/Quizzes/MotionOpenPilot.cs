using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

internal static class MotionOpenPilot
{
    internal const string Prefix="motion-open/";
    internal sealed record Context(string Id,string Subject,int MinimumSpeed,int MaximumSpeed);
    internal sealed record Relation(string Id,MotionQuestionKind Kind,string Fact0,string AlternateFact0,string Fact1,string? Fact2,string Question,string Lead);
    internal sealed record Data(Context Context,Relation Relation,string Profile,int Speed,int TravelHours,int RestHours,AppLanguage Language)
    {
        internal int Distance=>checked(Speed*TravelHours);
        internal bool HasRest=>Relation.Kind==MotionQuestionKind.BasicRestDistance;
        internal int ElapsedHours=>TravelHours+RestHours;
        internal int First=>Relation.Kind is MotionQuestionKind.BasicSpeed or MotionQuestionKind.BasicTime?Distance:Speed;
        internal int Second=>Relation.Kind==MotionQuestionKind.BasicTime?Speed:HasRest?ElapsedHours:TravelHours;
        internal int Answer=>Relation.Kind switch {MotionQuestionKind.BasicSpeed=>Speed,MotionQuestionKind.BasicTime=>TravelHours,_=>Distance};
        internal string TimeUnit=>Language==AppLanguage.Vietnamese?"giờ":"hours";
        internal string RestUnit=>Language==AppLanguage.Vietnamese?"giờ":RestHours==1?"hour":"hours";
        internal string AnswerUnit=>Relation.Kind switch {MotionQuestionKind.BasicSpeed=>"km/h",MotionQuestionKind.BasicTime=>TimeUnit,_=>"km"};
        internal Dictionary<string,string> Values
        {
            get
            {
                var v=new Dictionary<string,string>{{"f0",N(First)},{"f1",N(Second)},{"v0",Context.Subject},{"v1","km/h"},{"v2",TimeUnit},{"v3","km"}};
                if(HasRest){v["f2"]=N(RestHours);v["v4"]=RestUnit;}return v;
            }
        }
        internal string Equation=>Relation.Kind switch {
            MotionQuestionKind.BasicDistance=>$"{Speed} × {TravelHours} = {Answer}",
            MotionQuestionKind.BasicSpeed=>$"{Distance} ÷ {TravelHours} = {Answer}",
            MotionQuestionKind.BasicTime=>$"{Distance} ÷ {Speed} = {Answer}",
            _=>$"{Speed} × ({ElapsedHours} - {RestHours}) = {Answer}"};
        internal string Render(string text){foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return text;}
        internal string Problem=>Render(string.Join(" ",new[]{Relation.Fact0,Relation.Fact1,Relation.Fact2,Relation.Question}.Where(x=>!string.IsNullOrWhiteSpace(x))));
    }
    private static string N(int value)=>value.ToString(CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage l)=>QuizContentCatalog.LoadList<Context>("MotionOpen.Contexts",QuizContentCatalog.Culture(l));
    internal static IReadOnlyList<Relation> Relations(AppLanguage l)=>QuizContentCatalog.LoadList<Relation>("MotionOpen.Relations",QuizContentCatalog.Culture(l));
    internal static IEnumerable<string> Profiles(AppLanguage l)=>Relations(l).SelectMany(r=>Contexts(l).Select(c=>Prefix+r.Id+"/"+c.Id));
    internal static bool IsProfile(string? id)=>id?.StartsWith(Prefix,StringComparison.Ordinal)==true;
    internal static bool ValidContract(MotionQuizContract p)
    {
        if(!IsProfile(p.NarrativeId)||p.Type!=MotionQuizType.Basic||p.Facts.Any(x=>x<=0))return false;
        string[] parts=p.NarrativeId!.Split('/');
        if(parts.Length!=3||parts[2] is not ("car" or "bus" or "bicycle"))return false;
        var kind=parts[1] switch {"distance"=>MotionQuestionKind.BasicDistance,"speed"=>MotionQuestionKind.BasicSpeed,
            "time"=>MotionQuestionKind.BasicTime,"rest"=>MotionQuestionKind.BasicRestDistance,_=>(MotionQuestionKind)(-1)};
        if(p.QuestionKind!=kind||p.Facts.Count!=(kind==MotionQuestionKind.BasicRestDistance?3:2))return false;
        var f=p.Facts;
        if(kind==MotionQuestionKind.BasicRestDistance&&f[2]>=f[1])return false;
        bool division=kind is MotionQuestionKind.BasicSpeed or MotionQuestionKind.BasicTime;
        if(division&&f[0]%f[1]!=0)return false;
        var left=(System.Numerics.BigInteger)f[0];
        int right=kind==MotionQuestionKind.BasicRestDistance?f[1]-f[2]:f[1];
        var answer=division?left/right:left*right;
        string expectedUnit=kind==MotionQuestionKind.BasicSpeed?"km/h":kind==MotionQuestionKind.BasicTime
            ?p.RequiredProblemUnits.FirstOrDefault(x=>x is "giờ" or "hours")??"":"km";
        return answer>0&&p.CorrectAnswer==answer&&p.AnswerUnit==expectedUnit&&expectedUnit.Length>0
            &&p.RequiredProblemUnits.Contains("km/h")&&p.RequiredProblemUnits.Contains("km")
            &&p.RequiredProblemUnits.Count(x=>x is "giờ" or "hours")==1
            &&p.RepresentativeLeft==left&&p.RepresentativeRight==right
            &&p.RepresentativeOperation==(division?ArithmeticOperation.Divide:ArithmeticOperation.Multiply);
    }
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        string[] parts=profile.Split('/');
        if(parts.Length!=3||parts[0]!="motion-open"||!Enum.IsDefined(tier))throw new ArgumentException("InvalidMotionProfile");
        var relation=Relations(language).SingleOrDefault(r=>r.Id==parts[1])??throw new ArgumentException("InvalidMotionRelation");
        var context=Contexts(language).SingleOrDefault(c=>c.Id==parts[2])??throw new ArgumentException("InvalidMotionContext");
        int speed=random.Next(context.MinimumSpeed,Math.Min(context.MaximumSpeed,context.MinimumSpeed+10*(int)tier)+1);
        int time=random.Next(2,Math.Min(5,(int)tier+2)+1);
        int rest=relation.Kind==MotionQuestionKind.BasicRestDistance?random.Next(1,3):0;
        return new(context,relation,profile,speed,time,rest,language);
    }
}

public sealed partial class MotionQuizGenerator
{
    public ArithmeticQuizQuestion GenerateNarrative(ArithmeticQuizMode mode,AppLanguage language,MotionQuizType type,QuizCurriculumContext context,string? profile=null)
    {
        if(MotionExtendedPilot.IsProfile(profile))return GenerateExtendedNarrative(mode,language,type,context,profile!);
        if(!MotionOpenPilot.IsProfile(profile))return GenerateAlgorithm(mode,language,type,context);
        if(type!=MotionQuizType.Basic)throw new ArgumentException("InvalidMotionRelation");
        var d=MotionOpenPilot.Create(profile!,language,context.Tier,_random);
        string solution=d.Render(d.Relation.Lead)+":\n"+d.Equation+" "+d.AnswerUnit+"\n"
            +(language==AppLanguage.Vietnamese?"Đáp số: ":"Answer: ")+d.Answer+" "+d.AnswerUnit;
        var facts=d.HasRest?new[]{d.First,d.Second,d.RestHours}:new[]{d.First,d.Second};
        var p=new MotionQuizContract(type,d.Relation.Kind,facts,d.Answer,d.AnswerUnit,d.Render(d.Relation.Lead),d.Problem,d.Equation,solution,
            d.Relation.Kind is MotionQuestionKind.BasicSpeed or MotionQuestionKind.BasicTime?d.Distance:d.Speed,
            d.Relation.Kind is MotionQuestionKind.BasicSpeed or MotionQuestionKind.BasicTime?ArithmeticOperation.Divide:ArithmeticOperation.Multiply,
            d.Relation.Kind==MotionQuestionKind.BasicTime?d.Speed:d.TravelHours,new[]{"km/h",d.TimeUnit,"km"})
            {NarrativeActors=[d.Context.Subject],NarrativeId=d.Profile};
        return CreateQuestion(mode,p);
    }
}
