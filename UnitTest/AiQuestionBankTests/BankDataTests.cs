using MathSolver.Models;
using MathSolver.Services.QuestionBank;
using SQLite;
using System.IO.Compression;
using System.Xml.Linq;

internal static class BankDataTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory, ValidatedBankQuestion sample, Func<BasicQuestionContract, string> prose)
    {
        string database = Path.Combine(directory, "data-tests.db3");
        var store = new QuestionBankStore(database);
        Check(await store.InsertAsync(sample), "Data test seed failed.");
        var count = await store.QueryAsync("SELECT count(*) AS Total FROM BasicQuestionBank;");
        Check(count.IsSuccess && count.Columns.SequenceEqual(["Total"]) && count.Rows[0][0] == "1", "SQL inquiry returned wrong values.");
        foreach (var (sql, expectedCode, expectedMessage) in new[]
        {
            ("SELECT ;", SQLitePCL.raw.SQLITE_ERROR, "near \";\": syntax error"),
            ("SELECT * FROM MissingQuestionTable", SQLitePCL.raw.SQLITE_ERROR, "no such table: MissingQuestionTable"),
            ("SELECT MissingQuestionColumn FROM BasicQuestionBank", SQLitePCL.raw.SQLITE_ERROR, "no such column: MissingQuestionColumn"),
            // Prepare succeeds, but stepping the statement fails. Discard partial results as well.
            ("SELECT 1 UNION ALL SELECT abs(-9223372036854775808)", SQLitePCL.raw.SQLITE_ERROR, "integer overflow")
        })
        {
            var failed = await store.QueryAsync(sql);
            Check(!failed.IsSuccess && failed.ErrorCode == "SqliteQueryError"
                && failed.SqliteErrorCode == expectedCode && failed.ErrorMessage == expectedMessage,
                "SQLite query did not return its precise error without throwing: " + sql);
            Check(failed.Columns.Length == 0 && failed.Rows.Count == 0 && !failed.Truncated,
                "Failed SQL exposed misleading partial results.");
            var recovery = await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank");
            Check(recovery.IsSuccess && recovery.Rows[0][0] == "1", "Invalid SQL broke subsequent queries.");
            Check(!await store.InsertAsync(sample), "Invalid SQL damaged insertion or deduplication.");
        }
        var cte = await store.QueryAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<101) SELECT x FROM n");
        Check(cte.Rows.Count == 100 && cte.Truncated, "Query row limit failed.");
        var limited = await store.QueryAsync("SELECT printf('%05000d',1) AS LongText");
        Check(limited.Rows[0][0].Length == 4001, "Query cell limit failed.");
        var allocationLimited = await store.QueryAsync("SELECT randomblob(1000000)");
        Check(!allocationLimited.IsSuccess && allocationLimited.SqliteErrorCode == SQLitePCL.raw.SQLITE_TOOBIG
            && !string.IsNullOrWhiteSpace(allocationLimited.ErrorMessage), "SQLite inquiry accepted an oversized native allocation.");
        var boundedOutput = await store.QueryAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<100) SELECT printf('%04000d',x) FROM n");
        Check(boundedOutput.Truncated && boundedOutput.Rows.Count < 100, "SQLite inquiry output exceeded its text budget.");
        foreach (string sql in new[] { "", "SELECT 1\0", "SELECT 1; DELETE FROM BasicQuestionBank;",
            "DROP TABLE BasicQuestionBank", "ATTACH DATABASE ':memory:' AS other" })
        {
            var blocked = await store.QueryAsync(sql);
            Check(!blocked.IsSuccess && blocked.ErrorCode == "SupportedStatementRequired", "Unsupported or multiple SQL statements were accepted.");
        }
        var columnLimited = await store.QueryAsync("SELECT " + string.Join(",", Enumerable.Repeat("1", 33)));
        Check(!columnLimited.IsSuccess && columnLimited.ErrorCode == "QueryColumnLimit", "Query column limit failed.");
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30)))
        {
            bool cancelled = false;
            try { await store.QueryAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n) SELECT sum(x) FROM n", cancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "Long SQLite inquiry did not cancel.");
        }
        var timedOut = await store.QueryAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n) SELECT sum(x) FROM n");
        Check(!timedOut.IsSuccess && timedOut.ErrorCode == "QueryTimedOut"
            && (await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank")).Rows[0][0] == "1", "Query timeout damaged the database or failed.");
        using (var cancellation = new CancellationTokenSource())
        {
            var backgroundQuery = store.QueryAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n) SELECT sum(x) FROM n", cancellation.Token);
            await Task.Delay(50);
            try { await store.InsertAsync(sample).WaitAsync(TimeSpan.FromSeconds(1)); }
            finally { cancellation.Cancel(); }
            try { await backgroundQuery; } catch (OperationCanceledException) { }
        }

        foreach (var language in Enum.GetValues<MathSolver.Services.AppLanguage>())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var contract = BasicQuestionContract.Create(operation, tier, language, new Random(420 + (int)tier));
            var draft = BasicQuestionValidator.Validate(prose(contract), contract).Draft!;
            await store.InsertAsync(new(contract, draft, prose(contract), "model-đề.xlsx", new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc)));
        }
        using var workbook = new MemoryStream();
        var exported = await store.ExportExcelAsync(workbook);
        Check(exported.Exported >= 40 && exported.Skipped == 0, "Whole-bank Excel export missed valid records.");
        using (var packageStream = new MemoryStream(workbook.ToArray()))
        using (var package = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(packageStream, false))
        {
            var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(package).ToArray();
            Check(errors.Length == 0, "Exported workbook violates SpreadsheetML: " + string.Join("; ", errors.Select(e => e.Description)));
        }
        var target = new QuestionBankStore(Path.Combine(directory, "imported.db3"));
        workbook.Position = 0;
        var imported = await target.ImportExcelAsync(workbook);
        Check(imported.Inserted == exported.Exported && imported.Rejected == 0, "Bilingual Excel data failed round-trip.");
        workbook.Position = 0;
        var duplicates = await target.ImportExcelAsync(workbook);
        Check(duplicates.Duplicates == exported.Exported && duplicates.Inserted == 0, "Excel import inserted duplicates.");
        var metadata = await target.QueryAsync("SELECT CreatedUtc FROM BasicQuestionBank WHERE ModelName='model-đề.xlsx' LIMIT 1");
        Check(metadata.Rows[0][0] == new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc).Ticks.ToString(), "UTC metadata changed across SQLite/Excel.");

        using var single = new MemoryStream();
        QuestionBankWorkbook.Write(single, [sample]);
        byte[] original = single.ToArray();
        // Excel commonly rewrites inline strings into shared strings and saves numeric cells.
        byte[] shared = Rewrite(original, (archive, sheet) =>
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var strings = new XElement(ns + "sst");
            foreach (var cell in sheet.Descendants(ns + "c"))
            {
                string value = cell.Descendants(ns + "t").Single().Value;
                cell.RemoveNodes();
                if (cell.Attribute("r")!.Value is "A2" or "C2" or "E2" or "F2")
                { cell.SetAttributeValue("t", "n"); cell.Add(new XElement(ns + "v", value + "E0")); }
                else
                {
                    cell.SetAttributeValue("t", "s"); cell.Add(new XElement(ns + "v", strings.Elements().Count()));
                    int split = Math.Min(value.Length, 1);
                    strings.Add(new XElement(ns + "si", new XElement(ns + "r", new XElement(ns + "t", value[..split])),
                        new XElement(ns + "r", new XElement(ns + "t", value[split..]))));
                }
            }
            WritePart(archive, "xl/sharedStrings.xml", strings);
            var relationships = ReadPart(archive, "xl/_rels/workbook.xml.rels");
            relationships.Add(new XElement(relationships.Name.Namespace + "Relationship", new XAttribute("Id", "strings"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"), new XAttribute("Target", "sharedStrings.xml")));
            WritePart(archive, "xl/_rels/workbook.xml.rels", relationships);
            var types = ReadPart(archive, "[Content_Types].xml");
            types.Add(new XElement(types.Name.Namespace + "Override", new XAttribute("PartName", "/xl/sharedStrings.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml")));
            WritePart(archive, "[Content_Types].xml", types);
        });
        using (var input = new MemoryStream(shared))
        {
            var rows = QuestionBankWorkbook.Read(input);
            Check(rows.Count == 1 && rows[0].Question?.Draft == sample.Draft, "Excel shared strings, rich text or numeric cells were read incorrectly.");
        }
        using (var input = new MemoryStream(shared))
        using (var package = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(input, false))
            Check(!new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(package).Any(), "Shared-string Excel fixture violates SpreadsheetML.");
        byte[] mutations = Rewrite(original, (_, sheet) =>
        {
            XNamespace ns = sheet.Name.Namespace;
            var validRow = sheet.Descendants(ns + "row").Last();
            validRow.Elements(ns + "c").Single(c => (string?)c.Attribute("r") == "P2").Element(ns + "is")!.Element(ns + "t")!.Value = "999999";
            var invalidRow = new XElement(validRow);
            invalidRow.SetAttributeValue("r", "3");
            foreach (var cell in invalidRow.Elements(ns + "c")) cell.SetAttributeValue("r", cell.Attribute("r")!.Value.Replace("2", "3"));
            invalidRow.Elements(ns + "c").Single(c => (string?)c.Attribute("r") == "E3").Add(new XElement(ns + "f", "7"));
            validRow.Parent!.Add(invalidRow);
        });
        using (var input = new MemoryStream(mutations))
        {
            var report = await new QuestionBankStore(Path.Combine(directory, "formula.db3")).ImportExcelAsync(input);
            Check(report.Inserted == 1 && report.Rejected == 1 && report.Issues[0].RowNumber == 3
                && report.Issues[0].ErrorCode == "ExcelFormulaNotAllowed", "Formula validation or row diagnostics failed.");
        }
        using (var input = new MemoryStream(mutations))
            Check(QuestionBankWorkbook.Read(input)[0].Question!.Contract.Answer == sample.Contract.Answer, "Excel answer replaced the C# answer.");
        byte[] badText = Rewrite(original, (_, sheet) =>
        {
            XNamespace ns = sheet.Name.Namespace;
            sheet.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == "K2").Element(ns + "is")!.Element(ns + "t")!.Value = "Lan cho đi 999 quyển sách.";
        });
        using (var input = new MemoryStream(badText))
            Check((await target.ImportExcelAsync(input)).Rejected == 1, "Invalid imported mathematical prose reached SQLite.");
        byte[] missing = Rewrite(original, (_, sheet) => sheet.Descendants(sheet.Name.Namespace + "row").First().Remove());
        bool missingHeader = false;
        try { using var input = new MemoryStream(missing); await target.ImportExcelAsync(input); }
        catch (InvalidDataException error) when (error.Message == "ExcelColumnsMissing") { missingHeader = true; }
        Check(missingHeader, "Workbook without required headers was imported.");
        using (var template = new MemoryStream())
        {
            QuestionBankWorkbook.Write(template, []); template.Position = 0;
            Check(QuestionBankWorkbook.Read(template).Count == 0, "Empty Excel template is unusable.");
        }

        // A real database failure on the second insert must roll back the first insert.
        var firstContract = sample.Contract with { Subject = "ExcelFirst" };
        var failedContract = sample.Contract with { Subject = "ExcelFailure" };
        ValidatedBankQuestion Question(BasicQuestionContract c) => new(c, BasicQuestionValidator.Validate(prose(c), c).Draft!, prose(c), "transaction-test", DateTime.UtcNow);
        using (var db = new SQLiteConnection(database))
            db.Execute("CREATE TRIGGER fail_excel BEFORE INSERT ON BasicQuestionBank WHEN NEW.ContractJson LIKE '%ExcelFailure%' BEGIN SELECT RAISE(ABORT, 'test failure'); END");
        using (var transactionFile = new MemoryStream())
        {
            QuestionBankWorkbook.Write(transactionFile, [Question(firstContract), Question(failedContract)]); transactionFile.Position = 0;
            bool failed = false;
            try { await store.ImportExcelAsync(transactionFile); } catch (SQLiteException) { failed = true; }
            Check(failed && (await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank WHERE ModelName='transaction-test'")).Rows[0][0] == "0", "Failed Excel batch was partially committed.");
        }
        Console.WriteLine("PASS SQLite inquiry, limits/cancellation, XLSX bilingual round-trip, shared strings/numbers, duplicates, row validation and atomic import");
    }

    private static byte[] Rewrite(byte[] source, Action<ZipArchive, XElement> change)
    {
        using var memory = new MemoryStream(); memory.Write(source); memory.Position = 0;
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Update, true))
        {
            var sheet = ReadPart(archive, "xl/worksheets/sheet1.xml");
            change(archive, sheet); WritePart(archive, "xl/worksheets/sheet1.xml", sheet);
        }
        return memory.ToArray();
    }
    private static XElement ReadPart(ZipArchive archive, string path)
    { using var stream = archive.GetEntry(path)!.Open(); return XElement.Load(stream); }
    private static void WritePart(ZipArchive archive, string path, XElement content)
    { archive.GetEntry(path)?.Delete(); using var stream = archive.CreateEntry(path).Open(); content.Save(stream); }
}
