using SQLite;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

public sealed record BankGridColumn(string Name, bool IsInteger, string DefaultValue, bool IsKey = false);
public sealed record BankGridRow(string Hash, IReadOnlyDictionary<string, object?> Values);
public sealed record BankGridMutationResult(bool IsSuccess, string? ErrorCode = null);

public sealed partial class QuestionBankStore
{
    // This is a database editor, just like direct SQL. Practice/export still validate the C# payload.
    public static IReadOnlyList<BankGridColumn> GridColumns { get; } = Array.AsReadOnly(new BankGridColumn[]
    {
        new("Hash", false, "", true), new("Operation", true, "0"), new("Stars", true, "1"),
        new("Language", true, "0"), new("Version", true, "1"), new("ContractJson", false, ""),
        new("DraftJson", false, ""), new("RawJson", false, ""), new("ModelName", false, ""),
        new("CreatedUtc", true, "0"), new("LastUsedUtc", true, "0"), new("UseCount", true, "0"),
        new("Structure", true, "0"), new("TopicId", false, ""), new("SceneId", false, "")
    });

    // Only direct single-table projections with a genuine Hash are editable. Never infer
    // record identity from a column alias in a JOIN, expression, aggregate or UNION result.
    internal static string[]? GridProjection(string sql, string[] columns)
    {
        string clean = Regex.Replace(sql,
            @"'(''|[^'])*'|""(""""|[^""])*""|\[[^\]]*\]|`[^`]*`|--[^\r\n]*|/\*[\s\S]*?\*/",
            match => match.Value.StartsWith("--", StringComparison.Ordinal) || match.Value.StartsWith("/*", StringComparison.Ordinal)
                ? " " : match.Value);
        var select = Regex.Match(clean,
            @"\A\s*SELECT\s+(?<projection>.*?)\s+FROM\s+(?:main\.)?(?:BasicQuestionBank|""BasicQuestionBank""|\[BasicQuestionBank\]|`BasicQuestionBank`)(?=\s|;|$)(?<tail>.*?)\s*;?\s*\z",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!select.Success) return null;
        string tail = select.Groups["tail"].Value.Trim().TrimEnd(';').Trim();
        string keywords = Regex.Replace(tail, @"'(''|[^'])*'|""(""""|[^""])*""|\[[^\]]*\]|`[^`]*`", " ");
        if (tail.Length > 0 && !Regex.IsMatch(tail, @"\A(WHERE\b|ORDER\s+BY\b|LIMIT\b)", RegexOptions.IgnoreCase)
            || Regex.IsMatch(keywords, @"\b(JOIN|UNION|INTERSECT|EXCEPT|GROUP|HAVING)\b", RegexOptions.IgnoreCase)) return null;
        string projection = select.Groups["projection"].Value.Trim();
        string[] sources;
        if (projection is "*" or "BasicQuestionBank.*")
        {
            // Historical databases can have a different column order after migrations.
            sources = columns.Select(name => GridColumns.FirstOrDefault(column =>
                column.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Name ?? "").ToArray();
            if (sources.Any(string.IsNullOrEmpty)) return null;
        }
        else
        {
            var fields = projection.Split(',');
            sources = new string[fields.Length];
            for (int index = 0; index < fields.Length; index++)
            {
                var field = Regex.Match(fields[index].Trim(),
                    @"\A(?:BasicQuestionBank\.)?(?<name>\w+|""\w+""|\[\w+\]|`\w+`)(?:\s+(?:AS\s+)?(?:\w+|""\w+""|\[\w+\]|`\w+`))?\z", RegexOptions.IgnoreCase);
                if (!field.Success) return null;
                string name = field.Groups["name"].Value.Trim('"', '[', ']', '`');
                var column = GridColumns.FirstOrDefault(column => column.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (column is null) return null;
                sources[index] = column.Name;
            }
        }
        return sources.Length == columns.Length && sources.Count(name => name == "Hash") == 1 ? sources : null;
    }

    public Task<BankGridRow?> GetGridRowAsync(string hash, CancellationToken cancellationToken = default) =>
        WithDatabaseAsync(db => ReadGridRow(db, hash), cancellationToken);

    private static BankGridRow? ReadGridRow(SQLiteConnection db, string hash)
    {
        // Raw SQLite values distinguish NULL from text "NULL" and preserve long JSON in full.
        int prepared = SQLitePCL.raw.sqlite3_prepare_v2(db.Handle, "SELECT * FROM BasicQuestionBank WHERE Hash=?", out var statement);
        using (statement)
        {
            if (prepared != SQLitePCL.raw.SQLITE_OK) throw new InvalidOperationException("SqliteQueryError");
            SQLitePCL.raw.sqlite3_bind_text(statement, 1, hash);
            if (SQLite3.Step(statement) != SQLite3.Result.Row) return null;
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (int index = 0; index < SQLite3.ColumnCount(statement); index++)
                values[SQLite3.ColumnName(statement, index)] = SQLitePCL.raw.sqlite3_column_type(statement, index) switch
                {
                    SQLitePCL.raw.SQLITE_NULL => null,
                    SQLitePCL.raw.SQLITE_INTEGER => SQLitePCL.raw.sqlite3_column_int64(statement, index),
                    SQLitePCL.raw.SQLITE_FLOAT => SQLitePCL.raw.sqlite3_column_double(statement, index),
                    _ => SQLite3.ColumnString(statement, index)
                };
            return new(hash, new ReadOnlyDictionary<string, object?>(values));
        }
    }

    public Task<BankGridMutationResult> UpdateGridCellAsync(BankGridRow original, string columnName,
        string text, bool isNull, CancellationToken cancellationToken = default)
    {
        var column = GridColumns.FirstOrDefault(column => column.Name == columnName && !column.IsKey);
        if (column is null) return Task.FromResult(new BankGridMutationResult(false, "GridColumnReadOnly"));
        if (!TryGridValue(column, text, isNull, out var value))
            return Task.FromResult(new BankGridMutationResult(false, "GridIntegerRequired"));
        return MutateGridRowAsync(original, $"UPDATE BasicQuestionBank SET \"{column.Name}\"=?", [value], cancellationToken);
    }

    public Task<BankGridMutationResult> DeleteGridRowAsync(BankGridRow original, CancellationToken cancellationToken = default) =>
        MutateGridRowAsync(original, "DELETE FROM BasicQuestionBank", [], cancellationToken);

    private Task<BankGridMutationResult> MutateGridRowAsync(BankGridRow original, string command, object?[] args, CancellationToken cancellationToken) =>
        WithDatabaseAsync(db =>
        {
            if (string.IsNullOrEmpty(original.Hash) || GridColumns.Any(column => !original.Values.ContainsKey(column.Name))
                || !Equals(original.Values["Hash"], original.Hash)) return new BankGridMutationResult(false, "GridRowChanged");
            // Compare all original values in the same atomic statement. Concurrent generation,
            // practice, another editor or a direct query cannot silently overwrite this snapshot.
            string where = string.Join(" AND ", GridColumns.Select(column => $"\"{column.Name}\" IS ?"));
            object?[] parameters = [.. args, .. GridColumns.Select(column => original.Values[column.Name])];
            BankGridMutationResult result = new(false, "GridRowChanged");
            db.RunInTransaction(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                int changed = db.Execute(command + " WHERE " + where, parameters!);
                cancellationToken.ThrowIfCancellationRequested();
                result = new(changed == 1, changed == 1 ? null : "GridRowChanged");
            });
            return result;
        }, cancellationToken);

    public Task<BankGridMutationResult> InsertGridRowAsync(IReadOnlyDictionary<string, string?> inputs,
        CancellationToken cancellationToken = default) => WithDatabaseAsync(db =>
        {
            if (inputs.Keys.Any(key => !GridColumns.Any(column => column.Name == key)))
                return new BankGridMutationResult(false, "GridColumnReadOnly");
            var values = new object?[GridColumns.Count];
            for (int index = 0; index < GridColumns.Count; index++)
            {
                var column = GridColumns[index];
                string? text = inputs.TryGetValue(column.Name, out var provided) ? provided : column.DefaultValue;
                if (column.IsKey && string.IsNullOrWhiteSpace(text)) text = Guid.NewGuid().ToString("N");
                if (!TryGridValue(column, text ?? "", text is null, out values[index]))
                    return new BankGridMutationResult(false, "GridIntegerRequired");
            }
            BankGridMutationResult result = new(false);
            db.RunInTransaction(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Ignore only a duplicate key; all other database constraints still report errors.
                int changed = db.Execute("INSERT INTO BasicQuestionBank (" + string.Join(',', GridColumns.Select(column => column.Name))
                    + ") VALUES (" + string.Join(',', GridColumns.Select(_ => "?")) + ") ON CONFLICT(Hash) DO NOTHING", values!);
                cancellationToken.ThrowIfCancellationRequested();
                result = new(changed == 1, changed == 1 ? null : "GridDuplicateHash");
            });
            return result;
        }, cancellationToken);

    private static bool TryGridValue(BankGridColumn column, string text, bool isNull, out object? value)
    {
        value = null;
        if (isNull) return !column.IsKey;
        if (!column.IsInteger) { value = text; return true; }
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return false;
        value = integer;
        return true;
    }
}
