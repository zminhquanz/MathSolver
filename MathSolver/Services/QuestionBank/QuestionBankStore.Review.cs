using SQLite;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public sealed record PendingQuestionReview(string Id, BasicQuestionContract Contract, BasicQuestionDraft Draft,
    string RawJson, string ModelName, DateTime CreatedUtc, string Reason)
{
    public static PendingQuestionReview Create(BasicQuestionContract c, BasicQuestionDraft d, string raw, string model, string reason) =>
        new(QuestionProseIdentity.Hash(c,d),c,d,raw,model,DateTime.UtcNow,reason);
}

public sealed partial class QuestionBankStore
{
    [Table("QuestionPendingReview")]
    public sealed class PendingReviewRow
    {
        [PrimaryKey] public string Id { get; set; } = "";
        public string ContractJson { get; set; } = "";
        public string RawJson { get; set; } = "";
        public string ModelName { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public string Reason { get; set; } = "";
    }

    public Task<bool> QueueReviewAsync(PendingQuestionReview pending, CancellationToken cancellationToken = default) =>
        WithDatabaseAsync(db =>
        {
            var validation = BasicQuestionValidator.Validate(pending.RawJson,pending.Contract);
            if (!pending.Contract.OpenProse || !validation.NeedsReview) throw new InvalidDataException("Only inconclusive pilot drafts can await review.");
            string id = QuestionProseIdentity.Hash(pending.Contract,validation.Draft!);
            if (ProseExists(db,id)) return false;
            return db.Insert(new PendingReviewRow { Id=id,ContractJson=JsonSerializer.Serialize(pending.Contract,JsonOptions),
                RawJson=pending.RawJson,ModelName=pending.ModelName,CreatedUtc=pending.CreatedUtc,Reason=validation.ErrorDetails??pending.Reason },"OR IGNORE") == 1;
        },cancellationToken);

    public Task<IReadOnlyList<PendingQuestionReview>> GetPendingReviewsAsync(CancellationToken cancellationToken = default) =>
        WithDatabaseAsync<IReadOnlyList<PendingQuestionReview>>(db => db.Table<PendingReviewRow>().OrderByDescending(r=>r.CreatedUtc).ToList()
            .Select(ReadPending).OfType<PendingQuestionReview>().ToArray(),cancellationToken);

    public Task<IReadOnlySet<string>> GetPendingProseHashesAsync(CancellationToken cancellationToken=default) =>
        WithDatabaseAsync<IReadOnlySet<string>>(db=>db.Table<PendingReviewRow>().ToList().Select(r=>r.Id).ToHashSet(StringComparer.Ordinal),cancellationToken);

    private static PendingQuestionReview? ReadPending(PendingReviewRow row)
    {
        try
        {
            var c=JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson,JsonOptions);
            if(c is null || !c.OpenProse) return null;
            var validation=OpenQuestionProse.ValidateSyntax(row.RawJson,c);
            return !validation.IsValid ? null : new(row.Id,c,validation.Draft!,row.RawJson,row.ModelName,row.CreatedUtc,row.Reason);
        }
        catch(Exception e) when(e is JsonException or ArgumentException or InvalidOperationException) { return null; }
    }

    // Called only by an explicit user review action. The existing user-authorship
    // provenance keeps this human decision distinct from automatic verification.
    public Task<bool> ApproveReviewAsync(string id, CancellationToken cancellationToken = default) => WithDatabaseAsync(db =>
    {
        var row=db.Find<PendingReviewRow>(id);
        var pending=row is null?null:ReadPending(row);
        if(pending is null) return false;
        bool inserted=false;
        db.RunInTransaction(()=>
        {
            inserted=Insert(db,new(pending.Contract,pending.Draft,pending.RawJson,pending.ModelName,pending.CreatedUtc){UserAuthored=true});
            if(inserted || ProseExists(db,id)) db.Delete<PendingReviewRow>(id);
        });
        return inserted;
    },cancellationToken);

    public Task<bool> RejectReviewAsync(string id,CancellationToken cancellationToken=default) =>
        WithDatabaseAsync(db=>db.Delete<PendingReviewRow>(id)>0,cancellationToken);
}
