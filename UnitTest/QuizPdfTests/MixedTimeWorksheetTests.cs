using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using PdfSharp.Pdf.IO;

internal static class MixedTimeWorksheetTests
{
    internal static async Task Run(PracticeQuestionFactory factory, string directory)
    {
        var types = ElementaryQuizGenerator.MixedTimeTypes;
        var catalogue = new QuizProblemTypeCatalog();
        int selections = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            int next = 0;
            var sheet = await QuizWorksheetBuilder.CreateAsync(20,
                language == AppLanguage.Vietnamese ? "Thời gian hỗn hợp — cộng, trừ, nhân, chia" : "Mixed time — addition, subtraction, multiplication, division",
                language == AppLanguage.Vietnamese ? "Giờ · Phút · Giây · 3 sao" : "Hours · Minutes · Seconds · 3 stars",
                language, CurriculumTier.ThreeStars, ArithmeticQuizMode.Essay, token =>
                {
                    int index = next++;
                    token.ThrowIfCancellationRequested();
                    var type = types[index % types.Length];
                    return Task.FromResult(new ElementaryQuizGenerator(new Random(713 + index)).Generate(
                        ArithmeticQuizMode.Essay, QuizProblemKind.Time, type, language, CurriculumTier.ThreeStars));
                });
            Check(sheet.Questions.Select(q => q.Source.ElementaryProblem!.Type).Distinct().Count() == 4, "Mixed time PDF missed an operation.");
            foreach (var item in sheet.Questions)
            {
                Check(item.Solution == item.Source.ElementaryProblem!.Reasoning!.Explanation, "Mixed time PDF lost its unit conversion steps.");
                Check(!item.Solution.Contains("Đáp số:") && !item.Solution.Contains("Answer:"), "Mixed time PDF repeats its final answer.");
                Check(item.Visual is null && item.Diagram is null && item.Table is null, "Unexpected mixed time drawing or duplicated facts.");
            }
            string frozen = JsonSerializer.Serialize(sheet);
            foreach (bool answers in new[] { false, true })
            {
                var bytes = QuizPdfWriter.Write(sheet, answers);
                File.WriteAllBytes(Path.Combine(directory, $"mixed-time-{language}-{(answers ? "answers" : "worksheet")}.pdf"), bytes);
                using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
                Check(pdf.PageCount > 1 && pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>().All(p => Math.Abs(p.Width.Point - 595.28) < 1 && Math.Abs(p.Height.Point - 841.89) < 1), "Mixed time PDF pagination/A4 failed.");
                PdfAssertions.HasEmbeddedText(bytes);
                string text = PdfTestReader.ExtractText(bytes);
                Check(text.Contains(sheet.Title), "Mixed time PDF lost its title.");
                if (answers)
                {
                    Check(!text.Contains(language == AppLanguage.Vietnamese ? "Đáp án:" : "Answer:"), "Mixed time PDF adds a duplicate answer label.");
                    foreach (var item in sheet.Questions)
                        Check(text.Contains(item.Answer), "Mixed time answer PDF lost its mixed-unit result.");
                    var pages = new List<string>();
                    foreach (var page in pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>())
                    {
                        using var one = new PdfSharp.Pdf.PdfDocument();
                        one.AddPage(page);
                        using var buffer = new MemoryStream();
                        one.Save(buffer, false);
                        pages.Add(PdfTestReader.ExtractText(buffer.ToArray()));
                    }
                    string Compact(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
                    for (int i = 0; i < sheet.Questions.Count; i++)
                    {
                        string number = (language == AppLanguage.Vietnamese ? "Câu " : "Question ") + (i + 1) + ".";
                        Check(pages.Any(page => page.Contains(number) && Compact(page).Contains(Compact(sheet.Questions[i].Solution))),
                            "A mixed time question number and its worked solution were split between PDF pages.");
                    }
                }
                else
                {
                    Check(!text.Contains("÷ 3600") && !text.Contains("× 3600"), "Mixed time calculation steps leaked into the worksheet.");
                    foreach (var item in sheet.Questions)
                        Check(text.Contains(item.Prompt), "Mixed time worksheet changed a question.");
                }
            }
            Check(JsonSerializer.Serialize(sheet) == frozen, "Mixed time PDF mutated the frozen questions.");
            foreach (var type in types)
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            {
                int index = catalogue.Options.ToList().FindIndex(o => o.FixedRequest?.Kind == QuizProblemKind.Time);
                Check(QuizWorksheetChoices.Subtypes(QuizProblemKind.Time).Any(s => s.Request.ElementaryType == type), "Missing mixed time PDF subtype picker.");
                var settings = new QuizWorksheetSettings(index, new(QuizProblemKind.Time, ElementaryType: type), tier, language);
                var q = await settings.GenerateAsync(factory, catalogue);
                Check(q.ElementaryProblem!.Type == type && q.ElementaryProblem.Reasoning!.Tier == tier && q.Mode == ArithmeticQuizMode.Essay, "PDF ignored mixed time settings.");
                selections++;
            }
        }
        Console.WriteLine($"PASS {selections} mixed time PDF selections; bilingual frozen 20-question sets, A4 pages, embedded text and mixed-unit solutions.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
