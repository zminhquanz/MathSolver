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
    public bool UserAuthored { get; init; }
    public MathWordProblem WordProblem => Draft.ToWordProblem(Contract);
}

public interface IQuestionBankStore
{
    Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        => Task.FromException<int>(new NotSupportedException());
    Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default);
    Task<bool> ContainsProseAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
    Task<IReadOnlySet<string>> GetProseHashesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
    Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, CancellationToken cancellationToken = default);
    Task<ValidatedBankQuestion?> TakeReasoningAsync(BankQuestionFamily family, int variant, CurriculumTier tier,
        AppLanguage language, CancellationToken cancellationToken = default)
        => Task.FromResult<ValidatedBankQuestion?>(null);
    async Task<ValidatedBankQuestion?> TakeChartAsync(DataChartProfile profile, CancellationToken cancellationToken = default)
    {
        var saved = await TakeReasoningAsync(BankQuestionFamily.Data, (int)profile.Type, profile.Tier, profile.Language, cancellationToken).ConfigureAwait(false);
        return saved?.Contract.Story?.ChartProfile is { } stored && ChartProfilesMatch(stored, profile) ? saved : null;
    }
    internal static bool ChartProfilesMatch(DataChartProfile stored, DataChartProfile selected) =>
        stored.Type == selected.Type && stored.Tier == selected.Tier && stored.Language == selected.Language
        && stored.ContextId == selected.ContextId && stored.QuestionKind == selected.QuestionKind
        && stored.CategoryIds.SequenceEqual(selected.CategoryIds) && stored.TargetCategoryIds.SequenceEqual(selected.TargetCategoryIds)
        && stored.HiddenCategoryIds.SequenceEqual(selected.HiddenCategoryIds);
    Task<ValidatedBankQuestion?> TakeFractionAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
        => Task.FromResult<ValidatedBankQuestion?>(null);
    Task<ValidatedBankQuestion?> TakeFindXAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
        => Task.FromResult<ValidatedBankQuestion?>(null);
    async Task<ValidatedBankQuestion?> TakeForProfileAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
    {
        var question = await TakeAsync(operation, tier, language, cancellationToken).ConfigureAwait(false);
        return question?.Contract is { } c && profile.Includes(c.KnowledgeGroup) ? question : null;
    }
}

