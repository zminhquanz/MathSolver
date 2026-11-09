using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

internal static class EverydayItemWorksheetTests
{
    private sealed record Item(QuestionKnowledgeGroup Group, string UnitId, string Prefix);

    private static readonly Item[] Subjects =
    [
        new(QuestionKnowledgeGroup.SchoolSupplies, "erasers", "school-erasers-"),
        new(QuestionKnowledgeGroup.Objects, "footwear-pairs", "daily-footwear-pairs-"),
        new(QuestionKnowledgeGroup.Objects, "clothing-sets", "daily-clothing-sets-"),
        new(QuestionKnowledgeGroup.Objects, "toothbrushes", "daily-toothbrushes-"),
        new(QuestionKnowledgeGroup.Objects, "combs", "daily-combs-"),
        new(QuestionKnowledgeGroup.Objects, "serving-bowls", "daily-serving-bowls-"),
        new(QuestionKnowledgeGroup.Objects, "rice-bowls", "daily-rice-bowls-"),
        new(QuestionKnowledgeGroup.Objects, "plates", "daily-plates-"),
        new(QuestionKnowledgeGroup.Objects, "spoons", "daily-spoons-"),
        new(QuestionKnowledgeGroup.Objects, "forks", "daily-forks-"),
        new(QuestionKnowledgeGroup.Objects, "chopstick-pairs", "daily-chopstick-pairs-"),
        new(QuestionKnowledgeGroup.Objects, "drinking-glasses", "daily-drinking-glasses-")
    ];

    private static readonly (string ContextId, string ArtworkId)[] ExpandedSubjects =
    [
        ("daily-footwear-stock", "footwear"),
        ("daily-clothing-stock", "clothing"),
        ("daily-toothbrush-stock", "toothbrush"),
        ("daily-comb-stock", "comb"),
        ("daily-serving-bowl-stock", "serving_bowl"),
        ("daily-rice-bowl-stock", "rice_bowl"),
        ("daily-plate-stock", "plate"),
        ("daily-spoon-stock", "spoon"),
        ("daily-fork-stock", "fork"),
        ("daily-chopsticks-stock", "chopsticks"),
        ("daily-drinking-glass-stock", "drinking_glass")
    ];

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static AppliedQuestionScene Scene(Item item, ArithmeticOperation operation)
    {
        var matches = AppliedQuestionCatalogue.GenerationAvailable(new(item.Group), operation, CurriculumTier.ThreeStars)
            .Where(scene => scene.UnitId == item.UnitId && scene.Id.StartsWith(item.Prefix, StringComparison.Ordinal))
            .ToArray();
        Check(matches.Length > 0, $"Missing {operation} scene for {item.UnitId}");
        return matches[0];
    }

    private static BasicQuestionContract Contract(Item item, ArithmeticOperation operation, AppLanguage language)
    {
        var scene = Scene(item, operation);
        var contract = AppliedQuestionCatalogue.Create(new(item.Group), operation, CurriculumTier.ThreeStars,
            language, new Random(319 + (int)operation * 11), scene.Id);
        Check(contract.KnowledgeGroup == item.Group && contract.SceneId == scene.Id,
            $"Scene routing changed: {item.UnitId}, {operation}, {language}");
        var unit = QuestionUnits.Find(item.UnitId)
            ?? throw new InvalidOperationException($"Missing unit: {item.UnitId}");
        Check(contract.Unit == unit.Item(language), $"Incorrect unit: {item.UnitId}");
        if (language == AppLanguage.English)
            Check(contract.GroupUnit == unit.Group(language, singular: true), $"Incorrect group singular: {item.UnitId}");
        long quantity = operation == ArithmeticOperation.Multiply
            ? (long)contract.Left * contract.Right : Math.Max(contract.Left, contract.Right);
        if (quantity > 30)
        {
            string expectedActor = language == AppLanguage.Vietnamese ? "cửa hàng" : "shop";
            Check(contract.Subject.Contains(expectedActor, StringComparison.Ordinal)
                  && contract.OtherSubject.Contains(expectedActor, StringComparison.Ordinal),
                $"Implausible bulk-item actors: {item.UnitId} / {language} / {contract.Subject}");
        }
        return contract;
    }

