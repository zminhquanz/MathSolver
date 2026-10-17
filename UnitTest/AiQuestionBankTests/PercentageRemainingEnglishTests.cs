using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class PercentageRemainingEnglishTests
{
    internal static async Task RunAsync()
    {
        int cases=0;
        foreach(var (_,seed) in PercentageRemainingOpenTests.NativeCases().Where(x=>x.Contract.Language==AppLanguage.English))
        {
            var c=seed with {OpenProse=true};Check(c,ReasoningStoryCatalogue.Draft(c));cases++;
        }
        await CheckRoutingAsync();
        Console.WriteLine($"PASS focused English follow-up: {cases} contexts, 90 original/target wording combinations and their wrong-role/base/target mutations, aliases, punctuation, exact prior native cases and review/retry routing.");
    }

    internal static void Check(BasicQuestionContract c,BasicQuestionDraft reference)
    {
        string[] initialClauses=["{v0} started with {f0} {v1}.","{v0} possessed {f0} {v1} at the start.",
            "{v0} owned {f0} {v1} initially.","{v0} held {f0} {v1} originally.","{v0} starts with {f0} {v1}."];
        string[] targets=["How many {v1} does {v0} possess now?","How many {v1} does {v0} own now?",
            "How many {v1} does {v0} hold now?","How many {v1} does {v0} have after the gift?",
            "How many {v1} does {v0} still possess?","How many {v1} does {v0} still hold?"];
        foreach(string first in initialClauses)
        foreach(string target in targets)
        {
            var d=reference with {Facts=[new("fact_0",first),new("fact_1","{v0} gave {f1} of the initial {v1} to {v2}.")],Question=target};
            var result=Validate(c,d);
            Require(result.IsValid,"Clear original count / present remainder clause rejected: "+result);
            Require(result.Draft!.Facts![0].Text==first&&result.Draft.Question==target,"Proof changed the stored wording.");
            foreach(string share in new[]{"{v2} gave {f1} of the original {v1} to {v0}.",
                "{v0} gave {f1} of the remaining {v1} to {v2}.",
                "{v0} retains {f1} of the original {v1} and gives the rest to {v2}.",
                "{v0} gave {f1} of the original {v1} to {v2} again."})
                Require(Validate(c,d with {Facts=[d.Facts[0],new("fact_1",share)]}).ErrorCode=="ChangedRelationOrTarget",
                    "A newly proved question hid a reversed gift, remaining base, retained rate or second transfer.");
            foreach(string wrong in new[]{"Before the gift, "+target,"How many {v1} did {v0} give away?"})
                Require(Validate(c,d with {Question=wrong}).ErrorCode=="ChangedRelationOrTarget","Original/donated target accepted.");
            Require(!Validate(c,d with {Question=target.Replace("does {v0}","does {v0} not")}).IsValid,"Negated target accepted.");
            Require(Validate(c,d with {Facts=[d.Facts[0],new("fact_1","{v0} gave {f1} of their original {v1} to {v2}.")]}).NeedsReview,
                "A proved target hid an unresolved original-stock owner.");
        }
        // Reproduce the exact three English outcomes of the previous native run.
        var previousClear=reference with {Facts=[new("fact_0",initialClauses[0]),new("fact_1","{v0} gave {f1} of the initial {v1} to {v2}.")],Question=targets[0]};
        Require(Validate(c,previousClear).IsValid,"Former clear native candidate remains unproved.");
        PercentageRemainingOpenTests.CheckPractice(c,previousClear);
        foreach(string caption in new[]{"Number of {v1} remaining for {v0}?","Number of {v1} remaining with {v0}","Count of {v1} left with {v0}?"})
        {
            var fragment=previousClear with {Facts=[new("fact_0",initialClauses[1]),previousClear.Facts![1]],Question=caption};
            var result=Validate(c,fragment);
            Require(result.NeedsReview&&result.ErrorDetails!.Contains("complete question",StringComparison.Ordinal),"A caption must remain an editorial review.");
        }
        foreach(string notation in new[]{"f(f)","g(x)","n ( n )","q(a,b)"})
        {
            var technical=previousClear with {Facts=[new("fact_0","The total count of {v1} held by {v0} was "+notation+" items initially, where "+notation+" equals {f0}."),previousClear.Facts![1]]};
            Require(Validate(c,technical).ErrorCode=="InvalidText","Invented function notation must reject, not just miss a proof.");
            Require(Validate(c,previousClear with {Question=targets[0].TrimEnd('?')+" using "+notation+"?"}).ErrorCode=="InvalidText",
                "Function notation in the question escaped validation.");
        }
        var alias=previousClear with {Facts=[new("fact_0","Let n represent the {f0} {v1} that {v0} had initially."),previousClear.Facts![1]]};
        Require(Validate(c,alias).ErrorCode=="InvalidText","Invented letter alias was accepted.");
        var explanation=previousClear with {SolutionLeads=[new("step_0","Number of {v1} remaining using g(x)")]};
        Require(Validate(c,explanation).ErrorCode=="InvalidText","Technical content in the lead escaped validation.");
        var ordinaryParenthesis=previousClear with {Facts=[new("fact_0","{v0} had {f0} {v1} (the initial stock)."),previousClear.Facts![1]]};
        Require(Validate(c,ordinaryParenthesis).NeedsReview,"Ordinary parenthesized prose was mistaken for a function definition.");
        var oldTechnical=previousClear with {Facts=[new("fact_0","The total count of {v1} held by {v0} was f(f) items initially, where f(f) equals {f0}."),
            new("fact_1","{v0} hands over {f1} of the original stock of {v1} to {v2}.")],Question="Number of {v1} remaining with {v0}"};
        Require(Validate(c,oldTechnical).ErrorCode=="InvalidText","Exact former technical native candidate was not rejected.");
        foreach(string token in new[]{"f{f0}","n{f0}","{f0}x","f'{f0}'","g’{f0}’"})
            Require(Validate(c,previousClear with {Facts=[new("fact_0","{v0} possessed "+token+" {v1}."),previousClear.Facts![1]]}).ErrorCode=="InvalidText",
                "An unbound letter fused to a supplied quantity was accepted.");
        Require(Validate(c,previousClear with {Facts=[previousClear.Facts![0],new("fact_1","{v0} gave f{f1} of the initial {v1} to {v2}.")]}).ErrorCode=="InvalidText",
            "A letter fused to the donated rate was accepted.");
        foreach(string aliasOwner in new[]{"f's","n’s"})
            Require(Validate(c,previousClear with {Facts=[new("fact_0","{v0} had {f0} {v1} in "+aliasOwner+" collection."),previousClear.Facts![1]]}).ErrorCode=="InvalidText",
                "An unbound single-letter owner was accepted.");
        Require(Validate(c,previousClear with {Facts=[new("fact_0","{v0}'s initial collection had {f0} {v1}."),previousClear.Facts![1]]}).NeedsReview,
            "A supplied owner's possessive was mistaken for an unbound letter alias.");
        Require(Validate(c,previousClear with {Question="How  many {v1} does {v0} possess now?"}).IsValid,"Harmless spaces lost the complete-clause proof.");
        foreach(var unpunctuated in new[]{previousClear with {Facts=[previousClear.Facts![0] with {Text=initialClauses[0].TrimEnd('.')},previousClear.Facts[1]]},
            previousClear with {Facts=[previousClear.Facts![0],previousClear.Facts[1] with {Text=previousClear.Facts[1].Text.TrimEnd('.')}]},
            previousClear with {Question=targets[0].TrimEnd('?')}})
        {
            var result=Validate(c,unpunctuated);
            Require(result.NeedsReview&&result.ErrorDetails!.Contains("separate sentences",StringComparison.Ordinal),"A run-on assembled problem was auto-approved or retried as wrong mathematics.");
        }
    }

    internal static async Task CheckRoutingAsync()
    {
        using var workspace=TestWorkspace.Temporary("percentage-remaining-english");
        var store=new QuestionBankStore(workspace.FilePath("worker.db3"));var runtime=new Runtime();
        var worker=new AiQuestionGenerationService(runtime,store);
        worker.Start(new(ArithmeticOperation.Add,CurriculumTier.ThreeStars,AppLanguage.English,2,true,
            Family:BankQuestionFamily.Percentage,StoryVariant:(int)PercentageQuizType.FindRemainingAfterDonation,CreativeProse:true));
        await worker.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        Require(worker.Snapshot.State==AiJobState.Completed&&runtime.Calls==3,"Technical rejection / one-attempt editorial review / next item routing changed.");
        Require(worker.Snapshot.Items[0].State==AiItemState.PendingReview&&worker.Snapshot.Items[0].Attempts.Count==2
            &&worker.Snapshot.Items[0].Attempts[0].ErrorCode=="InvalidText"&&worker.Snapshot.Items[1].State==AiItemState.Saved,
            "Technical output should retry, then a caption should review immediately and the next clear question should save.");
        var pending=(await store.GetPendingReviewsAsync()).Single();
        Require(!await store.ContainsProseAsync(new(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc)),"An English caption entered the active bank.");
        Console.WriteLine("PASS English percentage remainder: original/remaining clause proofs and wrong-role mutations, former native candidates, technical notation/alias rejection, caption review, unchanged wording and worker quarantine/continuation.");
    }

    private sealed class Runtime:IQuestionTextRuntime
    {
        public bool IsLoaded=>true; public string ModelName=>"percentage-remaining-english-fixture"; public int Calls;
        public Task ReleaseAsync()=>Task.CompletedTask;
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken cancellationToken,
            Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {
            Calls++;var d=ReasoningStoryCatalogue.Draft(c);
            if(Calls==1)d=d with {Facts=[d.Facts![0] with {Text="{v0} had f(f) {v1} initially, where f(f) equals {f0}."},d.Facts[1]]};
            if(Calls==2)d=d with {Question="Number of {v1} remaining for {v0}?"};
            if(Calls==3)d=d with {Facts=[new("fact_0","{v0} possessed {f0} {v1} at the start."),d.Facts![1]],Question="How many {v1} does {v0} possess now?"};
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Require(bool value,string message)=>TestAssert.That(value,message);
}
