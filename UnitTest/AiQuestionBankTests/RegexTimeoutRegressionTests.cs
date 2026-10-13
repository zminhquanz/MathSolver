using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class RegexTimeoutRegressionTests
{
    internal static async Task RunAsync()
    {
        const string vi="Sau khi lấy ra {f0} {v0}, còn {f1} {v0}.";
        const string en="After removing {f0} {v0}, {f1} {v0} remain.";
        Check(EquivalentProseStructures.Expand([vi],AppLanguage.Vietnamese).Contains("Lượng còn lại sau khi lấy ra {f0} {v0} là {f1} {v0}."),"Repeated-unit transformation lost its slots.");
        Check(EquivalentProseStructures.Expand([en],AppLanguage.English).Contains("The amount remaining after removing {f0} {v0} is {f1} {v0}."),"English repeated-unit transformation failed.");
        foreach(var (text,language) in new[]{(vi,AppLanguage.Vietnamese),(en,AppLanguage.English)})
        {
            Check(EquivalentProseStructures.Expand([text.Replace("{f1} {v0}","{f1} {v1}")],language).Length==1,"Different repeated units were treated as equivalent.");
            string huge=new('a',100_000);
            Check(EquivalentProseStructures.Expand([huge],language).SequenceEqual([huge]),"An oversized clause was transformed.");
            Check(!SemanticProseRules.Matches(huge,[text],language),"Oversized prose was accepted.");
        }
        await Task.WhenAll(Enumerable.Range(0,8).Select(worker=>Task.Run(()=>
        {
            for(int i=0;i<100;i++)
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)ElementaryQuizType.SecondsConversion,
                    CurriculumTier.OneStar,i%2==0?AppLanguage.Vietnamese:AppLanguage.English,new Random(worker*100+i));
                var draft=ReviewedReasoningProse.NovelDrafts(c,new HashSet<string>()).First();
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft),c).IsValid,"Concurrent seconds/minutes validation failed.");
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft with{Question=draft.Question+" 999?"}),c).IsValid,"New data passed validation under load.");
            }
        })));

        using var workspace=TestWorkspace.Temporary("RegexTimeout");
        foreach(int failedCall in new[]{1,2})
        {
            var runtime=new TimeoutRuntime(failedCall);
            var store=new QuestionBankStore(workspace.FilePath("retry-"+failedCall+".db3"));
            var service=new AiQuestionGenerationService(runtime,store);
            service.Start(Options());await service.Completion.WaitAsync(TimeSpan.FromMinutes(2));
            Check(service.Snapshot.State==AiJobState.Completed&&service.Snapshot.Items.Count==10&&service.Snapshot.Items.All(i=>i.State==AiItemState.Saved),"A transient regex timeout stopped the batch.");
            var retry=service.Snapshot.Items.Single(i=>i.Attempts.Count==2);
            Check(retry.Attempts[0].ErrorCode=="TextProcessingTimeout"&&retry.Attempts[0].IsComplete
                &&retry.Attempts[0].RawJson==TimeoutRuntime.Partial&&retry.Attempts[0].ErrorDetails!.Contains("stage=generation")
                &&retry.Attempts[0].ErrorDetails!.Contains("pattern=")&&retry.Error is null,"Timeout retry lost diagnostics or kept a stale error.");
            Check(runtime.Preparations==1&&runtime.Releases==1&&!runtime.IsLoaded,"Retry did not release the model exactly once.");
            Check((await store.GetProseHashesAsync()).Count==10,"Partial or duplicate timeout output was saved.");
        }
        var failing=new TimeoutRuntime(0);
        var failedStore=new QuestionBankStore(workspace.FilePath("exhausted.db3"));
        var failed=new AiQuestionGenerationService(failing,failedStore);failed.Start(Options());await failed.Completion;
        Check(failed.Snapshot.State==AiJobState.Failed&&failed.Snapshot.Error=="RetriesExhausted"
            &&failed.Snapshot.Items.Count==1&&failed.Snapshot.Items[0].Attempts.Count==3
            &&failed.Snapshot.Items[0].Error=="TextProcessingTimeout"&&(await failedStore.GetProseHashesAsync()).Count==0
            &&failing.Releases==1&&!failing.IsLoaded,"Repeated timeouts bypassed retry limits, inserted partial output or retained weights.");
        Console.WriteLine("PASS 800 concurrent bilingual seconds/minutes profiles; unit-role guards, large inputs, timeout recovery after partial output/saved items, retry limits and model cleanup.");
    }

    internal static async Task RunModelBatchAsync(string path)
    {
        using var workspace=TestWorkspace.Artifacts("seconds-regex-batch","artifacts/verification/regex-timeout");
        await using var model=new ModelTestSession();
        await model.Runtime.SelectAsync(path);
        var store=new QuestionBankStore(workspace.FilePath("native-bank.db3"));
        var service=new AiQuestionGenerationService(model.Runtime,store);
        int saved=0;
        service.Changed+=(_,_)=>
        {
            var snapshot=service.Snapshot;
            int count=snapshot.Items.Count(i=>i.State==AiItemState.Saved);
            if(count>saved){saved=count;Console.WriteLine($"Real GGUF batch: saved {saved}/10.");}
        };
        service.Start(Options());
        await service.Completion.WaitAsync(TimeSpan.FromMinutes(20));
        await File.WriteAllTextAsync(workspace.FilePath("snapshot.json"),JsonSerializer.Serialize(service.Snapshot,new JsonSerializerOptions{WriteIndented=true}));
        Check(service.Snapshot.State==AiJobState.Completed&&service.Snapshot.Items.Count==10&&service.Snapshot.Items.All(i=>i.State==AiItemState.Saved),
            "Native screenshot-profile batch failed: "+service.Snapshot.Error+"; "+string.Join(",",service.Snapshot.Items.Select(i=>i.Number+":"+i.State+":"+i.Error)));
        Check(!model.Runtime.IsLoaded&&model.Runtime.CanGenerate,"Native batch retained weights or lost the selected model.");
        Check((await store.GetProseHashesAsync()).Count==10,"Native batch lost SQLite questions.");
        foreach(var item in service.Snapshot.Items)
        {
            Check(item.Attempts.Last().Metrics?.GeneratedTokens>0,"Native batch has no token evidence.");
            TimeFractionBankTests.Verify(item.Question!.Contract.FreshFacts(new Random(713)),item.Question.Draft);
        }
        Console.WriteLine("PASS real GGUF: Time/SecondsConversion/OneStar/Vietnamese, 10 auto-saved questions, streaming metrics, fresh math and released weights. Evidence: "+workspace.DirectoryPath);
    }
    private static AiGenerationOptions Options()=>new(ArithmeticOperation.Add,CurriculumTier.OneStar,AppLanguage.Vietnamese,10,true,
        Family:BankQuestionFamily.Time,StoryVariant:(int)ElementaryQuizType.SecondsConversion);
    private sealed class TimeoutRuntime(int failedCall):IQuestionTextRuntime
    {
        internal const string Partial="{\"facts\":[";
        public bool IsLoaded{get;private set;}public bool CanGenerate=>true;public string ModelName=>"regex-timeout-test";
        internal int Preparations,Releases,Calls;
        public Task PrepareAsync(CancellationToken token){Preparations++;IsLoaded=true;return Task.CompletedTask;}
        public Task ReleaseAsync(){Releases++;IsLoaded=false;return Task.CompletedTask;}
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken token,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
            =>GenerateNovelAsync(c,prompt,new HashSet<string>(),token,onText,onMetrics);
        public Task<string> GenerateNovelAsync(BasicQuestionContract c,string prompt,IReadOnlySet<string> excluded,CancellationToken token,
            Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            token.ThrowIfCancellationRequested();Calls++;
            if(failedCall==0||Calls==failedCall)
            {
                onMetrics?.Invoke(new(1,TimeSpan.Zero));onText?.Invoke(Partial);
                throw new RegexMatchTimeoutException(Partial,"simulated-timeout",TimeSpan.FromMilliseconds(100));
            }
            string raw=QuestionBankStore.SerializeDraft(ReviewedReasoningProse.NovelDrafts(c,excluded).First());
            onMetrics?.Invoke(new(3,TimeSpan.FromSeconds(1)));onText?.Invoke(raw);return Task.FromResult(raw);
        }
    }
}
