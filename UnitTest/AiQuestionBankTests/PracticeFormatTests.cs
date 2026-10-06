using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class PracticeFormatTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory)
    {
        var store = new SelectionStore();
        // The old random format would choose numeric every time: explicit word mode must override it.
        var builtIn = new BasicPracticeQuestionProvider(store, new SourceRandom(0), new SourceRandom(0));
        var bank = new BasicPracticeQuestionProvider(store, new SourceRandom(1), new SourceRandom(0));
        var arithmeticGenerator = new ArithmeticQuizGenerator(new BasicArithmeticEngine(), new Random(351));
        var findXGenerator = new FindXQuizGenerator(new FindXEngine(), new Random(905));
        var validator = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        int cases = 0;
        foreach (var group in QuestionLearningProfile.Groups())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var profile = new QuestionLearningProfile(group);
            var arithmetic = arithmeticGenerator.Generate(mode, operation, new(tier, false));
            var findX = findXGenerator.Generate(mode, operation, new(tier, false));
            int reads = store.Reads;
            Check(ReferenceEquals(await bank.SelectAsync(arithmetic, tier, language, profile: profile,
                format: PracticeQuestionFormat.Numeric), arithmetic), "Numeric arithmetic rebuilt the question.");
            Check(ReferenceEquals(await bank.SelectFindXAsync(findX, tier, language, profile,
                format: PracticeQuestionFormat.Numeric), findX), "Numeric Find X rebuilt the question.");
            Check(store.Reads == reads && arithmetic.WordProblem is null && findX.WordProblem is null,
                "Numeric practice read SQLite or acquired prose.");

            store.Arithmetic = Record(AppliedQuestionCatalogue.Create(profile, operation, tier, language, new Random(17)));
            store.FindX = Record(FindXQuestionCatalogue.Create(profile, operation, tier, language, new Random(37)));
            foreach (var provider in new[] { builtIn, bank })
            {
                var story = await provider.SelectAsync(arithmetic, tier, language, profile: profile,
                    format: PracticeQuestionFormat.WordProblem);
                Check(story.WordProblem is not null && story.Mode == mode && story.Expression.Operation == operation
                    && story.FindXProblem is null && validator.Validate(story).IsValid,
                    $"Arithmetic word mode failed: {group}/{operation}/{tier}/{language}/{mode}");
                var unknown = await provider.SelectFindXAsync(findX, tier, language, profile,
                    format: PracticeQuestionFormat.WordProblem);
                Check(unknown.WordProblem is not null && unknown.Mode == mode && unknown.FindXProblem?.Operation == operation
                    && validator.Validate(unknown).IsValid,
                    $"Find X word mode failed: {group}/{operation}/{tier}/{language}/{mode}");
                if (ReferenceEquals(provider, bank))
                {
                    // SQLite templates keep their context and unknown role while C# refreshes the facts.
                    Check(story.WordProblem!.Quantity == store.Arithmetic.WordProblem.Quantity
                        && story.WordProblem.ArithmeticReasoning?.Rule == store.Arithmetic.WordProblem.ArithmeticReasoning?.Rule,
                        $"Stored arithmetic dimensions/reasoning changed: {group}/{operation}/{tier}/{language}/{mode}");
                    Check(unknown.FindXProblem!.UnknownIsLeftOperand == FindXQuestionCatalogue.Equation(store.FindX.Contract).UnknownIsLeftOperand,
                        "Stored Find X changed the unknown role.");
                }
            }
            Check(store.Reads == reads + 2, "Built-in word mode touched SQLite or bank selection was skipped.");
            store.Arithmetic = store.FindX = null;
            Check((await bank.SelectAsync(arithmetic, tier, language, profile: profile,
                format: PracticeQuestionFormat.WordProblem)).WordProblem is not null,
                "Empty arithmetic bank fell back to numeric instead of a word problem.");
            Check((await bank.SelectFindXAsync(findX, tier, language, profile,
                format: PracticeQuestionFormat.WordProblem)).WordProblem is not null,
                "Empty Find X bank fell back to numeric instead of a word problem.");
            cases++;
        }

        var target = arithmeticGenerator.Generate(ArithmeticQuizMode.Essay, ArithmeticOperation.Add, new(CurriculumTier.OneStar, false));
        var objects = new QuestionLearningProfile(QuestionKnowledgeGroup.Objects);
        var valid = Record(AppliedQuestionCatalogue.Create(objects, ArithmeticOperation.Add,
            CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(33)));
        foreach (var invalid in new[]
        {
            valid with { Draft = valid.Draft with { GivenA = "Changed roles and numbers: 999" } },
            Record(AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Money), ArithmeticOperation.Add,
                CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(33))),
            Record(AppliedQuestionCatalogue.Create(objects, ArithmeticOperation.Subtract,
                CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(33))),
            Record(FindXQuestionCatalogue.Create(objects, ArithmeticOperation.Add,
                CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(33)))
        })
        {
            store.Arithmetic = invalid;
            var selected = await bank.SelectAsync(target, CurriculumTier.OneStar, AppLanguage.Vietnamese,
                profile: objects, format: PracticeQuestionFormat.WordProblem);
            Check(selected.WordProblem is not null && selected.FindXProblem is null
                && selected.Expression.Operation == ArithmeticOperation.Add && validator.Validate(selected).IsValid,
                "An invalid or mismatched template escaped the word-mode fallback.");
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool cancelled = false;
        try { await bank.SelectAsync(target, CurriculumTier.OneStar, AppLanguage.Vietnamese,
            cancellation.Token, format: PracticeQuestionFormat.Numeric); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "Numeric mode ignored cancellation.");

        // Also exercise the real database filter; the two families must not cross-select.
        var sqlite = new QuestionBankStore(Path.Combine(directory, "explicit-practice-formats.db3"));
        var savedX = Record(FindXQuestionCatalogue.Create(objects, ArithmeticOperation.Add,
            CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(84)));
        Check(await sqlite.InsertAsync(valid) && await sqlite.InsertAsync(savedX), "Practice format SQLite seed failed.");
        var sqliteProvider = new BasicPracticeQuestionProvider(sqlite, new SourceRandom(1), new SourceRandom(0));
        var savedStory = await sqliteProvider.SelectAsync(target, CurriculumTier.OneStar, AppLanguage.Vietnamese,
            profile: objects, format: PracticeQuestionFormat.WordProblem);
        var savedUnknown = await sqliteProvider.SelectFindXAsync(findXGenerator.Generate(ArithmeticQuizMode.Essay,
            ArithmeticOperation.Add, new(CurriculumTier.OneStar, false)), CurriculumTier.OneStar, AppLanguage.Vietnamese,
            objects, format: PracticeQuestionFormat.WordProblem);
        Check(savedStory.WordProblem is not null && savedStory.FindXProblem is null && savedUnknown.WordProblem is not null
            && savedUnknown.FindXProblem is not null, "SQLite word modes crossed question families.");
        Console.WriteLine($"Explicit practice formats passed: {cases} group/operation/star/language/answer-mode cases; numeric isolation, word-only C#/SQLite, empty/invalid bank fallback.");
    }

    private static ValidatedBankQuestion Record(BasicQuestionContract contract)
    {
        var draft = BasicQuestionTemplates.Example(contract);
        return new(contract, draft, QuestionBankStore.SerializeDraft(draft), "practice-format-test", DateTime.UtcNow);
    }

    private sealed class SourceRandom(int source) : Random
    { public override int Next(int maxValue) => Math.Min(source, maxValue - 1); }

    private sealed class SelectionStore : IQuestionBankStore
    {
        public int Reads { get; private set; }
        public ValidatedBankQuestion? Arithmetic { get; set; }
        public ValidatedBankQuestion? FindX { get; set; }
        public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(Arithmetic); }
        public Task<ValidatedBankQuestion?> TakeForProfileAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(Arithmetic); }
        public Task<ValidatedBankQuestion?> TakeFindXAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(FindX); }
    }
}
