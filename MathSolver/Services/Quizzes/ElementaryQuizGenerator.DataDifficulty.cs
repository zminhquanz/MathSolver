using MathSolver.Models;
using System.Collections.Frozen;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    public static readonly ElementaryQuizType[] DataChartTypes = [ElementaryQuizType.ReadTable,
        ElementaryQuizType.ReadBarChart, ElementaryQuizType.ReadPieChart, ElementaryQuizType.ChartTotal,
        ElementaryQuizType.ChartDifference];

    public ArithmeticQuizQuestion GenerateDataChart(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, string contextId,
        DataChartQuestionKind? questionKind = null)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            var contract = CreateDataChart(type, language, tier, _random.Next(int.MaxValue), contextId, questionKind: questionKind);
            if (!HasDirectedChartOrder(contract)) continue;
            return CompleteQuestion(mode, contract, [], null);
        }
        throw new InvalidOperationException("DataChartProfileUnavailable");
    }

    public ArithmeticQuizQuestion GenerateDataChart(ArithmeticQuizMode mode, DataChartProfile profile)
    {
        DataChartQuestionValidator.ValidateProfile(profile);
        for (int attempt = 0; attempt < 128; attempt++)
        {
            var contract = CreateDataChart(profile.Type, profile.Language, profile.Tier, _random.Next(int.MaxValue),
                profile.ContextId, profile);
            // A directed question keeps its two subjects in place. Resample numbers
            // if their ordering no longer satisfies the supplied "more than" relation.
            if (!HasDirectedChartOrder(contract)) continue;
            return CompleteQuestion(mode, contract, [], null);
        }
        throw new InvalidOperationException("DataChartProfileUnavailable");
    }

    internal static ElementaryQuizContract RebuildDataChart(DataChartQuestionContract chart) =>
        CreateDataChart(chart.Profile.Type, chart.Profile.Language, chart.Profile.Tier,
            chart.DataSeed, chart.Profile.ContextId, chart.Profile);

    public ArithmeticQuizQuestion RecreateDataChart(ArithmeticQuizMode mode, DataChartQuestionContract chart)
    {
        DataChartQuestionValidator.ValidateProfile(chart.Profile);
        if (chart.Version != 1 || chart.DataSeed < 0) throw new InvalidDataException("Unsupported chart contract.");
        return CompleteQuestion(mode, RebuildDataChart(chart), [], null);
    }

    private static bool HasDirectedChartOrder(ElementaryQuizContract contract)
    {
        var profile = contract.DataChart!.Profile;
        if (profile.QuestionKind != DataChartQuestionKind.MoreThan) return true;
        int first = profile.CategoryIds.ToList().IndexOf(profile.TargetCategoryIds[0]);
        int second = profile.CategoryIds.ToList().IndexOf(profile.TargetCategoryIds[1]);
        return contract.Visual!.Values[first] > contract.Visual.Values[second];
    }

    private ElementaryQuizContract CreateDataDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
        => CreateDataChart(type, language, tier, _random.Next(int.MaxValue));

    private static ElementaryQuizContract CreateDataChart(ElementaryQuizType type, AppLanguage language,
        CurriculumTier tier, int seed, string? contextId = null, DataChartProfile? profile = null,
        DataChartQuestionKind? questionKind = null) => new ElementaryQuizGenerator(new Random(seed))
            .CreateDataChartCore(type, language, tier, seed, contextId, profile, questionKind);

    private ElementaryQuizContract CreateDataChartCore(ElementaryQuizType type, AppLanguage language,
        CurriculumTier tier, int seed, string? contextId, DataChartProfile? profile, DataChartQuestionKind? questionKind)
    {
        if (!DataChartTypes.Contains(type) || !Enum.IsDefined(tier) || seed < 0)
            throw new ArgumentException("InvalidDataChartProfile");
        var t = new DifficultyBuilder(QuizProblemKind.Data, type, language, tier);
        t.Constant(2);
        int level = (int)tier;
        var contexts = DataChartStoryContextCatalog.GetProfile(language);
        int contextIndex = _random.Next(contexts.Count); // Also consumed on replay.
        var context = contextId is null ? contexts[contextIndex]
            : contexts.SingleOrDefault(c => c.ContextId == contextId) ?? throw new ArgumentException("InvalidDataChartContext");
        string[] categoryIds = context.CategoryIds!.ToArray();
        _random.Shuffle(categoryIds);
        if (profile is not null) categoryIds = profile.CategoryIds.ToArray();
        string[] labels = categoryIds.Select(id => context.Labels[context.CategoryIds!.ToList().IndexOf(id)]).ToArray();
        DataChartQuestionKind targetKind = questionKind ?? profile?.QuestionKind ?? (type == ElementaryQuizType.ChartTotal
            ? DataChartQuestionKind.Total : type == ElementaryQuizType.ChartDifference
                ? DataChartQuestionKind.AbsoluteDifference : DataChartQuestionKind.CategoryValue);
        if (type == ElementaryQuizType.ChartDifference ? targetKind is not (DataChartQuestionKind.AbsoluteDifference or DataChartQuestionKind.MoreThan)
            : targetKind != (type == ElementaryQuizType.ChartTotal ? DataChartQuestionKind.Total : DataChartQuestionKind.CategoryValue))
            throw new ArgumentException("InvalidDataChartTarget");
        string[] targetIds = [];
        bool pie = type == ElementaryQuizType.ReadPieChart;
        int maxFirst = Math.Min(40, (context.Capacity - 10) / 2);
        int[] values = [_random.Next(2, Math.Max(3, maxFirst / 5 + 1)) * 5, _random.Next(2, 7) * 5, 0];
        values[2] = pie ? 100 - values[0] - values[1] : _random.Next(8, 25);
        string unit = pie ? "%" : context.Unit;
        var hidden = new HashSet<int>();
        string kind = type == ElementaryQuizType.ReadTable ? "table" : pie ? "pie" : "bar";
        string name = (kind == "table" ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.032") : (kind == "pie" ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.033") : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.034")));
        string introduction = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.001", ("name", $"{name}"), ("context_Description", $"{context.Description}"));
        string problem, expression, answerLabel, scenario;
        if (pie)
        {
            int selected = _random.Next(3);
            if (profile is not null) selected = Array.IndexOf(categoryIds, profile.TargetCategoryIds[0]);
            targetIds = [categoryIds[selected]];
            int[] others = Enumerable.Range(0, 3).Where(index => index != selected).ToArray();
            int total = _random.Next(1, Math.Max(2, Math.Min(40, context.Capacity / 20) + 1)) * 20;
            if (level == 1)
            {
                foreach (int index in Enumerable.Range(0, 3)) t.Given("row-" + index, values[index], "%");
                expression = values[selected].ToString();
                problem = introduction + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.002", ("labels_selected", $"{labels[selected]}"));
                answerLabel = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.003", ("labels_selected", $"{labels[selected]}"));
                scenario = "read-percentage";
                t.RequiresSolution = false;
            }
            else
            {
                t.Constant(100);
                string targetPercentage = values[selected].ToString();
                if (level is 3 or 5)
                {
                    hidden.Add(selected);
                    string first = t.Given("row-" + others[0], values[others[0]], "%");
                    string second = t.Given("row-" + others[1], values[others[1]], "%");
                    targetPercentage = $"(100-{first}-{second})";
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.004"), targetPercentage, "%");
                }
                else foreach (int index in Enumerable.Range(0, 3)) t.Given("row-" + index, values[index], "%");
                string totalExpression;
                if (level <= 3)
                {
                    totalExpression = t.Given("total", total, context.Unit);
                    problem = introduction + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.005", ("totalExpression", $"{totalExpression}"), ("context_Unit", $"{context.Unit}"));
                }
                else if (level == 4)
                {
                    string remaining = t.Given("other-count", (100 - values[selected]) * total / 100, context.Unit);
                    totalExpression = $"{remaining}*100/(100-{targetPercentage})";
                    problem = introduction + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.006", ("labels_selected", $"{labels[selected]}"), ("remaining", $"{remaining}"), ("context_Unit", $"{context.Unit}"));
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.007"), totalExpression, context.Unit);
                }
                else
                {
                    string firstCount = t.Given("count-" + others[0], values[others[0]] * total / 100, context.Unit);
                    string secondCount = t.Given("count-" + others[1], values[others[1]] * total / 100, context.Unit);
                    totalExpression = $"({firstCount}+{secondCount})*100/({values[others[0]]}+{values[others[1]]})";
                    problem = introduction + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.008", ("labels_others_0", $"{labels[others[0]]}"), ("firstCount", $"{firstCount}"), ("context_Unit", $"{context.Unit}"), ("labels_others_1", $"{labels[others[1]]}"), ("secondCount", $"{secondCount}"));
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.009"), $"{firstCount}+{secondCount}", context.Unit);
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.010"), totalExpression, context.Unit);
                }
                expression = $"({totalExpression})*{targetPercentage}/100";
                answerLabel = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.011", ("labels_selected", $"{labels[selected]}"));
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.012", ("labels_selected", $"{labels[selected]}"), ("context_Unit", $"{context.Unit}"));
                scenario = "pie-inference-" + level;
                unit = context.Unit;
            }
        }
        else
        {
            string[] expressions = new string[3];
            if (level <= 2)
            {
                for (int index = 0; index < 3; index++) expressions[index] = t.Given("row-" + index, values[index], unit);
                introduction += level == 2 ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.013") : "";
                scenario = "read-or-calculate-" + level;
            }
            else if (type is ElementaryQuizType.ChartTotal or ElementaryQuizType.ChartDifference)
            {
                hidden.Add(2);
                expressions[0] = t.Given("row-0", values[0], unit);
                expressions[1] = t.Given("row-1", values[1], unit);
                if (level == 3)
                {
                    string extra = t.Given("extra", _random.Next(3, 10), unit);
                    values[2] = values[0] + int.Parse(extra);
                    expressions[2] = $"({expressions[0]}+{extra})";
                    introduction += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.014", ("labels_2", $"{labels[2]}"), ("labels_0", $"{labels[0]}"), ("extra", $"{extra}"), ("unit", $"{unit}"));
                }
                else
                {
                    string factor = t.Given("factor", 2);
                    values[2] = 2 * values[0];
                    expressions[2] = $"({expressions[0]}*{factor})";
                    introduction += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.015", ("labels_2", $"{labels[2]}"), ("factor", $"{factor}"), ("labels_0", $"{labels[0]}"));
                    if (level == 5)
                    {
                        hidden.Add(1); t.Givens.RemoveAll(fact => fact.Role == "row-1");
                        string extra = t.Given("extra", _random.Next(3, 10), unit);
                        values[1] = values[2] + int.Parse(extra);
                        expressions[1] = $"({expressions[2]}+{extra})";
                        introduction += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.016", ("labels_1", $"{labels[1]}"), ("labels_2", $"{labels[2]}"), ("extra", $"{extra}"), ("unit", $"{unit}"));
                    }
                }
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.017", ("labels_2", $"{labels[2]}")), expressions[2], unit);
                if (level == 5) t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.018", ("labels_1", $"{labels[1]}")), expressions[1], unit);
                scenario = "linked-categories-" + level;
            }
            else
            {
                hidden.Add(2);
                expressions[0] = t.Given("row-0", values[0], unit);
                if (level == 3)
                {
                    expressions[1] = t.Given("row-1", values[1], unit);
                    string total = t.Given("total", values.Sum(), unit);
                    expressions[2] = $"({total}-{expressions[0]}-{expressions[1]})";
                    introduction += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.019", ("total", $"{total}"), ("unit", $"{unit}"));
                }
                else
                {
                    hidden.Add(1);
                    int part = _random.Next(4, 12);
                    values[1] = 2 * part; values[2] = 3 * part;
                    string total;
                    if (level == 4) total = t.Given("total", values.Sum(), unit);
                    else
                    {
                        int first = values.Sum() / 2;
                        string batchA = t.Given("batch-first", first, unit), batchB = t.Given("batch-second", values.Sum() - first, unit);
                        total = $"({batchA}+{batchB})";
                        introduction += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.020", ("batchA", $"{batchA}"), ("batchB", $"{batchB}"), ("unit", $"{unit}"));
                        t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.021"), total, unit);
                    }
                    string remaining = $"({total}-{expressions[0]})";
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.022"), remaining, unit);
                    if (level == 4)
                    {
                        string r1 = t.Given("ratio-first", 2), r2 = t.Given("ratio-second", 3);
                        expressions[1] = $"({remaining}/({r1}+{r2})*{r1})";
                        expressions[2] = $"({remaining}/({r1}+{r2})*{r2})";
                        introduction += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.023", ("total", $"{total}"), ("unit", $"{unit}"), ("labels_1", $"{labels[1]}"), ("labels_2", $"{labels[2]}"), ("r1", $"{r1}"), ("r2", $"{r2}"));
                    }
                    else
                    {
                        string difference = t.Given("difference", values[2] - values[1], unit);
                        expressions[1] = $"(({remaining}-{difference})/2)";
                        expressions[2] = $"(({remaining}+{difference})/2)";
                        introduction += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.024", ("labels_2", $"{labels[2]}"), ("labels_1", $"{labels[1]}"), ("difference", $"{difference}"), ("unit", $"{unit}"));
                    }
                }
                scenario = "missing-category-" + level;
            }
            if (type == ElementaryQuizType.ChartTotal)
            {
                targetIds = categoryIds.ToArray();
                expression = string.Join("+", expressions);
                answerLabel = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.025", ("context_QuantityName", $"{context.QuantityName}"));
                problem = introduction + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.026", ("unit", $"{unit}"));
            }
            else if (type == ElementaryQuizType.ChartDifference)
            {
                // At five stars, comparing the two hidden categories would merely
                // repeat the supplied difference. Compare across the linked relations instead.
                int firstRow = level == 5 ? 0 : 1, secondRow = level == 5 ? 1 : 2;
                if (profile is not null)
                {
                    firstRow = Array.IndexOf(categoryIds, profile.TargetCategoryIds[0]);
                    secondRow = Array.IndexOf(categoryIds, profile.TargetCategoryIds[1]);
                }
                int high = values[firstRow] > values[secondRow] ? firstRow : secondRow;
                int low = high == firstRow ? secondRow : firstRow;
                if (targetKind == DataChartQuestionKind.MoreThan)
                {
                    if (profile is null) { firstRow = high; secondRow = low; }
                    high = firstRow; low = secondRow;
                }
                targetIds = [categoryIds[firstRow], categoryIds[secondRow]];
                expression = $"({expressions[high]})-({expressions[low]})";
                answerLabel = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.027");
                problem = introduction + QuizContentCatalog.Text(t.Language,
                    targetKind == DataChartQuestionKind.MoreThan ? "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.035"
                        : "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.028",
                    ("labels_firstRow", labels[firstRow]), ("labels_secondRow", labels[secondRow]), ("unit", unit));
            }
            else
            {
                int selected = level >= 3 ? 2 : _random.Next(3);
                if (profile is not null) selected = Array.IndexOf(categoryIds, profile.TargetCategoryIds[0]);
                targetIds = [categoryIds[selected]];
                expression = expressions[selected];
                answerLabel = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.029", ("labels_selected", $"{labels[selected]}"));
                problem = introduction + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.030", ("labels_selected", $"{labels[selected]}"), ("unit", $"{unit}"));
                t.RequiresSolution = level >= 2;
            }
            if (level == 2)
            {
                string added = t.Given("added-after-recording", _random.Next(2, 9), unit);
                expression = $"({expression})+{added}";
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DataDifficulty.CreateDataDifficulty.031", ("added", $"{added}"), ("unit", $"{unit}"));
            }
        }
        t.Answer(answerLabel, expression, unit);
        var visual = new QuizVisualData(kind, labels, values.Select(value => (decimal)value).ToArray(), pie ? "%" : context.Unit,
            ScenarioId: context.ContextId, HiddenValueIndices: hidden.ToFrozenSet());
        var boundProfile = new DataChartProfile(context.ContextId, type, tier, language,
            Array.AsReadOnly(categoryIds), Array.AsReadOnly(targetIds),
            Array.AsReadOnly(hidden.Order().Select(i => categoryIds[i]).ToArray()), targetKind);
        return t.Build(scenario, problem, visual) with { StoryContextId = context.ContextId,
            DataChart = new(boundProfile, seed) };
    }
}
