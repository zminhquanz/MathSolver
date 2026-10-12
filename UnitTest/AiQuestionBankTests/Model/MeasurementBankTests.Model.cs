using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static partial class MeasurementBankTests
{
    public static async Task RunModelAsync(string modelPath, string? resumeDirectory = null)
    {
        string dir = resumeDirectory is null
            ? Path.GetFullPath("artifacts/verification/measurement-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"))
            : Path.GetFullPath(resumeDirectory);
        Directory.CreateDirectory(dir);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime; var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var evidence = new List<object>(); var rendered = new StringBuilder();
        var completed = new HashSet<string>(StringComparer.Ordinal);
        if (resumeDirectory is not null)
        {
            using var prior = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, "results.json")));
            foreach (var row in prior.RootElement.EnumerateArray())
            {
                if (row.GetProperty("ErrorCode").ValueKind != JsonValueKind.Null) continue;
                string name = row.GetProperty("Case").GetString()!;
                completed.Add(name[(name.IndexOf('-') + 1)..]); evidence.Add(row.Clone());
            }
            await CheckEvidenceAsync(dir);
            rendered.Append(await File.ReadAllTextAsync(Path.Combine(dir, "rendered-samples.txt")));
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        Console.WriteLine("Real measurement GGUF: " + dir);
        try
        {
            await modelSession.LoadAsync(modelPath, timeout.Token);
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            foreach (var type in ElementaryQuizGenerator.MeasurementStoryTypes)
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            {
                var contexts = ElementaryQuizGenerator.MeasurementContexts(language, type);
                var context = contexts[((int)tier - 1) % contexts.Count];
                if (completed.Contains($"{language}-{type}-{tier}-{context.Id}")) continue;
                var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement, (int)type, tier, language, new(47), context.Id);
                var original = ReasoningStoryCatalogue.Draft(c);
                await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "original", DateTime.UtcNow), timeout.Token);
                var excluded = (await store.GetProseHashesAsync(timeout.Token)).ToHashSet(StringComparer.Ordinal);
                string prompt = BasicQuestionPrompt.Build(c, "DuplicateProse", [original.ProblemText], excluded);
                var streamed = new StringBuilder(); AiGenerationMetrics? metrics = null;
                string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token,
                    t => streamed.Append(t), m => metrics = m);
                var result = BasicQuestionValidator.Validate(raw, c);
                string name = $"{evidence.Count + 1}-{language}-{type}-{tier}-{context.Id}";
                await File.WriteAllTextAsync(Path.Combine(dir, name + ".txt"), prompt + "\nRAW\n" + raw + "\nVALIDATION\n" + (result.ErrorCode ?? "Valid"));
                evidence.Add(new { Case = name, result.ErrorCode, result.ErrorDetails, Metrics = metrics });
                Check(result.IsValid && metrics?.GeneratedTokens > 1 && raw == streamed.ToString(), "Native measurement failed: " + name + " " + result.ErrorCode);
                Check(ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Native measurement facts did not vary");
                var saved = new ValidatedBankQuestion(c, result.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                CheckPractice(c.FreshFacts(new(881)), saved.Draft);
                Check(await store.InsertAsync(saved, timeout.Token) && !await store.InsertAsync(saved, timeout.Token), "Native measurement dedup failed");
                var retrieved = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language, timeout.Token);
                if (retrieved is not null && QuestionProseIdentity.Hash(retrieved.Contract, retrieved.Draft) != QuestionProseIdentity.Hash(c, saved.Draft))
                    retrieved = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language, timeout.Token);
                Check(retrieved is not null && QuestionProseIdentity.Hash(retrieved.Contract, retrieved.Draft) == QuestionProseIdentity.Hash(c, saved.Draft),
                    "Native measurement retrieval failed or returned the seeded old prose");
                var fresh = retrieved!.Contract.FreshFacts(new(881)); CheckPractice(fresh, retrieved.Draft);
                var p = ReasoningStoryCatalogue.ToPractice(fresh, retrieved.Draft, ArithmeticQuizMode.Essay).ElementaryProblem!;
                rendered.AppendLine(name).AppendLine(p.ProblemText).AppendLine(p.SolutionText).AppendLine();
                await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
                await File.WriteAllTextAsync(Path.Combine(dir, "rendered-samples.txt"), rendered.ToString());
                Console.WriteLine($"PASS {name}: {metrics!.GeneratedTokens} tokens; native stream/dedup/SQLite/fresh math/grading");
            }
            await runtime.ReleaseAsync();
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese, 4, true,
                Family: BankQuestionFamily.Measurement, StoryVariant: (int)ElementaryQuizType.MassConversion));
            try { await worker.Completion.WaitAsync(timeout.Token); }
            catch { worker.Stop(); await worker.Completion; throw; }
            await File.WriteAllTextAsync(Path.Combine(dir, "worker.json"), JsonSerializer.Serialize(worker.Snapshot, new JsonSerializerOptions { WriteIndented = true }));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved),
                "Measurement worker failed: " + worker.Snapshot.Error);
            foreach (var item in worker.Snapshot.Items) CheckPractice(item.Contract.FreshFacts(new(105 + item.Number)), item.Question!.Draft);
            Check(!runtime.IsLoaded && runtime.CanGenerate, "Model weights retained after measurement job");
        }
        finally
        {
            await modelSession.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(Path.Combine(dir, "rendered-samples.txt"), rendered.ToString());
        }
        Console.WriteLine($"Real measurement model passed: {evidence.Count} profiles and 4 worker items; {dir}");
    }
}
