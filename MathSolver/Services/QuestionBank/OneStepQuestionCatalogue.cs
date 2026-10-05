using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>A reviewed prose pattern. Numeric roles and capacity are shared with the AI bank.</summary>
public sealed record OneStepQuestionTemplate(ArithmeticOperation Operation, CurriculumTier Tier,
    string SceneId, string TopicId, BasicQuestionStructure Structure)
{
    public BasicQuestionContract CreateFacts(AppLanguage language, Random random) => Operation == ArithmeticOperation.Add
        ? AdditionQuestionCatalogue.Create(Tier, language, random, SceneId, Structure)
        : ArithmeticQuestionCatalogue.Create(Operation, Tier, language, random, SceneId, Structure);
}

public static class OneStepQuestionCatalogue
{
    // Store reusable patterns rather than fixed questions or preview operands.
    public static IReadOnlyList<OneStepQuestionTemplate> All { get; } = Build().AsReadOnly();

    private static List<OneStepQuestionTemplate> Build()
    {
        var templates = new List<OneStepQuestionTemplate>();
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            templates.AddRange(AdditionQuestionCatalogue.Available(tier).Select(p =>
                new OneStepQuestionTemplate(ArithmeticOperation.Add, tier, p.Scene.Id, p.Scene.TopicId, p.Structure)));
            foreach (var operation in new[] { ArithmeticOperation.Subtract, ArithmeticOperation.Multiply, ArithmeticOperation.Divide })
                templates.AddRange(ArithmeticQuestionCatalogue.Available(operation, tier).Select(p =>
                    new OneStepQuestionTemplate(operation, tier, p.Scene.Id, p.Scene.TopicId, p.Structure)));
        }
        return templates;
    }

    /// <summary>One canonical role example also anchors AI prompts; reviewed variants add wording variety.</summary>
    public static BasicQuestionDraft Draft(BasicQuestionContract facts, int variant = 0)
    {
        if (variant is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(variant));
        var draft = BasicQuestionTemplates.Example(facts);
        if (variant == 0) return draft;
        bool vi = facts.Language == AppLanguage.Vietnamese;
        if (OneStepRelationRules.IsExtended(facts.Structure))
            return draft with { Question = vi ? draft.Question.Replace("Hỏi ", "").Replace("mấy lần", "bao nhiêu lần")
                : draft.Question.Replace(" have", " own") };
        if (vi)
        {
            draft = draft with { Question = draft.Question.Replace("tổng cộng", variant == 1 ? "tất cả" : "cộng lại")
                .Replace("còn lại bao nhiêu", variant == 1 ? "còn bao nhiêu" : "còn lại bao nhiêu") };
            if (facts.Structure == BasicQuestionStructure.EqualGroups)
                draft = draft with { GivenA = variant == 1 ? "Mỗi {group_one} có {a} {unit}," : "Trong mỗi {group_one} có {a} {unit}," };
            if (facts.Structure is BasicQuestionStructure.EqualShare or BasicQuestionStructure.CountGroups)
                draft = draft with { GivenB = draft.GivenB.Replace("xếp đều", "chia đều").Replace("xếp vào", "chia vào") };
            if (facts.Structure == BasicQuestionStructure.Remaining)
            {
                string action = ArithmeticQuestionCatalogue.Removal(facts).Vi;
                string alternative = action switch { "bán" => "đã bán", "phát" => "đã phát", "cho mượn" => "đã cho mượn",
                    "sử dụng" => "đã sử dụng", "cho đi" => "đã cho đi", _ => action };
                draft = draft with { GivenB = draft.GivenB.Replace(action, alternative) };
            }
        }
        else
        {
            draft = draft with { Question = draft.Question.Replace("altogether", variant == 1 ? "in total" : "in all")
                .Replace("in total", variant == 2 ? "altogether" : "in total") };
            if (facts.Structure == BasicQuestionStructure.EqualGroups)
                draft = draft with { GivenA = variant == 1 ? "Each {group_one} contains {a} {unit}," : "Each {group_one} has {a} {unit}," };
            if (facts.Structure is BasicQuestionStructure.EqualShare or BasicQuestionStructure.CountGroups)
                draft = draft with { GivenB = draft.GivenB.Replace("packs", "divides") };
        }
        return draft;
    }
}

/// <summary>Instance-owned, bounded history; selecting a C# question never needs a model or SQLite.</summary>
public sealed class OneStepQuestionCycle(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly Dictionary<(ArithmeticOperation, CurriculumTier, AppLanguage), List<(string Scene, string Topic, BasicQuestionStructure Structure)>> _history = [];

    public ArithmeticQuizQuestion Next(ArithmeticOperation operation, CurriculumTier tier, AppLanguage language, ArithmeticQuizMode mode)
    {
        var key = (operation, tier, language);
        if (!_history.TryGetValue(key, out var history)) _history[key] = history = [];
        var candidates = OneStepQuestionCatalogue.All.Where(t => t.Operation == operation && t.Tier == tier).ToArray();
        if (candidates.Length == 0) throw new ArgumentOutOfRangeException(nameof(tier));
        var selected = candidates.OrderBy(_ => _random.Next())
            .OrderBy(t => history.Count(h => h.Structure == t.Structure))
            .ThenBy(t => history.Count > 0 && history[^1].Scene == t.SceneId)
            .ThenBy(t => history.Count(h => h.Topic == t.TopicId))
            .ThenBy(t => history.Count(h => h.Scene == t.SceneId)).First();
        history.Add((selected.SceneId, selected.TopicId, selected.Structure));
        if (history.Count > 64) history.RemoveAt(0);
        var facts = selected.CreateFacts(language, _random);
        var draft = OneStepQuestionCatalogue.Draft(facts, _random.Next(3));
        return facts.ToPracticeQuestion(draft.ToWordProblem(facts), mode, _random);
    }
}