/// <summary>Short serialized SQLite operations; no connection/transaction spans inference.</summary>
public sealed partial class QuestionBankStore(string databasePath) : IQuestionBankStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;
    // Accessed only inside the database gate. Keep a small history, never the question payloads.
    private readonly Dictionary<(ArithmeticOperation, CurriculumTier, AppLanguage), List<SelectionBucket>> _selectionHistory = [];
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
        public int Grade { get; set; }
        public int KnowledgeGroup { get; set; }
        public int ProblemType { get; set; }
        public int ProblemVariant { get; set; }
    }

    public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
        => WithDatabaseAsync(db => Insert(db, question), cancellationToken);

    /// <summary>Delete saved questions atomically, retaining the schema and model files.</summary>
    public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        => WithDatabaseAsync(db =>
        {
            int deleted = 0;
            db.RunInTransaction(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                deleted = db.Execute("DELETE FROM BasicQuestionBank");
                // Also remove any stale derived identities left by older installations.
                db.Execute("DELETE FROM QuestionProseIndex");
                cancellationToken.ThrowIfCancellationRequested();
            });
            _selectionHistory.Clear();
            _findXHistory.Clear();
            _fractionHistory.Clear();
            _storyHistory.Clear();
            return deleted;
        }, cancellationToken);

    private static bool Insert(SQLiteConnection db, ValidatedBankQuestion question)
    {
        // Recheck syntax for user templates; retain strict semantic validation for AI.
        string draftJson = SerializeDraft(question.Draft);
        var checkedDraft = ValidateSavedQuestion(question);
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
        if (c.Version is AdditionQuestionCatalogue.Version or ArithmeticQuestionCatalogue.Version)
            identity = $"{c.Version}/{c.Operation}/{c.Tier}/{c.Language}/{c.Structure}/{c.TopicId}/{c.SceneId}/{question.Draft.UnitId}\n{draftJson}";
        if (c.Version is AppliedQuestionCatalogue.Version or FindXQuestionCatalogue.Version or FractionQuestionCatalogue.Version)
            identity = $"{c.Version}/{c.Operation}/{c.Tier}/{c.Language}/{c.Grade}/{c.KnowledgeGroup}/{c.SceneId}/{question.Draft.UnitId}\n{draftJson}";
        if (c.Version == ReasoningStoryCatalogue.Version)
            identity = $"{c.Version}/{c.Family}/{c.BankVariant}/{c.Tier}/{c.Language}/{c.Story!.Schema}\n{draftJson}";
        if (c.Family == BankQuestionFamily.FindX) identity += "\n" + c.UnknownRole;
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        string proseHash = QuestionProseIdentity.Hash(c, question.Draft);
        bool inserted = false;
        db.RunInTransaction(() =>
        {
            RefreshProseIndex(db);
            if (ProseExists(db, proseHash)) return;
            inserted = db.Execute(
                "INSERT OR IGNORE INTO BasicQuestionBank (Hash,Operation,Stars,Language,Version,ContractJson,DraftJson,RawJson,ModelName,CreatedUtc,LastUsedUtc,UseCount,Structure,TopicId,SceneId,Grade,KnowledgeGroup,ProblemType,ProblemVariant) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                hash, (int)question.Contract.Operation, (int)question.Contract.Tier, (int)question.Contract.Language,
                question.Contract.Version, contractJson, draftJson, question.RawJson, question.ModelName,
                question.CreatedUtc, DateTime.MinValue, 0, (int)c.Structure, c.TopicId, c.SceneId, c.Grade, (int)c.KnowledgeGroup, (int)c.Family, c.BankVariant) == 1;
            if (inserted)
            {
                if (question.UserAuthored) db.Execute("INSERT INTO QuestionUserAuthorship(Hash) VALUES (?)", hash);
                db.Execute("INSERT OR REPLACE INTO QuestionProseIndex(Hash,ProseHash) VALUES (?,?)", hash, proseHash);
            }
        });
        return inserted;
    }

    public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, CancellationToken cancellationToken = default)
        => TakeCoreAsync(operation, tier, language, null, cancellationToken);

    public Task<ValidatedBankQuestion?> TakeForProfileAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
        => TakeCoreAsync(operation, tier, language, profile, cancellationToken);

    private async Task<ValidatedBankQuestion?> TakeCoreAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, QuestionLearningProfile? profile, CancellationToken cancellationToken)
    {
        try
        {
            return await WithDatabaseAsync<ValidatedBankQuestion?>(db =>
            {
                var candidates = ContextCandidates(db, operation, tier, language, profile);
                foreach (var row in candidates)
                {
                    try
                    {
                        var contract = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson, JsonOptions);
                        if (contract is null || row.ProblemType != (int)contract.Family || row.ProblemVariant != (int)contract.UnknownRole || row.Version != contract.Version || contract.Operation != operation || contract.Tier != tier || contract.Language != language)
                            continue;
                        if (contract.Version is AdditionQuestionCatalogue.Version or ArithmeticQuestionCatalogue.Version or AppliedQuestionCatalogue.Version && ((int)contract.Structure != row.Structure
                            || contract.TopicId != row.TopicId || contract.SceneId != row.SceneId)) continue;
                        if (contract.Grade != row.Grade || (int)contract.KnowledgeGroup != row.KnowledgeGroup
                            || profile is not null && !profile.Includes(contract.KnowledgeGroup)) continue;
                        bool userAuthored = IsUserAuthored(db, row.Hash);
                        var validation = ValidateDraft(row.DraftJson, contract, userAuthored);
                        if (!validation.IsValid) continue;
                        if (validation.Contract is { } resolved && resolved != contract) continue;
                        db.Execute("UPDATE BasicQuestionBank SET UseCount=UseCount+1,LastUsedUtc=? WHERE Hash=?", DateTime.UtcNow, row.Hash);
                        var key = (operation, tier, language);
                        if (!_selectionHistory.TryGetValue(key, out var history)) _selectionHistory[key] = history = [];
                        history.Add(new() { Structure = (int)contract.Structure, TopicId = contract.TopicId, SceneId = contract.SceneId });
                        if (history.Count > 64) history.RemoveAt(0);
                        return new(contract, validation.Draft!, row.RawJson, row.ModelName, row.CreatedUtc) { UserAuthored = userAuthored };
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

    private sealed class SelectionBucket
    {
        public int Structure { get; set; }
        public string TopicId { get; set; } = "";
        public string SceneId { get; set; } = "";
        public DateTime LastUsedUtc { get; set; }
    }

    private IEnumerable<Row> ContextCandidates(SQLiteConnection db, ArithmeticOperation operation, CurriculumTier tier, AppLanguage language,
        QuestionLearningProfile? profile)
    {
        // Historical imports can omit the new columns even after initialization.
        // Treat NULL family/group as the original arithmetic/objects defaults.
        string filter = "COALESCE(ProblemType,0)=0 AND Operation=? AND Stars=? AND Language=? AND " + (profile is null
            ? "Version IN (1,2,3,4)" : profile.Group == QuestionKnowledgeGroup.Objects
                ? "Version IN (1,2,3,4,5) AND COALESCE(KnowledgeGroup,0)=?" : profile.Group == QuestionKnowledgeGroup.Measurement
                    ? "Version=5 AND KnowledgeGroup IN (2,3,4,7)" : "Version=5 AND KnowledgeGroup=?");
        object[] args = profile is null ? [(int)operation, (int)tier, (int)language]
            : profile.Group == QuestionKnowledgeGroup.Measurement ? [(int)operation, (int)tier, (int)language]
            : [(int)operation, (int)tier, (int)language, (int)profile.Group];
        var buckets = db.Query<SelectionBucket>("SELECT COALESCE(Structure,0) AS Structure,COALESCE(TopicId,'') AS TopicId,COALESCE(SceneId,'') AS SceneId,MAX(LastUsedUtc) AS LastUsedUtc FROM BasicQuestionBank WHERE "
            + filter + " GROUP BY COALESCE(Structure,0),COALESCE(TopicId,''),COALESCE(SceneId,'')", args);
        // Ignore forged metadata buckets before sorting, but retain historical v1/v2 rows.
        bool LegacyBucket(SelectionBucket b) => b.SceneId == "" && b.TopicId == "" || AdditionQuestionCatalogue.Find(b.SceneId) is { } scene
            && scene.TopicId == b.TopicId && (operation == ArithmeticOperation.Add ? scene.Supports((BasicQuestionStructure)b.Structure, tier)
                : ArithmeticQuestionCatalogue.Supports(b.SceneId, operation, tier, (BasicQuestionStructure)b.Structure));
        buckets = buckets.Where(b => profile is not null
            ? Enum.TryParse<QuestionKnowledgeGroup>(b.TopicId, out var group) && profile.Includes(group)
                && AppliedQuestionCatalogue.Available(profile, operation, tier).Any(s => s.Id == b.SceneId)
                || profile.Group == QuestionKnowledgeGroup.Objects && LegacyBucket(b)
            : LegacyBucket(b)).ToList();
        var history = _selectionHistory.GetValueOrDefault((operation, tier, language)) ?? [];
        var relationUsed = buckets.GroupBy(b => b.Structure).ToDictionary(g => g.Key, g => g.Max(b => b.LastUsedUtc));
        var topicUsed = buckets.GroupBy(b => b.TopicId).ToDictionary(g => g.Key, g => g.Max(b => b.LastUsedUtc));
        var ordered = buckets.OrderBy(_ => Random.Shared.Next())
            .OrderBy(b => history.Count(h => h.Structure == b.Structure)).ThenBy(b => relationUsed[b.Structure])
            // Prefer an unused setting before its broad topic. Otherwise a
            // school-supplies setting can starve because the library was used.
            .ThenBy(b => history.Count(h => h.SceneId == b.SceneId)).ThenBy(b => history.Count(h => h.TopicId == b.TopicId))
            .ThenBy(b => topicUsed[b.TopicId])
            .ThenBy(b => history.Count > 0 && history[^1].SceneId == b.SceneId).ThenBy(b => b.LastUsedUtc);
        foreach (var bucket in ordered)
            foreach (var row in db.Query<Row>("SELECT * FROM BasicQuestionBank WHERE " + filter
                + " AND COALESCE(Structure,0)=? AND COALESCE(TopicId,'')=? AND COALESCE(SceneId,'')=? ORDER BY UseCount,LastUsedUtc LIMIT 32",
                [.. args, bucket.Structure, bucket.TopicId, bucket.SceneId])) yield return row;
    }

    public static BasicDraftValidation ValidateSavedQuestion(ValidatedBankQuestion question)
        => ValidateDraft(SerializeDraft(question.Draft), question.Contract, question.UserAuthored);

    internal static BasicDraftValidation ValidateDraft(string json, BasicQuestionContract contract, bool userAuthored)
        => userAuthored ? UserQuestionTemplateValidator.Validate(json, contract) : BasicQuestionValidator.Validate(json, contract);

    private static bool IsUserAuthored(SQLiteConnection db, string hash)
        => db.ExecuteScalar<int>("SELECT EXISTS(SELECT 1 FROM QuestionUserAuthorship WHERE Hash=?)", hash) != 0;

    public static string SerializeDraft(BasicQuestionDraft draft) => draft.Facts is not null
        ? JsonSerializer.Serialize(new { facts = draft.Facts.Select(f => new { role = f.Role, text = f.Text }), question = draft.Question,
            solution_leads = (draft.SolutionLeads ?? []).Select(l => new { step = l.Role, text = l.Text }) }, JsonOptions)
        : draft.UnitId is null && draft.SolutionLead is null
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
                    RenameProblemColumns(db);
                    db.CreateTable<Row>();
                    // Added integer columns can be NULL on pre-v6 SQLite tables.
                    // Keep their arithmetic rows available after the schema upgrade.
                    db.Execute("UPDATE BasicQuestionBank SET ProblemType=0 WHERE ProblemType IS NULL");
                    db.Execute("UPDATE BasicQuestionBank SET ProblemVariant=0 WHERE ProblemVariant IS NULL");
                    // SQLite-net adds missing columns in existing installations; old payloads stay intact.
                    db.Execute("CREATE INDEX IF NOT EXISTS AdditionSelection ON BasicQuestionBank(Operation,Stars,Language,Structure,TopicId,SceneId)");
                    db.Execute("CREATE INDEX IF NOT EXISTS KnowledgeSelection ON BasicQuestionBank(Operation,Stars,Language,KnowledgeGroup,SceneId)");
                    db.Execute("CREATE INDEX IF NOT EXISTS FindXSelection ON BasicQuestionBank(ProblemType,Operation,Stars,Language,KnowledgeGroup,ProblemVariant,SceneId)");
                    db.Execute("CREATE INDEX IF NOT EXISTS StorySelection ON BasicQuestionBank(ProblemType,ProblemVariant,Stars,Language,SceneId)");
                    // Keep provenance outside the public grid schema and old SELECT * imports.
                    db.Execute("CREATE TABLE IF NOT EXISTS QuestionUserAuthorship(Hash TEXT PRIMARY KEY NOT NULL)");
                    db.Execute("CREATE TRIGGER IF NOT EXISTS QuestionUserAuthorshipDeleted AFTER DELETE ON BasicQuestionBank BEGIN DELETE FROM QuestionUserAuthorship WHERE Hash=OLD.Hash; END");
                    db.Execute("CREATE TRIGGER IF NOT EXISTS QuestionUserAuthorshipRenamed AFTER UPDATE OF Hash ON BasicQuestionBank BEGIN UPDATE OR REPLACE QuestionUserAuthorship SET Hash=NEW.Hash WHERE Hash=OLD.Hash; END");
                    InitializeProseIndex(db);
                    _initialized = true;
                }
                return action(db);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
