using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class ProportionBankTests
{
    public static async Task CheckPromptBudgetAsync(string modelPath)
    {
        using var weights = await LLama.LLamaWeights.LoadFromFileAsync(new LLama.Common.ModelParams(modelPath)
            { GpuLayerCount = 0, UseMemorymap = true });
        int count = 0, maximum = 0;
        foreach (var c in Cases())
        {
            var original = ReasoningStoryCatalogue.Draft(c);
            var excluded = new HashSet<string> { QuestionProseIdentity.Hash(c, original) };
            string prompt = BasicQuestionPrompt.Build(c, "DuplicateProse", [original.ProblemText], excluded);
            int tokens = weights.Tokenize($"<|turn>user\n{prompt}<turn|>\n<|turn>model\n", true, true, Encoding.UTF8).Length;
            GgufQuestionRuntime.GetContextTokens(tokens); maximum = Math.Max(maximum, tokens); count++;
        }
        Console.WriteLine($"Proportion actual Gemma tokenizer: {count} retry prompts; maximum {maximum} input tokens; context 2048/output 700/margin 64.");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    internal static IEnumerable<BasicQuestionContract> Cases()
    {
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var row in QuizContentCatalog.LoadList<JsonElement>("ProportionQuizGenerator.Templates", QuizContentCatalog.Culture(language)))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var type = Enum.Parse<ProportionQuizType>(row.GetProperty("Type").GetString()!);
            BasicQuestionContract? c = null;
            try { c = ReasoningStoryCatalogue.Create(BankQuestionFamily.Proportion, (int)type, tier, language,
                new Random(47), row.GetProperty("NarrativeId").GetString()!); }
            catch (ArgumentException e) when (e.Message == "InvalidProportionNarrativeProfile") { }
            if (c is not null) yield return c;
        }
    }

    private static void CheckMath(ProportionQuizContract p)
    {
        // Independent conservation checks: fixed rate or fixed workload/stock.
        BigInteger value = p.CorrectAnswer;
        if (p.AsksForAdditionalPeople) value += p.A;
        Check(p.A > 0 && p.B > 0 && p.C > 0 && value > 0, "Invalid proportion quantities");
        Check(p.IsDirect ? value * p.A == (BigInteger)p.B * p.C
            : value * p.C == (BigInteger)p.A * p.B, "Rate/workload conservation failed");
        if (p.AsksForAdditionalPeople) Check(p.C < p.B && p.CorrectAnswer > 0, "Additional people is not an increase");
    }

    private static void CheckPractice(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c, draft, mode);
            var p = q.ProportionProblem!;
            Check(p is not null && p.Type == (ProportionQuizType)c.BankVariant && q.CorrectAnswer == c.Answer, "Lost proportion profile");
            CheckMath(p!);
            Check(!Regex.IsMatch(p!.ProblemText + p.SolutionText, @"\{[fv]\d+\}"), "Unrendered proportion variable");
            Check(ProportionQuizSolutionFormatter.Format(p, c.Language, CultureInfo.InvariantCulture) == p.SolutionText, "UI discarded AI solution leads");
            Check(new BasicArithmeticEngine().CalculateInteger(q.Expression).Result == q.CorrectAnswer, "Representative arithmetic changed");
            if (mode == ArithmeticQuizMode.TrueFalse) Check(q.PresentedEquationIsCorrect == (q.PresentedAnswer == q.CorrectAnswer), "Broken true/false");
            if (mode == ArithmeticQuizMode.MultipleChoice) Check(q.Choices.Count == 4 && q.Choices.Distinct().Count() == 4 && q.Choices.Contains(q.CorrectAnswer), "Broken choices");
            if (mode == ArithmeticQuizMode.Essay)
            {
                var input = EssayCombinedInputParser.Parse(p.SolutionText!, true, preserveAllCalculations: true);
                var grade = new EssayAnswerValidator(new BasicArithmeticEngine()).Validate(q, input.Solution, input.Equation, input.Answer);
                Check(grade.IsCorrect, "Proportion essay failed: " + p.SolutionText + "\n" + grade);
                if (p.IsDirect && p.B % p.A != 0)
                {
                    var wrong = new EssayAnswerValidator(new BasicArithmeticEngine()).Validate(q, input.Solution,
                        $"{p.B / p.A} × {p.C} = {p.CorrectAnswer}", input.Answer);
                    Check(!wrong.IsCorrect, "Rounded unit rate was accepted");
                }
            }
        }
    }

    private static IEnumerable<BasicQuestionDraft> Mutations(BasicQuestionDraft draft)
    {
        yield return draft with { Question = draft.Question + " 123" };
        yield return draft with { Question = draft.Question.Replace("}", "_bad}") };
        yield return draft with { Facts = draft.Facts!.Skip(1).ToArray() };
        string Swap(string s) => s.Replace("{f0}", "{tmp}").Replace("{f1}", "{f0}").Replace("{tmp}", "{f1}");
        yield return draft with { Facts = draft.Facts!.Select(f => f with { Text = Swap(f.Text) }).ToArray() };
        yield return draft with { Question = draft.Question + " Cần thêm bao nhiêu người?" };
        foreach (var (from, to) in new[] { ("cùng năng suất", "khác năng suất"), ("như nhau", "khác nhau"),
            ("không đổi", "thay đổi"), ("cùng công việc", "công việc khác"), ("cùng quãng đường", "quãng đường khác"),
            ("có thêm bao nhiêu người", "có tất cả bao nhiêu người"), ("đồng", "kg"), ("ngày", "giờ"),
            ("same rate", "different rates"), ("same productivity", "different productivity"),
            ("same job", "different job"), ("additional people", "people in total"), ("unchanged", "doubled"),
            ("days", "hours"), ("kg", "liters"), ("$", ""),
            ("năng suất mỗi người như nhau", ""), ("same productivity and daily working hours", ""),
            ("khẩu phần mỗi người mỗi ngày không đổi", ""), ("each person's daily portion unchanged", "") })
        {
            var changed = draft with { Facts = draft.Facts!.Select(f => f with { Text = f.Text.Replace(from, to) }).ToArray(),
                Question = draft.Question.Replace(from, to) };
            if (changed.ProblemText != draft.ProblemText) yield return changed;
        }
    }

    public static async Task RunAsync()
    {
        int count = 0, alternatives = 0;
        var coverage = new HashSet<(AppLanguage, string)>();
        foreach (var c in Cases())
        {
            var lesson = ReasoningStoryCatalogue.Lesson(c);
            var p = lesson.QuestionModel.ProportionProblem!;
            coverage.Add((c.Language, p.NarrativeId));
            Check(c.IsValid, "Invalid proportion contract");
            // Named projection must still render the original positional JSON exactly.
            var row = QuizContentCatalog.LoadList<JsonElement>("ProportionQuizGenerator.Templates", QuizContentCatalog.Culture(c.Language))
                .Single(r => r.GetProperty("NarrativeId").GetString() == p.NarrativeId);
            Check(p.ProblemText == string.Format(CultureInfo.CurrentCulture, row.GetProperty("Template").GetString()!, p.A, p.B, p.C), "Named roles changed original data");
            Check(lesson.Quantities.Where(v => v.Id.StartsWith('f')).All(v => v.Role is not ("given" or "constant")), "Unnamed proportion role");
            var draft = ReasoningStoryCatalogue.Draft(c);
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Canonical rejected");
            CheckPractice(c, draft);
            foreach (var f in lesson.Facts)
            foreach (string text in lesson.FactPhrasings[f.Role])
            {
                var changed = draft with { Facts = draft.Facts!.Select(v => v.Role == f.Role ? v with { Text = text } : v).ToArray() };
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(changed), c).IsValid, "Reviewed proportion rejected");
                alternatives++;
            }
            var novel = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).ToArray();
            Check(novel.Length > 0 && novel.All(d => ReviewedReasoningProse.HasNewFacts(c, d)), "No fact novelty: " + p.NarrativeId);
            var excluded = novel.Select(d => QuestionProseIdentity.Hash(c, d)).ToHashSet();
            try { ReasoningStoryValidator.Grammar(c, excluded); throw new Exception("Exhausted prose generated"); }
            catch (InvalidOperationException e) when (e.Message == "DuplicateProseRetriesExhausted") { }
            foreach (var bad in Mutations(draft))
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(bad), c).IsValid, "Unsafe prose passed");
            var numbers = new HashSet<(int, int, int)> { (p.A, p.B, p.C) };
            for (int seed = 0; seed < 3; seed++)
            {
                var fresh = c.FreshFacts(new Random(51 + seed));
                var fp = ReasoningStoryCatalogue.Lesson(fresh).QuestionModel.ProportionProblem!;
                numbers.Add((fp.A, fp.B, fp.C));
                Check(fresh.Story!.Schema == c.Story!.Schema && fp.Scenario == p.Scenario
                    && fp.AsksForAdditionalPeople == p.AsksForAdditionalPeople && fp.InverseChangesSecondQuantity == p.InverseChangesSecondQuantity, "Fresh facts changed target");
                CheckPractice(fresh, novel[seed % novel.Length]);
                Check(QuestionProseIdentity.Hash(fresh, novel[0]) == QuestionProseIdentity.Hash(c, novel[0]), "Numbers bypassed prose dedup");
            }
            Check(numbers.Count > 1, "Fresh facts did not vary numbers");
            Check(!(c with { Story = c.Story! with { NarrativeId = "missing" } }).IsValid, "Invalid narrative accepted");
            count++;
        }
        Check(coverage.Count == 94, "Missing one of 47 templates in both languages: " + coverage.Count);
        var profiles = Cases().Where(c => c.Language == AppLanguage.Vietnamese && c.Tier == CurriculumTier.FiveStars
            && c.BankVariant == (int)ProportionQuizType.Inverse).ToArray();
        var all = profiles.SelectMany(c => ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>())
            .Select(d => QuestionProseIdentity.Hash(c, d))).ToHashSet();
        string remaining = all.Last();
        var partiallyExhausted = all.Where(hash => hash != remaining).ToHashSet();
        var cycle = new ReasoningStoryCycle();
        var available = cycle.Next(BankQuestionFamily.Proportion, (int)ProportionQuizType.Inverse,
            CurriculumTier.FiveStars, AppLanguage.Vietnamese, partiallyExhausted);
        Check(ReviewedReasoningProse.NovelDrafts(available, partiallyExhausted).Any(), "Cycle stopped at an exhausted context");
        try
        {
            cycle.Next(BankQuestionFamily.Proportion, (int)ProportionQuizType.Inverse,
                CurriculumTier.FiveStars, AppLanguage.Vietnamese, all);
            throw new Exception("Whole exhausted catalog did not stop");
        }
        catch (InvalidOperationException e) when (e.Message == "DuplicateProseRetriesExhausted") { }
        await CheckStoreAsync();
        Console.WriteLine($"Proportion: {count} cases; 94 bilingual templates; {alternatives} reviewed clauses; fresh math/all modes/grading, unsafe prose, exhaustion, SQLite/Excel/provider passed.");
    }

    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue); }
    private static async Task CheckStoreAsync()
    {
        string dir = Path.GetFullPath(Path.Combine("artifacts", "verification", "proportion-bank"));
        Directory.CreateDirectory(dir);
        var store = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        var c = Cases().First();
        var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
        var saved = new ValidatedBankQuestion(c, draft, QuestionBankStore.SerializeDraft(draft), "test", DateTime.UtcNow);
        Check(await store.InsertAsync(saved), "Insert failed");
        Check(!await store.InsertAsync(saved with { Contract = c.FreshFacts(new(74)) }), "Duplicate prose inserted");
        Check((await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language))?.Contract.Family == c.Family, "Retrieval failed");
        using var excel = new MemoryStream(); await store.ExportExcelAsync(excel); excel.Position = 0;
        var restored = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        Check((await restored.ImportExcelAsync(excel)).Inserted == 1, "Excel roundtrip failed");
        var generated = ReasoningStoryCatalogue.ToPractice(c, draft, ArithmeticQuizMode.Essay);
        var provider = new BasicPracticeQuestionProvider(restored, new AlwaysBank());
        var selected = await provider.SelectReasoningAsync(generated, c.Tier, c.Language);
        Check(selected != generated && selected.ProportionProblem?.Type == generated.ProportionProblem!.Type, "Provider failed");
        Check(await provider.SelectReasoningAsync(generated, c.Tier, AppLanguage.English) == generated, "Wrong-language fallback failed");
    }

    public static async Task RunModelAsync(string modelPath, string? resumeDirectory = null)
    {
        string dir = resumeDirectory is null ? Path.GetFullPath(Path.Combine("artifacts", "verification", "proportion-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")))
            : Path.GetFullPath(resumeDirectory);
        Directory.CreateDirectory(dir);
        var runtime = new GgufQuestionRuntime();
        var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var evidence = new List<object>(); var rendered = new StringBuilder();
        var completed = new HashSet<string>();
        if (resumeDirectory is not null)
        {
            using var prior = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(dir, "results.json")));
            foreach (var item in prior.RootElement.EnumerateArray())
            {
                if (item.GetProperty("ErrorCode").ValueKind != JsonValueKind.Null) continue;
                string name = item.GetProperty("Case").GetString()!;
                completed.Add(name[(name.IndexOf('-') + 1)..]); evidence.Add(item.Clone());
            }
            using var workbook = new MemoryStream(); await store.ExportExcelAsync(workbook); workbook.Position = 0;
            foreach (var row in QuestionBankWorkbook.Read(workbook))
            {
                var saved = row.Question ?? throw new Exception("Invalid resume row");
                CheckPractice(saved.Contract.FreshFacts(new(881)), saved.Draft);
            }
            rendered.Append(await File.ReadAllTextAsync(Path.Combine(dir, "rendered-samples.txt")));
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        Console.WriteLine("Real proportion GGUF: " + dir);
        try
        {
            await runtime.LoadAsync(modelPath, timeout.Token);
            // Every reviewed template in both languages, plus every subtype/star/language profile.
            var cases = Cases().GroupBy(c => (c.Language, c.Story!.NarrativeId)).Select(g => g.First()).ToList();
            var additionalIds = new HashSet<(AppLanguage, string)>();
            foreach (var group in Cases().GroupBy(c => (c.Language, c.BankVariant, c.Tier)))
                if (!cases.Any(c => (c.Language, c.BankVariant, c.Tier) == group.Key))
                {
                    // Prose identity ignores stars. Do not consume three profiles of
                    // one two-alternative template merely to cover the star matrix.
                    var c = group.First(c => !additionalIds.Contains((c.Language, c.Story!.NarrativeId))
                        && ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Count() >= 2);
                    additionalIds.Add((c.Language, c.Story!.NarrativeId)); cases.Add(c);
                }
            int number = evidence.Count, maximumTokens = 0;
            foreach (var c in cases)
            {
                string key = $"{c.Language}-{c.Story!.NarrativeId}-{c.Tier}";
                if (completed.Contains(key)) continue;
                var original = ReasoningStoryCatalogue.Draft(c);
                await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "original", DateTime.UtcNow), timeout.Token);
                var excluded = (await store.GetProseHashesAsync(timeout.Token)).ToHashSet(StringComparer.Ordinal);
                string prompt = BasicQuestionPrompt.Build(c, "DuplicateProse", [original.ProblemText], excluded);
                var streamed = new StringBuilder(); AiGenerationMetrics? metrics = null;
                string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, t => streamed.Append(t), m => metrics = m);
                var result = BasicQuestionValidator.Validate(raw, c);
                string name = $"{++number}-{c.Language}-{c.Story!.NarrativeId}-{c.Tier}";
                await File.WriteAllTextAsync(Path.Combine(dir, name + ".txt"), prompt + "\nRAW\n" + raw + "\nVALIDATION\n" + (result.ErrorCode ?? "Valid"));
                evidence.Add(new { Case = name, result.ErrorCode, result.ErrorDetails, Metrics = metrics });
                Check(result.IsValid && metrics?.GeneratedTokens > 1 && raw == streamed.ToString(), "Native generation failed: " + name + " " + result.ErrorCode);
                maximumTokens = Math.Max(maximumTokens, metrics!.GeneratedTokens);
                Check(ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Native prose did not vary facts");
                var saved = new ValidatedBankQuestion(c, result.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                Check(await store.InsertAsync(saved, timeout.Token) && !await store.InsertAsync(saved, timeout.Token), "Native dedup failed");
                var fresh = c.FreshFacts(new(881)); CheckPractice(fresh, result.Draft!);
                var p = ReasoningStoryCatalogue.ToPractice(fresh, result.Draft!, ArithmeticQuizMode.Essay).ProportionProblem!;
                rendered.AppendLine(name).AppendLine(p.ProblemText).AppendLine(p.SolutionText).AppendLine();
                Console.WriteLine($"PASS {name}: {metrics.GeneratedTokens} tokens; native stream/dedup/fresh math/grading");
            }
            await runtime.ReleaseAsync();
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese, 4, true,
                Family: BankQuestionFamily.Proportion, StoryVariant: (int)ProportionQuizType.Inverse));
            try { await worker.Completion.WaitAsync(timeout.Token); }
            catch { worker.Stop(); await worker.Completion; throw; }
            await File.WriteAllTextAsync(Path.Combine(dir, "worker.json"), JsonSerializer.Serialize(worker.Snapshot, new JsonSerializerOptions { WriteIndented = true }));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Worker failed: " + worker.Snapshot.Error);
            foreach (var item in worker.Snapshot.Items) CheckPractice(item.Contract.FreshFacts(new(103 + item.Number)), item.Question!.Draft);
            Check(!runtime.IsLoaded && runtime.CanGenerate, "Worker retained model weights");
            Console.WriteLine($"PASS real worker/save/release; maximum output {maximumTokens}/700 tokens");
        }
        finally
        {
            await runtime.EjectAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(Path.Combine(dir, "rendered-samples.txt"), rendered.ToString());
        }
        Console.WriteLine($"Real model proportion verification passed: {evidence.Count} direct generations; {dir}");
    }
}
