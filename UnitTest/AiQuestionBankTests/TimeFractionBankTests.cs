using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class TimeFractionBankTests
{
    internal static readonly ElementaryQuizType[] Types = [ElementaryQuizType.SecondsConversion, ElementaryQuizType.YearCentury,
        ElementaryQuizType.CalendarWeekday, ElementaryQuizType.OrderRelatedFractions];
    private static BankQuestionFamily Family(ElementaryQuizType type) => type == ElementaryQuizType.OrderRelatedFractions
        ? BankQuestionFamily.FractionQuantity : BankQuestionFamily.Time;

    internal static async Task RunAsync()
    {
        int cases = 0;
        foreach (var type in Types)
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 8; seed++)
        {
            var c = ReasoningStoryCatalogue.Create(Family(type), (int)type, tier, language, new Random(seed));
            Check(c.IsValid, "Invalid extension bank profile: " + type);
            var drafts = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Take(2).ToArray();
            Check(drafts.Length == 2, "Insufficient reviewed novel facts: " + type);
            foreach (var draft in drafts)
            {
                string raw = QuestionBankStore.SerializeDraft(draft);
                Check(BasicQuestionValidator.Validate(raw, c).IsValid, "Rejected reviewed extension prose: " + raw);
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft with { Question = draft.Question + " 999?" }), c).IsValid,
                    "An added numeric condition passed.");
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft with {
                    Facts = draft.Facts!.Select(f => f with { Text = f.Text.Replace("{f0}", "{f999}") }).ToArray() }), c).IsValid,
                    "An altered quantity placeholder passed.");
                for (int freshSeed = 0; freshSeed < 2; freshSeed++) Verify(c.FreshFacts(new Random(713 + freshSeed)), draft);
                cases++;
            }
            Check(GgufQuestionRuntime.BuildNovelGrammar(c, new HashSet<string>()).Contains("root ::="), "Missing native extension grammar.");
        }
        using var workspace = TestWorkspace.Temporary("TimeFractionBank");
        var store = new QuestionBankStore(workspace.FilePath("bank.db3"));
        var provider = new BasicPracticeQuestionProvider(store, new AlwaysBank());
        foreach (var type in Types)
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var c = ReasoningStoryCatalogue.Create(Family(type), (int)type, CurriculumTier.ThreeStars, language, new Random(731));
            var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
            var saved = new ValidatedBankQuestion(c, draft, QuestionBankStore.SerializeDraft(draft), "reviewed-test", DateTime.UtcNow);
            Check(await store.InsertAsync(saved), "Extension SQLite insertion failed.");
            Check(!await store.InsertAsync(saved), "Extension prose duplicate was inserted.");
            var restored = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
            Check(restored is not null && QuestionBankStore.ValidateSavedQuestion(restored).IsValid, "Extension round-trip failed.");
            Verify(restored!.Contract.FreshFacts(new Random(739)), restored.Draft);
            var generated = new ElementaryQuizGenerator(new Random(744)).GenerateTimeFractionExtension(ArithmeticQuizMode.Essay, type, language, c.Tier);
            var selected = await provider.SelectReasoningAsync(generated, c.Tier, language);
            Check(selected.ElementaryProblem!.Type == type && selected.ElementaryProblem.StoryContextId == c.SceneId, "Practice lost the requested bank subtype.");
        }
        using var excel = new MemoryStream();
        Check((await store.ExportExcelAsync(excel)).Exported == 8, "Extension Excel export lost rows.");
        excel.Position = 0;
        var imported = new QuestionBankStore(workspace.FilePath("imported.db3"));
        Check((await imported.ImportExcelAsync(excel)).Inserted == 8, "Extension Excel import lost roles or templates.");
        foreach (var type in Types)
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var runtime = new ReviewedRuntime();
            var batchStore = new QuestionBankStore(workspace.FilePath(type + "-" + language + ".db3"));
            var worker = new AiQuestionGenerationService(runtime, batchStore);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, 10, true,
                Family: Family(type), StoryVariant: (int)type));
            await worker.Completion.WaitAsync(TimeSpan.FromMinutes(2));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.Count == 10
                && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Extension ten-item worker failed: " + type + "/" + language + ": " + worker.Snapshot.Error);
            Check(!runtime.IsLoaded, "Extension worker retained its runtime.");
        }
        Console.WriteLine($"PASS {cases} reviewed bilingual time/fraction templates; fresh exact math, all answer modes, unsafe mutations, native grammar and SQLite/provider integration.");
    }

    internal static void Verify(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c, draft, mode);
            var p = q.ElementaryProblem!;
            Check(!p.ProblemText.Contains('{') && new EssayAnswerValidator(new BasicArithmeticEngine()).ValidateSubmission(q, p.AnswerText).IsCorrect,
                "Fresh extension rendering/grading failed.");
            int G(string role) => int.Parse(p.Reasoning!.Givens.Single(g => g.Role == role).Value);
            if (p.Type == ElementaryQuizType.YearCentury)
                Check(p.Answers[0].Value == new ReducedFraction((G("year") + 99) / 100, 1), "Fresh century boundary failed.");
            if (p.Type == ElementaryQuizType.CalendarWeekday)
            {
                var date = new DateTime(G("year"), G("month"), G("day"));
                int day = ((int)date.AddDays(G("elapsed-days") * (p.StoryContextId == "weekday-forward" ? 1 : -1)).DayOfWeek + 6) % 7;
                Check(p.AnswerText == QuizContentCatalog.Text(c.Language, "TimeFraction.Weekday" + day), "Fresh weekday calculation failed.");
            }
            if (p.Type == ElementaryQuizType.SecondsConversion)
                Check(p.Answers[0].Value == new ReducedFraction(p.StoryContextId == "seconds-forward" ? G("duration") * 60 : G("duration") / 60, 1), "Fresh seconds calculation failed.");
            if (p.Type == ElementaryQuizType.OrderRelatedFractions)
            {
                var fractions = p.AnswerText.Split(" -> ").Select(s => { ReducedFraction.TryParse(s, out var f); return f; }).ToArray();
                Check(fractions.Length == p.Facts.Count / 2, "Fresh ordering lost a fraction.");
                for (int i = 1; i < fractions.Length; i++)
                    Check((fractions[i-1].Numerator * fractions[i].Denominator < fractions[i].Numerator * fractions[i-1].Denominator)
                        == (p.StoryContextId == "fractions-ascending"), "Fresh fraction order failed.");
            }
        }
    }

    internal static async Task RunModelAsync(string modelPath)
    {
        using var workspace = TestWorkspace.Artifacts("time-fraction-model", "artifacts/verification/time-fraction");
        await using var model = new ModelTestSession();
        await model.LoadAsync(modelPath);
        int count = 0;
        foreach (var type in Types)
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var c = ReasoningStoryCatalogue.Create(Family(type), (int)type, CurriculumTier.ThreeStars, language, new Random(713));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            var native = await model.GenerateAsync(c, BasicQuestionPrompt.Build(c, null), cancellation.Token, new HashSet<string>());
            await File.WriteAllTextAsync(workspace.FilePath(type + "-" + language + ".json"), JsonSerializer.Serialize(new {
                Contract = c, native.Raw, native.Metrics, native.Validation.ErrorCode, native.Validation.ErrorDetails }, new JsonSerializerOptions { WriteIndented = true }));
            Check(native.Validation.IsValid, "Native extension rejected: " + native.Validation.ErrorCode + ": " + native.Validation.ErrorDetails);
            Verify(c.FreshFacts(new Random(717)), native.Validation.Draft!);
            count++; Console.WriteLine($"PASS real GGUF {language}/{type}, fresh values and all answer modes.");
        }
        Console.WriteLine($"PASS {count} native extension profiles. Evidence: {workspace.DirectoryPath}");
    }
    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => maxValue - 1; }
    private sealed class ReviewedRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded { get; private set; }
        public bool CanGenerate => true;
        public string ModelName => "reviewed-time-fraction-test";
        public Task PrepareAsync(CancellationToken cancellationToken) { IsLoaded = true; return Task.CompletedTask; }
        public Task ReleaseAsync() { IsLoaded = false; return Task.CompletedTask; }
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null) =>
            GenerateNovelAsync(c, prompt, new HashSet<string>(), cancellationToken, onText, onMetrics);
        public Task<string> GenerateNovelAsync(BasicQuestionContract c, string prompt, IReadOnlySet<string> excluded,
            CancellationToken cancellationToken, Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string json = QuestionBankStore.SerializeDraft(ReviewedReasoningProse.NovelDrafts(c, excluded).First());
            onMetrics?.Invoke(new(10, TimeSpan.FromSeconds(1))); onText?.Invoke(json);
            return Task.FromResult(json);
        }
    }
}
