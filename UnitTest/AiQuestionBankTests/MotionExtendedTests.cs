using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static partial class MotionExtendedTests
{
    internal static readonly string[] Relations=["meeting","chasing","downstream","upstream","boat","current"];
    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string relation in Relations)
        {
            string context=relation is "meeting" or "chasing"?"cyclists":"canoe";
            yield return ($"MotionExtended-{relation}-{context}-{language}",Create(relation,context,language,CurriculumTier.ThreeStars,713+Array.IndexOf(Relations,relation)*41));
        }
    }
    private static BasicQuestionContract Create(string relation,string context,AppLanguage language,CurriculumTier tier,int seed)
        =>ReasoningStoryCatalogue.Create(BankQuestionFamily.Motion,(int)Type(relation),tier,language,new(seed),$"motion-extended/{relation}/{context}") with {OpenProse=true};
    private static MotionQuizType Type(string relation)=>relation switch {"meeting"=>MotionQuizType.Meeting,"chasing"=>MotionQuizType.Chasing,_=>MotionQuizType.River};
    private static MotionQuestionKind Kind(string relation)=>relation switch {
        "meeting"=>MotionQuestionKind.MeetingTime,"chasing"=>MotionQuestionKind.CatchUpTime,
        "downstream"=>MotionQuestionKind.RiverDownstreamSpeed,"upstream"=>MotionQuestionKind.RiverUpstreamSpeed,
        "boat"=>MotionQuestionKind.RiverBoatSpeed,"current"=>MotionQuestionKind.RiverCurrentSpeed,_=>throw new ArgumentOutOfRangeException(nameof(relation))};

    // Physical quantities are checked independently of the representative expression.
    private static int Answer(string relation,IReadOnlyList<int> f)
    {
        Require(f.All(x=>x>0),"Motion extended inputs must be positive.");
        if(relation is "meeting" or "chasing")
        {
            Require(f.Count==3&&f[2]>f[1],"Road inputs must bind slower first actor and faster second actor.");
            int closure=relation=="meeting"?f[1]+f[2]:f[2]-f[1];
            return Enumerable.Range(1,1000).Single(x=>x*closure==f[0]);
        }
        Require(f.Count==2&&f[0]>f[1],"River inputs must keep upstream speed positive.");
        if(relation=="downstream")return f[0]+f[1];
        if(relation=="upstream")return f[0]-f[1];
        Require((f[0]-f[1])%2==0,"River inverse inputs must have exact parity.");
        int current=(f[0]-f[1])/2,boat=f[1]+current;
        Require(boat+current==f[0]&&boat-current==f[1],"Both river-bank observations must agree.");
        return relation=="boat"?boat:current;
    }

    internal static void CheckPractice(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        string relation=c.SceneId.Split('/')[1];
        foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q=ReasoningStoryCatalogue.ToPractice(c,draft,mode);var m=q.MotionProblem!;int expected=Answer(relation,m.Facts);
            string unit=relation is "meeting" or "chasing"?(c.Language==AppLanguage.Vietnamese?"giờ":"hours"):"km/h";
            Require(m.Type==Type(relation)&&m.QuestionKind==Kind(relation)&&m.NarrativeId==c.SceneId,"Motion relation/profile identity changed.");
            var context=MotionExtendedPilot.Contexts(c.Language).Single(x=>x.Id==c.SceneId.Split('/')[2]);
            Require(m.NarrativeActors.SequenceEqual(Type(relation)==MotionQuizType.River?new[]{context.First}:new[]{context.First,context.Second}),"Extended motion lost or exchanged the supplied actors.");
            Require(m.CorrectAnswer==expected&&q.CorrectAnswer==expected&&c.Answer==expected,"Independent extended motion arithmetic failed.");
            Require(m.AnswerUnit==unit&&m.RequiredProblemUnits.Contains("km/h"),"Motion extended exact answer unit changed.");
            if(c.Language==AppLanguage.English)Require(!System.Text.RegularExpressions.Regex.IsMatch(m.SolutionText,@"\b1 hours\b"),"A singular duration must not render as 1 hours.");
            Require(MotionExtendedPilot.ValidContract(m),"Extended motion model rejected by its source invariant.");
            var arithmetic=new ArithmeticQuizValidator(new BasicArithmeticEngine());
            Require(arithmetic.Validate(q).IsValid,"Extended motion model rejected by arithmetic validator.");
            Require(new BasicArithmeticEngine().CalculateInteger(q.Expression).Result==expected,"Representative C# arithmetic disagrees with the physical oracle.");
            Require(!m.ProblemText.Contains("{f",StringComparison.Ordinal)&&!m.SolutionText.Contains("{v",StringComparison.Ordinal),"Raw motion slots reached practice.");
            if(mode==ArithmeticQuizMode.MultipleChoice)Require(q.Choices.Count==4&&q.Choices.Distinct().Count()==4&&q.Choices.Count(x=>x==expected)==1,"Bad extended motion choices.");
            if(mode==ArithmeticQuizMode.TrueFalse)Require(q.PresentedEquationIsCorrect==(q.PresentedAnswer==expected),"Bad extended motion assertion.");
            if(mode!=ArithmeticQuizMode.Essay)continue;
            var essay=new EssayAnswerValidator(new BasicArithmeticEngine());string solution=ReasoningStoryCatalogue.Solution(c,draft);
            var grade=essay.ValidateSubmission(q,solution,out var parsed);
            Require(grade.IsCorrect,$"Teacher solution rejected: {c.SceneId}/{c.Language}; {grade.SolutionError}/{grade.EquationError}/{grade.AnswerError}; {solution}; parsed={parsed}.");
            string answer=$"{expected} {unit}";
            bool vi=c.Language==AppLanguage.Vietnamese;
            string wrongHeading=relation switch {
                "meeting"=>vi?"Thời gian đuổi kịp":"Time needed to catch up",
                "chasing"=>vi?"Thời gian gặp nhau":"Time until they meet",
                "downstream"=>vi?"Vận tốc ngược dòng so với bờ":"Upstream speed relative to the riverbank",
                "upstream"=>vi?"Vận tốc xuôi dòng so với bờ":"Downstream speed relative to the riverbank",
                "boat"=>vi?"Vận tốc dòng nước":"Current speed relative to the riverbank",
                _=>vi?"Vận tốc của thuyền khi nước yên lặng":"Craft speed in still water"};
            Require(!essay.Validate(q,wrongHeading,m.EquationText+" "+unit,answer).IsCorrect,"Wrong extended target heading passed alongside correct arithmetic.");
            Require(!essay.Validate(q,(vi?"Không phải ":"Not ")+m.SubjectName,m.EquationText+" "+unit,answer).IsCorrect,"Negated target heading passed alongside correct arithmetic.");
            if(relation is "downstream" or "upstream" or "current")
            {
                string role=relation=="downstream"?vi?"xuôi dòng":"Downstream":relation=="upstream"?vi?"ngược dòng":"Upstream":vi?"dòng nước":"Current";
                string frameHeading=vi?$"Vận tốc {role} so với nước":$"{role} speed relative to the water";
                Require(!essay.Validate(q,frameHeading,m.EquationText+" "+unit,answer).IsCorrect,"Water-relative heading passed for a bank-relative motion target.");
                if(relation!="current")Require(!essay.Validate(q,vi?$"Vận tốc {role} trong nước yên lặng":$"{role} speed in still water",m.EquationText+" "+unit,answer).IsCorrect,"Still-water heading passed for a bank-relative direction target.");
            }
            foreach(string equation in new[] {$"0 + {expected} = {expected} {unit}",$"{expected} = {expected} {unit}",m.EquationText.Replace("= "+expected,"= "+(expected+1),StringComparison.Ordinal)+" "+unit})
                Require(!essay.Validate(q,m.SubjectName,equation,answer).IsCorrect,"A data-free or false worked equation passed extended motion grading: "+equation);
            string wrongUnit=unit=="km/h"?"km":c.Language==AppLanguage.Vietnamese?"hours":"giờ";
            Require(!essay.Validate(q,m.SubjectName,m.EquationText+" "+unit,$"{expected} {wrongUnit}").IsCorrect,"A conflicting final answer unit passed.");
            var changed=m with {Facts=[m.Facts[0]+1,..m.Facts.Skip(1)]};
            Require(!arithmetic.Validate(q with {MotionProblem=changed}).IsValid,"Mutated physical givens passed using the old representative answer.");
            Require(!arithmetic.Validate(q with {MotionProblem=m with {AnswerUnit=wrongUnit}}).IsValid,"Mutated extended motion answer unit passed.");
            var wrongActors=m with {NarrativeActors=Type(relation)==MotionQuizType.River?["unrelated"]:m.NarrativeActors.Reverse().ToArray()};
            Require(!arithmetic.Validate(q with {MotionProblem=wrongActors}).IsValid,"Wrong or exchanged extended motion actor metadata passed.");
            if(relation is "boat" or "current")
            {
                string reduced=$"{m.RepresentativeLeft} ÷ {m.RepresentativeRight} = {expected} {unit}";
                Require(!essay.Validate(q,m.SubjectName,reduced,answer).IsCorrect,"Inverse river work lost the actual downstream/upstream operands.");
            }
            if(relation=="meeting")
            {
                var swapped=m with {Facts=[m.Facts[0],m.Facts[2],m.Facts[1]]};
                Require(swapped.Facts[1]+swapped.Facts[2]==m.Facts[1]+m.Facts[2],"Commutative ownership contrast must preserve the numerical sum.");
                Require(!arithmetic.Validate(q with {MotionProblem=swapped}).IsValid,"Swapping meeting speed owners passed because their sum was unchanged.");
            }
        }
    }

    internal static async Task RunAsync()
    {
        Require((int)MotionQuizType.Basic==0&&(int)MotionQuizType.Chasing==1&&(int)MotionQuizType.Meeting==2&&(int)MotionQuizType.River==3,"Stored motion enum identities changed.");
        Require(NativeCases().Count()==12,"Native motion evidence must have six relations in both languages.");
        int cases=0,freshCount=0,mutations=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var profiles=MotionExtendedPilot.Profiles(language).ToArray();
            Require(profiles.Length==12&&profiles.Distinct().Count()==12,"Extended catalogue must have six relations with exactly two compatible contexts.");
            foreach(string relation in Relations)
            foreach(string context in relation is "meeting" or "chasing"?new[]{"cyclists","cars"}:new[]{"canoe","motorboat"})
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=Create(relation,context,language,tier,713);var d=ReasoningStoryCatalogue.Draft(c);
                Require(profiles.Contains(c.SceneId)&&c.IsValid&&OpenQuestionProse.Supports(c),"Extended relation route missing: "+c.SceneId);
                var result=Validate(c,d);Require(result.IsValid,$"Extended reference not proved: {c.SceneId}/{language}/{tier}: {result.ErrorCode}/{result.ErrorDetails}");
                Require(d.Facts!.Count==(Type(relation)==MotionQuizType.River?3:4),"Wrong road/river fact count.");
                string grammar=OpenQuestionProse.Grammar(c);
                Require(grammar.All(ch=>ch<=127)&&grammar.Contains("character ::="),"Extended grammar must escape Unicode for native interop.");
                Require(!grammar.Contains(System.Text.Json.JsonSerializer.Serialize(d.Facts[0].Text),StringComparison.Ordinal),"Extended grammar must not enumerate complete fact sentences.");
                string[] measures=Type(relation)==MotionQuizType.River?["{f0} {v2}","{f1} {v2}"]:["{f0} {v4}","{f1} {v2}","{f2} {v2}"];
                Require(measures.All(pair=>grammar.Contains(System.Text.Json.JsonSerializer.Serialize(pair),StringComparison.Ordinal)),"Extended grammar lost an atomic quantity/unit pair.");
                Require(d.Facts.All(f=>f.Text.TrimEnd('.').Length<=260),"Reference exceeds extended clause grammar length.");
                CheckPractice(c,d);
                var novel=Novel(d,relation,language==AppLanguage.Vietnamese);
                Require(Validate(c,novel).IsValid,"Clear extended motion wording outside Lists not proved: "+c.SceneId+"/"+language);
                Require(!Validate(c with {OpenProse=false},novel).IsValid,"Novel extended motion fixture is already in the closed List catalogue.");
                CheckPractice(c,novel);mutations+=CheckSemantics(c,d,novel);
                var originalFacts=ReasoningStoryCatalogue.ToPractice(c,d,ArithmeticQuizMode.Essay).MotionProblem!.Facts;
                bool freshDifferent=false;
                for(int seed=0;seed<4;seed++)
                {
                    var fresh=c.FreshFacts(new(901+seed));
                    Require(fresh.SceneId==c.SceneId&&fresh.Story!.Schema==c.Story!.Schema&&fresh.BankVariant==c.BankVariant&&fresh.OpenProse,"Fresh motion facts changed relation/schema/open identity.");
                    Require(QuestionProseIdentity.Hash(fresh,novel)==QuestionProseIdentity.Hash(c,novel),"Fresh quantities changed prose identity.");
                    freshDifferent|=!ReasoningStoryCatalogue.ToPractice(fresh,novel,ArithmeticQuizMode.Essay).MotionProblem!.Facts.SequenceEqual(originalFacts);
                    CheckPractice(fresh,novel);freshCount++;
                }
                Require(freshDifferent,"Fresh motion facts never changed the initial quantities.");
                cases++;
            }
        }
        Require(cases==120,"Extended motion matrix must cover 120 language/context/relation/tier cases.");
        CheckCycles();CheckContent();await CheckStorageAndWorkerAsync();
        Console.WriteLine($"PASS {cases} extended motion profiles, {freshCount} fresh facts, {mutations} semantic contrasts; six relations, exact physical arithmetic/units, all modes/teacher grading, outside-List bilingual prose, cycles, SQLite/Excel and worker quarantine.");
    }
    private static void CheckCycles()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var cycle=new ReasoningStoryCycle();var seen=Enumerable.Range(0,16).Select(_=>cycle.Next(BankQuestionFamily.Motion,(int)MotionQuizType.River,CurriculumTier.ThreeStars,language,creativeProse:true).SceneId.Split('/')[1]).ToArray();
            Require(seen.SequenceEqual(Enumerable.Range(0,4).SelectMany(_=>new[]{"downstream","upstream","boat","current"})),"River creative cycles must deterministically cover all four targets.");
            foreach(var type in new[]{MotionQuizType.Meeting,MotionQuizType.Chasing})
                Require(MotionExtendedCatalogue.IsProfile(cycle.Next(BankQuestionFamily.Motion,(int)type,CurriculumTier.ThreeStars,language,creativeProse:true)),"Creative road selection fell back to legacy prose.");
            Require(!MotionExtendedCatalogue.IsProfile(cycle.Next(BankQuestionFamily.Motion,(int)MotionQuizType.Basic,CurriculumTier.ThreeStars,language,creativeProse:true)),"Extended motion displaced the existing basic open profiles.");
        }
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Require(bool value,string message)=>TestAssert.That(value,message);
}
