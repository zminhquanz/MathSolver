using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static partial class MotionExtendedTests
{
    private static async Task CheckStorageAndWorkerAsync()
    {
        using var workspace=TestWorkspace.Temporary("motion-extended");var store=new QuestionBankStore(workspace.FilePath("bank.db3"));
        foreach(var (_,c) in NativeCases())
        {
            var d=ReasoningStoryCatalogue.Draft(c);var unknown=d with {Question=d.Question+(c.Language==AppLanguage.Vietnamese?" Theo cách mới.":" Under a new interpretation.")};
            string raw=QuestionBankStore.SerializeDraft(unknown);Require(Validate(c,unknown).NeedsReview,"Unknown extended prose must wait for review.");
            var pending=PendingQuestionReview.Create(c,unknown,raw,"fixture","review");
            Require(await store.QueueReviewAsync(pending),"Cannot queue extended motion draft.");
            Require(!await store.QueueReviewAsync(pending),"Pending extended motion identity was not deduplicated.");
            var unverified=new ValidatedBankQuestion(c,unknown,raw,"fixture",DateTime.UtcNow);
            Require(!await store.ContainsProseAsync(unverified),"Pending extended motion prose entered the active bank.");
            Require(await store.TakeReasoningAsync(c.Family,c.BankVariant,c.Tier,c.Language) is not { } row||QuestionProseIdentity.Hash(row.Contract,row.Draft)!=pending.Id,"Pending prose selected for practice.");
            bool blocked=false;try{await store.InsertAsync(unverified);}catch(InvalidDataException){blocked=true;}catch(ArgumentException){blocked=true;}catch(InvalidOperationException e)when(e.Message=="SemanticReviewRequired"){blocked=true;}
            Require(blocked,"Direct insertion bypassed extended motion semantic quarantine.");
            var good=new ValidatedBankQuestion(c,d,QuestionBankStore.SerializeDraft(d),"fixture",DateTime.UtcNow);
            Require(await store.InsertAsync(good),"Cannot save extended motion reference.");
            Require(!await store.InsertAsync(good with {Contract=c.FreshFacts(new(987))}),"Fresh numbers bypassed extended motion prose deduplication.");
        }
        Require((await store.GetPendingReviewsAsync()).Count==12&&(await store.GetProseHashesAsync()).Count==12,"Extended motion pending/active tables lost isolation.");
        using var excel=new MemoryStream();await store.ExportExcelAsync(excel);excel.Position=0;
        var restored=new QuestionBankStore(workspace.FilePath("restored.db3"));var imported=await restored.ImportExcelAsync(excel);
        Require(imported.Inserted==12&&(await restored.GetPendingReviewsAsync()).Count==0,"Extended motion Excel round trip lost profiles or exported quarantine.");
        foreach(var (_,c) in NativeCases())
        {
            var row=await restored.TakeReasoningAsync(c.Family,c.BankVariant,c.Tier,c.Language);
            Require(row is not null&&row.Contract.OpenProse&&row.Contract.SceneId.StartsWith("motion-extended/",StringComparison.Ordinal),"SQLite/Excel lost extended motion profile identity.");
            CheckPractice(row!.Contract,row.Draft);
        }
        foreach(var type in new[]{MotionQuizType.Meeting,MotionQuizType.Chasing,MotionQuizType.River})
        {
            var runtime=new Runtime();var workerStore=new QuestionBankStore(workspace.FilePath("worker-"+type+".db3"));var worker=new AiQuestionGenerationService(runtime,workerStore);
            worker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.Vietnamese,2,true,Family:BankQuestionFamily.Motion,StoryVariant:(int)type,CreativeProse:true));
            await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
            var first=worker.Snapshot.Items[0];
            Require(worker.Snapshot.State==AiJobState.Completed&&runtime.Calls==3&&runtime.Releases==1,"Extended motion retry/review/save or runtime release changed.");
            Require(first.State==AiItemState.PendingReview&&first.Question is null&&first.Attempts.Count==2
                &&first.Attempts[0].ErrorCode=="ChangedRelationOrTarget"&&first.Attempts[1].ErrorCode=="SemanticReviewRequired"
                &&worker.Snapshot.Items[1].State==AiItemState.Saved&&worker.Snapshot.Items[1].Attempts.Count==1,"Known false extended prose must retry; unknown prose must review once and continue.");
            Require((await workerStore.GetPendingReviewsAsync()).Count==1&&(await workerStore.GetProseHashesAsync()).Count==1,"Worker quarantine leaked into active motion bank.");
        }
    }
    private sealed class Runtime:IQuestionTextRuntime
    {
        public int Calls,Releases;public bool IsLoaded=>true;public string ModelName=>"motion-extended-fixture";
        public Task ReleaseAsync(){Releases++;return Task.CompletedTask;}
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken token,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            token.ThrowIfCancellationRequested();Calls++;var d=ReasoningStoryCatalogue.Draft(c);
            if(Calls==1)d=d with {Facts=[d.Facts![0] with {Text="Không phải "+d.Facts[0].Text},..d.Facts.Skip(1)]};
            if(Calls==2)d=d with {Question=d.Question+" Theo cách mới."};
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
}
