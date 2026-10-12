using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Services.Core;
using System.Text;
using System.Text.Json;

internal static partial class GenerationStructureModelTests
{
    public static async Task RunWorkerAsync(string path)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", "structure-worker-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        var worker = new AiQuestionGenerationService(runtime, store);
        var essay = new EssayAnswerValidator(new BasicArithmeticEngine());
        int saved = 0, streamingUpdates = 0;
        worker.Changed += (_, _) =>
        {
            if (worker.Snapshot.Items.Any(i => i.Attempts.Any(a => !a.IsComplete && a.RawJson.Length > 0)))
                Interlocked.Increment(ref streamingUpdates);
        };
        Console.WriteLine("Native batch/SQLite verification; evidence: " + directory);

        await runtime.SelectAsync(path);
        var jobs = new[] {
            new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 4, true,
                new(QuestionKnowledgeGroup.Objects), BankQuestionFamily.Fraction),
            new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.TwoStars, AppLanguage.English, 2, true,
                new(QuestionKnowledgeGroup.Objects), BankQuestionFamily.Fraction),
            new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese, 2, true,
                Family: BankQuestionFamily.Average, StoryVariant: 4) };
        for (int index = 0; index < jobs.Length; index++)
        {
            var options = jobs[index];
            worker.Start(options);
            try { await worker.Completion.WaitAsync(TimeSpan.FromMinutes(12)); }
            catch (TimeoutException) { worker.Stop(); await worker.Completion; throw; }
            var snapshot = worker.Snapshot;
            await File.WriteAllTextAsync(Path.Combine(directory, $"batch-{index + 1}.json"),
                JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            if (snapshot.State != AiJobState.Completed || snapshot.Items.Count != options.Count
                || snapshot.Items.Any(i => i.State != AiItemState.Saved))
                throw new Exception("Native batch failed: " + snapshot.Error);
            if (runtime.IsLoaded || !runtime.CanGenerate) throw new Exception("Weights were not released or model selection was lost");
            foreach (var item in snapshot.Items)
            {
                if (!item.Attempts.Any(a => a.Metrics?.GeneratedTokens > 1) || item.Question is null)
                    throw new Exception("Missing generation metrics or saved template");
                var stored = options.Family == BankQuestionFamily.Fraction
                    ? await store.TakeFractionAsync(options.Operation, options.Tier, options.Language, options.Profile!)
                    : await store.TakeReasoningAsync(options.Family, options.StoryVariant, options.Tier, options.Language);
                if (stored is null) throw new Exception("Native template cannot be read back from SQLite");
                var fresh = stored.Contract.FreshFacts(new Random(503 + item.Number));
                if (!fresh.IsValid || !BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(stored.Draft), fresh).IsValid)
                    throw new Exception("Saved prose does not support fresh C# facts");
                if (options.Family == BankQuestionFamily.Fraction)
                {
                    var word = stored.Draft.ToWordProblem(fresh);
                    foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    {
                        var practice = fresh.ToPracticeQuestion(word, mode, new Random(17));
                        if (practice.ExactAnswer != FractionQuestionCatalogue.Answer(fresh)) throw new Exception("Practice math changed");
                        if (mode == ArithmeticQuizMode.Essay && !essay.Validate(practice, word.SolutionLead,
                            $"{FractionQuestionCatalogue.Expression(fresh)} = {fresh.AnswerText} {fresh.Unit}",
                            $"{fresh.AnswerText} {fresh.Unit}").IsCorrect) throw new Exception("Native prose fails essay grading");
                    }
                }
                else
                    foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                        _ = ReasoningStoryCatalogue.ToPractice(fresh, stored.Draft, mode);
                saved++;
                Console.WriteLine($"PASS batch {index + 1}/{item.Number}: {item.Contract.SceneId}, attempts={item.Attempts.Count}");
            }
        }
        using var workbook = new MemoryStream();
        var exported = await store.ExportExcelAsync(workbook);
        if (exported.Exported != saved || streamingUpdates == 0) throw new Exception("Saved row count or streaming mismatch");
        Console.WriteLine($"PASS {saved} native batch templates saved/read/freshened, {streamingUpdates} streaming updates, weights released after each batch.");

    }
}
