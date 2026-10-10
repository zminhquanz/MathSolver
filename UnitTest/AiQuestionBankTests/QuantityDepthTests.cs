using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Text;
using System.Text.Json;

internal static class QuantityDepthTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    internal static IEnumerable<BasicQuestionContract> NativeCases()
    {
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            int i = 0;
            foreach (var type in ElementaryQuizGenerator.DecimalStoryTypes)
            {
                var language = ((int)tier + i) % 2 == 0 ? AppLanguage.English : AppLanguage.Vietnamese;
                string context = new[] { "sewing-uniforms", "juice-bottling", "feed-preparation", "rope-cutting", "rice-packing", "bakery-batches" }[((int)tier + i++) % 6];
                yield return ReasoningStoryCatalogue.Create(BankQuestionFamily.Decimal, (int)type, tier, language, new(71), context);
            }
            foreach (var type in ElementaryQuizGenerator.FractionQuantityStoryTypes)
            {
                var language = type == ElementaryQuizType.FractionOfNumber ? AppLanguage.Vietnamese : AppLanguage.English;
                string context = new[] { "reading-pages", "bread-flour", "juice-capacity", "sports-registration", "route-distance" }[(int)tier - 1];
                yield return ReasoningStoryCatalogue.Create(BankQuestionFamily.FractionQuantity, (int)type, tier, language, new(71), context);
            }
        }
    }

    public static async Task RunAsync()
    {
        int count = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var family in new[] { BankQuestionFamily.Decimal, BankQuestionFamily.FractionQuantity })
        foreach (int variant in ReasoningStoryCatalogue.Variants(family, tier))
        {
            var contexts = family == BankQuestionFamily.Decimal
                ? ElementaryQuizGenerator.DecimalContexts(language).Select(c => c.Id)
                : FractionQuantityStoryContextCatalog.GetProfile(language).Select(c => c.ContextId);
            foreach (string context in contexts)
            {
                var c = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(63), context);
                Verify(c); count++;
                bool old = family == BankQuestionFamily.Decimal
                    ? ElementaryQuizGenerator.DecimalContexts(language, false).Any(c => c.Id == context)
                    : FractionQuantityStoryContextCatalog.GetProfile(language, false).Any(c => c.ContextId == context);
                if (!old) continue;
                foreach (int version in new[] { 0, 1 })
                {
                    var legacy = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(63), context, contextVersion: version);
                    Verify(legacy); count++;
                }
            }
        }
        // A number must not change the template's identity; SQLite and Excel
        // must retain the new activity and the catalogue replay version.
        string directory = Path.GetFullPath("artifacts/verification/quantity-depth-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var store = new QuestionBankStore(Path.Combine(directory, "bank.db3"));
        foreach (var c in NativeCases())
        {
            var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
            Check(await store.InsertAsync(new(c, draft, QuestionBankStore.SerializeDraft(draft), "test", DateTime.UtcNow)), "Insert failed");
            Check(!await store.InsertAsync(new(c.FreshFacts(new(92)), draft, QuestionBankStore.SerializeDraft(draft), "test", DateTime.UtcNow)), "Changed numbers bypassed duplicate detection");
        }
        using var excel = new MemoryStream();
        int exported = (await store.ExportExcelAsync(excel)).Exported;
        excel.Position = 0;
        var restored = new QuestionBankStore(Path.Combine(directory, "import.db3"));
        Check((await restored.ImportExcelAsync(excel)).Inserted == exported && exported == 30, "Excel lost quantity stories");
        foreach (var c in NativeCases())
        {
            var saved = await restored.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
            Check(saved?.Contract.Story?.ContextVersion == NarrativeContextExpansion.Version, "Lost quantity catalogue version");
            ProseExpansionTests.VerifyPractice(saved!.Contract.FreshFacts(new(31)), saved.Draft);
        }
        Console.WriteLine($"Quantity depth: PASS {count} current/legacy profiles, 1–5 stars, Vietnamese/English; conservation, capacities, semantic rejection, grading, SQLite and Excel.");
    }

    private static void Verify(BasicQuestionContract c)
    {
        string label = $"{c.Family}/{c.BankVariant}/{c.Tier}/{c.Language}/{c.SceneId}/v{c.Story!.ContextVersion}";
        try
        {
            Check(c.IsValid, "Invalid contract");
            var original = ReasoningStoryCatalogue.Draft(c);
            var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(original), c).IsValid, "Base rejected");
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Reviewed wording rejected");
            ProseExpansionTests.VerifyPractice(c, draft);
            ProseExpansionTests.VerifyPractice(c.FreshFacts(new(19)), draft);
            _ = ReasoningStoryValidator.Grammar(c);
            var foreign = draft with { Question = draft.Question + " łącznie" };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(foreign), c).IsValid, "Foreign word accepted");
            var changed = draft with { Facts = draft.Facts!.Select(f => f with { Text = f.Text.Replace("{f0}", "{f999}") }).ToArray() };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(changed), c).IsValid, "Wrong quantity role accepted");
            if (c.Family == BankQuestionFamily.Decimal)
            {
                var context = ElementaryQuizGenerator.DecimalContexts(c.Language).Single(x => x.Id == c.SceneId);
                var q = ReasoningStoryCatalogue.Lesson(c).QuestionModel.ElementaryProblem!;
                var g = q.Reasoning!.Givens.ToDictionary(x => x.Role, x => decimal.Parse(x.Value, CultureInfo.InvariantCulture));
                decimal amount = g["quantity"] / ((int)c.Tier == 5 ? context.ConversionFactor : 1);
                if (context.GroupMaximum > 0 && q.Type is ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide)
                {
                    if (q.Type == ElementaryQuizType.DecimalDivide) amount /= g.GetValueOrDefault("portion-count", g.GetValueOrDefault("second-quantity"));
                    if ((int)c.Tier >= 3) amount += q.Type == ElementaryQuizType.DecimalMultiply ? .5m : .5m + ((int)c.Tier >= 4 ? .25m : 0);
                    Check(amount <= context.GroupMaximum, "Per-group capacity exceeded");
                }
                var answer = c.ExactAnswer;
                Check(answer.Numerator > 0 && answer.Numerator <= context.MaximumQuantity * answer.Denominator, "Total capacity exceeded");
            }
            else if (c.Tier == CurriculumTier.FourStars && c.Story.ContextVersion >= 2)
            {
                var givens = ReasoningStoryCatalogue.Lesson(c).QuestionModel.ElementaryProblem!.Reasoning!.Givens
                    .ToDictionary(g => g.Role, g => decimal.Parse(g.Value, CultureInfo.InvariantCulture));
                Check(givens["removed"] <= Math.Min(givens["quantity-first"], givens["quantity-second"]), "Impossible overlapping records");
            }
        }
        catch (Exception error) { throw new InvalidOperationException(label, error); }
    }

    public static async Task RunModelAsync(string model)
    {
        string directory = Path.GetFullPath("artifacts/verification/quantity-depth-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(directory);
        Console.WriteLine("Real GGUF evidence: " + directory);
        var runtime = new GgufQuestionRuntime();
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        var results = new List<object>();
        var failures = new List<string>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(45));
        try
        {
            await runtime.LoadAsync(model, cancellation.Token);
            Console.WriteLine($"CPU threads {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
            foreach (var c in NativeCases())
            {
                string name = $"{c.Family}-{c.BankVariant}-{(int)c.Tier}-{c.Language}";
                await File.WriteAllTextAsync(Path.Combine(directory, name + "-contract.json"), JsonSerializer.Serialize(c));
                bool passed = false;
                string? correction = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction);
                    var streamed = new StringBuilder();
                    AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateAsync(c, prompt, cancellation.Token, part => streamed.Append(part), value => metrics = value);
                    Check(streamed.ToString() == raw && metrics?.GeneratedTokens > 0, "Missing native streamed tokens");
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    await File.WriteAllTextAsync(Path.Combine(directory, name + "-" + attempt + ".txt"), prompt + "\n\n" + raw + "\n\n" + validation.ErrorCode + " " + validation.ErrorDetails);
                    results.Add(new { Case = name, Attempt = attempt, validation.ErrorCode, validation.ErrorDetails, Metrics = metrics });
                    Console.WriteLine($"{name}: attempt={attempt} {validation.ErrorCode ?? "Valid"}; {metrics!.GeneratedTokens} tokens, {metrics.TokensPerSecond:F2} token/s");
                    if (validation.IsValid)
                    {
                        var draft = validation.Draft!;
                        ProseExpansionTests.VerifyPractice(c, draft);
                        Check(await store.InsertAsync(new(c, draft, raw, Path.GetFileName(model), DateTime.UtcNow)), "Native insert failed");
                        var saved = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
                        Check(saved is not null, "Native read failed");
                        ProseExpansionTests.VerifyPractice(saved!.Contract.FreshFacts(new(92)), saved.Draft);
                        passed = true; break;
                    }
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
        Check(!runtime.IsLoaded, "Native weights retained");
        Check(failures.Count == 0, "Real model failures: " + string.Join(", ", failures));
        Console.WriteLine($"PASS 30 native profiles / {results.Count} attempts; all five tiers, both languages, streaming, SQLite, fresh exact math, grading and disposal.");
    }
}
