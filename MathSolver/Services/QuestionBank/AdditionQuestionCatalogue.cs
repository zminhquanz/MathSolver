using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

public enum AdditionSceneKind { Stock, Contributions, Periods, Parts, Arrivals }
public enum AdditionActorKind { Person, Team, Library, Garden, Park, Club, Workshop, Farm }

public sealed record AdditionScene(string Id, string TopicId, AdditionSceneKind Kind, AdditionActorKind ActorKind,
    string[] UnitIds, string VietnameseAction, string EnglishAction, string VietnameseVerbs, string EnglishVerbs,
    int MaxOperand, BasicQuestionStructure[] Structures)
{
    public bool Supports(BasicQuestionStructure structure, CurriculumTier tier) => Structures.Contains(structure)
        && (int)tier >= AdditionQuestionCatalogue.MinimumStars(structure);
}

/// <summary>Independent one-step relations, story settings, actor roles and realistic quantity domains.</summary>
public static class AdditionQuestionCatalogue
{
    public const int Version = 3;
    private static readonly BasicQuestionStructure[] StockRelations = [BasicQuestionStructure.Increase,
        BasicQuestionStructure.Combine, BasicQuestionStructure.RecoverInitial,
        BasicQuestionStructure.AddComparisonMore, BasicQuestionStructure.AddComparisonInverse];
    private static readonly BasicQuestionStructure[] ContributionRelations = [BasicQuestionStructure.Combine,
        BasicQuestionStructure.AddComparisonMore, BasicQuestionStructure.AddComparisonInverse];
    public static IReadOnlyList<AdditionScene> Scenes { get; } = Array.AsReadOnly(new AdditionScene[]
    {
        new("family-gifts", "family", AdditionSceneKind.Stock, AdditionActorKind.Person,
            ["books", "notebooks", "pencils", "candies", "cards", "stickers"], "có", "has", "có|giữ|sở hữu", "has|owns|holds|had", 50000, StockRelations),
        new("library", "school", AdditionSceneKind.Stock, AdditionActorKind.Library,
            ["books", "notebooks"], "có", "has", "có|giữ|lưu giữ", "has|holds|had", 50000, StockRelations),
        new("school-supplies", "school", AdditionSceneKind.Contributions, AdditionActorKind.Team,
            ["books", "notebooks", "pencils", "cards"], "góp được", "contributes", "góp|quyên góp|mang đến|chuẩn bị", "contributes?|donates?|brings?|prepares?", 5000, ContributionRelations),
        new("donations", "community", AdditionSceneKind.Contributions, AdditionActorKind.Team,
            ["books", "notebooks", "pencils", "cakes"], "quyên góp được", "donates", "góp|quyên góp|ủng hộ|chuẩn bị", "contributes?|donates?|prepares?|collects?", 5000, ContributionRelations),
        new("recycling", "environment", AdditionSceneKind.Contributions, AdditionActorKind.Team,
            ["plastic-bottles", "cans"], "thu gom được", "collects", "thu gom|gom|nhặt", "collects?|gathers?|picks? up", 5000, ContributionRelations),
        new("craft", "activities", AdditionSceneKind.Periods, AdditionActorKind.Workshop,
            ["cards", "paper-flowers"], "làm được", "makes", "gấp|làm|cắt|hoàn thành", "folds?|makes?|made|finishes?|cuts?", 50000, [BasicQuestionStructure.Combine]),
        new("harvest", "nature", AdditionSceneKind.Periods, AdditionActorKind.Farm,
            ["apples", "oranges", "flowers"], "thu hoạch được", "harvests", "hái|thu hoạch|thu gom", "picks?|harvests?|gathers?", 5000, [BasicQuestionStructure.Combine]),
        new("garden", "nature", AdditionSceneKind.Parts, AdditionActorKind.Garden,
            ["trees", "seedlings", "flowers"], "có", "has", "có|trồng|mọc", "has|contains?|grows?|planted", 50, [BasicQuestionStructure.Combine]),
        new("sports", "activities", AdditionSceneKind.Periods, AdditionActorKind.Team,
            ["points"], "ghi được", "scores", "ghi|đạt|giành", "scores?|earns?|gains?", 100, [BasicQuestionStructure.Combine]),
        new("birds-arrive", "nature", AdditionSceneKind.Arrivals, AdditionActorKind.Park,
            ["birds"], "bay đến", "fly in", "bay đến|bay tới|đậu thêm|đến thêm", "fly in|flies in|fly into|arrive|join", 250, [BasicQuestionStructure.Increase]),
        new("club-arrivals", "school", AdditionSceneKind.Arrivals, AdditionActorKind.Club,
            ["students"], "đến tham gia", "arrive to join", "đến|tới|tham gia|vào", "arrive|join|come|enter", 20, [BasicQuestionStructure.Increase]),
        new("shop-stock", "shopping", AdditionSceneKind.Stock, AdditionActorKind.Person,
            ["apples", "oranges", "cakes", "balls", "pencils"], "có", "has", "có|giữ|sở hữu", "has|owns|holds|had", 50000, StockRelations)
    });

