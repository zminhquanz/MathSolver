using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Countable stock and grouping contexts for subtraction, multiplication and division.
/// Numeric roles, realistic scale and group types are chosen before prompting the model.</summary>
public static class ArithmeticQuestionCatalogue
{
    public const int Version = 4;
    // Reuse the bilingual actor/quantity profiles, not the addition-specific actions.
    // Visits, scores, measurements and random outcomes are deliberately not treated as packable stock.
    private static readonly string[] SceneIds = ["family-gifts", "library", "school-supplies", "donations",
        "recycling", "craft", "notebook-production", "book-distribution", "harvest", "garden", "shop-stock",
        "school-furniture", "food-supplies", "bakery", "poultry", "fish-farm", "cattle", "crop-harvest",
        "product-production", "construction-stock", "green-planting", "survey-responses"];
    public static IReadOnlyList<AdditionScene> Scenes { get; } = Array.AsReadOnly(
        SceneIds.Select(id => AdditionQuestionCatalogue.Find(id)!).ToArray());
    public static AdditionScene? Find(string id) => Scenes.FirstOrDefault(s => s.Id == id);

    // A product may reach twice the primary bucket's maximum. Plan the owner's
    // scope for that total, rather than describing an individual holding factory quantities.
    public static CurriculumTier ActorTier(ArithmeticOperation operation, CurriculumTier tier)
        => operation == ArithmeticOperation.Multiply ? (CurriculumTier)Math.Min(5, (int)tier + 1) : tier;
    public static AdditionQuestionScale? Scale(string sceneId, ArithmeticOperation operation, CurriculumTier tier)
    {
        if (Find(sceneId) is null) return null;
        int stars = (int)ActorTier(operation, tier);
        var scale = AdditionQuestionScales.Find(sceneId, (CurriculumTier)stars);
        if (scale is null) return null;
        // A basket, pond or materials corner cannot sell, lend or use its stock.
        // Bind the keeper/operator instead. Institutions can act on their inventory.
        (string Vi, string En)? actor = sceneId switch {
            "food-supplies" when stars == 1 => ("{person}", "{person}"),
            "library" when stars <= 2 => stars == 1 ? ("{person}", "{person}") : ("người giữ tủ sách {person}", "bookcase keeper {person}"),
            "poultry" when stars == 1 => ("người nuôi gia cầm {person}", "poultry keeper {person}"),
            "fish-farm" when stars <= 2 => stars == 1 ? ("người nuôi cá {person}", "fish keeper {person}") : ("gia đình {person}", "{person}'s family"),
            "garden" when stars <= 2 => stars == 1 ? ("{person}", "{person}") : ("gia đình {person}", "{person}'s family"),
            "school-furniture" when stars <= 2 => stars == 1 ? ("{person}", "{person}") : ("trường do {person} phụ trách", "{person}'s school"),
            "construction-stock" => stars switch {
                1 => ("{person}", "{person}"), 2 => ("đội sửa nhà của {person}", "{person}'s home repair crew"),
                3 => ("đội thi công của {person}", "{person}'s construction crew"),
                4 => ("đơn vị thi công do {person} phụ trách", "{person}'s construction organisation"),
                _ => ("công ty xây dựng do {person} phụ trách", "{person}'s construction company") },
            _ => null };
        return actor is { } bound ? scale with { VietnameseActor = bound.Vi, EnglishActor = bound.En } : scale;
    }
    public static bool Supports(string sceneId, ArithmeticOperation operation, CurriculumTier tier, BasicQuestionStructure structure)
        => operation is ArithmeticOperation.Subtract or ArithmeticOperation.Multiply or ArithmeticOperation.Divide
        && BasicQuestionTemplates.AllowedContextual(operation, tier).Contains(structure)
        && Scale(sceneId, operation, tier) is { } scale
        && scale.MaxOperand >= QuizCurriculumLayer.GetMinimumPrimaryOperandValue(tier);
    public static IEnumerable<(AdditionScene Scene, BasicQuestionStructure Structure)> Available(ArithmeticOperation operation, CurriculumTier tier)
        => Scenes.SelectMany(scene => BasicQuestionTemplates.AllowedContextual(operation, tier)
            .Where(s => Supports(scene.Id, operation, tier, s)).Select(s => (scene, s)));

