using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class BackgroundPracticeTests
{
    public static async Task RunAsync(string directory)
    {
        var store = new QuestionBankStore(Path.Combine(directory, "background-practice.db3"));
        var runtime = new PausedRuntime();
        var generation = new AiQuestionGenerationService(runtime, store);
        var provider = new BasicPracticeQuestionProvider(store, new BankSourceRandom(), new FormatRandom(1));
        var numericProvider = new BasicPracticeQuestionProvider(store, new BankSourceRandom(), new FormatRandom(0));
        var generator = new ArithmeticQuizGenerator(new BasicArithmeticEngine(), new Random(3456));
        var validator = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        var options = new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.OneStar,
            AppLanguage.Vietnamese, 2, true);

        generation.Start(options);
        try
        {
            await runtime.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            {
                var fresh = generator.Generate(mode, options.Operation, new(options.Tier, false));
                var selected = await provider.SelectAsync(fresh, options.Tier, options.Language)
                    .WaitAsync(TimeSpan.FromSeconds(5));
                Check(selected.WordProblem is not null && selected.Mode == mode && validator.Validate(selected).IsValid && generation.IsRunning,
                    "An empty bank waited for inference instead of serving a C# question.");
                var numeric = await numericProvider.SelectAsync(fresh, options.Tier, options.Language)
                    .WaitAsync(TimeSpan.FromSeconds(5));
                Check(ReferenceEquals(numeric, fresh) && numeric.WordProblem is null && generation.IsRunning,
                    "Paused inference prevented immediate original random arithmetic practice.");
            }

            runtime.FirstOutput.TrySetResult();
            // The next inference starts only after the first question is validated
            // and committed. Leave it blocked while practice reads that row.
            await runtime.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(generation.Snapshot.Items[0].State == AiItemState.Saved,
                "The first valid question was not auto-saved before the next inference.");
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            {
                var fresh = generator.Generate(mode, options.Operation, new(options.Tier, false));
                var selected = await provider.SelectAsync(fresh, options.Tier, options.Language)
                    .WaitAsync(TimeSpan.FromSeconds(5));
                Check(selected.WordProblem is not null && selected.Mode == mode
                    && validator.Validate(selected).IsValid && generation.IsRunning,
                    "Practice could not read a committed question while inference was running.");
            }
        }
        finally
        {
            generation.Stop();
            await generation.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Check(generation.Snapshot.State == AiJobState.Stopped && runtime.Released,
            "Stopping the background job skipped runtime cleanup.");
        Check(await store.TakeAsync(options.Operation, options.Tier, options.Language) is not null,
            "Stopping inference discarded the already committed question.");
        Console.WriteLine("PASS practice in every answer mode during paused inference: empty-bank C# fallback, committed SQLite reads, auto-save and cancellation cleanup");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class BankSourceRandom : Random
    { public override int Next(int maxValue) => 1; }

    private sealed class FormatRandom(int format) : Random
    { public override int Next(int maxValue) => format; }

    private sealed class PausedRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "background-practice-test";
        public bool Released { get; private set; }
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstOutput { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public async Task<string> GenerateAsync(BasicQuestionContract contract, string prompt,
            CancellationToken cancellationToken, Action<string>? onText = null,
            Action<AiGenerationMetrics>? onMetrics = null)
        {
            if (++_calls == 1)
            {
                FirstStarted.TrySetResult();
                await FirstOutput.Task.WaitAsync(cancellationToken);
                return QuestionBankStore.SerializeDraft(BasicQuestionTemplates.Example(contract));
            }
            SecondStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Paused generation completed without cancellation.");
        }

        public Task ReleaseAsync()
        { Released = true; return Task.CompletedTask; }
    }
}
