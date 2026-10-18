using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class OvernightTimeTests
{
    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var (context,index) in OvernightTimePilot.Contexts(language).Select((c,i)=>(c,i)))
            yield return ($"Overnight-{context.Id}-{language}",ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)OvernightTimePilot.Type,
                (CurriculumTier)(index+1),language,new(2251+index*41+(language==AppLanguage.English?17:0)),OvernightTimePilot.Prefix+context.Id));
    }
    internal static void CheckPractice(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        var data=OvernightTimePilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed));
        Check(data.Start>=20*60&&data.Start<24*60&&data.End>=0&&data.End<data.Start&&data.EndDayOffset==1,"Overnight bounds/day role changed.");
        var start=new DateTime(2026,1,10,data.Start/60,data.Start%60,0,DateTimeKind.Utc);
        var end=new DateTime(2026,1,11,data.End/60,data.End%60,0,DateTimeKind.Utc);
        int elapsed=(int)(end-start).TotalMinutes;
        Check(elapsed>0&&elapsed<24*60&&elapsed==data.Elapsed,"Independent timestamp oracle disagrees.");
        foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q=ReasoningStoryCatalogue.ToPractice(c,draft,mode);var p=q.ElementaryProblem!;
            Check(p.Type==OvernightTimePilot.Type&&p.StoryContextId==c.SceneId&&p.Reasoning!.ScenarioId==OvernightTimePilot.Relation,"Overnight profile replaced.");
            Check(p.Answers.Count==1&&p.Answers[0].Value==new ReducedFraction(elapsed,1),"Wrong next-day elapsed answer.");
            Check(p.Reasoning!.Givens.Count==5&&p.Reasoning.Givens.Single(g=>g.Role=="end-day-offset").Value=="1","Missing immutable following-day role.");
            Check(p.Reasoning.Steps[^1].Value==new ReducedFraction(elapsed,1)&&!p.RequiresSolution&&!p.RequiresCalculation,"Direct overnight answer lost.");
            Check(!p.ProblemText.Contains("{f")&&!p.ProblemText.Contains("{v"),"Overnight slots not rendered.");
            if(c.Language==AppLanguage.Vietnamese)Check(!p.ProblemText.StartsWith("buổi"),"Overnight opening not capitalized.");
            if(mode==ArithmeticQuizMode.MultipleChoice)Check(p.ChoiceTexts is {Count:4}&&p.ChoiceTexts.Distinct().Count()==4&&p.ChoiceTexts.Contains(p.AnswerText),"Overnight choices invalid.");
            if(mode==ArithmeticQuizMode.TrueFalse)Check(q.PresentedEquationIsCorrect==(p.PresentedText==p.AnswerText),"Overnight true/false flag wrong.");
            if(mode!=ArithmeticQuizMode.Essay)continue;
            var grader=new EssayAnswerValidator(new BasicArithmeticEngine());
            foreach(string answer in new[]{elapsed.ToString(),elapsed+(c.Language==AppLanguage.Vietnamese?" phút":" minutes"),
                (c.Language==AppLanguage.Vietnamese?"Đáp số: ":"Answer: ")+elapsed+" "+p.Answers[0].Unit,ReasoningStoryCatalogue.Solution(c,draft)})
                Check(grader.ValidateSubmission(q,answer).IsCorrect,"Direct/complete overnight answer rejected: "+answer);
            foreach(string wrong in new[]{(data.End-data.Start).ToString(),(elapsed+24*60).ToString(),(elapsed+1).ToString(),elapsed+(c.Language==AppLanguage.Vietnamese?" giờ":" hours")})
                Check(!grader.ValidateSubmission(q,wrong).IsCorrect,"Wrong day/clock/unit accepted: "+wrong);
        }
    }
    internal static async Task RunAsync()
    {
        QuizContentTests.Run();CheckMalformedLists();
        var context=OvernightTimePilot.Contexts(AppLanguage.Vietnamese)[0];
        Check(new OvernightTimePilot.Data(context,OvernightTimePilot.Prefix+context.Id,22*60+45,20).Elapsed==95,"22:45 -> next-day 00:20 example failed.");
        Check(new OvernightTimePilot.Data(context,OvernightTimePilot.Prefix+context.Id,23*60+59,0).Elapsed==1,"Midnight boundary failed.");
        using var workspace=TestWorkspace.Temporary("overnight-time");
        int freshCount=0,clearCount=0,badCount=0,reviewCount=0,midnightCount=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var row in OvernightTimePilot.Contexts(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)OvernightTimePilot.Type,tier,language,new(2251),OvernightTimePilot.Prefix+row.Id) with {OpenProse=true};
            var d=ReasoningStoryCatalogue.Draft(c);
            Check(c.IsValid&&OpenQuestionProse.Supports(c)&&Validate(c,d).IsValid,"Reference overnight draft rejected: "+Validate(c,d));
            var alt=d with {Facts=row.AlternativeFacts.Select((s,i)=>new NarrativeClause("fact_"+i,s)).ToArray(),Question=row.AlternativeQuestion};
            var mixed=alt with {Facts=[d.Facts![0],alt.Facts![1],d.Facts[2]],SolutionLeads=[new("step_0",vi?"Thời lượng hoạt động":"Total duration")]};
            var clockVariant=d with {Facts=[new("fact_0",vi?"{v1} của {v0} bắt đầu vào lúc {f0} giờ {f1} phút.":"The {v1} for {v0} began at {f0} hours {f1} minutes."),
                new("fact_1",vi?"{v1} của {v0} kết thúc vào {f2} giờ {f3} phút ngày kế tiếp.":"The {v1} for {v0} concluded at {f2} hours {f3} minutes the next day."),d.Facts[2]]};
            var continuousVariant=d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",vi?"Hoạt động diễn ra liên tục, không có khoảng nghỉ.":"The activity takes place continuously with no breaks.")]};
            foreach(var clear in new[]{d,alt,mixed,clockVariant,continuousVariant})
            {
                var proof=Validate(c,clear);Check(proof.IsValid,"Clear next-day roles missed: "+proof);
                Check(QuestionBankStore.SerializeDraft(proof.Draft!)==QuestionBankStore.SerializeDraft(clear),"Verifier rewrote overnight prose.");
                CheckPractice(c,clear);clearCount++;
                foreach(var wrong in new[]{
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v1} của {v0} kết thúc lúc {f2} giờ {f3} phút cùng ngày.":"{v0}'s {v1} ends at {f2} hours {f3} minutes on the same day."),clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v1} của {v0} kết thúc lúc {f2} giờ {f3} phút hai ngày sau.":"{v0}'s {v1} ends at {f2} hours {f3} minutes two days later."),clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v1} của {v0} kết thúc lúc {f2} giờ {f3} phút ngày hôm trước.":"{v0}'s {v1} ends at {f2} hours {f3} minutes on the previous day."),clear.Facts[2]]},
                    clear with {Facts=[new("fact_0",vi?"{v1} của {v0} kết thúc lúc {f0} giờ {f1} phút.":"{v0}'s {v1} ends at {f0} hours {f1} minutes."),clear.Facts![1],clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v1} của {v0} bắt đầu lúc {f2} giờ {f3} phút ngày hôm sau.":"{v0}'s {v1} starts at {f2} hours {f3} minutes on the next day."),clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Hoạt động này có một lần nghỉ.":"The session includes a break.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Một buổi thứ hai diễn ra sau đó.":"A second session follows.")]},
                    clear with {Question=vi?"{v1} của {v0} kết thúc lúc mấy giờ?":"What time does {v0}'s {v1} end?"},
                    clear with {Question=vi?"{v1} của {v0} kéo dài bao nhiêu giờ?":"How many hours does {v0}'s {v1} last?"},
                    clear with {Question=clear.Question+" JSON"}})
                {var result=Validate(c,wrong);Check(!result.IsValid&&!result.NeedsReview,"Proved wrong overnight meaning not rejected: "+result);badCount++;}
                foreach(var unknown in new[]{
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v1} của {v0} kết thúc lúc {f2} giờ {f3} phút.":"{v0}'s {v1} ends at {f2} hours {f3} minutes."),clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Hoạt động này không được ghi là không nghỉ.":"The session is not described as without breaks.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Hoạt động này có thể diễn ra liên tục, không nghỉ.":"The activity might run continuously without a break.")]},
                    clear with {Question=vi?"{v1} của {v0} kéo dài bao lâu?":"How long does {v0}'s {v1} last?"},
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v1} của {v0} kết thúc lúc {f2} giờ {f3} phút sau buổi trưa ngày hôm sau.":"{v0}'s {v1} ends at {f2} hours {f3} minutes past noon on the following day."),clear.Facts[2]]},
                    clear with {Facts=[new("fact_0",vi?"{v1} của {v0} không bắt đầu lúc {f0} giờ {f1} phút.":"{v0}'s {v1} does not start at {f0} hours {f1} minutes."),clear.Facts![1],clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Hoạt động này diễn ra liên tục, không nghỉ, có thể vậy.":"The activity runs continuously without a break, perhaps.")]}})
                {Check(Validate(c,unknown).NeedsReview,"Unproved overnight day/continuity/target must review once.");reviewCount++;}
            }
            for(int seed=0;seed<20;seed++)
            {
                var fresh=c.FreshFacts(new(1021+seed));Check(fresh.SceneId==c.SceneId&&fresh.Story!.Schema==c.Story!.Schema&&fresh.OpenProse,"Fresh overnight schema changed.");
                CheckPractice(fresh,alt);freshCount++;
                if(OvernightTimePilot.Create(fresh.SceneId,language,tier,new(fresh.Story!.Seed)).End==0)midnightCount++;
            }
            var direct=new ElementaryQuizGenerator(new(2251)).Generate(ArithmeticQuizMode.Essay,QuizProblemKind.Time,OvernightTimePilot.Type,language,tier);
            Check(direct.ElementaryProblem!.StoryContextId!.StartsWith(OvernightTimePilot.Prefix),"Direct generation escaped overnight pilot.");
            Check(OvernightTimeCatalogue.IsProfile(new ReasoningStoryCycle().Next(c.Family,c.BankVariant,tier,language,creativeProse:true)),"AI cycle missed overnight profile.");
            Check(OpenQuestionProse.Grammar(c).All(ch=>ch<=127),"Overnight grammar not ASCII-escaped.");
            if(!vi&&row.Id=="watch")
                Check(Validate(c,d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2","The journey runs continuously without a break.")]}).NeedsReview,
                    "An unrelated journey must not prove continuity of a night watch.");
        }
        Check(midnightCount>0,"Fresh fixtures did not cover midnight end.");
        var store=new QuestionBankStore(workspace.FilePath("worker.db3"));var runtime=new Runtime();var worker=new AiQuestionGenerationService(runtime,store);
        worker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,true,Family:BankQuestionFamily.Time,StoryVariant:(int)OvernightTimePilot.Type,CreativeProse:true));
        await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        Check(worker.Snapshot.State==AiJobState.Completed&&runtime.Calls==3,"Overnight worker retry/review-once policy changed.");
        Check(worker.Snapshot.Items[0].State==AiItemState.PendingReview&&worker.Snapshot.Items[0].Attempts.Count==2&&worker.Snapshot.Items[1].State==AiItemState.Saved,"Invalid -> review once -> next verified flow failed.");
        var pending=(await store.GetPendingReviewsAsync()).Single();
        Check(!await store.ContainsProseAsync(new(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc)),"Pending overnight leaked into active bank.");
        Check(await store.ApproveReviewAsync(pending.Id),"Synthetic explicit approval failed.");
        using var excel=new MemoryStream();await store.ExportExcelAsync(excel);excel.Position=0;
        var restored=new QuestionBankStore(workspace.FilePath("restored.db3"));Check((await restored.ImportExcelAsync(excel)).Inserted==2,"Overnight SQLite/Excel roundtrip failed.");
        var record=await restored.TakeReasoningAsync(BankQuestionFamily.Time,(int)OvernightTimePilot.Type,CurriculumTier.ThreeStars,AppLanguage.Vietnamese);
        Check(record is not null&&OvernightTimeCatalogue.IsProfile(record.Contract),"Stored overnight profile lost.");
        CheckPractice(record!.Contract.FreshFacts(new(2511)),record.Draft);
        Console.WriteLine($"PASS {freshCount} fresh overnight fixtures ({midnightCount} end at midnight), {clearCount} clear drafts, {badCount} invalid and {reviewCount} review-only mutations; timestamp oracle, grading, worker/review-once, SQLite/Excel.");
    }
    private static void CheckMalformedLists()
    {
        using var stream=typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent.en-US.json")!;
        var pack=QuizContentCatalog.ReadPack(stream);
        foreach(var (key,value) in new[]{("TargetRole",(object)"end-clock"),("EndDayOffset",2),("Continuity","with-break"),("Question","How many hours does {v0}'s {v1} last {f3}?" )})
        {
            var rows=JsonNode.Parse(pack.Lists[OvernightTimePilot.ListName].GetRawText())!;
            rows[0]![key]=JsonSerializer.SerializeToNode(value);
            bool rejected=false;
            try{QuizContentCatalog.Validate(new(){Version=pack.Version,Culture=pack.Culture,Texts=pack.Texts,Lists=new(pack.Lists){[OvernightTimePilot.ListName]=JsonSerializer.SerializeToElement(rows)}});}
            catch(InvalidDataException){rejected=true;}
            Check(rejected,"Malformed overnight roles accepted: "+key);
        }
    }
    private sealed class Runtime:IQuestionTextRuntime
    {
        public bool IsLoaded=>true;public string ModelName=>"overnight-fixture";public int Calls;
        public Task ReleaseAsync()=>Task.CompletedTask;
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken cancellationToken,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            Calls++;var d=ReasoningStoryCatalogue.Draft(c);
            if(Calls==1)d=d with {Question="{v1} của {v0} kết thúc lúc mấy giờ?"};
            if(Calls==2)d=d with {Question="{v1} của {v0} kéo dài bao lâu?"};
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