    public static bool IsValid(BasicQuestionContract c)
    {
        var scene = Find(c.SceneId);
        var scale = Scale(c.SceneId, c.Operation, c.Tier);
        int cap = QuizCurriculumLayer.GetMaximumOperandValue(c.Tier);
        return c.Version == Version && scene is not null && scale is not null
            && c.TopicId == scene.TopicId && Supports(c.SceneId, c.Operation, c.Tier, c.Structure)
            && c.PartA == "" && c.PartB == ""
            && c.Left >= QuizCurriculumLayer.GetMinimumPrimaryOperandValue(c.Tier) && c.Left <= Math.Min(cap, scale.MaxOperand)
            && c.Right > 0 && (c.Operation == ArithmeticOperation.Subtract ? c.Structure == BasicQuestionStructure.Remaining ? c.Right <= c.Left : c.Right < c.Left
                : c.Structure is BasicQuestionStructure.CountGroups or BasicQuestionStructure.CompareFactor ? c.Left % c.Right == 0 && c.Left / c.Right is >= 2 and <= 9
                : c.Right is >= 2 and <= 9)
            && (c.Operation != ArithmeticOperation.Divide || c.Left % c.Right == 0)
            && (c.Operation != ArithmeticOperation.Multiply || (long)c.Left * c.Right <= Math.Min(2L * cap, scale.MaxCombinedQuantity))
            && scale.MatchesActor(c.Language, c.Subject) && scale.MatchesActor(c.Language, c.OtherSubject)
            && scale.UnitIds.Any(id => QuestionUnits.Find(id) is { } u && u.Item(c.Language) == c.Unit && u.GroupFor(c) == c.GroupUnit);
    }

    public static BasicQuestionContract Create(ArithmeticOperation operation, CurriculumTier tier, AppLanguage language,
        Random? random = null, string? sceneId = null, BasicQuestionStructure? structure = null, string? unitId = null)
    {
        if (!Enum.IsDefined(tier) || language is not (AppLanguage.Vietnamese or AppLanguage.English))
            throw new ArgumentOutOfRangeException(nameof(tier));
        random ??= Random.Shared;
        var choices = Available(operation, tier).Where(p => (sceneId is null || p.Scene.Id == sceneId)
            && (structure is null || p.Structure == structure)).ToArray();
        if (choices.Length == 0) throw new ArgumentException("InvalidArithmeticScene");
        var choice = choices[random.Next(choices.Length)];
        var scale = Scale(choice.Scene.Id, operation, tier)!;
        if (unitId is not null && !scale.UnitIds.Contains(unitId)) throw new ArgumentException("ChangedUnits");
        var unit = QuestionUnits.Find(unitId ?? scale.UnitIds[random.Next(scale.UnitIds.Length)])!;
        var (a, b) = Numbers(operation, tier, choice.Structure, scale, random);
        string Actor() => scale.Actor(language, scale.ActorPattern(language) == "{person}"
            ? QuestionNames.Create(language, random) : language == AppLanguage.Vietnamese
                ? (random.Next(2) == 0 ? QuestionNames.VietnameseMale : QuestionNames.VietnameseFemale)[random.Next(100)]
                : QuestionNames.GivenName(QuestionNames.Create(language, random)));
        string name = Actor(), other = name;
        for (int i = 0; i < 32 && other == name; i++) other = Actor();
        if (name == other) other = scale.Actor(language, language == AppLanguage.Vietnamese
            ? name == scale.Actor(language, "An") ? "Bình" : "An" : name == scale.Actor(language, "Emma") ? "James" : "Emma");
        return BasicQuestionTemplates.ApplyUnit(new(Version, operation, tier, language, a, b, name,
            unit.Item(language), unit.Group(language), choice.Structure, other, choice.Scene.TopicId, choice.Scene.Id), unit);
    }

