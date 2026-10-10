using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    internal sealed record TimeActivityContext(string Id, int MinimumStar, int StartHourMinimum,
        int StartHourMaximum, int MaximumHours);
    internal static IReadOnlyList<TimeActivityContext> TimeActivities(AppLanguage language) =>
        QuizContentCatalog.LoadList<TimeActivityContext>("TimeActivityContexts", QuizContentCatalog.Culture(language));
    internal static readonly string[] TimeActivityClauses = ["017", "019", "020", "022", "025", "029", "031", "033", "036"];

    private static string TimeActivityText(AppLanguage language, TimeActivityContext? activity, string suffix,
        params (string Key, string Value)[] values) => QuizContentCatalog.Text(language,
            "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty."
            + (activity is not null && TimeActivityClauses.Contains(suffix) ? "Activity." + activity.Id + "." : "") + suffix, values);

    private ElementaryQuizContract CreateTimeStory(ElementaryQuizType type, AppLanguage language, CurriculumTier tier,
        string contextId = "")
    {
        if (!_expandActivityStories) return AddTimeStory(CreateTimeDifficulty(type, language, tier), contextId);
        var contexts = TimeActivities(language).Where(c => c.MinimumStar <= (int)tier).ToArray();
        var activity = contextId.Length == 0
            ? contexts[NextContextVariant(type, language, "time-activity", contexts.Length)]
            : contexts.SingleOrDefault(c => c.Id == contextId) ?? throw new ArgumentException("InvalidTimeStoryProfile");
        return CreateTimeDifficulty(type, language, tier, activity) with { StoryContextId = activity.Id };
    }
}
