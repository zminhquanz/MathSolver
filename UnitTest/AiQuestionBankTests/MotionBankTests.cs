using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class MotionBankTests
{
    public static async Task CheckPromptBudgetAsync(string path)
    {
        using var weights = await LLama.LLamaWeights.LoadFromFileAsync(new LLama.Common.ModelParams(path)
            { GpuLayerCount = 0, UseMemorymap = true });
        int maximum = 0, count = 0;
        foreach (var c in Cases())
        {
            var original = ReasoningStoryCatalogue.Draft(c);
            var excluded = new HashSet<string> { QuestionProseIdentity.Hash(c, original) };
            string prompt = BasicQuestionPrompt.Build(c, "DuplicateProse", [original.ProblemText], excluded);
            int tokens = weights.Tokenize(GgufQuestionRuntime.FormatChatPrompt(weights, c, prompt), true, true, Encoding.UTF8).Length;
            GgufQuestionRuntime.GetContextTokens(tokens);
            maximum = Math.Max(maximum, tokens); count++;
        }
        Console.WriteLine($"Motion Gemma prompt budget: {count} cases, maximum {maximum} input tokens; fits context 2048, output 700, margin 64.");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    internal static IEnumerable<BasicQuestionContract> Cases(int seeds = 8)
    {
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in Enum.GetValues<MotionQuizType>())
        for (int seed = 0; seed < seeds; seed++)
            yield return ReasoningStoryCatalogue.Create(BankQuestionFamily.Motion, (int)type, tier, language, new(seed));
    }

    private static BigInteger LengthScale(string unit) => unit switch
    { "km" => 1000000, "m" => 1000, "cm" => 10, "mm" => 1, "mile" or "miles" => 1609344, _ => throw new Exception("Unknown distance unit: " + unit) };
    private static BigInteger TimeScale(string unit) => unit switch
    { "giờ" or "hour" or "hours" => 3600, "phút" or "minute" or "minutes" => 60,
        "giây" or "second" or "seconds" => 1, _ => throw new Exception("Unknown time unit: " + unit) };
    private static (BigInteger Length, BigInteger Time) RateScale(string unit) => unit switch
    { "km/h" => (1000000, 3600), "km/min" => (1000000, 60), "m/s" => (1000, 1),
        "cm/s" => (10, 1), "mm/s" => (1, 1), "mph" => (1609344, 3600), _ => throw new Exception("Unknown speed unit: " + unit) };

    private static void CheckMath(MotionQuizContract m)
    {
        var f = m.Facts;
        BigInteger numerator, denominator = 1;
        if (m.Type == MotionQuizType.River)
        {
            numerator = m.QuestionKind switch {
                MotionQuestionKind.RiverDownstreamSpeed => f[0] + f[1],
                MotionQuestionKind.RiverUpstreamSpeed => f[0] - f[1],
                MotionQuestionKind.RiverBoatSpeed => f[0] + f[1],
                _ => f[0] - f[1] };
            if (m.QuestionKind is MotionQuestionKind.RiverBoatSpeed or MotionQuestionKind.RiverCurrentSpeed) denominator = 2;
        }
        else
        {
            string speedUnit = m.RequiredProblemUnits.Single(u => u.Contains('/') || u == "mph");
            var (length, time) = RateScale(speedUnit);
            string timeUnit = m.RequiredProblemUnits.Single(u => u is "giờ" or "phút" or "giây" or "hours" or "minutes" or "seconds" or "hour" or "minute" or "second");
            string distanceUnit = m.RequiredProblemUnits.Single(u => u != speedUnit && u != timeUnit);
            var ds = LengthScale(distanceUnit); var ts = TimeScale(timeUnit);
            (numerator, denominator) = m.QuestionKind switch {
                MotionQuestionKind.BasicDistance => (f[0] * length * f[1] * ts, time * ds),
                MotionQuestionKind.BasicRestDistance => (f[0] * length * (f[1] - f[2]) * ts, time * ds),
                MotionQuestionKind.BasicSpeed => (f[0] * ds * time, f[1] * ts * length),
                MotionQuestionKind.BasicTime => (f[1] * ds * time, f[0] * length * ts),
                MotionQuestionKind.CatchUpTime => (f[0] * ds * time, (f[2] - f[1]) * length * ts),
                MotionQuestionKind.MeetingTime => (f[0] * ds * time, (f[1] + f[2]) * length * ts),
                _ => throw new Exception("Unsupported motion kind") };
        }
        Check(denominator > 0 && numerator > 0 && numerator % denominator == 0 && numerator / denominator == m.CorrectAnswer,
            "Independent direction/rest/unit arithmetic failed: " + m.QuestionKind);
    }

    internal static void CheckPractice(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c, draft, mode);
            var m = q.MotionProblem!;
            Check(m is not null && m.Type == (MotionQuizType)c.BankVariant && q.CorrectAnswer == c.Answer, "Lost motion contract");
            CheckMath(m!);
            Check(!Regex.IsMatch(m!.ProblemText + m.SolutionText, @"\{[fv]\d+\}"), "Unrendered motion slot");
            Check(m.RequiredProblemUnits.All(u => m.ProblemText.Contains(u, StringComparison.Ordinal)), "Lost problem unit");
            Check(new BasicArithmeticEngine().CalculateInteger(q.Expression).Result == q.CorrectAnswer, "Representative arithmetic changed");
            if (mode == ArithmeticQuizMode.TrueFalse) Check(q.PresentedEquationIsCorrect == (q.PresentedAnswer == q.CorrectAnswer), "Broken true/false");
            if (mode == ArithmeticQuizMode.MultipleChoice) Check(q.Choices.Count == 4 && q.Choices.Contains(q.CorrectAnswer), "Broken choices");
            if (mode == ArithmeticQuizMode.Essay)
            {
                var input = EssayCombinedInputParser.Parse(m.SolutionText, true, preserveAllCalculations: true);
                var grade = new EssayAnswerValidator(new BasicArithmeticEngine()).Validate(q, input.Solution, input.Equation, input.Answer);
                Check(grade.IsCorrect, "Motion essay grading failed: " + m.SolutionText + "\n" + grade);
            }
        }
    }

    public static async Task RunAsync()
    {
        int count = 0, alternatives = 0;
        var kinds = new HashSet<(AppLanguage, MotionQuestionKind)>();
        foreach (var c in Cases())
        {
            var lesson = ReasoningStoryCatalogue.Lesson(c);
            kinds.Add((c.Language, lesson.QuestionModel.MotionProblem!.QuestionKind));
            Check(c.IsValid, "Invalid motion contract");
            var draft = ReasoningStoryCatalogue.Draft(c);
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Canonical motion rejected");
            CheckPractice(c, draft);
            foreach (var fact in lesson.Facts)
            foreach (string text in lesson.FactPhrasings[fact.Role])
            {
                var changed = draft with { Facts = draft.Facts!.Select(f => f.Role == fact.Role ? f with { Text = text } : f).ToArray() };
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(changed), c).IsValid, "Reviewed motion fact rejected");
                alternatives++;
            }
            foreach (string text in lesson.QuestionPhrasings)
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft with { Question = text }), c).IsValid, "Reviewed target rejected");
            var novel = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).ToArray();
            Check(novel.Length >= 2 && novel.All(d => ReviewedReasoningProse.HasNewFacts(c, d)), "No factual novelty");
            var excluded = novel.Select(d => QuestionProseIdentity.Hash(c, d)).ToHashSet();
            try { ReasoningStoryValidator.Grammar(c, excluded); throw new Exception("Exhausted wording still generated"); }
            catch (InvalidOperationException e) when (e.Message == "DuplicateProseRetriesExhausted") { }
            foreach (var bad in Mutations(draft, c.Language))
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(bad), c).IsValid, "Unsafe motion prose passed");
            for (int seed = 0; seed < 3; seed++)
            {
                var fresh = c.FreshFacts(new Random(81 + seed));
                Check(fresh.Story!.Schema == c.Story!.Schema, "Changed motion roles or units");
                CheckPractice(fresh, novel[seed % novel.Length]);
                Check(QuestionProseIdentity.Hash(fresh, novel[0]) == QuestionProseIdentity.Hash(c, novel[0]), "Random actors/numbers changed identity");
            }
            count++;
        }
        Check(kinds.Count == 20, "Not all ten motion kinds covered in both languages: " + kinds.Count);
        await CheckStoreAsync();
        Console.WriteLine($"Motion: {count} subtype/star/language/seed cases; {alternatives} reviewed fact clauses; all ten kinds, three fresh sets/all modes, unsafe prose, exhaustion, SQLite, Excel, provider passed.");
    }

    private static IEnumerable<BasicQuestionDraft> Mutations(BasicQuestionDraft draft, AppLanguage language)
    {
        yield return draft with { Question = draft.Question + " 123" };
        yield return draft with { Question = draft.Question.Replace("}", "_bad}") };
        yield return draft with { Facts = draft.Facts!.Select((f, i) => i == 0 ? f with { Role = "wrong" } : f).ToArray() };
        string Swap(string text) => text.Replace("{f0}", "{temp}").Replace("{f1}", "{f0}").Replace("{temp}", "{f1}");
        yield return draft with { Facts = draft.Facts!.Select(f => f with { Text = Swap(f.Text) }).ToArray() };
        // Flip physical relations/targets, remove rest clauses, corrupt units.
        foreach (var (from, to) in new[] { ("cùng chiều", "ngược chiều"), ("ngược chiều", "cùng chiều"),
            ("toward each other", "in the same direction"), ("same direction", "opposite directions"),
            ("xuôi dòng", "ngược dòng"), ("downstream", "upstream"), ("nước yên", "ngược dòng"),
            ("still water", "upstream"), ("thời gian", "quãng đường"), ("travel time", "distance") })
        {
            var changed = draft with { Facts = draft.Facts!.Select(f => f with { Text = f.Text.Replace(from, to) }).ToArray(), Question = draft.Question.Replace(from, to) };
            if (changed.ProblemText != draft.ProblemText) yield return changed;
        }
        if (draft.Facts!.Count > 1) yield return draft with { Facts = draft.Facts.Take(1).ToArray() };
        yield return draft with { Question = draft.Question + (language == AppLanguage.Vietnamese ? " Không tính thời gian nghỉ." : " Include the rest time as travel time.") };
    }

    private static async Task CheckStoreAsync()
    {
        string dir = Path.GetFullPath(Path.Combine("artifacts", "verification", "motion-bank"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, Guid.NewGuid() + ".db3");
        try
        {
            var store = new QuestionBankStore(path);
            var c = Cases(1).First();
            var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
            var saved = new ValidatedBankQuestion(c, draft, QuestionBankStore.SerializeDraft(draft), "test", DateTime.UtcNow);
            Check(await store.InsertAsync(saved), "Motion insertion failed");
            Check(!await store.InsertAsync(saved with { Contract = c.FreshFacts(new(74)) }), "Fresh numbers bypassed duplicate prose");
            var row = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
            Check(row is not null && row.Contract.Family == BankQuestionFamily.Motion, "Motion retrieval failed");
            using var stream = new MemoryStream();
            await store.ExportExcelAsync(stream);
            var restored = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
            stream.Position = 0;
            var result = await restored.ImportExcelAsync(stream);
            Check(result.Inserted == 1, "Motion Excel roundtrip failed");
            var generated = ReasoningStoryCatalogue.ToPractice(c, draft, ArithmeticQuizMode.Essay);
            var provider = new BasicPracticeQuestionProvider(store, new AlwaysBank());
            CheckPractice(c, draft);
            var selected = await provider.SelectReasoningAsync(generated, c.Tier, c.Language);
            Check(selected != generated && selected.MotionProblem is not null && selected.MotionProblem.Type == generated.MotionProblem!.Type, "Motion provider did not use matching SQLite template");
            var empty = new BasicPracticeQuestionProvider(restored, new AlwaysBank());
            var differentLanguage = c.Language == AppLanguage.Vietnamese ? AppLanguage.English : AppLanguage.Vietnamese;
            Check(await empty.SelectReasoningAsync(generated, c.Tier, differentLanguage) == generated, "Empty/wrong-language bank did not fall back to C#");
        }
        finally { File.Delete(path); }
    }
    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue); }

    public static async Task WriteRenderedEvidenceAsync(string directory)
    {
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        using var excel = new MemoryStream();
        await store.ExportExcelAsync(excel); excel.Position = 0;
        var rendered = new StringBuilder();
        foreach (var row in QuestionBankWorkbook.Read(excel))
        {
            var saved = row.Question ?? throw new Exception("Invalid evidence row: " + row.ErrorCode);
            var q = ReasoningStoryCatalogue.ToPractice(saved.Contract, saved.Draft, ArithmeticQuizMode.Essay);
            CheckPractice(saved.Contract, saved.Draft);
            rendered.AppendLine($"{saved.Contract.Language} / {q.MotionProblem!.QuestionKind} / {(int)saved.Contract.Tier} stars / {saved.ModelName}")
                .AppendLine(q.MotionProblem.ProblemText).AppendLine(q.MotionProblem.SolutionText).AppendLine();
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "rendered-samples.txt"), rendered.ToString());
        Console.WriteLine("Validated rendered evidence written: " + directory);
    }

    public static async Task RunModelAsync(string modelPath)
    {
        string dir = Path.GetFullPath(Path.Combine("artifacts", "verification", "motion-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(dir);
        var runtime = new GgufQuestionRuntime();
        var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        var evidence = new List<object>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(60));
        Console.WriteLine("Real GGUF motion verification: " + dir);
        try
        {
            await runtime.LoadAsync(modelPath, timeout.Token);
            var cases = Cases(1).ToList();
            // Also cover any fine-grained target not selected by the first seed.
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            foreach (var kind in Enum.GetValues<MotionQuestionKind>())
                if (!cases.Any(c => c.Language == language && ReasoningStoryCatalogue.Lesson(c).QuestionModel.MotionProblem!.QuestionKind == kind))
                    cases.Add(Cases(20).First(c => c.Language == language && ReasoningStoryCatalogue.Lesson(c).QuestionModel.MotionProblem!.QuestionKind == kind));
            int number = 0;
            foreach (var c in cases)
            {
                var original = ReasoningStoryCatalogue.Draft(c);
                await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "original", DateTime.UtcNow), timeout.Token);
                int repeats = c.Tier == CurriculumTier.FiveStars ? 2 : 1;
                for (int repeat = 0; repeat < repeats; repeat++)
                {
                    var excluded = (await store.GetProseHashesAsync(timeout.Token)).ToHashSet(StringComparer.Ordinal);
                    string prompt = BasicQuestionPrompt.Build(c, repeat == 0 ? null : "DuplicateProse", excludedProse: excluded);
                    var streamed = new StringBuilder(); AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, t => streamed.Append(t), m => metrics = m);
                    var result = BasicQuestionValidator.Validate(raw, c);
                    string name = $"{++number}-{c.Language}-{(MotionQuizType)c.BankVariant}-{c.Tier}-{repeat}";
                    await File.WriteAllTextAsync(Path.Combine(dir, name + ".txt"), prompt + "\nRAW\n" + raw + "\nVALIDATION\n" + (result.ErrorCode ?? "Valid"));
                    evidence.Add(new { Case = name, Kind = ReasoningStoryCatalogue.Lesson(c).QuestionModel.MotionProblem!.QuestionKind.ToString(), result.ErrorCode, result.ErrorDetails, Metrics = metrics });
                    Check(result.IsValid && metrics?.GeneratedTokens > 1 && raw == streamed.ToString(), "Native motion rejected: " + name + " " + result.ErrorCode);
                    Check(ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Native motion did not change facts");
                    var saved = new ValidatedBankQuestion(c, result.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                    Check(await store.InsertAsync(saved, timeout.Token) && !await store.InsertAsync(saved, timeout.Token), "Native SQLite dedup failed");
                    CheckPractice(c.FreshFacts(new(881 + repeat)), result.Draft!);
                    Console.WriteLine($"PASS {name}: {metrics?.GeneratedTokens} tokens; native stream/SQLite/fresh math/grading");
                }
            }
            await runtime.ReleaseAsync();
            var worker = new AiQuestionGenerationService(runtime, store);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, 4, true,
                Family: BankQuestionFamily.Motion, StoryVariant: (int)MotionQuizType.Meeting));
            try { await worker.Completion.WaitAsync(timeout.Token); }
            catch { worker.Stop(); await worker.Completion; throw; }
            await File.WriteAllTextAsync(Path.Combine(dir, "worker.json"), JsonSerializer.Serialize(worker.Snapshot, new JsonSerializerOptions { WriteIndented = true }));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Motion worker failed: " + worker.Snapshot.Error);
            foreach (var item in worker.Snapshot.Items) CheckPractice(item.Contract.FreshFacts(new(103 + item.Number)), item.Question!.Draft);
            Check(!runtime.IsLoaded && runtime.CanGenerate, "Motion worker retained model");
            Console.WriteLine("PASS real motion worker batch/save/release");
        }
        finally
        {
            await runtime.EjectAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, "results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine("Real motion model verification passed: " + evidence.Count + " direct generations; " + dir);
        await WriteRenderedEvidenceAsync(dir);
    }
}
