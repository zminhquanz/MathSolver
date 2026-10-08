using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class DecimalBankTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    internal static IEnumerable<BasicQuestionContract> Cases()
    {
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var context in ElementaryQuizGenerator.DecimalContexts(language))
        foreach (var type in ElementaryQuizGenerator.DecimalStoryTypes)
        foreach (var tier in Enum.GetValues<CurriculumTier>())
            yield return ReasoningStoryCatalogue.Create(BankQuestionFamily.Decimal, (int)type,
                tier, language, new Random(47), context.Id);
    }

    private static void CheckPractice(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c, draft, mode);
            var p = q.ElementaryProblem!;
            CheckQuantityWording(p);
            Check(p.IsDecimalArithmetic && !p.IsNumericDecimalCalculation && p.StoryContextId == c.Story!.NarrativeId,
                "Decimal story became numeric practice or changed context");
            Check(q.ExactAnswer == c.ExactAnswer && p.Answers.Count == 1, "Lost exact decimal answer");
            Check(!Regex.IsMatch(p.ProblemText + p.SolutionText, @"\{[fv]\d+\}"), "Unrendered variables");
            var values = p.Reasoning!.Givens.ToDictionary(g => g.Role,
                g => decimal.Parse(g.Value, CultureInfo.InvariantCulture));
            decimal initial = values["quantity"];
            if (c.Tier == CurriculumTier.FiveStars)
                initial /= ElementaryQuizGenerator.DecimalContexts(c.Language).Single(s => s.Id == c.SceneId).ConversionFactor;
            decimal expected;
            int level = (int)c.Tier;
            if (level <= 2)
            {
                decimal second = values["second-quantity"];
                expected = p.Type switch {
                    ElementaryQuizType.DecimalAdd => initial + second,
                    ElementaryQuizType.DecimalSubtract => initial - second,
                    ElementaryQuizType.DecimalMultiply => initial * second,
                    _ => initial / second
                };
                if (p.Type is ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide)
                    Check(second == decimal.Truncate(second), "Fractional number of portions");
            }
            else
            {
                expected = p.Type switch {
                    ElementaryQuizType.DecimalAdd => initial + initial + values["difference"],
                    ElementaryQuizType.DecimalSubtract => initial - values["used-first"] - values["used-second"],
                    ElementaryQuizType.DecimalMultiply => (initial + values["extra-quantity"]) * values["portion-count"],
                    _ => (initial + values["extra-quantity"]) / values["portion-count"]
                };
                if (values.TryGetValue("portion-count", out decimal count))
                    Check(count > 0 && count == decimal.Truncate(count), "Invalid portion count");
                if (level >= 4) expected += values["adjustment"];
                Check(p.Reasoning.Steps.Count >= level - 1, "Missing decimal inference steps");
            }
            Check(expected > 0 && EssayCalculationEvaluator.TryEvaluate(expected.ToString(CultureInfo.InvariantCulture),
                out var exact, out _, true) && q.ExactAnswer == new ReducedFraction(exact.Numerator, exact.Denominator),
                "Decimal conservation failed: " + p.Type + "/" + c.Tier);
            var answer = p.Answers[0];
            Check(decimal.Parse(answer.DisplayValue!, CultureInfo.InvariantCulture) == expected, "Rounded display answer");
            if (!c.ExactAnswer.Denominator.IsOne)
            {
                try { _ = c.Answer; throw new Exception("Noninteger answer silently became its numerator"); }
                catch (InvalidOperationException e) when (e.Message == "UseExactAnswer") { }
            }
            Check(p.ChoiceTexts!.Count == 4 && p.ChoiceTexts.Distinct().Count() == 4 && p.ChoiceTexts.Contains(p.AnswerText),
                "Invalid decimal multiple choices");
            Check(q.PresentedEquationIsCorrect == (p.PresentedText == p.AnswerText), "Invalid true/false display");
            if (mode == ArithmeticQuizMode.Essay)
            {
                var input = EssayCombinedInputParser.Parse(p.SolutionText, true, preserveAllCalculations: true);
                var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
                Check(validator.Validate(q, input.Solution, input.Equation, input.Answer).IsCorrect,
                    "Exact decimal essay grading failed: " + p.SolutionText);
                Check(!validator.Validate(q, input.Solution, input.Equation,
                    (expected + .1m).ToString(CultureInfo.InvariantCulture) + " " + answer.Unit).IsCorrect,
                    "Wrong decimal answer passed grading");
                Check(validator.Validate(q, input.Solution, input.Equation,
                    expected.ToString(CultureInfo.InvariantCulture).Replace('.', ',') + " " + answer.Unit).IsCorrect,
                    "Decimal comma grading failed");
            }
        }
    }

    private static void CheckQuantityWording(ElementaryQuizContract problem)
    {
        if (problem.Language != AppLanguage.Vietnamese || problem.StoryContextId is null) return;
        string text = (problem.ProblemText + " " + problem.SolutionText).ToLowerInvariant();
        if (problem.Answers[0].Unit is "kg" or "l" or "ml")
            Check(!text.Contains("số đo") && !text.Contains("đo được"),
                "Generic measurement wording leaked into mass/capacity: " + problem.ProblemText);
        if (problem.Type == ElementaryQuizType.DecimalAdd && (int)problem.Reasoning!.Tier <= 2)
            Check(text.Contains(problem.Answers[0].Unit == "kg" ? "cân được"
                : problem.Answers[0].Unit is "l" or "ml" ? "dung tích" : "số đo"),
                "Built-in addition has the wrong quantity wording: " + problem.ProblemText);
    }

    public static async Task RunAsync()
    {
        int count = 0;
        foreach (var c in Cases())
        {
            Check(c.IsValid, "Invalid decimal contract");
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                CheckQuantityWording(new ElementaryQuizGenerator(new(47)).GenerateDecimalStory(mode,
                    (ElementaryQuizType)c.BankVariant, c.Language, c.Tier, c.SceneId).ElementaryProblem!);
            var original = ReasoningStoryCatalogue.Draft(c);
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(original), c).IsValid, "Canonical prose rejected");
            var novel = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Take(8).ToArray();
            Check(novel.Length > 0, "No reviewed factual alternatives");
            foreach (var d in novel)
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), c).IsValid, "Reviewed prose rejected");
            var lesson = ReasoningStoryCatalogue.Lesson(c);
            if (c.Language == AppLanguage.Vietnamese && c.BankVariant == (int)ElementaryQuizType.DecimalAdd
                && (int)c.Tier <= 2)
            {
                var clauses = lesson.FactPhrasings.Values.SelectMany(phrases => phrases).ToArray();
                string expectedVerb = c.Unit switch { "kg" => "cân được", "l" or "ml" => "có dung tích", _ => "đo được" };
                Check(clauses.Any(text => text.Contains(expectedVerb, StringComparison.Ordinal)),
                    "Measurement verb does not match the quantity dimension");
                if (c.Unit is "kg" or "l" or "ml")
                {
                    Check(clauses.All(text => !text.Contains("đo được", StringComparison.Ordinal)),
                        "Mass/capacity grammar still permits measured wording");
                    var appropriate = novel.First(d => d.Facts!.Any(f => f.Text.Contains(expectedVerb, StringComparison.Ordinal)));
                    var measured = appropriate with { Facts = appropriate.Facts!.Select(f =>
                        f with { Text = f.Text.Replace(expectedVerb, "đo được", StringComparison.Ordinal) }).ToArray() };
                    Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(measured), c).IsValid,
                        "Mass/capacity validator accepted the retired measurement verb");
                }
            }
            foreach (string lead in lesson.LeadPhrasings[lesson.Steps[^1].Id])
                CheckPractice(c, novel[0] with { SolutionLeads = novel[0].SolutionLeads!
                    .Select(s => s.Role == lesson.Steps[^1].Id ? s with { Text = lead } : s).ToArray() });
            CheckPractice(c, novel[0]);
            var fresh = c.FreshFacts(new(971));
            Check(fresh.Story!.Schema == c.Story!.Schema && fresh.SceneId == c.SceneId, "Fresh facts changed roles/context");
            Check(ReasoningStoryCatalogue.Lesson(fresh).Quantities.Where(v => v.Id.StartsWith('f')).Select(v => v.Value)
                .SequenceEqual(ReasoningStoryCatalogue.Lesson(c).Quantities.Where(v => v.Id.StartsWith('f')).Select(v => v.Value)) == false,
                "Fresh decimal values did not change");
            CheckPractice(fresh, novel[^1]);
            Check(QuestionProseIdentity.Hash(c, novel[0]) == QuestionProseIdentity.Hash(fresh, novel[0]), "Numbers bypass dedup");
            var excluded = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Select(d => QuestionProseIdentity.Hash(c, d)).ToHashSet();
            try { ReasoningStoryValidator.Grammar(c, excluded); throw new Exception("Exhausted prose generated"); }
            catch (InvalidOperationException e) when (e.Message == "DuplicateProseRetriesExhausted") { }
            foreach (var bad in new[] {
                original with { Facts = original.Facts!.Skip(1).ToArray() },
                original with { Question = original.Question + " 123" },
                original with { Question = original.Question.Replace("}", "_bad}") },
                original with { Question = c.Language == AppLanguage.Vietnamese ? "Hỏi tất cả các phần có bao nhiêu?" : "What is the amount in each portion?" },
                original with { Facts = original.Facts!.Select(f => f with { Text = f.Text.Replace("kg", "m").Replace("{f0}", "{f999}") }).ToArray() },
                original with { Facts = original.Facts!.Reverse().ToArray() }
            }.Where(bad => QuestionBankStore.SerializeDraft(bad) != QuestionBankStore.SerializeDraft(original)))
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(bad), c).IsValid, "Unsafe decimal prose accepted: " + QuestionBankStore.SerializeDraft(bad));
            Check(!(c with { Story = c.Story! with { NarrativeId = "retired" } }).IsValid, "Unknown context accepted");
            count++;
        }
        await CheckStoreAsync();
        Console.WriteLine($"Decimal: {count} bilingual context/subtype/star profiles passed; exact math, all modes, fresh facts, grading, unsafe prose, exhaustion and SQLite/Excel/provider.");
    }

    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue); }
    private static async Task CheckStoreAsync()
    {
        string dir = Path.GetFullPath("artifacts/verification/decimal-bank"); Directory.CreateDirectory(dir);
        var store = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        var c = Cases().First(c => !c.ExactAnswer.Denominator.IsOne);
        var d = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
        Check(await store.InsertAsync(new(c, d, QuestionBankStore.SerializeDraft(d), "test", DateTime.UtcNow)), "Decimal insert failed");
        Check(!await store.InsertAsync(new(c.FreshFacts(new(77)), d, QuestionBankStore.SerializeDraft(d), "test", DateTime.UtcNow)), "Duplicate decimal prose inserted");
        var saved = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
        Check(saved?.Contract.ExactAnswer == c.ExactAnswer, "SQLite lost rational answer");
        using var workbook = new MemoryStream(); await store.ExportExcelAsync(workbook); workbook.Position = 0;
        var restored = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        Check((await restored.ImportExcelAsync(workbook)).Inserted == 1, "Decimal Excel import failed");
        var read = await restored.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
        Check(read?.Contract.ExactAnswer == c.ExactAnswer, "Excel lost decimal precision");
        var original = ReasoningStoryCatalogue.ToPractice(c, d, ArithmeticQuizMode.Essay);
        var provider = new BasicPracticeQuestionProvider(restored, new AlwaysBank());
        var selected = await provider.SelectReasoningAsync(original, c.Tier, c.Language);
        Check(selected != original && selected.ElementaryProblem?.IsDecimalArithmetic == true, "Bank decimal provider failed");
        Check(await provider.SelectReasoningAsync(original, c.Tier, AppLanguage.English) == original, "Wrong-language fallback failed");
        var generator = new ElementaryQuizGenerator(new(31));
        ArithmeticQuizQuestion numeric;
        do { numeric = generator.Generate(ArithmeticQuizMode.Essay, QuizProblemKind.Decimal, ElementaryQuizType.DecimalAdd, c.Language, c.Tier); }
        while (!numeric.ElementaryProblem!.IsNumericDecimalCalculation);
        Check(await provider.SelectReasoningAsync(numeric, c.Tier, c.Language) == numeric, "Bank replaced numeric practice with prose");
        var comparison = generator.Generate(ArithmeticQuizMode.Essay, QuizProblemKind.Decimal, ElementaryQuizType.DecimalCompare, c.Language, c.Tier);
        Check(await provider.SelectReasoningAsync(comparison, c.Tier, c.Language) == comparison, "Bank replaced decimal comparison");
    }

    public static async Task RunModelAsync(string modelPath, string? resumeDirectory = null)
    {
        string dir = resumeDirectory is null ? Path.GetFullPath("artifacts/verification/decimal-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"))
            : Path.GetFullPath(resumeDirectory);
        Directory.CreateDirectory(dir);
        var runtime = new GgufQuestionRuntime(); var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var evidence = new List<object>(); var rendered = new StringBuilder();
        var completed = new HashSet<string>();
        if (resumeDirectory is not null)
        {
            using var prior = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, "results.json")));
            foreach (var row in prior.RootElement.EnumerateArray())
            {
                if (row.GetProperty("ErrorCode").ValueKind != JsonValueKind.Null) continue;
                string name = row.GetProperty("Case").GetString()!;
                completed.Add(name[(name.IndexOf('-') + 1)..]); evidence.Add(row.Clone());
            }
            using var workbook = new MemoryStream(); await store.ExportExcelAsync(workbook); workbook.Position = 0;
            foreach (var row in QuestionBankWorkbook.Read(workbook))
            {
                var saved = row.Question ?? throw new Exception("Invalid resumed decimal row");
                CheckPractice(saved.Contract.FreshFacts(new(881)), saved.Draft);
            }
            rendered.Append(await File.ReadAllTextAsync(Path.Combine(dir, "rendered-samples.txt")));
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        Console.WriteLine("Real decimal GGUF: " + dir);
        try
        {
            await runtime.LoadAsync(modelPath, timeout.Token);
            int number = evidence.Count;
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            foreach (var type in ElementaryQuizGenerator.DecimalStoryTypes)
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            {
                // Rotate all eight continuous-quantity contexts across the 40 real profiles.
                var contexts = ElementaryQuizGenerator.DecimalContexts(language);
                var context = contexts[((int)tier - 1 + Array.IndexOf(ElementaryQuizGenerator.DecimalStoryTypes, type) * 2) % contexts.Count];
                if (completed.Contains($"{language}-{type}-{tier}-{context.Id}")) continue;
                var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.Decimal, (int)type, tier, language, new(47), context.Id);
                var original = ReasoningStoryCatalogue.Draft(c);
                await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "original", DateTime.UtcNow), timeout.Token);
                var excluded = (await store.GetProseHashesAsync(timeout.Token)).ToHashSet(StringComparer.Ordinal);
                string prompt = BasicQuestionPrompt.Build(c, "DuplicateProse", [original.ProblemText], excluded);
                var streamed = new StringBuilder(); AiGenerationMetrics? metrics = null;
                string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, t => streamed.Append(t), m => metrics = m);
                var result = BasicQuestionValidator.Validate(raw, c);
                string name = $"{++number}-{language}-{type}-{tier}-{context.Id}";
                await File.WriteAllTextAsync(Path.Combine(dir, name + ".txt"), prompt + "\nRAW\n" + raw + "\nVALIDATION\n" + (result.ErrorCode ?? "Valid"));
                evidence.Add(new { Case = name, result.ErrorCode, result.ErrorDetails, Metrics = metrics });
                Check(result.IsValid && metrics?.GeneratedTokens > 1 && raw == streamed.ToString(), "Native decimal generation failed: " + name + " " + result.ErrorCode);
                Check(ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Native facts did not vary");
                var saved = new ValidatedBankQuestion(c, result.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                Check(await store.InsertAsync(saved, timeout.Token) && !await store.InsertAsync(saved, timeout.Token), "Native decimal dedup failed");
                var retrieved = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language, timeout.Token);
                Check(retrieved is not null, "Native decimal retrieval failed");
                var fresh = retrieved!.Contract.FreshFacts(new(881)); CheckPractice(fresh, retrieved.Draft);
                var p = ReasoningStoryCatalogue.ToPractice(fresh, retrieved.Draft, ArithmeticQuizMode.Essay).ElementaryProblem!;
                rendered.AppendLine(name).AppendLine(p.ProblemText).AppendLine(p.SolutionText).AppendLine();
                Console.WriteLine($"PASS {name}: {metrics!.GeneratedTokens} tokens; native stream/dedup/SQLite/fresh math/grading");
            }
            await runtime.ReleaseAsync();
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese, 4, true,
                Family: BankQuestionFamily.Decimal, StoryVariant: (int)ElementaryQuizType.DecimalDivide));
            try { await worker.Completion.WaitAsync(timeout.Token); }
            catch { worker.Stop(); await worker.Completion; throw; }
            await File.WriteAllTextAsync(Path.Combine(dir, "worker.json"), JsonSerializer.Serialize(worker.Snapshot, new JsonSerializerOptions { WriteIndented = true }));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Decimal worker failed: " + worker.Snapshot.Error);
            foreach (var item in worker.Snapshot.Items) CheckPractice(item.Contract.FreshFacts(new(105 + item.Number)), item.Question!.Draft);
            Check(!runtime.IsLoaded && runtime.CanGenerate, "Model weights retained after decimal job");
        }
        finally
        {
            await runtime.EjectAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(Path.Combine(dir, "rendered-samples.txt"), rendered.ToString());
        }
        Console.WriteLine($"Real model decimal verification passed: {evidence.Count} generations; {dir}");
    }
}
