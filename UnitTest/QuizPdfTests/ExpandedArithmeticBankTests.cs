using System.Text.Json;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class ExpandedArithmeticBankTests
{
    internal static async Task Run(string directory)
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var c = ExpandedArithmeticCatalogue.Create(new(QuestionKnowledgeGroup.Money), ArithmeticOperation.Add,
                CurriculumTier.FiveStars, language, new(39), "school-laptop");
            var draft = ExpandedArithmeticCatalogue.Drafts(c)[1];
            var store = new QuestionBankStore(Path.Combine(directory, "expanded-stored-" + language + ".db3"));
            TestAssert.That(await store.InsertAsync(new(c, draft, QuestionBankStore.SerializeDraft(draft), "reviewed-fixture", DateTime.UtcNow)),
                "Could not insert the expanded template for PDF.");
            var factory = new PracticeQuestionFactory(new(store, new BankSource()));
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            {
                var q = await factory.CreateAsync(new(QuizProblemKind.Arithmetic, ArithmeticOperation.Add), mode,
                    new(CurriculumTier.FiveStars, false), language, new(QuestionKnowledgeGroup.Money), PracticeQuestionFormat.WordProblem, false);
                TestAssert.That(q.WordProblem!.ProblemText.Contains(language == AppLanguage.Vietnamese ? "Ngày đầu mua" : "The first purchase"),
                    "PDF factory bypassed the saved expanded prose.");
                var item = QuizWorksheetBuilder.Format(q, language);
                var second = await factory.CreateAsync(new(QuizProblemKind.Arithmetic, ArithmeticOperation.Add), mode,
                    new(CurriculumTier.FiveStars, false), language, new(QuestionKnowledgeGroup.Money), PracticeQuestionFormat.WordProblem, false);
                var worksheet = new QuizWorksheet("STORED-EXPANDED", "Stored expanded template", "Bank replay", language,
                    CurriculumTier.FiveStars, mode, Array.AsReadOnly(new[] { item, QuizWorksheetBuilder.Format(second, language) }));
                string before = JsonSerializer.Serialize(worksheet);
                foreach (bool answers in new[] { false, true })
                {
                    byte[] pdf = QuizPdfWriter.Write(worksheet, answers);
                    PdfAssertions.HasEmbeddedText(pdf);
                    var text = PdfTestReader.ExtractText(pdf);
                    TestAssert.That(text.Contains("Stored expanded template"), "Stored template PDF could not be read.");
                    File.WriteAllBytes(Path.Combine(directory, $"stored-expanded-{language}-{mode}-{answers}.pdf"), pdf);
                }
                TestAssert.That(before == JsonSerializer.Serialize(worksheet), "Printing changed the saved/fresh bank facts.");
            }
        }
        Console.WriteLine("PASS stored expanded templates through the shared practice/PDF factory, all modes, embedded text and frozen facts.");
    }

    private sealed class BankSource : Random { public override int Next(int maxValue) => maxValue - 1; }
}
