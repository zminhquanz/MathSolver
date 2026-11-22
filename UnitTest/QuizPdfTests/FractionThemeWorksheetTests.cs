using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;

internal static class FractionThemeWorksheetTests
{
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    internal static async Task Run(string directory)
    {
        var subjects = FractionThemeCatalogue.Subjects;
        var scenes = FractionQuestionCatalogue.All
            .Where(scene => FractionThemeCatalogue.FindScene(scene.Id) is not null)
            .ToArray();
        Check(subjects.Count == 34 && scenes.Length == subjects.Count * 4,
            "Some fraction material subjects or operations are missing.");

        var records = new List<ValidatedBankQuestion>();
        int renderedQuestions = 0;
        int exportedPdfs = 0;
        foreach (var group in subjects.Select(subject => subject.Group).Distinct())
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var items = new List<QuizPrintItem>();
            var includedScenes = scenes.Where(scene => scene.Group == group).ToArray();
            foreach (var scene in includedScenes)
            {
                var contract = FractionQuestionCatalogue.Create(new(group), scene.Operation,
                    CurriculumTier.ThreeStars, language, new Random(41), scene.Id);
                var draft = FractionQuestionCatalogue.Draft(contract);
                var validated = BasicQuestionValidator.Validate(
                    QuestionBankStore.SerializeDraft(draft), contract);
                Check(validated.IsValid, "Invalid reviewed fraction material: " + scene.Id);

                var word = draft.ToWordProblem(contract);
                var artworkId = QuizStoryArtworkCatalogue.FractionArtwork(scene.Id);
                var picture = QuizStoryArtwork.Select(true, false, [word.AnswerUnit],
                    itemUnit: word.ItemUnit, artworkId: word.StoryArtworkId);
                Check(!string.IsNullOrEmpty(artworkId) && word.StoryArtworkId == artworkId
                    && picture == $"quiz_story_{artworkId}.png",
                    "Fraction material lost its authored image: " + scene.Id);

                var question = contract.ToPracticeQuestion(word, ArithmeticQuizMode.Essay);
                var printed = QuizWorksheetBuilder.Format(question, language);
                Check(!string.IsNullOrWhiteSpace(printed.Prompt)
                    && !string.IsNullOrWhiteSpace(printed.Answer)
                    && !printed.Prompt.Contains("{unit", StringComparison.Ordinal)
                    && !printed.Prompt.Contains("{name", StringComparison.Ordinal),
                    "Invalid fraction material worksheet: " + scene.Id);

                items.Add(printed);
                records.Add(new(contract, draft, QuestionBankStore.SerializeDraft(draft),
                    "offline-fraction-material", DateTime.UtcNow));
                renderedQuestions++;
            }

            Check(items.Count == includedScenes.Length, "Fraction worksheet lost a scene.");
            var worksheet = new QuizWorksheet("FRACTION-MATERIAL", group.ToString(), "",
                language, CurriculumTier.ThreeStars, ArithmeticQuizMode.Essay,
                items.AsReadOnly());
            foreach (bool showAnswers in new[] { false, true })
            {
                byte[] bytes = QuizPdfWriter.Write(worksheet, showAnswers);
                PdfAssertions.HasEmbeddedText(bytes);
                string embedded = PdfTestReader.ExtractText(bytes);
                Check(embedded.Contains(group.ToString(), StringComparison.Ordinal)
                    && !embedded.Contains("{unit", StringComparison.Ordinal)
                    && !embedded.Contains("{name", StringComparison.Ordinal),
                    "Fraction PDF lost content: " + group);
                string fileName = $"fraction-material-{group}-{language}-{(showAnswers ? "answers" : "questions")}.pdf";
                File.WriteAllBytes(Path.Combine(directory, fileName), bytes);
                exportedPdfs++;
            }
        }

        var bank = new QuestionBankStore(Path.Combine(directory, "fraction-material.db3"));
        foreach (var record in records)
            Check(await bank.InsertAsync(record), "Cannot save a fraction material: " + record.Contract.SceneId);

        using var workbook = new MemoryStream();
        await bank.ExportExcelAsync(workbook);
        workbook.Position = 0;
        var exported = QuestionBankWorkbook.Read(workbook);
        Check(exported.Count == records.Count && exported.All(row => row.Question?.Contract.IsValid == true)
            && exported.Select(row => row.Question!.Contract.SceneId).Distinct().Count() == scenes.Length,
            "Fraction Excel export lost scenes, mathematical facts or language variants.");
        File.WriteAllBytes(Path.Combine(directory, "fraction-material.xlsx"), workbook.ToArray());

        var importedBank = new QuestionBankStore(Path.Combine(directory, "fraction-material-import.db3"));
        workbook.Position = 0;
        var imported = await importedBank.ImportExcelAsync(workbook);
        Check(imported.Inserted == records.Count && imported.Rejected == 0,
            "Fraction Excel import failed to round-trip all new stories.");

        Console.WriteLine($"PASS {renderedQuestions} bilingual fraction material PDF questions, " +
            $"{exportedPdfs} worksheets/answer keys, {records.Count} Excel records round-tripped.");
    }
}
