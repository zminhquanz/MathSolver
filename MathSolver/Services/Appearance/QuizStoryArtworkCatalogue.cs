using System.Collections.Concurrent;
using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>Read presentation metadata from the same authored Lists as C# motion generation.</summary>
internal static partial class QuizStoryArtworkCatalogue
{
    private sealed record Row(string? Name = null, string? Subject = null, string? First = null,
        string? Second = null, string? ArtworkId = null);
    private static readonly ConcurrentDictionary<AppLanguage, IReadOnlyList<StoryArtworkActor>> MotionCache = new();
    private sealed record FractionRow(string Id, string? ArtworkId = null);
    private static readonly Lazy<IReadOnlyDictionary<string, string?>> FractionArtworkIds = new(() =>
        QuizContentCatalog.LoadList<FractionRow>("FractionScenes").ToDictionary(row => row.Id, row => row.ArtworkId));

    internal static string? FractionArtwork(string sceneId)
    {
        if (FractionArtworkIds.Value.TryGetValue(sceneId, out var artwork)) return artwork;
        return QuestionBank.FoodThemeCatalogue.NewRestaurantDishes.FirstOrDefault(d =>
            sceneId.StartsWith($"fraction-theme-food-{d.Unit.Id}-", StringComparison.Ordinal))?.ArtworkId;
    }

    internal static IReadOnlyList<StoryArtworkActor> MotionActors(AppLanguage language) => MotionCache.GetOrAdd(language, LoadMotion);

    private static IReadOnlyList<StoryArtworkActor> LoadMotion(AppLanguage language)
    {
        string culture = QuizContentCatalog.Culture(language);
        var actors = new List<StoryArtworkActor>();
        foreach (string list in new[] { "MotionQuizGenerator.MovingSubjects", "MotionQuizGenerator.MovingSubjects.Extensions",
            "MotionOpen.Contexts", "MotionExtended.Contexts", "MotionArtwork.Watercraft" })
        {
            var validCrafts = list == "MotionArtwork.Watercraft"
                ? QuizContentCatalog.LoadList<string>("MotionQuizGenerator.Watercraft", culture).ToHashSet(StringComparer.Ordinal) : null;
            foreach (var row in QuizContentCatalog.LoadList<Row>(list, culture))
                foreach (string? name in new[] { row.Name, row.Subject, row.First, row.Second })
                    if (!string.IsNullOrWhiteSpace(name) && (validCrafts is null || validCrafts.Contains(name)))
                        actors.Add(new(name, row.ArtworkId));
        }
        return actors.AsReadOnly();
    }
}
