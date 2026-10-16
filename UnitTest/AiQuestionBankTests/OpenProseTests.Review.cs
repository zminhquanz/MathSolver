using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static partial class OpenProseTests
{
    private static BasicQuestionDraft Unverified(BasicQuestionContract c)
    {
        var d=OpenQuestionProse.Example(c);
        string opening=c.Language==AppLanguage.Vietnamese?"Từ sổ ghi chép của lớp, ":"From the new classroom log, ";
        return d.Facts is null?d with {GivenA=opening+d.GivenA}:d with {Facts=d.Facts.Select((f,i)=>i==0?f with {Text=opening+f.Text}:f).ToArray()};
    }

    private sealed class ReviewRuntime(bool invalidFirst=false,bool allReview=false) : IQuestionTextRuntime
    {
        public bool IsLoaded=>true;
        public string ModelName=>"isolated-review-fixture";
        public int Calls,Releases;
        public Task ReleaseAsync(){Releases++;return Task.CompletedTask;}
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken cancellationToken,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            cancellationToken.ThrowIfCancellationRequested();Calls++;
            if(invalidFirst&&Calls==1)return Task.FromResult("{}");
            var draft=allReview||Calls==(invalidFirst?2:1)?Unverified(c):OpenQuestionProse.Example(c);
            if(allReview)
            {
                string prefix="Zephyrus "+(char)('A'+Calls)+", ";
                draft=draft.Facts is null?draft with {GivenA=prefix+draft.GivenA}
                    :draft with {Facts=draft.Facts.Select((f,i)=>i==0?f with {Text=prefix+f.Text}:f).ToArray()};
            }
            string raw=QuestionBankStore.SerializeDraft(draft);onText?.Invoke(raw);return Task.FromResult(raw);
        }
    }

    private static async Task CheckReviewStorageAndWorkerAsync()
    {
        using var workspace=TestWorkspace.Temporary("OpenProseReview");
        foreach(bool auto in new[]{false,true})
        {
            string path=workspace.FilePath("review-"+auto+".db3");
            var store=new QuestionBankStore(path);var runtime=new ReviewRuntime();var worker=new AiQuestionGenerationService(runtime,store);
            worker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,auto,new(QuestionKnowledgeGroup.Objects),CreativeProse:true));
            await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
            Check(worker.Snapshot.State==AiJobState.Completed && !worker.Completion.IsFaulted,"Reviewable prose stopped the batch or threw.");
            Check(runtime.Calls==2 && runtime.Releases==1,"An inconclusive question was retried or model cleanup changed.");
            var first=worker.Snapshot.Items[0];
            Check(first.State==AiItemState.PendingReview && first.Question is null && first.Review is not null,"Unverified prose became usable.");
            Check(first.Attempts.Count==1&&first.Attempts[0].ErrorCode=="SemanticReviewRequired"
                &&worker.Snapshot.Items[1].Attempts.Count==1,"Review should finish its item immediately and generate the next one.");
            Check(worker.Snapshot.Items[1].State==(auto?AiItemState.Saved:AiItemState.Ready),"Batch did not continue after a pending question.");
            await worker.InsertAsync(1);
            var pending=(await new QuestionBankStore(path).GetPendingReviewsAsync()).Single();
            Check(!await store.ContainsProseAsync(new(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc)),"Pending prose entered the practice bank.");
            var selected=await store.TakeForProfileAsync(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,new(QuestionKnowledgeGroup.Objects));
            Check(selected is null || QuestionProseIdentity.Hash(selected.Contract,selected.Draft)!=pending.Id,"Practice selected a quarantined question.");
            Check(!await store.QueueReviewAsync(pending),"Repeated pending prose was not deduplicated.");
            Check((await store.GetPendingProseHashesAsync()).SetEquals(new[]{pending.Id}),"Pending identity pool is incomplete.");
            var raw=new ValidatedBankQuestion(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc);
            bool blocked=false;try{await store.InsertAsync(raw);}catch(InvalidDataException){blocked=true;}catch(ArgumentException){blocked=true;}
            catch(InvalidOperationException e) when(e.Message=="SemanticReviewRequired"){blocked=true;}
            Check(blocked,"Direct insertion bypassed automatic semantic verification.");
            Check(await store.ApproveReviewAsync(pending.Id),"Explicit human approval failed.");
            worker.ResolveReview(pending.Id,true);
            Check(worker.Snapshot.Items[0] is {State:AiItemState.Saved,Question.UserAuthored:true,Review:null},"Human review did not update the batch preview.");
            Check((await store.GetPendingReviewsAsync()).Count==0 && await store.ContainsProseAsync(raw),"Approval did not move the draft atomically.");
            using(var db=new SQLite.SQLiteConnection(path,SQLite.SQLiteOpenFlags.ReadOnly))
                Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM QuestionUserAuthorship")==1,"Human-review provenance was lost.");
            Check(!await store.ApproveReviewAsync(pending.Id),"A double approval inserted again.");
            var approved=raw with {UserAuthored=true};
            Check(QuestionBankStore.ValidateSavedQuestion(approved).IsValid && !QuestionBankStore.ValidateSavedQuestion(raw).IsValid,"Human and automatic verification provenance became indistinguishable.");
            var fresh=approved with {Contract=approved.Contract.FreshFacts(new(879))};
            Check(fresh.Contract.OpenProse && QuestionBankStore.ValidateSavedQuestion(fresh).IsValid,"Fresh facts lost a human review decision.");
            var d=pending.Draft with {GivenA="Theo một bản ghi khác, "+pending.Draft.GivenA};
            var second=PendingQuestionReview.Create(pending.Contract,d,QuestionBankStore.SerializeDraft(d),runtime.ModelName,"review");
            Check(await store.QueueReviewAsync(second) && await store.RejectReviewAsync(second.Id),"Discarding a pending question failed.");
            Check(!await store.ContainsProseAsync(new(second.Contract,second.Draft,second.RawJson,second.ModelName,second.CreatedUtc)),"Discarded question became usable.");
            Check(await store.QueueReviewAsync(second),"Could not restore a separate pending fixture.");
            await store.DeleteAllAsync();
            Check((await store.GetPendingReviewsAsync()).Count==0 && (await store.GetProseHashesAsync()).Count==0,"Delete-all left review drafts behind.");
        }
        var pendingStore=new QuestionBankStore(workspace.FilePath("all-review.db3"));var pendingRuntime=new ReviewRuntime(allReview:true);
        var pendingWorker=new AiQuestionGenerationService(pendingRuntime,pendingStore);
        pendingWorker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,3,true,new(QuestionKnowledgeGroup.Objects),CreativeProse:true));
        await pendingWorker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        Check(pendingWorker.Snapshot.State==AiJobState.Completed&&pendingRuntime.Calls==3&&pendingRuntime.Releases==1
            &&pendingWorker.Snapshot.Items.All(i=>i.State==AiItemState.PendingReview&&i.Attempts.Count==1&&i.Question is null),
            "A batch of review-only questions was retried, stopped, or automatically approved.");
        Check((await pendingStore.GetPendingReviewsAsync()).Count==3&&(await pendingStore.GetProseHashesAsync()).Count==0,
            "Review-only batch did not persist three isolated drafts.");
        var retryStore=new QuestionBankStore(workspace.FilePath("invalid-then-review.db3"));var retryRuntime=new ReviewRuntime(invalidFirst:true);
        var retryWorker=new AiQuestionGenerationService(retryRuntime,retryStore);
        retryWorker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,true,new(QuestionKnowledgeGroup.Objects),CreativeProse:true));
        await retryWorker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        Check(retryWorker.Snapshot.State==AiJobState.Completed&&retryRuntime.Calls==3&&retryRuntime.Releases==1,"Invalid output did not retry before review/continuation.");
        var retried=retryWorker.Snapshot.Items[0];
        Check(retried.State==AiItemState.PendingReview&&retried.Attempts.Count==2&&retried.Attempts[0].ErrorCode=="InvalidFields"
            &&retried.Attempts[1].ErrorCode=="SemanticReviewRequired"&&retryWorker.Snapshot.Items[1].State==AiItemState.Saved,
            "An inconclusive retry should be queued immediately, preserving the invalid attempt and the next saved item.");
        Check((await retryStore.GetPendingReviewsAsync()).Count==1,"Retried review draft was not persisted exactly once.");
        var cancelledStore=new QuestionBankStore(workspace.FilePath("cancelled.db3"));var cancelledRuntime=new ReviewRuntime();var cancelled=new AiQuestionGenerationService(cancelledRuntime,cancelledStore);
        cancelled.Changed+=(_,_)=>{if(cancelled.Snapshot.Items.Any(i=>i.State==AiItemState.PendingReview))cancelled.Stop();};
        cancelled.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,true,new(QuestionKnowledgeGroup.Objects),CreativeProse:true));
        await cancelled.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        Check(cancelled.Snapshot.State==AiJobState.Stopped && cancelled.Snapshot.Items[0].State==AiItemState.PendingReview
            && (await cancelledStore.GetPendingReviewsAsync()).Count==1 && cancelledRuntime.Calls==1 && cancelledRuntime.Releases==1,"Stopping after immediate review lost the completed draft or started another generation.");
        Console.WriteLine("PASS one-attempt review quarantine, invalid-output retry then immediate review, manual provenance/approval, batch continuation, duplicate pending drafts, insertion bypass rejection, discard/delete-all and cancellation.");
    }
}
