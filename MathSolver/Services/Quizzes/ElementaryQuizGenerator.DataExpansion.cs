using MathSolver.Models;
using System.Collections.Frozen;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    internal static bool IsAdditionalDataType(ElementaryQuizType type) => type is
        ElementaryQuizType.ReadPictograph or ElementaryQuizType.SortData or ElementaryQuizType.CompleteBarChart;

    private ElementaryQuizContract CreateAdditionalDataChart(ElementaryQuizType type, AppLanguage language,
        CurriculumTier tier, int seed, string? contextId, DataChartProfile? profile, DataChartQuestionKind? requestedKind)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Data, type, language, tier);
        int level = (int)tier;
        var contexts = DataChartStoryContextCatalog.GetProfile(language);
        int contextIndex = _random.Next(contexts.Count); // Consume the same draw on seeded replay.
        var context = contextId is null ? contexts[contextIndex]
            : contexts.SingleOrDefault(c => c.ContextId == contextId) ?? throw new ArgumentException("InvalidDataChartContext");
        string[] ids = context.CategoryIds!.ToArray();
        _random.Shuffle(ids);
        if (profile is not null) ids = profile.CategoryIds.ToArray();
        string[] labels = ids.Select(id => context.Labels[context.CategoryIds!.ToList().IndexOf(id)]).ToArray();
        string Text(string id, params (string, string)[] args) => QuizContentCatalog.Text(language,
            "ElementaryQuizGenerator.DataDifficulty.Expansion." + id, args);
        string problem = Text("Intro", ("context", context.Description));
        var hidden = new HashSet<int>();
        string[] targets;
        DataChartQuestionKind targetKind;
        decimal? key = null;
        var observations = new List<DataChartObservationBatch>();
        int[] values;
        if (type == ElementaryQuizType.ReadPictograph)
        {
            int multiplier = Math.Min(new[] { 1, 2, 5, 10, 20 }[level - 1], Math.Max(1, context.Capacity / 18));
            key = multiplier;
            int[] icons = [_random.Next(2, 7), _random.Next(2, 7), _random.Next(1, 4)];
            values = icons.Select(n => n * multiplier).ToArray();
            string[] counts = icons.Select((n, i) => t.Given("icons-" + i, n)).ToArray();
            string legend = t.Given("key", multiplier, context.Unit);
            problem += Text("Legend", ("key", legend), ("unit", context.Unit));
            string expression;
            if (level <= 2)
            {
                int selected = profile is null ? _random.Next(3) : Array.IndexOf(ids, profile.TargetCategoryIds[0]);
                targets = [ids[selected]]; targetKind = DataChartQuestionKind.CategoryValue;
                expression = $"{counts[selected]}*{legend}";
                problem += Text("Category", ("label", labels[selected]), ("unit", context.Unit));
            }
            else if (level <= 4)
            {
                int size = level == 3 ? 2 : 3;
                targets = ids.Take(size).ToArray(); targetKind = DataChartQuestionKind.Total;
                string total = "(" + string.Join("+", counts.Take(size)) + ")";
                t.Step(Text("IconTotal"), total);
                expression = total + "*" + legend;
                problem += Text("Total", ("labels", string.Join(language == AppLanguage.Vietnamese ? " và " : " and ", labels.Take(size))), ("unit", context.Unit));
            }
            else
            {
                targets = ids.ToArray(); targetKind = DataChartQuestionKind.CombinedDifference;
                string difference = $"({counts[0]}+{counts[1]}-{counts[2]})";
                t.Step(Text("IconDifference"), difference);
                expression = difference + "*" + legend;
                problem += Text("CombinedDifference", ("first", labels[0]), ("second", labels[1]), ("third", labels[2]), ("unit", context.Unit));
            }
            t.Answer(Text("Quantity"), expression, context.Unit);
        }
        else
        {
            // A category occurrence alone counts objects, not kg, litres or minutes.
            // Measurement records must explicitly state what each entry represents.
            if (context.RequiresObservationUnit)
                problem += Text("ObservationMeaning", ("one", t.Given("observation-unit", 1, context.Unit)), ("unit", context.Unit));
            int limit = Math.Min(3 + level, Math.Max(2, context.Capacity / 9));
            int[] first = Enumerable.Range(0, 3).Select(_ => _random.Next(1, limit)).ToArray();
            int[] second = level >= 3 ? Enumerable.Range(0, 3).Select(_ => _random.Next(1, limit)).ToArray() : [0, 0, 0];
            int[] excluded = level >= 4 ? Enumerable.Range(0, 3).Select(i => _random.Next(first[i])).ToArray() : [0, 0, 0];
            // Include at least one excluded record without excluding every record of a category.
            if (level >= 4 && excluded.Sum() == 0) { first[0]++; excluded[0] = 1; }
            values = Enumerable.Range(0, 3).Select(i => first[i] + second[i] - excluded[i]).ToArray();
            string Batch(string role, int[] counts, bool removed, string textId)
            {
                string[] items = Enumerable.Range(0, 3).SelectMany(i => Enumerable.Repeat(ids[i], counts[i])).ToArray();
                _random.Shuffle(items);
                observations.Add(new(role, Array.AsReadOnly(items), removed));
                string display = string.Join(", ", items.Select(id => labels[Array.IndexOf(ids, id)]));
                return Text(textId, ("items", display));
            }
            problem += Batch("observations_first", first, false, level >= 3 ? "FirstBatch" : "Observations");
            if (level >= 3) problem += Batch("observations_second", second, false, "SecondBatch");
            if (level >= 4) problem += Batch("observations_excluded", excluded, true, "Excluded");
            var expressions = new string[3];
            for (int i = 0; i < 3; i++)
            {
                string a = t.Given("first-" + i, first[i], context.Unit);
                string expression = a;
                if (level >= 3) expression = $"({a}+{t.Given("second-" + i, second[i], context.Unit)})";
                if (level >= 4) expression = $"({expression}-{t.Given("excluded-" + i, excluded[i], context.Unit)})";
                expressions[i] = expression;
            }
            if (type == ElementaryQuizType.SortData)
            {
                targets = ids.ToArray(); targetKind = DataChartQuestionKind.CategoryCounts;
                hidden.UnionWith([0, 1, 2]);
                problem += Text("Classify", ("first", labels[0]), ("second", labels[1]), ("third", labels[2]), ("unit", context.Unit));
                for (int i = 0; i < 3; i++)
                {
                    if (level <= 2) t.Step(Text("Count", ("label", labels[i])), expressions[i], context.Unit);
                    t.Answer(labels[i], expressions[i], context.Unit);
                }
            }
            else
            {
                int selected = profile is null ? _random.Next(3) : Array.IndexOf(ids, profile.TargetCategoryIds[0]);
                hidden.Add(selected); targets = [ids[selected]]; targetKind = DataChartQuestionKind.CategoryValue;
                problem += Text("MissingBar", ("label", labels[selected]), ("unit", context.Unit));
                if (level <= 2) t.Step(Text("Count", ("label", labels[selected])), expressions[selected], context.Unit);
                t.Answer(labels[selected], expressions[selected], context.Unit);
            }
            t.RequiresSolution = level >= 3;
        }
        if (requestedKind is not null && requestedKind != targetKind) throw new ArgumentException("InvalidDataChartTarget");
        var bound = new DataChartProfile(context.ContextId, type, tier, language, Array.AsReadOnly(ids),
            Array.AsReadOnly(targets), Array.AsReadOnly(hidden.Order().Select(i => ids[i]).ToArray()), targetKind);
        var visual = new QuizVisualData(type == ElementaryQuizType.ReadPictograph ? "pictograph"
            : type == ElementaryQuizType.SortData ? "table" : "bar", labels, values.Select(n => (decimal)n).ToArray(),
            context.Unit, ScenarioId: context.ContextId, HiddenValueIndices: hidden.ToFrozenSet(), PictographKey: key);
        return t.Build("data-observations-" + type + "-" + level, problem, visual) with {
            StoryContextId = context.ContextId, DataChart = new(bound, seed, Observations: observations.Count == 0 ? null : observations.ToArray()) };
    }
}
