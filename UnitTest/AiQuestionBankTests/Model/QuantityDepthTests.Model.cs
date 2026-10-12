using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Text;
using System.Text.Json;

internal static partial class QuantityDepthTests
{
    public static async Task RunModelAsync(string model)
    {
        string directory = Path.GetFullPath("artifacts/verification/quantity-depth-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(directory);
        Console.WriteLine("Real GGUF evidence: " + directory);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        var results = new List<object>();
        var failures = new List<string>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(45));
        try
        {
            await modelSession.LoadAsync(model, cancellation.Token);
            Console.WriteLine($"CPU threads {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
            foreach (var c in NativeCases())
            {
                string name = $"{c.Family}-{c.BankVariant}-{(int)c.Tier}-{c.Language}";
                await File.WriteAllTextAsync(Path.Combine(directory, name + "-contract.json"), JsonSerializer.Serialize(c));
                bool passed = false;
                string? correction = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction);
                    var streamed = new StringBuilder();
                    AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateAsync(c, prompt, cancellation.Token, part => streamed.Append(part), value => metrics = value);
                    Check(streamed.ToString() == raw && metrics?.GeneratedTokens > 0, "Missing native streamed tokens");
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    await File.WriteAllTextAsync(Path.Combine(directory, name + "-" + attempt + ".txt"), prompt + "\n\n" + raw + "\n\n" + validation.ErrorCode + " " + validation.ErrorDetails);
                    results.Add(new { Case = name, Attempt = attempt, validation.ErrorCode, validation.ErrorDetails, Metrics = metrics });
                    Console.WriteLine($"{name}: attempt={attempt} {validation.ErrorCode ?? "Valid"}; {metrics!.GeneratedTokens} tokens, {metrics.TokensPerSecond:F2} token/s");
                    if (validation.IsValid)
                    {
                        var draft = validation.Draft!;
                        ProseExpansionTests.VerifyPractice(c, draft);
                        Check(await store.InsertAsync(new(c, draft, raw, Path.GetFileName(model), DateTime.UtcNow)), "Native insert failed");
                        var saved = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
                        Check(saved is not null, "Native read failed");
                        ProseExpansionTests.VerifyPractice(saved!.Contract.FreshFacts(new(92)), saved.Draft);
                        passed = true; break;
                    }
                    correction = validation.ErrorCode;
                }
                if (!passed) failures.Add(name);
            }
        }
        finally
        {
            await modelSession.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        Check(!runtime.IsLoaded, "Native weights retained");
        Check(failures.Count == 0, "Real model failures: " + string.Join(", ", failures));
        Console.WriteLine($"PASS 30 native profiles / {results.Count} attempts; all five tiers, both languages, streaming, SQLite, fresh exact math, grading and disposal.");
    }
}
