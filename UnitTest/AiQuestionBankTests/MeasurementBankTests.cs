using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class MeasurementBankTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static IEnumerable<BasicQuestionContract> Cases()
    {
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var type in ElementaryQuizGenerator.MeasurementStoryTypes)
        foreach (var context in ElementaryQuizGenerator.MeasurementContexts(language, type))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
            yield return ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement, (int)type,
                tier, language, new(47), context.Id);
    }

    private static void CheckPractice(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c, draft, mode);
            var p = q.ElementaryProblem!;
            Check(p.Kind == QuizProblemKind.Measurement && (int)p.Type == c.BankVariant
                && p.StoryContextId == c.SceneId && p.Answers.Count == 1, "Measurement profile changed");
            Check(!Regex.IsMatch(p.ProblemText + p.SolutionText, @"\{[fv]\d+\}"), "Unrendered measurement slots");
            var givens = p.Reasoning!.Givens.ToDictionary(g => g.Role, g => decimal.Parse(g.Value, CultureInfo.InvariantCulture));
            decimal factor = p.Type == ElementaryQuizType.LengthConversion ? (c.Tier == CurriculumTier.OneStar ? 10 : 100) : 1000;
            int level = (int)c.Tier;
            decimal expected = level == 1 ? givens["large-quantity"] * factor
                : level == 2 ? givens["small-quantity"] / factor
                : givens["large-quantity"] * factor + givens["small-quantity"]
                    + givens["extra-quantity"] * factor - givens.GetValueOrDefault("removed-quantity");
            if (level == 5) expected /= factor;
            string expectedUnit = p.Type switch
            {
                ElementaryQuizType.MassConversion => level is 2 or 5 ? "kg" : "g",
                ElementaryQuizType.CapacityConversion => level is 2 or 5 ? "L" : "mL",
                _ => level is 2 or 5 ? "m" : level == 1 ? "dm" : "cm"
            };
            Check(expected > 0 && decimal.Parse(p.Answers[0].DisplayValue!, CultureInfo.InvariantCulture) == expected
                && p.Answers[0].Unit == expectedUnit, $"Independent measurement calculation/unit failed: {p.Type}/{c.Tier}/{c.SceneId}: expected {expected} {expectedUnit}, got {p.Answers[0].DisplayValue} {p.Answers[0].Unit}");
            Check(EssayCalculationEvaluator.TryEvaluate(expected.ToString(CultureInfo.InvariantCulture), out var exact, out _, true)
                && q.ExactAnswer == new ReducedFraction(exact.Numerator, exact.Denominator)
                && q.ExactAnswer == c.ExactAnswer, "Measurement answer lost precision");
            Check(p.ChoiceTexts!.Count == 4 && p.ChoiceTexts.Distinct().Count() == 4
                && p.ChoiceTexts.Count(choice => ElementaryEssayValidator.CheckAnswers(q, choice)) == 1,
                "Measurement choices invalid");
            Check(ElementaryEssayValidator.CheckAnswers(q, p.PresentedText) == q.PresentedEquationIsCorrect,
                "Measurement true/false proposal disagrees with grading");
            if (c.Language == AppLanguage.Vietnamese)
            {
                string text = p.ProblemText.ToLowerInvariant();
                string wording = p.Type switch { ElementaryQuizType.MassConversion => "cân được",
                    ElementaryQuizType.CapacityConversion => "dung tích", _ => "độ dài" };
                Check(text.Contains(wording), "Quantity wording does not match its dimension");
                if (p.Type != ElementaryQuizType.LengthConversion)
                    Check(!text.Contains("số đo") && !text.Contains("đo được"), "Generic measurement verb used for mass/capacity");
            }
            if (mode == ArithmeticQuizMode.Essay)
            {
                var input = EssayCombinedInputParser.Parse(p.SolutionText, true, preserveAllCalculations: true);
                var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
                Check(validator.Validate(q, input.Solution, input.Equation, input.Answer).IsCorrect,
                    "Measurement model solution failed grading: " + p.SolutionText);
                Check(!validator.Validate(q, input.Solution, input.Equation,
                    (expected + .1m).ToString(CultureInfo.InvariantCulture) + " " + expectedUnit).IsCorrect,
                    "Wrong measurement amount passed grading");
                string wrongUnit = expectedUnit is "m" or "cm" or "dm" ? "kg" : "m";
                Check(!validator.Validate(q, input.Solution, input.Equation,
                    expected.ToString(CultureInfo.InvariantCulture) + " " + wrongUnit).IsCorrect,
                    "Wrong measurement unit passed grading");
            }
        }
    }

    public static async Task RunAsync()
    {
        int count = 0;
        foreach (var c in Cases())
        {
            Check(c.IsValid, "Invalid measurement contract");
            var canonical = ReasoningStoryCatalogue.Draft(c);
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(canonical), c).IsValid, "Canonical measurement rejected");
            var novel = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Take(8).ToArray();
            Check(novel.Length >= 2, "Too few measurement alternatives");
            foreach (var d in novel)
            {
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), c).IsValid, "Reviewed measurement rejected");
                CheckPractice(c, d);
            }
            var fresh = c.FreshFacts(new(881));
            Check(fresh.Story!.Schema == c.Story!.Schema && fresh.SceneId == c.SceneId, "Fresh measurement changed context/roles");
            Check(!ReasoningStoryCatalogue.Lesson(c).Quantities.Where(q => q.Id.StartsWith('f')).Select(q => q.Value)
                .SequenceEqual(ReasoningStoryCatalogue.Lesson(fresh).Quantities.Where(q => q.Id.StartsWith('f')).Select(q => q.Value)),
                "Fresh measurement numbers did not change");
            CheckPractice(fresh, novel[0]);
            Check(QuestionProseIdentity.Hash(c, novel[0]) == QuestionProseIdentity.Hash(fresh, novel[0]), "Numbers bypassed measurement dedup");
            foreach (var bad in new[]
            {
                canonical with { Question = canonical.Question + " 123 kg" },
                canonical with { Question = "What is the capacity in kilometres?" },
                canonical with { Facts = canonical.Facts!.Skip(1).ToArray() },
                canonical with { Facts = canonical.Facts!.Select(f => f with { Text = f.Text.Replace("{f0}", "{f999}") }).ToArray() },
                canonical with { Facts = canonical.Facts!.Select(f => f with { Text = f.Text.Replace("Nhận thêm", "Dùng bớt").Replace("is added", "is used up") }).ToArray() },
                canonical with { Facts = canonical.Facts!.Select(f => f with { Text = f.Text.Replace("cân được", "đo được").Replace("có dung tích", "đo được") }).ToArray() },
                canonical with { Facts = canonical.Facts!.Reverse().ToArray() }
            }.Where(bad => QuestionBankStore.SerializeDraft(bad) != QuestionBankStore.SerializeDraft(canonical)))
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(bad), c).IsValid, "Unsafe measurement prose accepted");
            var exhausted = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Select(d => QuestionProseIdentity.Hash(c, d)).ToHashSet();
            try { ReasoningStoryValidator.Grammar(c, exhausted); throw new Exception("Exhausted measurement prose generated"); }
            catch (InvalidOperationException e) when (e.Message == "DuplicateProseRetriesExhausted") { }
            Check(!(c with { Story = c.Story! with { NarrativeId = "retired" } }).IsValid, "Unknown measurement context accepted");
            count++;
        }
        await CheckStoreAsync();
        Console.WriteLine($"Measurement: {count} bilingual context/subtype/star profiles passed; exact conversions, all modes, fresh facts, grading, unsafe prose, exhaustion, SQLite/Excel/provider.");
    }

    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue); }
    private static async Task CheckStoreAsync()
    {
        string dir = Path.GetFullPath("artifacts/verification/measurement-bank"); Directory.CreateDirectory(dir);
        var store = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        var c = Cases().First(c => !c.ExactAnswer.Denominator.IsOne);
        var d = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
        var question = new ValidatedBankQuestion(c, d, QuestionBankStore.SerializeDraft(d), "test", DateTime.UtcNow);
        Check(await store.InsertAsync(question), "Measurement insert failed");
        Check(!await store.InsertAsync(question with { Contract = c.FreshFacts(new(77)) }), "Duplicate measurement prose inserted");
        Check((await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language))?.Contract.ExactAnswer == c.ExactAnswer,
            "SQLite lost measurement answer");
        using var workbook = new MemoryStream(); await store.ExportExcelAsync(workbook); workbook.Position = 0;
        var restored = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        Check((await restored.ImportExcelAsync(workbook)).Inserted == 1, "Measurement Excel import failed");
        Check((await restored.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language))?.Contract.ExactAnswer == c.ExactAnswer,
            "Excel lost exact measurement answer");
        var original = ReasoningStoryCatalogue.ToPractice(c, d, ArithmeticQuizMode.Essay);
        var provider = new BasicPracticeQuestionProvider(restored, new AlwaysBank());
        var selected = await provider.SelectReasoningAsync(original, c.Tier, c.Language);
        Check(selected != original && selected.ElementaryProblem?.Type == original.ElementaryProblem!.Type,
            "Measurement provider failed");
        Check(await provider.SelectReasoningAsync(original, c.Tier, AppLanguage.English) == original, "Wrong language fallback failed");
        foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.Measurement).Except(ElementaryQuizGenerator.MeasurementStoryTypes))
        {
            var unsupported = new ElementaryQuizGenerator(new(31)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Measurement, type, c.Language, c.Tier);
            Check(await provider.SelectReasoningAsync(unsupported, c.Tier, c.Language) == unsupported, "Bank replaced an unopened measurement skill");
        }
    }

    public static async Task RunModelAsync(string modelPath, string? resumeDirectory = null)
    {
        string dir = resumeDirectory is null
            ? Path.GetFullPath("artifacts/verification/measurement-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"))
            : Path.GetFullPath(resumeDirectory);
        Directory.CreateDirectory(dir);
        var runtime = new GgufQuestionRuntime(); var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
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
            await runtime.LoadAsync(modelPath, timeout.Token);
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
            await runtime.EjectAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(Path.Combine(dir, "rendered-samples.txt"), rendered.ToString());
        }
        Console.WriteLine($"Real measurement model passed: {evidence.Count} profiles and 4 worker items; {dir}");
    }

    public static async Task CheckEvidenceAsync(string directory)
    {
        string dir = Path.GetFullPath(directory);
        using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, "results.json")));
        var rendered = new StringBuilder();
        int count = 0;
        foreach (var row in evidence.RootElement.EnumerateArray())
        {
            Check(row.GetProperty("ErrorCode").ValueKind == JsonValueKind.Null
                && row.GetProperty("Metrics").GetProperty("GeneratedTokens").GetInt32() > 1, "Native evidence contains a failed generation");
            string name = row.GetProperty("Case").GetString()!;
            var profile = name[(name.IndexOf('-') + 1)..].Split('-');
            var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement, (int)Enum.Parse<ElementaryQuizType>(profile[1]),
                Enum.Parse<CurriculumTier>(profile[2]), Enum.Parse<AppLanguage>(profile[0]), new(47), profile[3]);
            string file = (await File.ReadAllTextAsync(Path.Combine(dir, name + ".txt"))).Replace("\r\n", "\n");
            string raw = file.Split("\nRAW\n")[1].Split("\nVALIDATION\n")[0];
            var result = BasicQuestionValidator.Validate(raw, c);
            Check(result.IsValid && ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Saved native raw JSON failed verification");
            Check(QuestionProseIdentity.Hash(c, result.Draft!) != QuestionProseIdentity.Hash(c, ReasoningStoryCatalogue.Draft(c)),
                "Native output repeated the seeded canonical prose");
            var fresh = c.FreshFacts(new(881)); CheckPractice(fresh, result.Draft!);
            var p = ReasoningStoryCatalogue.ToPractice(fresh, result.Draft!, ArithmeticQuizMode.Essay).ElementaryProblem!;
            rendered.AppendLine(name).AppendLine(p.ProblemText).AppendLine(p.SolutionText).AppendLine();
            count++;
        }
        var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        using var workbook = new MemoryStream();
        await store.ExportExcelAsync(workbook); workbook.Position = 0;
        var savedNative = QuestionBankWorkbook.Read(workbook).Select(row => row.Question)
            .Where(question => question is { Contract.Family: BankQuestionFamily.Measurement } && question.ModelName != "original").ToArray();
        Check(savedNative.Length >= count, "SQLite/export is missing native model outputs");
        foreach (var question in savedNative)
        {
            Check(BasicQuestionValidator.Validate(question!.RawJson, question.Contract).IsValid, "Native SQLite raw JSON failed validation");
            CheckPractice(question.Contract.FreshFacts(new(881)), question.Draft);
        }
        await File.WriteAllTextAsync(Path.Combine(dir, "rendered-samples.txt"), rendered.ToString());
        Console.WriteLine($"Verified {count} actual native raw outputs and {savedNative.Length} SQLite/Excel model rows with new C# facts, exact arithmetic and all grading modes.");
    }
}
