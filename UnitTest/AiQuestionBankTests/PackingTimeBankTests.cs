using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class PackingTimeBankTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static string[] Contexts(BankQuestionFamily family, CurriculumTier tier, AppLanguage language) =>
        family == BankQuestionFamily.Remainder ? ElementaryQuizGenerator.PackingStories(language).Select(c => c.Id).ToArray()
            : ElementaryQuizGenerator.TimeStoryContextIds(tier);
    private static IEnumerable<BasicQuestionContract> Cases()
    {
        foreach (var family in new[] { BankQuestionFamily.Remainder, BankQuestionFamily.Time })
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (int type in ReasoningStoryCatalogue.Variants(family, tier))
        foreach (string context in Contexts(family, tier, language))
            yield return ReasoningStoryCatalogue.Create(family, type, tier, language, new(47), context);
    }

    private static (int Total, int Size, int Quotient, int Remainder) PackingFacts(ElementaryQuizContract p)
    {
        var g = p.Reasoning!.Givens.ToDictionary(g => g.Role, g => int.Parse(g.Value, CultureInfo.InvariantCulture));
        int F(string role) => g.GetValueOrDefault(role);
        int total = g.ContainsKey("total") ? F("total") : F("first-batch") + F("second-batch") - F("already-accommodated");
        int size = g.ContainsKey("capacity") ? F("capacity") : F("nominal-capacity") - F("reserved-capacity");
        int quotient = Math.DivRem(total, size, out int remainder);
        return (total, size, quotient, remainder);
    }

    private static void CheckPractice(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c, draft, mode);
            var p = q.ElementaryProblem!;
            Check((int)p.Type == c.BankVariant && p.StoryContextId == c.SceneId, "Practice lost the subtype/context");
            Check(!Regex.IsMatch(p.ProblemText + p.SolutionText, @"\{[fv]\d+\}"), "Unrendered story variables");
            if (c.Family == BankQuestionFamily.Remainder)
            {
                var f = PackingFacts(p);
                var context = ElementaryQuizGenerator.PackingStories(c.Language).Single(s => s.Id == c.SceneId);
                Check(f.Total > 0 && f.Size >= 2 && f.Remainder >= 0 && f.Remainder < f.Size, "Invalid packing facts");
                int[] expected = p.Type switch {
                    ElementaryQuizType.MinimumGroups => [f.Quotient + (f.Remainder == 0 ? 0 : 1)],
                    ElementaryQuizType.Leftovers => [f.Remainder], _ => [f.Quotient, f.Remainder] };
                string[] units = p.Type switch {
                    ElementaryQuizType.MinimumGroups => [context.Container], ElementaryQuizType.Leftovers => [context.Item],
                    _ => [context.Container, context.Item] };
                Check(p.Answers.Count == expected.Length && p.Answers.Select(a => (int)a.Value.Numerator).SequenceEqual(expected)
                    && p.Answers.All(a => a.Value.Denominator.IsOne) && p.Answers.Select(a => a.Unit).SequenceEqual(units),
                    "Independent quotient/remainder/minimum/unit calculation failed");
                Check(q.ExactAnswers.SequenceEqual(c.ExactAnswers) && q.ExactAnswers.Count == expected.Length,
                    "A multi-part answer was reduced to its primary value");
                if (p.Answers.Count == 2)
                {
                    Check(p.AnswerText.Contains(p.Answers[0].Label) && p.AnswerText.Contains(p.Answers[1].Label), "Multi-part display lost labels");
                    Check(!ElementaryEssayValidator.CheckAnswers(q, ElementaryQuizContract.FormatAnswer(p.Answers[0])), "Missing remainder accepted");
                    string wrong = p.Answers[0].Label + ": " + ElementaryQuizContract.FormatAnswer(p.Answers[0]) + "; "
                        + p.Answers[1].Label + ": " + (f.Remainder + 1) + " " + context.Item;
                    Check(!ElementaryEssayValidator.CheckAnswers(q, wrong), "Correct quotient hid a wrong remainder");
                    Check(!ElementaryEssayValidator.CheckAnswers(q, f.Remainder + " " + context.Item + "; " + f.Quotient + " " + context.Container),
                        "Unlabelled reversed quantities accepted");
                }
            }
            else
            {
                var g = p.Reasoning!.Givens.ToDictionary(g => g.Role, g => int.Parse(g.Value, CultureInfo.InvariantCulture));
                int F(string role) => g.GetValueOrDefault(role);
                decimal expected;
                if (p.Type == ElementaryQuizType.ElapsedTime)
                {
                    int start = F("start-hour") * 60 + F("start-minute"), end = F("end-hour") * 60 + F("end-minute");
                    if (end < start) end += 1440;
                    expected = end - start - F("pause-minutes") + F("second-session");
                    string marker = c.Language == AppLanguage.Vietnamese
                        ? c.Tier >= CurriculumTier.ThreeStars ? "ngày hôm sau" : "cùng ngày"
                        : c.Tier >= CurriculumTier.ThreeStars ? "the next day" : "on the same day";
                    Check(p.ProblemText.Contains(marker), "Time story lost same-day/next-day constraint");
                }
                else
                {
                    expected = (F("first-hours") + F("second-hours")) * 60 + F("first-minutes") + F("extra-minutes") - F("pause-minutes");
                    if (c.Tier == CurriculumTier.FiveStars) expected /= 60;
                }
                Check(expected > 0 && (decimal)q.ExactAnswer.Numerator / (decimal)q.ExactAnswer.Denominator == expected
                    && q.ExactAnswers.Count == 1 && q.ExactAnswer == c.ExactAnswer, "Independent active duration failed");
                string unit = c.Language == AppLanguage.Vietnamese
                    ? p.Type == ElementaryQuizType.TimeAddition && c.Tier == CurriculumTier.FiveStars ? "giờ" : "phút"
                    : p.Type == ElementaryQuizType.TimeAddition && c.Tier == CurriculumTier.FiveStars ? "hours" : "minutes";
                Check(p.Answers[0].Unit == unit, "Wrong time answer unit");
            }
            Check(p.ChoiceTexts!.Count == 4 && p.ChoiceTexts.Distinct().Count() == 4
                && p.ChoiceTexts.Count(choice => ElementaryEssayValidator.CheckAnswers(q, choice)) == 1, "Invalid multiple-choice answer tuple");
            Check(ElementaryEssayValidator.CheckAnswers(q, p.PresentedText) == q.PresentedEquationIsCorrect, "True/false tuple disagreement");
            Check(ElementaryEssayValidator.CheckAnswers(q, p.AnswerText), "Full answer text rejected");
            Check(!ElementaryEssayValidator.CheckAnswers(q, string.Join("; ", p.Answers.Select(a => a.Label + ": " + a.Value + " kg"))),
                "Wrong answer dimensions accepted");
            if (mode == ArithmeticQuizMode.Essay)
            {
                var input = EssayCombinedInputParser.Parse(p.SolutionText, true, preserveAllCalculations: true);
                var result = new EssayAnswerValidator(new BasicArithmeticEngine()).Validate(q, input.Solution, input.Equation, input.Answer);
                Check(result.IsCorrect, "Stored narrative solution failed essay grading: " + c.Family + "/" + p.Type + "\n" + p.SolutionText);
            }
        }
    }

    private static BasicQuestionContract Boundary(ElementaryQuizType type, AppLanguage language, int remainderKind)
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.Remainder, (int)type, CurriculumTier.FiveStars,
                language, new(seed), "traffic");
            var f = PackingFacts(ReasoningStoryCatalogue.ToPractice(c, ReasoningStoryCatalogue.Draft(c), ArithmeticQuizMode.Essay).ElementaryProblem!);
            if (f.Remainder == (remainderKind == 2 ? f.Size - 1 : remainderKind)) return c;
        }
        throw new Exception("Packing boundary not found");
    }

    public static async Task RunAsync()
    {
        int count = 0;
        foreach (var c in Cases())
        {
            Check(c.IsValid, "Invalid packing/time bank contract");
            var original = ReasoningStoryCatalogue.Draft(c);
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(original), c).IsValid, "Canonical story rejected");
            CheckPractice(c, original);
            var novel = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Take(6).ToArray();
            Check(novel.Length >= 2, "No reviewed fact variants");
            foreach (var d in novel)
            {
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), c).IsValid, "Reviewed prose rejected");
                CheckPractice(c, d);
            }
            foreach (int seed in new[] { 881, 29, 164 })
            {
                var fresh = c.FreshFacts(new(seed));
                Check(fresh.Story!.Schema == c.Story!.Schema && fresh.SceneId == c.SceneId, "Fresh data changed the story schema");
                CheckPractice(fresh, novel[0]);
                Check(QuestionProseIdentity.Hash(c, novel[0]) == QuestionProseIdentity.Hash(fresh, novel[0]), "Changed numbers evaded prose dedup");
            }
            foreach (var bad in new[] {
                original with { Question = "How many full groups?" },
                original with { Question = original.Question + " 25 kg" },
                original with { Facts = original.Facts!.Skip(1).ToArray() },
                original with { Facts = original.Facts!.Reverse().ToArray() },
                original with { Facts = original.Facts!.Select(f => f with { Text = f.Text.Replace("{f0}", "{f999}") }).ToArray() },
                original with { Facts = original.Facts!.Select(f => f with { Text = f.Text.Replace("tối đa", "ít nhất").Replace("at most", "at least")
                    .Replace("không tính", "tính thêm").Replace("ngày hôm sau", "cùng ngày").Replace("the next day", "on the same day") }).ToArray() }
            }.Where(d => QuestionBankStore.SerializeDraft(d) != QuestionBankStore.SerializeDraft(original)))
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(bad), c).IsValid, "Unsafe relation/target/units accepted");
            Check(!(c with { Story = c.Story! with { NarrativeId = "retired" } }).IsValid, "Unknown context accepted");
            count++;
        }
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var type in ElementaryQuizGenerator.RemainderStoryTypes)
        for (int boundary = 0; boundary < 3; boundary++)
        {
            var c = Boundary(type, language, boundary); CheckPractice(c, ReasoningStoryCatalogue.Draft(c));
        }
        bool proposedWrongRemainder = false;
        for (int seed = 0; seed < 80; seed++)
        {
            var q = new ElementaryQuizGenerator(new(seed)).GenerateRemainderStory(ArithmeticQuizMode.TrueFalse,
                ElementaryQuizType.QuotientRemainder, AppLanguage.Vietnamese, CurriculumTier.FiveStars, "traffic");
            var p = q.ElementaryProblem!;
            Check(p.ChoiceTexts!.Any(choice => choice.StartsWith(p.Answers[0].Label + ": " + ElementaryQuizContract.FormatAnswer(p.Answers[0]) + ";")
                && !ElementaryEssayValidator.CheckAnswers(q, choice)), "No distractor exercises the remainder independently");
            proposedWrongRemainder |= q.PresentedEquationIsCorrect == false
                && p.PresentedText!.StartsWith(p.Answers[0].Label + ": " + ElementaryQuizContract.FormatAnswer(p.Answers[0]) + ";");
        }
        Check(proposedWrongRemainder, "True/false only changed the quotient");
        foreach (var c in Cases().GroupBy(c => c.Family + "/" + c.BankVariant + "/" + c.Tier).Select(g => g.First()))
        {
            var hashes = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Select(d => QuestionProseIdentity.Hash(c, d)).ToHashSet();
            try { ReasoningStoryValidator.Grammar(c, hashes); throw new Exception("Exhausted prose allowed"); }
            catch (InvalidOperationException e) when (e.Message == "DuplicateProseRetriesExhausted") { }
        }
        await CheckStoreAsync();
        Console.WriteLine($"Packing/time: {count} bilingual context/type/star profiles, 18 packing boundaries; all results/units, fresh facts, three modes/grading, unsafe prose, exhaustion, SQLite/Excel/provider passed.");
    }

    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue); }
    private static async Task CheckStoreAsync()
    {
        string dir = Path.GetFullPath("artifacts/verification/packing-time-bank"); Directory.CreateDirectory(dir);
        var store = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        var c = Boundary(ElementaryQuizType.QuotientRemainder, AppLanguage.Vietnamese, 1);
        var time = Cases().First(c => c.Family == BankQuestionFamily.Time && c.Tier == CurriculumTier.FiveStars);
        foreach (var profile in new[] { c, time })
        {
            var d = ReviewedReasoningProse.NovelDrafts(profile, new HashSet<string>()).First();
            Check(await store.InsertAsync(new(profile, d, QuestionBankStore.SerializeDraft(d), "test", DateTime.UtcNow)), "Bank insert failed");
            Check(!await store.InsertAsync(new(profile.FreshFacts(new(77)), d, QuestionBankStore.SerializeDraft(d), "test", DateTime.UtcNow)), "Bank accepted duplicate wording");
        }
        using var workbook = new MemoryStream(); await store.ExportExcelAsync(workbook); workbook.Position = 0;
        var restored = new QuestionBankStore(Path.Combine(dir, Guid.NewGuid() + ".db3"));
        Check((await restored.ImportExcelAsync(workbook)).Inserted == 2, "Excel import lost templates");
        foreach (var profile in new[] { c, time })
        {
            var saved = await restored.TakeReasoningAsync(profile.Family, profile.BankVariant, profile.Tier, profile.Language);
            Check(saved is not null && saved.Contract.ExactAnswers.SequenceEqual(profile.ExactAnswers), "SQLite/Excel lost secondary answer");
            CheckPractice(saved!.Contract.FreshFacts(new(74)), saved.Draft);
            var original = ReasoningStoryCatalogue.ToPractice(profile, ReasoningStoryCatalogue.Draft(profile), ArithmeticQuizMode.Essay);
            var provider = new BasicPracticeQuestionProvider(restored, new AlwaysBank());
            Check(await provider.SelectReasoningAsync(original, profile.Tier, profile.Language) != original, "Provider failed to use bank");
        }
        var fallback = new BasicPracticeQuestionProvider(restored, new AlwaysBank());
        foreach (var type in new[] { ElementaryQuizType.ReadClock, ElementaryQuizType.Calendar })
        {
            var q = new ElementaryQuizGenerator(new(31)).Generate(ArithmeticQuizMode.Essay, QuizProblemKind.Time, type, AppLanguage.Vietnamese, CurriculumTier.FiveStars);
            Check(await fallback.SelectReasoningAsync(q, CurriculumTier.FiveStars, AppLanguage.Vietnamese) == q, "Bank replaced clock/calendar rendering");
        }
    }

    public static async Task RunModelAsync(string path, string directory, string part)
    {
        string dir = Path.GetFullPath(directory); Directory.CreateDirectory(dir);
        var runtime = new GgufQuestionRuntime(); var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        try
        {
            await runtime.LoadAsync(path, timeout.Token);
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
        finally { await runtime.EjectAsync(); }
    }

    private static BasicQuestionContract CheckEvidence(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var c = doc.RootElement.GetProperty("Contract").Deserialize<BasicQuestionContract>()!;
        var result = BasicQuestionValidator.Validate(doc.RootElement.GetProperty("Raw").GetString()!, c);
        Check(result.IsValid && ReviewedReasoningProse.HasNewFacts(c, result.Draft!), "Native evidence invalid");
        Check(doc.RootElement.GetProperty("Metrics").GetProperty("GeneratedTokens").GetInt32() > 1, "Evidence has no sampled tokens");
        CheckPractice(c, result.Draft!); CheckPractice(c.FreshFacts(new(881)), result.Draft!);
        return c;
    }

    public static async Task CheckEvidenceAsync(string directory)
    {
        string dir = Path.GetFullPath(directory); int count = 0; var samples = new StringBuilder();
        foreach (string file in Directory.GetFiles(dir, "*.json").Where(file => !file.EndsWith("-worker.json")))
        {
            var c = CheckEvidence(file); count++;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var d = BasicQuestionValidator.Validate(doc.RootElement.GetProperty("Raw").GetString()!, c).Draft!;
            var p = ReasoningStoryCatalogue.ToPractice(c.FreshFacts(new(881)), d, ArithmeticQuizMode.Essay).ElementaryProblem!;
            samples.AppendLine(Path.GetFileNameWithoutExtension(file)).AppendLine(p.ProblemText).AppendLine(p.SolutionText).AppendLine();
        }
        var store = new QuestionBankStore(Path.Combine(dir, "model.db3"));
        using var workbook = new MemoryStream(); await store.ExportExcelAsync(workbook); workbook.Position = 0;
        var rows = QuestionBankWorkbook.Read(workbook).Select(row => row.Question).Where(q => q is not null && q.ModelName != "original").ToArray();
        Check(rows.Length >= count, "Native SQLite rows missing");
        foreach (var q in rows) CheckPractice(q!.Contract.FreshFacts(new(881)), q.Draft);
        await File.WriteAllTextAsync(Path.Combine(dir, "rendered-samples.txt"), samples.ToString());
        Console.WriteLine($"PASS {count} actual native outputs and {rows.Length} SQLite/Excel model rows with fresh data, all answers/units and all three grading modes.");
    }
}
