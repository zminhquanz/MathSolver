using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Globalization;

internal static class FractionBankTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory)
    {
        var essay = new EssayAnswerValidator(new BasicArithmeticEngine());

        int count = 0;
        foreach (var group in QuestionLearningProfile.Groups())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var scenes = FractionQuestionCatalogue.Available(new(group), operation, tier).ToArray();
            Check(scenes.Length > 0, $"Missing fraction group/operation/stars: {group}/{operation}/{tier}");
            foreach (var scene in scenes)
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            foreach (int seed in new[] { 2, 27, 305 })
            {
                var c = FractionQuestionCatalogue.Create(new(group), operation, tier, language, new Random(seed), scene.Id);
                string tag = $"{scene.Id}/{tier}/{language}/{seed}";
                Check(c.IsValid && c.Family == BankQuestionFamily.Fraction, "Invalid fraction contract: " + tag);
                var a = FractionQuestionCatalogue.Left(c); var b = FractionQuestionCatalogue.Right(c);
                var expected = operation switch {
                    ArithmeticOperation.Add => new ReducedFraction(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator),
                    ArithmeticOperation.Subtract => new ReducedFraction(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator),
                    ArithmeticOperation.Multiply => new ReducedFraction(a.Numerator * b.Numerator, a.Denominator * b.Denominator),
                    _ => new ReducedFraction(a.Numerator * b.Denominator, a.Denominator * b.Numerator) };
                if (scene.Formula == "({a} + {b}) × 2") expected = new(expected.Numerator * 2, expected.Denominator);
                if (scene.Formula == "({a} + {b}) ÷ 2") expected = new(expected.Numerator, expected.Denominator * 2);
                Check(FractionQuestionCatalogue.Answer(c) == expected && c.AnswerText == expected.ToString(), "Wrong exact answer: " + tag);
                if (scene.CountResult) Check(expected.Denominator.IsOne, "Fractional package count.");
                if (scene.WholeShare) Check(expected.Numerator <= expected.Denominator, "Budget/task exceeds one whole.");
                Check(!(c with { LeftDenominator = 0 }).IsValid && !(c with { Unit = "wrong" }).IsValid, "Forged facts/units passed.");
                var fresh = c.FreshFacts(new Random(seed + 900));
                Check(fresh.IsValid && fresh.SceneId == c.SceneId, "Fresh facts changed scene.");
                var draft = FractionQuestionCatalogue.Draft(c);
                string raw = QuestionBankStore.SerializeDraft(draft);
                var validation = BasicQuestionValidator.Validate(raw, c);
                Check(validation.IsValid && validation.Contract == c, "Reviewed fraction prose rejected: " + tag + ": " + validation.ErrorCode);
                foreach (var alternate in ReviewedQuestionProse.For(c)!.Stories())
                    Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(alternate), c).IsValid, "Reviewed alternative rejected: " + tag);
                foreach (var broken in new[] { draft with { GivenA = draft.GivenA + " 42" },
                    draft with { GivenB = draft.GivenB.Replace("{b}", "{a}") }, draft with { UnitId = "wrong" },
                    draft with { Question = draft.Question + " {a}" }, draft with { GivenA = draft.GivenB, GivenB = draft.GivenA } })
                    Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(broken), c).IsValid, "Changed AI facts/target passed: " + tag);
                var word = draft.ToWordProblem(c);
                Check(!word.ProblemText.Contains('{') && char.IsUpper(word.ProblemText[0]) && word.AnswerUnit == c.Unit,
                    "Unrendered prose/case/dimension: " + tag);
                Check(StreamingQuestionPreview.Render(raw, c) == word.ProblemText, "Streaming preview differs: " + tag);
                Check(QuestionProseIdentity.Hash(c, draft) == QuestionProseIdentity.Hash(fresh, draft), "Random fractions changed prose identity.");
                string grammar = GgufQuestionRuntime.BuildGrammar(c);
                Check(grammar.StartsWith("root ::=") && grammar.All(ch => ch <= 127), "Invalid native grammar.");
                Check(ReviewedQuestionProse.For(c)!.Stories().Select(d => QuestionProseIdentity.Hash(c, d)).Distinct().Count() >= 2,
                    "Scene has no alternate wording: " + tag);
                foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var question = c.ToPracticeQuestion(word, mode, new Random(seed));
                    Check(question.ExactAnswer == expected && (mode != ArithmeticQuizMode.TrueFalse || question.PresentedEquationIsCorrect == ((question.FractionProblem?.PresentedAnswer ?? question.ExpressionProblem!.PresentedAnswer) == expected)), "Invalid practice mode: " + tag);
                    if (mode == ArithmeticQuizMode.MultipleChoice)
                    {
                        var choices = question.FractionProblem?.Choices ?? question.ExpressionProblem!.Choices;
                        Check(choices.Distinct().Count() == 4 && choices.Contains(expected), "Invalid rational choices.");
                        if (scene.WholeShare) Check(choices.All(v => v.Numerator > 0 && v.Numerator <= v.Denominator), "An impossible whole-share choice reveals the answer.");
                        if (scene.CountResult) Check(choices.All(v => v.Denominator.IsOne), "Package-count choices must be integers.");
                    }
                    if (mode != ArithmeticQuizMode.Essay) continue;
                    string calc = $"{FractionQuestionCatalogue.Expression(c)} = {expected} {c.Unit}";
                    var answer = essay.Validate(question, word.SolutionLead, calc, $"{expected} {c.Unit}");
                    Check(answer.IsCorrect, "Correct fraction story solution rejected: " + tag + ": " + answer);
                    foreach (string equivalent in question.ExpressionProblem?.EquivalentExpressions
                        ?? new[] { $"({a}) {BasicArithmeticEngine.GetSymbol(operation)} ({b})" })
                        Check(essay.Validate(question, word.SolutionLead, $"{equivalent} = {expected} {c.Unit}", $"{expected} {c.Unit}").IsCorrect,
                            "Equivalent fraction story work rejected: " + tag);
                    Check(!essay.Validate(question, word.SolutionLead, calc, $"{expected} nonsenseunit").IsCorrect, "Wrong dimension accepted.");
                    Check(!essay.Validate(question, word.SolutionLead, $"{expected} × 1 = {expected} {c.Unit}", $"{expected} {c.Unit}").IsCorrect,
                        "Unrelated operands producing the same answer passed.");
                    string solution = ElementaryWordProblemSolutionFormatter.Format(question, language, CultureInfo.InvariantCulture);
                    Check(solution.Contains($"{expected} {c.Unit}"), "Solution lost exact answer/unit.");
                }
                count++;
            }
        }
        var store = new QuestionBankStore(Path.Combine(directory, "fraction.db3"));
        foreach (var group in QuestionLearningProfile.Groups())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        {
            var c = FractionQuestionCatalogue.Create(new(group), operation, CurriculumTier.FiveStars, AppLanguage.Vietnamese, new Random(47));
            var d = FractionQuestionCatalogue.Draft(c);
            var record = new ValidatedBankQuestion(c, d, QuestionBankStore.SerializeDraft(d), "fraction-test", DateTime.UtcNow);
            Check(await store.InsertAsync(record), "Fraction insert failed.");
            Check(!await store.InsertAsync(record with { Contract = c.FreshFacts(new Random(503)) }), "Duplicate wording with new numbers inserted.");
            Check((await store.TakeFractionAsync(operation, c.Tier, c.Language, new(group)))?.Contract == c, "SQLite lost exact fractions/profile.");
            Check(await store.TakeForProfileAsync(operation, c.Tier, c.Language, new(group)) is null, "Integer practice selected fraction facts.");
            var hashes = await store.GetProseHashesAsync();
            var novel = ReviewedQuestionProse.For(c)!.NovelExample(c, hashes)
                ?? throw new InvalidOperationException("No alternate wording for " + c.SceneId);
            Check(!hashes.Contains(QuestionProseIdentity.Hash(c, novel)), "Retry selected excluded story.");
            Check(GgufQuestionRuntime.BuildNovelGrammar(c, hashes).StartsWith("root ::="), "Novel fraction grammar failed.");
        }
        using var workbook = new MemoryStream();
        await store.ExportExcelAsync(workbook); workbook.Position = 0;
        var rows = QuestionBankWorkbook.Read(workbook);
        Check(rows.Count == 36 && rows.All(r => r.Question?.Contract.IsValid == true), "Fraction workbook roundtrip lost denominators.");
        var imported = new QuestionBankStore(Path.Combine(directory, "fraction-import.db3"));
        workbook.Position = 0;
        var result = await imported.ImportExcelAsync(workbook);
        Check(result.Inserted == 36 && result.Rejected == 0, "Fraction workbook import failed.");
        var background = new AiQuestionGenerationService(new Runtime(), imported);
        background.Start(new(ArithmeticOperation.Divide, CurriculumTier.ThreeStars, AppLanguage.English, 3, true,
            new(QuestionKnowledgeGroup.Packaging), BankQuestionFamily.Fraction));
        await background.Completion;
        Check(background.Snapshot.State == AiJobState.Completed && background.Snapshot.Items.All(i => i.State == AiItemState.Saved
            && i.Contract.Family == BankQuestionFamily.Fraction), "Background fraction generation did not save.");
        var generator = new FractionQuizGenerator(new FractionCalculationEngine(), new Random(13));
        var provider = new BasicPracticeQuestionProvider(imported, new Random(17));
        for (int i = 0; i < 40; i++)
        {
            var original = generator.Generate(ArithmeticQuizMode.Essay, FractionOperation.Add, new(CurriculumTier.FiveStars, false));
            Check(original.WordProblem is null, "Numeric practice requires a group.");
            var selected = await provider.SelectFractionAsync(original, CurriculumTier.FiveStars, AppLanguage.Vietnamese, new(QuestionKnowledgeGroup.Measurement));
            Check(selected.WordProblem is not null && selected.UsesFractionFormatting && selected.ExactAnswer.Denominator > 0, "Fraction practice fallback failed.");
        }
        await store.QueryAsync("UPDATE BasicQuestionBank SET ProblemVariant=99 WHERE ProblemType=2");
        Check(await store.TakeFractionAsync(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese,
            new(QuestionKnowledgeGroup.Objects)) is null, "Corrupted metadata passed.");
        Console.WriteLine($"Fraction bank passed: {count} scene/star/language/seed cases, 9 groups, 4 operations, 3 answer modes; SQLite/Excel/deduplication/background/practice.");
    }

    public static async Task RunModelAsync(string modelPath)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", "fraction-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        var runtime = new GgufQuestionRuntime();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        try
        {
            Console.WriteLine("Loading fraction model; evidence: " + directory);
            await runtime.LoadAsync(modelPath, timeout.Token);
            int index = 0;
            foreach (var group in QuestionLearningProfile.Groups())
            {
                var operation = (ArithmeticOperation)(index++ % 4);
                var language = index % 3 == 0 ? AppLanguage.English : AppLanguage.Vietnamese;
                var c = FractionQuestionCatalogue.Create(new(group), operation, CurriculumTier.FiveStars, language, new Random(index));
                var original = FractionQuestionCatalogue.Draft(c);
                await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "seed", DateTime.UtcNow));
                var excluded = await store.GetProseHashesAsync(timeout.Token);
                string prompt = BasicQuestionPrompt.Build(c, excludedProse: excluded);
                string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token);
                await File.WriteAllTextAsync(Path.Combine(directory, group + ".json"), raw, timeout.Token);
                var validation = BasicQuestionValidator.Validate(raw, c);
                Check(validation.IsValid, "Real fraction model validation failed: " + group + ": " + validation.ErrorCode);
                var question = new ValidatedBankQuestion(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                Check(!excluded.Contains(QuestionProseIdentity.Hash(c, question.Draft)), "Native grammar repeated seeded prose.");
                Check(await store.InsertAsync(question, timeout.Token), "Real fraction prose failed to insert.");
                Check(c.FreshFacts(new Random(405)).IsValid, "Real model changed dimensions.");
                Console.WriteLine($"PASS model {group}/{operation}/{language}: {question.WordProblem.ProblemText}");
            }
        }
        finally { await runtime.EjectAsync(); }
    }

    private sealed class Runtime : IQuestionTextRuntime
    {
        private readonly HashSet<string> _used = [];
        public bool IsLoaded => true;
        public string ModelName => "fraction-test";
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Check(c.Family == BankQuestionFamily.Fraction, "Worker created integer facts.");
            var d = ReviewedQuestionProse.For(c)!.NovelExample(c, _used)!;
            _used.Add(QuestionProseIdentity.Hash(c, d));
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
}
