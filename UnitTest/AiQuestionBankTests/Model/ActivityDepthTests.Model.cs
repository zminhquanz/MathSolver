using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Text;
using System.Text.Json;

internal static partial class ActivityDepthTests
{
    public static async Task RunModelAsync(string model, bool englishPackingOnly = false)
    {
        string dir = Path.GetFullPath("artifacts/verification/activity-depth-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(dir); Console.WriteLine("Real GGUF: " + dir);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime; var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var results = new List<object>(); var failures = new List<string>();
        var cases = NativeCases().Where(c => !englishPackingOnly || c.Family == BankQuestionFamily.Remainder && c.Language == AppLanguage.English).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        try
        {
            await modelSession.LoadAsync(model, timeout.Token);
            Console.WriteLine($"Threads: {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
            foreach (var c in cases)
            {
                string name = $"{c.Family}-{c.BankVariant}-{(int)c.Tier}-{c.Language}";
                await File.WriteAllTextAsync(Path.Combine(dir, name + "-contract.json"), JsonSerializer.Serialize(c));
                string? correction = null; bool passed = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction); var streamed = new StringBuilder(); AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateAsync(c, prompt, timeout.Token, chunk => streamed.Append(chunk), m => metrics = m);
                    Check(raw == streamed.ToString() && metrics?.GeneratedTokens > 0, "Missing native stream");
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    results.Add(new { Case = name, Attempt = attempt, validation.ErrorCode, validation.ErrorDetails, Metrics = metrics });
                    await File.WriteAllTextAsync(Path.Combine(dir, name + "-" + attempt + ".txt"), prompt + "\n\n" + raw + "\n" + validation.ErrorCode + " " + validation.ErrorDetails);
                    Console.WriteLine($"{name}: attempt={attempt} {validation.ErrorCode ?? "Valid"}; {metrics!.TokensPerSecond:F2} token/s");
                    if (validation.IsValid)
                    {
                        Verify(c, validation.Draft); Verify(c.FreshFacts(new(94)), validation.Draft);
                        Check(await store.InsertAsync(new(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow)), "Native save failed");
                        var saved = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
                        Verify(saved!.Contract.FreshFacts(new(95)), saved.Draft);
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
            await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        Check(!runtime.IsLoaded && failures.Count == 0, "Native failures or retained model: " + string.Join(", ", failures));
        Console.WriteLine($"PASS {cases.Length} native cases / {results.Count} attempts; streaming, validation, SQLite, fresh math/grading, model disposal.");
    }
}
