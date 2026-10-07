using SQLite;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public sealed partial class QuestionBankStore
{
    // Separate derived index keeps the existing public table, row keys and Excel/grid
    // formats intact. Duplicate historical rows remain available; nothing is deleted.
    private sealed class ProseIndexSource
    {
        public string Hash { get; set; } = "";
        public string ContractJson { get; set; } = "";
        public string DraftJson { get; set; } = "";
    }

    public Task<bool> ContainsProseAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
        => WithDatabaseAsync(db =>
        {
            RefreshProseIndex(db);
            return ProseExists(db, QuestionProseIdentity.Hash(question.Contract, question.Draft));
        }, cancellationToken);

    public Task<IReadOnlySet<string>> GetProseHashesAsync(CancellationToken cancellationToken = default)
        => WithDatabaseAsync<IReadOnlySet<string>>(db =>
        {
            RefreshProseIndex(db);
            return db.Query<ProseHashRow>("SELECT DISTINCT ProseHash FROM QuestionProseIndex WHERE ProseHash<>''")
                .Select(row => row.ProseHash).ToHashSet(StringComparer.Ordinal);
        }, cancellationToken);

    private sealed class ProseHashRow
    {
        public string ProseHash { get; set; } = "";
    }

    private static bool ProseExists(SQLiteConnection db, string proseHash) =>
        db.ExecuteScalar<int>("SELECT EXISTS(SELECT 1 FROM QuestionProseIndex WHERE ProseHash=?)", proseHash) != 0;

    private static void InitializeProseIndex(SQLiteConnection db)
    {
        db.Execute("CREATE TABLE IF NOT EXISTS QuestionProseIndex (Hash TEXT PRIMARY KEY NOT NULL, ProseHash TEXT NOT NULL)");
        db.Execute("CREATE INDEX IF NOT EXISTS QuestionProseLookup ON QuestionProseIndex(ProseHash)");
        db.Execute("CREATE TABLE IF NOT EXISTS QuestionProseIndexMetadata (Key TEXT PRIMARY KEY NOT NULL, Value INTEGER NOT NULL)");
        // Expanded reviewed clauses can make a previously invalid advanced-user
        // row valid. Revisit cached blank identities once after this policy change.
        // Public questions, their row keys and usage metadata are never rewritten.
        const int validationRevision = 3;
        if (db.ExecuteScalar<int>("SELECT EXISTS(SELECT 1 FROM QuestionProseIndexMetadata WHERE Key='ValidationRevision' AND Value=?)", validationRevision) == 0)
            db.RunInTransaction(() =>
            {
                db.Execute("DELETE FROM QuestionProseIndex");
                db.Execute("INSERT OR REPLACE INTO QuestionProseIndexMetadata(Key,Value) VALUES ('ValidationRevision',?)", validationRevision);
            });
        // Grid edits and SQL from any connection invalidate only the affected row.
        db.Execute("CREATE TRIGGER IF NOT EXISTS QuestionProseInserted AFTER INSERT ON BasicQuestionBank BEGIN DELETE FROM QuestionProseIndex WHERE Hash=NEW.Hash; END");
        db.Execute("CREATE TRIGGER IF NOT EXISTS QuestionProseUpdated AFTER UPDATE OF Hash,ContractJson,DraftJson ON BasicQuestionBank BEGIN DELETE FROM QuestionProseIndex WHERE Hash=OLD.Hash OR Hash=NEW.Hash; END");
        db.Execute("CREATE TRIGGER IF NOT EXISTS QuestionProseDeleted AFTER DELETE ON BasicQuestionBank BEGIN DELETE FROM QuestionProseIndex WHERE Hash=OLD.Hash; END");
    }

    private static void RefreshProseIndex(SQLiteConnection db)
    {
        db.RunInTransaction(() =>
        {
            // Backfill existing databases lazily, and reindex rows invalidated by edits.
            foreach (var row in db.Query<ProseIndexSource>("SELECT b.Hash,b.ContractJson,b.DraftJson FROM BasicQuestionBank b LEFT JOIN QuestionProseIndex p ON p.Hash=b.Hash WHERE p.Hash IS NULL"))
            {
                string proseHash = "";
                try
                {
                    var contract = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson, JsonOptions);
                    if (contract?.IsValid == true)
                    {
                        var validation = BasicQuestionValidator.Validate(row.DraftJson, contract);
                        if (validation.IsValid)
                            proseHash = QuestionProseIdentity.Hash(validation.Contract ?? contract, validation.Draft!);
                    }
                }
                catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
                {
                    // Invalid advanced-user rows do not block valid question generation.
                }
                db.Execute("INSERT OR REPLACE INTO QuestionProseIndex(Hash,ProseHash) VALUES (?,?)", row.Hash, proseHash);
            }
        });
    }
}
