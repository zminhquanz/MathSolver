using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class GenerationContinuationTests
{
    internal static async Task RunModelAsync(string model, string directory)
    {
        Directory.CreateDirectory(directory);
        var results = new List<object>();
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            await using var session = new ModelTestSession();
            await session.LoadAsync(model);
            var runtime = new InvalidThenNativeRuntime(session.Runtime);
            var store = new QuestionBankStore(Path.Combine(directory, language + "-" + Guid.NewGuid().ToString("N") + ".db3"));
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(Options(BankQuestionFamily.Arithmetic, language, true) with {
                Tier = CurriculumTier.FiveStars, Profile = new(QuestionKnowledgeGroup.Objects) });
            await worker.Completion.WaitAsync(TimeSpan.FromMinutes(10));
            await File.WriteAllTextAsync(Path.Combine(directory, language + ".json"),
                JsonSerializer.Serialize(worker.Snapshot, new JsonSerializerOptions { WriteIndented = true }));
            var next = worker.Snapshot.Items.LastOrDefault();
            Assert(worker.Snapshot.State == AiJobState.CompletedWithSkips && runtime.Calls == 4
                && worker.Snapshot.Items[0] is { State: AiItemState.Rejected, Question: null }
                && worker.Snapshot.Items[0].Attempts.Count == 3 && next?.State == AiItemState.Saved
                && next.Attempts.Count == 1 && !session.Runtime.IsLoaded && runtime.Releases == 1
                && (await store.GetProseHashesAsync()).Count == 1,
                $"{language}: native inference did not save the next item after three injected invalid outputs: {worker.Snapshot.State}/{worker.Snapshot.Error}");
            var metrics = next!.Attempts.Single().Metrics!;
            Assert(metrics.GeneratedTokens > 0 && metrics.Preparation?.OffloadedGpuLayers > 0,
                "The native continuation smoke must actually generate on GPU.");
            results.Add(new { Language = language.ToString(), runtime.Calls, runtime.Releases, Metrics = metrics });
            Console.WriteLine($"PASS {language}: three injected invalid outputs skipped; next actual GPU question saved; {metrics.TokensPerSecond:F2} tok/s, {metrics.Preparation!.OffloadedGpuLayers} GPU layers.");
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static async Task RunAsync()
    {
        int families = 0, grammarCases = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var family in Enum.GetValues<BankQuestionFamily>())
        foreach (bool autoInsert in new[] { false, true })
        {
            var options = Options(family, language, autoInsert);
            var runtime = new Runtime(invalidCalls: 3);
            var store = new Store();
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(options);
            await worker.Completion;
            Assert(worker.Snapshot.State.ToString() == "CompletedWithSkips" && worker.Snapshot.Error is null
                && worker.Snapshot.Items.Count == 2 && runtime.Calls == 4 && runtime.Releases == 1,
                $"{family}/{language}: three invalid attempts stopped the batch: {worker.Snapshot.State}/{worker.Snapshot.Error}");
            var rejected = worker.Snapshot.Items[0];
            var valid = worker.Snapshot.Items[1];
            Assert(rejected is { State: AiItemState.Rejected, Question: null } && rejected.Attempts.Count == 3
                && rejected.Attempts.All(a => a.IsComplete && a.RawJson == "{}" && a.ErrorCode is not null)
                && valid.State == (autoInsert ? AiItemState.Saved : AiItemState.Ready)
                && valid.Attempts.Count == 1 && valid.Question is not null && store.Saves == (autoInsert ? 1 : 0),
                $"{family}/{language}: continuation lost the rejected item's diagnostics or published invalid output.");
            families++;
        }

        // Exercise the real finite-grammar exhaustion path for every reasoning
        // subtype, not just a fake exception in the arithmetic runtime.
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var family in Enum.GetValues<BankQuestionFamily>().Where(ReasoningStoryCatalogue.Supports))
        foreach (int variant in ReasoningStoryCatalogue.Variants(family, CurriculumTier.ThreeStars))
        {
            var runtime = new Runtime(exhaustFirstGrammar: true);
            var store = new Store();
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(Options(family, language, true) with { Tier = CurriculumTier.ThreeStars, StoryVariant = variant });
            await worker.Completion;
            Assert(worker.Snapshot.State.ToString() == "CompletedWithSkips" && worker.Snapshot.Error is null
                && worker.Snapshot.Items.Count == 2 && worker.Snapshot.Items[0] is { State: AiItemState.Duplicate, Error: "ProseContextExhausted" }
                && worker.Snapshot.Items[1].State == AiItemState.Saved && runtime.Calls == 2
                && runtime.Releases == 1 && store.Saves == 1,
                $"{family}/{variant}/{language}: a finite grammar pool leaked an exception or stopped later questions: {worker.Snapshot.Error}");
            grammarCases++;
        }

        foreach (bool autoInsert in new[] { false, true })
        {
            var runtime = new Runtime();
            var store = new Store(duplicateChecks: 3);
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(Options(BankQuestionFamily.Arithmetic, AppLanguage.Vietnamese, autoInsert));
            await worker.Completion;
            Assert(worker.Snapshot.State.ToString() == "CompletedWithSkips" && worker.Snapshot.Items.Count == 2
                && worker.Snapshot.Items[0].State == AiItemState.Duplicate && worker.Snapshot.Items[0].Attempts.Count == 3
                && worker.Snapshot.Items[1].Question is not null && runtime.Calls == 4 && store.Saves == (autoInsert ? 1 : 0),
                $"Three duplicates did not advance (auto={autoInsert}, calls={runtime.Calls}, saves={store.Saves}, state={worker.Snapshot.State}, "
                    + $"items={string.Join(';', worker.Snapshot.Items.Select(i => i.State + "/" + i.Error + "/" + string.Join(',', i.Attempts.Select(a => a.ErrorCode))))}).");
        }

        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (int queueRejections in new[] { 0, 3 })
        {
            var reviewRuntime = new ReviewRuntime();
            var reviewStore = new Store(queueRejections: queueRejections);
            var reviewWorker = new AiQuestionGenerationService(reviewRuntime, reviewStore);
            reviewWorker.Start(Options(BankQuestionFamily.Arithmetic, language, true) with {
                Tier = CurriculumTier.ThreeStars, Profile = new(QuestionKnowledgeGroup.Objects), CreativeProse = true });
            await reviewWorker.Completion;
            Assert(reviewWorker.Snapshot.State == (queueRejections == 0 ? AiJobState.Completed : AiJobState.CompletedWithSkips)
                && reviewWorker.Snapshot.Items.Count == 2 && reviewRuntime.Calls == (queueRejections == 0 ? 2 : 4)
                && reviewStore.Queued == (queueRejections == 0 ? 2 : 1) && reviewStore.Saves == 0
                && reviewWorker.Snapshot.Items[^1] is { State: AiItemState.PendingReview, Question: null }
                && reviewWorker.Snapshot.Items[^1].Attempts.Count == 1
                && reviewWorker.Snapshot.Items[0].Attempts.Count == (queueRejections == 0 ? 1 : 3),
                $"{language}: unproved meaning was retried unnecessarily, or review insertion duplicates stopped the batch.");
        }

        var invalid = new Runtime(invalidCalls: int.MaxValue);
        var invalidStore = new Store();
        var invalidWorker = new AiQuestionGenerationService(invalid, invalidStore);
        invalidWorker.Start(Options(BankQuestionFamily.Arithmetic, AppLanguage.English, true) with { Count = 4 });
        await invalidWorker.Completion;
        Assert(invalidWorker.Snapshot.State.ToString() == "CompletedWithSkips" && invalidWorker.Snapshot.Error is null
            && invalidWorker.Snapshot.Items.Count == 4 && invalid.Calls == 12 && invalid.Releases == 1
            && invalidStore.Saves == 0 && invalidWorker.Snapshot.Items.All(i => i.State == AiItemState.Rejected && i.Attempts.Count == 3),
            "All-invalid batches must stay bounded to three attempts for each requested item.");

        var queueStopStore = new Store();
        var queueStop = new AiQuestionGenerationService(new ReviewRuntime(), queueStopStore);
        queueStopStore.OnQueued = queueStop.Stop;
        queueStop.Start(Options(BankQuestionFamily.Arithmetic, AppLanguage.English, true) with {
            Tier = CurriculumTier.ThreeStars, Profile = new(QuestionKnowledgeGroup.Objects), CreativeProse = true });
        await queueStop.Completion;
        Assert(queueStop.Snapshot.State == AiJobState.Stopped && queueStop.Snapshot.Items.Count == 1
            && queueStop.Snapshot.Items[0] is { State: AiItemState.PendingReview, Review: not null, Question: null }
            && queueStopStore.Queued == 1 && queueStopStore.Saves == 0,
            "Stop racing a completed queue write hid a persisted review or started the next item.");

        var stoppedRuntime = new Runtime();
        var stopped = new AiQuestionGenerationService(stoppedRuntime, new Store(duplicateChecks: int.MaxValue));
        stopped.Changed += (_, _) =>
        {
            if (stopped.Snapshot.Items.Any(i => i.State == AiItemState.Duplicate && i.Attempts.Count == 3
                && i.Attempts.All(a => a.IsComplete))) stopped.Stop();
        };
        stopped.Start(Options(BankQuestionFamily.Arithmetic, AppLanguage.English, true));
        await stopped.Completion;
        Assert(stopped.Snapshot.State == AiJobState.Stopped && stopped.Snapshot.Items.Count == 1
            && stopped.Snapshot.Items[0].State == AiItemState.Duplicate && stoppedRuntime.Calls == 3 && stoppedRuntime.Releases == 1,
            "Stop after a skipped question started another item or overwrote the duplicate result.");
        Console.WriteLine($"PASS {families} bilingual family/manual/auto continuations, {grammarCases} real exhausted-grammar subtype routes, duplicate skipping, all-invalid bounds and cancellation");
    }

    private static AiGenerationOptions Options(BankQuestionFamily family, AppLanguage language, bool autoInsert) =>
        new(ArithmeticOperation.Add, CurriculumTier.OneStar, language, 2, autoInsert,
            Family: family, UnknownRole: family == BankQuestionFamily.FindX ? FindXUnknownRole.Addend : FindXUnknownRole.None,
            StoryVariant: ReasoningStoryCatalogue.Supports(family) ? ReasoningStoryCatalogue.Variants(family, CurriculumTier.OneStar)[0] : 0);

    private static BasicQuestionDraft Draft(BasicQuestionContract c, IReadOnlySet<string> excluded) =>
        c.Version == ExpandedArithmeticCatalogue.Version ? ExpandedArithmeticCatalogue.NovelDrafts(c, excluded).First()
        : c.Version == ReasoningStoryCatalogue.Version ? ReviewedReasoningProse.NovelDrafts(c, excluded).First()
        // The standard grammar permits newly phrased prose beyond its examples;
        // a runtime can also ignore exclusions and repeat an old example.
        : ReviewedQuestionProse.For(c)!.Stories().FirstOrDefault(d => !excluded.Contains(QuestionProseIdentity.Hash(c, d)))
            ?? ReviewedQuestionProse.For(c)!.Stories().First();

    private static void Assert(bool condition, string message) => TestAssert.That(condition, message);

    private sealed class InvalidThenNativeRuntime(GgufQuestionRuntime native) : IQuestionTextRuntime
    {
        public bool IsLoaded => native.IsLoaded;
        public bool CanGenerate => native.CanGenerate;
        public string ModelName => native.ModelName;
        public int Calls, Releases;
        public Task PrepareAsync(CancellationToken token) => native.PrepareAsync(token);
        public Task ReleaseAsync() { Releases++; return native.ReleaseAsync(); }
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken token,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
            => GenerateNovelAsync(c, prompt, new HashSet<string>(), token, onText, onMetrics);
        public Task<string> GenerateNovelAsync(BasicQuestionContract c, string prompt, IReadOnlySet<string> excluded,
            CancellationToken token, Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            if (++Calls > 3) return native.GenerateNovelAsync(c, prompt, excluded, token, onText, onMetrics);
            onText?.Invoke("{}");
            return Task.FromResult("{}");
        }
    }

    private sealed class ReviewRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "review-continuation-regression";
        public int Calls;
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken token,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            var draft = OpenQuestionProse.Example(c);
            string opening = "Zephyrus " + (char)('A' + ++Calls) + ", ";
            draft = draft.Facts is null ? draft with { GivenA = opening + draft.GivenA }
                : draft with { Facts = draft.Facts.Select((f, i) => i == 0 ? f with { Text = opening + f.Text } : f).ToArray() };
            string raw = QuestionBankStore.SerializeDraft(draft);
            onText?.Invoke(raw);
            return Task.FromResult(raw);
        }
    }

    private sealed class Runtime(int invalidCalls = 0, bool exhaustFirstGrammar = false) : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "continuation-regression";
        public int Calls, Releases;
        public Task ReleaseAsync() { Releases++; return Task.CompletedTask; }
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken token,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
            => GenerateNovelAsync(c, prompt, new HashSet<string>(), token, onText, onMetrics);
        public Task<string> GenerateNovelAsync(BasicQuestionContract c, string prompt, IReadOnlySet<string> excluded,
            CancellationToken token, Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Calls++;
            if (Calls == 1 && exhaustFirstGrammar)
            {
                var hashes = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>())
                    .Select(d => QuestionProseIdentity.Hash(c, d)).ToHashSet(StringComparer.Ordinal);
                _ = ReasoningStoryValidator.Grammar(c, hashes);
                throw new InvalidDataException("The exhausted-grammar fixture unexpectedly produced a grammar.");
            }
            string raw = Calls <= invalidCalls ? "{}" : QuestionBankStore.SerializeDraft(Draft(c, excluded));
            onText?.Invoke(raw);
            return Task.FromResult(raw);
        }
    }

    private sealed class Store(int duplicateChecks = 0, int queueRejections = 0) : IQuestionBankStore
    {
        public int Saves, Queued;
        public Action? OnQueued;
        public Task<bool> QueueReviewAsync(PendingQuestionReview question, CancellationToken cancellationToken = default)
        {
            if (queueRejections-- > 0) return Task.FromResult(false);
            Queued++;
            OnQueued?.Invoke();
            return Task.FromResult(true);
        }
        public Task<bool> ContainsProseAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
            => Task.FromResult(duplicateChecks-- > 0);
        public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
        { Saves++; return Task.FromResult(true); }
        public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, CancellationToken cancellationToken = default) => Task.FromResult<ValidatedBankQuestion?>(null);
    }
}
