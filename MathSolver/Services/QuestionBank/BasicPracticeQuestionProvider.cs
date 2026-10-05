using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Mixes random arithmetic, reviewed C# prose and optional stored templates; never runs model inference.</summary>
public sealed class BasicPracticeQuestionProvider(IQuestionBankStore store, Random? random = null,
    Random? csharpFormatRandom = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly Random _csharpFormatRandom = csharpFormatRandom ?? Random.Shared;
    private readonly OneStepQuestionCycle _builtIn = new();
    private readonly AppliedQuestionCycle _applied = new();
    private readonly FindXQuestionCycle _findX = new();

    public async Task<ArithmeticQuizQuestion> SelectFindXAsync(ArithmeticQuizQuestion generated,
        CurriculumTier tier, AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operation = generated.FindXProblem?.Operation ?? throw new ArgumentException("ExpectedFindXQuestion");
        ArithmeticQuizQuestion BuiltIn()
        {
            var c = _findX.Next(profile, operation, tier, language);
            var q = c.ToPracticeQuestion(FindXQuestionCatalogue.Draft(c).ToWordProblem(c), generated.Mode);
            return _csharpFormatRandom.Next(2) == 0 ? q with { WordProblem = null } : q;
        }
        if (_random.Next(2) == 0) return BuiltIn();
        var saved = await store.TakeFindXAsync(operation, tier, language, profile, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (saved is null) return BuiltIn();
        // Never trust an arbitrary provider's family/profile, even if it returns a record.
        var c = saved.Contract;
        var validated = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(saved.Draft), c);
        if (!validated.IsValid || c.Family != BankQuestionFamily.FindX || c.Operation != operation
            || c.Tier != tier || c.Language != language || c.KnowledgeGroup != profile.Group) return BuiltIn();
        var fresh = c.FreshFacts();
        return fresh.ToPracticeQuestion(saved.Draft.ToWordProblem(fresh), generated.Mode);
    }

    public async Task<ArithmeticQuizQuestion> SelectAsync(ArithmeticQuizQuestion generated,
        CurriculumTier tier, AppLanguage language, CancellationToken cancellationToken = default,
        QuestionLearningProfile? profile = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // An independent 50/50 choice per question, even when the bank contains matching entries.
        // Within C#, keep the original random-number exercise alongside reviewed stories.
        // Return the original question intact so its operands, choices and grading are preserved.
        if (profile is not null && !profile.Allows(generated.Expression.Operation))
            throw new ArgumentException("InvalidLearningProfile", nameof(profile));
        ArithmeticQuizQuestion BuiltIn()
        {
            bool numeric = _csharpFormatRandom.Next(2) == 0;
            if (profile is null) return numeric ? generated
                : _builtIn.Next(generated.Expression.Operation, tier, language, generated.Mode);
            var c = _applied.Next(profile, generated.Expression.Operation, tier, language);
            var question = c.ToPracticeQuestion(AppliedQuestionCatalogue.Draft(c).ToWordProblem(c), generated.Mode);
            return numeric ? question.WordProblem?.ArithmeticReasoning is not null ? generated
                : question with { WordProblem = null } : question;
        }
        if (_random.Next(2) == 0) return BuiltIn();

        var saved = await (profile is null
            ? store.TakeAsync(generated.Expression.Operation, tier, language, cancellationToken)
            : store.TakeForProfileAsync(generated.Expression.Operation, tier, language, profile, cancellationToken)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (saved is null) return BuiltIn();
        var facts = saved.Contract.FreshFacts();
        return facts.ToPracticeQuestion(saved.Draft.ToWordProblem(facts), generated.Mode);
    }
}
