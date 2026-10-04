using SQLite;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

public sealed record BankQueryResult(string[] Columns, IReadOnlyList<string[]> Rows, bool Truncated)
{
    public string? ErrorCode { get; init; }
    public int? SqliteErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsSuccess => ErrorCode is null;
    public bool IsWrite { get; init; }
    public int AffectedRows { get; init; }

    public static BankQueryResult Failure(string code, int? sqliteCode = null, string? message = null)
        => new([], [], false) { ErrorCode = code, SqliteErrorCode = sqliteCode, ErrorMessage = message };
}
public sealed record BankImportIssue(int RowNumber, string ErrorCode);
public sealed record BankImportReport(int Inserted, int Duplicates, int Rejected, IReadOnlyList<BankImportIssue> Issues);
public sealed record BankExportReport(int Exported, int Skipped);

public sealed partial class QuestionBankStore
{
    public const string DefaultInquiry = "SELECT Operation, Stars, Language, DraftJson FROM BasicQuestionBank ORDER BY CreatedUtc DESC LIMIT 50;";

    public async Task<BankQueryResult> QueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Invalid user SQL is an expected result, rather than a faulted task/first-chance exception.
        if (string.IsNullOrWhiteSpace(sql) || sql.Length > 10000 || sql.Contains('\0')
            || !Regex.IsMatch(sql[SkipSqlTrivia(sql)..], @"^(SELECT|WITH|INSERT|UPDATE|DELETE)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return BankQueryResult.Failure("SupportedStatementRequired");
        string path = await WithDatabaseAsync(db => db.DatabasePath, cancellationToken).ConfigureAwait(false);
        return await Task.Run(() =>
        {
            // A separate connection keeps long queries out of the store's serialized operations.
            using var reader = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex);
            reader.BusyTimeout = TimeSpan.FromSeconds(2);
            // Bound SQLite allocations before materializing user-selected strings or blobs.
            SQLitePCL.raw.sqlite3_limit(reader.Handle, SQLitePCL.raw.SQLITE_LIMIT_LENGTH, 256 * 1024);
            var timer = Stopwatch.StartNew();
            SQLitePCL.raw.sqlite3_progress_handler(reader.Handle, 1000,
                _ => cancellationToken.IsCancellationRequested || timer.Elapsed > TimeSpan.FromSeconds(3) ? 1 : 0, null);
            int prepared = SQLitePCL.raw.sqlite3_prepare_v2(reader.Handle, sql, out var statement, out string tail);
            using (statement)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timer.Elapsed > TimeSpan.FromSeconds(3)) return BankQueryResult.Failure("QueryTimedOut");
                if (prepared != SQLitePCL.raw.SQLITE_OK)
                    return BankQueryResult.Failure("SqliteQueryError", prepared,
                        SQLitePCL.raw.sqlite3_errmsg(reader.Handle).utf8_to_string());
                if (SkipSqlTrivia(tail) != tail.Length)
                    return BankQueryResult.Failure("SupportedStatementRequired");
                bool isWrite = SQLitePCL.raw.sqlite3_stmt_readonly(statement) == 0;
                int count = SQLite3.ColumnCount(statement);
                if (count > 32 || !isWrite && count < 1) return BankQueryResult.Failure("QueryColumnLimit");
                string[] columns = Enumerable.Range(0, count).Select(i => SQLite3.ColumnName(statement, i)).ToArray();
                var rows = new List<string[]>();
                int characters = columns.Sum(column => column.Length);
                bool transaction = false, truncated = false;
                try
                {
                    if (isWrite)
                    {
                        int started = ExecuteInquiryControl(reader, "BEGIN");
                        transaction = started == SQLitePCL.raw.SQLITE_OK;
                        cancellationToken.ThrowIfCancellationRequested();
                        if (timer.Elapsed > TimeSpan.FromSeconds(3)) return BankQueryResult.Failure("QueryTimedOut");
                        if (started != SQLitePCL.raw.SQLITE_OK)
                            return BankQueryResult.Failure("SqliteQueryError", started,
                                SQLitePCL.raw.sqlite3_errmsg(reader.Handle).utf8_to_string());
                    }
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var result = SQLite3.Step(statement);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (timer.Elapsed > TimeSpan.FromSeconds(3)) return BankQueryResult.Failure("QueryTimedOut");
                        if (result == SQLite3.Result.Done)
                        {
                            int affected = isWrite ? SQLitePCL.raw.sqlite3_changes(reader.Handle) : 0;
                            if (isWrite)
                            {
                                int committed = ExecuteInquiryControl(reader, "COMMIT");
                                if (committed != SQLitePCL.raw.SQLITE_OK)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    if (timer.Elapsed > TimeSpan.FromSeconds(3)) return BankQueryResult.Failure("QueryTimedOut");
                                    return BankQueryResult.Failure("SqliteQueryError", committed,
                                        SQLitePCL.raw.sqlite3_errmsg(reader.Handle).utf8_to_string());
                                }
                                transaction = false;
                            }
                            return new BankQueryResult(columns, rows, truncated) { IsWrite = isWrite, AffectedRows = affected };
                        }
                        if (result != SQLite3.Result.Row)
                            return BankQueryResult.Failure("SqliteQueryError", (int)result,
                                SQLitePCL.raw.sqlite3_errmsg(reader.Handle).utf8_to_string());
                        // RETURNING must run to completion even after display limits are reached.
                        if (truncated) continue;
                        if (rows.Count == 100)
                        {
                            if (!isWrite) return new BankQueryResult(columns, rows, true);
                            truncated = true;
                            continue;
                        }
                        var values = Enumerable.Range(0, count).Select(i =>
                        {
                            string text = SQLite3.ColumnString(statement, i) ?? "NULL";
                            return text.Length > 4000 ? text[..4000] + "…" : text;
                        }).ToArray();
                        characters += values.Sum(value => value.Length);
                        if (characters > 256 * 1024)
                        {
                            if (!isWrite) return new BankQueryResult(columns, rows, true);
                            truncated = true;
                        }
                        else rows.Add(values);
                    }
                }
                finally
                {
                    if (transaction)
                    {
                        // Cancellation/time limits must not interrupt cleanup.
                        SQLitePCL.raw.sqlite3_progress_handler(reader.Handle, 0, null, null);
                        ExecuteInquiryControl(reader, "ROLLBACK");
                    }
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static int ExecuteInquiryControl(SQLiteConnection connection, string sql)
    {
        int result = SQLitePCL.raw.sqlite3_prepare_v2(connection.Handle, sql, out var statement);
        using (statement)
        {
            if (result != SQLitePCL.raw.SQLITE_OK) return result;
            result = SQLitePCL.raw.sqlite3_step(statement);
            return result == SQLitePCL.raw.SQLITE_DONE ? SQLitePCL.raw.SQLITE_OK : result;
        }
    }

    private static int SkipSqlTrivia(string sql)
    {
        int index = 0;
        while (index < sql.Length)
        {
            if (char.IsWhiteSpace(sql[index]) || sql[index] == '\uFEFF') { index++; continue; }
            if (index + 1 < sql.Length && sql[index] == '-' && sql[index + 1] == '-')
            {
                index += 2;
                while (index < sql.Length && sql[index] is not ('\r' or '\n')) index++;
                continue;
            }
            if (index + 1 < sql.Length && sql[index] == '/' && sql[index + 1] == '*')
            {
                int end = sql.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? sql.Length : end + 2;
                continue;
            }
            break;
        }
        return index;
    }

    public async Task<BankImportReport> ImportExcelAsync(Stream input, CancellationToken cancellationToken = default)
    {
        var rows = await Task.Run(() => QuestionBankWorkbook.Read(input, cancellationToken), cancellationToken).ConfigureAwait(false);
        return await WithDatabaseAsync(db =>
        {
            int inserted = 0, duplicates = 0, rejected = 0;
            var issues = new List<BankImportIssue>();
            db.RunInTransaction(() =>
            {
                foreach (var row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (row.Question is null)
                    {
                        rejected++;
                        if (issues.Count < 50) issues.Add(new(row.RowNumber, row.ErrorCode!));
                    }
                    else if (Insert(db, row.Question)) inserted++;
                    else duplicates++;
                }
                cancellationToken.ThrowIfCancellationRequested();
            });
            return new BankImportReport(inserted, duplicates, rejected, issues);
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<BankExportReport> ExportExcelAsync(Stream output, CancellationToken cancellationToken = default)
        => WithDatabaseAsync(db =>
        {
            int skipped = 0;
            IEnumerable<ValidatedBankQuestion> Questions()
            {
                foreach (var row in db.DeferredQuery<Row>("SELECT * FROM BasicQuestionBank ORDER BY Hash"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidatedBankQuestion? question = null;
                    try
                    {
                        var contract = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson, JsonOptions);
                        if (contract is not null && row.Version == contract.Version && row.Operation == (int)contract.Operation
                            && row.Stars == (int)contract.Tier && row.Language == (int)contract.Language
                            && (contract.Version != AdditionQuestionCatalogue.Version || (int)contract.Structure == row.Structure
                                && contract.TopicId == row.TopicId && contract.SceneId == row.SceneId))
                        {
                            var checkedDraft = BasicQuestionValidator.Validate(row.DraftJson, contract);
                            if (checkedDraft.IsValid && (checkedDraft.Contract is null || checkedDraft.Contract == contract))
                                question = new(contract, checkedDraft.Draft!, row.RawJson, row.ModelName, row.CreatedUtc);
                        }
                    }
                    catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException) { }
                    if (question is not null) yield return question;
                    else skipped++;
                }
            }
            int count = QuestionBankWorkbook.Write(output, Questions(), cancellationToken);
            return new BankExportReport(count, skipped);
        }, cancellationToken);
}
