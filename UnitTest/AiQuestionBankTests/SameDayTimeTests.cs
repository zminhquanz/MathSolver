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
        {
            string context=relation.Id switch {"start"=>"sport","end"=>"craft",_=>"reading"};
            yield return ($"Time-{relation.Id}-{language}",ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)relation.Type,
                CurriculumTier.ThreeStars,language,new(713),SameDayTimePilot.Prefix+relation.Id+"/"+context));
        }
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var (relation,context) in new[]{("elapsed","travel"),("end","club")})
        {
            var type=relation=="elapsed"?ElementaryQuizType.SameDayElapsed:ElementaryQuizType.SameDayEnd;
            yield return ($"Time-{relation}-{context}-{language}",ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)type,
                CurriculumTier.ThreeStars,language,new(713),SameDayTimePilot.Prefix+relation+"/"+context));
        }
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
            string grammar=OpenQuestionProse.Grammar(c);
            Check(grammar.Contains("character ::=")&&!grammar.Contains(System.Text.Json.JsonSerializer.Serialize(draft.Question)),"Time grammar enumerates whole questions.");
            Check(grammar.Contains(System.Text.Json.JsonSerializer.Serialize(language==AppLanguage.Vietnamese?"{v1} của {v0}":"{v0}'s {v1}")),"Time grammar lost the owner/activity role group.");
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
            Check(Validate(c,noInterruption).IsValid,"Explicit uninterrupted same-day activity should be verified.");
            CheckOpenRolePhrasings(c,draft);
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
        foreach(var (name,seed) in NativeCases())
        {
            var c=seed with {OpenProse=true};var draft=ReasoningStoryCatalogue.Draft(c);
            var pendingDraft=draft with {Facts=draft.Facts!.Select((f,i)=>i==0?f with {Text="Zephyrus, "+f.Text}:f).ToArray()};
            Check(Validate(c,pendingDraft).NeedsReview,"Unknown time should need review.");
            var store=new QuestionBankStore(workspace.FilePath(name+".db3"));
            string raw=QuestionBankStore.SerializeDraft(pendingDraft);
            var pending=PendingQuestionReview.Create(c,pendingDraft,raw,"synthetic-test","review");
            Check(await store.QueueReviewAsync(pending),"Cannot queue time review.");
            Check(await store.TakeReasoningAsync(BankQuestionFamily.Time,c.BankVariant,c.Tier,c.Language) is null,"Pending time leaked to active bank.");
            Check(await store.ApproveReviewAsync(pending.Id),"Time explicit approval failed.");
            var saved=await store.TakeReasoningAsync(BankQuestionFamily.Time,c.BankVariant,c.Tier,c.Language);
            Check(saved?.UserAuthored==true,"Time approval provenance lost.");CheckPractice(saved!.Contract.FreshFacts(new(87)),saved.Draft);
            using var excel=new MemoryStream();await store.ExportExcelAsync(excel);excel.Position=0;
            var restored=new QuestionBankStore(workspace.FilePath(name+"-restored.db3"));
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
            Check(runtime.Calls==4&&worker.Snapshot.Items[0].Attempts.Count==1
                &&worker.Snapshot.Items[1].Attempts.Count==3&&worker.Snapshot.Items[1].Attempts.Take(2).All(a=>a.ErrorCode=="InvalidText"),
                "Time review must finish after one attempt; only the following invalid output should retry.");
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
    private static void CheckOpenRolePhrasings(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string relation=c.SceneId.Split('/')[1];
        string verb=vi?relation=="start"?"kết thúc":"bắt đầu":relation=="start"?"finishes":"begins";
        string moved=vi?$"Vào lúc {{f0}} giờ {{f1}} phút, {{v1}} của {{v0}} {verb}.":$"At {{f0}} hours and {{f1}} minutes, {{v0}}'s {{v1}} {verb}.";
        var novel=draft with {Facts=draft.Facts!.Select((f,i)=>i==0?f with {Text=moved}:f).ToArray()};
        Check(Validate(c,novel).IsValid,"Time-first clock clause was not verified: "+moved);
        Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(novel),c with {OpenProse=false}).IsValid,"Novel fixture already accepted by closed grammar.");
        CheckPractice(c,novel);
        foreach(var text in new[]{
            moved.Replace("{f0}","{temp}").Replace("{f1}","{f0}").Replace("{temp}","{f1}"),
            moved.Replace("{v0}","{temp}").Replace("{v1}","{v0}").Replace("{temp}","{v1}"),
            moved.Replace(verb,vi?relation=="start"?"bắt đầu":"kết thúc":relation=="start"?"begins":"finishes"),
            moved+(vi?" Sau đó có một hoạt động khác.":" A different activity follows.")})
        {
            var bad=novel with {Facts=novel.Facts!.Select((f,i)=>i==0?f with {Text=text}:f).ToArray()};
            Check(!Validate(c,bad).IsValid,"Clock roles changed despite identical variable set: "+text);
        }
        string question=vi?relation=="elapsed"?"{v1} của {v0} mất bao nhiêu phút?":$"Hỏi {{v1}} của {{v0}} sẽ {(relation=="start"?"bắt đầu":"kết thúc")} vào lúc mấy giờ, mấy phút?"
            :relation=="elapsed"?"What is the total duration of {v0}'s {v1} in minutes?":$"When does {{v0}}'s {{v1}} {(relation=="start"?"begin":"finish")} in hours and minutes?";
        Check(Validate(c,novel with {Question=question}).IsValid,"New target phrasing rejected.");
        if(relation=="elapsed")
        {
            var durationTarget=novel with {
                Question=vi?"{v1} của {v0} đã diễn ra trong bao nhiêu phút?":"How long does the {v1} for {v0} last in minutes?",
                SolutionLeads=[new("step_0",vi?"Khoảng thời gian hoạt động":"The total duration in minutes")]};
            Check(Validate(c,durationTarget).IsValid,"Explicit elapsed-minute target or equivalent lead was not proved.");
            Check(!Validate(c,durationTarget with {Question=vi?"{v1} của {v0} đã bắt đầu lúc mấy giờ, mấy phút?":"What time does the {v1} for {v0} begin?"}).IsValid,"Elapsed target changed to start clock.");
        }
        string negated=vi?"Hoạt động không có thời gian nghỉ trong cùng một ngày.":"The activity runs without any pauses on the same day.";
        var noRest=draft with {Facts=draft.Facts!.Select((f,i)=>i==2?f with {Text=negated}:f).ToArray()};
        Check(!PilotMeaningRules.TimeContradiction(noRest,c.Language),"Explicit negation of rest was misclassified as a break.");
        Check(Validate(c,noRest).NeedsReview,"Unproved rest phrasing bypassed field verification.");
        if(vi)
        {
            var linked=novel with {Facts=novel.Facts!.Select((f,i)=>i==2?f with {Text="Toàn bộ quá trình này diễn ra không nghỉ trong cùng một ngày."}
                :i==1&&relation!="elapsed"?f with {Text="Việc này kéo dài {f2} phút."}:f).ToArray()};
            Check(Validate(c,linked).IsValid,"Single-activity anaphora lost its duration/continuity binding.");
            Check(!Validate(c,linked with {Facts=linked.Facts!.Select((f,i)=>i==2?f with {Text="Toàn bộ quá trình này diễn ra không nghỉ trong hai ngày."}:f).ToArray()}).IsValid,"Anaphora allowed a different day scope.");
        }
        if(!vi)
        {
            var paraphrase=novel with {
                Facts=novel.Facts!.Select((f,i)=>i==2?f with {Text="This activity continues without interruption on the same day."}
                    :i==1&&relation!="elapsed"?f with {Text="The activity is {f2} minutes long."}:f).ToArray(),
                Question=relation=="elapsed"?"What is the total number of elapsed minutes for the {v1} for {v0}?"
                    :$"What is the {(relation=="start"?"start":"end")} time of the {{v1}} for {{v0}}?"};
            Check(Validate(c,paraphrase).IsValid,"Composed duration/clock paraphrase not verified.");
            foreach(string changed in new[]{"This activity continues without interruption throughout the day.","This activity continues with an interruption on the same day.","This activity continues without interruption on the next day."})
                Check(!Validate(c,paraphrase with {Facts=paraphrase.Facts!.Select((f,i)=>i==2?f with {Text=changed}:f).ToArray()}).IsValid,"Expanded continuity ignored a changed day/rest condition.");
        }
        if(relation!="elapsed")
        {
            string duration=vi?"Hoạt động này có tổng thời gian là {f2} phút.":"The time spent on this activity is {f2} minutes.";
            var nominal=novel with {Facts=novel.Facts!.Select((f,i)=>i==1?f with {Text=duration}:f).ToArray()};
            Check(Validate(c,nominal).IsValid,"Bound nominal duration was not verified.");
            Check(!Validate(c,nominal with {Facts=nominal.Facts!.Select((f,i)=>i==1?f with {Text=duration+(vi?" Sau đó có nghỉ.":" A break follows.")}:f).ToArray()}).IsValid,"Nominal duration bypassed condition validation.");
            string wrong=vi?"Thời gian nghỉ của hoạt động này là {f2} phút.":"The time spent resting is {f2} minutes.";
            Check(!Validate(c,nominal with {Facts=nominal.Facts!.Select((f,i)=>i==1?f with {Text=wrong}:f).ToArray()}).IsValid,"Break duration was accepted as activity duration.");
        }
        if(vi&&relation=="start")
            Check(!Validate(c,draft with {Facts=draft.Facts!.Select((f,i)=>i==0?f with {Text=f.Text.Replace("lúc","từ")}:f).ToArray()}).IsValid,"An ambiguous ending-from clock was trusted.");
        if(!vi)
            Check(!Validate(c,novel with {Question="What is the total duration in {v0} minutes of the {v1}?"}).IsValid,"Person mistaken for a duration unit.");
        foreach(string condition in vi?new[]{"Hoạt động diễn ra liên tục nhưng sang ngày hôm sau.","Hoạt động không liên tục trong cùng ngày.","Hoạt động nghỉ rồi tiếp tục trong cùng ngày."}
            :new[]{"The activity is continuous but ends the next day.","The activity does not run continuously on the same day.","The activity pauses then continues on the same day."})
            Check(!Validate(c,novel with {Facts=novel.Facts!.Select((f,i)=>i==2?f with {Text=condition}:f).ToArray()}).IsValid,"Continuity reversal accepted.");
        string wrongLead=vi?relation=="elapsed"?"Thời điểm kết thúc hoạt động":"Thời gian diễn ra hoạt động"
            :relation=="elapsed"?"Ending time of the activity":"Duration of the activity";
        Check(!Validate(c,novel with {SolutionLeads=[new("step_0",wrongLead)]}).IsValid,"Changed solution target accepted.");
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
