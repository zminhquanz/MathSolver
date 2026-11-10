using System.Text.Json;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;

internal static class MoneyItemWorksheetTests
{
    private static readonly (string Id, string ArtworkId)[] Items =
    [
        ("ballpoint-pen", "pen"),
        ("cooking-oil", "oil"),
        ("school-backpack", "backpack"),
        ("child-bicycle", "bicycle"),
        ("refrigerator", "refrigerator"),
        ("mobile-phone", "mobile_phone")
    ];

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);
        var validator = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        int generated = 0, aiDrafts = 0, pdfs = 0, importedRows = 0;

        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var money = QuizContentCatalog.LoadList<PracticeMoneyItem>("PracticeMoneyItems", QuizContentCatalog.Culture(language));
            var records = new List<ValidatedBankQuestion>();
            foreach (var (id, artwork) in Items)
            {
                var item = money.Single(i => i.Id == id);
                Check(item.ArtworkId == artwork && item.MinimumStars is 4 or 5,
                    $"Invalid money item metadata: {language}/{id}");
                PracticeArithmeticStoryGenerator.Validate(item);
                Check(QuizStoryArtwork.Select(true, false, [], artworkId: artwork) == $"quiz_story_{artwork}.png",
                    $"Missing artwork registration: {id}");

                foreach (var tier in new[] { CurriculumTier.FourStars, CurriculumTier.FiveStars }
                    .Where(t => (int)t >= item.MinimumStars))
                foreach (var operation in Enum.GetValues<ArithmeticOperation>())
                {
                    Check(ExpandedArithmeticCatalogue.Contexts(QuestionKnowledgeGroup.Money, tier, language).Contains(id),
                        $"Missing AI context: {language}/{tier}/{id}");

                    var contract = ExpandedArithmeticCatalogue.Create(new(QuestionKnowledgeGroup.Money), operation, tier,
                        language, new Random(341 + (int)tier * 100 + (int)operation), id);
                    Check(ExpandedArithmeticCatalogue.IsValid(contract) && contract.SceneId == id,
                        $"Invalid AI facts: {language}/{tier}/{operation}/{id}");
                    var draft = ExpandedArithmeticCatalogue.Draft(contract);
                    string raw = QuestionBankStore.SerializeDraft(draft);
                    Check(ExpandedArithmeticCatalogue.Validate(raw, contract).IsValid,
                        $"AI draft rejected: {language}/{tier}/{operation}/{id}");
                    records.Add(new(contract, draft, raw, "Reviewed money item", DateTime.UtcNow));
                    aiDrafts++;

                    foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    {
                        // Exercise the C# List path independently of the bank, with repeatable seeds.
                        var question = new PracticeArithmeticStoryGenerator(new Random(387 + (int)tier * 100
                            + (int)operation * 10 + (int)mode)).Generate(operation, tier, language,
                            QuestionKnowledgeGroup.Money, mode, id);
                        Check(validator.Validate(question).IsValid && question.WordProblem is not null,
                            $"Invalid C# question: {language}/{tier}/{operation}/{mode}/{id}");
                        var word = question.WordProblem!;
                        Check(word.StoryArtworkId == artwork && word.PracticeDetails?.ContextId == id
                            && word.PracticeDetails.Purchases.Count > 0
                            && word.PracticeDetails.Purchases.All(p => p.ItemId == id && p.ItemName == item.Name),
                            $"Incorrect money List identity: {language}/{tier}/{operation}/{mode}/{id}");
                        Check(QuizStoryArtworkCatalogue.PurchasedItemArtwork(word, language) == artwork
                            && QuizStoryArtworkCatalogue.SourceArtwork(question, language) == artwork,
                            $"Question UI cannot select purchased artwork: {language}/{tier}/{operation}/{mode}/{id}");
                        var printed = QuizWorksheetBuilder.Format(question, language);
                        Check(printed.Prompt.Contains(item.Name, StringComparison.Ordinal)
                            || item.PluralName.Length > 0 && printed.Prompt.Contains(item.PluralName, StringComparison.Ordinal),
                            $"Missing item in exported question: {language}/{id}/{mode}");
                        Check(!string.IsNullOrWhiteSpace(printed.Answer) && !printed.Solution.Contains('{'),
                            $"Unresolved worksheet facts: {language}/{id}/{mode}");
                        generated++;
                    }
                }
            }

            // A changed item name cannot silently reuse a valid picture.
            var original = new PracticeArithmeticStoryGenerator(new Random(29)).Generate(ArithmeticOperation.Add,
                CurriculumTier.FourStars, language, QuestionKnowledgeGroup.Money, ArithmeticQuizMode.Essay, "refrigerator");
            var details = original.WordProblem!.PracticeDetails!;
            var tampered = original.WordProblem with
            {
                PracticeDetails = details with
                {
                    Purchases = details.Purchases.Select(p => p with { ItemName = "wrong item" }).ToArray()
                }
            };
            Check(QuizStoryArtworkCatalogue.PurchasedItemArtwork(tampered, language) is null,
                $"Purchased illustration accepted altered prose: {language}");

            foreach (var tier in new[] { CurriculumTier.FourStars, CurriculumTier.FiveStars })
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            {
                var questions = records.Where(r => r.Contract.Tier == tier)
                    .Select(r => r.Contract.ToPracticeQuestion(r.Draft.ToWordProblem(r.Contract), mode, new Random(91)))
                    .ToArray();
                var print = questions.Select(q => QuizWorksheetBuilder.Format(q, language)).ToArray();
                var sheet = new QuizWorksheet("MONEY-SIX", "Money and transactions", "Reviewed item purchases",
                    language, tier, mode, print);
                string snapshot = JsonSerializer.Serialize(sheet);
                foreach (bool withAnswers in new[] { false, true })
                {
                    byte[] pdf = QuizPdfWriter.Write(sheet, withAnswers);
                    PdfAssertions.HasEmbeddedText(pdf);
                    string text = PdfTestReader.ExtractText(pdf);
                    Check(text.Contains("Money and transactions", StringComparison.Ordinal)
                        && !text.Contains("{item", StringComparison.Ordinal),
                        $"Incorrect PDF text: {language}/{tier}/{mode}");
                    await File.WriteAllBytesAsync(Path.Combine(output,
                        $"money-six-{language}-{tier}-{mode}-{(withAnswers ? "answers" : "worksheet")}.pdf"), pdf);
                    pdfs++;
                }
                Check(JsonSerializer.Serialize(sheet) == snapshot, "PDF mutated the questions");
            }

            using var backup = new MemoryStream();
            QuestionBankWorkbook.WriteBackup(backup, records);
            string culture = QuizContentCatalog.Culture(language);
            await File.WriteAllBytesAsync(Path.Combine(output, $"money-six-{culture}-backup.xlsx"), backup.ToArray());
            backup.Position = 0;
            var roundTrip = QuestionBankWorkbook.Read(backup);
            Check(roundTrip.Count == records.Count && roundTrip.All(r => r.Question is not null),
                $"Excel backup lost money records: {language}");
            for (int i = 0; i < records.Count; i++)
                Check(roundTrip[i].Question!.Contract == records[i].Contract,
                    $"Excel changed money facts: {language}/{i}");

            var store = new QuestionBankStore(Path.Combine(output, $"money-six-{culture}.db3"));
            backup.Position = 0;
            var imported = await store.ImportExcelAsync(backup);
            int distinct = records.Select(r => QuestionProseIdentity.Hash(r.Contract, r.Draft))
                .Distinct(StringComparer.Ordinal).Count();
            Check(imported.Inserted == distinct && imported.Duplicates == records.Count - distinct && imported.Rejected == 0,
                $"Excel import failed: {language}/{JsonSerializer.Serialize(imported)}");
            importedRows += distinct;

            using var exported = new MemoryStream();
            await store.ExportExcelAsync(exported);
            exported.Position = 0;
            var saved = QuestionBankWorkbook.Read(exported);
            Check(saved.Count == distinct && saved.All(row => row.Question is not null)
                && saved.All(row => Items.Any(i => i.Id == row.Question!.Contract.SceneId)),
                $"Excel export lost product identity: {language}");
        }
        Console.WriteLine($"PASS six money items: {generated} C# cases, {aiDrafts} AI templates, "
            + $"{pdfs} PDFs, {importedRows} Excel/SQLite round-trip records (VI/EN).");
    }
}
