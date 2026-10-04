using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Mixes fresh C# arithmetic with optional stored prose; never runs model inference.</summary>
public sealed class BasicPracticeQuestionProvider(IQuestionBankStore store, Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;

    public async Task<ArithmeticQuizQuestion> SelectAsync(ArithmeticQuizQuestion generated,
        CurriculumTier tier, AppLanguage language, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // An independent 50/50 choice per question, even when the bank contains matching entries.
        // Leave the original C# question (including its random operands and answer choices) intact.
        if (_random.Next(2) == 0) return generated;

        var saved = await store.TakeAsync(generated.Expression.Operation, tier, language, cancellationToken)
            .ConfigureAwait(false);
        if (saved is null) return generated;
        var facts = saved.Contract.FreshFacts();
        return facts.ToPracticeQuestion(saved.Draft.ToWordProblem(facts), generated.Mode);
    }
}
