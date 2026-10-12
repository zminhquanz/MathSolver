using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static partial class PackingTimeBankTests
{
    public static async Task RunModelAsync(string path, string directory, string part)
    {
        string dir = Path.GetFullPath(directory); Directory.CreateDirectory(dir);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime; var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));

        await modelSession.LoadAsync(path, timeout.Token);
        if (part == "worker")
        {
            await runtime.ReleaseAsync();
            foreach (var family in new[] { BankQuestionFamily.Remainder, BankQuestionFamily.Time })
            {
                var worker = new AiQuestionGenerationService(runtime, store);
                worker.Start(new(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese, 3, true,
                    Family: family, StoryVariant: (int)(family == BankQuestionFamily.Remainder ? ElementaryQuizType.QuotientRemainder : ElementaryQuizType.ElapsedTime)));
                try { await worker.Completion.WaitAsync(timeout.Token); }
                catch { worker.Stop(); await worker.Completion; throw; }
                await File.WriteAllTextAsync(Path.Combine(dir, family + "-worker.json"), JsonSerializer.Serialize(worker.Snapshot, new JsonSerializerOptions { WriteIndented = true }));
                Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.Count == 3
                    && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Native worker failed: " + worker.Snapshot.Error);
                foreach (var item in worker.Snapshot.Items) CheckPractice(item.Contract.FreshFacts(new(105 + item.Number)), item.Question!.Draft);
                Check(!runtime.IsLoaded && runtime.CanGenerate, "Worker retained model weights");
                Console.WriteLine("PASS native worker " + family + ": 3 saved templates and fresh grading; weights released");
            }
            return;
        }
        bool boundary = part.EndsWith("Boundary", StringComparison.Ordinal);
        var type = Enum.Parse<ElementaryQuizType>(boundary ? part[..^8] : part);
        var familyForType = ElementaryQuizGenerator.RemainderStoryTypes.Contains(type) ? BankQuestionFamily.Remainder : BankQuestionFamily.Time;
        var profiles = new List<(string Name, BasicQuestionContract Contract)>();
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            if (boundary)
            {
                for (int b = 0; b < 3; b++) profiles.Add(($"{type}-{language}-Boundary{b}", Boundary(type, language, b)));
            }
            else foreach (var tier in Enum.GetValues<CurriculumTier>())
            {
                var contexts = Contexts(familyForType, tier, language);
                profiles.Add(($"{type}-{language}-{tier}", ReasoningStoryCatalogue.Create(familyForType, (int)type, tier,
                    language, new(47), contexts[((int)tier - 1) % contexts.Length])));
            }
        }
        foreach (var (name, c) in profiles)
        {
            string file = Path.Combine(dir, name + ".json");
            if (File.Exists(file)) { CheckEvidence(file); Console.WriteLine("REPLAY " + name); continue; }
            var original = ReasoningStoryCatalogue.Draft(c);
            await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "original", DateTime.UtcNow), timeout.Token);
            var excluded = await store.GetProseHashesAsync(timeout.Token);
            string prompt = BasicQuestionPrompt.Build(c, "DuplicateProse", [original.ProblemText], excluded);
            var stream = new StringBuilder(); AiGenerationMetrics? metrics = null;
            string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, t => stream.Append(t), m => metrics = m);
            var result = BasicQuestionValidator.Validate(raw, c);
            Check(result.IsValid && metrics?.GeneratedTokens > 1 && stream.ToString() == raw, "Native JSON/stream failed: " + name + "/" + result.ErrorCode);
            Check(ReviewedReasoningProse.HasNewFacts(c, result.Draft!) && !excluded.Contains(QuestionProseIdentity.Hash(c, result.Draft!)), "Native prose repeated");
            var question = new ValidatedBankQuestion(c, result.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
            CheckPractice(c, question.Draft); CheckPractice(c.FreshFacts(new(881)), question.Draft);
            Check(await store.InsertAsync(question, timeout.Token) && !await store.InsertAsync(question, timeout.Token), "Native dedup failed");
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { Case = name, Contract = c, Raw = raw, Prompt = prompt, Metrics = metrics }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS native {name}: {metrics!.GeneratedTokens} tokens; fresh math/units/all answers/grading/dedup");
        }

    }
}
