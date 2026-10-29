using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using SQLite;
using System.Reflection;
using System.Runtime.ExceptionServices;

internal static class QuestionReviewBatchTests
{
    public static async Task RunAsync()
    {
        // Reflection keeps the runner compilable before the new public API exists.
        Check(typeof(QuestionBankStore).GetMethod("ResolveReviewsAsync", [typeof(IEnumerable<string>), typeof(bool), typeof(CancellationToken)]) is not null,
            "Missing public batch review API ResolveReviewsAsync(IEnumerable<string>, bool, CancellationToken).");
        using var workspace = TestWorkspace.Temporary("QuestionReviewBatch");
        await ApprovesOnlySelectedAndPersistsProvenanceAsync(workspace.FilePath("subset.db3"));
        await AllUsesExplicitSnapshotAsync(workspace.FilePath("snapshot.db3"));
        await RejectLeavesActiveQuestionsAsync(workspace.FilePath("reject.db3"));
        await DeduplicatesAndReportsMissingAsync(workspace.FilePath("duplicates.db3"));
        await AlreadyActiveApprovalResolvesPendingAsync(workspace.FilePath("active.db3"));
        await InvalidRowsRollBackWholeBatchAsync(workspace.FilePath("invalid.db3"));
        await CancellationLeavesBatchUnchangedAsync(workspace.FilePath("cancel.db3"));
        await OverlappingBatchesResolveEachIdOnceAsync(workspace.FilePath("overlap.db3"));
        await ValidatesInputsBeforeMutationAsync(workspace.FilePath("inputs.db3"));
        Console.WriteLine("PASS batch review subsets, explicit all snapshot, rejection isolation, duplicate/missing IDs, provenance reload, invalid-row rollback, cancellation and overlapping requests.");
    }

