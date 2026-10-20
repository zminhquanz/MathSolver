using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class FebruaryCommonCalendarTests
{
    internal const string Prefix="calendar-february-common/", ListName="FebruaryCommonCalendarOpen.Contexts", Relation="february-common-to-march-include-both";
    internal static ElementaryQuizType Type=>Enum.Parse<ElementaryQuizType>("FebruaryCommonInclusive");
    private static IReadOnlyList<SameMonthCalendarPilot.Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<SameMonthCalendarPilot.Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var (context,index) in Contexts(language).Select((c,i)=>(c,i)))
        {
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)Type,
                (CurriculumTier)(index+1),language,new(19731+index*41+(language==AppLanguage.English?17:0)),Prefix+context.Id);
            // Include both month lengths and a real one-day crossing in the LLM run.
            for(int seed=0;index<2;seed++)
            {
                var data=SameMonthCalendarPilot.Create(c.SceneId,language,c.Tier,new(seed));
                if(index==0&&(data.StartDay!=28||data.EndDay!=1)||index==1&&(data.StartDay!=26||data.EndDay!=3))continue;
                c=c with {Story=c.Story! with {Seed=seed}};break;
            }
            yield return ($"CalendarFebruaryCommon-{context.Id}-{language}",c);
        }
    }
    internal static void CheckPractice(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        var d=SameMonthCalendarPilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed));
        var start=new DateTime(d.Year,d.Month,d.StartDay);var end=new DateTime(d.Year,d.EndMonth,d.EndDay);
        int elapsed=(end-start).Days+1;
        Check(d.EndMonth==d.Month+1&&d.Month==2&&d.EndMonth==3&&d.MonthLength==28&&IsCommonYear(d.Year),"Calendar escaped consecutive non-February same-year months.");
        Check(elapsed>0&&elapsed==d.Elapsed,"Independent DateTime oracle disagrees.");
        foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q=ReasoningStoryCatalogue.ToPractice(c,draft,mode);var p=q.ElementaryProblem!;
            Check(p.Type==Type&&p.StoryContextId==c.SceneId&&p.Reasoning!.ScenarioId==Relation,"Adjacent profile changed.");
            Check(p.Answers.Count==1&&p.Answers[0].Value==new ReducedFraction(elapsed,1),"Adjacent answer off by one.");
            var g=p.Reasoning!.Givens.ToDictionary(x=>x.Role,x=>int.Parse(x.Value));
            Check(g.Count==8&&g["include-start"]==1&&g["include-end"]==1&&g["start-month"]==d.Month&&g["end-month"]==d.EndMonth&&g["start-month-length"]==d.MonthLength&&g["year"]==d.Year,"Calendar roles changed.");
            Check(!p.RequiresSolution&&!p.RequiresCalculation&&!p.ProblemText.Contains('{'),"Direct answer/rendering lost.");
            if(mode==ArithmeticQuizMode.MultipleChoice)Check(p.ChoiceTexts is {Count:4}&&p.ChoiceTexts.Distinct().Count()==4&&p.ChoiceTexts.Contains(p.AnswerText),"Choices invalid.");
            if(mode==ArithmeticQuizMode.TrueFalse)Check(q.PresentedEquationIsCorrect==(p.PresentedText==p.AnswerText),"Truth flag invalid.");
            if(mode!=ArithmeticQuizMode.Essay)continue;
            var grader=new EssayAnswerValidator(new BasicArithmeticEngine());
            foreach(string answer in new[]{elapsed.ToString(),elapsed+" "+p.Answers[0].Unit,(c.Language==AppLanguage.Vietnamese?"Đáp số: ":"Answer: ")+p.AnswerText,
                ReasoningStoryCatalogue.Solution(c,draft),$"{d.Expression} = {elapsed} {p.Answers[0].Unit}"})
                Check(grader.ValidateSubmission(q,answer).IsCorrect,"Direct/labeled/worked answer rejected: "+answer);
            foreach(string wrong in new[]{(elapsed+1).ToString(),(elapsed-1).ToString(),(-elapsed).ToString(),elapsed+(c.Language==AppLanguage.Vietnamese?" giờ":" hours")})
                Check(!grader.ValidateSubmission(q,wrong).IsCorrect,"Wrong convention/value/unit accepted: "+wrong);
        }
    }
    internal static async Task RunAsync()
    {
        Check(Enum.TryParse<ElementaryQuizType>("FebruaryCommonInclusive",out _),"Missing separate February common-year type.");
        QuizContentTests.Run();MalformedLists();
        var context=Contexts(AppLanguage.Vietnamese)[0];
        Check(new SameMonthCalendarPilot.Data(context,Prefix+context.Id,26,3,2,2027,3).Elapsed==6,"26 February 2027 -> 3 March 2027 including both dates must be six days.");
        foreach(int year in Enumerable.Range(2025,11).Where(IsCommonYear))
        {
            int length=DateTime.DaysInMonth(year,2);
            Check(new SameMonthCalendarPilot.Data(context,Prefix+context.Id,length,1,2,year,3).Elapsed==2,"Final date -> next first date including both dates must be two days.");
        }
        int clearCount=0,invalidCount=0,reviewCount=0,freshCount=0,boundary=0;var months=new HashSet<int>();var years=new HashSet<int>();
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var row in Contexts(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)Type,tier,language,new(19731),Prefix+row.Id) with {OpenProse=true};
            var d=ReasoningStoryCatalogue.Draft(c);
            var alt=d with {Facts=row.AlternativeFacts.Select((s,i)=>new NarrativeClause("fact_"+i,s)).ToArray(),Question=row.AlternativeQuestion};
            var mixed=alt with {Facts=[d.Facts![0],alt.Facts![1],d.Facts[2],alt.Facts[3]]};
            Check(c.IsValid&&OpenQuestionProse.Supports(c),"Adjacent contract invalid.");
            Check(SameMonthCalendarCatalogue.PromptRelation(c).Contains("INCLUDE both start and end")&&!SameMonthCalendarCatalogue.PromptRelation(c).Contains("EXCLUDE start"),"Inclusive prompt inherited elapsed convention.");
            Check(!OpenQuestionProse.Prompt(c).Contains("EXCLUDE start"),"Prompt mixed calendar conventions.");
            Check(!OpenQuestionProse.Prompt(c).Contains("30/31")&&!OpenQuestionProse.Prompt(c).Contains("30 or 31")&&!OpenQuestionProse.Prompt(c).Contains("30 hoặc 31"),"February prompt inherited non-February month lengths.");
            Check(!c.Story!.Schema.Equals(ReasoningStoryCatalogue.Create(c.Family,(int)SameMonthCalendarPilot.AdjacentType,tier,language,new(19731),SameMonthCalendarPilot.AdjacentPrefix+row.Id).Story!.Schema),"Inclusive and elapsed schemas collided.");
            foreach(var good in new[]{d,alt,mixed})
            {
                var proof=Validate(c,good);Check(proof.IsValid,"Clear adjacent draft missed: "+proof);
                Check(QuestionBankStore.SerializeDraft(good)==QuestionBankStore.SerializeDraft(proof.Draft!),"Verifier rewrote prose.");
                CheckPractice(c,good);clearCount++;
                BasicQuestionDraft Fact(int i,string text)=>good with {Facts=good.Facts!.Select((f,j)=>i==j?new NarrativeClause(f.Role,text):f).ToArray()};
                foreach(var bad in new[]{
                    Fact(3,vi?"Không tính ngày bắt đầu, có tính ngày kết thúc.":"Exclude the start date and include the end date."),
                    Fact(3,vi?"Không tính ngày bắt đầu, không tính ngày kết thúc.":"Exclude both the start date and the end date."),
                    Fact(3,vi?"Chỉ tính ngày làm việc.":"Count only working days."),
                    Fact(3,vi?"Tính cả ngày bắt đầu và ngày kết thúc nhưng bỏ qua cuối tuần.":"Include both the start date and the end date, excluding weekends."),
                    Fact(0,good.Facts![0].Text.Replace("{f2}","{f4}")),
                    Fact(1,good.Facts![1].Text.Replace("{f4}","{f2}")),
                    Fact(2,good.Facts![2].Text.Replace("{f2}","{f4}")),
                    Fact(2,vi?"Tháng {f2} không có {f5} ngày.":"Month {f2} does not have {f5} days."),
                    Fact(2,vi?"Tháng {f2} có {f5} ngày trong năm nhuận.":"Month {f2} has {f5} days in a leap year."),
                    Fact(2,good.Facts![2].Text.Replace("{f5}","29")),
                    Fact(0,good.Facts![0].Text.Replace("{f0}","29")),
                    Fact(1,good.Facts![1].Text.Replace("{f3}","2028")),
                    Fact(1,vi?"{v0} {v3} {v1} vào ngày {f4} tháng {f1} năm {f3}.":"{v0} {v3} {v1} on day {f4} of month {f1} in year {f3}."),
                    Fact(2,vi?"Tháng {f5} có {f2} ngày.":"Month {f5} has {f2} days."),
                    Fact(1,good.Facts![1].Text.TrimEnd('.')+(vi?" năm sau.":" next year.")),
                    Fact(1,good.Facts![1].Text.TrimEnd('.')+(vi?" cùng tháng.":" in the same month.")),
                    good with {Question=vi?"Ngày kết thúc {v4} của {v0} là ngày nào?":"What is the end date of {v0}'s {v4}?"},
                    good with {Question=good.Question+" JSON"}})
                {var result=Validate(c,bad);Check(!result.IsValid&&!result.NeedsReview,"Definite contradiction not rejected: "+result);invalidCount++;}
                foreach(var uncertain in new[]{
                    Fact(3,vi?"Có thể không tính ngày bắt đầu, có tính ngày kết thúc.":"Perhaps exclude the start date and include the end date."),
                    Fact(3,vi?"Không chắc ngày bắt đầu không được tính, ngày kết thúc được tính.":"It is not known whether the start date is excluded and the end date is included."),
                    Fact(3,vi?"Nếu không tính ngày bắt đầu, có tính ngày kết thúc.":"If the start date is excluded and the end date is included."),
                    Fact(3,vi?"Hai ngày thuộc hai tháng.":"The dates fall in two months."),
                    Fact(2,vi?"Có lẽ tháng {f2} có {f5} ngày.":"Perhaps month {f2} has {f5} days."),
                    Fact(0,(vi?"Không phải ":"It is not true that ")+good.Facts![0].Text),
                    Fact(1,good.Facts![1].Text.TrimEnd('.')+(vi?" tháng sau.":" next month.")),
                    good with {Question=vi?"{v0} thực sự sử dụng {v1} bao nhiêu ngày trong {v4}?":"For how many days did {v0} actually use {v1} during {v4}?"},
                    good with {Question=vi?"{v4} của {v0} kéo dài bao lâu?":"How long did {v0}'s {v4} last?"},
                    good with {SolutionLeads=[new("step_0",vi?"Tính số ngày":"Calculate the number of days")]}})
                {var result=Validate(c,uncertain);Check(!result.IsValid,"Unproved statement auto-approved: "+result);reviewCount++;}
            }
            for(int seed=0;seed<30;seed++)
            {
                var fresh=c.FreshFacts(new(1781+seed));Check(fresh.OpenProse&&fresh.SceneId==c.SceneId&&fresh.Story!.Schema==c.Story!.Schema,"Fresh schema changed.");
                CheckPractice(fresh,alt);freshCount++;
                var data=SameMonthCalendarPilot.Create(fresh.SceneId,language,tier,new(fresh.Story!.Seed));months.Add(data.Month);years.Add(data.Year);
                if(data.StartDay==data.MonthLength&&data.EndDay==1)boundary++;
            }
            var direct=new ElementaryQuizGenerator(new(19731)).Generate(ArithmeticQuizMode.Essay,QuizProblemKind.Time,Type,language,tier).ElementaryProblem!;
            Check(direct.StoryContextId!.StartsWith(Prefix),"Direct generator escaped adjacent profile.");
            Check(SameMonthCalendarCatalogue.IsProfile(new ReasoningStoryCycle().Next(c.Family,c.BankVariant,tier,language,creativeProse:true)),"AI cycle missed adjacent profile.");
            Check(OpenQuestionProse.Grammar(c).All(ch=>ch<=127),"Grammar not ASCII escaped.");
            var mismatch=c with {Story=c.Story! with {NarrativeId=SameMonthCalendarPilot.AdjacentInclusivePrefix+row.Id}};
            Check(!SameMonthCalendarCatalogue.IsProfile(mismatch)&&!mismatch.IsValid,"Cross-profile schema accepted.");
        }
        // Independent seeds cover every allowed pair and the single-day edge;
        // FreshFacts also applies its own seed mixer, so do not rely on chance.
        for(int seed=0;seed<2000;seed++)
        {
            var data=SameMonthCalendarPilot.Create(Prefix+context.Id,AppLanguage.Vietnamese,CurriculumTier.OneStar,new(seed));
            months.Add(data.Month);years.Add(data.Year);
            Check(data.Month==2&&data.EndMonth==3&&data.MonthLength==28&&IsCommonYear(data.Year),"February year/length escaped common-year scope.");
            Check(data.Elapsed==(new DateTime(data.Year,data.EndMonth,data.EndDay)-new DateTime(data.Year,data.Month,data.StartDay)).Days+1,"Date oracle seed sweep failed.");
            if(data.StartDay==data.MonthLength&&data.EndDay==1)boundary++;
        }
        Check(months.SetEquals([2])&&years.Count==9&&boundary>0,$"Missing allowed months ({months.Count}) or one-day crossing boundary ({boundary}).");
        using var workspace=TestWorkspace.Temporary("calendar-february-common");
        var store=new QuestionBankStore(workspace.FilePath("worker.db3"));var runtime=new Runtime();var worker=new AiQuestionGenerationService(runtime,store);
        worker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,true,Family:BankQuestionFamily.Time,StoryVariant:(int)Type,CreativeProse:true));
        await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        Check(worker.Snapshot.State==AiJobState.Completed&&runtime.Calls==3,"Retry/review-once policy changed.");
        Check(worker.Snapshot.Items[0].State==AiItemState.PendingReview&&worker.Snapshot.Items[0].Attempts.Count==2&&worker.Snapshot.Items[1].State==AiItemState.Saved,"Invalid -> review once -> next verified flow failed.");
        var pending=(await store.GetPendingReviewsAsync()).Single();
        Check(!await store.ContainsProseAsync(new(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc)),"Pending entered active bank.");
        using var excel=new MemoryStream();await store.ExportExcelAsync(excel);excel.Position=0;
        var restored=new QuestionBankStore(workspace.FilePath("restored.db3"));Check((await restored.ImportExcelAsync(excel)).Inserted==1,"Excel roundtrip failed.");
        Check(await restored.TakeReasoningAsync(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.AdjacentType,CurriculumTier.ThreeStars,AppLanguage.Vietnamese) is null,"Adjacent record leaked into same-month bank.");
        var saved=await restored.TakeReasoningAsync(BankQuestionFamily.Time,(int)Type,CurriculumTier.ThreeStars,AppLanguage.Vietnamese);
        Check(saved is not null,"Saved adjacent profile lost.");CheckPractice(saved!.Contract.FreshFacts(new(1451)),saved.Draft);
        Console.WriteLine($"PASS {freshCount} fresh common-February fixtures ({boundary} two-counted-day boundary crossings, all {years.Count} common years), {clearCount} clear, {invalidCount} invalid, {reviewCount} unproved; DateTime oracle, direct grading, worker, quarantine, SQLite/Excel.");
    }
    private static void MalformedLists()
    {
        using var stream=typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent.en-US.json")!;var pack=QuizContentCatalog.ReadPack(stream);
        foreach(var (key,value) in new[]{("CountRule","exclude-start-include-end"),("TargetRole","end-day"),("RelationId",SameMonthCalendarPilot.Relation),("Question","How many days {v0} {v4} {f1}?")})
        {
            var rows=JsonNode.Parse(pack.Lists[ListName].GetRawText())!;rows[0]![key]=value;bool rejected=false;
            try{QuizContentCatalog.Validate(new(){Version=pack.Version,Culture=pack.Culture,Texts=pack.Texts,Lists=new(pack.Lists){[ListName]=JsonSerializer.SerializeToElement(rows)}});}
            catch(InvalidDataException){rejected=true;}Check(rejected,"Malformed adjacent list accepted: "+key);
        }
    }
    private sealed class Runtime:IQuestionTextRuntime
    {
        public bool IsLoaded=>true;public string ModelName=>"february-common-fixture";public int Calls;
        public Task ReleaseAsync()=>Task.CompletedTask;
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken cancellationToken,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            Calls++;var d=ReasoningStoryCatalogue.Draft(c);
            if(Calls<=2)d=d with {Facts=d.Facts!.Select((f,i)=>i==3?new NarrativeClause(f.Role,Calls==1?"Không tính ngày bắt đầu, có tính ngày kết thúc.":"Hai mốc thời gian thuộc hai tháng."):f).ToArray()};
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static bool IsCommonYear(int year)=>year%4!=0||year%100==0&&year%400!=0;
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
