using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class WorksheetSettingsTests
{
    internal static async Task Run(PracticeQuestionFactory factory)
    {
        var catalog = new QuizProblemTypeCatalog(new Random(190));
        var practice = new QuizWorksheetSettings(1, new(QuizProblemKind.Arithmetic, ArithmeticOperation.Add),
            CurriculumTier.OneStar, AppLanguage.Vietnamese);
        string unchanged = JsonSerializer.Serialize(practice);
        var local = practice.SelectWords(true) with { Group = QuestionKnowledgeGroup.Money, Tier = CurriculumTier.FiveStars };
        Check(practice.Tier == CurriculumTier.OneStar && !practice.UsesWords && local.CanChooseGroup, "PDF edits leaked to practice.");
        Check(local.Resolve(catalog).ArithmeticOperation == ArithmeticOperation.Add, "Initial operation lost.");
        local = local.SelectProblem(8).SelectSubtype(new(QuizProblemKind.Percentage, PercentageType: PercentageQuizType.FindWholeFromPercentageValue));
        Check(!local.CanChooseGroup && !local.CanChooseFormat && local.Resolve(catalog).PercentageType == PercentageQuizType.FindWholeFromPercentageValue,
            "Percentage subtype or stale learning controls.");
        local = local.SelectProblem(3).SelectSubtype(new(QuizProblemKind.Geometry, GeometryShape: GeometryQuizShape.Cube));
        local = local with { Request = local.Request!.Value with { GeometryMeasurement = GeometryMeasurement.Volume } };
        local = local.SelectSubtype(new(QuizProblemKind.Geometry, GeometryShape: GeometryQuizShape.Rectangle));
        Check(local.Request?.GeometryMeasurement is null, "Volume persisted on a rectangle.");
        local = local with { Request = local.Request!.Value with { GeometryMeasurement = GeometryMeasurement.Area } };
        Check(local.SelectSubtype(new(QuizProblemKind.Geometry, GeometryShape: GeometryQuizShape.Square)).Request?.GeometryMeasurement == GeometryMeasurement.Area,
            "Compatible measurement unnecessarily reset.");
        local = local.SelectProblem(14).SelectSubtype(new(QuizProblemKind.Decimal, ElementaryType: ElementaryQuizType.DecimalAdd)).SelectWords(true);
        Check(local.CanChooseFormat && !local.CanChooseGroup, "Decimal format/group visibility differs from practice.");
        Check(!local.SelectSubtype(new(QuizProblemKind.Decimal, ElementaryType: ElementaryQuizType.DecimalRound)).CanChooseFormat,
            "Rounding exposed an unsupported word-problem format.");
        try { local.SelectSubtype(new(QuizProblemKind.FindX)); throw new Exception("Accepted another family's subtype."); }
        catch (ArgumentException) { }

        int cases = 0;
        foreach (var option in catalog.Options)
        {
            Check(QuizChoiceCatalog.Definitions.ContainsKey(option.LocalizationKey), "Missing family illustration: " + option.LocalizationKey);
            if (option.FixedRequest is not { } fixedRequest) continue;
            int index = catalog.Options.ToList().IndexOf(option);
            foreach (var choice in QuizWorksheetChoices.Subtypes(fixedRequest.Kind))
            {
                Check(QuizChoiceCatalog.Definitions.ContainsKey(choice.Key), "Missing subtype illustration: " + choice.Key);
                foreach (var language in Enum.GetValues<AppLanguage>())
                foreach (var tier in Enum.GetValues<CurriculumTier>())
                {
                    var settings = practice.SelectProblem(index).SelectSubtype(choice.Request) with { Language = language, Tier = tier };
                    var resolved = settings.Resolve(catalog);
                    Check(resolved.Kind == fixedRequest.Kind, "Wrong worksheet family.");
                    // Fixed subtypes must survive catalogue resolution; mixed arithmetic/fraction may select comparisons.
                    if (choice != QuizWorksheetChoices.Subtypes(fixedRequest.Kind)[0])
                        Check(resolved == choice.Request, "Subtype changed on resolution: " + choice.Key);
                    var question = await settings.GenerateAsync(factory, catalog);
                    Check(question.Mode == ArithmeticQuizMode.Essay, "PDF exposed a non-written answer mode.");
                    var printed = QuizWorksheetBuilder.Format(question, language);
                    Check(printed.Prompt.Length > 0 && printed.Answer.Length > 0, "Selected subtype failed to print.");
                    cases++;
                }
            }
        }
        foreach (var group in QuestionLearningProfile.Groups(includeMixed: true))
        {
            Check(QuizChoiceCatalog.Definitions.ContainsKey("Learning.Group." + group), "Missing group illustration.");
            foreach (var family in new[] { 1, 2, 4 })
            {
                var settings = practice.SelectProblem(family).SelectWords(true) with { Group = group, Tier = CurriculumTier.ThreeStars };
                settings = settings.SelectSubtype(family switch
                {
                    1 => new(QuizProblemKind.Arithmetic, ArithmeticOperation: ArithmeticOperation.Add),
                    2 => new(QuizProblemKind.Fraction, FractionOperation: FractionOperation.Add),
                    _ => new(QuizProblemKind.FindX, FindXOperation: ArithmeticOperation.Add)
                });
                var question = await settings.GenerateAsync(factory, catalog);
                Check(question.WordProblem is not null, "Selected story format lost its word problem.");
            }
        }
        var frozen = practice with { Tier = CurriculumTier.ThreeStars };
        var pending = QuizWorksheetBuilder.CreateAsync(10, "Independent settings", "Arithmetic", frozen.Language, frozen.Tier,
            ArithmeticQuizMode.Essay, token => frozen.GenerateAsync(factory, catalog, token));
        local = frozen.SelectProblem(4).SelectWords(true) with { Tier = CurriculumTier.FiveStars, Group = QuestionKnowledgeGroup.Packaging };
        var sheet = await pending;
        Check(sheet.Tier == CurriculumTier.ThreeStars && sheet.Questions.All(q => q.Source.FindXProblem is null && q.Source.WordProblem is null),
            "Editing the next set changed an in-progress or displayed set.");
        var replacement = await frozen.GenerateAsync(factory, catalog);
        Check(replacement.FindXProblem is null && replacement.WordProblem is null, "Replacement used the next set's format.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await local.GenerateAsync(factory, catalog, cancelled.Token); throw new Exception("Cancellation ignored."); }
        catch (OperationCanceledException) { }
        Check(JsonSerializer.Serialize(practice) == unchanged, "Practice selections mutated after PDF generation.");
        Console.WriteLine($"PASS independent worksheet settings: {cases} subtype/star/language cases; illustrated choice coverage, story groups, geometry transitions, frozen generation and cancellation.");
    }
}
