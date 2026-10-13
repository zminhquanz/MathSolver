using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using PdfSharp.Pdf.IO;

internal static class TimeFractionWorksheetTests
{
    internal static async Task Run(PracticeQuestionFactory factory, string directory)
    {
        ElementaryQuizType[] types = [ElementaryQuizType.SecondsConversion, ElementaryQuizType.YearCentury,
            ElementaryQuizType.CalendarWeekday, ElementaryQuizType.OrderRelatedFractions];
        var catalogue = new QuizProblemTypeCatalog(); int cases = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            int next = 0;
            var sheet = await QuizWorksheetBuilder.CreateAsync(20,
                language == AppLanguage.Vietnamese ? "Thời gian và sắp xếp phân số khác mẫu" : "Time and fraction ordering",
                "Seconds · Centuries · Weekdays · Fractions", language, CurriculumTier.ThreeStars, ArithmeticQuizMode.Essay, token =>
                {
                    var type = types[next++ % types.Length];
                    return factory.CreateAsync(new(type == ElementaryQuizType.OrderRelatedFractions ? QuizProblemKind.FractionSkills : QuizProblemKind.Time,
                        ElementaryType: type), ArithmeticQuizMode.Essay, new(CurriculumTier.ThreeStars, false), language,
                        null, PracticeQuestionFormat.Numeric, false, token);
                });
            Check(sheet.Questions.Select(q => q.Source.ElementaryProblem!.Type).Distinct().Count() == 4, "PDF missed an extension skill.");
            string frozen = JsonSerializer.Serialize(sheet);
            foreach (bool answers in new[] { false, true })
            {
                var bytes = QuizPdfWriter.Write(sheet, answers);
                File.WriteAllBytes(Path.Combine(directory, $"time-fraction-{language}-{(answers ? "answers" : "worksheet")}.pdf"), bytes);
                using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
                Check(pdf.PageCount > 1 && pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>().All(p => Math.Abs(p.Width.Point - 595.28) < 1 && Math.Abs(p.Height.Point - 841.89) < 1), "Extension PDF pagination/A4 failed.");
                PdfAssertions.HasEmbeddedText(bytes);
                string text = PdfTestReader.ExtractText(bytes);
                Check(text.Contains(sheet.Title), "Extension PDF lost its Unicode title.");
                if (answers) Check(text.Contains(language == AppLanguage.Vietnamese ? "Quy đồng" : "common denominator"), "Fraction answer PDF omitted its common-denominator explanation.");
                else Check(!text.Contains(language == AppLanguage.Vietnamese ? "Quy đồng" : "common denominator"), "Fraction solution leaked into student PDF.");
            }
            Check(JsonSerializer.Serialize(sheet) == frozen, "PDF changed the frozen extension questions.");
            foreach (var type in types)
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            {
                var kind = type == ElementaryQuizType.OrderRelatedFractions ? QuizProblemKind.FractionSkills : QuizProblemKind.Time;
                int index = catalogue.Options.ToList().FindIndex(o => o.FixedRequest?.Kind == kind);
                Check(QuizWorksheetChoices.Subtypes(kind).Any(s => s.Request.ElementaryType == type), "Missing PDF subtype picker.");
                var settings = new QuizWorksheetSettings(index, new(kind, ElementaryType: type), tier, language);
                var q = await settings.GenerateAsync(factory, catalogue);
                Check(q.ElementaryProblem!.Type == type && q.Mode == ArithmeticQuizMode.Essay, "PDF selection changed the requested subtype.");
                cases++;
            }
        }
        Console.WriteLine($"PASS {cases} time/fraction PDF selections; bilingual frozen 20-question sets, A4 pages, embedded fonts and separate common-denominator solutions.");
    }
}
