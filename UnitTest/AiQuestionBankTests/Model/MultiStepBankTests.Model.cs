using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class MultiStepBankTests
{
    public static async Task RunModelAsync(string path)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", "multistep-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        var evidence = new List<object>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(45));
        Console.WriteLine("Real GGUF multi-step verification; evidence: " + directory);
        try
        {
            await modelSession.LoadAsync(path, timeout.Token);
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.MultiStep))
            {
                // Every subtype at the most demanding tier; additions also at
                // the simplest tier to exercise the forward/reverse structures.
                foreach (var tier in type == ElementaryQuizType.MultiStepAddSubtract
                    ? new[] { CurriculumTier.OneStar, CurriculumTier.FiveStars } : [CurriculumTier.FiveStars])
                {
                    var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.MultiStep, (int)type, tier, language, new(19));
                    var lesson = ReasoningStoryCatalogue.Lesson(c);
                    var original = ReasoningStoryCatalogue.Draft(c);
                    foreach (string question in lesson.QuestionPhrasings)
                    {
                        var draft = original with { Question = question };
                        await store.InsertAsync(new(c, draft, QuestionBankStore.SerializeDraft(draft), "existing-wording", DateTime.UtcNow), timeout.Token);
                    }
                    // Repeated generation over the SAME schema forces genuine
                    // factual rephrasing after SQLite and in-batch exclusions.
                    for (int repeat = 1; repeat <= 2; repeat++)
                    {
                        var excluded = new HashSet<string>(await store.GetProseHashesAsync(timeout.Token), StringComparer.Ordinal);
                        string prompt = BasicQuestionPrompt.Build(c, repeat == 2 ? "DuplicateProse" : null, excludedProse: excluded);
                        var streamed = new StringBuilder();
                        AiGenerationMetrics? metrics = null;
                        string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token,
                            t => streamed.Append(t), m => metrics = m);
                        var result = BasicQuestionValidator.Validate(raw, c);
                        string name = $"{language}-{type}-{tier}-{repeat}";
                        await File.WriteAllTextAsync(Path.Combine(directory, name + ".txt"), prompt + "\n\nRAW\n" + raw
                            + "\nVALIDATION\n" + (result.ErrorCode ?? "Valid") + ": " + result.ErrorDetails, timeout.Token);
                        evidence.Add(new { Case = name, result.ErrorCode, result.ErrorDetails, Metrics = metrics, ContextTokens = runtime.LastContextTokens });
                        Check(result.IsValid, name + " rejected: " + result.ErrorCode);
                        Check(metrics?.GeneratedTokens > 1 && streamed.ToString() == raw, "No real inference or broken streaming.");
                        Check(ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Only a context opening or question text changed.");
                        var saved = new ValidatedBankQuestion(c, result.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                        Check(!await store.ContainsProseAsync(saved, timeout.Token) && await store.InsertAsync(saved, timeout.Token), "Native output repeated SQLite prose.");
                        Check(!await store.InsertAsync(saved, timeout.Token), "Duplicate insertion succeeded.");
                        CheckPractice(c.FreshFacts(new Random(811 + repeat)), saved.Draft);
                        Console.WriteLine($"PASS {name}: {metrics?.GeneratedTokens} tokens, native streaming/SQLite/fresh C# math and essay grading.");
                    }
                }
            }
            // Exercise the actual app job: load once, save a batch, then release.
            await runtime.ReleaseAsync();
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, 4, true,
                Family: BankQuestionFamily.MultiStep, StoryVariant: (int)ElementaryQuizType.MultiStepShare));
            try { await worker.Completion.WaitAsync(timeout.Token); }
            catch { worker.Stop(); await worker.Completion; throw; }
            await File.WriteAllTextAsync(Path.Combine(directory, "worker.json"), JsonSerializer.Serialize(worker.Snapshot,
                new JsonSerializerOptions { WriteIndented = true }));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved),
                "Native batch failed: " + worker.Snapshot.Error);
            foreach (var item in worker.Snapshot.Items)
                CheckPractice(item.Contract.FreshFacts(new Random(902 + item.Number)), item.Question!.Draft);
            Check(!runtime.IsLoaded && runtime.CanGenerate, "Job retained weights or lost model selection.");
            Console.WriteLine("PASS real four-item worker batch, auto-save and model release.");
        }
        finally
        {
            await modelSession.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(evidence,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine("Real multi-step verification passed; evidence: " + directory);
    }
}
