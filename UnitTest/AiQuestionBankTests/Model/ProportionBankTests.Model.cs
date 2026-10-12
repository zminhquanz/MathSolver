using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static partial class ProportionBankTests
{
    public static async Task RunModelAsync(string modelPath, string? resumeDirectory = null)
    {
        string dir = resumeDirectory is null ? Path.GetFullPath(Path.Combine("artifacts", "verification", "proportion-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")))
            : Path.GetFullPath(resumeDirectory);
        Directory.CreateDirectory(dir);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var evidence = new List<object>(); var rendered = new StringBuilder();
        var completed = new HashSet<string>();
        if (resumeDirectory is not null)
        {
            using var prior = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, "results.json")));
            foreach (var item in prior.RootElement.EnumerateArray())
            {
                if (item.GetProperty("ErrorCode").ValueKind != JsonValueKind.Null) continue;
                string name = item.GetProperty("Case").GetString()!;
                completed.Add(name[(name.IndexOf('-') + 1)..]); evidence.Add(item.Clone());
            }
            using var workbook = new MemoryStream(); await store.ExportExcelAsync(workbook); workbook.Position = 0;
            foreach (var row in QuestionBankWorkbook.Read(workbook))
            {
                var saved = row.Question ?? throw new Exception("Invalid resume row");
                CheckPractice(saved.Contract.FreshFacts(new(881)), saved.Draft);
            }
            rendered.Append(await File.ReadAllTextAsync(Path.Combine(dir, "rendered-samples.txt")));
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        Console.WriteLine("Real proportion GGUF: " + dir);
        try
        {
            await modelSession.LoadAsync(modelPath, timeout.Token);
            // Every reviewed template in both languages, plus every subtype/star/language profile.
            var cases = Cases().GroupBy(c => (c.Language, c.Story!.NarrativeId)).Select(g => g.First()).ToList();
            var additionalIds = new HashSet<(AppLanguage, string)>();
            foreach (var group in Cases().GroupBy(c => (c.Language, c.BankVariant, c.Tier)))
                if (!cases.Any(c => (c.Language, c.BankVariant, c.Tier) == group.Key))
                {
                    // Prose identity ignores stars. Do not consume three profiles of
                    // one two-alternative template merely to cover the star matrix.
                    var c = group.First(c => !additionalIds.Contains((c.Language, c.Story!.NarrativeId))
                        && ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Count() >= 2);
                    additionalIds.Add((c.Language, c.Story!.NarrativeId)); cases.Add(c);
                }
            int number = evidence.Count, maximumTokens = 0;
            foreach (var c in cases)
            {
                string key = $"{c.Language}-{c.Story!.NarrativeId}-{c.Tier}";
                if (completed.Contains(key)) continue;
                var original = ReasoningStoryCatalogue.Draft(c);
                await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "original", DateTime.UtcNow), timeout.Token);
                var excluded = (await store.GetProseHashesAsync(timeout.Token)).ToHashSet(StringComparer.Ordinal);
                string prompt = BasicQuestionPrompt.Build(c, "DuplicateProse", [original.ProblemText], excluded);
                var streamed = new StringBuilder(); AiGenerationMetrics? metrics = null;
                string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, t => streamed.Append(t), m => metrics = m);
                var result = BasicQuestionValidator.Validate(raw, c);
                string name = $"{++number}-{c.Language}-{c.Story!.NarrativeId}-{c.Tier}";
                await File.WriteAllTextAsync(Path.Combine(dir, name + ".txt"), prompt + "\nRAW\n" + raw + "\nVALIDATION\n" + (result.ErrorCode ?? "Valid"));
                evidence.Add(new { Case = name, result.ErrorCode, result.ErrorDetails, Metrics = metrics });
                Check(result.IsValid && metrics?.GeneratedTokens > 1 && raw == streamed.ToString(), "Native generation failed: " + name + " " + result.ErrorCode);
                maximumTokens = Math.Max(maximumTokens, metrics!.GeneratedTokens);
                Check(ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Native prose did not vary facts");
                var saved = new ValidatedBankQuestion(c, result.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                Check(await store.InsertAsync(saved, timeout.Token) && !await store.InsertAsync(saved, timeout.Token), "Native dedup failed");
                var fresh = c.FreshFacts(new(881)); CheckPractice(fresh, result.Draft!);
                var p = ReasoningStoryCatalogue.ToPractice(fresh, result.Draft!, ArithmeticQuizMode.Essay).ProportionProblem!;
                rendered.AppendLine(name).AppendLine(p.ProblemText).AppendLine(p.SolutionText).AppendLine();
                Console.WriteLine($"PASS {name}: {metrics.GeneratedTokens} tokens; native stream/dedup/fresh math/grading");
            }
            await runtime.ReleaseAsync();
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese, 4, true,
                Family: BankQuestionFamily.Proportion, StoryVariant: (int)ProportionQuizType.Inverse));
            try { await worker.Completion.WaitAsync(timeout.Token); }
            catch { worker.Stop(); await worker.Completion; throw; }
            await File.WriteAllTextAsync(Path.Combine(dir, "worker.json"), JsonSerializer.Serialize(worker.Snapshot, new JsonSerializerOptions { WriteIndented = true }));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Worker failed: " + worker.Snapshot.Error);
            foreach (var item in worker.Snapshot.Items) CheckPractice(item.Contract.FreshFacts(new(103 + item.Number)), item.Question!.Draft);
            Check(!runtime.IsLoaded && runtime.CanGenerate, "Worker retained model weights");
            Console.WriteLine($"PASS real worker/save/release; maximum output {maximumTokens}/700 tokens");
        }
        finally
        {
            await modelSession.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(Path.Combine(dir, "rendered-samples.txt"), rendered.ToString());
        }
        Console.WriteLine($"Real model proportion verification passed: {evidence.Count} direct generations; {dir}");
    }
}
