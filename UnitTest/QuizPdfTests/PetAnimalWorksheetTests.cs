using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.IO.Compression;
using System.Xml.Linq;

internal static class PetAnimalWorksheetTests
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }

    internal static async Task Run(string directory)
    {
        string[] ids =
        [
            "pomeranians", "hamsters", "guinea-pigs", "dwarf-rabbits", "parrots", "budgerigars",
            "cockatiels", "lovebirds", "pigeons", "canaries", "zebra-finches", "flowerhorns",
            "arowanas", "guppies", "goldfish", "mollies", "platies", "swordtails"
        ];
        var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Animals);
        var samples = new List<ValidatedBankQuestion>();
        int contracts = 0, pdfs = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var printed = new List<QuizPrintItem>();
            foreach (string id in ids)
            {
                Check(QuestionUnits.Find(id) is not null, "Pet unit missing: " + id);
                foreach (var (suffix, operation) in new[]
                {
                    ("total", ArithmeticOperation.Add), ("remaining", ArithmeticOperation.Subtract),
                    ("groups", ArithmeticOperation.Multiply), ("share", ArithmeticOperation.Divide)
                })
                {
                    string sceneId = $"theme-animal-{id}-{suffix}";
                    var scene = AppliedQuestionCatalogue.Find(sceneId);
                    Check(scene?.Group == QuestionKnowledgeGroup.Animals && scene.UnitId == id, "Pet scene missing: " + sceneId);
                    foreach (var tier in Enum.GetValues<CurriculumTier>())
                    {
                        var c = AppliedQuestionCatalogue.Create(profile, operation, tier, language, new Random(712 + (int)tier), sceneId);
                        var d = AppliedQuestionCatalogue.Draft(c);
                        string raw = QuestionBankStore.SerializeDraft(d);
                        var validation = BasicQuestionValidator.Validate(raw, c);
                        Check(c.IsValid && validation.IsValid, $"Invalid pet {sceneId}/{tier}/{language}: {validation.ErrorCode}");
                        if (operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide
                            && id is "hamsters" or "flowerhorns" or "arowanas")
                        {
                            var perGroup = operation == ArithmeticOperation.Divide ? c.Answer : c.Left;
                            Check(perGroup == 1, "Solitary pet housed with others: " + sceneId);
                            var wrong = operation == ArithmeticOperation.Multiply
                                ? c with { Left = 2 } : c with { Left = 2 * c.Right };
                            Check(!AppliedQuestionCatalogue.IsValid(wrong), "Unsafe shared enclosure accepted: " + sceneId);
                        }
                        string prompt = BasicQuestionPrompt.Build(c);
                        Check(prompt.Contains(c.Unit, StringComparison.Ordinal), "AI prompt lost animal unit: " + sceneId);
                        Check(prompt.Contains(language == AppLanguage.Vietnamese ? "Loài cụ thể" : "Selected species", StringComparison.Ordinal),
                            "AI prompt lost animal-specific care context: " + sceneId);
                        if (OpenQuestionProse.Supports(c))
                        {
                            var open = c with { OpenProse = true };
                            Check(BasicQuestionValidator.Validate(raw, open).IsValid, "AI creative prose invalid: " + sceneId);
                            Check(BasicQuestionPrompt.Build(open).Contains(c.Unit, StringComparison.Ordinal), "AI creative prompt lost pet: " + sceneId);
                        }
                        if (tier == CurriculumTier.ThreeStars)
                        {
                            var saved = new ValidatedBankQuestion(c, d, raw, "pet-animal-test", DateTime.UtcNow);
                            Check(QuestionBankStore.ValidateSavedQuestion(saved).IsValid, "Pet worksheet row invalid: " + sceneId);
                            samples.Add(saved);
                            var question = c.ToPracticeQuestion(d.ToWordProblem(c), ArithmeticQuizMode.Essay);
                            var item = QuizWorksheetBuilder.Format(question, language);
                            Check(!string.IsNullOrWhiteSpace(item.Prompt) && !string.IsNullOrWhiteSpace(item.Answer),
                                "Pet PDF output missing: " + sceneId);
                            printed.Add(item);
                        }
                        contracts++;
                    }
                    var findScene = FindXQuestionCatalogue.All
                        .FirstOrDefault(s => s.SourceScene.Id == sceneId);
                    Check(findScene is not null, "Pet Find X scene missing: " + sceneId);
                    var findX = FindXQuestionCatalogue.Create(profile, findScene!.EquationOperation, CurriculumTier.ThreeStars, language,
                        new Random(813), sceneId: findScene!.Id);
                    Check(findX.IsValid && BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(FindXQuestionCatalogue.Draft(findX)), findX).IsValid,
                        "Pet Find X question invalid: " + sceneId);
                }
            }
            string title = language == AppLanguage.Vietnamese ? "Bài toán thú cưng và cá cảnh" : "Pet and ornamental fish problems";
            var worksheet = new QuizWorksheet("pets", title, "", language, CurriculumTier.ThreeStars,
                ArithmeticQuizMode.Essay, printed.AsReadOnly());
            foreach (bool answers in new[] { false, true })
            {
                byte[] pdf = QuizPdfWriter.Write(worksheet, answers);
                PdfAssertions.HasEmbeddedText(pdf);
                string text = PdfTestReader.ExtractText(pdf);
                Check(text.Contains(title, StringComparison.Ordinal) && !text.Contains("{unit", StringComparison.Ordinal),
                    "Pet PDF lost title or rendered placeholders");
                File.WriteAllBytes(Path.Combine(directory, $"pets-{language}-{answers}.pdf"), pdf);
                pdfs++;
            }
            var example = samples.First(s => s.Contract.Language == language && s.Contract.SceneId == "theme-animal-hamsters-share");
            using var template = new MemoryStream();
            QuestionAuthoringWorkbook.Write(template, example.Contract, title, language);
            template.Position = 0;
            Check(QuestionBankWorkbook.Read(template).Count == 0, "Pet authoring example should default to Skip");
            template.Position = 0;
            using (var zip = new ZipArchive(template, ZipArchiveMode.Update, true))
            {
                var entry = zip.GetEntry("xl/worksheets/sheet2.xml")!;
                XElement xml;
                using (var input = entry.Open()) xml = XElement.Load(input);
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                xml.Descendants(ns + "c").Single(e => (string?)e.Attribute("r") == "A2")
                    .Descendants(ns + "t").Single().Value = language == AppLanguage.Vietnamese ? "Nhập" : "Import";
                entry.Delete();
                using var output = zip.CreateEntry("xl/worksheets/sheet2.xml").Open();
                xml.Save(output);
            }
            template.Position = 0;
            var edited = QuestionBankWorkbook.Read(template).Single();
            Check(edited.Question?.Contract == example.Contract && QuestionBankStore.ValidateSavedQuestion(edited.Question!).IsValid,
                "Pet authoring Excel failed import");
            File.WriteAllBytes(Path.Combine(directory, $"pets-{language}-authoring.xlsx"), template.ToArray());
        }
        using var excel = new MemoryStream();
        QuestionBankWorkbook.WriteBackup(excel, samples);
        excel.Position = 0;
        var rows = QuestionBankWorkbook.Read(excel);
        Check(rows.Count == samples.Count, "Pet Excel backup row count mismatch");
        for (int i = 0; i < rows.Count; i++)
            Check(rows[i].Question?.Contract == samples[i].Contract && rows[i].Question?.Draft == samples[i].Draft,
                "Pet Excel changed a question");
        var store = new QuestionBankStore(Path.Combine(directory, "pet-animals-" + Guid.NewGuid().ToString("N") + ".db3"));
        excel.Position = 0;
        var imported = await store.ImportExcelAsync(excel);
        Check(imported.Inserted == samples.Count && imported.Rejected == 0, "Pet Excel import rejected questions");
        using var exported = new MemoryStream();
        await store.ExportExcelAsync(exported);
        exported.Position = 0;
        Check(QuestionBankWorkbook.Read(exported).Count == samples.Count, "Pet Excel export lost questions");
        File.WriteAllBytes(Path.Combine(directory, "pet-animals-backup.xlsx"), excel.ToArray());
        Console.WriteLine($"PASS pet animals: {ids.Length} species, {contracts} VI/EN arithmetic contracts, Find X, {pdfs} PDFs, 2 authoring templates, {samples.Count} Excel/SQLite rows.");
    }
}
