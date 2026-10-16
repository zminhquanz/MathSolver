using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Runtime.ExceptionServices;

internal static class AiStopTests
{
    internal static async Task RunAsync()
    {
        foreach (string phase in new[] { "prepare", "stream", "next-question", "retry", "validate", "duplicate-check", "auto-insert" })
        {
            var runtime = new CooperativeRuntime(phase);
            var store = new StopStore(phase);
            var service = new AiQuestionGenerationService(runtime, store);
            int exceptions = 0;
            EventHandler<FirstChanceExceptionEventArgs> observe = (_, e) =>
            {
                if (e.Exception is OperationCanceledException && e.Exception.StackTrace?.Contains(nameof(AiQuestionGenerationService), StringComparison.Ordinal) == true)
                    Interlocked.Increment(ref exceptions);
            };
            service.Changed += (_, _) =>
            {
                var snapshot = service.Snapshot;
                if (phase == "next-question" && snapshot.Items.Any(i => i.State == AiItemState.Ready)
                    || phase == "auto-insert" && snapshot.Items.Any(i => i.State == AiItemState.Ready)
                    || phase == "retry" && snapshot.Items.Any(i => i.Attempts.Any(a => a.IsComplete))
                    || phase == "validate" && snapshot.State == AiJobState.Validating)
                    service.Stop();
            };
            AppDomain.CurrentDomain.FirstChanceException += observe;
            try
            {
                service.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese,
                    phase == "next-question" ? 2 : 1, phase is "validate" or "duplicate-check" or "auto-insert"));
                if (phase is "prepare" or "stream" or "duplicate-check")
                {
                    await (phase == "duplicate-check" ? store.Started.Task : runtime.Started.Task).WaitAsync(TimeSpan.FromSeconds(5));
                    service.Stop();
                }
                await service.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { AppDomain.CurrentDomain.FirstChanceException -= observe; }
            Check(service.Snapshot.State == AiJobState.Stopped && service.Snapshot.Error is null,
                $"Stopping during {phase} must finish normally as Stopped.");
            Check(exceptions == 0, $"Stopping during {phase} threw {exceptions} first-chance cancellation exceptions from the worker.");
            Check(runtime.Releases == 1 && !runtime.IsLoaded && store.Saved == 0,
                $"Stopping during {phase} skipped cleanup or saved interrupted output.");
            if (phase == "prepare") Check(runtime.Calls == 0 && store.ProseReads == 0, "Generation/store access began after preparation was stopped.");
            if (phase == "stream")
            {
                var item = service.Snapshot.Items.Single();
                Check(item.State == AiItemState.Stopped && item.Question is null && !item.Attempts.Single().IsComplete
                    && item.Attempts[0].RawJson == "partial output + final buffered tail", "Stopping lost buffered output or validated a partial draft.");
            }
            if (phase == "next-question") Check(runtime.Calls == 1 && service.Snapshot.Items.Single().State == AiItemState.Ready,
                "Stopping between questions discarded the completed preview or started another question.");
            if (phase == "retry") Check(runtime.Calls == 1 && service.Snapshot.Items.Single().State == AiItemState.Stopped,
                "Stopping before retry started another inference attempt.");
            if (phase == "auto-insert") Check(runtime.Calls == 1 && service.Snapshot.Items.Single() is { State: AiItemState.Ready, Question: not null },
                "Stopping before auto-insert must retain the completed preview without saving it.");
        }
        Console.WriteLine("PASS AI Stop: prepare, streaming, next question, retry, validation, duplicate check and auto-insert; no worker first-chance cancellation, no partial saves, release once");
    }

    private sealed class CooperativeRuntime(string phase) : IQuestionTextRuntime
    {
        public bool IsLoaded { get; private set; }
        public bool CanGenerate => true;
        public string ModelName => "cooperative-stop";
        public int Calls { get; private set; }
        public int Releases { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task PrepareAsync(CancellationToken token)
        {
            if (phase == "prepare") { Started.TrySetResult(); await StopSignal(token); }
            IsLoaded = true;
        }
        public Task ReleaseAsync() { Releases++; IsLoaded = false; return Task.CompletedTask; }
        public async Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken token,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Calls++;
            if (phase == "stream")
            {
                onText?.Invoke("partial output");
                Started.TrySetResult();
                await StopSignal(token);
                return "partial output + final buffered tail";
            }
            return phase == "retry" ? "{}" : QuestionBankStore.SerializeDraft(BasicQuestionTemplates.Example(contract));
        }
    }

    private sealed class StopStore(string phase) : IQuestionBankStore
    {
        public int Saved { get; private set; }
        public int ProseReads { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlySet<string>> GetProseHashesAsync(CancellationToken token = default)
        { ProseReads++; return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()); }
        public async Task<bool> ContainsProseAsync(ValidatedBankQuestion question, CancellationToken token = default)
        {
            if (phase == "duplicate-check") { Started.TrySetResult(); await StopSignal(token); }
            return false;
        }
        public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
        { Saved++; return Task.FromResult(true); }
        public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier, AppLanguage language,
            CancellationToken cancellationToken = default) => Task.FromResult<ValidatedBankQuestion?>(null);
    }

    private static async Task StopSignal(CancellationToken token)
    {
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() => stopped.TrySetResult());
        await stopped.Task;
    }

    private static void Check(bool condition, string message) => MathSolver.Testing.TestAssert.That(condition, message);
}
