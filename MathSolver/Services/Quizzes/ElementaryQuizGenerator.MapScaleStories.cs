using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    internal sealed record MapScaleStoryContext(string Id, int MinimumStar, int MaximumStar, int MaximumScale);
    internal static IReadOnlyList<MapScaleStoryContext> MapScaleContexts(AppLanguage language) =>
        QuizContentCatalog.LoadList<MapScaleStoryContext>("MapScaleContexts", QuizContentCatalog.Culture(language));

    public ArithmeticQuizQuestion GenerateMapScaleStory(ArithmeticQuizMode mode, AppLanguage language,
        CurriculumTier tier, string contextId = "") => CompleteQuestion(mode, CreateMapScale(language, tier, contextId), [], null);
}
