using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class SameDayBreakTests
{
    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var (context,index) in SameDayBreakPilot.Contexts(language).Select((c,i)=>(c,i)))
            yield return ($"TimeBreak-{context.Id}-{language}",ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)SameDayBreakPilot.Type,
                (CurriculumTier)(index+1),language,new(1813+index*41+(language==AppLanguage.English?17:0)),SameDayBreakPilot.Prefix+context.Id));
    }
    internal static void CheckPractice(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        var data=SameDayBreakPilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed));
        Check(data.Start>=0&&data.Start<data.End&&data.End<24*60&&data.Break>0&&data.Break<data.Elapsed,"Invalid clock/break bounds.");
        // Count each active minute independently instead of evaluating the generator expression.
        int active=0;for(int minute=data.Start;minute<data.End;minute++)if(minute>=data.Start+data.Break)active++;
        foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q=ReasoningStoryCatalogue.ToPractice(c,draft,mode);var p=q.ElementaryProblem!;
            Check(p.Type==SameDayBreakPilot.Type&&p.StoryContextId==c.SceneId&&p.Reasoning!.ScenarioId==SameDayBreakPilot.Relation,"Break profile replaced.");
            Check(p.Answers.Count==1&&p.Answers[0].Value==new ReducedFraction(active,1)&&data.Active==active,"Active-minute oracle disagrees.");
            Check(p.Reasoning!.Givens.Count==5&&p.Reasoning.Givens.Single(x=>x.Role=="included-break-minutes").Value==data.Break.ToString(),"Break role missing.");
            Check(!p.RequiresCalculation&&!p.RequiresSolution&&!p.ProblemText.Contains("{f")&&!p.ProblemText.Contains("{v"),"Direct answer or rendered facts lost.");
            Check(p.Reasoning.Steps[^1].Value==new ReducedFraction(active,1),"Final calculation does not match active-minute oracle.");
            if(c.Language==AppLanguage.Vietnamese)Check(!p.ProblemText.StartsWith("buổi"),"Sentence opening not capitalized.");
            if(mode==ArithmeticQuizMode.MultipleChoice)Check(p.ChoiceTexts is {Count:4}&&p.ChoiceTexts.Distinct().Count()==4&&p.ChoiceTexts.Contains(p.AnswerText),"Duplicate break choices or missing answer.");
            if(mode==ArithmeticQuizMode.TrueFalse)Check(q.PresentedEquationIsCorrect==(p.PresentedText==p.AnswerText),"Wrong break truth flag.");
            if(mode!=ArithmeticQuizMode.Essay)continue;
            var grader=new EssayAnswerValidator(new BasicArithmeticEngine());
            foreach(string answer in new[]{active.ToString(),active+(c.Language==AppLanguage.Vietnamese?" phút":" minutes"),
                (c.Language==AppLanguage.Vietnamese?"Đáp số: ":"Answer: ")+active+" "+p.Answers[0].Unit,ReasoningStoryCatalogue.Solution(c,draft)})
                Check(grader.ValidateSubmission(q,answer).IsCorrect,"Break answer or worked solution rejected: "+answer);
            foreach(string wrong in new[]{data.Elapsed.ToString(),(data.Elapsed+data.Break).ToString(),(active+1).ToString(),active+(c.Language==AppLanguage.Vietnamese?" giờ":" hours")})
                Check(!grader.ValidateSubmission(q,wrong).IsCorrect,"Wrong active time/unit accepted: "+wrong);
        }
    }
    internal static async Task RunAsync()
    {
        QuizContentTests.Run();CheckMalformedList();SameDayBreakMeaningTests.Run();
        var example=new SameDayBreakPilot.Data(SameDayBreakPilot.Contexts(AppLanguage.Vietnamese)[0],SameDayBreakPilot.Prefix+"study",8*60+15,9*60+30,15);
        Check(example.Elapsed==75&&example.Active==60,"75 minus 15 example failed.");
        using var workspace=TestWorkspace.Temporary("same-day-break");int freshCount=0,clearCount=0,badCount=0,reviewCount=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var context in SameDayBreakPilot.Contexts(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)SameDayBreakPilot.Type,tier,language,new(1813),SameDayBreakPilot.Prefix+context.Id) with {OpenProse=true};
            var d=ReasoningStoryCatalogue.Draft(c);
            Check(c.IsValid&&OpenQuestionProse.Supports(c)&&Validate(c,d).IsValid,"Reference one-break draft rejected: "+Validate(c,d));
            var alt=d with {Facts=context.AlternativeFacts.Select((s,i)=>new NarrativeClause("fact_"+i,s)).ToArray(),Question=context.AlternativeQuestion};
            var mixed=alt with {Facts=[d.Facts![0],alt.Facts![1],d.Facts[2],alt.Facts[3]],
                Question=vi?"Không kể thời gian nghỉ, {v1} của {v0} có bao nhiêu phút?":"How many active minutes does {v0}'s {v1} have, excluding the break?"};
            var native=d with {Facts=[d.Facts![0],d.Facts[1],
                new("fact_2",vi?"Trong khoảng thời gian diễn ra {v1} của {v0} có một lần nghỉ dài {f4} phút.":"During {v0}'s {v1} there is one break of {f4} minutes."),
                new("fact_3",vi?"Cả hai mốc thời gian này đều nằm trong cùng một ngày.":"The start and end times are on the same day.")],
                Question=vi?"Thời gian thực hiện {v1} của {v0} không tính thời gian nghỉ là bao nhiêu phút?":d.Question};
            var active=native with {Facts=[native.Facts![0],native.Facts[1],
                new("fact_2",vi?"Trong suốt {v1} của {v0} chỉ có một lần nghỉ dài {f4} phút.":context.Facts[2]),
                new("fact_3",vi?"Toàn bộ sự việc diễn ra trong cùng một ngày.":context.Facts[3])],
                Question=vi?"Thời gian thực tế mà {v0} dành cho {v1} (không tính thời gian nghỉ) là bao nhiêu phút?":context.AlternativeQuestion,
                SolutionLeads=[new("step_0",vi?"Thời gian hoạt động thực tế không kể thời gian nghỉ":context.Lead)]};
            foreach(var clear in new[]{d,alt,mixed,native,active})
            {
                var proof=Validate(c,clear);Check(proof.IsValid,"Clear included-break roles missed: "+proof);
                Check(QuestionBankStore.SerializeDraft(proof.Draft!)==QuestionBankStore.SerializeDraft(clear),"Verifier rewrote AI prose.");
                CheckPractice(c,clear);clearCount++;
                foreach(var bad in new[]{
                    clear with {Question=vi?"Tổng thời gian {v1} của {v0} là bao nhiêu phút?":"What is the total elapsed duration of {v0}'s {v1} in minutes?"},
                    clear with {Question=vi?"{v1} của {v0} kết thúc lúc mấy giờ?":"What time does {v0}'s {v1} end?"},
                    clear with {Question=vi?"{v1} của {v0} bao gồm thời gian nghỉ là bao nhiêu phút?":"How many minutes does {v0}'s {v1} last, including the break?"},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Sau khi {v1} của {v0} kết thúc, có một lần nghỉ dài {f4} phút.":"After {v0}'s {v1} ends, there is one break of {f4} minutes."),clear.Facts[3]]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Trong {v1} của {v0}, có hai lần nghỉ dài {f4} phút.":"During {v0}'s {v1}, there are multiple breaks of {f4} minutes."),clear.Facts[3]]},
                    clear with {Facts=[new("fact_0",vi?"{v1} của {v0} kết thúc lúc {f0} giờ {f1} phút.":"{v0}'s {v1} ends at {f0} hours {f1} minutes."),clear.Facts![1],clear.Facts[2],clear.Facts[3]]},
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v1} của {v0} bắt đầu lúc {f2} giờ {f3} phút.":"{v0}'s {v1} starts at {f2} hours {f3} minutes."),clear.Facts[2],clear.Facts[3]]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],clear.Facts[2],new("fact_3",vi?"Buổi hoạt động kết thúc ngày hôm sau.":"The session ends the next day.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],clear.Facts[2],new("fact_3",vi?"Buổi hoạt động liên tục trong cùng ngày.":"The session runs continuously on the same day.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],clear.Facts[2],new("fact_3",vi?"Một buổi thứ hai diễn ra sau đó.":"A second session follows.")]},
                    clear with {Question=clear.Question+" JSON"}})
                {Check(!Validate(c,bad).IsValid&&!Validate(c,bad).NeedsReview,"Clearly wrong break relation not rejected: "+Validate(c,bad));badCount++;}
                foreach(var unknown in new[]{
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"{v1} của {v0} có khoảng nghỉ dài {f4} phút.":"{v0}'s {v1} has a break of {f4} minutes."),clear.Facts[3]]},
                    clear with {Question=vi?"{v1} của {v0} kéo dài bao nhiêu phút?":"How long does {v0}'s {v1} last in minutes?"},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],clear.Facts[2],new("fact_3",vi?"Hai mốc giờ được ghi lại trong sổ.":"Both clock times are written in a notebook.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Trước khi {v1} của {v0} kết thúc, có đúng một lần nghỉ dài {f4} phút.":"Before {v0}'s {v1} ends, there is exactly one break of {f4} minutes."),clear.Facts[3]]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],clear.Facts[2],new("fact_3",vi?"Buổi hoạt động không liên tục trong cùng một ngày.":"The session does not run continuously on the same day.")]},
                    clear with {Question=vi?"Tổng thời gian thực hiện {v1} của {v0}, không tính nghỉ, là bao nhiêu phút?":"What is the total duration of {v0}'s {v1} in minutes, not including the break?"},
                    clear with {Question=vi?"Thời gian thực hiện {v1} của {v0} không tính thời gian nghỉ được ghi thế nào bằng phút?":"How many minutes does {v0}'s {v1} take without counting the break?"}})
                {Check(Validate(c,unknown).NeedsReview,"Unproved inclusion, day or target auto-saved.");reviewCount++;}
                var endReference=clear with {Facts=[clear.Facts![0],new("fact_1",vi?
                    "Hoạt động này kết thúc vào {f2} giờ {f3} phút trong cùng một ngày với thời điểm bắt đầu {v1} của {v0}.":
                    "The session ends at {f2} hours {f3} minutes on the same day as the start of {v0}'s {v1}."),clear.Facts[2],clear.Facts[3]]};
                Check(Validate(c,endReference).NeedsReview,"An unproved reference to start in an end clause must review once, not retry.");reviewCount++;
                foreach(string condition in vi?new[]{"Buổi hoạt động không sang ngày hôm sau.","Buổi hoạt động không có buổi thứ hai."}
                    :new[]{"The session stays on one day without crossing midnight.","The session is not uninterrupted because it includes a break."})
                {
                    Check(Validate(c,clear with {Facts=[clear.Facts![0],clear.Facts[1],clear.Facts[2],new("fact_3",condition)]}).NeedsReview,
                        "A condition keyword under negation must not cause an unsupported retry.");reviewCount++;
                }
            }
            string grammar=OpenQuestionProse.Grammar(c);
            Check(grammar.Contains("word-gap ::=")&&grammar.All(ch=>ch<=127)&&!grammar.Contains(JsonSerializer.Serialize(d.Question)),"Break grammar closes full sentences.");
            for(int seed=0;seed<12;seed++)
            {
                var fresh=c.FreshFacts(new(91+seed));Check(fresh.SceneId==c.SceneId&&fresh.Story!.Schema==c.Story!.Schema&&fresh.OpenProse,"Fresh break schema changed.");
                CheckPractice(fresh,alt);freshCount++;
            }
            var generated=new ElementaryQuizGenerator(new(1873)).Generate(ArithmeticQuizMode.Essay,QuizProblemKind.Time,SameDayBreakPilot.Type,language,tier);
            Check(generated.ElementaryProblem!.StoryContextId!.StartsWith(SameDayBreakPilot.Prefix),"Direct practice fell back to midnight/second session.");
            Check(SameDayBreakCatalogue.IsProfile(new ReasoningStoryCycle().Next(c.Family,c.BankVariant,tier,language,creativeProse:true)),"Creative cycle missed break.");
        }
        var store=new QuestionBankStore(workspace.FilePath("worker.db3"));var runtime=new Runtime();var worker=new AiQuestionGenerationService(runtime,store);
        worker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,true,Family:BankQuestionFamily.Time,StoryVariant:(int)SameDayBreakPilot.Type,CreativeProse:true));
        await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        Check(worker.Snapshot.State==AiJobState.Completed&&runtime.Calls==3,"Break retry/review-once policy changed.");
        Check(worker.Snapshot.Items[0].State==AiItemState.PendingReview&&worker.Snapshot.Items[0].Attempts.Count==2&&worker.Snapshot.Items[1].State==AiItemState.Saved,"Invalid retry -> immediate review -> next saves failed.");
        var pending=(await store.GetPendingReviewsAsync()).Single();
        Check(!await store.ContainsProseAsync(new(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc)),"Pending break entered active bank.");
        Check(await store.ApproveReviewAsync(pending.Id),"Synthetic explicit approval failed.");
        using var excel=new MemoryStream();await store.ExportExcelAsync(excel);excel.Position=0;
        var restored=new QuestionBankStore(workspace.FilePath("restored.db3"));Check((await restored.ImportExcelAsync(excel)).Inserted==2,"Break Excel roundtrip failed.");
        var record=await restored.TakeReasoningAsync(BankQuestionFamily.Time,(int)SameDayBreakPilot.Type,CurriculumTier.ThreeStars,AppLanguage.Vietnamese);
        Check(record is not null&&SameDayBreakCatalogue.IsProfile(record.Contract),"Stored break profile lost.");
        CheckPractice(record!.Contract.FreshFacts(new(135)),record.Draft);
        Console.WriteLine($"PASS {freshCount} fresh one-break fixtures, {clearCount} clear variants, {badCount} invalid and {reviewCount} review-only mutations; same-day bounds, active-minute oracle, grading, worker/review-once and SQLite/Excel.");
    }
    private static void CheckMalformedList()
    {
        using var stream=typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent.en-US.json")!;
        var pack=QuizContentCatalog.ReadPack(stream);
        foreach(var (key,value) in new[]{("TargetRole","elapsed-minutes"),("RelationId","same-day-no-break"),("Question","How long is {v0}'s {v1} break {f4}?" )})
        {
            var rows=JsonNode.Parse(pack.Lists[SameDayBreakPilot.ListName].GetRawText())!;rows[0]![key]=value;
            bool rejected=false;try{QuizContentCatalog.Validate(new(){Version=pack.Version,Culture=pack.Culture,Texts=pack.Texts,Lists=new(pack.Lists){[SameDayBreakPilot.ListName]=JsonSerializer.SerializeToElement(rows)}});}
            catch(InvalidDataException){rejected=true;}Check(rejected,"Malformed break list accepted: "+key);
        }
    }
    private sealed class Runtime:IQuestionTextRuntime
    {
        public bool IsLoaded=>true;public string ModelName=>"one-break-fixture";public int Calls;
        public Task ReleaseAsync()=>Task.CompletedTask;
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken cancellationToken,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            Calls++;var d=ReasoningStoryCatalogue.Draft(c);
            if(Calls==1)d=d with {Question="Tổng thời gian {v1} của {v0} là bao nhiêu phút?"};
            if(Calls==2)d=d with {Question="{v1} của {v0} kéo dài bao nhiêu phút?"};
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
