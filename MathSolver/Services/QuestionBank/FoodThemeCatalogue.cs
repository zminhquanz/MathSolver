using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Authored subjects and units. Country examples never determine a mathematical relation.</summary>
public static class FoodThemeCatalogue
{
    public sealed record Subject(QuestionKnowledgeGroup Group, QuestionUnit Unit, string ArtworkId,
        string? FractionVietnamese = null, string? FractionEnglish = null, string? FractionArtworkId = null);
    private static QuestionUnit Unit(string id, string vi, string one, string many) => new(id, vi, one, many, "khay", "tray", "trays");
    public static IReadOnlyList<Subject> Subjects { get; } = Array.AsReadOnly<Subject>(
    [
        .. BreadQuestionCatalogue.Types.Select(t => new Subject(QuestionKnowledgeGroup.Food, t.Unit, t.ArtworkId)),
        new(QuestionKnowledgeGroup.Food, Unit("cakes","chiếc bánh kem","cream cake","cream cakes"),"cake"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-cam","chiếc bánh cam","Vietnamese sesame ball","Vietnamese sesame balls"),"banh_cam"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-com","chiếc bánh cốm","green rice cake","green rice cakes"),"banh_com"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-trung-thu","chiếc bánh trung thu","mooncake","mooncakes"),"banh_trung_thu"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-pia","chiếc bánh pía","pia pastry","pia pastries"),"banh_pia"),
        new(QuestionKnowledgeGroup.Food, Unit("croissants","chiếc bánh croissant","croissant","croissants"),"croissant"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-da-lon","miếng bánh da lợn","layered steamed cake slice","layered steamed cake slices"),"banh_da_lon"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-bo","chiếc bánh bò","Vietnamese honeycomb cake","Vietnamese honeycomb cakes"),"banh_bo"),
        new(QuestionKnowledgeGroup.Food, Unit("muffins","chiếc bánh muffin","muffin","muffins"),"muffin"),
        new(QuestionKnowledgeGroup.Food, Unit("cupcakes","chiếc bánh cupcake","cupcake","cupcakes"),"cupcake"),
        new(QuestionKnowledgeGroup.Food, Unit("cookies","chiếc bánh cookie","cookie","cookies"),"cookie"),
        new(QuestionKnowledgeGroup.Food, Unit("donuts","chiếc bánh donut","donut","donuts"),"donut"),
        new(QuestionKnowledgeGroup.Food, Unit("waffles","chiếc bánh waffle","waffle","waffles"),"waffle"),
        new(QuestionKnowledgeGroup.Food, Unit("apple-pies","chiếc bánh táo","apple pie","apple pies"),"apple_pie"),
        new(QuestionKnowledgeGroup.Food, Unit("flans","chiếc bánh flan","crème caramel","crème caramels"),"flan"),
        new(QuestionKnowledgeGroup.Food, Unit("biscuits","chiếc bánh quy","biscuit","biscuits"),"biscuit"),
        new(QuestionKnowledgeGroup.Food, Unit("eggs","quả trứng","egg","eggs"),"egg"),
        new(QuestionKnowledgeGroup.Food, Unit("candies","viên kẹo","sweet","sweets"),"candy"),
        new(QuestionKnowledgeGroup.Food, Unit("pho","tô phở","bowl of pho","bowls of pho"),"pho"),
        new(QuestionKnowledgeGroup.Food, Unit("lamingtons","chiếc bánh lamington","lamington","lamingtons"),"lamington"),
        new(QuestionKnowledgeGroup.Food, Unit("fish-chips","suất cá chiên và khoai tây","fish and chips meal","fish and chips meals"),"fish_chips"),
        new(QuestionKnowledgeGroup.Food, Unit("hamburgers","chiếc hamburger","hamburger","hamburgers"),"hamburger"),
        new(QuestionKnowledgeGroup.Food, Unit("pancakes","chiếc bánh pancake","pancake","pancakes"),"pancake"),
        new(QuestionKnowledgeGroup.Food, Unit("com-tam","suất cơm tấm","serving of broken rice","servings of broken rice"),"com_tam"),
        new(QuestionKnowledgeGroup.Food, Unit("op-la","phần trứng ốp-la","serving of fried eggs","servings of fried eggs"),"op_la"),
        new(QuestionKnowledgeGroup.Food, Unit("thit-kho-trung","phần thịt kho trứng","serving of braised pork and eggs","servings of braised pork and eggs"),"thit_kho_trung"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-xeo","chiếc bánh xèo","Vietnamese sizzling pancake","Vietnamese sizzling pancakes"),"banh_xeo"),
        new(QuestionKnowledgeGroup.Food, Unit("bun-thit-nuong-cha-gio","tô bún thịt nướng chả giò","bowl of grilled pork vermicelli with spring rolls","bowls of grilled pork vermicelli with spring rolls"),"bun_thit_nuong_cha_gio"),
        new(QuestionKnowledgeGroup.Food, Unit("hu-tieu-nam-vang","tô hủ tiếu Nam Vang","bowl of Nam Vang noodle soup","bowls of Nam Vang noodle soup"),"hu_tieu_nam_vang"),
        new(QuestionKnowledgeGroup.Food, Unit("mi-quang","tô mì Quảng","bowl of Quang noodles","bowls of Quang noodles"),"mi_quang"),
        new(QuestionKnowledgeGroup.Food, Unit("bun-bo-hue","tô bún bò Huế","bowl of Hue beef noodle soup","bowls of Hue beef noodle soup"),"bun_bo_hue"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-cuon","đĩa bánh cuốn","plate of steamed rice rolls","plates of steamed rice rolls"),"banh_cuon"),
        new(QuestionKnowledgeGroup.Food, Unit("kimchi","đĩa kim chi","plate of kimchi","plates of kimchi"),"kimchi"),
        new(QuestionKnowledgeGroup.Food, Unit("sushi","phần sushi","serving of sushi","servings of sushi"),"sushi"),
        new(QuestionKnowledgeGroup.Drinks, Unit("drinking-water","ly nước lọc","glass of drinking water","glasses of drinking water"),"water","lít nước lọc","litres of drinking water"),
        new(QuestionKnowledgeGroup.Drinks, Unit("milk-glasses","ly sữa","glass of milk","glasses of milk"),"milk_liquid","lít sữa","litres of milk"),
        new(QuestionKnowledgeGroup.Drinks, Unit("milk-cartons","hộp sữa","milk carton","milk cartons"),"milk_carton","lít sữa","litres of milk","milk_liquid"),
        new(QuestionKnowledgeGroup.Drinks, Unit("orange-juice-glasses","ly nước cam","glass of orange juice","glasses of orange juice"),"orange_juice","lít nước cam","litres of orange juice"),
        new(QuestionKnowledgeGroup.Drinks, Unit("tea-cups","tách trà","cup of tea","cups of tea"),"tea","lít trà","litres of tea"),
        new(QuestionKnowledgeGroup.Drinks, Unit("lemon-tea-glasses","ly trà chanh","glass of lemon tea","glasses of lemon tea"),"lemon_tea","lít trà chanh","litres of lemon tea"),
        new(QuestionKnowledgeGroup.Drinks, Unit("milkshakes","ly sữa lắc","glass of milkshake","glasses of milkshake"),"milkshake","lít sữa lắc","litres of milkshake"),
        new(QuestionKnowledgeGroup.Fruit, Unit("apples","quả táo","apple","apples"),"apple"),
        new(QuestionKnowledgeGroup.Fruit, Unit("oranges","quả cam","orange","oranges"),"orange"),
        new(QuestionKnowledgeGroup.Fruit, Unit("mangoes","quả xoài","mango","mangoes"),"mango"),
        new(QuestionKnowledgeGroup.Fruit, Unit("bananas","quả chuối","banana","bananas"),"banana"),
        new(QuestionKnowledgeGroup.Fruit, Unit("dragon-fruit","quả thanh long","dragon fruit","dragon fruits"),"dragon_fruit"),
        new(QuestionKnowledgeGroup.Fruit, Unit("strawberries","quả dâu tây","strawberry","strawberries"),"strawberry"),
        new(QuestionKnowledgeGroup.Fruit, Unit("pears","quả lê","pear","pears"),"pear")
    ]);
    // Existing registries retain their original singular/plural/group units for saved contracts.
    private static readonly string[] ExistingUnits = ["sandwich-bread","round-bread","banh-mi","cakes","candies","milk-cartons","apples","oranges","mangoes"];
    internal static IEnumerable<QuestionUnit> AdditionalUnits => Subjects.Where(s => !ExistingUnits.Contains(s.Unit.Id)).Select(s => s.Unit);
    public static bool IsTheme(QuestionKnowledgeGroup group) => group is QuestionKnowledgeGroup.Food or QuestionKnowledgeGroup.Drinks or QuestionKnowledgeGroup.Fruit;
    // Food names are stable identifiers shared by C#, AI contracts, PDF and Excel.
    // Bánh cuốn and bánh xèo remain restaurant dishes.
    public static bool IsBakeryFood(string? unitId) => unitId is
        "sandwich-bread" or "round-bread" or "banh-mi" or "bread-rolls" or
        "cakes" or "biscuits" or "lamingtons" or "pancakes" or
        "banh-cam" or "banh-com" or "banh-trung-thu" or "banh-pia" or
        "croissants" or "banh-da-lon" or "banh-bo" or "muffins" or
        "cupcakes" or "cookies" or "donuts" or "waffles" or "apple-pies" or "flans";

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
        var subject = Subjects.FirstOrDefault(s => sceneId.StartsWith(
            $"fraction-theme-{s.Group.ToString().ToLowerInvariant()}-{s.Unit.Id}-", StringComparison.Ordinal));
        if (subject is null || (!proper && !one)) return unit;
        string singular = subject.FractionEnglish?.Replace("litres", "litre", StringComparison.Ordinal) ?? subject.Unit.Singular;
        return proper ? "of " + ("aeiou".Contains(char.ToLowerInvariant(singular[0])) ? "an " : "a ") + singular : singular;
    }
    public static bool IsThemedItem(string? unit) => unit is "chiếc bánh mì" or "ổ bánh mì" or "bread rolls" or "bread roll" or "suất ăn" or "meals" or "túi gạo" or "rice bags"
        || Subjects.Any(s => unit == s.Unit.Vietnamese || unit == s.Unit.Plural || unit == s.Unit.Singular);
    public static bool IsLegacyContract(BasicQuestionContract c) => c.KnowledgeGroup == QuestionKnowledgeGroup.Objects &&
        (c.Version == FractionQuestionCatalogue.Version ? FractionQuestionCatalogue.Find(c.SceneId) is { } f && IsLegacyFraction(f)
        : c.Version == FindXQuestionCatalogue.Version ? FindXQuestionCatalogue.Find(c.SceneId) is { } x && IsLegacySubject(x.SourceScene)
        : c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.Find(c.SceneId) is { } a && IsLegacySubject(a)
        : IsThemedItem(c.Unit));
    public static bool IsLegacySubject(AppliedQuestionScene s) => s.Group == QuestionKnowledgeGroup.Objects
        && (s.UnitId is "rolls" or "meals" or "rice-bags" || Subjects.Any(t => t.Unit.Id == s.UnitId));
    public static bool IsLegacyFraction(FractionQuestionCatalogue.Scene s) => s.Group == QuestionKnowledgeGroup.Objects
        && (s.Id.StartsWith("fraction-bread-",StringComparison.Ordinal) || s.Id.StartsWith("fraction-cake-",StringComparison.Ordinal));
}