    public static IReadOnlyList<QuestionUnit> ExtraUnits { get; } = Array.AsReadOnly(new QuestionUnit[]
    {
        new("plastic-bottles", "chai nhựa", "plastic bottle", "plastic bottles", "bao", "sack", "sacks"),
        new("cans", "lon", "can", "cans", "bao", "sack", "sacks"),
        new("paper-flowers", "bông hoa giấy", "paper flower", "paper flowers", "hộp", "box", "boxes"),
        new("trees", "cây", "tree", "trees", "luống", "row", "rows"),
        new("seedlings", "cây con", "seedling", "seedlings", "luống", "row", "rows"),
        new("points", "điểm", "point", "points", "lượt", "round", "rounds"),
        new("birds", "con chim", "bird", "birds", "đàn", "flock", "flocks"),
        new("students", "bạn", "student", "students", "nhóm", "group", "groups")
    });
    public static AdditionScene? Find(string id) => Scenes.FirstOrDefault(s => s.Id == id);
    public static int MinimumStars(BasicQuestionStructure structure) => structure switch
    {
        BasicQuestionStructure.Increase or BasicQuestionStructure.Combine => 1,
        BasicQuestionStructure.AddComparisonMore => 2,
        BasicQuestionStructure.RecoverInitial or BasicQuestionStructure.AddComparisonInverse => 3,
        _ => int.MaxValue
    };
    public static IEnumerable<(AdditionScene Scene, BasicQuestionStructure Structure)> Available(CurriculumTier tier)
        => Scenes.SelectMany(s => s.Structures.Where(r => s.Supports(r, tier)).Select(r => (s, r)));

    public static bool IsValid(BasicQuestionContract c)
    {
        var scene = Find(c.SceneId);
        return c.Version == Version && c.Operation == ArithmeticOperation.Add && scene is not null
            && scene.TopicId == c.TopicId && scene.Supports(c.Structure, c.Tier)
            && c.Left <= scene.MaxOperand && c.Right <= scene.MaxOperand
            && scene.UnitIds.Any(id => QuestionUnits.Find(id) is { } u && u.Item(c.Language) == c.Unit && u.GroupFor(c) == c.GroupUnit)
            && (c.PartA, c.PartB) == Parts(scene, c.Language);
    }

    public static BasicQuestionContract Create(CurriculumTier tier, AppLanguage language, Random? random = null,
        string? sceneId = null, BasicQuestionStructure? structure = null)
    {
        if (!Enum.IsDefined(tier) || language is not (AppLanguage.Vietnamese or AppLanguage.English))
            throw new ArgumentOutOfRangeException(nameof(tier));
        random ??= Random.Shared;
        var choices = Available(tier).Where(p => (sceneId is null || p.Scene.Id == sceneId)
            && (structure is null || p.Structure == structure)).ToArray();
        if (choices.Length == 0) throw new ArgumentException("InvalidAdditionScene");
        var choice = choices[random.Next(choices.Length)];
        var numbers = BasicQuestionContract.Create(ArithmeticOperation.Add, tier, language, random);
        int a = numbers.Left, b = numbers.Right;
        // Reroll rather than clamp: large tiers must not freeze bounded settings at their limit.
        if (a > choice.Scene.MaxOperand) a = random.Next(1, choice.Scene.MaxOperand + 1);
        if (b > choice.Scene.MaxOperand) b = random.Next(1, choice.Scene.MaxOperand + 1);
        string name = Actor(choice.Scene, language, random, Math.Max(a, b)), other = name;
        for (int i = 0; i < 32 && QuestionNames.GivenName(other) == QuestionNames.GivenName(name); i++)
            other = Actor(choice.Scene, language, random, Math.Max(a, b));
        if (QuestionNames.GivenName(other) == QuestionNames.GivenName(name))
            other = Actor(choice.Scene, language, new Random(7919), Math.Max(a, b));
        if (other == name) other = language == AppLanguage.Vietnamese ? "nhóm khác" : "the other team";
        var unit = QuestionUnits.Find(choice.Scene.UnitIds[random.Next(choice.Scene.UnitIds.Length)])!;
        var parts = Parts(choice.Scene, language);
        return BasicQuestionTemplates.ApplyUnit(new(Version, ArithmeticOperation.Add, tier, language, a, b,
            name, unit.Item(language), unit.Group(language), choice.Structure, other,
            choice.Scene.TopicId, choice.Scene.Id, parts.A, parts.B), unit);
    }