    private static byte[] ActivateExample(byte[] bytes, AppLanguage language)
    {
        using var buffer = new MemoryStream();
        buffer.Write(bytes);
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
        {
            var part = archive.GetEntry("xl/worksheets/sheet2.xml")
                ?? throw new InvalidOperationException("Authoring input sheet missing");
            XElement sheet;
            using (var reader = part.Open()) sheet = XElement.Load(reader);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var cell = sheet.Descendants(ns + "c")
                .Single(c => (string?)c.Attribute("r") == "A2");
            cell.Descendants(ns + "t").Single().Value = language == AppLanguage.Vietnamese ? "Nh\u1eadp" : "Import";
            part.Delete();
            using var writer = archive.CreateEntry("xl/worksheets/sheet2.xml").Open();
            sheet.Save(writer);
        }
        return buffer.ToArray();
    }

    public static async Task Run(string output)
    {
        Directory.CreateDirectory(output);
        var validator = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        var traces = new List<object>();
        int questions = 0, expandedQuestions = 0, pdfCount = 0, templates = 0, bankRows = 0;

        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var records = new List<ValidatedBankQuestion>();
            foreach (var item in Subjects)
            foreach (var operation in Enum.GetValues<ArithmeticOperation>())
            {
                var contract = Contract(item, operation, language);
                var draft = BasicQuestionTemplates.Example(contract);
                string raw = QuestionBankStore.SerializeDraft(draft);
                Check(BasicQuestionValidator.Validate(raw, contract).IsValid,
                    $"Invalid source template: {contract.SceneId} / {language}");
                records.Add(new(contract, draft, raw, "C# everyday items", DateTime.UtcNow));
            }

            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            {
                var printItems = new List<QuizPrintItem>();
                foreach (var record in records)
                {
                    var question = record.Contract.ToPracticeQuestion(record.Draft.ToWordProblem(record.Contract), mode, new Random(91));
                    Check(validator.Validate(question).IsValid, $"Invalid question: {record.Contract.SceneId} / {mode}");
                    var printed = QuizWorksheetBuilder.Format(question, language);
                    Check(!string.IsNullOrWhiteSpace(printed.Prompt) && !string.IsNullOrWhiteSpace(printed.Answer),
                        $"Empty print text: {record.Contract.SceneId}");
                    Check(!printed.Prompt.Contains('{') && !printed.Answer.Contains('{'),
                        $"Unexpanded print placeholder: {record.Contract.SceneId}");
                    if (mode == ArithmeticQuizMode.TrueFalse)
                        Check(printed.Answer == (question.PresentedEquationIsCorrect == true ? printed.Options[0] : printed.Options[1]),
                            $"Incorrect true/false key: {record.Contract.SceneId}");
                    printItems.Add(printed);
                    traces.Add(new { language, mode, record.Contract.SceneId, record.Contract.KnowledgeGroup,
                        record.Contract.Unit, printed.Prompt, printed.Answer });
                    questions++;
                }

                string title = language == AppLanguage.Vietnamese ? "\u0110\u1ed3 d\u00f9ng h\u1eb1ng ng\u00e0y v\u00e0 h\u1ecdc t\u1eadp" : "Everyday and school items";
                var sheet = new QuizWorksheet("EVERYDAY-ITEMS", title, title, language,
                    CurriculumTier.ThreeStars, mode, printItems);
                string snapshot = JsonSerializer.Serialize(sheet);
                foreach (bool withAnswers in new[] { false, true })
                {
                    byte[] pdf = QuizPdfWriter.Write(sheet, withAnswers);
                    PdfAssertions.HasEmbeddedText(pdf);
                    string text = PdfTestReader.ExtractText(pdf);
                    Check(!text.Contains("{unit") && !text.Contains("{group") && !text.Contains("{name"),
                        "PDF contains unexpanded template variables");
                    Check(text.Contains(title, StringComparison.Ordinal), "PDF missing worksheet title");
                    string name = $"everyday-{language}-{mode}-{(withAnswers ? "answers" : "worksheet")}.pdf";
                    await File.WriteAllBytesAsync(Path.Combine(output, name), pdf);
                    pdfCount++;
                }
                Check(JsonSerializer.Serialize(sheet) == snapshot, "PDF export changed source questions");
            }

            foreach (var tier in new[] { CurriculumTier.FourStars, CurriculumTier.FiveStars })
            {
                var expandedRecords = new List<ValidatedBankQuestion>();
                foreach (var (contextId, artworkId) in ExpandedSubjects)
                foreach (var operation in Enum.GetValues<ArithmeticOperation>())
                {
                    var contract = ExpandedArithmeticCatalogue.Create(new(QuestionKnowledgeGroup.Objects),
                        operation, tier, language, new Random(733 + (int)tier * 97 + (int)operation), contextId);
                    var draft = ExpandedArithmeticCatalogue.Draft(contract);
                    string raw = QuestionBankStore.SerializeDraft(draft);
                    Check(contract.IsValid && BasicQuestionValidator.Validate(raw, contract).IsValid,
                        $"Invalid expanded everyday item: {contextId} / {operation} / {tier} / {language}");
                    var word = draft.ToWordProblem(contract);
                    Check(word.StoryArtworkId == artworkId &&
                        QuizStoryArtwork.Select(true, word.FactTable is not null, [word.AnswerUnit],
                            itemUnit: word.ItemUnit, artworkId: word.StoryArtworkId) == $"quiz_story_{artworkId}.png",
                        $"Expanded artwork mismatch: {contextId} / {tier} / {language}");
                    expandedRecords.Add(new(contract, draft, raw, "C# expanded everyday items", DateTime.UtcNow));
                }

                foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var printItems = new List<QuizPrintItem>();
                    foreach (var record in expandedRecords)
                    {
                        var question = record.Contract.ToPracticeQuestion(record.Draft.ToWordProblem(record.Contract),
                            mode, new Random(91));
                        Check(validator.Validate(question).IsValid &&
                            question.WordProblem?.StoryArtworkId is not null,
                            $"Expanded replay invalid: {record.Contract.SceneId} / {mode}");
                        var printed = QuizWorksheetBuilder.Format(question, language);
                        Check(!string.IsNullOrWhiteSpace(printed.Prompt) &&
                            !printed.Prompt.Contains('{') && !printed.Answer.Contains('{'),
                            $"Expanded PDF text invalid: {record.Contract.SceneId}");
                        printItems.Add(printed);
                        questions++;
                        expandedQuestions++;
                    }
                    string title = language == AppLanguage.Vietnamese
                        ? "Đồ dùng hằng ngày" : "Everyday items";
                    var sheet = new QuizWorksheet("EVERYDAY-EXPANDED", title, title, language, tier, mode, printItems);
                    string snapshot = JsonSerializer.Serialize(sheet);
                    foreach (bool withAnswers in new[] { false, true })
                    {
                        byte[] pdf = QuizPdfWriter.Write(sheet, withAnswers);
                        PdfAssertions.HasEmbeddedText(pdf);
                        Check(PdfTestReader.ExtractText(pdf).Contains(title, StringComparison.Ordinal),
                            "Expanded PDF missing worksheet title");
                        await File.WriteAllBytesAsync(Path.Combine(output,
                            $"everyday-expanded-{language}-{tier}-{mode}-{(withAnswers ? "answers" : "worksheet")}.pdf"),
                            pdf);
                        pdfCount++;
                    }
                    Check(JsonSerializer.Serialize(sheet) == snapshot, "Expanded PDF changed source facts");
                }
                records.AddRange(expandedRecords);
            }

