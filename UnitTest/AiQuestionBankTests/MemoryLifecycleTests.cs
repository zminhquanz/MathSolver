using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static class MemoryLifecycleTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static async Task RunAsync()
    {
        string selectedFile = Path.Combine(Path.GetTempPath(), "question-memory-" + Guid.NewGuid().ToString("N") + ".gguf");
        string invalidFile = Path.ChangeExtension(selectedFile, ".invalid.gguf");
        var selected = new GgufQuestionRuntime();
        try
        {
            await File.WriteAllBytesAsync(selectedFile, "GGUF"u8.ToArray());
            await File.WriteAllBytesAsync(invalidFile, "NOPE"u8.ToArray());
            await selected.SelectAsync(selectedFile);
            Check(selected.CanGenerate && !selected.IsLoaded && selected.InferenceThreadCount == 0,
                "Selection created resident native weights.");
            try { await selected.SelectAsync(invalidFile); throw new Exception("Invalid GGUF header accepted."); }
            catch (InvalidDataException) { }
            Check(selected.ModelPath == selectedFile, "Invalid selection discarded the existing model choice.");
            await selected.ReleaseAsync();
            Check(selected.CanGenerate && !selected.IsLoaded, "Memory release removed the model choice.");
            await selected.EjectAsync();
            Check(!selected.CanGenerate && selected.ModelName == "", "Explicit eject did not clear the model choice.");
        }
        finally { await selected.EjectAsync(); File.Delete(selectedFile); File.Delete(invalidFile); }

        Check(GgufQuestionRuntime.GetContextTokens(100) == 1024, "Small prompt reserved excess context.");
        Check(GgufQuestionRuntime.GetContextTokens(700) == 1536, "Context rounding changed.");
        Check(GgufQuestionRuntime.GetContextTokens(1284) == 2048, "Maximum prompt budget rejected.");
        foreach (int oversized in new[] { 1285, int.MaxValue })
        {
            try { GgufQuestionRuntime.GetContextTokens(oversized); throw new Exception("Oversized prompt accepted."); }
            catch (InvalidDataException error) { Check(error.Message == "PromptTooLong", "Wrong context budget error."); }
        }

        var options = new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 3, true);
        var runtime = new LifecycleRuntime();
        var store = new MemoryStore();
        var service = new AiQuestionGenerationService(runtime, store);
        service.Start(options);
        await service.Completion;
        Check(service.Snapshot.State == AiJobState.Completed && store.Saved == 3
            && runtime.Loads == 1 && runtime.Releases == 1 && runtime.Calls == 3 && !runtime.IsLoaded,
            "Batch did not share one load or release all resident weights.");
        Check(service.Snapshot.Items.All(i => i.Question?.ModelName == "lifecycle"), "Cleanup lost model provenance.");
        service.Start(options with { Count = 1 });
        await service.Completion;
        Check(runtime.Loads == 2 && runtime.Releases == 2 && store.Saved == 4,
            "Next job could not reload the selected model.");

        foreach (string mode in new[] { "invalid", "throw", "load-failure", "cancel-load", "cancel-generation", "release-failure", "cancel-release" })
        {
            runtime = new() { Mode = mode };
            store = new();
            service = new(runtime, store);
            service.Start(options);
            if (mode.StartsWith("cancel-"))
            {
                await runtime.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (mode == "cancel-release")
                {
                    Check(service.IsRunning, "Job exposed completion before native cleanup.");
                    try { service.Start(options); throw new Exception("New job raced disposal."); }
                    catch (InvalidOperationException error) { Check(error.Message == "JobAlreadyRunning", "Wrong job guard."); }
                    service.Stop();
                    runtime.FinishRelease.TrySetResult();
                }
                else service.Stop();
            }
            await service.Completion;
            Check(runtime.Releases == 1 && !runtime.IsLoaded, $"{mode} skipped cleanup.");
            Check(service.Snapshot.State == (mode == "cancel-release" ? AiJobState.Completed
                : mode.StartsWith("cancel-") ? AiJobState.Stopped : AiJobState.Failed), $"{mode} reported wrong terminal state.");
            if (mode == "invalid") Check(runtime.Calls == 3 && store.Saved == 0, "Retry policy changed.");
            if (mode is "load-failure" or "cancel-load") Check(runtime.Calls == 0, "Generation ran after failed/cancelled loading.");
        }
        Console.WriteLine("PASS bounded context, batch load/reload and disposal on success, retries, errors and cancellation; cleanup cannot race a new job.");
    }

    public static async Task RunModelAsync(string path)
    {
        var runtime = new GgufQuestionRuntime();
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
            await runtime.EjectAsync();
            Check(!runtime.CanGenerate && !runtime.IsLoaded, "Explicit eject retained model selection.");
        }
        finally { service.Stop(); await service.Completion; await runtime.EjectAsync(); }
        Console.WriteLine("PASS real GGUF lazy loading, bounded context, valid batch/single generation, disposal and automatic reload. No app database writes or benchmark.");
    }

    private sealed class LifecycleRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded { get; private set; }
        public bool CanGenerate => true;
        public string ModelName => "lifecycle";
        public string Mode { get; init; } = "success";
        public int Loads { get; private set; }
        public int Releases { get; private set; }
        public int Calls { get; private set; }
        public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FinishRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task PrepareAsync(CancellationToken token)
        {
            Loads++;
            if (Mode == "cancel-load") { Blocked.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
            if (Mode == "load-failure") throw new IOException("Model load failed.");
            IsLoaded = true;
        }
        public async Task ReleaseAsync()
        {
            Releases++;
            if (Mode == "cancel-release") { Blocked.TrySetResult(); await FinishRelease.Task; }
            IsLoaded = false;
            if (Mode == "release-failure") throw new IOException("Release failed.");
        }
        public async Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Check(IsLoaded, "Attempt ran without resident weights.");
            Calls++;
            if (Mode == "cancel-generation") { Blocked.TrySetResult(); await Task.Delay(Timeout.Infinite, cancellationToken); }
            if (Mode == "throw") throw new IOException("Generation failed.");
            return Mode == "invalid" ? "{}" : QuestionBankStore.SerializeDraft(BasicQuestionTemplates.Example(contract));
        }
    }
}
