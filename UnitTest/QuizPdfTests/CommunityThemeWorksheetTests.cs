using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.IO.Compression;
using System.Xml.Linq;

internal static class CommunityThemeWorksheetTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static async Task Run(string directory)
    {
        var records = new List<ValidatedBankQuestion>(); int cases = 0, pdfs = 0, presentations = 0, templates = 0;
        // This is the pre-existing Vietnamese saved-contract unit, independent of the new School wording.
        var legacy = AdditionQuestionCatalogue.Create(CurriculumTier.OneStar, AppLanguage.Vietnamese,
            new(7), sceneId: "club-arrivals", structure: BasicQuestionStructure.Increase) with { Unit = "bạn" };
        Check(legacy.IsValid, "Pre-existing club-arrivals/student contract became invalid");
        var legacyDraft = BasicQuestionTemplates.Example(legacy);
        records.Add(new(legacy, legacyDraft, QuestionBankStore.SerializeDraft(legacyDraft), "legacy-students", DateTime.UtcNow));
        foreach (string name in new[] { "Toys", "Healthcare", "School" })
        {
            Check(Enum.TryParse<QuestionKnowledgeGroup>(name, out var group) && QuestionLearningProfile.Groups().Contains(group), "Missing selectable group: " + name);
            foreach (var language in Enum.GetValues<AppLanguage>())
            {
                var choice = QuizChoiceCatalog.Create(0, "Learning.Group." + name, name, language, false);
                Check(!string.IsNullOrWhiteSpace(choice.Description), "Missing group description");
                var printed = new List<QuizPrintItem>();
                var representative = new Dictionary<(BankQuestionFamily, ArithmeticOperation), QuizPrintItem>();
                foreach (var tier in Enum.GetValues<CurriculumTier>())
                foreach (var op in Enum.GetValues<ArithmeticOperation>())
                {
                    var profile = new QuestionLearningProfile(group);
                    var sources = AppliedQuestionCatalogue.Available(profile, op, tier).ToArray();
                    Check(sources.Length > 0, $"Missing scenes: {group}/{op}/{tier}");
                    foreach (var scene in sources)
                    {
                        var contract = AppliedQuestionCatalogue.Create(profile, op, tier, language, new(71), scene.Id);
                        Verify(contract, AppliedQuestionCatalogue.Draft(contract));
                    }
                    var find = FindXQuestionCatalogue.Available(profile, op, tier).ToArray();
                    Check(find.Length > 0, "Missing Find X: " + group + "/" + op);
                    foreach (var scene in find)
                    {
                        var contract = FindXQuestionCatalogue.Create(profile, op, tier, language, new(29), sceneId: scene.Id);
                        Verify(contract, FindXQuestionCatalogue.Draft(contract));
                    }
                    var fraction = FractionQuestionCatalogue.Create(profile, op, tier, language, new(31));
                    Verify(fraction, FractionQuestionCatalogue.Draft(fraction));
                }
                var sample = representative.Values.ToArray();
                var sheet = new QuizWorksheet("community", name, "", language, CurriculumTier.ThreeStars,
                    ArithmeticQuizMode.Essay, Array.AsReadOnly(sample));
                foreach (bool answers in new[] { false, true })
                {
                    byte[] pdf = QuizPdfWriter.Write(sheet, answers); PdfAssertions.HasEmbeddedText(pdf);
                    string embedded = PdfTestReader.ExtractText(pdf);
                    Check(embedded.Contains(name) && !embedded.Contains("{unit") && !embedded.Contains("{name}"), "PDF lost title or contains unresolved fields");
                    File.WriteAllBytes(Path.Combine(directory, $"{name}-{language}-{answers}.pdf"), pdf); pdfs++;
                }
                foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.FindX, BankQuestionFamily.Fraction })
                {
                    var c = records.Last(r => r.Contract.KnowledgeGroup == group && r.Contract.Language == language && r.Contract.Family == family).Contract;
                    using var template = new MemoryStream();
                    QuestionAuthoringWorkbook.Write(template, c, name, language); template.Position = 0;
                    Check(QuestionBankWorkbook.Read(template).Count == 0, "Example imported without explicit selection");
                    template.Position = 0;
                    using (var zip = new ZipArchive(template, ZipArchiveMode.Update, true))
                    {
                        var entry = zip.GetEntry("xl/worksheets/sheet2.xml")!; XElement xml;
                        using (var input = entry.Open()) xml = XElement.Load(input);
                        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                        xml.Descendants(ns + "c").Single(e => (string?)e.Attribute("r") == "A2").Descendants(ns + "t").Single().Value
                            = language == AppLanguage.Vietnamese ? "Nhập" : "Import";
                        entry.Delete(); using var output = zip.CreateEntry("xl/worksheets/sheet2.xml").Open(); xml.Save(output);
                    }
                    template.Position = 0;
                    var authored = QuestionBankWorkbook.Read(template).Single();
                    Check(authored.Question?.Contract == c && QuestionBankStore.ValidateSavedQuestion(authored.Question!).IsValid, "Authoring template lost contract");
                    File.WriteAllBytes(Path.Combine(directory, $"{name}-{language}-{family}-template.xlsx"), template.ToArray()); templates++;
                }

                void Verify(BasicQuestionContract c, BasicQuestionDraft d)
                {
                    string raw = QuestionBankStore.SerializeDraft(d);
                    var valid = BasicQuestionValidator.Validate(raw, c);
                    Check(c.IsValid && valid.IsValid, $"Invalid {c.SceneId}/{language}/{c.Tier}: {valid.ErrorCode}");
                    new AiGenerationOptions(c.Operation, c.Tier, language, 1, false, new(group), c.Family, c.UnknownRole).Validate();
                    if (OpenQuestionProse.Supports(c))
                    {
                        var open = c with { OpenProse = true };
                        Check(BasicQuestionValidator.Validate(raw, open).IsValid, "Creative AI route rejected the source: " + c.SceneId);
                        string theme = language == AppLanguage.Vietnamese
                            ? name switch { "Toys" => "Đồ chơi", "Healthcare" => "Y tế", _ => "Trường học" } : name;
                        Check(BasicQuestionPrompt.Build(open).Contains(theme, StringComparison.Ordinal), "Creative AI prompt lost theme");
                    }
                    Check(!BasicQuestionValidator.Validate(raw, c with { KnowledgeGroup = QuestionKnowledgeGroup.Food }).IsValid,
                        "A mismatched imported theme was accepted");
                    if (name == "Healthcare")
                    {
                        var unsafeDraft = d with { GivenB = d.GivenB + (language == AppLanguage.Vietnamese
                            ? " Uống thuốc mỗi ngày." : " Take medicine daily.") };
                        Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(unsafeDraft), c).IsValid,
                            "Healthcare stock contract accepted dosing advice");
                        if (OpenQuestionProse.Supports(c))
                            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(unsafeDraft), c with { OpenProse = true }).IsValid,
                                "Creative healthcare stock contract accepted unrelated dosing advice");
                    }
                    string prompt = BasicQuestionPrompt.Build(c);
                    string expectedUnit = c.Family == BankQuestionFamily.Fraction
                        ? language == AppLanguage.Vietnamese ? FractionQuestionCatalogue.Find(c.SceneId)!.ResultUnit
                            : FractionQuestionCatalogue.Find(c.SceneId)!.EnResultUnit : c.Unit;
                    Check(prompt.Contains(expectedUnit, StringComparison.Ordinal), "Prompt lost unit: " + c.SceneId);
                    if (name == "Healthcare") Check(prompt.Contains("stock", StringComparison.OrdinalIgnoreCase)
                        || prompt.Contains("tồn kho", StringComparison.OrdinalIgnoreCase), "Missing healthcare stock guidance");
                    var question = c.ToPracticeQuestion(d.ToWordProblem(c), ArithmeticQuizMode.Essay);
                    var source = c.Family == BankQuestionFamily.FindX ? FindXQuestionCatalogue.Find(c.SceneId)!.SourceScene
                        : c.Family == BankQuestionFamily.Arithmetic ? AppliedQuestionCatalogue.Find(c.SceneId) : null;
                    string? artwork = QuizStoryArtwork.Select(true, false, [question.WordProblem!.AnswerUnit],
                        itemUnit: question.WordProblem.ItemUnit, artworkId: question.WordProblem.StoryArtworkId);
                    if (source?.UnitId == "students") Check(artwork == "quiz_story_student.png", "Existing student PNG not routed");
                    if (source?.UnitId == "marbles") Check(artwork == "quiz_story_marble.png", "Existing marble PNG not routed");
                    if (name == "Healthcare")
                    {
                        string? expected = source?.UnitId switch
                        {
                            "paracetamol-boxes" => "medical_paracetamol_box",
                            "vitamin-c-boxes" => "medical_vitamin_c_box",
                            "cough-syrup-bottles" => "medical_cough_syrup_bottle",
                            "medical-bandage-rolls" => "medical_bandage_roll",
                            "medical-gauze-packs" => "medical_gauze_pack",
                            _ => null
                        };
                        Check(question.WordProblem.StoryArtworkId == expected,
                            "Healthcare contract lost exact authored artwork for " + c.SceneId);
                        Check(artwork == (expected is null ? null : $"quiz_story_{expected}.png"),
                            "Healthcare picture mismatch for " + c.SceneId);
                    }
                    var output = QuizWorksheetBuilder.Format(question, language);
                    Check(!string.IsNullOrWhiteSpace(output.Prompt) && !string.IsNullOrWhiteSpace(output.Answer), "Empty PDF question");
                    printed.Add(output);
                    representative.TryAdd((c.Family, c.Operation), output);
                    foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    {
                        var shown = c.ToPracticeQuestion(d.ToWordProblem(c), mode);
                        Check(!string.IsNullOrWhiteSpace(QuizWorksheetBuilder.Format(shown, language).Answer), "Empty answer in " + mode);
                        if (c.Family != BankQuestionFamily.Fraction)
                            Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(shown).IsValid, "Invalid integer presentation");
                        presentations++;
                    }
                    records.Add(new(c, d, raw, "offline-community", DateTime.UtcNow)); cases++;
                }
            }
        }
        using var backup = new MemoryStream(); QuestionBankWorkbook.WriteBackup(backup, records); backup.Position = 0;
        var read = QuestionBankWorkbook.Read(backup);
        Check(read.Count == records.Count && read.All(r => r.Question is not null), "Excel lost records");
        for (int i = 0; i < read.Count; i++) Check(read[i].Question!.Contract == records[i].Contract
            && read[i].Question!.Draft == records[i].Draft, "Excel changed contract/draft");
        var store = new QuestionBankStore(Path.Combine(directory, "community.db3")); backup.Position = 0;
        var imported = await store.ImportExcelAsync(backup);
        Check(imported.Rejected == 0 && imported.Inserted > 0, "SQLite rejected community groups");
        using var exported = new MemoryStream(); await store.ExportExcelAsync(exported); exported.Position = 0;
        var again = QuestionBankWorkbook.Read(exported);
        Check(again.Count == imported.Inserted && again.All(r => r.Question is not null), "SQLite re-export lost questions");
        File.WriteAllBytes(Path.Combine(directory, "community-backup.xlsx"), backup.ToArray());
        Console.WriteLine($"PASS community themes: {cases} VI/EN scene/tier/operation/family contracts, {presentations} presentations, {pdfs} PDFs, {templates} authoring templates, Excel/SQLite roundtrips.");
    }
}
