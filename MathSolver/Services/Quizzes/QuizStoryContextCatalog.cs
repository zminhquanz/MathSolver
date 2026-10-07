using MathSolver.Models;

namespace MathSolver.Services;

public sealed record QuizStoryContext(string Id, QuestionKnowledgeGroup Group, string UnitId, int MaximumPerPeriod)
{
    public string ViSetting => QuizContentCatalog.Text("vi-VN", $"StoryContext.{Id}.Setting");
    public string EnSetting => QuizContentCatalog.Text("en-US", $"StoryContext.{Id}.Setting");
    public string ViAction => QuizContentCatalog.Text("vi-VN", $"StoryContext.{Id}.Action");
    public string EnAction => QuizContentCatalog.Text("en-US", $"StoryContext.{Id}.Action");
    public string ViUnit => QuizContentCatalog.Text("vi-VN", $"StoryContext.{Id}.Unit");
    public string EnUnit => QuizContentCatalog.Text("en-US", $"StoryContext.{Id}.Unit");
    public string ViPeriod => QuizContentCatalog.Text("vi-VN", $"StoryContext.{Id}.Period");
    public string EnPeriod => QuizContentCatalog.Text("en-US", $"StoryContext.{Id}.Period");
    public string Setting(AppLanguage language) => QuizContentCatalog.Text(language, $"StoryContext.{Id}.Setting");
    public string Unit(AppLanguage language) => QuizContentCatalog.Text(language, $"StoryContext.{Id}.Unit");
    public string Period(AppLanguage language) => QuizContentCatalog.Text(language, $"StoryContext.{Id}.Period");
}

public static class QuizStoryContextCatalog
{
    public static IReadOnlyList<QuizStoryContext> All { get; } = QuizContentCatalog.LoadList<QuizStoryContext>("StoryContexts");
    public static QuizStoryContext Find(string id) => All.Single(c => c.Id == id);
    public static IEnumerable<QuizStoryContext> AverageContexts => All.Where(c => c.Id is not ("family-age" or "classroom"));
    public static string PluralPeriod(string period) => period == "match" ? "matches" : period + "s";
}