    public static BasicQuestionContract Refresh(BasicQuestionContract c, Random? random = null)
        => Create(c.Operation, c.Tier, c.Language, random, c.SceneId, c.Structure, QuestionUnits.Find(c)!.Id);

    private static (int A, int B) Numbers(ArithmeticOperation operation, CurriculumTier tier, BasicQuestionStructure structure,
        AdditionQuestionScale scale, Random random)
    {
        int min = QuizCurriculumLayer.GetMinimumPrimaryOperandValue(tier);
        int max = Math.Min(QuizCurriculumLayer.GetMaximumOperandValue(tier), scale.MaxOperand);
        if (operation == ArithmeticOperation.Subtract)
        {
            // Prefer borrowing at higher tiers, without ever shrinking the primary digit bucket.
            (int A, int B) best = default;
            int bestBorrows = -1, desired = (int)tier >= 3 && random.Next(3) != 0 ? (int)tier - 2 : 0;
            for (int attempt = 0; attempt < 48; attempt++)
            {
                int a = random.Next(min, max + 1), b = random.Next(1, structure == BasicQuestionStructure.Remaining ? a + 1 : a), count = CountBorrows(a, b);
                if (count > bestBorrows) { best = (a, b); bestBorrows = count; }
                if (count >= desired) return (a, b);
            }
            return best;
        }
        if (operation == ArithmeticOperation.Multiply)
        {
            int totalCap = Math.Min(2 * QuizCurriculumLayer.GetMaximumOperandValue(tier), scale.MaxCombinedQuantity);
            int b = random.Next(2, Math.Min(9, totalCap / min) + 1);
            return (random.Next(min, Math.Min(max, totalCap / b) + 1), b);
        }
        // Construct an exact multiple within the primary bucket. Rejection loops
        // can silently fall back to tiny dividends at high stars.
        int factor = random.Next(2, Math.Min(9, max) + 1);
        int quotient = random.Next(Math.Max(1, (min + factor - 1) / factor), max / factor + 1);
        int dividend = factor * quotient;
        return (dividend, structure is BasicQuestionStructure.CountGroups or BasicQuestionStructure.CompareFactor ? quotient : factor);
    }

    internal static int CountBorrows(int a, int b)
    {
        int borrow = 0, count = 0;
        while (a > 0 || b > 0)
        {
            borrow = a % 10 - borrow < b % 10 ? 1 : 0;
            count += borrow; a /= 10; b /= 10;
        }
        return count;
    }

    public static string GroupFor(BasicQuestionContract c, QuestionUnit unit, bool singular)
    {
        int perGroup = c.Structure switch {
            BasicQuestionStructure.EqualGroups => c.Left,
            BasicQuestionStructure.EqualShare => c.Right > 0 ? c.Left / c.Right : 0,
            BasicQuestionStructure.CountGroups => c.Right,
            _ => 0 };
        (string Vi, string One, string Many) group = unit.Id switch {
            "chickens" or "ducks" => perGroup <= 100 ? ("đàn", "flock", "flocks") : ("khu nuôi", "rearing section", "rearing sections"),
            "cows" => perGroup <= 100 ? ("đàn", "herd", "herds") : ("cụm trang trại", "farm cluster", "farm clusters"),
            "fish" => perGroup <= 100 ? ("bể", "tank", "tanks") : ("ao nuôi", "rearing pond", "rearing ponds"),
            "trees" or "seedlings" => perGroup <= 100 ? ("luống", "row", "rows")
                : perGroup <= 9999 ? ("khu ươm", "nursery section", "nursery sections") : ("cụm vườn ươm", "nursery cluster", "nursery clusters"),
            "desks" or "chairs" => perGroup <= 40 ? ("phòng", "room", "rooms") : ("lô hàng", "stock lot", "stock lots"),
            "bricks" or "tiles" => perGroup <= 1000 ? ("kiện", "pallet", "pallets") : ("lô vật liệu", "materials lot", "materials lots"),
            "responses" => perGroup <= 100 ? ("tập", "bundle", "bundles") : ("lô phiếu", "response batch", "response batches"),
            _ => perGroup <= 100 ? (unit.VietnameseGroup, unit.GroupSingular, unit.GroupPlural) : ("lô hàng", "stock lot", "stock lots") };
        return c.Language == AppLanguage.Vietnamese ? group.Vi : singular ? group.One : group.Many;
    }

