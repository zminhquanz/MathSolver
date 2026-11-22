using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Authored subjects and units. Country examples never determine a mathematical relation.</summary>
internal sealed record FoodDessertShopEntry(string UnitId, string ShopKind);

public static class FoodThemeCatalogue
{
    public sealed record Subject(QuestionKnowledgeGroup Group, QuestionUnit Unit, string ArtworkId,
        string? FractionVietnamese = null, string? FractionEnglish = null, string? FractionArtworkId = null);
    public static IReadOnlyList<Subject> Subjects { get; } = Array.AsReadOnly<Subject>([.. BreadQuestionCatalogue.Types.Select(t => new Subject(QuestionKnowledgeGroup.Food, t.Unit, t.ArtworkId)), .. QuizContentCatalog.LoadList<Subject>("FoodThemeSubjects")]);
    // A shared C# source for question scenes, AI templates, PDF and Excel contracts.
    // Shared scenes for additional dishes; their selling venue depends on IsBakeryFood.
    private static readonly IReadOnlyList<string> RestaurantDishIds = QuizContentCatalog.LoadList<string>("FoodThemeRestaurantDishIds");
    public static IReadOnlyList<Subject> NewRestaurantDishes { get; } =
        Array.AsReadOnly(RestaurantDishIds.Select(id => Subjects.Single(subject => subject.Unit.Id == id)).ToArray());
    // Match the complete unit ID: "steak-bo" must not capture "steak-bo-sot-tieu".
    internal static Subject? RestaurantDishForScene(string sceneId)
    {
        const string appliedPrefix = "theme-food-";
        const string fractionPrefix = "fraction-theme-food-";
        string prefix = sceneId.StartsWith(fractionPrefix, StringComparison.Ordinal) ? fractionPrefix
            : sceneId.StartsWith(appliedPrefix, StringComparison.Ordinal) ? appliedPrefix : "";
        if (prefix.Length == 0) return null;
        int suffixStart = sceneId.LastIndexOf('-');
        if (suffixStart <= prefix.Length) return null;
        string suffix = sceneId[(suffixStart + 1)..];
        if (suffix is not ("total" or "remaining" or "groups" or "share")) return null;
        string dishId = sceneId[prefix.Length..suffixStart];
        return NewRestaurantDishes.FirstOrDefault(d => d.Unit.Id == dishId);
    }
    // Existing registries retain their original singular/plural/group units for saved contracts.
    private static readonly IReadOnlyList<string> ExistingUnits = QuizContentCatalog.LoadList<string>("FoodThemeExistingUnits");
    internal static IEnumerable<QuestionUnit> AdditionalUnits => Subjects.Where(s => !ExistingUnits.Contains(s.Unit.Id)).Select(s => s.Unit);
    public static bool IsTheme(QuestionKnowledgeGroup group) => group is QuestionKnowledgeGroup.Food or QuestionKnowledgeGroup.Drinks or QuestionKnowledgeGroup.Fruit;
    private static readonly HashSet<string> CoffeeDrinkIds = new(QuizContentCatalog.LoadList<string>("FoodThemeCoffeeDrinkIds"), StringComparer.Ordinal);
    public static bool IsCoffeeDrinkId(string? unitId) => unitId is not null && CoffeeDrinkIds.Contains(unitId);
    private static readonly HashSet<string> MilkTeaDrinkIds = new(QuizContentCatalog.LoadList<string>("FoodThemeMilkTeaDrinkIds"), StringComparer.Ordinal);
    public static bool IsMilkTeaDrinkId(string? unitId) => unitId is not null && MilkTeaDrinkIds.Contains(unitId);
    internal static Subject? DrinkForScene(string sceneId)
    {
        const string applied = "theme-drinks-";
        const string fraction = "fraction-theme-drinks-";
        string prefix = sceneId.StartsWith(fraction, StringComparison.Ordinal) ? fraction
            : sceneId.StartsWith(applied, StringComparison.Ordinal) ? applied : "";
        if (prefix.Length == 0) return null;
        int suffixStart = sceneId.LastIndexOf('-');
        if (suffixStart <= prefix.Length || sceneId[(suffixStart + 1)..] is not ("total" or "remaining" or "groups" or "share"))
            return null;
        string id = sceneId[prefix.Length..suffixStart];
        return Subjects.FirstOrDefault(s => s.Group == QuestionKnowledgeGroup.Drinks && s.Unit.Id == id);
    }
    internal static bool IsCoffeeDrinkContract(BasicQuestionContract contract)
    {
        if (contract.KnowledgeGroup != QuestionKnowledgeGroup.Drinks) return false;
        string sceneId = contract.Version == FindXQuestionCatalogue.Version
            ? FindXQuestionCatalogue.Find(contract.SceneId)?.SourceScene.Id ?? "" : contract.SceneId;
        return IsCoffeeDrinkId(DrinkForScene(sceneId)?.Unit.Id);
    }
    internal static bool IsMilkTeaDrinkContract(BasicQuestionContract contract)
    {
        if (contract.KnowledgeGroup != QuestionKnowledgeGroup.Drinks) return false;
        string sceneId = contract.Version == FindXQuestionCatalogue.Version
            ? FindXQuestionCatalogue.Find(contract.SceneId)?.SourceScene.Id ?? "" : contract.SceneId;
        return IsMilkTeaDrinkId(DrinkForScene(sceneId)?.Unit.Id);
    }
    private static readonly IReadOnlyDictionary<string, string> DessertShopKinds =
        QuizContentCatalog.LoadList<FoodDessertShopEntry>("FoodThemeDessertShopKinds")
            .ToDictionary(entry => entry.UnitId, entry => entry.ShopKind, StringComparer.Ordinal);
    public static bool IsCheDessert(string? unitId) =>
        unitId is not null && DessertShopKinds.TryGetValue(unitId, out var shopKind) && shopKind == "chè";
    // One stable venue type for each prepared dessert, shared by C#, AI, PDF and Excel.
    internal static string? DessertShopKind(string? unitId) =>
        unitId is not null && DessertShopKinds.TryGetValue(unitId, out var shopKind) ? shopKind : null;
    internal static string? DessertShopKind(BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food) return null;
        string? unitId = c.Version switch
        {
            FractionQuestionCatalogue.Version => RestaurantDishForScene(c.SceneId)?.Unit.Id,
            FindXQuestionCatalogue.Version => FindXQuestionCatalogue.Find(c.SceneId)?.SourceScene.UnitId,
            AppliedQuestionCatalogue.Version => AppliedQuestionCatalogue.Find(c.SceneId)?.UnitId,
            _ => null
        };
        return DessertShopKind(unitId);
    }
    internal static bool IsCheDessertContract(BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food) return false;
        return c.Version switch
        {
            FractionQuestionCatalogue.Version => IsCheDessert(RestaurantDishForScene(c.SceneId)?.Unit.Id),
            FindXQuestionCatalogue.Version => IsCheDessert(FindXQuestionCatalogue.Find(c.SceneId)?.SourceScene.UnitId),
            AppliedQuestionCatalogue.Version => IsCheDessert(AppliedQuestionCatalogue.Find(c.SceneId)?.UnitId),
            _ => false
        };
    }
    // Food names are stable identifiers shared by C#, AI contracts, PDF and Excel.
    // Bánh cuốn and bánh xèo remain restaurant dishes.
    private static readonly HashSet<string> BakeryIds = new(
        QuizContentCatalog.LoadList<string>("FoodThemeBakeryIds"), StringComparer.Ordinal);
    public static bool IsBakeryFood(string? unitId) => unitId is not null && BakeryIds.Contains(unitId);

    public static bool IsBakeryFractionScene(string sceneId) =>
        Subjects.Any(s => s.Group == QuestionKnowledgeGroup.Food && IsBakeryFood(s.Unit.Id)
            && sceneId.StartsWith($"fraction-theme-food-{s.Unit.Id}-", StringComparison.Ordinal));

    public static bool IsBakeryContract(BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food) return false;
        if (c.Version == FractionQuestionCatalogue.Version) return IsBakeryFractionScene(c.SceneId);
        if (c.Version == FindXQuestionCatalogue.Version)
            return IsBakeryFood(FindXQuestionCatalogue.Find(c.SceneId)?.SourceScene.UnitId);
        if (c.Version == AppliedQuestionCatalogue.Version)
            return IsBakeryFood(AppliedQuestionCatalogue.Find(c.SceneId)?.UnitId);
        return false;
    }
    internal static string EnglishFractionUnit(string sceneId, string unit, bool proper, bool one)
    {
        const string prefix = "fraction-theme-";
        int suffixStart = sceneId.LastIndexOf('-');
        if (!sceneId.StartsWith(prefix, StringComparison.Ordinal) || suffixStart <= prefix.Length) return unit;
        string groupAndUnit = sceneId[prefix.Length..suffixStart];
        var subject = Subjects.FirstOrDefault(s => groupAndUnit ==
            $"{s.Group.ToString().ToLowerInvariant()}-{s.Unit.Id}");
        if (subject is null || (!proper && !one)) return unit;
        string singular = subject.FractionEnglish?.Replace("litres", "litre", StringComparison.Ordinal) ?? subject.Unit.Singular;
        return proper ? "of " + ("aeiou".Contains(char.ToLowerInvariant(singular[0])) ? "an " : "a ") + singular : singular;
    }
    private static readonly HashSet<string> LegacyItemNames = new(
        QuizContentCatalog.LoadList<string>("FoodThemeLegacyItemNames"), StringComparer.Ordinal);
    private static readonly HashSet<string> LegacySubjectUnitIds = new(
        QuizContentCatalog.LoadList<string>("FoodThemeLegacySubjectUnitIds"), StringComparer.Ordinal);
    public static bool IsThemedItem(string? unit) => unit is not null &&
        (LegacyItemNames.Contains(unit)
            || Subjects.Any(s => unit == s.Unit.Vietnamese || unit == s.Unit.Plural || unit == s.Unit.Singular));
    public static bool IsLegacyContract(BasicQuestionContract c) => c.KnowledgeGroup == QuestionKnowledgeGroup.Objects &&
        (c.Version == FractionQuestionCatalogue.Version ? FractionQuestionCatalogue.Find(c.SceneId) is { } f && IsLegacyFraction(f)
        : c.Version == FindXQuestionCatalogue.Version ? FindXQuestionCatalogue.Find(c.SceneId) is { } x && IsLegacySubject(x.SourceScene)
        : c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.Find(c.SceneId) is { } a && IsLegacySubject(a)
        : IsThemedItem(c.Unit));
    public static bool IsLegacySubject(AppliedQuestionScene s) => s.Group == QuestionKnowledgeGroup.Objects
        && (LegacySubjectUnitIds.Contains(s.UnitId) || Subjects.Any(t => t.Unit.Id == s.UnitId));
    public static bool IsLegacyFraction(FractionQuestionCatalogue.Scene s) => s.Group == QuestionKnowledgeGroup.Objects
        && (s.Id.StartsWith("fraction-bread-",StringComparison.Ordinal) || s.Id.StartsWith("fraction-cake-",StringComparison.Ordinal));
}
