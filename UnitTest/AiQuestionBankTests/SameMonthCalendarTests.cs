using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class SameMonthCalendarTests
{
    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var (context,index) in SameMonthCalendarPilot.Contexts(language).Select((c,i)=>(c,i)))
            yield return ($"CalendarSameMonth-{context.Id}-{language}",ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.Type,
                (CurriculumTier)(index+1),language,new(3251+index*41+(language==AppLanguage.English?17:0)),SameMonthCalendarPilot.Prefix+context.Id));
    }
    internal static void CheckPractice(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        var d=SameMonthCalendarPilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed));
        var start=new DateTime(d.Year,d.Month,d.StartDay);var end=new DateTime(d.Year,d.Month,d.EndDay);
        int includeStart=d.IncludeStart?1:0;
        int elapsed=(end-start).Days+includeStart;
        Check(elapsed>0&&elapsed==d.Elapsed&&start.Month==end.Month&&start.Year==end.Year,"Independent calendar oracle disagrees.");
        foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q=ReasoningStoryCatalogue.ToPractice(c,draft,mode);var p=q.ElementaryProblem!;
            Check(p.Type==SameMonthCalendarPilot.ProfileType(c.SceneId)&&p.StoryContextId==c.SceneId&&p.Reasoning!.ScenarioId==SameMonthCalendarPilot.ProfileRelation(c.SceneId),"Calendar profile changed.");
            Check(p.Answers.Count==1&&p.Answers[0].Value==new ReducedFraction(elapsed,1),"Calendar answer off by one.");
            var givens=p.Reasoning!.Givens.ToDictionary(g=>g.Role,g=>int.Parse(g.Value));
            Check(givens.Count==6&&givens["include-start"]==includeStart&&givens["include-end"]==1&&givens["month"]==d.Month&&givens["year"]==d.Year,"Calendar convention/date roles changed.");
            Check(!p.RequiresSolution&&!p.RequiresCalculation&&!p.ProblemText.Contains('{'),"Calendar direct answer or rendered slots lost.");
            if(mode==ArithmeticQuizMode.MultipleChoice)Check(p.ChoiceTexts is {Count:4}&&p.ChoiceTexts.Distinct().Count()==4&&p.ChoiceTexts.Contains(p.AnswerText),"Calendar choices invalid.");
            if(mode==ArithmeticQuizMode.TrueFalse)Check(q.PresentedEquationIsCorrect==(p.PresentedText==p.AnswerText),"Calendar truth flag invalid.");
            if(mode!=ArithmeticQuizMode.Essay)continue;
            var grader=new EssayAnswerValidator(new BasicArithmeticEngine());
            foreach(string answer in new[]{elapsed.ToString(),elapsed+" "+p.Answers[0].Unit,(c.Language==AppLanguage.Vietnamese?"Đáp số: ":"Answer: ")+p.AnswerText,
                ReasoningStoryCatalogue.Solution(c,draft),$"{d.EndDay} - {d.StartDay}"+(d.IncludeStart?" + 1":"")+$" = {elapsed} {p.Answers[0].Unit}"})
                Check(grader.ValidateSubmission(q,answer).IsCorrect,"Calendar direct/labeled/worked answer rejected: "+answer);
            foreach(string wrong in new[]{(elapsed+1).ToString(),(elapsed-1).ToString(),(-elapsed).ToString(),elapsed+(c.Language==AppLanguage.Vietnamese?" giờ":" hours")})
                Check(!grader.ValidateSubmission(q,wrong).IsCorrect,"Wrong count convention/direction/unit accepted: "+wrong);
        }
    }
    internal static async Task RunAsync()
    {
        QuizContentTests.Run();MalformedLists();SameMonthCalendarMeaningTests.Run();
        var context=SameMonthCalendarPilot.Contexts(AppLanguage.Vietnamese)[0];
        Check(new SameMonthCalendarPilot.Data(context,SameMonthCalendarPilot.Prefix+context.Id,10,15,10,2026).Elapsed==5,"10 -> 15 example must be five days.");
        Check(new SameMonthCalendarPilot.Data(context,SameMonthCalendarPilot.Prefix+context.Id,30,31,10,2026).Elapsed==1,"End-of-month boundary failed.");
        int clearCount=0,wrongCount=0,reviewCount=0,freshCount=0,lastDayCount=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var row in SameMonthCalendarPilot.Contexts(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.Type,tier,language,new(3251),SameMonthCalendarPilot.Prefix+row.Id) with {OpenProse=true};
            var d=ReasoningStoryCatalogue.Draft(c);
            var alternative=d with {Facts=row.AlternativeFacts.Select((s,i)=>new NarrativeClause("fact_"+i,s)).ToArray(),Question=row.AlternativeQuestion};
            var mixed=alternative with {Facts=[d.Facts![0],alternative.Facts![1],d.Facts[2]]};
            Check(c.IsValid&&OpenQuestionProse.Supports(c),"Same-month open contract invalid.");
            foreach(var clear in new[]{d,alternative,mixed})
            {
                var proof=Validate(c,clear);Check(proof.IsValid,"Clear calendar draft missed: "+proof);
                Check(QuestionBankStore.SerializeDraft(clear)==QuestionBankStore.SerializeDraft(proof.Draft!),"Calendar verifier rewrote prose.");
                CheckPractice(c,clear);clearCount++;
                foreach(var wrong in new[]{
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Tính cả ngày bắt đầu và ngày kết thúc.":"Include both the start date and the end date.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Không tính ngày bắt đầu, không tính ngày kết thúc.":"Exclude both the start date and the end date.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Chỉ tính ngày làm việc.":"Count only working days.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Không tính ngày bắt đầu, có tính ngày kết thúc và bỏ qua cuối tuần.":"Exclude the start date and include the end date, excluding weekends.")]},
                    clear with {Facts=[new("fact_0",clear.Facts![0].Text.Replace("{f0}","{f1}")),clear.Facts[1],clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],new("fact_1",clear.Facts[1].Text.Replace("{f1}","{f0}")),clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v0} {v3} {v1} vào ngày {f2} tháng {f1} năm {f3}.":"{v0} {v3} {v1} on day {f2} of month {f1} in year {f3}."),clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v0} {v3} {v1} vào ngày {f1} tháng {f2} năm {f3} tháng sau.":"{v0} {v3} {v1} on day {f1} of month {f2} in year {f3} next month."),clear.Facts[2]]},
                    clear with {Question=vi?"Ngày kết thúc {v4} của {v0} là ngày nào?":"What is the end date of {v0}'s {v4}?"},
                    clear with {Question=clear.Question+" JSON"}})
                {var result=Validate(c,wrong);Check(!result.IsValid&&!result.NeedsReview,"Definite calendar contradiction not rejected: "+result);wrongCount++;}
                foreach(var unknown in new[]{
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Hai mốc thời gian ở trong cùng tháng.":"Both dates are in the same month.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Có thể không tính ngày bắt đầu, có tính ngày kết thúc.":"Perhaps exclude the start date and include the end date.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Không chắc ngày bắt đầu không được tính, ngày kết thúc được tính.":"It is not known whether the start date is excluded and the end date is included.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Không tính ngày bắt đầu, có tính ngày kết thúc, có lẽ vậy.":"The start date is excluded and the end date is included, perhaps.")]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Không tính ngày bắt đầu, có tính ngày kết thúc và không bỏ qua ngày nghỉ.":"The start date is excluded and the end date is included without excluding holidays.")]},
                    clear with {Question=vi?"{v4} của {v0} kéo dài bao lâu?":"How long does {v0}'s {v4} last?"},
                    clear with {SolutionLeads=[new("step_0",vi?"Tính số ngày trôi qua":"Calculate the elapsed days")]}})
                {Check(Validate(c,unknown).NeedsReview,"Unproved calendar convention/target must review once.");reviewCount++;}
            }
            for(int seed=0;seed<20;seed++)
            {
                var fresh=c.FreshFacts(new(1291+seed));Check(fresh.OpenProse&&fresh.SceneId==c.SceneId&&fresh.Story!.Schema==c.Story!.Schema,"Fresh calendar schema changed.");
                CheckPractice(fresh,alternative);freshCount++;
                var data=SameMonthCalendarPilot.Create(fresh.SceneId,language,tier,new(fresh.Story!.Seed));
                if(data.EndDay==DateTime.DaysInMonth(data.Year,data.Month))lastDayCount++;
            }
            Check(new ElementaryQuizGenerator(new(3251)).Generate(ArithmeticQuizMode.Essay,QuizProblemKind.Time,SameMonthCalendarPilot.Type,language,tier).ElementaryProblem!.StoryContextId!.StartsWith(SameMonthCalendarPilot.Prefix),"Direct calendar generation escaped profile.");
            Check(SameMonthCalendarCatalogue.IsProfile(new ReasoningStoryCycle().Next(c.Family,c.BankVariant,tier,language,creativeProse:true)),"AI cycle missed same-month profile.");
            Check(OpenQuestionProse.Grammar(c).All(ch=>ch<=127),"Calendar grammar not ASCII escaped.");
        }
        Check(lastDayCount>0,"No month-end fixture covered.");
        using var workspace=TestWorkspace.Temporary("calendar-same-month");
        var store=new QuestionBankStore(workspace.FilePath("worker.db3"));var runtime=new Runtime();var worker=new AiQuestionGenerationService(runtime,store);
        worker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,true,Family:BankQuestionFamily.Time,StoryVariant:(int)SameMonthCalendarPilot.Type,CreativeProse:true));
        await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        Check(worker.Snapshot.State==AiJobState.Completed&&runtime.Calls==3,"Calendar retry/review-once policy changed.");
        Check(worker.Snapshot.Items[0].State==AiItemState.PendingReview&&worker.Snapshot.Items[0].Attempts.Count==2&&worker.Snapshot.Items[1].State==AiItemState.Saved,"Calendar invalid -> review once -> next verified flow failed.");
        var pending=(await store.GetPendingReviewsAsync()).Single();
        Check(!await store.ContainsProseAsync(new(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc)),"Unreviewed calendar entered active bank.");
        using var excel=new MemoryStream();await store.ExportExcelAsync(excel);excel.Position=0;
        var restored=new QuestionBankStore(workspace.FilePath("restored.db3"));Check((await restored.ImportExcelAsync(excel)).Inserted==1,"Verified calendar Excel roundtrip failed.");
        var record=await restored.TakeReasoningAsync(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.Type,CurriculumTier.ThreeStars,AppLanguage.Vietnamese);
        Check(record is not null&&SameMonthCalendarCatalogue.IsProfile(record.Contract),"Saved calendar profile lost.");CheckPractice(record!.Contract.FreshFacts(new(1451)),record.Draft);
        Console.WriteLine($"PASS {freshCount} fresh same-month fixtures ({lastDayCount} month-end), {clearCount} clear, {wrongCount} invalid, {reviewCount} review-only; DateTime oracle, grading, worker, quarantine, SQLite/Excel.");
    }
    private static void MalformedLists()
    {
        using var stream=typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent.en-US.json")!;
        var pack=QuizContentCatalog.ReadPack(stream);
        foreach(var (key,value) in new[]{("CountRule","include-both"),("TargetRole","end-day"),("RelationId","across-months"),("Question","How many days {v0} {v4} {f1}?")})
        {
            var rows=JsonNode.Parse(pack.Lists[SameMonthCalendarPilot.ListName].GetRawText())!;rows[0]![key]=value;
            bool rejected=false;
            try{QuizContentCatalog.Validate(new(){Version=pack.Version,Culture=pack.Culture,Texts=pack.Texts,Lists=new(pack.Lists){[SameMonthCalendarPilot.ListName]=JsonSerializer.SerializeToElement(rows)}});}
            catch(InvalidDataException){rejected=true;}
            Check(rejected,"Malformed same-month list accepted: "+key);
        }
    }
    private sealed class Runtime:IQuestionTextRuntime
    {
        public bool IsLoaded=>true;public string ModelName=>"same-month-fixture";public int Calls;
        public Task ReleaseAsync()=>Task.CompletedTask;
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken cancellationToken,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            Calls++;var d=ReasoningStoryCatalogue.Draft(c);
            if(Calls==1)d=d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2","Tính cả ngày bắt đầu và ngày kết thúc.")]};
            if(Calls==2)d=d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2","Hai mốc thời gian ở trong cùng tháng.")]};
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