    public static (string Vi, string En) Removal(BasicQuestionContract c) => c.SceneId switch {
        "library" => ("cho mượn", "lends out"),
        "recycling" => ("chuyển đi tái chế", "sends for recycling"),
        "construction-stock" => ("sử dụng", "uses"),
        "garden" or "green-planting" => ("chuyển đi", "transfers out"),
        "school-supplies" or "donations" or "craft" => ("phát", "hands out"),
        "survey-responses" => ("chuyển đi xử lý", "sends for processing"),
        "family-gifts" => ("cho đi", "gives away"),
        _ => ("bán", "sells") };

    public static BasicQuestionDraft Example(BasicQuestionContract c)
    {
        // The v2 examples express the same mathematical roles. Use them as a base
        // without recursing into the v4 dispatcher, then specialise the activity.
        var draft = BasicQuestionTemplates.Example(c with { Version = BasicQuestionContract.CurrentVersion }, QuestionUnits.Find(c)!.Id);
        if (c.Structure == BasicQuestionStructure.Remaining)
        {
            var action = Removal(c);
            draft = draft with { GivenB = QuizContentCatalog.Text(c.Language, "ArithmeticQuestionCatalogue.Example.001", ("action_Vi", $"{action.Vi}"), ("action_En", $"{action.En}")) };
        }
        bool living = QuestionUnits.Find(c)!.Id is "chickens" or "ducks" or "fish" or "cows" or "trees" or "seedlings";
        if (living)
        {
            if (c.Structure == BasicQuestionStructure.EqualGroups)
                draft = draft with { GivenA = draft.GivenA.Replace("chứa", "có").Replace("holds", "has") };
            if (c.Structure is BasicQuestionStructure.EqualShare or BasicQuestionStructure.CountGroups)
                draft = draft with { GivenB = draft.GivenB.Replace("xếp đều vào", "chia đều thành").Replace("xếp vào", "chia thành")
                    .Replace("chứa", "có").Replace("packs", "divides") };
            if (c.Structure == BasicQuestionStructure.CountGroups)
                draft = draft with { Question = draft.Question.Replace("xếp", "chia").Replace("fill", "form"),
                    SolutionLead = draft.SolutionLead!.Replace("xếp", "chia").Replace("filled", "formed") };
        }
        return draft;
    }
}

/// <summary>Bounded per-operation history keeps both relations and settings varied.</summary>
public sealed class ArithmeticQuestionCycle(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly Dictionary<(ArithmeticOperation, CurriculumTier, AppLanguage), List<(string Scene, string Topic, BasicQuestionStructure Structure)>> _history = [];
    public BasicQuestionContract Next(ArithmeticOperation operation, CurriculumTier tier, AppLanguage language)
    {
        var key = (operation, tier, language);
        if (!_history.TryGetValue(key, out var history)) _history[key] = history = [];
        var selected = ArithmeticQuestionCatalogue.Available(operation, tier).OrderBy(_ => _random.Next())
            .OrderBy(p => history.Count(h => h.Structure == p.Structure))
            .ThenBy(p => history.Count(h => h.Scene == p.Scene.Id)).ThenBy(p => history.Count(h => h.Topic == p.Scene.TopicId))
            .ThenBy(p => history.Count > 0 && history[^1].Scene == p.Scene.Id).First();
        history.Add((selected.Scene.Id, selected.Scene.TopicId, selected.Structure));
        if (history.Count > 64) history.RemoveAt(0);
        return ArithmeticQuestionCatalogue.Create(operation, tier, language, _random, selected.Scene.Id, selected.Structure);
    }
}
