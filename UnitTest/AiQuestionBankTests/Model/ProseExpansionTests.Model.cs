using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text;
using System.Text.Json;

internal static partial class ProseExpansionTests
{
    public static async Task RunModelAsync(string model, bool narrativeDepth = false)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", (narrativeDepth ? "narrative-depth-model-" : "prose-expansion-model-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        Console.WriteLine("Actual GGUF inference, isolated SQLite, fresh C# facts. Evidence: " + directory);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        var results = new List<object>();
        var failures = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(50));
        try
        {
            await modelSession.LoadAsync(model, timeout.Token);
            Console.WriteLine($"CPU generation threads: {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            foreach (var family in narrativeDepth ? NarrativeDepthTests.Families : Enum.GetValues<BankQuestionFamily>())
            {
                var c = narrativeDepth ? NarrativeDepthTests.NativeContract(family, language) : NativeContract(family, language);
                string name = $"{family}-{(int)c.Tier}-{language}";
                await File.WriteAllTextAsync(Path.Combine(directory, name + "-contract.json"), JsonSerializer.Serialize(c));
                string? correction = null;
                bool passed = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction);
                    var streamed = new StringBuilder();
                    AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateAsync(c, prompt, timeout.Token, text => streamed.Append(text), m => metrics = m);
                    Check(streamed.ToString() == raw && metrics?.GeneratedTokens > 0, "Missing real streamed inference");
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    await File.WriteAllTextAsync(Path.Combine(directory, $"{name}-{attempt}.txt"), prompt + "\n\n" + raw + "\n\n" + validation.ErrorCode + " " + validation.ErrorDetails, timeout.Token);
                    results.Add(new { Case = name, Attempt = attempt, validation.ErrorCode, validation.ErrorDetails, Metrics = metrics });
                    Console.WriteLine($"{name}: attempt {attempt}, {validation.ErrorCode ?? "Valid"}, tokens={metrics!.GeneratedTokens}, tok/s={metrics.TokensPerSecond:F2}");
                    if (validation.IsValid)
                    {
                        var saved = new ValidatedBankQuestion(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                        Check(await store.InsertAsync(saved, timeout.Token), "Native prose SQLite insert failed");
                        var read = ReasoningStoryCatalogue.Supports(family)
                            ? await store.TakeReasoningAsync(family, c.BankVariant, c.Tier, language, timeout.Token)
                            : family == BankQuestionFamily.Fraction ? await store.TakeFractionAsync(c.Operation, c.Tier, language, new(c.KnowledgeGroup), timeout.Token)
                            : family == BankQuestionFamily.FindX ? await store.TakeFindXAsync(c.Operation, c.Tier, language, new(c.KnowledgeGroup), timeout.Token)
                            : await store.TakeForProfileAsync(c.Operation, c.Tier, language, new(c.KnowledgeGroup), timeout.Token);
                        Check(read is not null, "Native prose SQLite read failed");
                        var fresh = read!.Contract.FreshFacts(new(193));
                        Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(read.Draft), fresh).IsValid, "Native prose failed with fresh numbers");
                        if (ReasoningStoryCatalogue.Supports(family)) VerifyPractice(fresh, read.Draft);
                        else foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                            _ = fresh.ToPracticeQuestion(read.Draft.ToWordProblem(fresh), mode, new(3));
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
        Check(!runtime.IsLoaded, "Weights not released");
        Check(failures.Count == 0, "Native failures: " + string.Join(", ", failures));
        Console.WriteLine($"PASS {results.Count} real inference attempts; SQLite/fresh math/grading, streaming and model disposal verified.");
    }
}
