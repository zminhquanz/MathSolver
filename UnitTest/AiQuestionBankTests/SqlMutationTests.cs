using MathSolver.Services.QuestionBank;

internal static class SqlMutationTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory, ValidatedBankQuestion sample)
    {
        string path = Path.Combine(directory, "sql-mutations.db3");
        var store = new QuestionBankStore(path);
        await store.InsertAsync(sample);
        var read = await store.QueryAsync("-- comment before SELECT\r\n/* block comment */ SELECT ModelName FROM BasicQuestionBank; -- trailing comment");
        Check(read.IsSuccess && !read.IsWrite && read.AffectedRows == 0 && read.Rows.Count == 1,
            "SQL comments or SELECT reporting failed.");
        var update = await store.QueryAsync("--SELECT * FROM BasicQuestionBank;\nUPDATE BasicQuestionBank SET ModelName='edited; -- literal /* text */'; /* trailing */");
        Check(update.IsSuccess && update.IsWrite && update.AffectedRows == 1 && update.Columns.Length == 0
            && (await store.QueryAsync("SELECT ModelName FROM BasicQuestionBank")).Rows[0][0] == "edited; -- literal /* text */",
            "Commented SQL, literal semicolons or persisted UPDATE failed.");
        var insert = await store.QueryAsync("INSERT INTO BasicQuestionBank (Hash,Operation,Stars,Language,Version,ContractJson,DraftJson,RawJson,ModelName,CreatedUtc,LastUsedUtc,UseCount) SELECT Hash || '-copy',Operation,Stars,Language,Version,ContractJson,DraftJson,RawJson,ModelName,CreatedUtc,LastUsedUtc,UseCount FROM BasicQuestionBank");
        Check(insert.IsSuccess && insert.IsWrite && insert.AffectedRows == 1
            && (await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank")).Rows[0][0] == "2", "INSERT failed.");
        var duplicate = await store.QueryAsync("INSERT OR IGNORE INTO BasicQuestionBank SELECT * FROM BasicQuestionBank");
        Check(duplicate.IsSuccess && duplicate.IsWrite && duplicate.AffectedRows == 0, "INSERT OR IGNORE affected-row count failed.");
        // OR FAIL preserves earlier row changes unless our outer transaction rolls back.
        var failed = await store.QueryAsync("UPDATE OR FAIL BasicQuestionBank SET Hash='same-key'");
        Check(!failed.IsSuccess && failed.SqliteErrorCode == SQLitePCL.raw.SQLITE_CONSTRAINT
            && failed.ErrorMessage?.Contains("UNIQUE constraint failed") == true
            && (await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank WHERE Hash='same-key'")).Rows[0][0] == "0",
            "Constraint error details or rollback failed.");
        var cte = await store.QueryAsync("WITH n AS (SELECT Hash FROM BasicQuestionBank LIMIT 1) UPDATE BasicQuestionBank SET UseCount=UseCount+1 WHERE Hash IN (SELECT Hash FROM n) RETURNING UseCount");
        Check(cte.IsSuccess && cte.IsWrite && cte.AffectedRows == 1 && cte.Rows.Count == 1 && cte.Rows[0][0] == "1",
            "WITH UPDATE RETURNING failed.");
        var multiple = await store.QueryAsync("UPDATE BasicQuestionBank SET ModelName='must-not-commit'; -- second statement\nDELETE FROM BasicQuestionBank;");
        Check(!multiple.IsSuccess && multiple.ErrorCode == "SupportedStatementRequired"
            && (await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank WHERE ModelName='must-not-commit'")).Rows[0][0] == "0",
            "Multiple-statement rejection committed the first write.");
        var delete = await store.QueryAsync("WITH n AS (SELECT Hash FROM BasicQuestionBank WHERE Hash LIKE '%-copy') DELETE FROM BasicQuestionBank WHERE Hash IN (SELECT Hash FROM n)");
        Check(delete.IsSuccess && delete.AffectedRows == 1, "WITH DELETE failed.");

        // Generate a larger RETURNING result; display caps must not shorten the write.
        var batch = await store.QueryAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<120) INSERT INTO BasicQuestionBank (Hash,Operation,Stars,Language,Version,ContractJson,DraftJson,RawJson,ModelName,CreatedUtc,LastUsedUtc,UseCount) SELECT b.Hash || '-' || n.x,b.Operation,b.Stars,b.Language,b.Version,b.ContractJson,b.DraftJson,b.RawJson,b.ModelName,b.CreatedUtc,b.LastUsedUtc,b.UseCount FROM BasicQuestionBank b CROSS JOIN n");
        Check(batch.IsSuccess && batch.AffectedRows == 120, "WITH INSERT failed.");
        var returning = await store.QueryAsync("UPDATE BasicQuestionBank SET UseCount=2 RETURNING Hash");
        Check(returning.IsSuccess && returning.IsWrite && returning.AffectedRows == 121 && returning.Rows.Count == 100
            && returning.Truncated && (await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank WHERE UseCount=2")).Rows[0][0] == "121",
            "RETURNING display limits left a write incomplete.");

        var timedOut = await store.QueryAsync("UPDATE BasicQuestionBank SET UseCount=(WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n) SELECT sum(x) FROM n)");
        Check(!timedOut.IsSuccess && timedOut.ErrorCode == "QueryTimedOut"
            && (await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank WHERE UseCount=2")).Rows[0][0] == "121",
            "Timed-out UPDATE left changes in the bank.");
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(40)))
        {
            bool cancelled = false;
            try { await store.QueryAsync("UPDATE BasicQuestionBank SET UseCount=(WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n) SELECT sum(x) FROM n)", cancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "UPDATE did not cancel.");
        }
        Check((await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank WHERE UseCount=2")).Rows[0][0] == "121",
            "Cancelled UPDATE left changes in the bank.");
        var removed = await store.QueryAsync("-- SELECT * FROM BasicQuestionBank;\nDELETE FROM BasicQuestionBank");
        Check(removed.IsSuccess && removed.IsWrite && removed.AffectedRows == 121
            && (await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank")).Rows[0][0] == "0", "DELETE without WHERE failed.");
        Check(await store.InsertAsync(sample), "SQL writes broke subsequent normal insertions.");

        // Direct SQL edits retain C# validation when a question is read for practice/export.
        Check((await store.QueryAsync("UPDATE BasicQuestionBank SET DraftJson='{}'")).IsSuccess, "Direct JSON edit failed.");
        Check(await store.TakeAsync(sample.Contract.Operation, sample.Contract.Tier, sample.Contract.Language) is null,
            "Invalid directly edited prose entered practice.");
        using var output = new MemoryStream();
        var exported = await store.ExportExcelAsync(output);
        Check(exported.Exported == 0 && exported.Skipped == 1, "Invalid directly edited row entered Excel export.");
        Console.WriteLine("PASS SQL INSERT/UPDATE/DELETE, comments, affected rows, WITH/RETURNING, atomic rollback and validated practice fallback");
    }
}
