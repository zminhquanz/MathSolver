using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    internal const string AppliedNarrativeList = "Narrative.Applied.Phrasings";
    internal static readonly ElementaryQuizType[] AppliedNarrativeTypes =
    [ElementaryQuizType.AreaConversion, ElementaryQuizType.VolumeConversion, ElementaryQuizType.MixedLength,
        ElementaryQuizType.CompositeArea, ElementaryQuizType.RectangleSide, ElementaryQuizType.Calendar,
        ElementaryQuizType.ReadClock, ElementaryQuizType.DecimalRound, ElementaryQuizType.DecimalCompare];
    internal static readonly ElementaryQuizType[] VisualBankTypes =
        [.. SpatialSkillTypes, ElementaryQuizType.RectangleSide, ElementaryQuizType.CompositeArea];
    public static bool IsAppliedNarrative(ElementaryQuizType type) => AppliedNarrativeTypes.Contains(type);
    private bool _appliedNarrative;
    private sealed record AppliedTextBinding(string SourceId, string Id);

    private string AppliedTextId(string sourceId) => !_appliedNarrative ? sourceId
        : QuizContentCatalog.LoadList<AppliedTextBinding>("AppliedNarrative.TextBindings")
            .FirstOrDefault(row => row.SourceId == sourceId)?.Id ?? sourceId;
    private string AppliedText(AppLanguage language, string id, params (string Key, string Value)[] values)
        => QuizContentCatalog.Text(language, AppliedTextId(id), values);
    private string AppliedTextForUnit(AppLanguage language, string unit, string id, params (string Key, string Value)[] values)
        => QuizContentCatalog.TextForUnit(language, unit, AppliedTextId(id), values);

    /// <summary>Reviewed contextual prose over the existing C# quantities, diagrams and calculations.</summary>
    public ArithmeticQuizQuestion GenerateAppliedNarrative(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, string contextId = "")
    {
        if (!IsAppliedNarrative(type) || !Enum.IsDefined(tier) || !Enum.IsDefined(language)
            || contextId.Length > 0 && contextId != "applied-" + type)
            throw new ArgumentException("InvalidAppliedNarrativeProfile");
        bool previous = _appliedNarrative;
        _appliedNarrative = true;
        try
        {
            ElementaryQuizContract contract;
            if (type is ElementaryQuizType.RectangleSide or ElementaryQuizType.CompositeArea)
            {
                int level = (int)tier;
                int a = _random.Next(2, 5 + level * 4), b = _random.Next(2, 5 + level * 3);
                var task = type == ElementaryQuizType.RectangleSide ? CreateMissingSideTask(language, a, b, level)
                    : CreateCompositeAreaTask(language, a, b, _random.Next(2, 5 + level * 3), level);
                var builder = new DifficultyBuilder(QuizProblemKind.VisualGeometry, type, language, tier)
                    { RequiresSolution = task.RequiresSolution };
                for (int i = 0; i < task.Facts.Length; i++)
                    builder.Given("shape-given-" + i, decimal.Parse(task.Facts[i], System.Globalization.CultureInfo.InvariantCulture));
                foreach (string constant in task.Constants) builder.Constants.Add(constant);
                builder.Answer(task.AnswerLabel, task.Expression, task.Unit);
                contract = builder.Build(task.Visual.ScenarioId!, task.ProblemText, task.Visual);
            }
            else if (type is ElementaryQuizType.Calendar or ElementaryQuizType.ReadClock)
                contract = CreateTimeDifficulty(type, language, tier);
            else if (type is ElementaryQuizType.DecimalRound or ElementaryQuizType.DecimalCompare)
            {
                contract = CreateDecimalDifficulty(type, language, tier);
                if (type == ElementaryQuizType.DecimalRound)
                    contract = contract with { Answers = contract.Answers.Select(a => a with { Unit = "m" }).ToArray() };
            }
            else contract = CreateMeasurementDifficulty(type, language, tier);
            contract = contract with { StoryContextId = "applied-" + type };
            return CompleteQuestion(mode, contract, [], contract.ChoiceTexts?.ToArray());
        }
        finally { _appliedNarrative = previous; }
    }
}