    public static BasicQuestionContract Refresh(BasicQuestionContract stored, Random? random = null)
        => BasicQuestionTemplates.ApplyUnit(Create(stored.Tier, stored.Language, random, stored.SceneId, stored.Structure), QuestionUnits.Find(stored)!);

    private static (string A, string B) Parts(AdditionScene scene, AppLanguage language)
    {
        bool vi = language == AppLanguage.Vietnamese;
        return scene.Kind switch
        {
            AdditionSceneKind.Periods when scene.Id == "sports" => vi ? ("hiệp đầu", "hiệp sau") : ("the first half", "the second half"),
            AdditionSceneKind.Periods => vi ? ("buổi sáng", "buổi chiều") : ("the morning", "the afternoon"),
            AdditionSceneKind.Parts => vi ? ("luống bên trái", "luống bên phải") : ("the left row", "the right row"),
            _ => ("", "")
        };
    }

    private static string Actor(AdditionScene scene, AppLanguage language, Random random, int quantity)
    {
        bool vi = language == AppLanguage.Vietnamese;
        var pool = random.Next(2) == 0 ? QuestionNames.VietnameseMale : QuestionNames.VietnameseFemale;
        string person = vi ? pool[random.Next(pool.Count)] : QuestionNames.GivenName(QuestionNames.Create(language, random));
        return scene.ActorKind switch
        {
            AdditionActorKind.Person => scene.Id == "shop-stock" || quantity > 100
                ? vi ? $"cửa hàng của {person}" : $"{person}'s shop" : QuestionNames.Create(language, random),
            AdditionActorKind.Team => vi ? $"nhóm của {person}" : $"{person}'s team",
            AdditionActorKind.Library => vi ? $"thư viện trường {person}" : $"{person} School's library",
            AdditionActorKind.Garden => vi ? $"vườn của {person}" : $"{person}'s garden",
            AdditionActorKind.Park => vi ? $"công viên {person}" : $"{person} Park",
            AdditionActorKind.Club => vi ? $"câu lạc bộ của {person}" : $"{person}'s club",
            AdditionActorKind.Workshop => quantity > 100 ? vi ? $"xưởng của {person}" : $"{person}'s workshop" : person,
            _ => quantity > 100 ? vi ? $"nông trại của {person}" : $"{person}'s farm" : person
        };
    }

