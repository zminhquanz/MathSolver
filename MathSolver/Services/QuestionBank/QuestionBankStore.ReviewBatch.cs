namespace MathSolver.Services.QuestionBank;

public sealed record QuestionReviewBatchResult(
    IReadOnlyList<string> ApprovedIds,
    IReadOnlyList<string> RejectedIds,
    IReadOnlyList<string> MissingIds);

public sealed partial class QuestionBankStore
{
    /// <summary>Resolve only the supplied pending IDs in one atomic manual review.</summary>
    public Task<QuestionReviewBatchResult> ResolveReviewsAsync(IEnumerable<string> ids, bool approve,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        // Finish enumeration and validation before opening a transaction. The caller's
        // explicit snapshot must never grow into a later view of the pending queue.
        string[] snapshot = ids.ToArray();
        if (snapshot.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Review IDs must not be null or blank.", nameof(ids));
        snapshot = snapshot.Distinct(StringComparer.Ordinal).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot.Length == 0)
            return Task.FromResult(new QuestionReviewBatchResult([], [], []));

        return WithDatabaseAsync(db =>
        {
            var approved = new List<string>();
            var rejected = new List<string>();
            var missing = new List<string>();
            db.RunInTransaction(() =>
            {
                foreach (string id in snapshot)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var row = db.Find<PendingReviewRow>(id);
                    if (row is null)
                    {
                        missing.Add(id);
                        continue;
                    }
                    if (approve)
                    {
                        var pending = ReadPending(row);
                        if (pending is null || !pending.Contract.IsValid
                            || !StringComparer.Ordinal.Equals(id, QuestionProseIdentity.Hash(pending.Contract, pending.Draft)))
                            throw new InvalidDataException("The pending review is invalid or its identity has changed.");

                        bool inserted = Insert(db, new(pending.Contract, pending.Draft, pending.RawJson,
                            pending.ModelName, pending.CreatedUtc) { UserAuthored = true });
                        if (!inserted && !ProseExists(db, id))
                            throw new InvalidDataException("The pending review could not be saved.");
                        approved.Add(id);
                    }
                    else rejected.Add(id);

                    // Rejection intentionally needs no readable payload and never
                    // removes active prose. Approval includes an already-active copy.
                    db.Delete<PendingReviewRow>(id);
                }
                cancellationToken.ThrowIfCancellationRequested();
            });
            return new QuestionReviewBatchResult(approved.ToArray(), rejected.ToArray(), missing.ToArray());
        }, cancellationToken);
    }
}
