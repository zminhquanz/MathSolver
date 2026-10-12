using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static partial class FractionQuantityBankTests
{
    public static async Task RunModelAsync(string path, string directory)
    {
        string dir = Path.GetFullPath(directory); Directory.CreateDirectory(dir);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime; var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromHours(2));

        await modelSession.LoadAsync(path, timeout.Token);
        // Full bilingual math/validator matrix runs in RunAsync. Native sampling
        // covers both types/languages and every current context, with stratified stars.
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var type in ElementaryQuizGenerator.FractionQuantityStoryTypes)
        {
            var contexts = FractionQuantityStoryContextCatalog.GetProfile(language);
            for (int i = 0; i < contexts.Count; i++)
            {
                var tier = (CurriculumTier)(1 + i % 5);
                var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.FractionQuantity, (int)type, tier, language, new(47), contexts[i].ContextId);
                string name = $"{type}-{language}-{tier}-{contexts[i].ContextId}", file = Path.Combine(dir, name + ".json");
                if (File.Exists(file)) { CheckEvidence(file); Console.WriteLine("REPLAY " + name); continue; }
                var original = ReasoningStoryCatalogue.Draft(c);
                await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "original", DateTime.UtcNow), timeout.Token);
                var excluded = await store.GetProseHashesAsync(timeout.Token);
                string prompt = BasicQuestionPrompt.Build(c, "DuplicateProse", [original.ProblemText], excluded);
                var stream = new StringBuilder(); AiGenerationMetrics? metrics = null;
                string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, t => stream.Append(t), m => metrics = m);
                var result = BasicQuestionValidator.Validate(raw, c);
                Check(result.IsValid && metrics?.GeneratedTokens > 1 && stream.ToString() == raw, "Native JSON/stream failed: " + name + "/" + result.ErrorCode);
                Check(ReviewedReasoningProse.HasNewFacts(c, result.Draft!) && !excluded.Contains(QuestionProseIdentity.Hash(c, result.Draft!)), "Native repeated facts");
                CheckPractice(c, result.Draft!); CheckPractice(c.FreshFacts(new(881)), result.Draft!); CheckUnsafe(c, original);
                var question = new ValidatedBankQuestion(c, result.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                Check(await store.InsertAsync(question, timeout.Token) && !await store.InsertAsync(question, timeout.Token), "Native SQLite dedup failed");
                await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { Case = name, Contract = c, Raw = raw, Prompt = prompt, Metrics = metrics }, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"PASS native {name}: {metrics!.GeneratedTokens} tokens; fresh math/units/grading/dedup");
            }
        }
        await runtime.ReleaseAsync();
        foreach (var type in ElementaryQuizGenerator.FractionQuantityStoryTypes)
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            string file = Path.Combine(dir, $"worker-{type}-{language}.json");
            if (File.Exists(file)) continue;
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.FiveStars, language, 2, true,
                Family: BankQuestionFamily.FractionQuantity, StoryVariant: (int)type));
            try { await worker.Completion.WaitAsync(timeout.Token); }
            catch { worker.Stop(); await worker.Completion; throw; }
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.Count == 2
                && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Native worker failed: " + worker.Snapshot.Error);
            foreach (var item in worker.Snapshot.Items) CheckPractice(item.Contract.FreshFacts(new(105 + item.Number)), item.Question!.Draft);
            Check(!runtime.IsLoaded && runtime.CanGenerate, "Worker retained model weights");
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(worker.Snapshot, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS native worker {type}/{language}: 2 saved templates; fresh grading and released weights");
        }

        await CheckEvidenceAsync(dir);
    }
}
