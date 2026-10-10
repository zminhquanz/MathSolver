using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Text;
using System.Text.Json;

internal static class NextAiFormsTests
{
    internal static readonly ElementaryQuizType[] Types = [ElementaryQuizType.ReadPictograph,
        ElementaryQuizType.SortData, ElementaryQuizType.CompleteBarChart, ElementaryQuizType.MapScale,
        ElementaryQuizType.Likelihood, ElementaryQuizType.ExperimentalProbability];
    static void Check(bool valid, string reason) { if (!valid) throw new InvalidOperationException(reason); }
    static BankQuestionFamily Family(ElementaryQuizType type) => type == ElementaryQuizType.MapScale
        ? BankQuestionFamily.Measurement : type is ElementaryQuizType.Likelihood or ElementaryQuizType.ExperimentalProbability
        ? BankQuestionFamily.Probability : BankQuestionFamily.Data;

    static void Verify(BasicQuestionContract c, BasicQuestionDraft? draft = null)
    {
        Check(c.IsValid, "Invalid new form " + c.Family + "/" + c.BankVariant);
        draft ??= ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
        Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Invalid reviewed prose");
        ProseExpansionTests.VerifyPractice(c, draft);
        var p = ReasoningStoryCatalogue.ToPractice(c, draft, ArithmeticQuizMode.Essay).ElementaryProblem!;
        var g = p.Reasoning!.Givens.ToDictionary(x => x.Role, x => decimal.Parse(x.Value, CultureInfo.InvariantCulture));
        decimal F(string role) => g.GetValueOrDefault(role);
        decimal Value(int i) => (decimal)p.Answers[i].Value.Numerator / (decimal)p.Answers[i].Value.Denominator;
        int level = (int)c.Tier;
        if (c.Family == BankQuestionFamily.Data)
        {
            DataChartQuestionValidator.Validate(p);
            var profile = p.DataChart!.Profile;
            int Row(string id) => profile.CategoryIds.ToList().IndexOf(id);
            if (p.Type == ElementaryQuizType.ReadPictograph)
            {
                Check(p.Visual!.Values.Select((n,i) => n == F("icons-" + i) * F("key")).All(x => x), "Detached pictograph legend");
                decimal expected = level <= 2 ? p.Visual.Values[Row(profile.TargetCategoryIds[0])]
                    : level == 3 ? p.Visual.Values[0] + p.Visual.Values[1] : level == 4 ? p.Visual.Values.Sum()
                    : p.Visual.Values[0] + p.Visual.Values[1] - p.Visual.Values[2];
                Check(Value(0) == expected && p.Visual.PictographKey == F("key"), "Pictograph answer/key mismatch");
                Reject(() => DataChartQuestionValidator.Validate(p with { Visual = p.Visual with { PictographKey = F("key") + 1 } }), "Changed legend accepted");
            }
            else
            {
                var batches = p.DataChart.Observations!;
                decimal[] counts = profile.CategoryIds.Select(id => (decimal)batches.Sum(batch =>
                    (batch.Excluded ? -1 : 1) * batch.CategoryIds.Count(item => item == id))).ToArray();
                Check(counts.SequenceEqual(p.Visual!.Values) && counts.All(n => n > 0), "Observations disagree with visual");
                Check(level < 4 || batches.Single(b => b.Excluded).CategoryIds.Count > 0, "Missing exclusions");
                Check(p.Type == ElementaryQuizType.SortData ? counts.SequenceEqual(p.Answers.Select((_, i) => Value(i)))
                    : counts[Row(profile.TargetCategoryIds[0])] == Value(0), "Classification/missing bar target changed");
                Check(p.Visual.HiddenValueIndices!.Count == (p.Type == ElementaryQuizType.SortData ? 3 : 1), "Answer shown before grading");
                Reject(() => DataChartQuestionValidator.Validate(p with { DataChart = p.DataChart with {
                    Observations = batches.Select((b, i) => i == 0 ? b with { Excluded = true } : b).ToArray() } }), "Changed observation role accepted");
            }
            Reject(() => DataChartQuestionValidator.Validate(p with { Visual = p.Visual! with {
                Values = p.Visual!.Values.Select(n => n + 1).ToArray() } }), "Changed visual values accepted");
        }
        else if (p.Type == ElementaryQuizType.MapScale)
        {
            decimal expected = level == 5 ? (F("actual-km") * 100000 + F("actual-m") * 100) / F("scale-denominator")
                : (F("map-cm") + F("second-map-cm")) * F("scale-denominator") / (level <= 2 ? 100 : 100000);
            Check(Value(0) == expected && p.Answers[0].Unit == (level == 5 ? "cm" : level <= 2 ? "m" : "km"), "Map direction/unit mismatch");
            var context = ElementaryQuizGenerator.MapScaleContexts(c.Language).Single(x => x.Id == c.SceneId);
            Check(level >= context.MinimumStar && level <= context.MaximumStar && F("scale-denominator") <= context.MaximumScale, "Unrealistic map context");
        }
        else
        {
            var scenario = p.ProbabilityScenario!;
            Check(scenario.EventCount >= 0 && scenario.EventCount <= scenario.TotalCount && scenario.TotalCount > 0, "Invalid experiment");
            if (p.Type == ElementaryQuizType.Likelihood)
            {
                int n = (int)F("outcomes"), k = (int)F("favorable");
                if (level == 5) { n -= (int)F("removed-favorable"); k -= (int)F("removed-favorable"); }
                int total = level <= 2 ? n : level == 3 ? n*n : n*(n-1);
                int events = level == 1 ? k : level == 2 ? n-k : level == 3 ? k*k : k*(k-1);
                Check(!scenario.UsesObservedResults && scenario.TotalCount == total && scenario.EventCount == events, "Sampling/replacement changed");
                Check(p.Answers[0].Text == QuizContentCatalog.Text(c.Language,
                    "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty." + (events == 0 ? "008" : events == total ? "009" : "010")), "Wrong classification");
                Check(!string.IsNullOrWhiteSpace(p.Reasoning.Explanation) && p.SolutionText.Contains(p.Reasoning.Explanation), "Missing classification explanation");
            }
            else
            {
                var totalRoles = g.Keys.Where(k => k.StartsWith("total-", StringComparison.Ordinal)).ToArray();
                decimal observed = totalRoles.Sum(role => { string suffix = role[6..]; return g.TryGetValue("success-" + suffix, out var success)
                    ? success : g[role] - g["failures-" + suffix]; });
                decimal total = totalRoles.Sum(F) - F("invalid-failures");
                bool complement = scenario.EventText.Contains(c.Language == AppLanguage.Vietnamese ? "không xảy ra" : "does not occur");
                decimal numerator = complement ? total - observed : observed;
                Check(scenario.UsesObservedResults && scenario.TotalCount == total && scenario.EventCount == numerator
                    && p.Answers[0].Value == new ReducedFraction((int)numerator, (int)total), "Observed frequency changed");
            }
        }
    }

