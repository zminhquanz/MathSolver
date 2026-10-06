using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class ProseDeduplicationTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory)
    {
        var c = BasicQuestionContract.CreateTemplate(ArithmeticOperation.Add, CurriculumTier.OneStar,
            AppLanguage.Vietnamese, new Random(3)) with { Structure = BasicQuestionStructure.Increase };
        c = BasicQuestionTemplates.ApplyUnit(c, QuestionUnits.All.First(u => u.Id == "books"));
        var draft = BasicQuestionTemplates.Example(c);
        var question = new ValidatedBankQuestion(c, draft, QuestionBankStore.SerializeDraft(draft), "test", DateTime.UtcNow);
        string identity = QuestionProseIdentity.Hash(c, draft);
        var fresh = c with { Left = 40, Right = 13, Subject = "Lan", OtherSubject = "Mai", Tier = CurriculumTier.TwoStars };
        Check(identity == QuestionProseIdentity.Hash(fresh, draft with { SolutionLead = "Một câu dẫn khác:" }),
            "Random facts, stars, names or the solution lead changed prose identity.");
        var cosmetic = draft with { GivenA = draft.GivenA.Replace(",", ".").Replace(" ", "  "), Question = draft.Question.ToUpperInvariant() };
        Check(identity == QuestionProseIdentity.Hash(c, cosmetic), "Punctuation, whitespace or case bypassed deduplication.");
        Check(identity == QuestionProseIdentity.Hash(c, draft with { Question = draft.Question.Normalize(System.Text.NormalizationForm.FormD) }),
            "Unicode normalization bypassed deduplication.");
        var different = draft with { GivenB = "{name} được cho thêm {b} {unit}." };
        Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(different), c).IsValid,
            "Alternate wording fixture must preserve the arithmetic relationship.");
        Check(identity != QuestionProseIdentity.Hash(c, different), "Different wording was treated as a duplicate.");
        var notebook = BasicQuestionTemplates.ApplyUnit(c, QuestionUnits.All.First(u => u.Id == "notebooks"));
        Check(identity != QuestionProseIdentity.Hash(notebook, draft with { UnitId = "notebooks" }),
            "Different object/unit wording was treated as a duplicate.");

        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var saving = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Money), ArithmeticOperation.Add,
                CurriculumTier.ThreeStars, language, new Random(17), "saving-total");
            var choices = ReviewedQuestionProse.For(saving)!;
            var stories = choices.Stories().ToArray();
            Check(stories.Length >= 48, "Savings still has too few distinct factual clauses.");
            foreach (var story in stories)
            {
                string raw = QuestionBankStore.SerializeDraft(story);
                Check(BasicQuestionValidator.Validate(raw, saving).IsValid, "A new reviewed savings story was rejected.");
                Check(!BasicQuestionValidator.Validate(raw.Replace("{a}", "{b}"), saving).IsValid,
                    "New prose alternatives allowed swapped quantities.");
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(story with {
                    GivenB = story.GivenB + (language == AppLanguage.Vietnamese ? " Nhưng chưa biết số tiền bị mất." : " But an unknown amount was lost.") }), saving).IsValid,
                    "New prose alternatives allowed an extra condition.");
            }
            var all = stories.Select(s => QuestionProseIdentity.Hash(saving, s)).ToHashSet(StringComparer.Ordinal);
            Check(all.Count == stories.Length, "Cosmetic variants inflate the savings pool.");
            bool poolExhausted = false;
            try { GgufQuestionRuntime.BuildNovelGrammar(saving, all); }
            catch (ProseAlternativesExhaustedException) { poolExhausted = true; }
            Check(poolExhausted, "Exhausted wording produced a grammar allowing duplicates.");
            var last = stories[^1];
            all.Remove(QuestionProseIdentity.Hash(saving, last));
            Check(choices.NovelExample(saving, all) == last, "Prompt selected an example already in SQLite.");
            Check(BasicQuestionPrompt.Build(saving, excludedProse: all).Contains(last.GivenB),
                "Prompt failed to give the model a concrete unused factual phrasing.");
            Check(GgufQuestionRuntime.BuildNovelGrammar(saving, all).StartsWith("root ::="),
                "A final unused story was mistaken for exhaustion.");
        }

        string path = Path.Combine(directory, "prose-identity.db3");
        var store = new QuestionBankStore(path);
        Check(await store.InsertAsync(question), "Initial wording was not stored.");
        Check(await store.ContainsProseAsync(question with { Contract = fresh }), "SQLite missed wording with different C# facts.");
        Check((await store.GetProseHashesAsync()).Contains(identity), "Generation's exclusion snapshot missed stored prose.");
        using (var oldIndex = new SQLite.SQLiteConnection(path))
        {
            oldIndex.Execute("UPDATE QuestionProseIndex SET ProseHash=''");
            oldIndex.Execute("UPDATE QuestionProseIndexMetadata SET Value=1 WHERE Key='ValidationRevision'");
        }
        Check((await new QuestionBankStore(path).GetProseHashesAsync()).Contains(identity),
            "Upgraded prose policy retained a cached blank identity and allowed an existing question again.");
        Check(!await store.InsertAsync(question with { Contract = fresh }), "A different tier/numeric contract bypassed storage deduplication.");
        var secondInstance = new QuestionBankStore(path);
        Check(!await secondInstance.InsertAsync(question with { Draft = draft with { SolutionLead = "Tổng số {unit} là:" } }),
            "Reopened storage deduplicated on solution lead rather than question prose.");
        Check(await store.InsertAsync(question with { Draft = different }), "Storage rejected different natural phrasing.");

        // An old/public-table row with an arbitrary key is indexed without rewriting or deleting it.
        await store.QueryAsync("DELETE FROM BasicQuestionBank");
        string contractJson = JsonSerializer.Serialize(c);
        string draftJson = QuestionBankStore.SerializeDraft(draft);
        using (var db = new SQLite.SQLiteConnection(path))
            db.Execute("INSERT INTO BasicQuestionBank(Hash,ContractJson,DraftJson) VALUES (?,?,?)", "historical-row", contractJson, draftJson);
        Check(await secondInstance.ContainsProseAsync(question), "Historical unindexed row was ignored.");
        using (var db = new SQLite.SQLiteConnection(path))
            db.Execute("UPDATE BasicQuestionBank SET DraftJson=? WHERE Hash=?", QuestionBankStore.SerializeDraft(different), "historical-row");
        Check(!await store.ContainsProseAsync(question) && await store.ContainsProseAsync(question with { Draft = different }),
            "Direct SQL edits left stale wording in the index.");
        await store.QueryAsync("DELETE FROM BasicQuestionBank");
        Check(!await store.ContainsProseAsync(question with { Draft = different }), "Deleted wording still blocked generation.");
        Check((await store.GetProseHashesAsync()).Count == 0, "Deleted wording remained in the native exclusion snapshot.");

        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (bool autoInsert in new[] { false, true })
        {
            var runtime = new Runtime();
            var duplicates = new DuplicateStore(2);
            var worker = new AiQuestionGenerationService(runtime, duplicates);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, language, 1, autoInsert));
            await worker.Completion;
            var item = worker.Snapshot.Items.Single();
            Check(worker.Snapshot.State == AiJobState.Completed && runtime.Prompts.Count == 3
                && item.State == (autoInsert ? AiItemState.Saved : AiItemState.Ready)
                && item.Attempts.Take(2).All(a => a.ErrorCode == "DuplicateProse"), "Duplicate wording did not retry for manual/auto mode.");
            Check(runtime.Prompts[1].Contains(language == AppLanguage.Vietnamese ? "Mẫu lời văn trước đã trùng" : "previous question wording was duplicated")
                && runtime.Prompts[1].Contains(item.Question!.Draft.Question)
                && runtime.Contracts.All(contract => contract == runtime.Contracts[0]),
                "Retry prompt omitted rejected wording, language or retained C# facts.");
            Check(duplicates.Saves == (autoInsert ? 1 : 0), "A rejected duplicate was saved.");
        }

        var exhaustedRuntime = new Runtime();
        var exhaustedStore = new DuplicateStore(int.MaxValue);
        var exhausted = new AiQuestionGenerationService(exhaustedRuntime, exhaustedStore);
        exhausted.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 5, true));
        await exhausted.Completion;
        Check(exhaustedRuntime.Prompts.Count == 3 && exhaustedStore.Saves == 0
            && exhausted.Snapshot.Error == "DuplicateProseRetriesExhausted"
            && exhausted.Snapshot.Items.Single() is { State: AiItemState.Duplicate, Question: null },
            "Persistent duplicates were saved or the retry bound was exceeded.");

        var finiteRuntime = new ExhaustedRuntime();
        var finiteStore = new DuplicateStore(0);
        var finiteWorker = new AiQuestionGenerationService(finiteRuntime, finiteStore);
        finiteWorker.Start(new(ArithmeticOperation.Add, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, 4, true,
            new(QuestionKnowledgeGroup.Money)));
        await finiteWorker.Completion;
        Check(finiteWorker.Snapshot.State == AiJobState.Failed && finiteWorker.Snapshot.Error == "ProseAlternativesExhausted"
            && finiteRuntime.Calls == 1 && finiteRuntime.Releases == 1 && finiteStore.Saves == 0
            && finiteWorker.Snapshot.Items.Single() is { State: AiItemState.Duplicate, Question: null }
            && finiteWorker.Snapshot.Items.Single().Attempts.Single().IsComplete,
            "Pool exhaustion retried impossible output, saved a duplicate or skipped model cleanup.");

        var raceRuntime = new Runtime();
        var racingStore = new DuplicateStore(0, rejectFirstInsert: true);
        var racing = new AiQuestionGenerationService(raceRuntime, racingStore);
        racing.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.English, 1, true));
        await racing.Completion;
        Check(racing.Snapshot.State == AiJobState.Completed && raceRuntime.Prompts.Count == 2
            && racingStore.Saves == 1 && racing.Snapshot.Items.Single().Attempts[0].ErrorCode == "DuplicateProse",
            "An insert-time duplicate did not regenerate after the precheck race.");

        // Returning one fixed reviewed variant per scene must eventually repeat
        // within a batch, even before any item has been manually saved to SQLite.
        var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Geometry);
        int scenes = AppliedQuestionCatalogue.Available(profile, ArithmeticOperation.Multiply, CurriculumTier.OneStar).Count();
        Check(scenes is > 0 and < 100, "Batch fixture needs a finite scene set.");
        var pendingStore = new DuplicateStore(0);
        var batch = new AiQuestionGenerationService(new ProfileRuntime(), pendingStore);
        batch.Start(new(ArithmeticOperation.Multiply, CurriculumTier.OneStar, AppLanguage.Vietnamese, scenes + 1, false, profile));
        await batch.Completion;
        var accepted = batch.Snapshot.Items.Where(i => i.State == AiItemState.Ready).ToArray();
        Check(batch.Snapshot.Error == "DuplicateProseRetriesExhausted" && pendingStore.Saves == 0
            && accepted.Length > 0 && accepted.Select(i => QuestionProseIdentity.Hash(i.Question!.Contract, i.Question.Draft)).Distinct().Count() == accepted.Length
            && batch.Snapshot.Items.Last().Attempts.Count == 3,
            "Manual batch offered duplicate wording before SQLite insertion.");

        var realStore = new QuestionBankStore(Path.Combine(directory, "prose-worker.db3"));
        var seededRuntime = new SeededRuntime(realStore);
        var realWorker = new AiQuestionGenerationService(seededRuntime, realStore);
        realWorker.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 1, true,
            new(QuestionKnowledgeGroup.Objects)));
        await realWorker.Completion;
        var realItem = realWorker.Snapshot.Items.Single();
        Check(realWorker.Snapshot.State == AiJobState.Completed && realItem.State == AiItemState.Saved
            && seededRuntime.Calls == 2 && realItem.Attempts[0].ErrorCode == "DuplicateProse"
            && realItem.Attempts[0].RawJson != realItem.Attempts[1].RawJson,
            "Worker did not replace existing SQLite wording with a valid distinct question.");
        Console.WriteLine("PASS wording identity, legacy SQLite/edit/delete synchronization, bilingual duplicate retries and retry bounds");
    }

    private sealed class Runtime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "prose-test";
        public List<string> Prompts { get; } = [];
        public List<BasicQuestionContract> Contracts { get; } = [];
        public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Prompts.Add(prompt);
            Contracts.Add(contract);
            return Task.FromResult(QuestionBankStore.SerializeDraft(AdditionQuestionCatalogue.Example(contract)));
        }
    }

    private sealed class ExhaustedRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "exhausted-prose";
        public int Calls { get; private set; }
        public int Releases { get; private set; }
        public Task ReleaseAsync() { Releases++; return Task.CompletedTask; }
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken token,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
            => throw new InvalidOperationException("Worker must pass exclusions to GenerateNovelAsync.");
        public Task<string> GenerateNovelAsync(BasicQuestionContract c, string prompt, IReadOnlySet<string> excluded,
            CancellationToken token, Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Calls++;
            return Task.FromException<string>(new ProseAlternativesExhaustedException());
        }
    }

    private sealed class DuplicateStore(int duplicates, bool rejectFirstInsert = false) : IQuestionBankStore
    {
        public int Saves { get; private set; }
        public Task<bool> ContainsProseAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
            => Task.FromResult(duplicates-- > 0);
        public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
        {
            if (rejectFirstInsert) { rejectFirstInsert = false; return Task.FromResult(false); }
            Saves++;
            return Task.FromResult(true);
        }
        public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, CancellationToken cancellationToken = default) => Task.FromResult<ValidatedBankQuestion?>(null);
    }

    private sealed class ProfileRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "batch-prose-test";
        public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
            => Task.FromResult(QuestionBankStore.SerializeDraft(AppliedQuestionCatalogue.Draft(contract)));
    }

    private sealed class SeededRuntime(QuestionBankStore store) : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "sqlite-rephrase-test";
        public int Calls { get; private set; }
        public async Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            int variant = Calls++ == 0 ? 0 : 1;
            var draft = AppliedQuestionCatalogue.Draft(contract, variant);
            string raw = QuestionBankStore.SerializeDraft(draft);
            if (variant == 0)
                Check(await store.InsertAsync(new(contract, draft, raw, "previous-model", DateTime.UtcNow), cancellationToken),
                    "Could not seed the wording already stored in SQLite.");
            return raw;
        }
    }
}
