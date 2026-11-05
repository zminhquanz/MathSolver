using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class ProseExhaustionTests
{
    internal static async Task RunAsync()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Objects);
            var pool = new Dictionary<string, (BasicQuestionContract Contract, BasicQuestionDraft Draft)>();
            foreach (string context in ExpandedArithmeticCatalogue.Contexts(profile.Group, CurriculumTier.FiveStars, language))
            for (int seed = 0; seed < 64; seed++)
            {
                var contract = ExpandedArithmeticCatalogue.Create(profile, ArithmeticOperation.Add,
                    CurriculumTier.FiveStars, language, new Random(seed), context);
                foreach (var draft in ExpandedArithmeticCatalogue.Drafts(contract))
                    pool.TryAdd(QuestionProseIdentity.Hash(contract, draft), (contract, draft));
            }
            TestAssert.That(pool.Count > 1, "The exhaustion fixture must contain real reviewed wording.");

            foreach (bool autoInsert in new[] { false, true })
            {
                var store = new SeededStore(pool.Keys.ToHashSet(StringComparer.Ordinal));
                var runtime = new ReviewedRuntime();
                var worker = new AiQuestionGenerationService(runtime, store);
                var options = new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.FiveStars,
                    language, 10, autoInsert, profile);
                worker.Start(options);
                await worker.Completion;
                TestAssert.That(worker.Snapshot.Error == "ProsePoolExhausted",
                    "An exhausted pool leaked an exception into the UI: " + worker.Snapshot.Error);
                TestAssert.That(!worker.IsRunning && worker.Snapshot.Items.Count == 0 && runtime.Calls == 0
                    && runtime.Releases == 1 && store.Saves == 0,
                    "An exhausted pool started inference, saved duplicates or skipped cleanup.");

                // Leave exactly one wording unused. Keep its generated preview/save
                // when the next requested item reaches the end of the pool.
                string remaining = pool.Keys.First();
                store = new SeededStore(pool.Keys.Where(hash => hash != remaining).ToHashSet(StringComparer.Ordinal));
                runtime = new ReviewedRuntime();
                worker = new(runtime, store);
                worker.Start(options);
                await worker.Completion;
                var item = worker.Snapshot.Items.Single();
                TestAssert.That(worker.Snapshot.Error == "ProsePoolExhausted" && !worker.IsRunning
                    && item.State == (autoInsert ? AiItemState.Saved : AiItemState.Ready)
                    && QuestionProseIdentity.Hash(item.Question!.Contract, item.Question.Draft) == remaining
                    && item.Attempts.Count == 1 && runtime.Calls == 1 && runtime.Releases == 1
                    && store.Saves == (autoInsert ? 1 : 0),
                    "Pool exhaustion discarded a valid item, retried it, or offered duplicate prose.");
            }

            var reasoningHashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var context in ElementaryQuizGenerator.MeasurementContexts(language, ElementaryQuizType.LengthConversion))
            {
                var contract = ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement,
                    (int)ElementaryQuizType.LengthConversion, CurriculumTier.OneStar, language, new(713), narrativeId: context.Id);
                foreach (var draft in ReviewedReasoningProse.NovelDrafts(contract, new HashSet<string>()))
                    reasoningHashes.Add(QuestionProseIdentity.Hash(contract, draft));
            }
            var reasoningRuntime = new ReviewedRuntime();
            var reasoningWorker = new AiQuestionGenerationService(reasoningRuntime, new SeededStore(reasoningHashes));
            reasoningWorker.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, language, 10, true,
                Family: BankQuestionFamily.Measurement, StoryVariant: (int)ElementaryQuizType.LengthConversion));
            await reasoningWorker.Completion;
            TestAssert.That(reasoningWorker.Snapshot is { State: AiJobState.Failed, Error: "ProsePoolExhausted" }
                && reasoningWorker.Snapshot.Items.Count == 0 && reasoningRuntime.Calls == 0 && reasoningRuntime.Releases == 1,
                $"{language}: exhausted reasoning contexts leaked a cycle exception or started unnecessary inference.");
        }
        var grammarRuntime = new ExhaustingRuntime();
        var grammarWorker = new AiQuestionGenerationService(grammarRuntime, new SeededStore([]));
        grammarWorker.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 1, false));
        await grammarWorker.Completion;
        TestAssert.That(grammarWorker.Snapshot.State == AiJobState.CompletedWithSkips && grammarWorker.Snapshot.Error is null
            && grammarWorker.Snapshot.Items.Single() is { State: AiItemState.Duplicate, Error: "ProseContextExhausted" }
            && grammarWorker.Snapshot.Items.Single().Attempts.Single().RawJson == "partial output"
            && grammarRuntime.Releases == 1,
            "A grammar-level exhausted pool leaked exception text or lost its diagnostic output.");
        Console.WriteLine("PASS bilingual finite prose exhaustion before inference, partial manual/auto batches, native grammar exhaustion and cleanup");
    }

    private sealed class SeededStore(HashSet<string> hashes) : IQuestionBankStore
    {
        public int Saves { get; private set; }
        public Task<IReadOnlySet<string>> GetProseHashesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(hashes.ToHashSet(StringComparer.Ordinal));
        public Task<bool> ContainsProseAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
            => Task.FromResult(hashes.Contains(QuestionProseIdentity.Hash(question.Contract, question.Draft)));
        public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
        {
            bool inserted = hashes.Add(QuestionProseIdentity.Hash(question.Contract, question.Draft));
            if (inserted) Saves++;
            return Task.FromResult(inserted);
        }
        public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, CancellationToken cancellationToken = default) => Task.FromResult<ValidatedBankQuestion?>(null);
    }

    private sealed class ReviewedRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "exhaustion-regression";
        public int Calls, Releases;
        public Task ReleaseAsync() { Releases++; return Task.CompletedTask; }
        public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken token,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
            => GenerateNovelAsync(contract, prompt, new HashSet<string>(), token, onText, onMetrics);
        public Task<string> GenerateNovelAsync(BasicQuestionContract contract, string prompt, IReadOnlySet<string> excluded,
            CancellationToken token, Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Calls++;
            string raw = QuestionBankStore.SerializeDraft(ExpandedArithmeticCatalogue.NovelDrafts(contract, excluded).First());
            onText?.Invoke(raw);
            return Task.FromResult(raw);
        }
    }

    private sealed class ExhaustingRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "grammar-exhaustion-regression";
        public int Releases;
        public Task ReleaseAsync() { Releases++; return Task.CompletedTask; }
        public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken token,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            onText?.Invoke("partial output");
            throw new InvalidOperationException("DuplicateProseRetriesExhausted");
        }
    }
}
