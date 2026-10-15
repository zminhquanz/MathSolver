using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Globalization;

internal static class SameDayTimeTests
{
    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var relation in SameDayTimePilot.Relations(language))
            yield return ($"Time-{relation.Id}-{language}",ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)relation.Type,
                CurriculumTier.ThreeStars,language,new(713),SameDayTimePilot.Prefix+relation.Id+"/reading"));
    }
    internal static void CheckPractice(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        var data=SameDayTimePilot.Create(c.SceneId,(ElementaryQuizType)c.BankVariant,c.Language,c.Tier,new(c.Story!.Seed));
        Check(data.Start>=0&&data.End<1440&&data.Start<data.End&&data.Duration<=data.Context.MaximumMinutes,"Chronology or context duration broken.");
        var grader=new EssayAnswerValidator(new BasicArithmeticEngine());
        foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q=ReasoningStoryCatalogue.ToPractice(c,draft,mode);var p=q.ElementaryProblem!;
            Check(p.StoryContextId==c.SceneId&&!p.RequiresSolution&&p.Reasoning!.Steps.Count==1,"Time practice lost its relation or result-only policy.");
            int result=data.Relation.Id switch{"elapsed"=>data.End-data.Start,"start"=>data.End-data.Duration,_=>data.Start+data.Duration};
            Check(p.Reasoning!.Steps[0].Value==new ReducedFraction(result,1),"Worked time calculation disagrees with independent clock arithmetic.");
            if(data.Relation.Id=="elapsed")Check(p.Answers.Count==1&&(int)p.Answers[0].Value.Numerator==result,"Duration calculation wrong.");
            else Check(p.Answers.Count==2&&(int)p.Answers[0].Value.Numerator*60+(int)p.Answers[1].Value.Numerator==result,"Clock calculation wrong.");
            if(mode!=ArithmeticQuizMode.Essay)continue;
            string[] answers=data.Relation.Id=="elapsed"?[result.ToString(CultureInfo.InvariantCulture),result+(c.Language==AppLanguage.Vietnamese?" phút":" minutes")]
                :[c.Language==AppLanguage.Vietnamese?$"{result/60} giờ {result%60} phút":$"{result/60} hours {result%60} minutes",
                    c.Language==AppLanguage.Vietnamese?$"Giờ: {result/60}, phút: {result%60}":$"Hours: {result/60}, minutes: {result%60}",p.AnswerText];
            foreach(string answer in answers)
                Check(grader.ValidateSubmission(q,answer).IsCorrect,"Direct time answer rejected: "+c.SceneId+" / "+answer);
            string worked=ReasoningStoryCatalogue.Solution(c,draft);
            Check(grader.ValidateSubmission(q,worked,out var parsed).IsCorrect,"Worked time solution rejected: "+c.SceneId+" / "+worked+" / "+parsed);
            string wrong=data.Relation.Id=="elapsed"?(result+1)+" minutes":$"{result/60} hours {(result%60+1)%60} minutes";
            Check(!grader.ValidateSubmission(q,wrong).IsCorrect,"Wrong time accepted.");
        }
    }
    internal static async Task RunAsync()
    {
        QuizContentTests.Run();
        using var workspace=TestWorkspace.Temporary("same-day-open");
        int profiles=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var relation in SameDayTimePilot.Relations(language))
        foreach(string profile in SameDayTimePilot.Profiles(relation.Type,language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)relation.Type,tier,language,new(713),profile) with {OpenProse=true};
            Check(c.IsValid&&OpenQuestionProse.Supports(c),"Same-day profile unsupported.");
            var draft=ReasoningStoryCatalogue.Draft(c);
            Check(Validate(c,draft).IsValid,"Time reference rejected: "+profile);
            CheckPractice(c,draft);
            for(int seed=0;seed<8;seed++)
            {
                var fresh=c.FreshFacts(new(913+seed));
                Check(fresh.SceneId==c.SceneId&&fresh.Story!.Schema==c.Story!.Schema&&fresh.OpenProse,"Fresh time changed role/schema.");
                CheckPractice(fresh,draft);
            }
            var alternative=draft with {Facts=draft.Facts!.Select((f,i)=>f with {Text=relation.AlternativeFacts[i]}).ToArray(),Question=relation.AlternativeQuestion};
            Check(Validate(c,alternative).IsValid,"Equivalent time phrasing rejected: "+language+" / "+profile+" / "+Validate(c,alternative));
            var novel=alternative with {Facts=alternative.Facts!.Select((f,i)=>i==0?f with {Text=(language==AppLanguage.Vietnamese?"Theo ghi chép, ":"According to the records, ")+f.Text}:f).ToArray()};
            Check(Validate(c,novel).IsValid,"New verifiable time opening rejected.");
            var uninterrupted=draft with {Facts=draft.Facts!.Select((f,i)=>i==2?f with {Text=language==AppLanguage.Vietnamese?"Hoạt động diễn ra trong cùng ngày và không nghỉ.":"The activity runs on the same day without a break."}:f).ToArray()};
            Check(Validate(c,uninterrupted).IsValid,"Negated break was mistaken for an actual break.");
            var noInterruption=draft with {Facts=draft.Facts!.Select((f,i)=>i==2?f with {Text=language==AppLanguage.Vietnamese?"Hoạt động không bị gián đoạn trong cùng một ngày.":"The activity is not interrupted on the same day."}:f).ToArray()};
            Check(!PilotMeaningRules.TimeContradiction(noInterruption,language),"Negated interruption was mistaken for an interruption.");
            Check(!Validate(c,noInterruption).IsValid&&Validate(c,noInterruption).NeedsReview,"Unproved interruption phrasing should await review, not bypass proof.");
            foreach(string token in new[]{"solution-leads", "SolutionLeads", "solution leads", "given_a", "JSON"})
                Check(Validate(c,draft with {Question=draft.Question+" '"+token+"': xxx"}).ErrorCode=="InvalidText","Technical text was not rejected: "+token);
            foreach(string bad in new[]{"{f0}","{f1}"})
            {
                string raw=QuestionBankStore.SerializeDraft(draft).Replace(bad,"{f99}");
                Check(!BasicQuestionValidator.Validate(raw,c).IsValid,"Changed time variable accepted.");
            }
            var reversed=draft with {Facts=draft.Facts!.Select((f,i)=>i==0?f with {Text=f.Text.Replace("bắt đầu","kết thúc").Replace("starts","ends")}:f).ToArray()};
            if(QuestionBankStore.SerializeDraft(reversed)!=QuestionBankStore.SerializeDraft(draft))Check(!Validate(c,reversed).IsValid,"Start/end role reversal accepted.");
            foreach(var other in SameDayTimePilot.Relations(language).Where(r=>r.Id!=relation.Id))
                Check(!Validate(c,draft with {Question=other.Question}).IsValid,"Wrong time target accepted.");
            string unknown=language==AppLanguage.Vietnamese?"Một hoạt động khác diễn ra tiếp theo.":"A different activity follows this one.";
            Check(Validate(c,draft with {Question=draft.Question+" "+unknown}).NeedsReview,"Unknown time condition was trusted.");
            var condition=draft with {Facts=draft.Facts!.Select((f,i)=>i==2?f with {Text=language==AppLanguage.Vietnamese?"Hoạt động kết thúc vào ngày hôm sau.":"The activity ends the next day."}:f).ToArray()};
            Check(Validate(c,condition).ErrorCode=="ChangedRelationOrTarget","Next-day condition not rejected.");
            Check(!Validate(c,draft with {Facts=draft.Facts!.Select((f,i)=>i==2?f with {Text=language==AppLanguage.Vietnamese?"Hoạt động có nghỉ giữa giờ.":"The activity has a break."}:f).ToArray()}).IsValid,"Break accepted.");
            profiles++;
        }
        CheckDecimalMeaningRegression();
        foreach(var (_,seed) in NativeCases())
        {
            var c=seed with {OpenProse=true};var draft=ReasoningStoryCatalogue.Draft(c);
            var pendingDraft=draft with {Facts=draft.Facts!.Select((f,i)=>i==0?f with {Text="Zephyrus, "+f.Text}:f).ToArray()};
            Check(Validate(c,pendingDraft).NeedsReview,"Unknown time should need review.");
            var store=new QuestionBankStore(workspace.FilePath(c.Language+"-"+c.BankVariant+".db3"));
            string raw=QuestionBankStore.SerializeDraft(pendingDraft);
            var pending=PendingQuestionReview.Create(c,pendingDraft,raw,"synthetic-test","review");
            Check(await store.QueueReviewAsync(pending),"Cannot queue time review.");
            Check(await store.TakeReasoningAsync(BankQuestionFamily.Time,c.BankVariant,c.Tier,c.Language) is null,"Pending time leaked to active bank.");
            Check(await store.ApproveReviewAsync(pending.Id),"Time explicit approval failed.");
            var saved=await store.TakeReasoningAsync(BankQuestionFamily.Time,c.BankVariant,c.Tier,c.Language);
            Check(saved?.UserAuthored==true,"Time approval provenance lost.");CheckPractice(saved!.Contract.FreshFacts(new(87)),saved.Draft);
            using var excel=new MemoryStream();await store.ExportExcelAsync(excel);excel.Position=0;
            var restored=new QuestionBankStore(workspace.FilePath(c.Language+"-"+c.BankVariant+"-restored.db3"));
            Check((await restored.ImportExcelAsync(excel)).Inserted==1,"Time backup lost profile.");
            var record=await restored.TakeReasoningAsync(BankQuestionFamily.Time,c.BankVariant,c.Tier,c.Language);
            Check(record!.Contract.OpenProse&&record.UserAuthored,"Time backup lost open/review policy.");
        }
        foreach(var type in new[]{ElementaryQuizType.SameDayElapsed,ElementaryQuizType.SameDayStart,ElementaryQuizType.SameDayEnd})
        {
            var runtime=new Runtime();var store=new QuestionBankStore(workspace.FilePath(type+"-worker.db3"));
            var worker=new AiQuestionGenerationService(runtime,store);
            worker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,true,Family:BankQuestionFamily.Time,StoryVariant:(int)type,CreativeProse:true));
            await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
            Check(worker.Snapshot.State==AiJobState.Completed&&runtime.Releases==1,"Time worker did not finish/release.");
            Check(worker.Snapshot.Items[0].State==AiItemState.PendingReview&&worker.Snapshot.Items[0].Question is null,"Time worker trusted unknown.");
            Check(worker.Snapshot.Items[1].State==AiItemState.Saved,"Time worker did not continue saving verified relations.");
        }
        Console.WriteLine($"PASS {profiles} bilingual same-day time profiles; 8 fresh seeds, independent chronology/answers, all modes, result-only grading, relation/target/condition mutations, technical-text rejection, worker, review isolation and Excel.");
    }
    private static void CheckDecimalMeaningRegression()
    {
        foreach(var (_,seed) in DecimalOpenTests.NativeCases())
        {
            var c=seed with {OpenProse=true};var d=ReasoningStoryCatalogue.Draft(c);
            var novel=d with {Facts=d.Facts!.Select((f,i)=>i==0?f with {Text=f.Text.Replace(" có "," sở hữu ").Replace(" has "," possesses ")}:f).ToArray()};
            Check(Validate(c,novel).IsValid,"Decimal bound ownership equivalence not verified.");
            foreach(string junk in new[]{"solution-leads","solutionLeads","solution leads"})
                Check(Validate(c,d with {Question=d.Question+", '"+junk+"': xxx"}).ErrorCode=="InvalidText","Decimal technical leak accepted.");
            var ambiguous=d with {Question=d.Question+(c.Language==AppLanguage.Vietnamese?" Của một nhóm khác.":" Of another group.")};
            Check(!Validate(c,ambiguous).IsValid,"Ambiguous decimal target accepted.");
        }
    }
    private sealed class Runtime:IQuestionTextRuntime
    {
        public bool IsLoaded=>true;public string ModelName=>"same-day-fixture";public int Calls,Releases;
        public Task ReleaseAsync(){Releases++;return Task.CompletedTask;}
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken cancellationToken,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            Calls++;var d=ReasoningStoryCatalogue.Draft(c);
            if(Calls==1)d=d with {Facts=d.Facts!.Select((f,i)=>i==0?f with {Text="Zephyrus, "+f.Text}:f).ToArray()};
            else if(Calls<=3)d=d with {Question=d.Question+", 'solutionLeads': xxx"};
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