    public static BasicQuestionDraft Example(BasicQuestionContract c)
    {
        var scene = Find(c.SceneId)!;
        bool vi = c.Language == AppLanguage.Vietnamese;
        string verb = vi ? scene.VietnameseAction : scene.EnglishAction;
        string baseVerb = scene.Id switch { "craft" => "make", "harvest" => "harvest", "sports" => "score",
            "recycling" => "collect", "donations" => "donate", "school-supplies" => "contribute", _ => "have" };
        string periods = scene.Id == "sports" ? "các hiệp" : "các buổi";
        string loss = scene.Id == "shop-stock" ? "bán" : "cho đi";
        string amount = scene.Kind == AdditionSceneKind.Contributions ? $"{verb}" : "có";
        string amountEn = scene.Kind == AdditionSceneKind.Contributions ? baseVerb : "have";
        var clauses = c.Structure switch
        {
            BasicQuestionStructure.Increase when scene.Kind == AdditionSceneKind.Arrivals => vi
                ? ("Ở {name} có {a} {unit}.", $"Có {{b}} {{unit}} khác {verb} {{name}}.", "Hỏi ở {name} có tất cả bao nhiêu {unit}?", "Tổng số {unit} ở {name} là:")
                : ("There are {a} {unit} at {name}.", scene.Id == "birds-arrive" ? "Another {b} {unit} fly into {name}." : "Another {b} {unit} arrive to join {name}.", "How many {unit} are at {name} altogether?", "The total number of {unit} at {name} is:"),
            BasicQuestionStructure.Increase => vi
                ? ("{name} có {a} {unit}.", "{name} bổ sung thêm {b} {unit}.", "Hỏi {name} có tất cả bao nhiêu {unit}?", "Tổng số {unit} mà {name} có là:")
                : ("{name} has {a} {unit}.", "{name} gets {b} more {unit}.", "How many {unit} does {name} have in total?", "The total number of {unit} that {name} has is:"),
            BasicQuestionStructure.Combine when scene.Kind == AdditionSceneKind.Periods => vi
                ? ($"Vào {{part_a}}, {{name}} {verb} {{a}} {{unit}}.", $"Vào {{part_b}}, {{name}} {verb} {{b}} {{unit}}.", $"Qua {periods}, {{name}} {verb} tất cả bao nhiêu {{unit}}?", $"Tổng số {{unit}} mà {{name}} {verb} qua {periods} là:")
                : ($"In {{part_a}}, {{name}} {verb} {{a}} {{unit}}.", $"In {{part_b}}, {{name}} {verb} {{b}} {{unit}}.", $"How many {{unit}} does {{name}} {baseVerb} in total over these periods?", $"The total number of {{unit}} {(scene.Id == "sports" ? "scored" : scene.Id == "harvest" ? "harvested" : "made")} over these periods is:"),
            BasicQuestionStructure.Combine when scene.Kind == AdditionSceneKind.Parts => vi
                ? ("{part_a} trong {name} có {a} {unit}.", "{part_b} trong {name} có {b} {unit}.", "Hỏi các luống này trong {name} có tổng cộng bao nhiêu {unit}?", "Tổng số {unit} trong các luống này của {name} là:")
                : ("{part_a} in {name} has {a} {unit}.", "{part_b} in {name} has {b} {unit}.", "How many {unit} are in these rows of {name} altogether?", "The total number of {unit} in these rows is:"),
            BasicQuestionStructure.Combine => vi
                ? ($"{{name}} {verb} {{a}} {{unit}}.", $"{{other}} {verb} {{b}} {{unit}}.", $"Hỏi {{name}} và {{other}} {amount} tổng cộng bao nhiêu {{unit}}?", "Tổng số {unit} của {name} và {other} là:")
                : ($"{{name}} {verb} {{a}} {{unit}}.", $"{{other}} {verb} {{b}} {{unit}}.", $"How many {{unit}} do {{name}} and {{other}} {amountEn} altogether?", "The combined number of {unit} is:"),
            BasicQuestionStructure.RecoverInitial => vi
                ? ("{name} còn lại {a} {unit}.", $"Trước đó, {{name}} đã {loss} {{b}} {{unit}}.", "Hỏi ban đầu {name} có bao nhiêu {unit}?", "Số {unit} mà {name} có ban đầu là:")
                : ("{name} has {a} {unit} left.", "Previously, {name} gave away {b} {unit}.", "How many {unit} did {name} have originally?", "The original number of {unit} that {name} had is:"),
            BasicQuestionStructure.AddComparisonMore => vi
                ? ($"{{other}} {verb} {{a}} {{unit}}.", $"{{name}} {amount} nhiều hơn {{other}} là {{b}} {{unit}}.", $"Hỏi {{name}} {amount} bao nhiêu {{unit}}?", $"Số {{unit}} mà {{name}} {amount} là:")
                : ($"{{other}} {verb} {{a}} {{unit}}.", $"{{name}} {verb} {{b}} more {{unit}} than {{other}}.", $"How many {{unit}} does {{name}} {amountEn}?", $"The number of {{unit}} from {{name}} is:"),
            _ => vi
                ? ($"{{other}} {verb} {{a}} {{unit}}.", $"{{other}} {amount} ít hơn {{name}} là {{b}} {{unit}}.", $"Hỏi {{name}} {amount} bao nhiêu {{unit}}?", $"Số {{unit}} mà {{name}} {amount} là:")
                : ($"{{other}} {verb} {{a}} {{unit}}.", $"{{other}} {verb} {{b}} fewer {{unit}} than {{name}}.", $"How many {{unit}} does {{name}} {amountEn}?", $"The number of {{unit}} from {{name}} is:")
        };
        string a = clauses.Item1.TrimEnd('.') + ",";
        string b = char.ToLowerInvariant(clauses.Item2[0]) + clauses.Item2[1..];
        return new(a, b, clauses.Item3, clauses.Item4, QuestionUnits.Find(c)!.Id);
    }
}

/// <summary>Choose relations first, then topics/settings; a large theme catalogue cannot dominate a batch.</summary>
public sealed class AdditionQuestionCycle(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly Dictionary<(CurriculumTier, AppLanguage), List<(string Scene, string Topic, BasicQuestionStructure Structure)>> _history = [];
    public BasicQuestionContract Next(CurriculumTier tier, AppLanguage language)
    {
        if (!Enum.IsDefined(tier) || language is not (AppLanguage.Vietnamese or AppLanguage.English))
            throw new ArgumentOutOfRangeException(nameof(tier));
        var key = (tier, language);
        if (!_history.TryGetValue(key, out var history)) _history[key] = history = [];
        var candidates = AdditionQuestionCatalogue.Available(tier).OrderBy(_ => _random.Next())
            .OrderBy(p => history.Count(h => h.Structure == p.Structure))
            .ThenBy(p => history.Count(h => h.Topic == p.Scene.TopicId))
            .ThenBy(p => history.Count(h => h.Scene == p.Scene.Id))
            .ThenBy(p => history.Count > 0 && history[^1].Scene == p.Scene.Id).ToArray();
        var selected = candidates[0];
        history.Add((selected.Scene.Id, selected.Scene.TopicId, selected.Structure));
        if (history.Count > 64) history.RemoveAt(0);
        return AdditionQuestionCatalogue.Create(tier, language, _random, selected.Scene.Id, selected.Structure);
    }
}