            using var backup = new MemoryStream();
            QuestionBankWorkbook.WriteBackup(backup, records);
            string culture = QuizContentCatalog.Culture(language);
            await File.WriteAllBytesAsync(Path.Combine(output, $"everyday-{culture}-backup.xlsx"), backup.ToArray());
            backup.Position = 0;
            var rows = QuestionBankWorkbook.Read(backup);
            Check(rows.Count == records.Count && rows.All(r => r.Question is not null),
                $"Invalid Excel backup rows: {culture}");
            for (int index = 0; index < records.Count; index++)
                Check(rows[index].Question!.Contract == records[index].Contract,
                    $"Excel changed typed facts for {records[index].Contract.SceneId}");

            var store = new QuestionBankStore(Path.Combine(output, $"everyday-{culture}.db3"));
            backup.Position = 0;
            var imported = await store.ImportExcelAsync(backup);
            int distinct = records.Select(record => QuestionProseIdentity.Hash(record.Contract, record.Draft))
                .Distinct(StringComparer.Ordinal).Count();
            Check(imported.Inserted == distinct && imported.Duplicates == records.Count - distinct && imported.Rejected == 0,
                $"SQLite import mismatch: {JsonSerializer.Serialize(imported)}");
            bankRows += distinct;

            foreach (var group in new[] { QuestionKnowledgeGroup.SchoolSupplies, QuestionKnowledgeGroup.Objects })
            {
                var retrieved = await store.TakeForProfileAsync(ArithmeticOperation.Add, CurriculumTier.ThreeStars,
                    language, new(group));
                Check(retrieved?.Contract.KnowledgeGroup == group, $"SQLite category routing failed: {group}");
            }

