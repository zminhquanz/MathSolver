using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;
using System.Text.Json.Serialization;

// Explicit evidence replay only: the default suite must not depend on external artifacts.
internal static class PracticalPriorStorageTests
{
    internal static async Task RunAsync(string evidenceDirectory)
    {
        string attemptsPath=Path.Combine(Path.GetFullPath(evidenceDirectory),"attempts.json");
        using var attempts=JsonDocument.Parse(await File.ReadAllTextAsync(attemptsPath));
        var options=new JsonSerializerOptions();options.Converters.Add(new JsonStringEnumConverter());
        var selected=new[]{("MoneyChange-notebooks-English",1),("TriangleArea-card-Vietnamese",2),("TriangleArea-card-English",2)};
        var samples=selected.Select(selection=>
        {
            var row=attempts.RootElement.EnumerateArray().Single(value=>value.GetProperty("Case").GetString()==selection.Item1
                &&value.GetProperty("Number").GetInt32()==selection.Item2);
            var contract=row.GetProperty("Contract").Deserialize<BasicQuestionContract>(options)
                ??throw new InvalidDataException("Missing prior contract: "+selection.Item1);
            string raw=row.GetProperty("RawJson").GetString()??throw new InvalidDataException("Missing prior raw draft: "+selection.Item1);
            var proof=BasicQuestionValidator.Validate(raw,contract);
            Check(contract.OpenProse&&proof.IsValid,"Previously reviewed clear native draft still unproved: "+selection.Item1+"#"+selection.Item2+": "+proof);
            return new Sample(selection.Item1,selection.Item2,contract,proof.Draft!,raw);
        }).ToArray();

        using var workspace=TestWorkspace.Temporary("practical-prior-storage");
        string activePath=workspace.FilePath("active.db3");
        var store=new QuestionBankStore(activePath);
        foreach(var sample in samples)
        {
            var c=sample.Contract;var d=sample.Draft;
            PracticalOpenTests.CheckPractice(c,d);
            var fresh=c.FreshFacts(new(1837));
            Check(BasicQuestionValidator.Validate(sample.Raw,fresh).IsValid,"Prior native clause proof depended on old numeric facts: "+sample.Name);
            PracticalOpenTests.CheckPractice(fresh,d);
            var item=new ValidatedBankQuestion(c,d,sample.Raw,"prior-native-clear-replay",DateTime.UtcNow);
            Check(await store.InsertAsync(item),"Prior clear native prose failed active insertion: "+sample.Name);
            Check(!await store.InsertAsync(item)&&!await store.InsertAsync(item with {Contract=fresh}),"Prior clear duplicate/fresh facts bypassed prose deduplication: "+sample.Name);

            var unknown=Unproved(c,d);string unknownRaw=QuestionBankStore.SerializeDraft(unknown);
            var review=BasicQuestionValidator.Validate(unknownRaw,c);
            Check(review.NeedsReview,"Unknown prior-clause prefix must require review: "+sample.Name+": "+review);
            var pending=PendingQuestionReview.Create(c,unknown,unknownRaw,"prior-native-clear-replay",review.ErrorDetails??"review");
            Check(await store.QueueReviewAsync(pending)&&!await store.QueueReviewAsync(pending),"Prior unknown review queue/deduplication failed: "+sample.Name);
            var unreviewed=new ValidatedBankQuestion(c,unknown,unknownRaw,"prior-native-clear-replay",DateTime.UtcNow);
            Check(!await store.ContainsProseAsync(unreviewed),"Unknown prior prose leaked into active storage: "+sample.Name);
            bool blocked=false;
            try{await store.InsertAsync(unreviewed);}
            catch(Exception error) when(error is InvalidDataException or ArgumentException or InvalidOperationException){blocked=true;}
            Check(blocked&&!await store.ContainsProseAsync(unreviewed),"Direct insertion bypassed unknown prior semantic review: "+sample.Name);
        }
        Check((await store.GetProseHashesAsync()).Count==3&&(await store.GetPendingReviewsAsync()).Count==3,"Prior replay active/pending counts differ.");
        await CheckReopenedAsync(new QuestionBankStore(activePath),samples,"SQLite reopen");

        using var excel=new MemoryStream();await store.ExportExcelAsync(excel);excel.Position=0;
        string restoredPath=workspace.FilePath("restored.db3");
        var restored=new QuestionBankStore(restoredPath);
        Check((await restored.ImportExcelAsync(excel)).Inserted==3&&(await restored.GetProseHashesAsync()).Count==3,"Prior clear Excel roundtrip lost verified records.");
        Check((await restored.GetPendingReviewsAsync()).Count==0,"Excel promoted unknown prior prose.");
        await CheckReopenedAsync(new QuestionBankStore(restoredPath),samples,"Excel SQLite reopen");
        Check((await new QuestionBankStore(activePath).GetPendingReviewsAsync()).Count==3,"Reading/exporting prior evidence changed quarantined rows.");

        foreach(var sample in samples.DistinctBy(s=>(s.Contract.Family,s.Contract.BankVariant,s.Contract.Language)))
        {
            var c=sample.Contract;var runtime=new Runtime(sample);
            var workerStore=new QuestionBankStore(workspace.FilePath($"worker-{c.Family}-{c.Language}.db3"));
            var worker=new AiQuestionGenerationService(runtime,workerStore);
            worker.Start(new(ArithmeticOperation.Add,c.Tier,c.Language,2,true,Family:c.Family,StoryVariant:c.BankVariant,CreativeProse:true));
            await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
            Check(worker.Snapshot.State==AiJobState.Completed&&runtime.Calls==3&&runtime.Releases==1,"Prior-native worker must retry invalid, review unknown once, save next and release once: "+sample.Name);
            Check(worker.Snapshot.Items.Count==2&&worker.Snapshot.Items[0].State==AiItemState.PendingReview
                &&worker.Snapshot.Items[0].Attempts.Count==2&&worker.Snapshot.Items[0].Attempts[0].ErrorCode=="InvalidFields"
                &&worker.Snapshot.Items[0].Attempts[1].ErrorCode=="SemanticReviewRequired"
                &&worker.Snapshot.Items[1].State==AiItemState.Saved&&worker.Snapshot.Items[1].Attempts.Count==1,
                "Prior-native retry/review/save dispositions changed: "+sample.Name);
            var pending=(await workerStore.GetPendingReviewsAsync()).Single();
            Check(!await workerStore.ContainsProseAsync(new(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc))
                &&(await workerStore.GetProseHashesAsync()).Count==1,"Prior-native worker promoted pending prose: "+sample.Name);
            var saved=await new QuestionBankStore(workspace.FilePath($"worker-{c.Family}-{c.Language}.db3"))
                .TakeReasoningAsync(c.Family,c.BankVariant,c.Tier,c.Language);
            Check(saved is not null&&QuestionBankStore.ValidateSavedQuestion(saved).IsValid
                &&QuestionBankStore.SerializeDraft(saved.Draft)==QuestionBankStore.SerializeDraft(sample.Draft),"Prior-native worker saved different/unverified prose: "+sample.Name);
            PracticalOpenTests.CheckPractice(saved!.Contract.FreshFacts(new(2917)),saved.Draft);
        }
        Console.WriteLine("PASS three unchanged prior clear native drafts: fresh grading, isolated active SQLite/deduplication/reopen/Excel, unknown quarantine and three family/language worker retry-review-save routes.");
    }

