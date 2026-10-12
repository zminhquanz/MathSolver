using MathSolver.Services.QuestionBank;
using MathSolver.Services;
using MathSolver.Testing;

internal static partial class AiTestCommands
{
    private static IEnumerable<TestCommand> Evidence() =>
    [
        new(["--equivalent-prose-evidence"], "--equivalent-prose-evidence <equivalentProseEvidence>", args =>
        {
            var equivalentProseEvidence = args[1];
            EquivalentProseModelTests.CheckEvidence(equivalentProseEvidence);
            return Task.CompletedTask;
        }, args => args is ["--equivalent-prose-evidence", var equivalentProseEvidence]),
        new(["--next-ai-evidence"], "--next-ai-evidence <nextEvidence>", args =>
        {
            var nextEvidence = args[1];
            NextAiFormsTests.CheckModelEvidence(nextEvidence);
            return Task.CompletedTask;
        }, args => args is ["--next-ai-evidence", var nextEvidence]),
        new(["--activity-depth-evidence"], "--activity-depth-evidence <directory> [more-directories ...]", args =>
        {
            var activityDirectories = args[1..];
            ActivityDepthTests.CheckModelEvidence(activityDirectories);
            return Task.CompletedTask;
        }, args => args is ["--activity-depth-evidence", .. var activityDirectories] && activityDirectories.Length > 0),
        new(["--fraction-quantity-evidence"], "--fraction-quantity-evidence <fractionQuantityEvidence>", async args =>
        {
            var fractionQuantityEvidence = args[1];
            await FractionQuantityBankTests.CheckEvidenceAsync(fractionQuantityEvidence);
        }, args => args is ["--fraction-quantity-evidence", var fractionQuantityEvidence]),
        new(["--packing-time-evidence"], "--packing-time-evidence <packingEvidenceDirectory>", async args =>
        {
            var packingEvidenceDirectory = args[1];
            await PackingTimeBankTests.CheckEvidenceAsync(packingEvidenceDirectory);
        }, args => args is ["--packing-time-evidence", var packingEvidenceDirectory]),
        new(["--measurement-evidence"], "--measurement-evidence <measurementEvidenceDirectory>", async args =>
        {
            var measurementEvidenceDirectory = args[1];
            await MeasurementBankTests.CheckEvidenceAsync(measurementEvidenceDirectory);
        }, args => args is ["--measurement-evidence", var measurementEvidenceDirectory]),
        new(["--motion-evidence"], "--motion-evidence <motionDirectory>", async args =>
        {
            var motionDirectory = args[1];
            await MotionBankTests.WriteRenderedEvidenceAsync(motionDirectory);
        }, args => args is ["--motion-evidence", var motionDirectory]),
        new(["--validate-quiz-content"], "--validate-quiz-content <contentFile>", args =>
        {
            var contentFile = args[1];
            var pack = QuizContentTests.Read(contentFile); Console.WriteLine($"Valid quiz content: {pack.Culture}, {pack.Texts.Count} texts, {pack.Lists.Count} lists.");
            return Task.CompletedTask;
        }, args => args is ["--validate-quiz-content", var contentFile]),
        new(["--check-quiz-translation"], "--check-quiz-translation <translatedFile> <referenceFile>", args =>
        {
            var translatedFile = args[1];
            var referenceFile = args[2];
            QuizContentValidation.CheckTranslation(QuizContentTests.Read(translatedFile), QuizContentTests.Read(referenceFile)); Console.WriteLine("Quiz translation structure passed. Review the meaning of mathematical relations before publishing.");
            return Task.CompletedTask;
        }, args => args is ["--check-quiz-translation", var translatedFile, var referenceFile]),
    ];
}
