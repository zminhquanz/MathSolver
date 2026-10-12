using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static partial class MotionBankTests
{
    public static async Task RunModelAsync(string modelPath)
    {
        string dir = Path.GetFullPath(Path.Combine("artifacts", "verification", "motion-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(dir);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var evidence = new List<object>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        Console.WriteLine("Real GGUF motion verification: " + dir);
        try
        {
            await modelSession.LoadAsync(modelPath, timeout.Token);
            var cases = Cases(1).ToList();
            // Also cover any fine-grained target not selected by the first seed.
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            foreach (var kind in Enum.GetValues<MotionQuestionKind>())
                if (!cases.Any(c => c.Language == language && ReasoningStoryCatalogue.Lesson(c).QuestionModel.MotionProblem!.QuestionKind == kind))
                    cases.Add(Cases(20).First(c => c.Language == language && ReasoningStoryCatalogue.Lesson(c).QuestionModel.MotionProblem!.QuestionKind == kind));
            int number = 0;
            foreach (var c in cases)
            {
                var original = ReasoningStoryCatalogue.Draft(c);
                await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "original", DateTime.UtcNow), timeout.Token);
                int repeats = c.Tier == CurriculumTier.FiveStars ? 2 : 1;
                for (int repeat = 0; repeat < repeats; repeat++)
                {
                    var excluded = (await store.GetProseHashesAsync(timeout.Token)).ToHashSet(StringComparer.Ordinal);
                    string prompt = BasicQuestionPrompt.Build(c, repeat == 0 ? null : "DuplicateProse", excludedProse: excluded);
                    var native = await modelSession.GenerateAsync(c, prompt, timeout.Token, excluded);
                    string raw = native.Raw; var metrics = native.Metrics; var result = native.Validation;
                    string name = $"{++number}-{c.Language}-{(MotionQuizType)c.BankVariant}-{c.Tier}-{repeat}";
                    await File.WriteAllTextAsync(Path.Combine(dir, name + ".txt"), prompt + "\nRAW\n" + raw + "\nVALIDATION\n" + (result.ErrorCode ?? "Valid"));
                    evidence.Add(new { Case = name, Kind = ReasoningStoryCatalogue.Lesson(c).QuestionModel.MotionProblem!.QuestionKind.ToString(), result.ErrorCode, result.ErrorDetails, Metrics = metrics });
                    Check(result.IsValid && metrics?.GeneratedTokens > 1, "Native motion rejected: " + name + " " + result.ErrorCode);
                    Check(ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Native motion did not change facts");
                    var saved = new ValidatedBankQuestion(c, result.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                    Check(await store.InsertAsync(saved, timeout.Token) && !await store.InsertAsync(saved, timeout.Token), "Native SQLite dedup failed");
                    CheckPractice(c.FreshFacts(new(881 + repeat)), result.Draft!);
                    Console.WriteLine($"PASS {name}: {metrics?.GeneratedTokens} tokens; native stream/SQLite/fresh math/grading");
                }
            }
            await runtime.ReleaseAsync();
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, 4, true,
                Family: BankQuestionFamily.Motion, StoryVariant: (int)MotionQuizType.Meeting));
            try { await worker.Completion.WaitAsync(timeout.Token); }
            catch { worker.Stop(); await worker.Completion; throw; }
            await File.WriteAllTextAsync(Path.Combine(dir, "worker.json"), JsonSerializer.Serialize(worker.Snapshot, new JsonSerializerOptions { WriteIndented = true }));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Motion worker failed: " + worker.Snapshot.Error);
            foreach (var item in worker.Snapshot.Items) CheckPractice(item.Contract.FreshFacts(new(103 + item.Number)), item.Question!.Draft);
            Check(!runtime.IsLoaded && runtime.CanGenerate, "Motion worker retained model");
            Console.WriteLine("PASS real motion worker batch/save/release");
        }
        finally
        {
            await modelSession.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine("Real motion model verification passed: " + evidence.Count + " direct generations; " + dir);
        await WriteRenderedEvidenceAsync(dir);
    }
}
