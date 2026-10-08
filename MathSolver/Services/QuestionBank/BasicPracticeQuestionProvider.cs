using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

public enum PracticeQuestionFormat { Mixed, Numeric, WordProblem }

/// <summary>Mixes random arithmetic, reviewed C# prose and optional stored templates; never runs model inference.</summary>
public sealed class BasicPracticeQuestionProvider(IQuestionBankStore store, Random? random = null,
    Random? csharpFormatRandom = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly Random _csharpFormatRandom = csharpFormatRandom ?? Random.Shared;
    private readonly OneStepQuestionCycle _builtIn = new();
    private readonly AppliedQuestionCycle _applied = new();
    private readonly FindXQuestionCycle _findX = new();
    private readonly FractionQuestionCycle _fractions = new();

    public async Task<ArithmeticQuizQuestion> SelectReasoningAsync(ArithmeticQuizQuestion generated,
        CurriculumTier tier, AppLanguage language, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (generated.ElementaryProblem is { Kind: QuizProblemKind.Decimal } decimalProblem
            && (!decimalProblem.IsDecimalArithmetic || decimalProblem.StoryContextId is null)) return generated;
        if (generated.ElementaryProblem is { Kind: QuizProblemKind.Measurement } measurementProblem
            && !ElementaryQuizGenerator.MeasurementStoryTypes.Contains(measurementProblem.Type)) return generated;
        if (generated.ElementaryProblem is { Kind: QuizProblemKind.Time } timeProblem
            && !ElementaryQuizGenerator.TimeStoryTypes.Contains(timeProblem.Type)) return generated;
        var (family, variant) = generated.ElementaryProblem is { Kind: QuizProblemKind.TwoNumbers or QuizProblemKind.MultiStep or QuizProblemKind.Decimal or QuizProblemKind.Measurement or QuizProblemKind.Remainder or QuizProblemKind.Time } elementary
            ? (elementary.Kind switch { QuizProblemKind.MultiStep => BankQuestionFamily.MultiStep,
                QuizProblemKind.Measurement => BankQuestionFamily.Measurement,
                QuizProblemKind.Remainder => BankQuestionFamily.Remainder,
                QuizProblemKind.Time => BankQuestionFamily.Time,
                QuizProblemKind.Decimal => BankQuestionFamily.Decimal, _ => BankQuestionFamily.TwoNumbers }, (int)elementary.Type)
            : generated.AverageProblem is { } average ? (BankQuestionFamily.Average, (int)average.Type)
            : generated.PercentageProblem is { } percentage ? (BankQuestionFamily.Percentage, (int)percentage.Type)
            : generated.MotionProblem is { } motion ? (BankQuestionFamily.Motion, (int)motion.Type)
            : generated.ProportionProblem is { } proportion ? (BankQuestionFamily.Proportion, (int)proportion.Type)
            : throw new ArgumentException("ExpectedReasoningQuestion");
        if (_random.Next(2) == 0) return generated;
        var saved = await store.TakeReasoningAsync(family, variant, tier, language, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (saved is null || saved.Contract is not { Version: ReasoningStoryCatalogue.Version } c
            || c.Family != family || c.BankVariant != variant || c.Tier != tier || c.Language != language
            || !BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(saved.Draft), c).IsValid) return generated;
        try
        {
            var fresh = c.FreshFacts(_random);
            return ReasoningStoryCatalogue.ToPractice(fresh, saved.Draft, generated.Mode);
        }
        catch (InvalidOperationException)
        {
            // A retired curriculum schema cannot be silently adapted to a different role.
            return generated;
        }
    }

    public async Task<ArithmeticQuizQuestion> SelectFractionAsync(ArithmeticQuizQuestion generated,
        CurriculumTier tier, AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operation = (ArithmeticOperation)(generated.FractionProblem?.Operation ?? throw new ArgumentException("ExpectedFractionQuestion"));
        ArithmeticQuizQuestion BuiltIn()
        {
            var c = _fractions.Next(profile, operation, tier, language);
            return c.ToPracticeQuestion(FractionQuestionCatalogue.Draft(c).ToWordProblem(c), generated.Mode, _random);
        }
        if (_random.Next(2) == 0) return BuiltIn();
        var saved = await store.TakeFractionAsync(operation, tier, language, profile, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (saved is null) return BuiltIn();
        var c = saved.Contract;
        var validation = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(saved.Draft), c);
        if (!validation.IsValid || c.Family != BankQuestionFamily.Fraction || c.Operation != operation
            || c.Tier != tier || c.Language != language || c.KnowledgeGroup != profile.Group) return BuiltIn();
        var fresh = c.FreshFacts(_random);
        return fresh.ToPracticeQuestion(saved.Draft.ToWordProblem(fresh), generated.Mode, _random);
    }

    public async Task<ArithmeticQuizQuestion> SelectFindXAsync(ArithmeticQuizQuestion generated,
        CurriculumTier tier, AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default,
        PracticeQuestionFormat format = PracticeQuestionFormat.Mixed)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        if (format == PracticeQuestionFormat.Numeric) return generated;
        var operation = generated.FindXProblem?.Operation ?? throw new ArgumentException("ExpectedFindXQuestion");
        ArithmeticQuizQuestion BuiltIn()
        {
            var c = _findX.Next(profile, operation, tier, language);
            var q = c.ToPracticeQuestion(FindXQuestionCatalogue.Draft(c).ToWordProblem(c), generated.Mode);
            return format == PracticeQuestionFormat.Mixed && _csharpFormatRandom.Next(2) == 0
                ? q with { WordProblem = null } : q;
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
        QuestionLearningProfile? profile = null, PracticeQuestionFormat format = PracticeQuestionFormat.Mixed)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        // Numeric practice preserves the generator's facts and choices and never reads SQLite.
        if (format == PracticeQuestionFormat.Numeric) return generated;
        // An independent 50/50 choice per question, even when the bank contains matching entries.
        // Within C#, keep the original random-number exercise alongside reviewed stories.
        // Return the original question intact so its operands, choices and grading are preserved.
        if (profile is not null && !profile.Allows(generated.Expression.Operation))
            throw new ArgumentException("InvalidLearningProfile", nameof(profile));
        ArithmeticQuizQuestion BuiltIn()
        {
            bool numeric = format == PracticeQuestionFormat.Mixed && _csharpFormatRandom.Next(2) == 0;
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
        var c = saved.Contract;
        var validation = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(saved.Draft), c);
        if (!validation.IsValid || c.Family != BankQuestionFamily.Arithmetic
            || c.Operation != generated.Expression.Operation || c.Tier != tier || c.Language != language
            || profile is not null && !profile.Includes(c.KnowledgeGroup)) return BuiltIn();
        var facts = saved.Contract.FreshFacts();
        return facts.ToPracticeQuestion(saved.Draft.ToWordProblem(facts), generated.Mode);
    }
}
