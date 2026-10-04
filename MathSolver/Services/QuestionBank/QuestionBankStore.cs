using MathSolver.Models;
using SQLite;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public sealed record ValidatedBankQuestion(BasicQuestionContract Contract, BasicQuestionDraft Draft,
    string RawJson, string ModelName, DateTime CreatedUtc)
{
    public MathWordProblem WordProblem => Draft.ToWordProblem(Contract);
}

public interface IQuestionBankStore
{
    Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default);
    Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, CancellationToken cancellationToken = default);
}

/// <summary>Short serialized SQLite operations; no connection/transaction spans inference.</summary>
public sealed partial class QuestionBankStore(string databasePath) : IQuestionBankStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;
    // Accessed only inside the database gate. Keep a small history, never the question payloads.
    private readonly Dictionary<(CurriculumTier, AppLanguage), List<AdditionBucket>> _additionHistory = [];
    private static readonly JsonSerializerOptions JsonOptions = new() {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [Table("BasicQuestionBank")]
    public sealed class Row
    {
        [PrimaryKey] public string Hash { get; set; } = "";
        [Indexed(Name = "QuestionSelection", Order = 1)] public int Operation { get; set; }
        [Indexed(Name = "QuestionSelection", Order = 2)] public int Stars { get; set; }
        [Indexed(Name = "QuestionSelection", Order = 3)] public int Language { get; set; }
        public int Version { get; set; }
        public string ContractJson { get; set; } = "";
        public string DraftJson { get; set; } = "";
        public string RawJson { get; set; } = "";
        public string ModelName { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public DateTime LastUsedUtc { get; set; }
        public long UseCount { get; set; }
        public int Structure { get; set; }
        public string TopicId { get; set; } = "";
        public string SceneId { get; set; } = "";
    }

    public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
        => WithDatabaseAsync(db => Insert(db, question), cancellationToken);

    private static bool Insert(SQLiteConnection db, ValidatedBankQuestion question)
    {
        // Validate again at the storage boundary, including manual insertion.
        string draftJson = SerializeDraft(question.Draft);
        var checkedDraft = BasicQuestionValidator.Validate(draftJson, question.Contract);
        if (!checkedDraft.IsValid) throw new InvalidOperationException(checkedDraft.ErrorCode);
        if (checkedDraft.Contract is { } resolved && resolved != question.Contract)
            throw new InvalidOperationException("ChangedUnits");
        string contractJson = JsonSerializer.Serialize(question.Contract, JsonOptions);
        var c = question.Contract;
        // Keep the original v1 hash format so old databases/Excel imports still deduplicate.
        string legacyContract = JsonSerializer.Serialize(new { c.Version, c.Operation, c.Tier, c.Language,
            c.Left, c.Right, c.Subject, c.Unit, c.GroupUnit }, JsonOptions);
        string identity = c.IsTemplate
            ? $"{c.Version}/{c.Operation}/{c.Tier}/{c.Language}/{c.Structure}/{question.Draft.UnitId}\n{draftJson}"
            : legacyContract + "\n" + BasicQuestionValidator.Normalize(question.Draft.ProblemText);
        if (c.Version == AdditionQuestionCatalogue.Version)
            identity = $"{c.Version}/{c.Operation}/{c.Tier}/{c.Language}/{c.Structure}/{c.TopicId}/{c.SceneId}/{question.Draft.UnitId}\n{draftJson}";
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return db.Execute(
            "INSERT OR IGNORE INTO BasicQuestionBank (Hash,Operation,Stars,Language,Version,ContractJson,DraftJson,RawJson,ModelName,CreatedUtc,LastUsedUtc,UseCount,Structure,TopicId,SceneId) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            hash, (int)question.Contract.Operation, (int)question.Contract.Tier, (int)question.Contract.Language,
            question.Contract.Version, contractJson, draftJson, question.RawJson, question.ModelName,
            question.CreatedUtc, DateTime.MinValue, 0, (int)c.Structure, c.TopicId, c.SceneId) == 1;
    }

    public async Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, CancellationToken cancellationToken = default)
    {
        try
        {
            return await WithDatabaseAsync<ValidatedBankQuestion?>(db =>
            {
                var candidates = operation == ArithmeticOperation.Add ? AdditionCandidates(db, tier, language)
                    : db.Query<Row>("SELECT * FROM BasicQuestionBank WHERE Operation=? AND Stars=? AND Language=? AND Version IN (1,2) ORDER BY UseCount,LastUsedUtc LIMIT 32",
                        (int)operation, (int)tier, (int)language);
                foreach (var row in candidates)
                {
                    try
                    {
                        var contract = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson, JsonOptions);
                        if (contract is null || row.Version != contract.Version || contract.Operation != operation || contract.Tier != tier || contract.Language != language)
                            continue;
                        if (contract.Version == AdditionQuestionCatalogue.Version && ((int)contract.Structure != row.Structure
                            || contract.TopicId != row.TopicId || contract.SceneId != row.SceneId)) continue;
                        var validation = BasicQuestionValidator.Validate(row.DraftJson, contract);
                        if (!validation.IsValid) continue;
                        if (validation.Contract is { } resolved && resolved != contract) continue;
                        db.Execute("UPDATE BasicQuestionBank SET UseCount=UseCount+1,LastUsedUtc=? WHERE Hash=?", DateTime.UtcNow, row.Hash);
                        if (operation == ArithmeticOperation.Add)
                        {
                            var key = (tier, language);
                            if (!_additionHistory.TryGetValue(key, out var history)) _additionHistory[key] = history = [];
                            history.Add(new() { Structure = (int)contract.Structure, TopicId = contract.TopicId, SceneId = contract.SceneId });
                            if (history.Count > 64) history.RemoveAt(0);
                        }
                        return new(contract, validation.Draft!, row.RawJson, row.ModelName, row.CreatedUtc);
                    }
                    catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
                    { Debug.WriteLine($"Question bank row skipped: {error.GetType().Name}"); }
                }
                return null;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is SQLiteException or IOException or UnauthorizedAccessException)
        {
            // The normal C# generators remain available even if storage fails.
            Debug.WriteLine($"Question bank unavailable: {error.GetType().Name}");
            return null;
        }
    }

    private sealed class AdditionBucket
    {
        public int Structure { get; set; }
        public string TopicId { get; set; } = "";
        public string SceneId { get; set; } = "";
        public DateTime LastUsedUtc { get; set; }
    }

    private IEnumerable<Row> AdditionCandidates(SQLiteConnection db, CurriculumTier tier, AppLanguage language)
    {
        var buckets = db.Query<AdditionBucket>("SELECT COALESCE(Structure,0) AS Structure,COALESCE(TopicId,'') AS TopicId,COALESCE(SceneId,'') AS SceneId,MAX(LastUsedUtc) AS LastUsedUtc FROM BasicQuestionBank WHERE Operation=0 AND Stars=? AND Language=? AND Version IN (1,2,3) GROUP BY COALESCE(Structure,0),COALESCE(TopicId,''),COALESCE(SceneId,'')",
            (int)tier, (int)language);
        // Ignore forged metadata buckets before sorting, but retain historical v1/v2 rows.
        buckets = buckets.Where(b => b.SceneId == "" && b.TopicId == "" || AdditionQuestionCatalogue.Find(b.SceneId) is { } scene
            && scene.TopicId == b.TopicId && scene.Supports((BasicQuestionStructure)b.Structure, tier)).ToList();
        var history = _additionHistory.GetValueOrDefault((tier, language)) ?? [];
        var relationUsed = buckets.GroupBy(b => b.Structure).ToDictionary(g => g.Key, g => g.Max(b => b.LastUsedUtc));
        var topicUsed = buckets.GroupBy(b => b.TopicId).ToDictionary(g => g.Key, g => g.Max(b => b.LastUsedUtc));
        var ordered = buckets.OrderBy(_ => Random.Shared.Next())
            .OrderBy(b => history.Count(h => h.Structure == b.Structure)).ThenBy(b => relationUsed[b.Structure])
            .ThenBy(b => history.Count(h => h.TopicId == b.TopicId)).ThenBy(b => history.Count(h => h.SceneId == b.SceneId))
            .ThenBy(b => topicUsed[b.TopicId])
            .ThenBy(b => history.Count > 0 && history[^1].SceneId == b.SceneId).ThenBy(b => b.LastUsedUtc);
        foreach (var bucket in ordered)
            foreach (var row in db.Query<Row>("SELECT * FROM BasicQuestionBank WHERE Operation=0 AND Stars=? AND Language=? AND Version IN (1,2,3) AND COALESCE(Structure,0)=? AND COALESCE(TopicId,'')=? AND COALESCE(SceneId,'')=? ORDER BY UseCount,LastUsedUtc LIMIT 32",
                (int)tier, (int)language, bucket.Structure, bucket.TopicId, bucket.SceneId)) yield return row;
    }

    public static string SerializeDraft(BasicQuestionDraft draft) => draft.UnitId is null && draft.SolutionLead is null
        ? JsonSerializer.Serialize(new { given_a = draft.GivenA, given_b = draft.GivenB, question = draft.Question }, JsonOptions)
        : JsonSerializer.Serialize(new { given_a = draft.GivenA, given_b = draft.GivenB, question = draft.Question,
            solution_lead = draft.SolutionLead, unit_id = draft.UnitId }, JsonOptions);

    private async Task<T> WithDatabaseAsync<T>(Func<SQLiteConnection, T> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
                using var db = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
                db.BusyTimeout = TimeSpan.FromSeconds(2);
                if (!_initialized)
                {
                    db.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
                    db.CreateTable<Row>();
                    // SQLite-net adds missing columns in existing installations; old payloads stay intact.
                    db.Execute("CREATE INDEX IF NOT EXISTS AdditionSelection ON BasicQuestionBank(Operation,Stars,Language,Structure,TopicId,SceneId)");
                    _initialized = true;
                }
                return action(db);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
