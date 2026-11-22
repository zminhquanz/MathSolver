using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

public enum AdditionSceneKind { Stock, Contributions, Periods, Parts, Arrivals }
public enum AdditionActorKind { Person, Team, Library, Garden, Park, Club, Workshop, Farm }

public sealed record AdditionScene(string Id, string TopicId, AdditionSceneKind Kind, AdditionActorKind ActorKind,
    string[] UnitIds, string VietnameseAction, string EnglishAction, string VietnameseVerbs, string EnglishVerbs,
    int MaxOperand, BasicQuestionStructure[] Structures, string? EnglishBaseVerb = null, string? EnglishPastVerb = null)
{
    public AdditionQuestionScale? Scale(CurriculumTier tier) => AdditionQuestionScales.Find(Id, tier);
    public bool Supports(BasicQuestionStructure structure, CurriculumTier tier) => Structures.Contains(structure)
        && (int)tier >= AdditionQuestionCatalogue.MinimumStars(structure)
        && Scale(tier) is { } scale && scale.MaxOperand >= QuizCurriculumLayer.GetMinimumPrimaryOperandValue(tier);
}

/// <summary>Independent one-step relations, story settings, actor roles and realistic quantity domains.</summary>
public static class AdditionQuestionCatalogue
{
    public const int Version = 3;
    public static IReadOnlyList<AdditionScene> Scenes { get; } = QuizContentCatalog.LoadList<AdditionScene>("AdditionScenes");

