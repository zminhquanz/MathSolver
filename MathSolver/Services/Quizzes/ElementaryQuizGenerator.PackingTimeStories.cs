using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    internal static readonly ElementaryQuizType[] RemainderStoryTypes =
        [ElementaryQuizType.MinimumGroups, ElementaryQuizType.Leftovers, ElementaryQuizType.QuotientRemainder];
    internal static readonly ElementaryQuizType[] TimeStoryTypes =
        [ElementaryQuizType.TimeAddition, ElementaryQuizType.ElapsedTime];

    public ArithmeticQuizQuestion GenerateRemainderStory(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, string contextId = "")
    {
        if (!Enum.IsDefined(tier) || !RemainderStoryTypes.Contains(type))
            throw new ArgumentException("InvalidRemainderStoryProfile");
        return CompleteQuestion(mode, CreateRemainderStory(type, language, tier, contextId), [], null);
    }

    public ArithmeticQuizQuestion GenerateTimeStory(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, string contextId = "")
    {
        if (!Enum.IsDefined(tier) || !TimeStoryTypes.Contains(type))
            throw new ArgumentException("InvalidTimeStoryProfile");
        return CompleteQuestion(mode, AddTimeStory(CreateTimeDifficulty(type, language, tier), contextId), [], null);
    }
}
