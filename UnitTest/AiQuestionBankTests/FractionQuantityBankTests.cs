using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class FractionQuantityBankTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private static IEnumerable<BasicQuestionContract> Cases()
    {
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in ElementaryQuizGenerator.FractionQuantityStoryTypes)
        foreach (var context in FractionQuantityStoryContextCatalog.GetProfile(language))
            yield return ReasoningStoryCatalogue.Create(BankQuestionFamily.FractionQuantity, (int)type, tier,
                language, new(47), context.ContextId);
    }

    private static void CheckPractice(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        var context = FractionQuantityStoryContextCatalog.GetProfile(c.Language).Single(s => s.ContextId == c.SceneId);
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c, draft, mode);
            var p = q.ElementaryProblem!;
            Check(p.Kind == QuizProblemKind.FractionSkills && (int)p.Type == c.BankVariant && p.StoryContextId == c.SceneId,
                "Practice changed fraction subtype/context");
            Check(!Regex.IsMatch(p.ProblemText + p.SolutionText, @"\{[fv]\d+\}"), "Unrendered fraction variables");
            var g = p.Reasoning!.Givens.ToDictionary(g => g.Role, g => BigInteger.Parse(g.Value, CultureInfo.InvariantCulture));
            BigInteger supplied = c.Tier <= CurriculumTier.TwoStars || c.Tier == CurriculumTier.FiveStars ? g["quantity"]
                : g["quantity-first"] + g["quantity-second"] - g.GetValueOrDefault("removed");
            var share = new ReducedFraction(g["numerator"] * (c.Tier == CurriculumTier.FiveStars ? 3 : 1),
                g["denominator"] * (c.Tier == CurriculumTier.FiveStars ? 4 : 1));
            bool findPart = p.Type == ElementaryQuizType.FractionOfNumber;
            var expected = findPart ? new ReducedFraction(supplied * share.Numerator, share.Denominator)
                : new ReducedFraction(supplied * share.Denominator, share.Numerator);
            var whole = findPart ? new ReducedFraction(supplied, 1) : expected;
            Check(q.ExactAnswer == expected && c.ExactAnswer == expected && q.ExactAnswers.Count == 1
                && p.Answers[0].Unit == context.Unit && whole.Numerator <= context.Capacity * whole.Denominator,
                "Independent part/whole/capacity/exact value failed");
            if (context.Quantity == WordProblemQuantity.Count)
                Check(expected.Denominator.IsOne && p.Reasoning.Steps.Where(s => s.Unit.Length > 0).All(s => s.Value.Denominator.IsOne),
                    "A counted object was split into fractional items");
            Check(p.ChoiceTexts!.Count == 4 && p.ChoiceTexts.Distinct().Count() == 4
                && p.ChoiceTexts.Count(choice => ElementaryEssayValidator.CheckAnswers(q, choice)) == 1, "Invalid fraction choices");
            Check(ElementaryEssayValidator.CheckAnswers(q, p.PresentedText) == q.PresentedEquationIsCorrect, "Wrong true/false grading");
            Check(ElementaryEssayValidator.CheckAnswers(q, p.AnswerText), "Full exact answer rejected");
            Check(!ElementaryEssayValidator.CheckAnswers(q, expected + " invalidunit"), "Wrong unit accepted");
            if (mode == ArithmeticQuizMode.Essay)
            {
                var input = EssayCombinedInputParser.Parse(p.SolutionText, true, preserveAllCalculations: true);
                var result = new EssayAnswerValidator(new BasicArithmeticEngine()).Validate(q, input.Solution, input.Equation, input.Answer);
                Check(result.IsCorrect, "Stored fraction solution failed grading: " + c.SceneId + "/" + c.Tier + "/" + p.Type + "\n" + p.SolutionText);
            }
        }
    }

    private static void CheckUnsafe(BasicQuestionContract c, BasicQuestionDraft d)
    {
        var invalid = new List<BasicQuestionDraft> {
            d with { Question = c.Language == AppLanguage.Vietnamese ? "Hỏi còn lại bao nhiêu?" : "How much is left?" },
            d with { Question = d.Question + " 25 kg" },
            d with { Facts = d.Facts!.Skip(1).ToArray() },
            d with { Facts = d.Facts!.Select(f => f with { Text = f.Text.Replace("{f0}", "{f999}") }).ToArray() },
            d with { Facts = d.Facts!.Select(f => f with { Text = f.Text.Replace("{v0}", "kg") }).ToArray() }
        };
        if (d.Facts!.Count > 1) invalid.Add(d with { Facts = d.Facts.Reverse().ToArray() });
        if (c.Tier == CurriculumTier.FiveStars)
            invalid.Add(d with { Facts = d.Facts.Select(f => f with { Text = f.Text.Replace("lượng còn lại", "lượng ban đầu")
                .Replace("of the remainder", "of the initial quantity") }).ToArray() });
        string original = QuestionBankStore.SerializeDraft(d);
        foreach (var bad in invalid.Where(b => QuestionBankStore.SerializeDraft(b) != original))
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(bad), c).IsValid, "Changed fraction base/target/role/unit accepted");
    }

    public static async Task RunAsync()
    {
        int count = 0;
        foreach (var c in Cases())
        {
            Check(c.IsValid && c.Story!.NarrativeId == c.SceneId, "Invalid fraction bank profile/context binding");
            var original = ReasoningStoryCatalogue.Draft(c);
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(original), c).IsValid, "Canonical fraction rejected");
            CheckPractice(c, original);
            CheckUnsafe(c, original);
            var novel = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Take(3).ToArray();
            Check(novel.Length >= 2, "No novel reviewed fraction facts: " + c.SceneId);
            foreach (var d in novel)
            {
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), c).IsValid, "Reviewed fraction prose rejected");
                CheckPractice(c, d);
            }
            var fresh = c.FreshFacts(new(881));
            Check(fresh.SceneId == c.SceneId && fresh.Story!.Schema == c.Story!.Schema, "Fresh facts changed fraction schema");
            CheckPractice(fresh, novel[0]);
            Check(QuestionProseIdentity.Hash(c, novel[0]) == QuestionProseIdentity.Hash(fresh, novel[0]), "Changed numbers evaded dedup");
            Check(!(c with { Story = c.Story! with { NarrativeId = "retired" } }).IsValid, "Unknown context accepted");
            var excluded = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Select(d => QuestionProseIdentity.Hash(c, d)).ToHashSet();
            try { ReasoningStoryValidator.Grammar(c, excluded); throw new Exception("Exhausted prose allowed"); }
            catch (InvalidOperationException e) when (e.Message == "DuplicateProseRetriesExhausted") { }
            count++;
        }
        var dir = Path.GetFullPath("artifacts/verification/fraction-quantity-bank"); Directory.CreateDirectory(dir);
        var store = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        foreach (var c in Cases())
        {
            var d = ReviewedReasoningProse.NovelDrafts(c, await store.GetProseHashesAsync()).FirstOrDefault();
            Check(d is not null, "No remaining SQLite wording: " + c.Language + "/" + c.BankVariant + "/" + c.Tier + "/" + c.SceneId);
            Check(await store.InsertAsync(new(c, d!, QuestionBankStore.SerializeDraft(d!), "test", DateTime.UtcNow)), "SQLite insert failed");
            Check(!await store.InsertAsync(new(c.FreshFacts(new(77)), d!, QuestionBankStore.SerializeDraft(d!), "test", DateTime.UtcNow)), "Duplicate stored words accepted");
        }
        using var workbook = new MemoryStream(); await store.ExportExcelAsync(workbook); workbook.Position = 0;
        var restored = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        Check((await restored.ImportExcelAsync(workbook)).Inserted == count, "Excel lost fraction profiles");
        foreach (var c in Cases().GroupBy(c => (c.BankVariant, c.Tier, c.Language)).Select(g => g.First()))
        {
            var saved = await restored.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
            Check(saved is not null, "SQLite take failed"); CheckPractice(saved!.Contract.FreshFacts(new(74)), saved.Draft);
            var original = ReasoningStoryCatalogue.ToPractice(c, ReasoningStoryCatalogue.Draft(c), ArithmeticQuizMode.Essay);
            var provider = new BasicPracticeQuestionProvider(restored, new AlwaysBank());
            Check(await provider.SelectReasoningAsync(original, c.Tier, c.Language) != original, "Provider ignored fraction bank");
        }
        foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.FractionSkills).Except(ElementaryQuizGenerator.FractionQuantityStoryTypes))
        {
            var q = new ElementaryQuizGenerator(new(47)).Generate(ArithmeticQuizMode.Essay, QuizProblemKind.FractionSkills, type,
                AppLanguage.Vietnamese, CurriculumTier.FiveStars);
            Check(await new BasicPracticeQuestionProvider(restored, new AlwaysBank()).SelectReasoningAsync(q, CurriculumTier.FiveStars, AppLanguage.Vietnamese) == q,
                "Bank replaced a numeric/visual fraction skill");
        }
        Console.WriteLine($"PASS fraction quantity bank: {count} bilingual subtype/star/context profiles; independent exact answers, counted objects, three grading modes, unsafe prose, exhaustion, SQLite/Excel and provider.");
    }

    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue); }

    public static async Task RunModelAsync(string path, string directory)
    {
        string dir = Path.GetFullPath(directory); Directory.CreateDirectory(dir);
        var runtime = new GgufQuestionRuntime(); var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromHours(2));
        try
        {
            await runtime.LoadAsync(path, timeout.Token);
            // Full 300-profile math/validator matrix runs in RunAsync. Native sampling
            // stratifies 60 profiles over both types/languages, every context and star.
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
        }
        finally { await runtime.EjectAsync(); }
        await CheckEvidenceAsync(dir);
    }

    private static void CheckEvidence(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var c = doc.RootElement.GetProperty("Contract").Deserialize<BasicQuestionContract>()!;
        var result = BasicQuestionValidator.Validate(doc.RootElement.GetProperty("Raw").GetString()!, c);
        Check(result.IsValid && ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Invalid model evidence");
        Check(doc.RootElement.GetProperty("Metrics").GetProperty("GeneratedTokens").GetInt32() > 1, "No sampled tokens");
        CheckPractice(c.FreshFacts(new(881)), result.Draft!);
    }

    public static async Task CheckEvidenceAsync(string directory)
    {
        var files = Directory.GetFiles(directory, "*.json").Where(file => !Path.GetFileName(file).StartsWith("worker-", StringComparison.Ordinal)).ToArray();
        Check(files.Length == 60, "Incomplete native context/type/language/star coverage");
        var samples = new StringBuilder();
        foreach (string file in files)
        {
            CheckEvidence(file);
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var c = doc.RootElement.GetProperty("Contract").Deserialize<BasicQuestionContract>()!;
            var d = BasicQuestionValidator.Validate(doc.RootElement.GetProperty("Raw").GetString()!, c).Draft!;
            var p = ReasoningStoryCatalogue.ToPractice(c.FreshFacts(new(881)), d, ArithmeticQuizMode.Essay).ElementaryProblem!;
            samples.AppendLine(Path.GetFileNameWithoutExtension(file)).AppendLine(p.ProblemText).AppendLine(p.SolutionText).AppendLine();
        }
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        using var workbook = new MemoryStream(); await store.ExportExcelAsync(workbook); workbook.Position = 0;
        var rows = QuestionBankWorkbook.Read(workbook).Select(r => r.Question).Where(q => q is not null && q.ModelName != "original").ToArray();
        Check(rows.Length >= 68, "SQLite lost native model rows");
        foreach (var q in rows) CheckPractice(q!.Contract.FreshFacts(new(881)), q.Draft);
        await File.WriteAllTextAsync(Path.Combine(directory, "rendered-samples.txt"), samples.ToString());
        Console.WriteLine($"PASS {files.Length} sampled profiles and {rows.Length} native SQLite/Excel rows; fresh facts and all three grading modes.");
    }
}
