using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class PracticeProviderTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory, ValidatedBankQuestion sample,
        Func<BasicQuestionContract, string> prose)
    {
        var store = new QuestionBankStore(Path.Combine(directory, "practice-mix.db3"));
        var tracked = new TrackingStore(store);
        var csharp = new BasicPracticeQuestionProvider(tracked, new FixedSourceRandom(0));
        var bank = new BasicPracticeQuestionProvider(tracked, new FixedSourceRandom(1));
        var generator = new ArithmeticQuizGenerator(new BasicArithmeticEngine(), new Random(12345));
        var validator = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var contract = BasicQuestionContract.Create(operation, tier, language, new Random(700 + (int)tier));
            string json = prose(contract);
            var saved = new ValidatedBankQuestion(contract, BasicQuestionValidator.Validate(json, contract).Draft!,
                json, "practice-test", DateTime.UtcNow);
            Check(await store.InsertAsync(saved), "Practice seed failed.");
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            {
                var fresh = generator.Generate(mode, operation, new(tier, false));
                int reads = tracked.Reads;
                var selected = await csharp.SelectAsync(fresh, tier, language);
                Check(ReferenceEquals(selected, fresh) && tracked.Reads == reads,
                    "C# source was replaced by SQLite or read the bank unnecessarily.");
                selected = await bank.SelectAsync(fresh, tier, language);
                Check(tracked.Reads == reads + 1 && selected.Expression == contract.Expression
                    && selected.WordProblem == saved.WordProblem && selected.Mode == mode
                    && selected.CorrectAnswer == contract.Answer && validator.Validate(selected).IsValid,
                    "Stored prose/facts, selection filters, answer mode or C# grading changed.");
            }
        }

        // Use the real random source choice with a reproducible seed, while the bank is populated.
        var mixed = new BasicPracticeQuestionProvider(tracked, new Random(24680));
        int freshCount = 0, savedCount = 0;
        var operands = new HashSet<IntegerArithmeticExpression>();
        for (int i = 0; i < 200; i++)
        {
            var fresh = generator.Generate(ArithmeticQuizMode.Essay, ArithmeticOperation.Add,
                new(CurriculumTier.OneStar, false));
            var selected = await mixed.SelectAsync(fresh, CurriculumTier.OneStar, AppLanguage.Vietnamese);
            if (ReferenceEquals(selected, fresh)) { freshCount++; operands.Add(selected.Expression); }
            else { savedCount++; Check(selected.WordProblem is not null, "Stored branch lost its prose."); }
        }
        Check(freshCount is >= 70 and <= 130 && freshCount + savedCount == 200 && operands.Count > 1,
            "Populated bank displaced fresh random C# questions or source mixing is biased.");

        var sparseStore = new QuestionBankStore(Path.Combine(directory, "practice-sparse.db3"));
        await sparseStore.InsertAsync(sample);
        var sparse = new BasicPracticeQuestionProvider(sparseStore, new FixedSourceRandom(1));
        foreach (var (operation, tier, language) in new[]
        {
            (ArithmeticOperation.Subtract, sample.Contract.Tier, sample.Contract.Language),
            (sample.Contract.Operation, CurriculumTier.TwoStars, sample.Contract.Language),
            (sample.Contract.Operation, sample.Contract.Tier, AppLanguage.English)
        })
        {
            var fresh = generator.Generate(ArithmeticQuizMode.Essay, operation, new(tier, false));
            Check(ReferenceEquals(await sparse.SelectAsync(fresh, tier, language), fresh),
                "No matching bank record did not preserve the fresh C# question.");
        }
        var fallback = generator.Generate(ArithmeticQuizMode.Essay, sample.Contract.Operation,
            new(sample.Contract.Tier, false));
        var empty = new BasicPracticeQuestionProvider(new QuestionBankStore(Path.Combine(directory, "practice-empty.db3")),
            new FixedSourceRandom(1));
        Check(ReferenceEquals(await empty.SelectAsync(fallback, sample.Contract.Tier, sample.Contract.Language), fallback),
            "Empty bank prevented C# practice.");
        string blockedPath = Path.Combine(directory, "practice-blocked");
        await File.WriteAllTextAsync(blockedPath, "not a directory");
        var unavailable = new BasicPracticeQuestionProvider(new QuestionBankStore(Path.Combine(blockedPath, "bank.db3")),
            new FixedSourceRandom(1));
        Check(ReferenceEquals(await unavailable.SelectAsync(fallback, sample.Contract.Tier, sample.Contract.Language), fallback),
            "Unreadable bank prevented C# practice.");
        Console.WriteLine("PASS random C#/SQLite practice mixing, fresh operands, matching filters, all answer modes and bank fallback");
    }

    private sealed class FixedSourceRandom(int source) : Random
    { public override int Next(int maxValue) => source; }

    private sealed class TrackingStore(IQuestionBankStore inner) : IQuestionBankStore
    {
        public int Reads { get; private set; }
        public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
            => inner.InsertAsync(question, cancellationToken);
        public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, CancellationToken cancellationToken = default)
        { Reads++; return inner.TakeAsync(operation, tier, language, cancellationToken); }
    }
}
