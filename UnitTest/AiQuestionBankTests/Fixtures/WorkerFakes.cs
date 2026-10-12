using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text;
using System.Text.Json;
using static BankDraftFixture;

sealed class FakeRuntime(Func<string, string> produce) : IQuestionTextRuntime
{
    public bool IsLoaded => true;
    public string ModelName => "fake";
    public int Calls { get; private set; }
    public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken token,
        Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null) { Calls++; return Task.FromResult(produce(prompt)); }
}
sealed class BlockingRuntime : IQuestionTextRuntime
{
    public bool IsLoaded => true;
    public string ModelName => "blocking";
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken token,
        Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
    { Started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return "{}"; }
}
sealed class MemoryStore : IQuestionBankStore
{
    public int Saved { get; private set; }
    public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
    { Saved++; return Task.FromResult(true); }
    public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier, AppLanguage language, CancellationToken cancellationToken = default)
        => Task.FromResult<ValidatedBankQuestion?>(null);
}
sealed class FailingStore : IQuestionBankStore
{
    public int Attempts { get; private set; }
    public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
    {
        Attempts++;
        return Attempts == 1 ? Task.FromException<bool>(new IOException("Test storage failure")) : Task.FromResult(true);
    }
    public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier, AppLanguage language, CancellationToken cancellationToken = default)
        => Task.FromResult<ValidatedBankQuestion?>(null);
}
