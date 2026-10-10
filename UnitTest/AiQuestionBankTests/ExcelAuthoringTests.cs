using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

internal static class ExcelAuthoringTests
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class BankRandom : Random { public override int Next(int maxValue) => Math.Min(1, maxValue - 1); }
    private static byte[] Rewrite(byte[] bytes, string sheet, Action<XElement> edit)
    {
        using var output = new MemoryStream(); output.Write(bytes);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, true))
        {
            var part = zip.GetEntry(sheet)!;
            XElement document;
            using (var stream = part.Open()) document = XElement.Load(stream);
            edit(document); part.Delete();
            using var target = zip.CreateEntry(sheet).Open(); document.Save(target);
        }
        return output.ToArray();
    }
    private static XElement Cell(XElement sheet, string address) => sheet.Descendants(Ns + "c").Single(c => (string?)c.Attribute("r") == address);
    private static void Text(XElement sheet, string address, string value) => Cell(sheet, address).Descendants(Ns + "t").Single().Value = value;
    private static void Schema(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator().Validate(document).Take(8).ToArray();
        Check(errors.Length == 0, "OOXML schema: " + string.Join("; ", errors.Select(e => e.Description + " " + e.Path?.XPath)));
    }

    public static async Task RunAsync()
    {
        int tested = 0;
        var samples = new List<ValidatedBankQuestion>();
        var userSamples = new List<ValidatedBankQuestion>();
        foreach (var family in Enum.GetValues<BankQuestionFamily>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            int[] variants = ReasoningStoryCatalogue.Supports(family) ? ReasoningStoryCatalogue.Variants(family, tier) : [0, 1, 2, 3];
            // Exercise the real illustrated-selector routes before writing the file.
            // Enum member names need not match localization/catalogue key suffixes.
            string familyKey = QuestionAuthoringChoices.FamilyKey(family);
            Check(!string.IsNullOrWhiteSpace(QuizChoiceCatalog.Create(0, familyKey, familyKey, language, false).Description),
                "Missing authoring family illustration: " + family);
            // Every currently exposed authoring subtype at every difficulty in both languages.
            foreach (int variant in variants)
            {
                if (ReasoningStoryCatalogue.Supports(family))
                {
                    string variantKey = QuestionAuthoringChoices.VariantKey(family, variant);
                    var choice = QuizChoiceCatalog.Create(variant, variantKey, variantKey, language, false);
                    Check(!string.IsNullOrWhiteSpace(choice.Description) && !string.IsNullOrWhiteSpace(choice.IllustrationId),
                        $"Missing authoring subtype illustration: {family}/{variant}/{tier}/{language}");
                }
                var c = QuestionAuthoringChoices.Create(family, variant, tier, language, random: new Random(5501 + tested));
                using var workbook = new MemoryStream();
                QuestionAuthoringWorkbook.Write(workbook, c, QuestionAuthoringChoices.FamilyLabel(family, language), language);
                var bytes = workbook.ToArray();
                if (tested < 17 || family is BankQuestionFamily.MultiStep or BankQuestionFamily.Geometry or BankQuestionFamily.Data) Schema(bytes);
                using (var original = new MemoryStream(bytes)) Check(QuestionBankWorkbook.Read(original).Count == 0, "Example was auto-imported: " + family);
                bytes = Rewrite(bytes, "xl/worksheets/sheet2.xml", sheet => Text(sheet, "A2", language == AppLanguage.Vietnamese ? "Nhập" : "Import"));
                using var input = new MemoryStream(bytes);
                var row = QuestionBankWorkbook.Read(input).Single();
                Check(row.Question is not null, $"Authoring failed: {family} / {variant} / {tier} / {language}: {row.ErrorCode} {string.Join(',', row.Issues)}");
                Check(System.Text.Json.JsonSerializer.Serialize(row.Question!.Contract) == System.Text.Json.JsonSerializer.Serialize(c)
                    && row.Question.Contract.AnswerText == c.AnswerText, "Authoring changed mathematics: " + family);
                if (variant == variants[0] && tier == CurriculumTier.OneStar) samples.Add(row.Question);
                if (variant == variants[0])
                {
                    var custom = Rewrite(bytes, "xl/worksheets/sheet2.xml", sheet => {
                        foreach (var cell in sheet.Descendants(Ns + "row").Single(r => (string?)r.Attribute("r") == "2").Elements(Ns + "c").Skip(1))
                        {
                            var text = cell.Descendants(Ns + "t").Single();
                            text.Value = (language == AppLanguage.Vietnamese ? "Theo ghi nhận của người soạn, " : "According to the author, ") + text.Value;
                        }
                    });
                    using var customInput = new MemoryStream(custom);
                    var authored = QuestionBankWorkbook.Read(customInput).Single().Question;
                    Check(authored?.UserAuthored == true && QuestionBankStore.ValidateSavedQuestion(authored).IsValid,
                        "User wording rejected: " + family + "/" + tier + "/" + language);
                    if (tier == CurriculumTier.OneStar) userSamples.Add(authored!);
                }
                tested++;
            }
        }
        Console.WriteLine($"PASS {tested} bilingual authoring configurations, field-based narratives, examples skipped, OOXML schema");

        int selectedScenes = 0;
        foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.FindX, BankQuestionFamily.Fraction })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var group in QuestionLearningProfile.Groups())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        {
            var scenes = QuestionAuthoringChoices.Scenes(family, operation, tier, group);
            if (scenes.Length == 0) continue;
            foreach (var language in Enum.GetValues<AppLanguage>())
            {
                var selected = QuestionAuthoringChoices.Create(family, (int)operation, tier, language, group, sceneId: scenes[0], random: new Random(908));
                Check(selected.IsValid && selected.SceneId == scenes[0], "Scene picker chose a different mathematical relation");
                selectedScenes++;
            }
        }
        Console.WriteLine($"PASS {selectedScenes} knowledge-group/operation selections preserve the selected situation");

        var contract = QuestionAuthoringChoices.Create(BankQuestionFamily.Arithmetic, 0, CurriculumTier.OneStar, AppLanguage.Vietnamese, random: new Random(89));
        using var file = new MemoryStream(); QuestionAuthoringWorkbook.Write(file, contract, "Phép cộng", AppLanguage.Vietnamese);
        byte[] editable = Rewrite(file.ToArray(), "xl/worksheets/sheet2.xml", sheet => Text(sheet, "A2", "Nhập"));
        byte[] missing = Rewrite(editable, "xl/worksheets/sheet2.xml", sheet => {
            var text = Cell(sheet, "C2").Descendants(Ns + "t").Single(); text.Value = text.Value.Replace("{b}", "");
        });
        using (var stream = new MemoryStream(missing))
        {
            var row = QuestionBankWorkbook.Read(stream).Single();
            Check(row.Question is null && row.RowNumber == 2 && row.Issues.Any(issue => issue.Column == "Mẫu dữ kiện thứ hai"
                && issue.ErrorCode == "ExcelMissingVariable" && issue.Details == "{b}"), "Missing-variable diagnostic lost the column or variable");
        }
        byte[] formula = Rewrite(editable, "xl/worksheets/sheet2.xml", sheet => Cell(sheet, "B2").Add(new XElement(Ns + "f", "1+1")));
        using (var stream = new MemoryStream(formula)) Check(QuestionBankWorkbook.Read(stream).Single().ErrorCode == "ExcelFormulaNotAllowed", "Formula accepted");
        byte[] formulaSkip = Rewrite(file.ToArray(), "xl/worksheets/sheet2.xml", sheet => Cell(sheet, "A2").Add(new XElement(Ns + "f", "1+1")));
        using (var stream = new MemoryStream(formulaSkip)) Check(QuestionBankWorkbook.Read(stream).Single().ErrorCode == "ExcelFormulaNotAllowed", "Cached Skip action trusted");
        byte[] unknown = Rewrite(editable, "xl/worksheets/sheet2.xml", sheet => {
            var text = Cell(sheet, "B2").Descendants(Ns + "t").Single(); text.Value += " {unknown}";
        });
        using (var stream = new MemoryStream(unknown)) Check(QuestionBankWorkbook.Read(stream).Single().Issues.Any(i => i.ErrorCode == "ExcelUnexpectedVariable"), "Unknown variable accepted");
        byte[] changedRelation = Rewrite(editable, "xl/worksheets/sheet2.xml", sheet => Text(sheet, "D2", "Hỏi {name} còn lại bao nhiêu {unit}?"));
        using (var stream = new MemoryStream(changedRelation)) Check(QuestionBankWorkbook.Read(stream).Single().Question?.UserAuthored == true, "User-controlled question was subjected to AI semantic validation");
        byte[] foreign = Rewrite(editable, "xl/worksheets/sheet2.xml", sheet => {
            var text = Cell(sheet, "B2").Descendants(Ns + "t").Single(); text.Value += " łącznie";
        });
        using (var stream = new MemoryStream(foreign))
        {
            var imported = QuestionBankWorkbook.Read(stream).Single().Question!;
            Check(imported.UserAuthored && !BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(imported.Draft), imported.Contract).IsValid,
                "Excel and AI language policies are no longer independent");
        }
        byte[] screenshot = Rewrite(editable, "xl/worksheets/sheet2.xml", sheet => {
            Text(sheet, "B2", "{name} có {a} {unit},");
            Text(sheet, "C2", "và {other} có {b} {unit}.");
            Text(sheet, "D2", "Hỏi cả hai có tổng cộng bao nhiêu {unit}?");
            Text(sheet, "E2", "Tổng số {unit} của {name} và {other} là:");
        });
        using (var stream = new MemoryStream(screenshot))
        {
            var question = QuestionBankWorkbook.Read(stream).Single().Question;
            Check(question?.UserAuthored == true && !question.WordProblem.ProblemText.Contains('{')
                && question.WordProblem.ProblemText.Contains(", và "), "User screenshot wording failed substitution or natural join");
        }
        foreach (string broken in new[] { "{name} có {a} {unit", "{{name}} có {a} {unit}", "{name} có {a} {}" })
        {
            byte[] badSyntax = Rewrite(editable, "xl/worksheets/sheet2.xml", sheet => Text(sheet, "B2", broken));
            using var stream = new MemoryStream(badSyntax);
            Check(QuestionBankWorkbook.Read(stream).Single().Question is null, "Broken substitution syntax accepted");
        }
        byte[] corruptedSchema = Rewrite(editable, "docProps/custom.xml", properties => {
            var value = properties.Elements().Single().Elements().Single();
            value.Value = value.Value.Replace("\"Format\":1", "\"Format\":999");
        });
        bool schemaRejected = false;
        try { using var stream = new MemoryStream(corruptedSchema); QuestionBankWorkbook.Read(stream); }
        catch (InvalidDataException error) when (error.Message == "ExcelAuthoringSchemaInvalid") { schemaRejected = true; }
        Check(schemaRejected, "Corrupted authoring metadata accepted");
        // Header positions are not fixed: spreadsheet users can reorder columns.
        byte[] reordered = Rewrite(editable, "xl/worksheets/sheet2.xml", sheet => {
            foreach (var row in sheet.Descendants(Ns + "row"))
            {
                var b = row.Elements(Ns + "c").Single(c => c.Attribute("r")!.Value.StartsWith('B'));
                var c = row.Elements(Ns + "c").Single(c => c.Attribute("r")!.Value.StartsWith('C'));
                string reference = b.Attribute("r")!.Value; b.SetAttributeValue("r", c.Attribute("r")!.Value); c.SetAttributeValue("r", reference);
            }
        });
        using (var stream = new MemoryStream(reordered)) Check(QuestionBankWorkbook.Read(stream).Single().Question is not null, "Reordered headers failed");

        // Spreadsheet editors commonly rewrite inline text as shared strings when saving.
        using (var saved = new MemoryStream())
        {
            saved.Write(editable);
            using (var document = SpreadsheetDocument.Open(saved, true))
            {
                var workbook = document.WorkbookPart!;
                var strings = workbook.AddNewPart<SharedStringTablePart>();
                strings.SharedStringTable = new DocumentFormat.OpenXml.Spreadsheet.SharedStringTable();
                var selected = workbook.Workbook!.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Sheets>()!
                    .Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().Single(sheet => sheet.Name?.Value == "Nhập đề");
                var inputSheet = (WorksheetPart)workbook.GetPartById(selected.Id!.Value!);
                foreach (var cell in inputSheet.Worksheet!.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                {
                    int index = strings.SharedStringTable.ChildElements.Count;
                    string value = cell.InnerText;
                    strings.SharedStringTable.Append(new DocumentFormat.OpenXml.Spreadsheet.SharedStringItem(
                        new DocumentFormat.OpenXml.Spreadsheet.Text(value)));
                    cell.RemoveAllChildren();
                    cell.DataType = DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString;
                    cell.CellValue = new DocumentFormat.OpenXml.Spreadsheet.CellValue(index.ToString());
                }
            }
            Schema(saved.ToArray()); saved.Position = 0;
            Check(QuestionBankWorkbook.Read(saved).Single().Question is not null, "Editor save lost shared-string prose or authoring metadata");
        }

        string directory = Path.Combine(Path.GetTempPath(), "MathSolver-excel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        {
            var store = new QuestionBankStore(Path.Combine(directory, "bank.db3"));
            using var stream = new MemoryStream(editable);
            var preview = await store.PreviewExcelAsync(stream);
            Check(!File.Exists(Path.Combine(directory, "bank.db3")), "Preview created or wrote a database");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                bool aborted = false;
                try { await store.ImportReviewedAsync(preview, cancelled.Token); }
                catch (OperationCanceledException) { aborted = true; }
                Check(aborted && !File.Exists(Path.Combine(directory, "bank.db3")), "Cancelled reviewed import wrote SQLite");
            }
            var report = await store.ImportReviewedAsync(preview);
            Check(report.Inserted == 1 && report.Rejected == 0, "Reviewed commit failed");
            Check((await store.ImportReviewedAsync(preview)).Duplicates == 1, "Reviewed commit lost deduplication");
            using var bad = new MemoryStream(missing);
            var invalid = await store.PreviewExcelAsync(bad);
            Check((await store.ImportReviewedAsync(invalid)).Rejected == 1, "Invalid rows reached database");
            var authoredStore = new QuestionBankStore(Path.Combine(directory, "user-prose.db3"));
            foreach (var user in userSamples)
            {
                Check(await authoredStore.InsertAsync(user), "User template rejected at SQLite boundary");
                var c = user.Contract;
                // A new store instance verifies persisted provenance, rather than an in-memory flag.
                var reopened = new QuestionBankStore(Path.Combine(directory, "user-prose.db3"));
                var picked = c.Family switch {
                    BankQuestionFamily.Arithmetic => await reopened.TakeForProfileAsync(c.Operation, c.Tier, c.Language, new(c.KnowledgeGroup)),
                    BankQuestionFamily.FindX => await reopened.TakeFindXAsync(c.Operation, c.Tier, c.Language, new(c.KnowledgeGroup)),
                    BankQuestionFamily.Fraction => await reopened.TakeFractionAsync(c.Operation, c.Tier, c.Language, new(c.KnowledgeGroup)),
                    _ => await reopened.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language)
                };
                Check(picked?.UserAuthored == true && picked.Draft == user.Draft
                    || picked?.UserAuthored == true && QuestionBankStore.SerializeDraft(picked.Draft) == QuestionBankStore.SerializeDraft(user.Draft),
                    "User template was lost during stored selection: " + c.Family);
                for (int seed = 0; seed < 3; seed++)
                {
                    var fresh = picked! with { Contract = c.FreshFacts(new Random(seed + 189)) };
                    Check(QuestionBankStore.ValidateSavedQuestion(fresh).IsValid && !fresh.WordProblem.ProblemText.Contains('{'),
                        "User template failed with fresh C# data: " + c.Family);
                }
                var generated = c.Version == ReasoningStoryCatalogue.Version
                    ? ReasoningStoryCatalogue.ToPractice(c, BasicQuestionTemplates.Example(c), ArithmeticQuizMode.Essay)
                    : c.ToPracticeQuestion(BasicQuestionTemplates.Example(c).ToWordProblem(c), ArithmeticQuizMode.Essay);
                var provider = new BasicPracticeQuestionProvider(reopened, new BankRandom());
                var practice = c.Family switch {
                    BankQuestionFamily.Arithmetic => await provider.SelectAsync(generated, c.Tier, c.Language,
                        profile: new(c.KnowledgeGroup), format: PracticeQuestionFormat.WordProblem),
                    BankQuestionFamily.FindX => await provider.SelectFindXAsync(generated, c.Tier, c.Language,
                        new(c.KnowledgeGroup), format: PracticeQuestionFormat.WordProblem),
                    BankQuestionFamily.Fraction => await provider.SelectFractionAsync(generated, c.Tier, c.Language, new(c.KnowledgeGroup)),
                    _ => await provider.SelectReasoningAsync(generated, c.Tier, c.Language)
                };
                string practiceText = practice.WordProblem?.ProblemText ?? practice.ElementaryProblem?.ProblemText
                    ?? practice.AverageProblem?.ProblemText ?? practice.PercentageProblem?.ProblemText
                    ?? practice.MotionProblem?.ProblemText ?? practice.ProportionProblem?.ProblemText ?? "";
                Check(practiceText.StartsWith(c.Language == AppLanguage.Vietnamese ? "Theo ghi nhận của người soạn, " : "According to the author, "),
                    "Provider silently fell back to C# wording: " + c.Family);
            }
            using (var authoredBackup = new MemoryStream())
            {
                var export = await authoredStore.ExportExcelAsync(authoredBackup);
                Check(export.Exported == userSamples.Count && export.Skipped == 0, "User templates skipped from backup");
                authoredBackup.Position = 0;
                var restoredUsers = new QuestionBankStore(Path.Combine(directory, "user-restored.db3"));
                var restoreUsers = await restoredUsers.ImportExcelAsync(authoredBackup);
                Check(restoreUsers.Inserted == userSamples.Count && restoreUsers.Rejected == 0, "User template backup lost syntax-only provenance");
                Check(await restoredUsers.GetProseHashesAsync() is { Count: > 0 }, "User template prose indexing failed");
            }
            using (var screenshotFile = new MemoryStream(screenshot))
            {
                var screenshotStore = new QuestionBankStore(Path.Combine(directory, "screenshot.db3"));
                Check((await screenshotStore.ImportExcelAsync(screenshotFile)).Inserted == 1, "Screenshot import did not commit");
                var selected = await screenshotStore.TakeForProfileAsync(contract.Operation, contract.Tier, contract.Language, new(contract.KnowledgeGroup));
                Check(selected?.UserAuthored == true && selected.Draft.GivenB == "và {other} có {b} {unit}.", "Screenshot prose rejected during practice selection");
                // A caller that forgets provenance must still go through strict AI validation.
                bool strictRejected = false;
                try { await screenshotStore.InsertAsync(selected! with { UserAuthored = false }); }
                catch (InvalidOperationException) { strictRejected = true; }
                Check(strictRejected, "AI insertion incorrectly inherited Excel validation policy");
            }
            foreach (var question in samples) await store.InsertAsync(question);
            using var backup = new MemoryStream();
            var exported = await store.ExportExcelAsync(backup);
            byte[] bytes = backup.ToArray(); Schema(bytes);
            using (var zip = new ZipArchive(new MemoryStream(bytes)))
            {
                using var document = zip.GetEntry("xl/workbook.xml")!.Open();
                var sheets = XElement.Load(document).Descendants(Ns + "sheet").ToArray();
                Check(sheets.Length == 3 && sheets[0].Attribute("name")!.Value == "Ngân hàng đề"
                    && sheets.Single(s => s.Attribute("name")!.Value == "Questions").Attribute("state")!.Value == "hidden", "Backup exposes internal fields");
            }
            bytes = Rewrite(bytes, "xl/worksheets/sheet1.xml", sheet => Text(sheet, "E2", "999999"));
            using var restoredFile = new MemoryStream(bytes);
            var restored = new QuestionBankStore(Path.Combine(directory, "restored.db3"));
            var restore = await restored.ImportExcelAsync(restoredFile);
            Check(restore.Inserted == exported.Exported && restore.Rejected == 0, "Backup restore lost schemas");
            restoredFile.Position = 0;
            Check(QuestionBankWorkbook.Read(restoredFile).All(r => r.Question?.Contract.AnswerText != "999999"), "Overview answer changed C# computation");
            using var legacy = new MemoryStream(); QuestionBankWorkbook.Write(legacy, samples); legacy.Position = 0;
            Check(QuestionBankWorkbook.Read(legacy).All(r => r.Question is not null), "Legacy workbook compatibility lost");
            byte[] oldFormat = Rewrite(legacy.ToArray(), "xl/worksheets/sheet1.xml", sheet => {
                var header = sheet.Descendants(Ns + "row").First();
                string column = string.Concat(header.Elements(Ns + "c").Single(cell => cell.Value == "UserAuthored")
                    .Attribute("r")!.Value.TakeWhile(char.IsLetter));
                foreach (var cell in sheet.Descendants(Ns + "c").Where(cell => string.Concat(cell.Attribute("r")!.Value.TakeWhile(char.IsLetter)) == column).ToArray())
                    cell.Remove();
                Text(sheet, "K2", "và " + Cell(sheet, "K2").Value);
            });
            using (var oldInput = new MemoryStream(oldFormat))
                Check(QuestionBankWorkbook.Read(oldInput).All(r => r.Question?.UserAuthored == true), "Historical user templates were subjected to strict AI validation");
            using var empty = new MemoryStream(); QuestionBankWorkbook.WriteBackup(empty, [], AppLanguage.English); Schema(empty.ToArray()); empty.Position = 0;
            Check(QuestionBankWorkbook.Read(empty).Count == 0, "Empty bilingual backup could not be restored");
            string artifacts = Path.Combine("artifacts", "verification", "excel-authoring"); Directory.CreateDirectory(artifacts);
            File.WriteAllBytes(Path.Combine(artifacts, "mau-soan-de-phep-cong.xlsx"), editable);
            using var multi = new MemoryStream();
            QuestionAuthoringWorkbook.Write(multi, QuestionAuthoringChoices.Create(BankQuestionFamily.MultiStep,
                ReasoningStoryCatalogue.Variants(BankQuestionFamily.MultiStep, (CurriculumTier)5)[0], (CurriculumTier)5, AppLanguage.Vietnamese), "Bài nhiều bước", AppLanguage.Vietnamese);
            File.WriteAllBytes(Path.Combine(artifacts, "mau-soan-de-nhieu-buoc.xlsx"), multi.ToArray());
            File.WriteAllBytes(Path.Combine(artifacts, "ngan-hang-de-sao-luu.xlsx"), backup.ToArray());
        }
        Console.WriteLine("PASS syntax-only user prose, screenshot wording, fresh facts, persisted selection/backup provenance; strict AI policy retained");
        Console.WriteLine("PASS preview without writes, explicit commit, duplicate protection, row/column errors, formulas, backup and legacy restore");
    }
}