    private static async Task ApprovesOnlySelectedAndPersistsProvenanceAsync(string path)
    {
        var store = new QuestionBankStore(path);
        var pending = await SeedAsync(store, "Alpha", "Beta", "Gamma");
        var result = await ResolveAsync(store, [pending[0].Id, pending[2].Id], true);
        AssertIds(result.ApprovedIds, [pending[0].Id, pending[2].Id], "Selected approvals were not reported.");
        AssertIds(result.RejectedIds, [], "Approval reported rejections.");
        AssertIds(result.MissingIds, [], "Existing rows were reported missing.");
        AssertIds(await store.GetPendingProseHashesAsync(), [pending[1].Id], "Approving a subset changed an unselected row.");
        var reloaded = new QuestionBankStore(path);
        AssertIds(await reloaded.GetProseHashesAsync(), [pending[0].Id, pending[2].Id], "Approved prose did not persist.");
        using (var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly))
        {
            Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM BasicQuestionBank") == 2, "Approval saved the wrong number of active questions.");
            Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM QuestionUserAuthorship") == 2, "Batch approval lost human-authorship provenance.");
        }
        var selected = await reloaded.TakeForProfileAsync(pending[0].Contract.Operation, pending[0].Contract.Tier,
            pending[0].Contract.Language, new(QuestionKnowledgeGroup.Objects));
        Check(selected is { UserAuthored: true } && QuestionBankStore.ValidateSavedQuestion(selected).IsValid,
            "Reloaded manual approval did not retain usable user-authored provenance.");
    }

    private static async Task AllUsesExplicitSnapshotAsync(string path)
    {
        var store = new QuestionBankStore(path);
        var original = await SeedAsync(store, "Delta", "Epsilon");
        string[] snapshot = (await store.GetPendingReviewsAsync()).Select(p => p.Id).ToArray();
        var later = (await SeedAsync(store, "Zeta")).Single();
        var result = await ResolveAsync(store, snapshot, true);
        AssertIds(result.ApprovedIds, original.Select(p => p.Id), "All snapshot did not approve its original rows.");
        AssertIds(await store.GetPendingProseHashesAsync(), [later.Id], "All snapshot implicitly approved a future arrival.");
        Check(!await store.ContainsProseAsync(AsQuestion(later)), "A future arrival entered the active bank.");
    }

    private static async Task RejectLeavesActiveQuestionsAsync(string path)
    {
        var store = new QuestionBankStore(path);
        var pending = await SeedAsync(store, "Eta", "Theta", "Iota");
        Check(await store.ApproveReviewAsync(pending[0].Id), "Could not seed a pre-existing active question.");
        var result = await ResolveAsync(store, [pending[0].Id, pending[1].Id], false);
        AssertIds(result.ApprovedIds, [], "Reject reported an approval.");
        AssertIds(result.RejectedIds, [pending[1].Id], "Reject did not report the pending row.");
        AssertIds(result.MissingIds, [pending[0].Id], "An active-only question was not reported missing from the pending queue.");
        AssertIds(await store.GetPendingProseHashesAsync(), [pending[2].Id], "Reject touched an unselected pending row.");
        AssertIds(await store.GetProseHashesAsync(), [pending[0].Id], "Reject deleted active prose or activated rejected prose.");
        Check(!await store.ContainsProseAsync(AsQuestion(pending[1])), "A rejected question became active.");
        using (var db = new SQLiteConnection(path))
            db.Execute("UPDATE QuestionPendingReview SET RawJson='{}',ContractJson='{}' WHERE Id=?", pending[2].Id);
        var malformed = await ResolveAsync(store, [pending[2].Id, pending[2].Id], false);
        AssertIds(malformed.RejectedIds, [pending[2].Id], "Reject could not discard an unreadable pending row.");
        Check(malformed.RejectedIds.Count == 1 && (await store.GetPendingProseHashesAsync()).Count == 0,
            "Reject did not deduplicate and delete the unreadable pending row exactly once.");
        AssertIds(await store.GetProseHashesAsync(), [pending[0].Id], "Discarding unreadable pending data touched an active question.");
    }

    private static async Task DeduplicatesAndReportsMissingAsync(string path)
    {
        var store = new QuestionBankStore(path);
        var pending = await SeedAsync(store, "Kappa", "Lambda");
        string uppercaseMissing = pending[1].Id.ToLowerInvariant();
        var result = await ResolveAsync(store, [pending[0].Id, pending[0].Id, "absent", "absent", uppercaseMissing], true);
        Check(result.ApprovedIds.Count == 1 && result.ApprovedIds[0] == pending[0].Id, "Duplicate IDs were resolved more than once.");
        Check(result.MissingIds.Count == 2, "Missing IDs were not deduplicated ordinally.");
        AssertIds(result.MissingIds, ["absent", uppercaseMissing], "Missing IDs were confused with active or differently cased IDs.");
        AssertIds(await store.GetPendingProseHashesAsync(), [pending[1].Id], "Ordinal ID deduplication changed a differently cased pending row.");
        var repeated = await ResolveAsync(store, [pending[0].Id], true);
        AssertIds(repeated.ApprovedIds, [], "Repeated batch approval claimed an absent pending row.");
        AssertIds(repeated.MissingIds, [pending[0].Id], "Repeated approval was not idempotent.");
    }

    private static async Task AlreadyActiveApprovalResolvesPendingAsync(string path)
    {
        var store = new QuestionBankStore(path);
        var pending = (await SeedAsync(store, "Mu")).Single();
        Check(await store.InsertAsync(AsQuestion(pending) with { UserAuthored = true }), "Could not seed a duplicate active question.");
        var result = await ResolveAsync(store, [pending.Id], true);
        AssertIds(result.ApprovedIds, [pending.Id], "Existing active prose left a stale pending card unresolved.");
        AssertIds(result.MissingIds, [], "Existing pending duplicate was misreported missing.");
        Check((await store.GetPendingProseHashesAsync()).Count == 0, "Already-active approval left its pending row.");
        using var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
        Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM BasicQuestionBank") == 1, "Already-active approval duplicated the question.");
        Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM QuestionUserAuthorship") == 1, "Already-active approval duplicated provenance.");
    }

    private static async Task InvalidRowsRollBackWholeBatchAsync(string path)
    {
        var store = new QuestionBankStore(path);
        var pending = await SeedAsync(store, "Nu", "Xi");
        using (var db = new SQLiteConnection(path))
            db.Execute("UPDATE QuestionPendingReview SET RawJson='{}' WHERE Id=?", pending[1].Id);
        await ThrowsAsync<InvalidDataException>(() => ResolveAsync(store, pending.Select(p => p.Id), true),
            "Malformed pending prose was silently skipped instead of aborting its batch.");
        AssertIds(await store.GetPendingProseHashesAsync(), pending.Select(p => p.Id), "Failure after a valid item deleted pending rows.");
        using (var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly))
        {
            Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM BasicQuestionBank") == 0, "Failure after a valid item partially committed an approval.");
            Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM QuestionUserAuthorship") == 0, "Rollback left approval provenance behind.");
            Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM QuestionProseIndex") == 0, "Rollback left an active prose identity behind.");
        }
        using (var db = new SQLiteConnection(path))
        {
            db.Execute("UPDATE QuestionPendingReview SET RawJson=? WHERE Id=?", pending[1].RawJson, pending[1].Id);
            db.Execute("UPDATE QuestionPendingReview SET ContractJson='{}' WHERE Id=?", pending[1].Id);
        }
        await ThrowsAsync<InvalidDataException>(() => ResolveAsync(store, pending.Select(p => p.Id), true),
            "An invalid pending contract did not abort its batch.");
        AssertIds(await store.GetPendingProseHashesAsync(), pending.Select(p => p.Id), "Invalid contract caused partial resolution.");
        Check((await store.GetProseHashesAsync()).Count == 0, "Invalid contract caused partial activation.");
        using (var db = new SQLiteConnection(path))
        {
            db.Execute("UPDATE QuestionPendingReview SET ContractJson=? WHERE Id=?",
                System.Text.Json.JsonSerializer.Serialize(pending[1].Contract), pending[1].Id);
            db.Execute("UPDATE QuestionPendingReview SET Id='stale-row-identity' WHERE Id=?", pending[1].Id);
        }
        await ThrowsAsync<InvalidDataException>(() => ResolveAsync(store, [pending[0].Id, "stale-row-identity"], true),
            "A pending row whose ID no longer matches its prose was approved.");
        AssertIds(await store.GetPendingProseHashesAsync(), [pending[0].Id, "stale-row-identity"], "Stale identity caused partial resolution.");
        Check((await store.GetProseHashesAsync()).Count == 0, "Stale identity caused partial activation.");
    }

    private static async Task CancellationLeavesBatchUnchangedAsync(string path)
    {
        var store = new QuestionBankStore(path);
        var pending = await SeedAsync(store, "Omicron", "Pi");
        foreach (bool approve in new[] { true, false })
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => ResolveAsync(store, pending.Select(p => p.Id), approve, cancellation.Token),
                "A pre-cancelled batch succeeded.");
        }
        // Cancel after the first input ID has been consumed. A streaming series
        // of individual writes would leave that first approval committed.
        using (var cancellation = new CancellationTokenSource())
        {
            await ThrowsAsync<OperationCanceledException>(() => ResolveAsync(store, CancelDuringEnumeration(pending, cancellation), true, cancellation.Token),
                "Cancellation after consuming part of the input committed an approval.");
        }
        AssertIds(await store.GetPendingProseHashesAsync(), pending.Select(p => p.Id), "Cancellation lost pending rows.");
        Check((await store.GetProseHashesAsync()).Count == 0, "Cancellation partially activated the batch.");
        using var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
        Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM QuestionUserAuthorship") == 0, "Cancellation left human provenance behind.");
    }

    private static async Task OverlappingBatchesResolveEachIdOnceAsync(string path)
    {
        var store = new QuestionBankStore(path);
        var pending = await SeedAsync(store, "Rho", "Sigma", "Tau");
        var operations = await Task.WhenAll(
            ResolveAsync(store, [pending[0].Id, pending[1].Id], true),
            ResolveAsync(store, [pending[1].Id, pending[2].Id], true));
        var approved = operations.SelectMany(r => r.ApprovedIds).ToArray();
        Check(approved.Length == 3, "Overlapping approvals resolved an ID twice or lost a question.");
        AssertIds(approved, pending.Select(p => p.Id), "Overlapping approvals saved the wrong IDs.");
        AssertIds(operations.SelectMany(r => r.MissingIds), [pending[1].Id], "The stale overlapping ID was not reported missing once.");
        Check((await store.GetPendingProseHashesAsync()).Count == 0, "Overlapping batches left a pending row.");
        using var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
        Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM BasicQuestionBank") == 3
            && db.ExecuteScalar<int>("SELECT COUNT(*) FROM QuestionUserAuthorship") == 3,
            "Overlapping batches duplicated active data or lost provenance.");
    }

    private static async Task ValidatesInputsBeforeMutationAsync(string path)
    {
        var store = new QuestionBankStore(path);
        var pending = (await SeedAsync(store, "Upsilon")).Single();
        foreach (bool approve in new[] { true, false })
        {
            var empty = await ResolveAsync(store, [], approve);
            Check(empty.ApprovedIds.Count + empty.RejectedIds.Count + empty.MissingIds.Count == 0, "Empty batch was not a no-op.");
            await ThrowsAsync<ArgumentException>(() => ResolveAsync(store, [pending.Id, " "], approve), "Blank IDs were accepted.");
            await ThrowsAsync<ArgumentException>(() => ResolveAsync(store, [pending.Id, null!], approve), "Null IDs were accepted.");
            await ThrowsAsync<ArgumentNullException>(() => ResolveAsync(store, null!, approve), "A null ID collection was accepted.");
            await ThrowsAsync<InvalidOperationException>(() => ResolveAsync(store, FailsDuringEnumeration(pending.Id), approve),
                "A failed input enumeration was swallowed.");
        }
        AssertIds(await store.GetPendingProseHashesAsync(), [pending.Id], "Input validation happened after a partial mutation.");
        Check((await store.GetProseHashesAsync()).Count == 0, "Invalid input activated a question.");
    }

    private static IEnumerable<string> FailsDuringEnumeration(string id)
    {
        yield return id;
        throw new InvalidOperationException("fixture enumeration failure");
    }

    private static IEnumerable<string> CancelDuringEnumeration(PendingQuestionReview[] pending, CancellationTokenSource cancellation)
    {
        yield return pending[0].Id;
        cancellation.Cancel();
        yield return pending[1].Id;
    }

    private static async Task<PendingQuestionReview[]> SeedAsync(QuestionBankStore store, params string[] labels)
    {
        var contract = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add,
            CurriculumTier.ThreeStars, AppLanguage.English, new(713)) with { OpenProse = true };
        var result = new List<PendingQuestionReview>();
        foreach (string label in labels)
        {
            var example = OpenQuestionProse.Example(contract);
            string prefix = "Zephyrus " + label + ", ";
            var draft = example.Facts is null ? example with { GivenA = prefix + example.GivenA }
                : example with { Facts = example.Facts.Select((fact, i) => i == 0 ? fact with { Text = prefix + fact.Text } : fact).ToArray() };
            string raw = QuestionBankStore.SerializeDraft(draft);
            Check(BasicQuestionValidator.Validate(raw, contract).NeedsReview, "Batch fixture did not require semantic review.");
            var pending = PendingQuestionReview.Create(contract, draft, raw, "isolated-batch-review-fixture", "human review");
            Check(await store.QueueReviewAsync(pending), "Could not queue isolated batch fixture.");
            result.Add(pending);
        }
        return result.ToArray();
    }

    private static ValidatedBankQuestion AsQuestion(PendingQuestionReview pending) =>
        new(pending.Contract, pending.Draft, pending.RawJson, pending.ModelName, pending.CreatedUtc);

    private sealed record BatchResult(IReadOnlyList<string> ApprovedIds, IReadOnlyList<string> RejectedIds, IReadOnlyList<string> MissingIds);

    private static async Task<BatchResult> ResolveAsync(QuestionBankStore store, IEnumerable<string> ids, bool approve, CancellationToken cancellationToken = default)
    {
        var method = typeof(QuestionBankStore).GetMethod("ResolveReviewsAsync", [typeof(IEnumerable<string>), typeof(bool), typeof(CancellationToken)])
            ?? throw new InvalidOperationException("Missing batch review API.");
        Task operation;
        try { operation = (Task)method.Invoke(store, [ids, approve, cancellationToken])!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        await operation;
        object result = operation.GetType().GetProperty("Result")!.GetValue(operation)!;
        IReadOnlyList<string> Read(string name) => (IReadOnlyList<string>)(result.GetType().GetProperty(name)?.GetValue(result)
            ?? throw new InvalidOperationException("Batch result missing " + name));
        return new(Read("ApprovedIds"), Read("RejectedIds"), Read("MissingIds"));
    }

    private static void AssertIds(IEnumerable<string> actual, IEnumerable<string> expected, string message) =>
        Check(actual.ToHashSet(StringComparer.Ordinal).SetEquals(expected), message);

    private static async Task ThrowsAsync<T>(Func<Task> action, string message) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException(message);
    }
}
