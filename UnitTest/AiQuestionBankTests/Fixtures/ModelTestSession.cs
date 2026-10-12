using MathSolver.Services.QuestionBank;
using System.Text;

/// <summary>Owns native weights even when loading fails before a test's try/finally.</summary>
internal sealed class ModelTestSession : IAsyncDisposable
{
    public GgufQuestionRuntime Runtime { get; } = new();
    private bool _disposed;

    public async Task LoadAsync(string path, CancellationToken cancellation = default)
    {
        if (!File.Exists(path)) throw new TestUsageException("GGUF file not found: " + path);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, deadline.Token);
        Console.WriteLine("Loading real GGUF: " + Path.GetFileName(path));
        await Runtime.LoadAsync(path, linked.Token);
    }

    internal async Task<NativeGeneration> GenerateAsync(BasicQuestionContract contract, string prompt,
        CancellationToken cancellation, IReadOnlySet<string>? excluded = null)
    {
        var streamed = new StringBuilder();
        AiGenerationMetrics? metrics = null;
        string raw = excluded is null
            ? await Runtime.GenerateAsync(contract, prompt, cancellation, text => streamed.Append(text), value => metrics = value)
            : await Runtime.GenerateNovelAsync(contract, prompt, excluded, cancellation, text => streamed.Append(text), value => metrics = value);
        Check(raw.Length > 0 && streamed.ToString() == raw, "Native streaming does not match final JSON.");
        Check(metrics?.GeneratedTokens > 0, "Native generation has no token metrics.");
        return new(raw, metrics!, BasicQuestionValidator.Validate(raw, contract));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await Runtime.EjectAsync();
    }
}

internal sealed record NativeGeneration(string Raw, AiGenerationMetrics Metrics, BasicDraftValidation Validation);