    public static IReadOnlyList<QuestionUnit> ExtraUnits { get; } = Array.AsReadOnly<QuestionUnit>([.. QuizContentCatalog.LoadList<QuestionUnit>("AdditionExtraUnits"), .. BreadQuestionCatalogue.Types.Select(t => t.Unit), .. FoodThemeCatalogue.AdditionalUnits]);
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
        var scale = scene?.Scale(c.Tier);
        return c.Version == Version && c.Operation == ArithmeticOperation.Add && scene is not null
            && scale is not null
            && scene.TopicId == c.TopicId && scene.Supports(c.Structure, c.Tier)
            && c.Left >= QuizCurriculumLayer.GetMinimumPrimaryOperandValue(c.Tier)
            && c.Left <= scale.MaxOperand && c.Right <= scale.MaxOperand
            && (long)c.Left + c.Right <= scale.MaxCombinedQuantity
            && scale.MatchesActor(c.Language, c.Subject) && scale.MatchesActor(c.Language, c.OtherSubject)
            && scale.UnitIds.Any(id => QuestionUnits.Find(id) is { } u && u.Item(c.Language) == c.Unit && u.GroupFor(c) == c.GroupUnit)
            && (c.PartA, c.PartB) == scale.Parts(c.Language);
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
        var scale = choice.Scene.Scale(tier)!;
        // Choose a viable domain first. Never reroll a high-star primary into a
        // lower digit bucket to make an incompatible small scene fit.
        var (a, b) = Numbers(tier, scale, random);
        string name = Actor(scale, language, random), other = name;
        for (int i = 0; i < 32 && other == name; i++) other = Actor(scale, language, random);
        if (other == name) other = scale.Actor(language, language == AppLanguage.Vietnamese
            ? name == scale.Actor(language, "An") ? "Bình" : "An"
            : name == scale.Actor(language, "James") ? "Mary" : "James");
        var units = BreadQuestionCatalogue.GenerationUnits(scale.UnitIds);
        var unit = QuestionUnits.Find(units[random.Next(units.Length)])!;
        var parts = scale.Parts(language);
        return BasicQuestionTemplates.ApplyUnit(new(Version, ArithmeticOperation.Add, tier, language, a, b,
            name, unit.Item(language), unit.Group(language), choice.Structure, other,
            choice.Scene.TopicId, choice.Scene.Id, parts.A, parts.B), unit);
    }

    public static BasicQuestionContract Refresh(BasicQuestionContract stored, Random? random = null)
        => BasicQuestionTemplates.ApplyUnit(Create(stored.Tier, stored.Language, random, stored.SceneId, stored.Structure), QuestionUnits.Find(stored)!);

    private static (int A, int B) Numbers(CurriculumTier tier, AdditionQuestionScale scale, Random random)
    {
        int primaryCap = Math.Min(scale.MaxOperand, scale.MaxCombinedQuantity - 1);
        int desiredCarries = (int)tier >= 3 && random.Next(3) != 0 ? (int)tier - 2 : 0;
        (int A, int B) best = default;
        int bestCarries = -1;
        for (int attempt = 0; attempt < 48; attempt++)
        {
            int a = QuizCurriculumLayer.NextPrimaryOperand(random, tier, maximumOverride: primaryCap);
            int b = QuizCurriculumLayer.NextSecondaryOperand(random, tier,
                maximumOverride: Math.Min(scale.MaxOperand, scale.MaxCombinedQuantity - a));
            int carries = CountCarries(a, b);
            if (carries > bestCarries) { best = (a, b); bestCarries = carries; }
            if (carries >= desiredCarries) return (a, b);
        }
        return best;
    }

    internal static int CountCarries(int a, int b)
    {
        int carry = 0, count = 0;
        while (a > 0 || b > 0)
        {
            carry = (a % 10 + b % 10 + carry) / 10;
            count += carry; a /= 10; b /= 10;
        }
        return count;
    }

    private static string Actor(AdditionQuestionScale scale, AppLanguage language, Random random)
    {
        bool vi = language == AppLanguage.Vietnamese;
        var pool = random.Next(2) == 0 ? QuestionNames.VietnameseMale : QuestionNames.VietnameseFemale;
        string person = vi ? pool[random.Next(pool.Count)] : QuestionNames.GivenName(QuestionNames.Create(language, random));
        return scale.Actor(language, scale.ActorPattern(language) == "{person}"
            ? QuestionNames.Create(language, random) : person);
    }

    public static BasicQuestionDraft Example(BasicQuestionContract c)
    {
        var scene = Find(c.SceneId)!;
        var scale = scene.Scale(c.Tier)!;
        bool vi = c.Language == AppLanguage.Vietnamese;
        string verb = vi ? scene.VietnameseAction : scene.EnglishAction;
        string baseVerb = scene.EnglishBaseVerb ?? (scene.Id switch { "craft" => "make", "notebook-production" => "produce", "harvest" => "harvest", "sports" => "score",
            "recycling" => "collect", "donations" => "donate", "school-supplies" => "contribute", _ => "have" });
        string pastVerb = scene.EnglishPastVerb ?? (scene.Id switch {
            "sports" => "scored", "harvest" => "harvested", "notebook-production" => "produced", _ => "made" });
        string periods = scale.VietnameseSpan;
        string loss = scene.Id is "shop-stock" or "book-distribution" ? "bán" : "cho đi";
        string amount = scene.Kind == AdditionSceneKind.Contributions ? $"{verb}" : "có";
        string amountEn = scene.Kind == AdditionSceneKind.Contributions ? baseVerb : "have";
        var clauses = c.Structure switch
        {
            BasicQuestionStructure.Increase when scene.Kind == AdditionSceneKind.Arrivals => vi
                ? ("Ở {name} có {a} {unit}.", $"Có {{b}} {{unit}} khác {verb} {{name}}.", "Hỏi ở {name} có tất cả bao nhiêu {unit}?", "Tổng số {unit} ở {name} là:")
                : ("There are {a} {unit} at {name}.", scene.Id == "birds-arrive" ? "Another {b} {unit} fly into {name}." : "Another {b} {unit} arrive to join {name}.", "How many {unit} are at {name} altogether?", "The total number of {unit} at {name} is:"),
            BasicQuestionStructure.Increase => (QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.001"), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.002"), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.003"), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.004")),
            BasicQuestionStructure.Combine when scene.Kind == AdditionSceneKind.Periods => (QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.029", ("verb", $"{verb}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.030", ("verb", $"{verb}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.031", ("periods", $"{periods}"), ("verb", $"{verb}"), ("baseVerb", $"{baseVerb}"), ("scale_EnglishSpan", $"{scale.EnglishSpan}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.032", ("verb", $"{verb}"), ("periods", $"{periods}"), ("pastVerb", $"{pastVerb}"), ("scale_EnglishSpan", $"{scale.EnglishSpan}"))),
            BasicQuestionStructure.Combine when scene.Kind == AdditionSceneKind.Parts => (QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.033"), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.034"), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.035", ("scale_VietnameseSpan", $"{scale.VietnameseSpan}"), ("scale_EnglishSpan", $"{scale.EnglishSpan}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.036", ("scale_VietnameseSpan", $"{scale.VietnameseSpan}"), ("scale_EnglishSpan", $"{scale.EnglishSpan}"))),
            BasicQuestionStructure.Combine => (QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.037", ("verb", $"{verb}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.038", ("verb", $"{verb}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.039", ("amount", $"{amount}"), ("amountEn", $"{amountEn}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.040")),
            BasicQuestionStructure.RecoverInitial => (QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.041"), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.042", ("loss", $"{loss}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.043"), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.044")),
            BasicQuestionStructure.AddComparisonMore => (QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.045", ("verb", $"{verb}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.046", ("amount", $"{amount}"), ("verb", $"{verb}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.047", ("amount", $"{amount}"), ("amountEn", $"{amountEn}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.048", ("amount", $"{amount}"))),
            _ => (QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.049", ("verb", $"{verb}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.050", ("amount", $"{amount}"), ("verb", $"{verb}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.051", ("amount", $"{amount}"), ("amountEn", $"{amountEn}")), QuizContentCatalog.Text(c.Language, "AdditionQuestionCatalogue.Example.052", ("amount", $"{amount}")))
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
            .ThenBy(p => history.Count(h => h.Scene == p.Scene.Id))
            .ThenBy(p => history.Count(h => h.Topic == p.Scene.TopicId))
            .ThenBy(p => history.Count > 0 && history[^1].Scene == p.Scene.Id).ToArray();
        var selected = candidates[0];
        history.Add((selected.Scene.Id, selected.Scene.TopicId, selected.Structure));
        if (history.Count > 64) history.RemoveAt(0);
        return AdditionQuestionCatalogue.Create(tier, language, _random, selected.Scene.Id, selected.Structure);
    }
}
