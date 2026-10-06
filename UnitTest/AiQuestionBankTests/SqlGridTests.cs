using MathSolver.Services.QuestionBank;
using MathSolver.Models;
using SQLite;
using System.Text.Json;

internal static class SqlGridTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory, ValidatedBankQuestion sample)
    {
        await CheckRowEditsAsync(directory, sample);
        string path = Path.Combine(directory, "sql-grid.db3");
        var store = new QuestionBankStore(path);
        Check(await store.InsertAsync(sample), "Grid seed failed.");
        var query = await store.QueryAsync(QuestionBankStore.DefaultInquiry);
        Check(query.EditableColumns?.SequenceEqual(["Hash", "ProblemType", "ProblemVariant", "Operation", "Stars", "Language", "DraftJson"]) == true,
            "Default SELECT does not provide editable identity.");
        string hash = query.Rows.Single()[0];
        foreach (string sql in new[]
        {
            "SELECT * FROM BasicQuestionBank", "SELECT Hash, ModelName FROM BasicQuestionBank WHERE Stars=1 ORDER BY CreatedUtc DESC LIMIT 50",
            "-- SELECT fake\nSELECT \"Hash\" AS key, [ModelName] AS value FROM [BasicQuestionBank]; /* trailing */",
            "SELECT BasicQuestionBank.Hash, ModelName AS Hash FROM main.BasicQuestionBank WHERE ModelName != 'JOIN UNION GROUP BY'"
        })
            Check((await store.QueryAsync(sql)).EditableColumns is not null, "Direct SELECT was not editable: " + sql);
        foreach (string sql in new[]
        {
            "SELECT ModelName FROM BasicQuestionBank", "SELECT count(*) AS Hash FROM BasicQuestionBank",
            "SELECT 'forged-key' AS Hash, ModelName FROM BasicQuestionBank",
            "SELECT Hash || '' AS Hash, ModelName FROM BasicQuestionBank",
            "SELECT Hash, ModelName FROM BasicQuestionBank UNION ALL SELECT Hash, ModelName FROM BasicQuestionBank",
            "SELECT b.Hash, b.ModelName FROM BasicQuestionBank b JOIN BasicQuestionBank c ON b.Hash=c.Hash",
            "SELECT Hash, ModelName FROM BasicQuestionBank GROUP BY Hash",
            "SELECT DISTINCT Hash, ModelName FROM BasicQuestionBank",
            "WITH b AS (SELECT * FROM BasicQuestionBank) SELECT Hash, ModelName FROM b",
            "SELECT Hash, Hash, ModelName FROM BasicQuestionBank"
        })
        {
            var read = await store.QueryAsync(sql);
            Check(read.IsSuccess && read.EditableColumns is null, "Ambiguous/expression SELECT is editable: " + sql);
        }
        Check((await store.QueryAsync("SELECT * FROM BasicQuestionBank WHERE 0")).EditableColumns is not null,
            "Empty direct results must permit insertion.");
        Check((await store.QueryAsync("UPDATE BasicQuestionBank SET UseCount=0 RETURNING Hash")).EditableColumns is null,
            "RETURNING must not expose stale editable identities.");

        var original = (await store.GetGridRowAsync(hash))!;
        const string literal = "NULL; ' quoted \" SQL -- /* */\nTiếng Việt";
        Check((await store.UpdateGridCellAsync(original, "ModelName", literal, false)).IsSuccess, "Quoted multiline cell edit failed.");
        var current = (await store.GetGridRowAsync(hash))!;
        Check(Equals(current.Values["ModelName"], literal), "Cell edit did not preserve literal whitespace or quotes.");
        Check((await store.UpdateGridCellAsync(original, "DraftJson", "{}", false)).ErrorCode == "GridRowChanged",
            "Stale cell update overwrote concurrent changes.");
        Check((await store.DeleteGridRowAsync(original)).ErrorCode == "GridRowChanged", "Stale row deletion succeeded.");
        Check((await store.UpdateGridCellAsync(current, "Hash", "different", false)).ErrorCode == "GridColumnReadOnly",
            "Existing primary key can be changed through the grid.");
        Check((await store.UpdateGridCellAsync(current, "ModelName\"=NULL; DELETE", "", false)).ErrorCode == "GridColumnReadOnly",
            "An injected column identifier was accepted.");
        foreach (string value in new[] { "1.5", "2,000", "9223372036854775808", "1;DELETE FROM BasicQuestionBank" })
            Check((await store.UpdateGridCellAsync(current, "UseCount", value, false)).ErrorCode == "GridIntegerRequired",
                "Invalid integer was accepted: " + value);
        Check((await store.UpdateGridCellAsync(current, "ModelName", "NULL", false)).IsSuccess, "Literal NULL text edit failed.");
        current = (await store.GetGridRowAsync(hash))!;
        Check(Equals(current.Values["ModelName"], "NULL"), "NULL text became SQL NULL.");
        Check((await store.UpdateGridCellAsync(current, "ModelName", "ignored", true)).IsSuccess, "SQL NULL edit failed.");
        current = (await store.GetGridRowAsync(hash))!;
        Check(current.Values["ModelName"] is null, "SQL NULL became text.");
        Check((await store.UpdateGridCellAsync(current, "ModelName", "", false)).IsSuccess, "Empty text edit failed.");
        current = (await store.GetGridRowAsync(hash))!;
        Check(Equals(current.Values["ModelName"], ""), "Empty text became SQL NULL.");
        Check((await store.UpdateGridCellAsync(current, "UseCount", long.MaxValue.ToString(), false)).IsSuccess,
            "Int64 value did not round-trip.");
        current = (await store.GetGridRowAsync(hash))!;
        Check(Equals(current.Values["UseCount"], long.MaxValue), "Large integer lost precision.");

        string fullJson = JsonSerializer.Serialize(new { data = new string('x', 6000) });
        Check((await store.UpdateGridCellAsync(current, "RawJson", fullJson, false)).IsSuccess, "Long JSON update failed.");
        var preview = await store.QueryAsync("SELECT Hash, RawJson FROM BasicQuestionBank");
        current = (await store.GetGridRowAsync(hash))!;
        Check(preview.Rows[0][1].Length == 4001 && Equals(current.Values["RawJson"], fullJson),
            "The editor reuses a truncated preview instead of the full database value.");
        using (var db = new SQLiteConnection(path)) db.Execute("UPDATE BasicQuestionBank SET UseCount=0 WHERE Hash=?", hash);
        Check((await store.DeleteGridRowAsync(current)).ErrorCode == "GridRowChanged", "Concurrent practice changes were ignored.");
        current = (await store.GetGridRowAsync(hash))!;
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            try { await store.DeleteGridRowAsync(current, cancellation.Token); throw new InvalidOperationException("Cancelled deletion succeeded."); }
            catch (OperationCanceledException) { }
        }
        Check(await store.GetGridRowAsync(hash) is not null, "Cancelled deletion removed data.");

        var copied = current.Values.ToDictionary(field => field.Key, field => Convert.ToString(field.Value, System.Globalization.CultureInfo.InvariantCulture));
        copied["Hash"] = "";
        copied["ModelName"] = literal;
        Check((await store.InsertGridRowAsync(copied)).IsSuccess, "Grid insertion failed.");
        var added = await store.QueryAsync("SELECT Hash, ModelName FROM BasicQuestionBank");
        Check(added.Rows.Count == 2 && added.Rows.Any(row => row[0] != hash && row[1] == literal),
            "Insertion did not generate a distinct key or preserve raw text.");
        copied["Hash"] = hash;
        Check((await store.InsertGridRowAsync(copied)).ErrorCode == "GridDuplicateHash", "Duplicate key overwrote a record.");
        Check((await store.InsertGridRowAsync(new Dictionary<string, string?> { ["Stars"] = "1.5" })).ErrorCode == "GridIntegerRequired",
            "Bad insert committed a partial row.");
        Check((await store.DeleteGridRowAsync(current)).IsSuccess && await store.GetGridRowAsync(hash) is null,
            "Deletion did not target the selected row.");
        Check((await store.DeleteGridRowAsync(current)).ErrorCode == "GridRowChanged", "Repeated deletion pretended to succeed.");
        Check((await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank")).Rows[0][0] == "1", "Deletion removed another row.");
        string addedHash = added.Rows.Single(row => row[0] != hash)[0];
        var addedRow = (await store.GetGridRowAsync(addedHash))!;
        Check((await store.UpdateGridCellAsync(addedRow, "DraftJson", "{}", false)).IsSuccess, "JSON grid edit failed.");
        Check(await store.TakeAsync(sample.Contract.Operation, sample.Contract.Tier, sample.Contract.Language) is null,
            "Invalid grid JSON reached practice without C# validation.");

        // A failing trigger must roll back a cell write.
        addedRow = (await store.GetGridRowAsync(addedHash))!;
        using (var db = new SQLiteConnection(path))
            db.Execute("CREATE TRIGGER reject_grid BEFORE UPDATE ON BasicQuestionBank BEGIN SELECT RAISE(ABORT, 'grid test'); END");
        try { await store.UpdateGridCellAsync(addedRow, "ModelName", "must not persist", false); throw new InvalidOperationException("Failing trigger did not fail."); }
        catch (SQLiteException) { }
        Check(Equals((await store.GetGridRowAsync(addedHash))!.Values["ModelName"], literal), "Failed update persisted.");
        string reorderedPath = Path.Combine(directory, "grid-column-order.db3");
        using (var db = new SQLiteConnection(reorderedPath))
            db.Execute("CREATE TABLE BasicQuestionBank (" + string.Join(',', QuestionBankStore.GridColumns.Reverse().Select(column =>
                column.Name + (column.IsInteger ? " INTEGER" : " TEXT") + (column.IsKey ? " PRIMARY KEY" : ""))) + ")");
        var reordered = await new QuestionBankStore(reorderedPath).QueryAsync("SELECT * FROM BasicQuestionBank");
        Check(reordered.IsSuccess && reordered.EditableColumns?.SequenceEqual(reordered.Columns) == true,
            "Historical column order maps a displayed cell to a different database column.");
        await ProblemColumnMigrationAsync(directory, sample);
        Console.WriteLine("PASS editable SELECT identity, aliases, empty results, safe cell/row CRUD, NULL/JSON/Int64, conflicts, cancellation and C# practice validation");
    }

    private static async Task ProblemColumnMigrationAsync(string directory, ValidatedBankQuestion sample)
    {
        string path = Path.Combine(directory, "problem-column-migration.db3");
        var initial = new QuestionBankStore(path);
        var contract = FindXQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects),
            MathSolver.Models.ArithmeticOperation.Subtract, MathSolver.Models.CurriculumTier.ThreeStars,
            MathSolver.Services.AppLanguage.Vietnamese, new Random(43), FindXUnknownRole.Minuend);
        var draft = FindXQuestionCatalogue.Draft(contract);
        var findX = new ValidatedBankQuestion(contract, draft, QuestionBankStore.SerializeDraft(draft), "migration-test", DateTime.UtcNow);
        Check(await initial.InsertAsync(sample) && await initial.InsertAsync(findX), "Migration fixture insert failed.");
        const string snapshotQuery = "SELECT Hash,ContractJson,DraftJson,Grade,KnowledgeGroup,ProblemType,ProblemVariant FROM BasicQuestionBank ORDER BY Hash";
        var before = await initial.QueryAsync(snapshotQuery);
        // Simulate the schema in installed versions, including the existing selection index.
        using (var db = new SQLiteConnection(path))
        {
            db.Execute("ALTER TABLE BasicQuestionBank RENAME COLUMN ProblemType TO Family");
            db.Execute("ALTER TABLE BasicQuestionBank RENAME COLUMN ProblemVariant TO UnknownRole");
        }
        var migrated = new QuestionBankStore(path);
        var after = await migrated.QueryAsync(snapshotQuery);
        Check(after.IsSuccess && before.Rows.SelectMany(row => row).SequenceEqual(after.Rows.SelectMany(row => row)),
            "Column migration changed keys, payloads or classification values.");
        var all = await migrated.QueryAsync("SELECT * FROM BasicQuestionBank");
        Check(all.EditableColumns?.SequenceEqual(all.Columns) == true
            && all.Columns.Contains("ProblemType") && all.Columns.Contains("ProblemVariant")
            && !all.Columns.Contains("Family") && !all.Columns.Contains("UnknownRole"),
            "New column names were not editable or old technical columns remained.");
        var selected = await migrated.TakeFindXAsync(contract.Operation, contract.Tier, contract.Language, new(contract.KnowledgeGroup));
        Check(selected?.Contract == contract && !await migrated.InsertAsync(findX),
            "Migration broke Find-X selection or existing prose deduplication.");
        string hash = after.Rows.Single(row => row[1] == JsonSerializer.Serialize(contract,
            new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))[0];
        var row = (await migrated.GetGridRowAsync(hash))!;
        Check((await migrated.UpdateGridCellAsync(row, "ProblemVariant", "99", false)).IsSuccess
            && await migrated.TakeFindXAsync(contract.Operation, contract.Tier, contract.Language, new(contract.KnowledgeGroup)) is null,
            "Renamed variant column could not be edited or forged data reached practice.");
        row = (await migrated.GetGridRowAsync(hash))!;
        Check((await migrated.UpdateGridCellAsync(row, "ProblemVariant", ((int)contract.UnknownRole).ToString(), false)).IsSuccess,
            "Could not restore the migrated variant.");
        var reopened = new QuestionBankStore(path);
        Check((await reopened.TakeFindXAsync(contract.Operation, contract.Tier, contract.Language, new(contract.KnowledgeGroup)))?.Contract == contract,
            "Repeat initialization damaged the migrated bank.");
        using (var db = new SQLiteConnection(path))
        {
            string index = db.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE name='FindXSelection'");
            Check(index.Contains("ProblemType") && index.Contains("ProblemVariant"), "Migration lost the selection index.");
        }
    }

    private static async Task CheckRowEditsAsync(string directory, ValidatedBankQuestion sample)
    {
        string path = Path.Combine(directory, "sql-row-edit.db3");
        var store = new QuestionBankStore(path);
        Check(await store.InsertAsync(sample), "Row edit seed failed.");
        string hash = (await store.QueryAsync("SELECT Hash FROM BasicQuestionBank")).Rows.Single()[0];
        var original = (await store.GetGridRowAsync(hash))!;
        string literal = "' ; DELETE FROM BasicQuestionBank --\nTiếng Việt\tNULL";
        string json = JsonSerializer.Serialize(new { text = new string('x', 8000) });
        var edits = new Dictionary<string, string?> {
            ["ModelName"] = literal, ["RawJson"] = json,
            ["UseCount"] = long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), ["TopicId"] = null };
        Check((await store.UpdateGridRowAsync(original, edits)).IsSuccess, "Whole-row save failed.");
        var current = (await store.GetGridRowAsync(hash))!;
        Check(Equals(current.Values["ModelName"], literal) && Equals(current.Values["RawJson"], json)
            && Equals(current.Values["UseCount"], long.MaxValue) && current.Values["TopicId"] is null,
            "Whole-row save lost literal text, long JSON, NULL or integer precision.");
        Check(Equals(current.Values["ContractJson"], original.Values["ContractJson"]), "Unprojected column was overwritten.");
        Check((await store.UpdateGridRowAsync(original, new Dictionary<string, string?> { ["ModelName"] = "stale" })).ErrorCode == "GridRowChanged",
            "A stale row edit overwrote concurrent changes.");
        foreach (var invalid in new Dictionary<string, string?>[] {
            new() { ["ModelName"] = "must not persist", ["Stars"] = "1.5" },
            new() { ["ModelName"] = "must not persist", ["Hash"] = "changed-key" },
            new() { ["ModelName"] = "must not persist", ["ModelName\"=NULL; DELETE"] = "bad" } })
        {
            Check(!(await store.UpdateGridRowAsync(current, invalid)).IsSuccess, "Invalid row edit was accepted.");
            Check(Equals((await store.GetGridRowAsync(hash))!.Values["ModelName"], literal), "A rejected edit partially saved other cells.");
        }
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            try { await store.UpdateGridRowAsync(current, edits, cancellation.Token); throw new InvalidOperationException("Cancelled row edit succeeded."); }
            catch (OperationCanceledException) { }
        }
        using (var db = new SQLiteConnection(path))
            db.Execute("CREATE TRIGGER reject_row BEFORE UPDATE ON BasicQuestionBank BEGIN SELECT RAISE(ABORT,'row test'); END");
        try
        {
            await store.UpdateGridRowAsync(current, new Dictionary<string, string?> { ["ModelName"] = "failed", ["RawJson"] = "{}" });
            throw new InvalidOperationException("Failing row trigger did not fail.");
        }
        catch (SQLiteException) { }
        var afterFailure = (await store.GetGridRowAsync(hash))!;
        Check(Equals(afterFailure.Values["ModelName"], literal) && Equals(afterFailure.Values["RawJson"], json),
            "SQLite failure persisted part of a row edit.");
        Console.WriteLine("PASS atomic row edits, full JSON/NULL/Int64, unchanged hidden columns, stale rows and failed-save rollback");
    }
}
