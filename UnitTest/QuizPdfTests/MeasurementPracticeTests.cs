using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;

internal static class MeasurementPracticeTests
{
    internal static async Task Run(string directory)
    {
        string path = Path.Combine(directory, "numeric-conversions-unopened.db3");
        Check(!File.Exists(path), "Use a fresh output directory for the no-SQLite check.");
        var provider = new BasicPracticeQuestionProvider(new QuestionBankStore(path), new AlwaysBank());
        var factory = new PracticeQuestionFactory(provider);
        var catalog = new QuizProblemTypeCatalog();
        int index = catalog.Options.ToList().FindIndex(o => o.FixedRequest?.Kind == QuizProblemKind.Measurement);
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.Measurement).Where(t => ElementaryQuizGenerator.SupportsMeasurementPractice(t)))
        {
            var request = new QuizProblemRequest(QuizProblemKind.Measurement, ElementaryType: type);
            var question = await factory.CreateAsync(request, mode, new(tier, false), language, null,
                PracticeQuestionFormat.Numeric, false);
            Check(question.ElementaryProblem!.IsNumericMeasurementConversion, "Factory ignored numeric measurement format.");
            Check(ReferenceEquals(question, await provider.SelectReasoningAsync(question, tier, language)),
                "Provider replaced numeric conversion with prose.");
            var item = QuizWorksheetBuilder.Format(question, language);
            Check(item.Prompt.Contains(question.ElementaryProblem.ProblemText), "PDF changed the conversion prompt.");
            if (mode == ArithmeticQuizMode.TrueFalse)
                Check(item.Prompt.Contains(question.ElementaryProblem.FormatMeasurementConversion(question.ElementaryProblem.PresentedText!)),
                    "PDF assertion omitted conversion units or operands.");
            var settings = new QuizWorksheetSettings(index, request, tier, language);
            Check(settings.CanChooseFormat && !settings.CanChooseGroup && !settings.UsesWords
                && settings.FormatPrefix == "MeasurementPractice", "Incorrect PDF measurement settings.");
            var printedQuestion = await settings.GenerateAsync(factory, catalog);
            Check(printedQuestion.ElementaryProblem!.IsNumericMeasurementConversion, "Worksheet ignored numeric format.");
            Check(settings.SelectWords(true).UsesWords && !settings.SelectWords(true).CanChooseGroup,
                "Measurement word-problem picker acquired an unrelated knowledge-group picker.");
            count++;
        }
        Check(!File.Exists(path), "Numeric conversions opened or wrote a SQLite database.");
        foreach (var type in new[] { ElementaryQuizType.ReadRuler, ElementaryQuizType.MapScale, ElementaryQuizType.MixedLength })
            Check(!new QuizWorksheetSettings(index, new(QuizProblemKind.Measurement, ElementaryType: type),
                CurriculumTier.OneStar, AppLanguage.Vietnamese).CanChooseFormat, "Unrelated measurement type gained a practice picker.");
        Console.WriteLine($"PASS {count} numeric conversion factory/PDF cases; no SQLite access even with bank selection forced.");
    }

    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => maxValue - 1; }
}