    static void Reject(Action action, string reason)
    { try { action(); } catch (InvalidDataException) { return; } throw new InvalidOperationException(reason); }

    public static async Task RunAsync()
    {
        int cases = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in Types)
        {
            var family = Family(type);
            var contexts = family == BankQuestionFamily.Data ? DataChartStoryContextCatalog.GetProfile(language).Select(x => x.ContextId)
                : type == ElementaryQuizType.MapScale ? ElementaryQuizGenerator.MapScaleContexts(language).Where(x => x.MinimumStar <= (int)tier && x.MaximumStar >= (int)tier).Select(x => x.Id)
                : new[] { "" };
            foreach (string context in contexts)
            for (int seed = 0; seed < (family == BankQuestionFamily.Probability ? 24 : 2); seed++)
            {
                DataChartProfile? profile = family == BankQuestionFamily.Data ? new ElementaryQuizGenerator(new(seed)).GenerateDataChart(
                    ArithmeticQuizMode.Essay, type, language, tier, context).ElementaryProblem!.DataChart!.Profile : null;
                var c = ReasoningStoryCatalogue.Create(family, (int)type, tier, language, new(seed),
                    narrativeId: family == BankQuestionFamily.Measurement ? context : "", chartProfile: profile);
                Verify(c); Verify(c.FreshFacts(new(seed + 717))); _ = ReasoningStoryValidator.Grammar(c);
                var d = ReasoningStoryCatalogue.Draft(c);
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d with { Question = d.Question + " łącznie" }), c).IsValid, "Foreign prose accepted");
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d with { Question = d.Question + " 123" }), c).IsValid, "Literal answer accepted");
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d with { Question = d.Question + (language == AppLanguage.Vietnamese ? " Không hoàn lại." : " Without replacement.") }), c).IsValid, "Extra condition accepted");
                cases++;
            }
        }
        string directory = Path.GetFullPath("artifacts/verification/next-ai-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var store = new QuestionBankStore(Path.Combine(directory, "bank.db3"));
        int saved = 0;
        foreach (var c in NativeCases(bothLanguages: true))
        {
            var d = ReviewedReasoningProse.NovelDrafts(c, await store.GetProseHashesAsync()).First();
            Check(await store.InsertAsync(new(c, d, QuestionBankStore.SerializeDraft(d), "reviewed", DateTime.UtcNow)), "Save failed: " + c.Family + "/" + c.BankVariant + "/" + c.Tier + "/" + c.Language);
            var restored = c.Family == BankQuestionFamily.Data ? await store.TakeChartAsync(c.Story!.ChartProfile!)
                : await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
            Check(restored is not null, "Read failed"); Verify(restored!.Contract.FreshFacts(new(319)), restored.Draft);
            var q = ReasoningStoryCatalogue.ToPractice(c, ReasoningStoryCatalogue.Draft(c), ArithmeticQuizMode.Essay);
            var selected = await new BasicPracticeQuestionProvider(store, new BankRandom()).SelectReasoningAsync(q, c.Tier, c.Language);
            Check(selected.ElementaryProblem!.ProblemText != q.ElementaryProblem!.ProblemText, "Practice did not select AI prose");
            saved++;
        }
        using var excel = new MemoryStream(); Check((await store.ExportExcelAsync(excel)).Exported == saved, "Excel lost records");
        excel.Position = 0; var imported = new QuestionBankStore(Path.Combine(directory, "import.db3"));
        Check((await imported.ImportExcelAsync(excel)).Inserted == saved, "Excel import failed");
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in new[] { CurriculumTier.OneStar, CurriculumTier.FiveStars })
        {
            var excluded = new HashSet<string>();
            foreach (var scene in ElementaryQuizGenerator.MapScaleContexts(language)
                .Where(scene => scene.MinimumStar <= (int)tier && scene.MaximumStar >= (int)tier))
            {
                var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement, (int)ElementaryQuizType.MapScale,
                    tier, language, new(11), scene.Id);
                foreach (var draft in ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()))
                    excluded.Add(QuestionProseIdentity.Hash(c, draft));
            }
            try
            {
                _ = new ReasoningStoryCycle().Next(BankQuestionFamily.Measurement, (int)ElementaryQuizType.MapScale, tier, language, excluded);
                throw new InvalidOperationException("Exhausted map catalogue reused old prose");
            }
            catch (InvalidOperationException error) when (error.Message == "DuplicateProseRetriesExhausted") { }
        }
        Console.WriteLine($"PASS next AI forms: {cases} profiles, 1–5 stars, VI/EN, independent math, visual integrity, grammar, fresh grading; {saved} SQLite/Excel records.");
    }

    static IEnumerable<BasicQuestionContract> NativeCases(bool bothLanguages = false)
    {
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int index = 0; index < Types.Length; index++)
        foreach (var language in bothLanguages ? new[] { AppLanguage.Vietnamese, AppLanguage.English }
            : new[] { ((int)tier + index) % 2 == 0 ? AppLanguage.English : AppLanguage.Vietnamese })
            yield return ReasoningStoryCatalogue.Create(Family(Types[index]), (int)Types[index], tier, language, new(171 + index));
    }

    public static void CheckModelEvidence(string directory)
    {
        using var database = new SQLite.SQLiteConnection(Path.Combine(directory, "model.db3"), SQLite.SQLiteOpenFlags.ReadOnly);
        var rows = database.Query<QuestionBankStore.Row>("SELECT * FROM BasicQuestionBank");
        int count = 0;
        foreach (var expected in NativeCases())
        {
            var row = rows.Single(row => row.ProblemType == (int)expected.Family && row.ProblemVariant == expected.BankVariant
                && row.Stars == (int)expected.Tier && row.Language == (int)expected.Language);
            var c = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson)!;
            Check(row.ModelName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) && c.Story!.Schema == expected.Story!.Schema,
                "Evidence did not come from the expected native profile");
            var validation = BasicQuestionValidator.Validate(row.RawJson, c);
            Check(validation.IsValid, "Stored native output no longer validates");
            Verify(c, validation.Draft); Verify(c.FreshFacts(new(821)), validation.Draft);
            count++;
        }
        Console.WriteLine($"PASS {count} native SQLite outputs against current source, fresh math and all grading modes; read-only evidence replay.");
    }

    public static async Task RunModelAsync(string model, string? evidenceDirectory = null)
    {
        string dir = Path.GetFullPath(evidenceDirectory ?? "artifacts/verification/next-ai-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(dir); Console.WriteLine("Native GGUF evidence: " + dir);
        var runtime = new GgufQuestionRuntime(); var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var results = new List<object>(); var failures = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        try
        {
            await runtime.LoadAsync(model, timeout.Token);
            Console.WriteLine($"Threads {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
            foreach (var c in NativeCases())
            {
                string name = $"{c.Family}-{c.BankVariant}-{(int)c.Tier}-{c.Language}";
                var previous = c.Family == BankQuestionFamily.Data ? await store.TakeChartAsync(c.Story!.ChartProfile!)
                    : await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
                if (previous is not null && previous.Contract.Story!.Schema == c.Story!.Schema)
                {
                    Check(previous.ModelName == runtime.ModelName && BasicQuestionValidator.Validate(previous.RawJson, previous.Contract).IsValid,
                        "Resume requires validated output from the actual selected model");
                    Verify(previous.Contract, previous.Draft); Verify(previous.Contract.FreshFacts(new(819)), previous.Draft);
                    Console.WriteLine(name + ": reused validated native SQLite evidence");
                    continue;
                }
                await File.WriteAllTextAsync(Path.Combine(dir, name + "-contract.json"), JsonSerializer.Serialize(c));
                string? correction = null; bool passed = false;
                var excluded = await store.GetProseHashesAsync();
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction, excludedProse: excluded); var stream = new StringBuilder(); AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, part => stream.Append(part), value => metrics = value);
                    Check(raw == stream.ToString() && metrics?.GeneratedTokens > 0, "Missing native streaming");
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    results.Add(new { Case = name, Attempt = attempt, validation.ErrorCode, validation.ErrorDetails, Metrics = metrics });
                    await File.WriteAllTextAsync(Path.Combine(dir, "results-" + name + "-" + attempt + ".json"),
                        JsonSerializer.Serialize(results[^1], new JsonSerializerOptions { WriteIndented = true }));
                    await File.WriteAllTextAsync(Path.Combine(dir, name + "-" + attempt + ".txt"), prompt + "\n\n" + raw + "\n" + validation.ErrorCode);
                    Console.WriteLine($"{name}: attempt {attempt}, {validation.ErrorCode ?? "Valid"}, {metrics!.TokensPerSecond:F2} token/s");
                    if (validation.IsValid)
                    {
                        Verify(c, validation.Draft); Verify(c.FreshFacts(new(819)), validation.Draft);
                        Check(await store.InsertAsync(new(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow)), "Native save failed");
                        passed = true; break;
                    }
                    correction = validation.ErrorCode;
                }
                if (!passed) failures.Add(name);
            }
        }
        finally { await runtime.EjectAsync(); await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })); }
        Check(!runtime.IsLoaded && failures.Count == 0, "Native failures: " + string.Join(", ", failures));
        Console.WriteLine($"PASS 30 actual native profiles / {results.Count} attempts; streaming, validation, fresh math and SQLite; model disposed.");
    }
    sealed class BankRandom : Random { public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue); }
}