            using var exported = new MemoryStream();
            await store.ExportExcelAsync(exported);
            exported.Position = 0;
            var roundTrip = QuestionBankWorkbook.Read(exported);
            Check(roundTrip.Count == distinct && roundTrip.All(row => row.Question is not null),
                $"Excel re-export lost item rows: {culture}");
            foreach (var item in Subjects)
            {
                var contract = Contract(item, ArithmeticOperation.Add, language);
                using var authoring = new MemoryStream();
                QuestionAuthoringWorkbook.Write(authoring, contract, item.UnitId, language);
                byte[] workbook = authoring.ToArray();
                using (var schemaStream = new MemoryStream(workbook))
                using (var document = SpreadsheetDocument.Open(schemaStream, false))
                    Check(!new OpenXmlValidator().Validate(document).Any(), $"Invalid template OOXML: {item.UnitId}");
                using (var inactive = new MemoryStream(workbook))
                    Check(QuestionBankWorkbook.Read(inactive).Count == 0, $"Inactive example auto-imported: {item.UnitId}");
                using (var active = new MemoryStream(ActivateExample(workbook, language)))
                {
                    var authored = QuestionBankWorkbook.Read(active).Single();
                    Check(authored.Question?.Contract == contract &&
                        QuestionBankStore.ValidateSavedQuestion(authored.Question!).IsValid,
                        $"Authoring import changed template: {item.UnitId} / {culture}");
                }
                await File.WriteAllBytesAsync(Path.Combine(output, $"everyday-{item.UnitId}-{culture}-template.xlsx"), workbook);
                templates++;
            }
        }

        await File.WriteAllTextAsync(Path.Combine(output, "everyday-items-cases.json"),
            JsonSerializer.Serialize(traces, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS everyday items: {Subjects.Length} subjects, {questions} VI/EN operation/mode questions, " +
            $"{expandedQuestions} expanded four/five-star checks, {pdfCount} PDF worksheets/answer keys, " +
            $"{bankRows} SQLite records, {templates} Excel authoring templates and backup/import/re-export.");
    }
}
