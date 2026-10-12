using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Text;
using System.Text.Json;

internal static partial class NextAiFormsTests
{
    public static async Task RunModelAsync(string model, string? evidenceDirectory = null)
    {
        string dir = Path.GetFullPath(evidenceDirectory ?? "artifacts/verification/next-ai-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(dir); Console.WriteLine("Native GGUF evidence: " + dir);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime; var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var results = new List<object>(); var failures = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        try
        {
            await modelSession.LoadAsync(model, timeout.Token);
            Console.WriteLine($"Threads {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
            foreach (var c in NativeCases())
            {
                string name = $"{c.Family}-{c.BankVariant}-{(int)c.Tier}-{c.Language}";
                var previous = c.Family == BankQuestionFamily.Data ? await store.TakeChartAsync(c.Story!.ChartProfile!)
                    : await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
                if (previous is not null && previous.Contract.Story!.Schema == c.Story!.Schema)
                {
                    Check(previous.ModelName == runtime.ModelName && BasicQuestionValidator.Validate(previous.RawJson, previous.Contract).IsValid,
                        "Resume requires validated output from the actual selected model");
                    Verify(previous.Contract, previous.Draft); Verify(previous.Contract.FreshFacts(new(819)), previous.Draft);
                    Console.WriteLine(name + ": reused validated native SQLite evidence");
                    continue;
                }
                await File.WriteAllTextAsync(Path.Combine(dir, name + "-contract.json"), JsonSerializer.Serialize(c));
                string? correction = null; bool passed = false;
                var excluded = await store.GetProseHashesAsync();
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction, excludedProse: excluded); var stream = new StringBuilder(); AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, part => stream.Append(part), value => metrics = value);
                    Check(raw == stream.ToString() && metrics?.GeneratedTokens > 0, "Missing native streaming");
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    results.Add(new { Case = name, Attempt = attempt, validation.ErrorCode, validation.ErrorDetails, Metrics = metrics });
                    await File.WriteAllTextAsync(Path.Combine(dir, "results-" + name + "-" + attempt + ".json"),
                        JsonSerializer.Serialize(results[^1], new JsonSerializerOptions { WriteIndented = true }));
                    await File.WriteAllTextAsync(Path.Combine(dir, name + "-" + attempt + ".txt"), prompt + "\n\n" + raw + "\n" + validation.ErrorCode);
                    Console.WriteLine($"{name}: attempt {attempt}, {validation.ErrorCode ?? "Valid"}, {metrics!.TokensPerSecond:F2} token/s");
                    if (validation.IsValid)
                    {
                        Verify(c, validation.Draft); Verify(c.FreshFacts(new(819)), validation.Draft);
                        Check(await store.InsertAsync(new(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow)), "Native save failed");
                        passed = true; break;
                    }
                    correction = validation.ErrorCode;
                }
                if (!passed) failures.Add(name);
            }
        }
        finally { await modelSession.DisposeAsync(); await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })); }
        Check(!runtime.IsLoaded && failures.Count == 0, "Native failures: " + string.Join(", ", failures));
        Console.WriteLine($"PASS 30 actual native profiles / {results.Count} attempts; streaming, validation, fresh math and SQLite; model disposed.");
    }
}
