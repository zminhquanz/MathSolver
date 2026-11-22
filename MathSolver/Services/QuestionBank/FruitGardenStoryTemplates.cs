using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Orchard planting and harvest stories share fruit identities with shop stories.</summary>
internal sealed record FruitGardenPlantUnit(string UnitId, string Vietnamese, string English, string EnglishSingular);

internal static class FruitGardenStoryTemplates
{
    private static readonly IReadOnlyList<FruitGardenPlantUnit> PlantUnits =
        QuizContentCatalog.LoadList<FruitGardenPlantUnit>("FruitGardenPlantUnits");
    private static readonly IReadOnlyDictionary<string, FruitGardenPlantUnit> PlantUnitsById =
        PlantUnits.ToDictionary(unit => unit.UnitId, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, string> PlantSingulars =
        PlantUnits.ToDictionary(unit => unit.English, unit => unit.EnglishSingular, StringComparer.Ordinal);

    internal static bool IsGardenScene(string sceneId) =>
        sceneId.StartsWith("theme-fruit-", StringComparison.Ordinal)
        && (sceneId.EndsWith("-plant-total", StringComparison.Ordinal)
            || sceneId.EndsWith("-harvest-total", StringComparison.Ordinal));

    private static bool IsPlanting(string sceneId) => sceneId.EndsWith("-plant-total", StringComparison.Ordinal);

    internal static string? Unit(AppliedQuestionScene scene, AppLanguage language)
    {
        if (!IsGardenScene(scene.Id)) return null;
        var fruit = FoodThemeCatalogue.Subjects.FirstOrDefault(item =>
            item.Group == QuestionKnowledgeGroup.Fruit && item.Unit.Id == scene.UnitId);
        if (fruit is null) return null;
        if (!IsPlanting(scene.Id)) return fruit.Unit.Item(language);

        if (!PlantUnitsById.TryGetValue(fruit.Unit.Id, out var plant))
            throw new InvalidDataException($"Missing fruit planting metadata for '{fruit.Unit.Id}'.");
        return language == AppLanguage.Vietnamese ? plant.Vietnamese : plant.English;
    }

    internal static string? Singular(string unit) => PlantSingulars.GetValueOrDefault(unit);

    // Classify each owner's location by its own count. Keep the unassigned boundary 100 with gardens.
    internal static string Venue(AppLanguage language, int individualCount)
        => individualCount <= 100
            ? language == AppLanguage.Vietnamese ? "khu vườn" : "garden"
            : language == AppLanguage.Vietnamese ? "nông trại" : "farm";

    internal static string Both(AppLanguage language, int left, int right)
    {
        bool firstGarden = left <= 100;
        bool secondGarden = right <= 100;
        if (firstGarden != secondGarden)
            return language == AppLanguage.Vietnamese
                ? $"cả {Venue(language, left)} và {Venue(language, right)}"
                : $"the {Venue(language, left)} and the {Venue(language, right)}";
        return language == AppLanguage.Vietnamese
            ? firstGarden ? "cả hai khu vườn" : "cả hai nông trại"
            : firstGarden ? "both gardens" : "both farms";
    }

    private static string[] PairPhrases(AppLanguage language) => language == AppLanguage.Vietnamese
        ? ["cả hai khu vườn", "cả hai nông trại", "cả khu vườn và nông trại", "cả nông trại và khu vườn"]
        : ["both gardens", "both farms", "the garden and the farm", "the farm and the garden"];

    internal static QuizStoryTemplate Create(BasicQuestionContract contract)
    {
        bool vi = contract.Language == AppLanguage.Vietnamese;
        bool planting = IsPlanting(contract.SceneId);
        string action = vi ? planting ? "trồng được" : "thu hoạch được"
            : planting ? "planted" : "harvested";
        string both = Both(contract.Language, contract.Left, contract.Right);

        var prose = vi
            ? new ReviewedQuestionProse(
                ["{name} " + action + " {a} {unit}.", "{name} đã " + action + " {a} {unit}."] ,
                ["{other} " + action + " {b} {unit}.", "{other} đã " + action + " {b} {unit}."] ,
                ["Hỏi " + both + " " + action + " bao nhiêu {unit}?",
                    both + " " + action + " tất cả bao nhiêu {unit}?"],
                ["Số {unit} " + both + " " + action + " là:"],
                AppliedQuestionCatalogue.Find(contract.SceneId)!.UnitId)
            : new ReviewedQuestionProse(
                ["{name} " + action + " {a} {unit}.", "{name} has " + action + " {a} {unit}."],
                ["{other} " + action + " {b} {unit}.", "{other} has " + action + " {b} {unit}."],
                ["How many {unit} did " + both + " " + (planting ? "plant" : "harvest") + " in total?",
                    "How many {unit} have " + both + " " + action + " altogether?"],
                ["The total number of {unit} " + action + " by " + both + " is:"],
                AppliedQuestionCatalogue.Find(contract.SceneId)!.UnitId);

        return new(contract.SceneId, "Applied", prose.Stories().Take(2).ToArray(), prose);
    }

    internal static string AlignVenue(string text, BasicQuestionContract c)
    {
        if (!IsGardenScene(c.SceneId)) return text;
        string pair = Both(c.Language, c.Left, c.Right);
        foreach (string candidate in PairPhrases(c.Language))
            text = text.Replace(candidate, pair, StringComparison.OrdinalIgnoreCase);
        return text;
    }

    internal static bool MatchesVenue(string question, BasicQuestionContract c)
    {
        if (!IsGardenScene(c.SceneId)) return true;
        var language = c.Language;
        string firstVenue = Venue(language, c.Left);
        string secondVenue = Venue(language, c.Right);
        string firstActor = language == AppLanguage.Vietnamese ? firstVenue + " của " : "'s " + firstVenue;
        string secondActor = language == AppLanguage.Vietnamese ? secondVenue + " của " : "'s " + secondVenue;
        string pair = Both(language, c.Left, c.Right);
        return c.Subject.Contains(firstActor, StringComparison.OrdinalIgnoreCase)
            && c.OtherSubject.Contains(secondActor, StringComparison.OrdinalIgnoreCase)
            && question.Contains(pair, StringComparison.OrdinalIgnoreCase)
            && PairPhrases(language).Where(candidate => !candidate.Equals(pair, StringComparison.OrdinalIgnoreCase))
                .All(candidate => !question.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    internal static string Guidance(BasicQuestionContract c)
    {
        bool planting = IsPlanting(c.SceneId);
        string pair = Both(c.Language, c.Left, c.Right);
        return c.Language == AppLanguage.Vietnamese
            ? $"\nĐề trái cây dùng {c.Subject} và {c.OtherSubject}. Mỗi địa điểm được phân loại riêng theo số cây/quả của nó (100 trở xuống là khu vườn, trên 100 là nông trại). Câu hỏi gọi {pair}. "
              + $"Hai dữ kiện phải cùng hành động '{(planting ? "trồng được" : "thu hoạch được")}'. "
              + $"Giữ nguyên đơn vị C# '{{unit}}' = '{c.Unit}' ở hai dữ kiện, câu hỏi và lời giải. "
              + $"Chỉ hỏi tổng số {(planting ? "cây" : "quả")} mà {pair} {(planting ? "trồng được" : "thu hoạch được")}."
            : $"\nFruit problems use {c.Subject} and {c.OtherSubject}. Classify each location by its own count (garden through 100, farm above 100). Refer to {pair} in the question. "
              + $"Both facts must describe {(planting ? "planting" : "harvesting")}. Preserve the C# unit '{{unit}}' = '{c.Unit}' throughout the story and the solution. "
              + $"Ask how many {(planting ? "plants or trees" : "fruits")} {pair} {(planting ? "planted" : "harvested")} in total.";
    }
}
