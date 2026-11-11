using System.Collections.Concurrent;

namespace MathSolver.Services.QuestionBank;

internal sealed record QuizStoryTemplate(string Id, string Family, BasicQuestionDraft[] Examples, ReviewedQuestionProse Prose);

internal static class QuizStoryTemplates
{
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<(string Family, string Id), QuizStoryTemplate>> Cache = new();

    internal static QuizStoryTemplate For(BasicQuestionContract contract)
    {
        string culture = QuizContentCatalog.Culture(contract.Language);
        var stories = Cache.GetOrAdd(culture, code => {
            var list = QuizContentCatalog.LoadList<QuizStoryTemplate>("Stories", code);
            var result = new Dictionary<(string, string), QuizStoryTemplate>();
            foreach (var story in list)
            {
                if (string.IsNullOrWhiteSpace(story.Id) || story.Family is not ("Applied" or "Fraction")
                    || story.Examples.Length == 0 || story.Prose is null || story.Prose.GivenA.Length == 0
                    || story.Prose.GivenB.Length == 0 || story.Prose.Questions.Length == 0 || story.Prose.Leads.Length == 0
                    || !result.TryAdd((story.Family, story.Id), story))
                    throw new InvalidDataException("Missing or duplicate story template.");
                foreach (var example in story.Examples)
                    if (string.IsNullOrWhiteSpace(example.GivenA) || string.IsNullOrWhiteSpace(example.GivenB)
                        || string.IsNullOrWhiteSpace(example.Question) || string.IsNullOrWhiteSpace(example.SolutionLead)
                        || example.UnitId != story.Prose.UnitId)
                        throw new InvalidDataException($"Invalid example in '{story.Id}'.");
            }
            return result;
        });
        string family = contract.Version == FractionQuestionCatalogue.Version ? "Fraction" : "Applied";
        if (stories.TryGetValue((family, contract.SceneId), out var template)) return template;

        // These nine food IDs share generated scenes. Five are takeaway bakery
        // products and need selling/packing prose; the others use eatery prose.
        var traditionalDish = FoodThemeCatalogue.NewRestaurantDishes.FirstOrDefault(d =>
            contract.SceneId.StartsWith($"{(family == "Fraction" ? "fraction-" : "")}theme-food-{d.Unit.Id}-", StringComparison.Ordinal));
        if (traditionalDish is not null)
        {
            string prefix = family == "Fraction" ? "fraction-theme-food-" : "theme-food-";
            string suffix = contract.SceneId[(prefix.Length + traditionalDish.Unit.Id.Length)..];
            string unitId = family == "Fraction" ? contract.SceneId : traditionalDish.Unit.Id;
            if (FoodThemeCatalogue.IsBakeryFood(traditionalDish.Unit.Id))
                return BakeryDishStoryTemplates.Create(contract.SceneId, family, suffix.TrimStart('-'), contract.Language, unitId);

            if (stories.TryGetValue((family, prefix + "pho" + suffix), out var reference))
            {
                return new(contract.SceneId, family,
                    reference.Examples.Select(e => e with { UnitId = unitId }).ToArray(),
                    reference.Prose with { UnitId = unitId });
            }
        }
        throw new InvalidDataException($"Missing {family} story '{contract.SceneId}' in '{culture}'.");
    }
}
