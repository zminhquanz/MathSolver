using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using SQLite;

internal static class DeleteAllTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory)
    {
        string path = Path.Combine(directory, "delete-all.db3");
        var store = new QuestionBankStore(path);
        var worker = new AiQuestionGenerationService(new Runtime(), store);
        var options = new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.English, 1, true);
        Check(await worker.DeleteAllAsync() == 0, "An empty bank must allow deletion without a model load.");
        worker.Start(options);
        await worker.Completion;
        Check(worker.Snapshot.Items.Single().State == AiItemState.Saved, "Seed generation failed.");
        var question = worker.Snapshot.Items.Single().Question!;
        Check(await store.TakeAsync(options.Operation, options.Tier, options.Language) is not null, "Seed cannot be selected.");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { await worker.DeleteAllAsync(cancelled.Token); throw new InvalidOperationException("Cancelled deletion succeeded."); }
            catch (OperationCanceledException) { }
        }
        Check(!worker.IsDeletingAll && worker.Snapshot.Items.Single().State == AiItemState.Saved
            && await store.ContainsProseAsync(question), "Cancellation changed saved data or preview.");

        // Force a failure after deleting the main rows, to prove both tables roll back together.
        using (var db = new SQLiteConnection(path))
            db.Execute("CREATE TRIGGER FailClear BEFORE DELETE ON QuestionProseIndex BEGIN SELECT RAISE(ABORT,'test failure'); END");
        try { await worker.DeleteAllAsync(); throw new InvalidOperationException("Failed transaction succeeded."); }
        catch (SQLiteException) { }
        Check(!worker.IsDeletingAll && worker.Snapshot.Items.Single().State == AiItemState.Saved
            && await store.ContainsProseAsync(question), "Failed deletion lost data or changed the preview.");
        using (var db = new SQLiteConnection(path)) db.Execute("DROP TRIGGER FailClear");

        // A malformed historical row must also be deleted; deletion does not depend on validation.
        using (var db = new SQLiteConnection(path)) db.Insert(new QuestionBankStore.Row { Hash = "invalid-history" });
        Check(await worker.DeleteAllAsync() == 2, "Whole-bank deletion omitted malformed or valid rows.");
        using (var db = new SQLiteConnection(path))
        {
            Check(db.ExecuteScalar<int>("SELECT count(*) FROM BasicQuestionBank") == 0, "Question rows remain.");
            Check(db.ExecuteScalar<int>("SELECT count(*) FROM QuestionProseIndex") == 0, "Deduplication identities remain.");
        }
        Check(worker.Snapshot.Items.Single().State == AiItemState.Ready, "Deleted preview still claims to be saved.");
        Check(await store.TakeAsync(options.Operation, options.Tier, options.Language) is null, "Deleted question remains selectable.");
        await worker.InsertAsync(1);
        Check(worker.Snapshot.Items.Single().State == AiItemState.Saved && await store.ContainsProseAsync(question),
            "A deleted preview cannot be saved again.");
        Check(await new QuestionBankStore(path).DeleteAllAsync() == 1, "Reopening the bank failed after deletion.");

        var guardedStore = new BlockingStore();
        var guarded = new AiQuestionGenerationService(new Runtime(), guardedStore);
        var deletion = guarded.DeleteAllAsync();
        await guardedStore.Entered.Task;
        Check(guarded.IsDeletingAll, "Missing busy state.");
        await ExpectBusyAsync(() => { guarded.Start(options); return Task.CompletedTask; });
        await ExpectBusyAsync(() => guarded.InsertAsync(1));
        await ExpectBusyAsync(() => guarded.DeleteAllAsync());
        guardedStore.Release.SetResult();
        await deletion;
        Check(!guarded.IsDeletingAll, "Busy state persists after deletion.");

        var slowRuntime = new BlockingRuntime();
        var generating = new AiQuestionGenerationService(slowRuntime, store);
        generating.Start(options);
        await slowRuntime.Entered.Task;
        await ExpectBusyAsync(() => generating.DeleteAllAsync());
        generating.Stop();
        await generating.Completion;
        Console.WriteLine("PASS whole-bank delete, empty bank, transaction rollback, cancellation, re-save and generation/write exclusion");
    }

    private static async Task ExpectBusyAsync(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidOperationException error) when (error.Message == "JobAlreadyRunning") { return; }
        throw new InvalidOperationException("A concurrent generation/write/delete was allowed.");
    }

    private sealed class Runtime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "clear-test";
        public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
            => Task.FromResult(QuestionBankStore.SerializeDraft(AdditionQuestionCatalogue.Example(contract)));
    }

    private sealed class BlockingRuntime : IQuestionTextRuntime
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsLoaded => true;
        public string ModelName => "blocked-test";
        public async Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Entered.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return "";
        }
    }

    private sealed class BlockingStore : IQuestionBankStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        { Entered.SetResult(); await Release.Task.WaitAsync(cancellationToken); return 0; }
        public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, CancellationToken cancellationToken = default) => Task.FromResult<ValidatedBankQuestion?>(null);
    }
}
