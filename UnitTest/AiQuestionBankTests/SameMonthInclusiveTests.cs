using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class SameMonthInclusiveTests
{
    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var (context,index) in SameMonthCalendarPilot.Contexts(language,true).Select((c,i)=>(c,i)))
        {
            int seed=8641+index*41+(language==AppLanguage.English?17:0);
            string profile=SameMonthCalendarPilot.InclusivePrefix+context.Id;
            var tier=(CurriculumTier)(index+1);
            // First context also tests actual equal endpoints through native AI.
            if(index==0)
            {
                while(true)
                {
                    var candidate=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.InclusiveType,tier,language,new(seed),profile);
                    var dates=SameMonthCalendarPilot.Create(profile,language,tier,new(candidate.Story!.Seed));
                    if(dates.StartDay==dates.EndDay)break;
                    seed++;
                }
            }
            yield return ($"CalendarInclusive-{context.Id}-{language}",ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,
                (int)SameMonthCalendarPilot.InclusiveType,tier,language,new(seed),profile));
        }
    }
    internal static async Task RunAsync()
    {
        QuizContentTests.Run();MalformedLists();
        int clear=0,review=0,invalid=0,fresh=0,equal=0,monthEnd=0;
        var context=SameMonthCalendarPilot.Contexts(AppLanguage.Vietnamese,true)[0];
        Check(new SameMonthCalendarPilot.Data(context,SameMonthCalendarPilot.InclusivePrefix+context.Id,10,15,10,2026).Elapsed==6,"Inclusive example is six days.");
        Check(new SameMonthCalendarPilot.Data(context,SameMonthCalendarPilot.InclusivePrefix+context.Id,31,31,10,2026).Elapsed==1,"Same date is one day, not zero or two.");
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var row in SameMonthCalendarPilot.Contexts(language,true))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.InclusiveType,tier,language,new(8641),SameMonthCalendarPilot.InclusivePrefix+row.Id) with {OpenProse=true};
            Check(c.IsValid&&OpenQuestionProse.Supports(c),"Inclusive profile not supported.");
            var d=ReasoningStoryCatalogue.Draft(c);
            var alternative=d with {Facts=row.AlternativeFacts.Select((s,i)=>new NarrativeClause("fact_"+i,s)).ToArray(),Question=row.AlternativeQuestion};
            var rephrased=alternative with {Facts=[alternative.Facts![0],alternative.Facts[1],new("fact_2",vi?"Đếm mọi ngày từ ngày bắt đầu đến hết ngày kết thúc.":"Count all calendar days from the start date through the end date, inclusive.")]};
            foreach(var valid in new[]{d,alternative,rephrased})
            {
                var result=Validate(c,valid);Check(result.IsValid,"Clear inclusive draft missed: "+result);
                Check(QuestionBankStore.SerializeDraft(result.Draft!)==QuestionBankStore.SerializeDraft(valid),"Verifier rewrote inclusive draft.");
                SameMonthCalendarTests.CheckPractice(c,valid);clear++;
                foreach(string wrong in vi?new[]{
                    "Không tính ngày bắt đầu, có tính ngày kết thúc.",
                    "Không tính ngày bắt đầu, không tính ngày kết thúc.",
                    "Tính ngày bắt đầu, không tính ngày kết thúc.",
                    "Chỉ tính ngày làm việc.",
                    "Tính cả ngày bắt đầu và ngày kết thúc nhưng bỏ qua cuối tuần.",
                    "Chỉ tính ngày kết thúc."}:new[]{
                    "Exclude the start date and include the end date.",
                    "Exclude both the start date and the end date.",
                    "Include the start date and exclude the end date.",
                    "Count only working days.",
                    "Include both the start date and the end date, excluding holidays.",
                    "Count only the end date."})
                {var v=Validate(c,Rule(valid,wrong));Check(!v.IsValid&&!v.NeedsReview,"Wrong inclusive count must reject: "+wrong);invalid++;}
                foreach(var wrong in new[]{
                    valid with {Question=vi?"Ngày kết thúc {v4} của {v0} là ngày nào?":"What is the end date of {v0}'s {v4}?"},
                    valid with {Facts=[new("fact_0",valid.Facts![0].Text.Replace("{f0}","{f1}")),valid.Facts[1],valid.Facts[2]]},
                    valid with {Facts=[valid.Facts![0],new("fact_1",vi?"{v0} {v3} {v1} vào ngày {f2} tháng {f1} năm {f3}.":"{v0} {v3} {v1} on day {f2} of month {f1} in year {f3}."),valid.Facts[2]]},
                    valid with {Facts=[valid.Facts![0],new("fact_1",vi?"{v0} {v3} {v1} vào ngày {f1} tháng {f2} năm {f3} tháng sau.":"{v0} {v3} {v1} on day {f1} of month {f2} in year {f3} next month."),valid.Facts[2]]}})
                {var v=Validate(c,wrong);Check(!v.IsValid&&!v.NeedsReview,"Changed calendar quantity/target must reject.");invalid++;}
                foreach(string uncertain in vi?new[]{
                    "Có thể tính cả ngày bắt đầu và ngày kết thúc.",
                    "Không chắc ngày bắt đầu và ngày kết thúc đều được tính.",
                    "Tính cả ngày bắt đầu và ngày kết thúc, có lẽ vậy.",
                    "Không được chỉ tính ngày kết thúc.",
                    "Tính mọi ngày từ ngày bắt đầu đến ngày kết thúc nếu không phải ngày nghỉ.",
                    "Chỉ đếm một số ngày từ ngày bắt đầu đến hết ngày kết thúc.",
                    "Hai ngày ở trong cùng tháng."}:new[]{
                    "Perhaps include both the start date and the end date.",
                    "It is not known whether both the start date and the end date are included.",
                    "Include both the start date and the end date, perhaps.",
                    "Do not count only the end date.",
                    "Count all calendar days from the start date through the end date unless it is a holiday.",
                    "Count some days from the start date through the end date, inclusive.",
                    "Both dates are in the same month."})
                {Check(Validate(c,Rule(valid,uncertain)).NeedsReview,"Qualified or missing inclusive rule must review once: "+uncertain);review++;}
                foreach(var unknown in new[]{
                    valid with {Question=vi?"{v4} của {v0} kéo dài bao lâu?":"How long does {v0}'s {v4} last?"},
                    valid with {Question=vi?"Số ngày sử dụng thực tế trong {v4} của {v0} là bao nhiêu?":"How many days of actual use did {v0}'s {v4} involve?"},
                    valid with {SolutionLeads=[new("step_0",vi?"Tính số ngày":"Calculate the days")]},
                    valid with {Facts=[new("fact_0",vi?"{v0} {v2} {v1} bắt đầu vào ngày {f0} tháng {f2} năm {f3}.":"{v0} {v2} {v1} started on day {f0} of month {f2} in year {f3}."),valid.Facts![1],valid.Facts[2]]}})
                {Check(Validate(c,unknown).NeedsReview,"Unproved inclusive target/event must review.");review++;}
            }
            var old=ReasoningStoryCatalogue.Create(c.Family,(int)SameMonthCalendarPilot.Type,tier,language,new(8641));
            var mismatched=c with {Story=c.Story! with {NarrativeId=old.Story!.NarrativeId}};
            Check(!SameMonthCalendarCatalogue.IsProfile(mismatched),"Mismatched calendar profile accepted.");
            Check(!mismatched.IsValid,"Cross-relation schema accepted.");
            for(int seed=0;seed<30;seed++)
            {
                var next=c.FreshFacts(new(1291+seed*7919));
                Check(next.Story!.Schema==c.Story!.Schema&&next.SceneId==c.SceneId,"Inclusive fresh schema changed.");
                SameMonthCalendarTests.CheckPractice(next,alternative);fresh++;
                var data=SameMonthCalendarPilot.Create(next.SceneId,language,tier,new(next.Story.Seed));
                if(data.StartDay==data.EndDay)equal++;
                if(data.EndDay==DateTime.DaysInMonth(data.Year,data.Month))monthEnd++;
            }
            var generated=new ElementaryQuizGenerator(new(8123)).Generate(ArithmeticQuizMode.Essay,QuizProblemKind.Time,SameMonthCalendarPilot.InclusiveType,language,tier).ElementaryProblem!;
            Check(generated.Type==SameMonthCalendarPilot.InclusiveType&&generated.StoryContextId!.StartsWith(SameMonthCalendarPilot.InclusivePrefix),"Direct inclusive generator escaped scope.");
            Check(SameMonthCalendarCatalogue.IsProfile(new ReasoningStoryCycle().Next(c.Family,c.BankVariant,tier,language,creativeProse:true)),"AI cycle missed inclusive type.");
            Check(OpenQuestionProse.Grammar(c).All(ch=>ch<=127),"Inclusive grammar not escaped.");
        }
        Check(equal>0&&monthEnd>0,$"Equal dates or month end not covered: {equal}, {monthEnd}.");
        foreach(var (_,c) in NativeCases())SameMonthCalendarTests.CheckPractice(c,ReasoningStoryCatalogue.Draft(c));
        using var workspace=TestWorkspace.Temporary("calendar-inclusive");
        var store=new QuestionBankStore(workspace.FilePath("worker.db3"));var runtime=new Runtime();var worker=new AiQuestionGenerationService(runtime,store);
        worker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,true,Family:BankQuestionFamily.Time,StoryVariant:(int)SameMonthCalendarPilot.InclusiveType,CreativeProse:true));
        await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        Check(worker.Snapshot.State==AiJobState.Completed&&runtime.Calls==3,"Inclusive retry/review-once flow changed.");
        Check(worker.Snapshot.Items[0].State==AiItemState.PendingReview&&worker.Snapshot.Items[0].Attempts.Count==2&&worker.Snapshot.Items[1].State==AiItemState.Saved,"Inclusive invalid -> review -> next verified flow failed.");
        var pending=(await store.GetPendingReviewsAsync()).Single();
        Check(!await store.ContainsProseAsync(new(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc)),"Pending inclusive entered active bank.");
        using var excel=new MemoryStream();await store.ExportExcelAsync(excel);excel.Position=0;
        var restored=new QuestionBankStore(workspace.FilePath("restored.db3"));Check((await restored.ImportExcelAsync(excel)).Inserted==1,"Inclusive Excel roundtrip failed.");
        Check(await restored.TakeReasoningAsync(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.Type,CurriculumTier.ThreeStars,AppLanguage.Vietnamese) is null,"Inclusive record leaked into elapsed relation.");
        var saved=await restored.TakeReasoningAsync(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.InclusiveType,CurriculumTier.ThreeStars,AppLanguage.Vietnamese);
        Check(saved is not null,"Inclusive record missing after import.");SameMonthCalendarTests.CheckPractice(saved!.Contract.FreshFacts(new(917)),saved.Draft);
        Console.WriteLine($"PASS inclusive calendar: {fresh} fresh fixtures ({equal} equal dates, {monthEnd} month-end), {clear} clear, {invalid} invalid, {review} review-only; three grading modes, separate relation, worker/SQLite/Excel quarantine.");
    }
    private static BasicQuestionDraft Rule(BasicQuestionDraft d,string text)=>d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",text)]};
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void MalformedLists()
    {
        using var stream=typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent.en-US.json")!;
        var pack=QuizContentCatalog.ReadPack(stream);
        foreach(var (key,value) in new[]{("CountRule","exclude-start-include-end"),("TargetRole","elapsed-days"),("RelationId",SameMonthCalendarPilot.Relation),("Question","How many days {v0} {v4} {f1}?")})
        {
            var rows=JsonNode.Parse(pack.Lists[SameMonthCalendarPilot.InclusiveListName].GetRawText())!;rows[0]![key]=value;
            bool rejected=false;
            try{QuizContentCatalog.Validate(new(){Version=pack.Version,Culture=pack.Culture,Texts=pack.Texts,Lists=new(pack.Lists){[SameMonthCalendarPilot.InclusiveListName]=JsonSerializer.SerializeToElement(rows)}});}
            catch(InvalidDataException){rejected=true;}
            Check(rejected,"Malformed inclusive list accepted: "+key);
        }
    }
    private sealed class Runtime:IQuestionTextRuntime
    {
        public bool IsLoaded=>true;public string ModelName=>"inclusive-fixture";public int Calls;
        public Task ReleaseAsync()=>Task.CompletedTask;
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken cancellationToken,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            Calls++;var d=ReasoningStoryCatalogue.Draft(c);
            if(Calls==1)d=Rule(d,"Không tính ngày bắt đầu, có tính ngày kết thúc.");
            if(Calls==2)d=Rule(d,"Hai mốc thời gian ở trong cùng tháng.");
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
