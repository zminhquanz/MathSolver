using System.Globalization;
using System.Text.Json;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;

internal static class PracticeNumberRangesTests
{
    internal static async Task Run(string directory)
    {
        string unusedBank = Path.Combine(directory, "expanded-shopping-empty.db3");
        var emptyStore = new QuestionBankStore(unusedBank);
        var provider = new BasicPracticeQuestionProvider(emptyStore, new Random(24));
        var factory = new PracticeQuestionFactory(provider);
        var validator = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var items = new List<QuizPrintItem>();
            for (int stars = 4; stars <= 5; stars++)
            foreach (var operation in Enum.GetValues<ArithmeticOperation>())
            {
                var request = new QuizProblemRequest(QuizProblemKind.Arithmetic, operation);
                var question = await factory.CreateAsync(request, mode, new((CurriculumTier)stars, false), language,
                    new(QuestionKnowledgeGroup.Money), PracticeQuestionFormat.WordProblem, false);
                Check(question.WordProblem?.PracticeDetails is not null, "Shopping PDF bypassed the reviewed item catalogue.");
                Check(validator.Validate(question).IsValid, "Factory produced incorrect shopping mathematics.");
                var printed = QuizWorksheetBuilder.Format(question, language);
                Check(printed.Prompt.Contains(question.WordProblem!.ProblemText), "PDF changed price-bound prose.");
                foreach (var purchase in question.WordProblem.PracticeDetails!.Purchases)
                {
                    var item = QuizContentCatalog.LoadList<PracticeMoneyItem>("PracticeMoneyItems", QuizContentCatalog.Culture(language)).Single(i => i.Id == purchase.ItemId);
                    Check(printed.Prompt.Contains(item.Name) || item.PluralName.Length > 0 && printed.Prompt.Contains(item.PluralName), "PDF omitted the purchased item.");
                }
                Check(!printed.Solution.Contains('{'), "Unresolved PDF variables.");
                foreach (var step in question.WordProblem.PracticeDetails.Steps)
                    Check(printed.Solution.Contains(step.Answer.ToString("N0", CultureInfo.GetCultureInfo(QuizContentCatalog.Culture(language)))),
                        "PDF omitted a C# calculation step.");
                items.Add(printed);
                var numeric = await factory.CreateAsync(request, mode, new((CurriculumTier)stars, false), language,
                    null, PracticeQuestionFormat.Numeric, false);
                Check(numeric.WordProblem is null && numeric.Expression.LeftOperand <= PracticeArithmeticDifficulty.Maximum((CurriculumTier)stars, numeric.ArithmeticStyle),
                    "Numeric PDF acquired a story or escaped its range.");
                items.Add(QuizWorksheetBuilder.Format(numeric, language));
            }
            // Deterministic million-sized fixture, through the same practice renderer as the UI.
            var large = new PracticeArithmeticStoryGenerator(new Random(14)).Generate(ArithmeticOperation.Multiply,
                CurriculumTier.FiveStars, language, QuestionKnowledgeGroup.Production, mode);
            items.Add(QuizWorksheetBuilder.Format(large, language));
            foreach (var style in Enum.GetValues<PracticeArithmeticStyle>())
            foreach (var operation in Enum.GetValues<ArithmeticOperation>())
            {
                var q = new ArithmeticQuizGenerator(new BasicArithmeticEngine(), new Random(31)).Generate(mode, operation,
                    new(CurriculumTier.FourStars, false), expandedPractice: true, requestedStyle: style);
                var printed = QuizWorksheetBuilder.Format(q, language);
                Check(printed.Prompt.StartsWith(PracticeArithmeticDifficulty.Prompt(style, mode, language), StringComparison.Ordinal),
                    "PDF lost the mental/written instruction used by the practice UI.");
                items.Add(printed);
            }
            foreach (var (group, id) in new[] { (QuestionKnowledgeGroup.Data, "population-statistics"),
                (QuestionKnowledgeGroup.Production, "annual-paper-production") })
            {
                var q = Enumerable.Range(0, 400).Select(seed => new PracticeArithmeticStoryGenerator(new Random(seed))
                    .Generate(ArithmeticOperation.Add, CurriculumTier.FourStars, language, group, mode))
                    .First(q => q.WordProblem!.PracticeDetails!.ContextId == id && q.CorrectAnswer >= 100_000_000);
                var printed = QuizWorksheetBuilder.Format(q, language);
                Check(printed.Prompt == q.WordProblem!.ProblemText || mode == ArithmeticQuizMode.TrueFalse
                    && printed.Prompt.StartsWith(q.WordProblem!.ProblemText, StringComparison.Ordinal), "PDF changed the statistical story.");
                items.Add(printed);
            }
            var worksheet = new QuizWorksheet("PRACTICE-RANGES", "Practice ranges", "Reviewed item prices", language,
                CurriculumTier.FiveStars, mode, items.AsReadOnly());
            string snapshot = JsonSerializer.Serialize(worksheet);
            foreach (bool answers in new[] { false, true })
            {
                byte[] pdf = QuizPdfWriter.Write(worksheet, answers);
                PdfAssertions.HasEmbeddedText(pdf);
                string text = PdfTestReader.ExtractText(pdf);
                Check(text.Contains("Practice ranges"), "PDF title could not be read.");
                if (!answers)
                {
                    foreach (var style in Enum.GetValues<PracticeArithmeticStyle>())
                        Check(Compact(text).Contains(Compact(PracticeArithmeticDifficulty.Prompt(style, mode, language))),
                            "Rendered PDF omitted a mental/written skill heading.");
                    foreach (var id in new[] { "population-statistics", "annual-paper-production" })
                    {
                        var story = items.First(i => i.Source.WordProblem?.PracticeDetails?.ContextId == id);
                        Check(Compact(text).Contains(Compact(story.Source.WordProblem!.SubjectName)), "Rendered PDF lost the statistical context.");
                    }
                }
                if (!answers)
                    Check(items[0].Source.WordProblem!.PracticeDetails!.Purchases.All(p => {
                        var item = QuizContentCatalog.LoadList<PracticeMoneyItem>("PracticeMoneyItems", QuizContentCatalog.Culture(language)).Single(i => i.Id == p.ItemId);
                        return Compact(text).Contains(Compact(item.Name)) || item.PluralName.Length > 0 && Compact(text).Contains(Compact(item.PluralName)); }),
                        $"Item names missing from the rendered PDF: {language}/{mode}.");
                else
                    foreach (var step in items[0].Source.WordProblem!.PracticeDetails!.Steps)
                        Check(Compact(text).Contains(Compact(step.Answer.ToString("N0", CultureInfo.GetCultureInfo(QuizContentCatalog.Culture(language))))),
                            "Rendered PDF omitted a calculated answer.");
                File.WriteAllBytes(Path.Combine(directory, $"practice-ranges-{language}-{mode}-{(answers ? "answers" : "worksheet")}.pdf"), pdf);
            }
            Check(snapshot == JsonSerializer.Serialize(worksheet), "PDF regenerated or changed frozen facts.");
        }
        var bankRows = await emptyStore.QueryAsync("SELECT Hash FROM BasicQuestionBank;");
        Check(bankRows.Rows.Count == 0, "Fallback practice inserted generated facts into SQLite.");
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var read = Enumerable.Range(0, 1000).Select(seed => new ElementaryQuizGenerator(new Random(seed), expandedNumberPractice: true)
                .Generate(ArithmeticQuizMode.Essay, QuizProblemKind.NumberSense, ElementaryQuizType.ReadNumber, language, CurriculumTier.FiveStars))
                .First(q => q.ElementaryProblem!.ProblemText.Contains("999999999"));
            var shopping = Enumerable.Range(0, 200).Select(seed => new PracticeArithmeticStoryGenerator(new Random(seed))
                .Generate(ArithmeticOperation.Multiply, CurriculumTier.FiveStars, language, QuestionKnowledgeGroup.Money, ArithmeticQuizMode.Essay))
                .First(q => q.WordProblem!.PracticeDetails!.Purchases[0].ItemId == "school-laptop" && q.WordProblem.PracticeDetails.Purchases[0].Count >= 10);
            var questions = Array.AsReadOnly(new[] { QuizWorksheetBuilder.Format(read, language), QuizWorksheetBuilder.Format(shopping, language) });
            var worksheet = new QuizWorksheet("NINE-DIGITS", "Nine-digit reading and procurement", "C# facts", language,
                CurriculumTier.FiveStars, ArithmeticQuizMode.Essay, questions);
            foreach (bool answers in new[] { false, true })
            {
                byte[] pdf = QuizPdfWriter.Write(worksheet, answers);
                PdfAssertions.HasEmbeddedText(pdf);
                string text = Compact(PdfTestReader.ExtractText(pdf));
                Check(text.Contains(Compact(answers ? read.ElementaryProblem!.Answers[0].Text! : "999999999")), "Nine-digit reading missing from PDF.");
                Check(text.Contains(Compact(answers ? shopping.CorrectAnswer.ToString(CultureInfo.InvariantCulture) : shopping.WordProblem!.PracticeDetails!.Purchases[0].Count.ToString())),
                    "Bulk-purchase fact or result missing from PDF.");
                File.WriteAllBytes(Path.Combine(directory, $"nine-digits-{language}-{(answers ? "answers" : "worksheet")}.pdf"), pdf);
            }
        }
        Console.WriteLine("PASS practice/PDF ranges, reviewed prices, intermediate calculations, frozen facts and AI bank isolation.");
    }

    private static string Compact(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
}
