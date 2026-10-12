using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static partial class MemoryLifecycleTests
{
    public static async Task RunModelAsync(string path)
    {
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        await runtime.SelectAsync(path);
        Check(runtime.CanGenerate && !runtime.IsLoaded, "Selecting GGUF eagerly loaded weights.");
        var store = new MemoryStore();
        var service = new AiQuestionGenerationService(runtime, store);
        try
        {
            foreach (var options in new[] {
                new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 2, true),
                new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.English, 1, true) })
            {
                Console.WriteLine($"GENERATING real GGUF {options.Language}/{options.Tier}, count {options.Count}");
                service.Start(options);
                await service.Completion.WaitAsync(TimeSpan.FromMinutes(16));
                foreach (var item in service.Snapshot.Items)
                {
                    foreach (var attempt in item.Attempts) Console.WriteLine($"Attempt {attempt.Number}: {attempt.ErrorCode ?? "PASS"}\n{attempt.RawJson}");
                    if (item.Question is { } question) Console.WriteLine("RENDERED: " + question.WordProblem.ProblemText);
                }
                Check(service.Snapshot.State == AiJobState.Completed, "Real GGUF lifecycle job failed: " + service.Snapshot.Error);
                Check(!runtime.IsLoaded && runtime.CanGenerate && runtime.ModelPath == Path.GetFullPath(path), "Native weights remained loaded or selection was lost.");
                Check(runtime.LastContextTokens is >= 1024 and <= 2048 && runtime.InferenceThreadCount == 0,
                    "Context exceeded the cap or resident parameters survived cleanup.");
                Console.WriteLine($"Released GGUF weights; last context: {runtime.LastContextTokens}; saved: {store.Saved}");
            }
            await modelSession.DisposeAsync();
            Check(!runtime.CanGenerate && !runtime.IsLoaded, "Explicit eject retained model selection.");
        }
        finally { service.Stop(); await service.Completion; await modelSession.DisposeAsync(); }
        Console.WriteLine("PASS real GGUF lazy loading, bounded context, valid batch/single generation, disposal and automatic reload. No app database writes or benchmark.");
    }
}
