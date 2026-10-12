using MathSolver.Testing;

internal static partial class AiTestCommands
{
    private static IEnumerable<TestCommand> Model() =>
    [
        new(["--equivalent-prose-model"], "--equivalent-prose-model <equivalentProseModel>", async args =>
        {
            var equivalentProseModel = args[1];
            await EquivalentProseModelTests.RunAsync(equivalentProseModel);
        }, args => args is ["--equivalent-prose-model", var equivalentProseModel]),
        new(["--structure-worker"], "--structure-worker <structureWorkerModel>", async args =>
        {
            var structureWorkerModel = args[1];
            await GenerationStructureModelTests.RunWorkerAsync(structureWorkerModel);
        }, args => args is ["--structure-worker", var structureWorkerModel]),
        new(["--structure-model", "--structure-repro"], "--structure-model | --structure-repro <model.gguf>", async args =>
        {
            await GenerationStructureModelTests.RunAsync(args[1], args[0] == "--structure-repro");
        }, args => args is ["--structure-model", _] or ["--structure-repro", _]),
        new(["--reasoning-budget"], "--reasoning-budget <reasoningBudgetModel>", async args =>
        {
            var reasoningBudgetModel = args[1];
            await ReasoningStoryTests.CheckPromptBudgetAsync(reasoningBudgetModel);
        }, args => args is ["--reasoning-budget", var reasoningBudgetModel]),
        new(["--fraction-quantity-model"], "--fraction-quantity-model <fractionQuantityModel> <fractionQuantityDirectory>", async args =>
        {
            var fractionQuantityModel = args[1];
            var fractionQuantityDirectory = args[2];
            await FractionQuantityBankTests.RunModelAsync(fractionQuantityModel, fractionQuantityDirectory);
        }, args => args is ["--fraction-quantity-model", var fractionQuantityModel, var fractionQuantityDirectory]),
        new(["--packing-time-model"], "--packing-time-model <packingModel> <packingDirectory> <packingPart>", async args =>
        {
            var packingModel = args[1];
            var packingDirectory = args[2];
            var packingPart = args[3];
            await PackingTimeBankTests.RunModelAsync(packingModel, packingDirectory, packingPart);
        }, args => args is ["--packing-time-model", var packingModel, var packingDirectory, var packingPart]),
        new(["--measurement-model"], "--measurement-model <measurementModel>", async args =>
        {
            var measurementModel = args[1];
            await MeasurementBankTests.RunModelAsync(measurementModel);
        }, args => args is ["--measurement-model", var measurementModel]),
        new(["--measurement-model-resume"], "--measurement-model-resume <measurementResumeModel> <measurementResumeDirectory>", async args =>
        {
            var measurementResumeModel = args[1];
            var measurementResumeDirectory = args[2];
            await MeasurementBankTests.RunModelAsync(measurementResumeModel, measurementResumeDirectory);
        }, args => args is ["--measurement-model-resume", var measurementResumeModel, var measurementResumeDirectory]),
        new(["--decimal-model-resume"], "--decimal-model-resume <decimalResumeModel> <decimalResumeDirectory>", async args =>
        {
            var decimalResumeModel = args[1];
            var decimalResumeDirectory = args[2];
            await DecimalBankTests.RunModelAsync(decimalResumeModel, decimalResumeDirectory);
        }, args => args is ["--decimal-model-resume", var decimalResumeModel, var decimalResumeDirectory]),
        new(["--decimal-model"], "--decimal-model <decimalModel>", async args =>
        {
            var decimalModel = args[1];
            await DecimalBankTests.RunModelAsync(decimalModel);
        }, args => args is ["--decimal-model", var decimalModel]),
        new(["--proportion-budget"], "--proportion-budget <proportionBudgetModel>", async args =>
        {
            var proportionBudgetModel = args[1];
            await ProportionBankTests.CheckPromptBudgetAsync(proportionBudgetModel);
        }, args => args is ["--proportion-budget", var proportionBudgetModel]),
        new(["--proportion-model"], "--proportion-model <proportionModel>", async args =>
        {
            var proportionModel = args[1];
            await ProportionBankTests.RunModelAsync(proportionModel);
        }, args => args is ["--proportion-model", var proportionModel]),
        new(["--proportion-model-resume"], "--proportion-model-resume <resumeModel> <resumeDirectory>", async args =>
        {
            var resumeModel = args[1];
            var resumeDirectory = args[2];
            await ProportionBankTests.RunModelAsync(resumeModel, resumeDirectory);
        }, args => args is ["--proportion-model-resume", var resumeModel, var resumeDirectory]),
        new(["--motion-budget"], "--motion-budget <motionBudgetModel>", async args =>
        {
            var motionBudgetModel = args[1];
            await MotionBankTests.CheckPromptBudgetAsync(motionBudgetModel);
        }, args => args is ["--motion-budget", var motionBudgetModel]),
        new(["--motion-model"], "--motion-model <motionModel>", async args =>
        {
            var motionModel = args[1];
            await MotionBankTests.RunModelAsync(motionModel);
        }, args => args is ["--motion-model", var motionModel]),
        new(["--multistep-model"], "--multistep-model <multiStepModel>", async args =>
        {
            var multiStepModel = args[1];
            await MultiStepBankTests.RunModelAsync(multiStepModel);
        }, args => args is ["--multistep-model", var multiStepModel]),
        new(["--reasoning-model"], "--reasoning-model <reasoningModel>", async args =>
        {
            var reasoningModel = args[1];
            await ReasoningStoryTests.RunModelAsync(reasoningModel);
        }, args => args is ["--reasoning-model", var reasoningModel]),
        new(["--prose-model"], "--prose-model <proseModelPath>", async args =>
        {
            var proseModelPath = args[1];
            await ProseModelTests.RunAsync(proseModelPath);
        }, args => args is ["--prose-model", var proseModelPath]),
        new(["--arithmetic-context-model"], "--arithmetic-context-model <arithmeticModelPath>", async args =>
        {
            var arithmeticModelPath = args[1];
            await ArithmeticContextTests.RunModelAsync(arithmeticModelPath);
        }, args => args is ["--arithmetic-context-model", var arithmeticModelPath]),
        new(["--fraction-model"], "--fraction-model <fractionModelPath>", async args =>
        {
            var fractionModelPath = args[1];
            await FractionBankTests.RunModelAsync(fractionModelPath);
        }, args => args is ["--fraction-model", var fractionModelPath]),
        new(["--addition-throughput"], "--addition-throughput <throughputModelPath>", async args =>
        {
            var throughputModelPath = args[1];
            await AdditionThroughputChecks.RunAsync(throughputModelPath);
        }, args => args is ["--addition-throughput", var throughputModelPath]),
        new(["--addition-context-model"], "--addition-context-model <contextModelPath>", async args =>
        {
            var contextModelPath = args[1];
            await AdditionThroughputChecks.RunAsync(contextModelPath, newContexts: true);
        }, args => args is ["--addition-context-model", var contextModelPath]),
        new(["--memory-model"], "--memory-model <memoryModelPath>", async args =>
        {
            var memoryModelPath = args[1];
                await MemoryLifecycleTests.RunModelAsync(memoryModelPath);
        }, args => args is ["--memory-model", var memoryModelPath]),
        new(["--model"], "--model <model.gguf>", args => ModelSmokeTests.RunAsync(args[1]),
            args => args is ["--model", _]),
        new(["--addition-model"], "--addition-model <additionModelPath>", async args =>
        {
            var additionModelPath = args[1];
            await AdditionTests.RunModelAsync(additionModelPath);
        }, args => args is ["--addition-model", var additionModelPath]),
        new(["--addition-model-stock"], "--addition-model-stock <stockAdditionModelPath>", async args =>
        {
            var stockAdditionModelPath = args[1];
            await AdditionTests.RunModelAsync(stockAdditionModelPath, stockOnly: true);
        }, args => args is ["--addition-model-stock", var stockAdditionModelPath]),
        new(["--addition-model-english"], "--addition-model-english <englishAdditionModelPath>", async args =>
        {
            var englishAdditionModelPath = args[1];
            await AdditionTests.RunModelAsync(englishAdditionModelPath, englishOnly: true);
        }, args => args is ["--addition-model-english", var englishAdditionModelPath]),
    ];
}
