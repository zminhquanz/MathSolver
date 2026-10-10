using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Text;
using System.Text.Json;

internal static class ActivityDepthTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static readonly BankQuestionFamily[] Families = [BankQuestionFamily.Remainder, BankQuestionFamily.Time, BankQuestionFamily.Measurement];
    private static IEnumerable<string> Contexts(BankQuestionFamily family, int variant, CurriculumTier tier, AppLanguage language, bool expanded) => family switch
    {
        BankQuestionFamily.Remainder => ElementaryQuizGenerator.PackingStories(language, expanded).Select(c => c.Id),
        BankQuestionFamily.Time => ElementaryQuizGenerator.TimeStoryContextIds(tier, expanded),
        _ => ElementaryQuizGenerator.MeasurementContexts(language, (ElementaryQuizType)variant, expanded).Select(c => c.Id)
    };

    private static void Verify(BasicQuestionContract c, BasicQuestionDraft? draft = null)
    {
        string profile = $"{c.Family}/{c.BankVariant}/{c.Tier}/{c.SceneId}/{c.Language}/v{c.Story!.ContextVersion}";
        Check(c.IsValid, "Invalid " + profile);
        draft ??= ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
        Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Rejected " + profile);
        ProseExpansionTests.VerifyPractice(c, draft);
        var p = ReasoningStoryCatalogue.ToPractice(c, draft, ArithmeticQuizMode.Essay).ElementaryProblem!;
        var g = p.Reasoning!.Givens.ToDictionary(x => x.Role, x => decimal.Parse(x.Value, CultureInfo.InvariantCulture));
        decimal F(string key) => g.GetValueOrDefault(key);
        decimal expected;
        if (c.Family == BankQuestionFamily.Remainder)
        {
            var context = ElementaryQuizGenerator.PackingStories(c.Language).Single(x => x.Id == c.SceneId);
            decimal total = g.ContainsKey("total") ? F("total") : F("first-batch") + F("second-batch") - F("already-accommodated");
            decimal size = g.ContainsKey("capacity") ? F("capacity") : F("nominal-capacity") - F("reserved-capacity");
            int quotient = (int)(total / size), remainder = (int)(total % size);
            Check(total > 0 && total <= context.Capacity && size >= 2 && (g.ContainsKey("nominal-capacity") ? F("nominal-capacity") : size) <= context.MaximumSize, "Impossible packing " + profile);
            if (c.Story.ContextVersion >= 3 && c.Tier == CurriculumTier.TwoStars) Check(remainder > 0, "Two-star problem must have a remainder");
            expected = p.Type == ElementaryQuizType.MinimumGroups ? quotient + (remainder > 0 ? 1 : 0)
                : p.Type == ElementaryQuizType.Leftovers ? remainder : quotient;
            Check(p.Answers[0].Unit == (p.Type == ElementaryQuizType.Leftovers ? context.Item : context.Container), "Wrong packing target unit");
            if (p.Type == ElementaryQuizType.QuotientRemainder) Check(p.Answers.Count == 2 && p.Answers[1].Value.Numerator == remainder, "Missing remainder");
            else Check(p.Answers.Count == 1, "Extra answer changes the target");
            if (c.Story.ContextVersion >= 3)
            {
                var work = new List<string>();
                if (g.ContainsKey("first-batch")) work.Add($"{F("first-batch")} + {F("second-batch")} = {F("first-batch") + F("second-batch")}");
                if (g.ContainsKey("already-accommodated")) work.Add($"{F("first-batch") + F("second-batch")} - {F("already-accommodated")} = {total}");
                if (g.ContainsKey("nominal-capacity")) work.Add($"{F("nominal-capacity")} - {F("reserved-capacity")} = {size}");
                string division = $"{total} ÷ {size} = {quotient} {(c.Language == AppLanguage.Vietnamese ? "dư" : "remainder")} {remainder}";
                work.Add(division);
                if (p.Type == ElementaryQuizType.MinimumGroups && remainder > 0)
                    work.Add($"{quotient} + 1 = {quotient + 1} {context.Container}");
                var q = ReasoningStoryCatalogue.ToPractice(c, draft, ArithmeticQuizMode.Essay);
                var grading = ElementaryEssayValidator.Validate(q, p.Answers[0].Label, string.Join("\n", work), p.AnswerText);
                Check(grading.IsCorrect, "School remainder work with derived quantities rejected " + profile + ": " + string.Join(" | ", grading.Details));
                var wrongWork = work.Select(line => line == division ? division[..^remainder.ToString().Length] + (remainder + 1) : line);
                Check(!ElementaryEssayValidator.Validate(q, p.Answers[0].Label, string.Join("\n", wrongWork), p.AnswerText).IsCorrect, "Wrong school remainder accepted");
            }
        }
        else if (c.Family == BankQuestionFamily.Time)
        {
            if (p.Type == ElementaryQuizType.ElapsedTime)
            {
                decimal start = F("start-hour") * 60 + F("start-minute"), end = F("end-hour") * 60 + F("end-minute");
                bool night = end < start;
                expected = end + (night ? 1440 : 0) - start - F("pause-minutes") + F("second-session");
                string marker = c.Language == AppLanguage.Vietnamese ? night ? "ngày hôm sau" : "cùng ngày" : night ? "the next day" : "on the same day";
                Check(p.ProblemText.Contains(marker), "Missing day relationship " + profile);
                if (c.Story.ContextVersion >= 3)
                {
                    var activity = ElementaryQuizGenerator.TimeActivities(c.Language).Single(x => x.Id == c.SceneId);
                    Check(F("start-hour") >= activity.StartHourMinimum && F("start-hour") <= activity.StartHourMaximum, "Unrealistic start time");
                }
            }
            else
            {
                expected = (F("first-hours") + F("second-hours")) * 60 + F("first-minutes") + F("extra-minutes") - F("pause-minutes");
                if (c.Tier == CurriculumTier.FiveStars) expected /= 60;
            }
            Check(expected > 0, "Nonpositive active duration");
        }
        else
        {
            var context = ElementaryQuizGenerator.MeasurementContexts(c.Language, p.Type).Single(x => x.Id == c.SceneId);
            decimal factor = p.Type == ElementaryQuizType.LengthConversion ? c.Tier == CurriculumTier.OneStar ? 10 : 100 : 1000;
            int level = (int)c.Tier;
            expected = level == 1 ? F("large-quantity") * factor : level == 2 ? F("small-quantity") / factor
                : F("large-quantity") * factor + F("small-quantity") + F("extra-quantity") * factor - F("removed-quantity");
            if (level == 5) expected /= factor;
            decimal quantity = level is 2 or 5 ? expected : expected / factor;
            Check(quantity > 0 && quantity <= context.MaximumQuantity, "Measurement capacity exceeded");
        }
        ReducedFraction independent;
        if (c.Family == BankQuestionFamily.Time && p.Type == ElementaryQuizType.TimeAddition && c.Tier == CurriculumTier.FiveStars)
            independent = new((System.Numerics.BigInteger)((F("first-hours") + F("second-hours")) * 60 + F("first-minutes") + F("extra-minutes") - F("pause-minutes")), 60);
        else
        {
            Check(EssayCalculationEvaluator.TryEvaluate(expected.ToString(CultureInfo.InvariantCulture), out var rational, out _, true), "Invalid independent number");
            independent = new(rational.Numerator, rational.Denominator);
        }
        Check(c.ExactAnswer == independent, "Independent calculation failed " + profile);
    }

    internal static IEnumerable<BasicQuestionContract> NativeCases()
    {
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var family in Families)
        {
            int i = 0;
            foreach (int variant in ReasoningStoryCatalogue.Variants(family, tier).Where(v => family != BankQuestionFamily.Measurement
                || ElementaryQuizGenerator.MeasurementStoryTypes.Contains((ElementaryQuizType)v)))
            {
                var language = ((int)tier + i++) % 2 == 0 ? AppLanguage.English : AppLanguage.Vietnamese;
                string context = family == BankQuestionFamily.Remainder
                    ? new[] { "school-teams", "nursery-trays", "sports-teams", "laundry-boxes", "fruit-crates" }[((int)tier + i) % 5]
                    : family == BankQuestionFamily.Time ? (ElementaryQuizType)variant == ElementaryQuizType.ElapsedTime && tier >= CurriculumTier.ThreeStars
                        ? "travel-night" : new[] { "library", "sports", "events", "kitchen", "workshop" }[(int)tier - 1]
                    : ElementaryQuizGenerator.MeasurementContexts(language, (ElementaryQuizType)variant).Last().Id;
                yield return ReasoningStoryCatalogue.Create(family, variant, tier, language, new(71), context);
            }
        }
    }

    public static async Task RunAsync()
    {
        int count = 0;
        foreach (var family in Families)
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (int variant in ReasoningStoryCatalogue.Variants(family, tier).Where(v => family != BankQuestionFamily.Measurement
            || ElementaryQuizGenerator.MeasurementStoryTypes.Contains((ElementaryQuizType)v)))
        foreach (string context in Contexts(family, variant, tier, language, true))
        {
            var c = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(63), context);
            Verify(c); Verify(c.FreshFacts(new(93))); count++;
            _ = ReasoningStoryValidator.Grammar(c);
            var d = ReasoningStoryCatalogue.Draft(c);
            var foreign = d with { Question = d.Question + " łącznie" };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(foreign), c).IsValid, "Foreign text accepted");
            var changed = d with { Facts = d.Facts!.Select(f => f with { Text = f.Text.Replace("{f0}", "{f999}") }).ToArray() };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(changed), c).IsValid, "Changed role accepted");
            if (family == BankQuestionFamily.Remainder)
            {
                var otherType = (ElementaryQuizType)variant == ElementaryQuizType.MinimumGroups
                    ? ElementaryQuizType.FullGroups : ElementaryQuizType.MinimumGroups;
                var other = ReasoningStoryCatalogue.Create(family, (int)otherType, tier, language, new(63), context);
                var wrongTarget = d with { Question = ReasoningStoryCatalogue.Draft(other).Question };
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(wrongTarget), c).IsValid, "Floor/ceiling target was interchangeable");
            }
            if (family == BankQuestionFamily.Time && (ElementaryQuizType)variant == ElementaryQuizType.ElapsedTime)
            {
                string same = language == AppLanguage.Vietnamese ? "cùng ngày" : "on the same day";
                string next = language == AppLanguage.Vietnamese ? "ngày hôm sau" : "the next day";
                var wrongDay = d with { Facts = d.Facts!.Select(f => f with { Text = f.Text.Contains(same)
                    ? f.Text.Replace(same, next) : f.Text.Replace(next, same) }).ToArray() };
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(wrongDay), c).IsValid, "Changed same/next-day relationship accepted");
            }
            if ((ElementaryQuizType)variant == ElementaryQuizType.FullGroups || !Contexts(family, variant, tier, language, false).Contains(context)) continue;
            foreach (int version in new[] { 0, 1, 2 })
            {
                Verify(ReasoningStoryCatalogue.Create(family, variant, tier, language, new(63), context, contextVersion: version)); count++;
            }
        }
        string dir = Path.GetFullPath("artifacts/verification/activity-depth-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new QuestionBankStore(Path.Combine(dir, "bank.db3"));
        foreach (var c in NativeCases())
        {
            var d = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
            Check(await store.InsertAsync(new(c, d, QuestionBankStore.SerializeDraft(d), "test", DateTime.UtcNow)), "Insert failed");
            Check(!await store.InsertAsync(new(c.FreshFacts(new(93)), d, QuestionBankStore.SerializeDraft(d), "test", DateTime.UtcNow)), "Changed numbers bypassed dedup");
        }
        using var excel = new MemoryStream();
        Check((await store.ExportExcelAsync(excel)).Exported == 45, "Lost activity export"); excel.Position = 0;
        var restored = new QuestionBankStore(Path.Combine(dir, "import.db3"));
        Check((await restored.ImportExcelAsync(excel)).Inserted == 45, "Lost activity import");
        foreach (var c in NativeCases())
        {
            var saved = await restored.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
            Check(saved?.Contract.Story?.ContextVersion == 3, "Lost activity version");
            Verify(saved!.Contract.FreshFacts(new(94)), saved.Draft);
        }
        Console.WriteLine($"Activity depth: PASS {count} current/legacy profiles; independent math, all tiers/languages, grammar, physical limits, fresh grading, SQLite/Excel.");
    }

    public static async Task RunModelAsync(string model, bool englishPackingOnly = false)
    {
        string dir = Path.GetFullPath("artifacts/verification/activity-depth-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(dir); Console.WriteLine("Real GGUF: " + dir);
        var runtime = new GgufQuestionRuntime(); var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var results = new List<object>(); var failures = new List<string>();
        var cases = NativeCases().Where(c => !englishPackingOnly || c.Family == BankQuestionFamily.Remainder && c.Language == AppLanguage.English).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        try
        {
            await runtime.LoadAsync(model, timeout.Token);
            Console.WriteLine($"Threads: {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
            foreach (var c in cases)
            {
                string name = $"{c.Family}-{c.BankVariant}-{(int)c.Tier}-{c.Language}";
                await File.WriteAllTextAsync(Path.Combine(dir, name + "-contract.json"), JsonSerializer.Serialize(c));
                string? correction = null; bool passed = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction); var streamed = new StringBuilder(); AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateAsync(c, prompt, timeout.Token, chunk => streamed.Append(chunk), m => metrics = m);
                    Check(raw == streamed.ToString() && metrics?.GeneratedTokens > 0, "Missing native stream");
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    results.Add(new { Case = name, Attempt = attempt, validation.ErrorCode, validation.ErrorDetails, Metrics = metrics });
                    await File.WriteAllTextAsync(Path.Combine(dir, name + "-" + attempt + ".txt"), prompt + "\n\n" + raw + "\n" + validation.ErrorCode + " " + validation.ErrorDetails);
                    Console.WriteLine($"{name}: attempt={attempt} {validation.ErrorCode ?? "Valid"}; {metrics!.TokensPerSecond:F2} token/s");
                    if (validation.IsValid)
                    {
                        Verify(c, validation.Draft); Verify(c.FreshFacts(new(94)), validation.Draft);
                        Check(await store.InsertAsync(new(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow)), "Native save failed");
                        var saved = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
                        Verify(saved!.Contract.FreshFacts(new(95)), saved.Draft);
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
            await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        Check(!runtime.IsLoaded && failures.Count == 0, "Native failures or retained model: " + string.Join(", ", failures));
        Console.WriteLine($"PASS {cases.Length} native cases / {results.Count} attempts; streaming, validation, SQLite, fresh math/grading, model disposal.");
    }

    public static void CheckModelEvidence(IEnumerable<string> directories)
    {
        var remaining = NativeCases().Select(c => $"{c.Family}-{c.BankVariant}-{(int)c.Tier}-{c.Language}").ToHashSet();
        // Latest native run replaces earlier wording for the same profile. Read-only:
        // do not remove old evidence or count obsolete drafts as final-source passes.
        foreach (string directory in directories.Reverse())
        {
            using var db = new SQLite.SQLiteConnection(Path.Combine(directory, "model.db3"), SQLite.SQLiteOpenFlags.ReadOnly);
            foreach (var row in db.Table<QuestionBankStore.Row>().ToArray())
            {
                var c = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson)!;
                string key = $"{c.Family}-{c.BankVariant}-{(int)c.Tier}-{c.Language}";
                if (!remaining.Contains(key)) continue;
                var validation = BasicQuestionValidator.Validate(row.DraftJson, c);
                Check(validation.IsValid, "Native evidence no longer valid: " + key + "/" + validation.ErrorDetails);
                Verify(c, validation.Draft); Verify(c.FreshFacts(new(96)), validation.Draft);
                remaining.Remove(key);
            }
        }
        Check(remaining.Count == 0, "Missing final-source native profiles: " + string.Join(", ", remaining));
        Console.WriteLine("PASS 45 final-source native profiles; read-only SQLite replay, fresh exact facts and all three grading modes.");
    }
}