    private static async Task CheckReopenedAsync(QuestionBankStore store,IReadOnlyList<Sample> samples,string phase)
    {
        foreach(var sample in samples)
        {
            var c=sample.Contract;
            var saved=await store.TakeReasoningAsync(c.Family,c.BankVariant,c.Tier,c.Language);
            Check(saved is not null&&QuestionBankStore.ValidateSavedQuestion(saved).IsValid
                &&QuestionBankStore.SerializeDraft(saved.Draft)==QuestionBankStore.SerializeDraft(sample.Draft),phase+" changed prior clear prose: "+sample.Name);
            Check(!await store.InsertAsync(saved!),phase+" lost prior prose deduplication: "+sample.Name);
            PracticalOpenTests.CheckPractice(saved!.Contract.FreshFacts(new(2719)),saved.Draft);
        }
    }
    private static BasicQuestionDraft Unproved(BasicQuestionContract c,BasicQuestionDraft draft)=>draft with
    {
        Facts=draft.Facts!.Select((fact,index)=>index==0?fact with
            {Text=(c.Language==AppLanguage.Vietnamese?"Theo ghi chú mới, ":"According to a new note, ")+fact.Text}:fact).ToArray()
    };
    private sealed record Sample(string Name,int Number,BasicQuestionContract Contract,BasicQuestionDraft Draft,string Raw);
    private sealed class Runtime(Sample sample):IQuestionTextRuntime
    {
        public bool IsLoaded=>true;public string ModelName=>"prior-native-clear-worker-fixture";public int Calls,Releases;
        public Task ReleaseAsync(){Releases++;return Task.CompletedTask;}
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken cancellationToken,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            cancellationToken.ThrowIfCancellationRequested();Calls++;
            if(Calls==1)return Task.FromResult("{}");
            if(Calls==2)return Task.FromResult(QuestionBankStore.SerializeDraft(Unproved(c,sample.Draft)));
            if(Calls==3)return Task.FromResult(sample.Raw);
            throw new InvalidOperationException("Unexpected retry after prior-native clear draft: "+sample.Name);
        }
    }
    private static void Check(bool condition,string message)=>TestAssert.That(condition,message);
}
