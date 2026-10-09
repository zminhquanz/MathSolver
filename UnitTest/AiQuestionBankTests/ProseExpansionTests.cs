using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text;
using System.Text.Json;

internal static class ProseExpansionTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static async Task RunAsync()
    {
        int cases = 0;
        foreach (var family in Enum.GetValues<BankQuestionFamily>().Where(ReasoningStoryCatalogue.Supports))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (int variant in ReasoningStoryCatalogue.Variants(family, tier))
        for (int seed = 0; seed < 2; seed++)
        {
            var c = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(seed));
            Check(c.IsValid, $"Invalid {family}/{variant}/{tier}/{language}/{seed}");
            if (family == BankQuestionFamily.Data)
            {
                var changed = c with { Story = c.Story! with { ChartProfile = c.Story!.ChartProfile! with {
                    Language = language == AppLanguage.Vietnamese ? AppLanguage.English : AppLanguage.Vietnamese } } };
                Check(!changed.IsValid, "Chart profile language mismatch accepted");
            }
            var original = ReasoningStoryCatalogue.Draft(c);
            var baseValidation = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(original), c);
            Check(baseValidation.IsValid, $"Base prose rejected {family}/{variant}/{tier}/{language}/{seed}: {baseValidation.ErrorCode}/{baseValidation.ErrorDetails}\n{QuestionBankStore.SerializeDraft(original)}");
            var drafts = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Take(3).ToArray();
            Check(drafts.Length >= 1, $"Missing factual variation {family}/{variant}/{tier}/{language}/{seed}\n"
                + JsonSerializer.Serialize(ReasoningStoryCatalogue.Lesson(c).FactPhrasings));
            foreach (var draft in drafts)
            {
                var validation = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c);
                Check(validation.IsValid, $"Reviewed variant rejected: {family}/{variant}: {validation.ErrorCode}/{validation.ErrorDetails}");
                VerifyPractice(c, draft);
            }
            BasicQuestionContract fresh;
            try { fresh = c.FreshFacts(new(seed + 213)); }
            catch (Exception error) { throw new Exception($"Fresh facts failed {family}/{variant}/{tier}/{language}/{seed}\n{original.ProblemText}", error); }
            Check(fresh.Story!.Schema == c.Story!.Schema, "Fresh C# facts changed the schema");
            VerifyPractice(fresh, drafts[0]);
            var foreign = drafts[0] with { Question = drafts[0].Question + " łącznie" };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(foreign), c).IsValid, "Foreign language accepted");
            var leaked = drafts[0] with { Question = drafts[0].Question + " 123" };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(leaked), c).IsValid, "Answer leak accepted");
            _ = ReasoningStoryValidator.Grammar(c);
            cases++;
        }
        string directory = Path.Combine(Path.GetTempPath(), "MathSolver-prose-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new QuestionBankStore(Path.Combine(directory, "test.db3"));
            var generated = new List<ValidatedBankQuestion>();
            foreach (var family in new[] { BankQuestionFamily.Geometry, BankQuestionFamily.Data })
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            {
                var c = ReasoningStoryCatalogue.Create(family, ReasoningStoryCatalogue.Variants(family, tier)[1], tier, language, new(71));
                var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
                var record = new ValidatedBankQuestion(c, draft, QuestionBankStore.SerializeDraft(draft), "reviewed", DateTime.UtcNow);
                Check(await store.InsertAsync(record), "Visual prose insert failed");
                generated.Add(record);
                var read = family == BankQuestionFamily.Data
                    ? await store.TakeChartAsync(c.Story!.ChartProfile!)
                    : await store.TakeReasoningAsync(family, c.BankVariant, tier, language);
                Check(read is not null, "Visual prose read failed");
                var prototype = ReasoningStoryCatalogue.ToPractice(c, ReasoningStoryCatalogue.Draft(c), ArithmeticQuizMode.Essay);
                var selected = await new BasicPracticeQuestionProvider(store, new BankRandom()).SelectReasoningAsync(prototype, tier, language);
                Check(selected.WordProblem?.ProblemText != prototype.WordProblem?.ProblemText
                    || selected.ElementaryProblem?.ProblemText != prototype.ElementaryProblem?.ProblemText, "Visual prose not selected");
                VerifyQuestion(selected);
                if (family == BankQuestionFamily.Data)
                {
                    var otherLanguage = c.Story!.ChartProfile! with {
                        Language = language == AppLanguage.Vietnamese ? AppLanguage.English : AppLanguage.Vietnamese };
                    Check(!IQuestionBankStore.ChartProfilesMatch(c.Story.ChartProfile!, otherLanguage),
                        "Chart profiles matched across languages");
                    var other = await store.TakeChartAsync(otherLanguage);
                    Check(other is null || other.Contract.Language == otherLanguage.Language,
                        "Chart prose selected for the wrong language");
                    var bad = selected.ElementaryProblem! with { Visual = selected.ElementaryProblem!.Visual! with {
                        Values = selected.ElementaryProblem.Visual!.Values.Select((x, i) => i == 0 ? x + 1 : x).ToArray() } };
                    try { DataChartQuestionValidator.Validate(bad); throw new Exception("Changed visual data accepted"); }
                    catch (InvalidDataException) { }
                }
            }
            using var export = new MemoryStream();
            Check((await store.ExportExcelAsync(export)).Exported == generated.Count, "Visual prose export lost rows");
            export.Position = 0;
            var imported = new QuestionBankStore(Path.Combine(directory, "import.db3"));
            var result = await imported.ImportExcelAsync(export);
            Check(result.Inserted == generated.Count, "Visual prose import lost roles or templates");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine($"Prose expansion: {cases} cases across 1–5 stars and Vietnamese/English; historical/new prose, fresh numbers, grading, visual integrity, SQLite/Excel passed.");
    }

    internal static void VerifyPractice(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c, draft, mode);
            Check(q.ExactAnswers.SequenceEqual(c.ExactAnswers), "AI prose changed numeric answers");
            try { VerifyQuestion(q); }
            catch (Exception error) { throw new Exception($"Visual presentation rejected: {c.Family}/{c.BankVariant}/{c.Tier}/{c.Language}\n{q.ElementaryProblem?.ProblemText}", error); }
            if (mode == ArithmeticQuizMode.Essay)
            {
                string text = ReasoningStoryCatalogue.Solution(c, draft);
                var submitted = EssayCombinedInputParser.Parse(text, true, preserveAllCalculations: true);
                var grade = new EssayAnswerValidator(new BasicArithmeticEngine()).Validate(q, submitted.Solution, submitted.Equation, submitted.Answer);
                Check(grade.IsCorrect, $"Projected solution cannot be graded: {c.Family}/{c.BankVariant}/{c.Tier}/{c.Language}\n{text}\n{grade}");
            }
        }
    }

    private static void VerifyQuestion(ArithmeticQuizQuestion q)
    {
        if (q.ElementaryProblem?.DataChart is not null) DataChartQuestionValidator.Validate(q.ElementaryProblem);
    }

    public static async Task RunModelAsync(string model, bool narrativeDepth = false)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", (narrativeDepth ? "narrative-depth-model-" : "prose-expansion-model-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        Console.WriteLine("Actual GGUF inference, isolated SQLite, fresh C# facts. Evidence: " + directory);
        var runtime = new GgufQuestionRuntime();
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        var results = new List<object>();
        var failures = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(50));
        try
        {
            await runtime.LoadAsync(model, timeout.Token);
            Console.WriteLine($"CPU generation threads: {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            foreach (var family in narrativeDepth ? NarrativeDepthTests.Families : Enum.GetValues<BankQuestionFamily>())
            {
                var c = narrativeDepth ? NarrativeDepthTests.NativeContract(family, language) : NativeContract(family, language);
                string name = $"{family}-{(int)c.Tier}-{language}";
                await File.WriteAllTextAsync(Path.Combine(directory, name + "-contract.json"), JsonSerializer.Serialize(c));
                string? correction = null;
                bool passed = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction);
                    var streamed = new StringBuilder();
                    AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateAsync(c, prompt, timeout.Token, text => streamed.Append(text), m => metrics = m);
                    Check(streamed.ToString() == raw && metrics?.GeneratedTokens > 0, "Missing real streamed inference");
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    await File.WriteAllTextAsync(Path.Combine(directory, $"{name}-{attempt}.txt"), prompt + "\n\n" + raw + "\n\n" + validation.ErrorCode + " " + validation.ErrorDetails, timeout.Token);
                    results.Add(new { Case = name, Attempt = attempt, validation.ErrorCode, validation.ErrorDetails, Metrics = metrics });
                    Console.WriteLine($"{name}: attempt {attempt}, {validation.ErrorCode ?? "Valid"}, tokens={metrics!.GeneratedTokens}, tok/s={metrics.TokensPerSecond:F2}");
                    if (validation.IsValid)
                    {
                        var saved = new ValidatedBankQuestion(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                        Check(await store.InsertAsync(saved, timeout.Token), "Native prose SQLite insert failed");
                        var read = ReasoningStoryCatalogue.Supports(family)
                            ? await store.TakeReasoningAsync(family, c.BankVariant, c.Tier, language, timeout.Token)
                            : family == BankQuestionFamily.Fraction ? await store.TakeFractionAsync(c.Operation, c.Tier, language, new(c.KnowledgeGroup), timeout.Token)
                            : family == BankQuestionFamily.FindX ? await store.TakeFindXAsync(c.Operation, c.Tier, language, new(c.KnowledgeGroup), timeout.Token)
                            : await store.TakeForProfileAsync(c.Operation, c.Tier, language, new(c.KnowledgeGroup), timeout.Token);
                        Check(read is not null, "Native prose SQLite read failed");
                        var fresh = read!.Contract.FreshFacts(new(193));
                        Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(read.Draft), fresh).IsValid, "Native prose failed with fresh numbers");
                        if (ReasoningStoryCatalogue.Supports(family)) VerifyPractice(fresh, read.Draft);
                        else foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                            _ = fresh.ToPracticeQuestion(read.Draft.ToWordProblem(fresh), mode, new(3));
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
        Check(!runtime.IsLoaded, "Weights not released");
        Check(failures.Count == 0, "Native failures: " + string.Join(", ", failures));
        Console.WriteLine($"PASS {results.Count} real inference attempts; SQLite/fresh math/grading, streaming and model disposal verified.");
    }

    private static BasicQuestionContract NativeContract(BankQuestionFamily family, AppLanguage language)
    {
        if (family == BankQuestionFamily.Arithmetic) return AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, new(17));
        if (family == BankQuestionFamily.FindX) return FindXQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Money), ArithmeticOperation.Subtract, CurriculumTier.FourStars, language, new(17), FindXUnknownRole.Minuend);
        if (family == BankQuestionFamily.Fraction) return FractionQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add, CurriculumTier.TwoStars, language, new(17));
        var tier = family is BankQuestionFamily.Average or BankQuestionFamily.Percentage or BankQuestionFamily.Motion or BankQuestionFamily.Geometry or BankQuestionFamily.Data
            ? CurriculumTier.FiveStars : CurriculumTier.ThreeStars;
        int variant = ReasoningStoryCatalogue.Variants(family, tier)[0];
        if (family == BankQuestionFamily.Average) variant = (int)AverageQuizType.IndirectData;
        if (family == BankQuestionFamily.Percentage) variant = ReasoningStoryCatalogue.Variants(family, tier)[2];
        if (family == BankQuestionFamily.Geometry) variant = ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Rectangle, GeometryMeasurement.Area);
        if (family == BankQuestionFamily.Data) variant = (int)ElementaryQuizType.ReadPieChart;
        return ReasoningStoryCatalogue.Create(family, variant, tier, language, new(17));
    }

    private sealed class BankRandom : Random { public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue); }
}
