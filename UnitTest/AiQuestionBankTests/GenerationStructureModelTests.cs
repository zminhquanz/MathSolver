using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Services.Core;
using System.Text;
using System.Text.Json;

internal static class GenerationStructureModelTests
{
    public static async Task RunWorkerAsync(string path)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", "structure-worker-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        var runtime = new GgufQuestionRuntime();
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        var worker = new AiQuestionGenerationService(runtime, store);
        var essay = new EssayAnswerValidator(new BasicArithmeticEngine());
        int saved = 0, streamingUpdates = 0;
        worker.Changed += (_, _) =>
        {
            if (worker.Snapshot.Items.Any(i => i.Attempts.Any(a => !a.IsComplete && a.RawJson.Length > 0)))
                Interlocked.Increment(ref streamingUpdates);
        };
        Console.WriteLine("Native batch/SQLite verification; evidence: " + directory);
        try
        {
            await runtime.SelectAsync(path);
            var jobs = new[] {
                new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 4, true,
                    new(QuestionKnowledgeGroup.Objects), BankQuestionFamily.Fraction),
                new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.TwoStars, AppLanguage.English, 2, true,
                    new(QuestionKnowledgeGroup.Objects), BankQuestionFamily.Fraction),
                new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese, 2, true,
                    Family: BankQuestionFamily.Average, StoryVariant: 4) };
            for (int index = 0; index < jobs.Length; index++)
            {
                var options = jobs[index];
                worker.Start(options);
                try { await worker.Completion.WaitAsync(TimeSpan.FromMinutes(12)); }
                catch (TimeoutException) { worker.Stop(); await worker.Completion; throw; }
                var snapshot = worker.Snapshot;
                await File.WriteAllTextAsync(Path.Combine(directory, $"batch-{index + 1}.json"),
                    JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
                if (snapshot.State != AiJobState.Completed || snapshot.Items.Count != options.Count
                    || snapshot.Items.Any(i => i.State != AiItemState.Saved))
                    throw new Exception("Native batch failed: " + snapshot.Error);
                if (runtime.IsLoaded || !runtime.CanGenerate) throw new Exception("Weights were not released or model selection was lost");
                foreach (var item in snapshot.Items)
                {
                    if (!item.Attempts.Any(a => a.Metrics?.GeneratedTokens > 1) || item.Question is null)
                        throw new Exception("Missing generation metrics or saved template");
                    var stored = options.Family == BankQuestionFamily.Fraction
                        ? await store.TakeFractionAsync(options.Operation, options.Tier, options.Language, options.Profile!)
                        : await store.TakeReasoningAsync(options.Family, options.StoryVariant, options.Tier, options.Language);
                    if (stored is null) throw new Exception("Native template cannot be read back from SQLite");
                    var fresh = stored.Contract.FreshFacts(new Random(503 + item.Number));
                    if (!fresh.IsValid || !BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(stored.Draft), fresh).IsValid)
                        throw new Exception("Saved prose does not support fresh C# facts");
                    if (options.Family == BankQuestionFamily.Fraction)
                    {
                        var word = stored.Draft.ToWordProblem(fresh);
                        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                        {
                            var practice = fresh.ToPracticeQuestion(word, mode, new Random(17));
                            if (practice.ExactAnswer != FractionQuestionCatalogue.Answer(fresh)) throw new Exception("Practice math changed");
                            if (mode == ArithmeticQuizMode.Essay && !essay.Validate(practice, word.SolutionLead,
                                $"{FractionQuestionCatalogue.Expression(fresh)} = {fresh.AnswerText} {fresh.Unit}",
                                $"{fresh.AnswerText} {fresh.Unit}").IsCorrect) throw new Exception("Native prose fails essay grading");
                        }
                    }
                    else
                        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                            _ = ReasoningStoryCatalogue.ToPractice(fresh, stored.Draft, mode);
                    saved++;
                    Console.WriteLine($"PASS batch {index + 1}/{item.Number}: {item.Contract.SceneId}, attempts={item.Attempts.Count}");
                }
            }
            using var workbook = new MemoryStream();
            var exported = await store.ExportExcelAsync(workbook);
            if (exported.Exported != saved || streamingUpdates == 0) throw new Exception("Saved row count or streaming mismatch");
            Console.WriteLine($"PASS {saved} native batch templates saved/read/freshened, {streamingUpdates} streaming updates, weights released after each batch.");
        }
        finally { await runtime.EjectAsync(); }
    }

    public static async Task RunAsync(string path, bool reproductionOnly = false)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", "structure-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        var runtime = new GgufQuestionRuntime();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        var failures = new List<string>();
        var results = new List<object>();
        Console.WriteLine("Native structure verification; evidence: " + directory);
        try
        {
            await runtime.LoadAsync(path, timeout.Token);
            var cases = Cases().ToArray();
            if (reproductionOnly) cases = cases.Take(1).ToArray();
            foreach (var (name, c) in cases)
            {
                string? correction = null;
                bool passed = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction);
                    var streamed = new StringBuilder();
                    AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateAsync(c, prompt, timeout.Token, t => streamed.Append(t), m => metrics = m);
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    if (streamed.ToString() != raw) throw new Exception("Streaming differs from the final output");
                    string details = validation.ErrorDetails ?? DescribeMismatch(raw, c);
                    await File.WriteAllTextAsync(Path.Combine(directory, $"{name}-{attempt}.txt"),
                        prompt + "\n\n" + raw + "\n\n" + (validation.ErrorCode ?? "Valid") + "\n" + details, timeout.Token);
                    results.Add(new { Case = name, Attempt = attempt, Error = validation.ErrorCode, ErrorDetails = details, Metrics = metrics });
                    Console.WriteLine($"{name} attempt {attempt}: {validation.ErrorCode ?? "Valid"}; {details}");
                    if (validation.IsValid) { passed = true; break; }
                    correction = validation.ErrorCode;
                }
                if (!passed) failures.Add(name);
            }
        }
        finally
        {
            await runtime.EjectAsync();
            await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failures.Count > 0) throw new Exception("Native structure failures: " + string.Join(", ", failures) + "; evidence: " + directory);
        Console.WriteLine("All native structure cases passed; evidence: " + directory);
    }

    private static IEnumerable<(string Name, BasicQuestionContract Contract)> Cases()
    {
        yield return ("fraction-bread-add-1-vi", FractionQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects),
            ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, new(19), "fraction-bread-Add"));
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var (operation, tier, group) in new[] {
            (ArithmeticOperation.Add, CurriculumTier.OneStar, QuestionKnowledgeGroup.Objects),
            (ArithmeticOperation.Subtract, CurriculumTier.ThreeStars, QuestionKnowledgeGroup.Money),
            (ArithmeticOperation.Multiply, CurriculumTier.FourStars, QuestionKnowledgeGroup.Measurement),
            (ArithmeticOperation.Divide, CurriculumTier.FiveStars, QuestionKnowledgeGroup.Geometry) })
        {
            if (language == AppLanguage.Vietnamese && operation == ArithmeticOperation.Add) continue;
            yield return ($"fraction-{operation}-{(int)tier}-{language}", FractionQuestionCatalogue.Create(new(group), operation, tier, language, new(19)));
        }
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            yield return ($"applied-saving-3-{language}", AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Money),
                ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, new(19), "saving-total"));
            yield return ($"findx-total-4-{language}", FindXQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects),
                ArithmeticOperation.Subtract, CurriculumTier.FourStars, language, new(19), FindXUnknownRole.Minuend));
        }
        foreach (var family in new[] { BankQuestionFamily.TwoNumbers, BankQuestionFamily.Average, BankQuestionFamily.Percentage })
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            yield return ($"{family}-5-{language}", ReasoningStoryCatalogue.Create(family,
                family == BankQuestionFamily.TwoNumbers ? 2 : family == BankQuestionFamily.Average ? 4 : 2,
                CurriculumTier.FiveStars, language, new(19)));
    }

    private static string DescribeMismatch(string raw, BasicQuestionContract c)
    {
        var pool = ReviewedQuestionProse.For(c);
        if (pool is null) return "";
        try
        {
            using var document = JsonDocument.Parse(raw);
            return string.Join(", ", new[] { ("given_a", pool.GivenA), ("given_b", pool.GivenB), ("question", pool.Questions), ("solution_lead", pool.Leads) }
                .Where(pair => !SemanticProseRules.Matches(document.RootElement.GetProperty(pair.Item1).GetString(), pair.Item2, c.Language))
                .Select(pair => pair.Item1 + " differs from semantic anchors"));
        }
        catch (JsonException) { return "Malformed JSON"; }
    }
}
