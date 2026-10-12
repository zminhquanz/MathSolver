using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text;
using System.Text.Json;
using static BankDraftFixture;

internal static partial class AiTestCommands
{
    private static IEnumerable<TestCommand> Offline() =>
    [
        new(["--harness"], "--harness (runner, workspaces, model teardown and worker lifecycle; no inference)", async args =>
        {
            await HarnessInfrastructureTests.RunAsync();
            await ModelHarnessTests.RunAsync();
            await WorkerLifecycleTests.RunAsync();
        }, args => args is ["--harness"]),
        new(["--area-measurement-foundation"], "--area-measurement-foundation", args =>
        {
            AreaMeasurementFoundationTests.Run();
            return Task.CompletedTask;
        }, args => args is ["--area-measurement-foundation"]),
        new(["--equivalent-prose"], "--equivalent-prose", async args =>
        {
            await EquivalentProseTests.RunAsync();
        }, args => args is ["--equivalent-prose"]),
        new(["--excel-authoring"], "--excel-authoring", async args =>
        {
            await ExcelAuthoringTests.RunAsync();
        }, args => args is ["--excel-authoring"]),
        new(["--next-ai", "--next-ai-model"], "--next-ai | --next-ai-model <model.gguf> [part]", async args =>
        {
            if (args.Length == 1) await NextAiFormsTests.RunAsync(); else await NextAiFormsTests.RunModelAsync(args[1], args.Length == 3 ? args[2] : null);
        }, args => args is ["--next-ai"] or ["--next-ai-model", _] or ["--next-ai-model", _, _]),
        new(["--activity-depth", "--activity-depth-model", "--activity-depth-packing-english-model"], "--activity-depth | --activity-depth-model <model.gguf> | --activity-depth-packing-english-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await ActivityDepthTests.RunAsync();
            else await ActivityDepthTests.RunModelAsync(args[1], args[0] == "--activity-depth-packing-english-model");
        }, args => args is ["--activity-depth"] or ["--activity-depth-model", _] or ["--activity-depth-packing-english-model", _]),
        new(["--quantity-depth", "--quantity-depth-model"], "--quantity-depth | --quantity-depth-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await QuantityDepthTests.RunAsync();
            else await QuantityDepthTests.RunModelAsync(args[1]);
        }, args => args is ["--quantity-depth"] or ["--quantity-depth-model", _]),
        new(["--narrative-depth", "--narrative-depth-model", "--legacy-prose-evidence", "--narrative-depth-evidence"], "--narrative-depth | --narrative-depth-model <model.gguf> | --legacy-prose-evidence <directory> | --narrative-depth-evidence <directory>", async args =>
        {
            if (args.Length == 1) await NarrativeDepthTests.RunAsync();
            else if (args[0] is "--legacy-prose-evidence" or "--narrative-depth-evidence")
                        NarrativeDepthTests.CheckDatabase(args[1], legacy: args[0] == "--legacy-prose-evidence");
            else await ProseExpansionTests.RunModelAsync(args[1], narrativeDepth: true);
        }, args => args is ["--narrative-depth"] or ["--narrative-depth-model", _] or ["--legacy-prose-evidence", _] or ["--narrative-depth-evidence", _]),
        new(["--prose-expansion", "--prose-expansion-model"], "--prose-expansion | --prose-expansion-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await ProseExpansionTests.RunAsync();
            else await ProseExpansionTests.RunModelAsync(args[1]);
        }, args => args is ["--prose-expansion"] or ["--prose-expansion-model", _]),
        new(["--structure-policy"], "--structure-policy", args =>
        {
            GenerationStructurePolicyTests.Run();
            return Task.CompletedTask;
        }, args => args is ["--structure-policy"]),
        new(["--decimal-bank"], "--decimal-bank", async args =>
        {
            await DecimalBankTests.RunAsync();
        }, args => args is ["--decimal-bank"]),
        new(["--packing-time-bank"], "--packing-time-bank", async args =>
        {
            await PackingTimeBankTests.RunAsync();
        }, args => args is ["--packing-time-bank"]),
        new(["--fraction-quantity-bank"], "--fraction-quantity-bank", async args =>
        {
            await FractionQuantityBankTests.RunAsync();
        }, args => args is ["--fraction-quantity-bank"]),
        new(["--measurement-bank"], "--measurement-bank", async args =>
        {
            await MeasurementBankTests.RunAsync();
        }, args => args is ["--measurement-bank"]),
        new(["--proportion-bank"], "--proportion-bank", async args =>
        {
            await ProportionBankTests.RunAsync();
        }, args => args is ["--proportion-bank"]),
        new(["--motion-bank"], "--motion-bank", async args =>
        {
            await MotionBankTests.RunAsync();
        }, args => args is ["--motion-bank"]),
        new(["--multistep-bank"], "--multistep-bank", async args =>
        {
            await MultiStepBankTests.RunAsync();
        }, args => args is ["--multistep-bank"]),
        new(["--reasoning-stories"], "--reasoning-stories", async args =>
        {
            await ReasoningStoryTests.RunAsync();
        }, args => args is ["--reasoning-stories"]),
        new(["--quiz-content"], "--quiz-content", args =>
        {
            QuizContentTests.Run();
            return Task.CompletedTask;
        }, args => args is ["--quiz-content"]),
        new(["--arithmetic-contexts"], "--arithmetic-contexts", async args =>
        {
            using var workspace = TestWorkspace.Temporary("arithmetic-contexts");
            string contextDirectory = workspace.DirectoryPath;
                 await ArithmeticContextTests.RunAsync(contextDirectory);
        }, args => args is ["--arithmetic-contexts"]),
        new(["--one-step-practice"], "--one-step-practice", args =>
        {
            OneStepPracticeTests.Run();
            return Task.CompletedTask;
        }, args => args is ["--one-step-practice"]),
        new(["--practice-formats"], "--practice-formats", async args =>
        {
            using var workspace = TestWorkspace.Temporary("practice-formats");
            string folder = workspace.DirectoryPath;
                 await PracticeFormatTests.RunAsync(folder);
        }, args => args is ["--practice-formats"]),
        new(["--fraction-bank"], "--fraction-bank", async args =>
        {
            using var workspace = TestWorkspace.Temporary("fraction-bank");
            string folder = workspace.DirectoryPath;
                 await FractionBankTests.RunAsync(folder);
        }, args => args is ["--fraction-bank"]),
        new(["--findx-bank"], "--findx-bank", async args =>
        {
            using var workspace = TestWorkspace.Temporary("findx-bank");
            string folder = workspace.DirectoryPath;
                 await FindXBankTests.RunAsync(folder);
        }, args => args is ["--findx-bank"]),
        new(["--clear-bank"], "--clear-bank", async args =>
        {
            using var workspace = TestWorkspace.Temporary("clear-bank");
            string folder = workspace.DirectoryPath;
                 await DeleteAllTests.RunAsync(folder);
        }, args => args is ["--clear-bank"]),
        new(["--sql-grid"], "--sql-grid", async args =>
        {
            using var workspace = TestWorkspace.Temporary("sql-grid");
            string folder = workspace.DirectoryPath;

            var contract = BasicQuestionContract.Create(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(43));
            string raw = Draft(contract);
            var draft = BasicQuestionValidator.Validate(raw, contract).Draft!;
            await SqlGridTests.RunAsync(folder, new(contract, draft, raw, "grid-test", DateTime.UtcNow));
        }, args => args is ["--sql-grid"]),
        new(["--prose-deduplication"], "--prose-deduplication", async args =>
        {
            using var workspace = TestWorkspace.Temporary("prose-deduplication");
            string folder = workspace.DirectoryPath;
            await ProseDeduplicationTests.RunAsync(folder);
        }, args => args is ["--prose-deduplication"]),
        new(["--learning-profiles"], "--learning-profiles", async args =>
        {
            using var workspace = TestWorkspace.Temporary("learning-profiles");
            string folder = workspace.DirectoryPath;
            await LearningProfileTests.RunAsync(folder);
        }, args => args is ["--learning-profiles"]),
        new(["--addition-tests"], "--addition-tests", async args =>
        {
            using var workspace = TestWorkspace.Temporary("addition-tests");
            string testDirectory = workspace.DirectoryPath;
            await AdditionTests.RunAsync(testDirectory);
        }, args => args is ["--addition-tests"]),
    ];
}
