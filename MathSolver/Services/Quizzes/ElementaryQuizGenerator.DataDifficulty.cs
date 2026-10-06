using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateDataDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Data, type, language, tier);
        t.Constant(2);
        int level = (int)tier;
        var contexts = DataChartStoryContextCatalog.GetProfile(language);
        var context = contexts[_random.Next(contexts.Count)];
        string[] labels = context.Labels.ToArray();
        _random.Shuffle(labels);
        bool pie = type == ElementaryQuizType.ReadPieChart;
        int maxFirst = Math.Min(40, (context.Capacity - 10) / 2);
        int[] values = [_random.Next(2, Math.Max(3, maxFirst / 5 + 1)) * 5, _random.Next(2, 7) * 5, 0];
        values[2] = pie ? 100 - values[0] - values[1] : _random.Next(8, 25);
        string unit = pie ? "%" : context.Unit;
        var hidden = new HashSet<int>();
        string kind = type == ElementaryQuizType.ReadTable ? "table" : pie ? "pie" : "bar";
        string name = t.L(kind == "table" ? "Bảng" : kind == "pie" ? "Biểu đồ tròn" : "Biểu đồ cột",
            kind == "table" ? "The table" : kind == "pie" ? "The pie chart" : "The bar chart");
        string introduction = t.L($"{name} thống kê {context.Description}. ", $"{name} shows {context.Description}. ");
        string problem, expression, answerLabel, scenario;
        if (pie)
        {
            int selected = _random.Next(3);
            int[] others = Enumerable.Range(0, 3).Where(index => index != selected).ToArray();
            int total = _random.Next(1, Math.Max(2, Math.Min(40, context.Capacity / 20) + 1)) * 20;
            if (level == 1)
            {
                foreach (int index in Enumerable.Range(0, 3)) t.Given("row-" + index, values[index], "%");
                expression = values[selected].ToString();
                problem = introduction + t.L($"Mục “{labels[selected]}” chiếm bao nhiêu phần trăm?",
                    $"What percentage belongs to “{labels[selected]}”?");
                answerLabel = t.L($"Tỉ lệ “{labels[selected]}”", $"Percentage for “{labels[selected]}”");
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
                    t.Step(t.L("Tỉ lệ mục còn thiếu", "Missing percentage"), targetPercentage, "%");
                }
                else foreach (int index in Enumerable.Range(0, 3)) t.Given("row-" + index, values[index], "%");
                string totalExpression;
                if (level <= 3)
                {
                    totalExpression = t.Given("total", total, context.Unit);
                    problem = introduction + t.L($"Tổng số lượng là {totalExpression} {context.Unit}. ", $"The total quantity is {totalExpression} {context.Unit}. ");
                }
                else if (level == 4)
                {
                    string remaining = t.Given("other-count", (100 - values[selected]) * total / 100, context.Unit);
                    totalExpression = $"{remaining}*100/(100-{targetPercentage})";
                    problem = introduction + t.L($"Hai mục ngoài “{labels[selected]}” có tổng cộng {remaining} {context.Unit}. ",
                        $"The two categories other than “{labels[selected]}” contain {remaining} {context.Unit} in total. ");
                    t.Step(t.L("Tổng số lượng", "Total quantity"), totalExpression, context.Unit);
                }
                else
                {
                    string firstCount = t.Given("count-" + others[0], values[others[0]] * total / 100, context.Unit);
                    string secondCount = t.Given("count-" + others[1], values[others[1]] * total / 100, context.Unit);
                    totalExpression = $"({firstCount}+{secondCount})*100/({values[others[0]]}+{values[others[1]]})";
                    problem = introduction + t.L($"Mục “{labels[others[0]]}” có {firstCount} {context.Unit}; mục “{labels[others[1]]}” có {secondCount} {context.Unit}. ",
                        $"Category “{labels[others[0]]}” contains {firstCount} {context.Unit}; “{labels[others[1]]}” contains {secondCount} {context.Unit}. ");
                    t.Step(t.L("Tổng lượng hai mục đã biết", "Total of the known categories"), $"{firstCount}+{secondCount}", context.Unit);
                    t.Step(t.L("Tổng số lượng", "Total quantity"), totalExpression, context.Unit);
                }
                expression = $"({totalExpression})*{targetPercentage}/100";
                answerLabel = t.L($"Số lượng “{labels[selected]}”", $"Quantity for “{labels[selected]}”");
                problem += t.L($"Mục “{labels[selected]}” có bao nhiêu {context.Unit}?", $"How many {context.Unit} belong to “{labels[selected]}”? ");
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
                introduction += level == 2 ? t.L("Có một thay đổi sau khi ghi số liệu. ", "A change occurs after recording the data. ") : "";
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
                    introduction += t.L($"Mục “{labels[2]}” nhiều hơn “{labels[0]}” {extra} {unit}. ",
                        $"Category “{labels[2]}” exceeds “{labels[0]}” by {extra} {unit}. ");
                }
                else
                {
                    string factor = t.Given("factor", 2);
                    values[2] = 2 * values[0];
                    expressions[2] = $"({expressions[0]}*{factor})";
                    introduction += t.L($"Mục “{labels[2]}” gấp {factor} lần “{labels[0]}”. ",
                        $"Category “{labels[2]}” is {factor} times “{labels[0]}”. ");
                    if (level == 5)
                    {
                        hidden.Add(1); t.Givens.RemoveAll(fact => fact.Role == "row-1");
                        string extra = t.Given("extra", _random.Next(3, 10), unit);
                        values[1] = values[2] + int.Parse(extra);
                        expressions[1] = $"({expressions[2]}+{extra})";
                        introduction += t.L($"Mục “{labels[1]}” nhiều hơn “{labels[2]}” {extra} {unit}. ",
                            $"Category “{labels[1]}” exceeds “{labels[2]}” by {extra} {unit}. ");
                    }
                }
                t.Step(t.L($"Số liệu “{labels[2]}”", $"Value for “{labels[2]}”"), expressions[2], unit);
                if (level == 5) t.Step(t.L($"Số liệu “{labels[1]}”", $"Value for “{labels[1]}”"), expressions[1], unit);
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
                    introduction += t.L($"Tổng ba mục là {total} {unit}. ", $"The three categories total {total} {unit}. ");
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
                        introduction += t.L($"Tổng ba mục bằng tổng hai lô {batchA} và {batchB} {unit}. ",
                            $"The three-category total equals the sum of two batches containing {batchA} and {batchB} {unit}. ");
                        t.Step(t.L("Tổng ba mục", "Three-category total"), total, unit);
                    }
                    string remaining = $"({total}-{expressions[0]})";
                    t.Step(t.L("Tổng hai mục chưa biết", "Total of the unknown categories"), remaining, unit);
                    if (level == 4)
                    {
                        string r1 = t.Given("ratio-first", 2), r2 = t.Given("ratio-second", 3);
                        expressions[1] = $"({remaining}/({r1}+{r2})*{r1})";
                        expressions[2] = $"({remaining}/({r1}+{r2})*{r2})";
                        introduction += t.L($"Tổng ba mục là {total} {unit}; tỉ số “{labels[1]}” và “{labels[2]}” là {r1}/{r2}. ",
                            $"The three-category total is {total} {unit}; the ratio of “{labels[1]}” to “{labels[2]}” is {r1}/{r2}. ");
                    }
                    else
                    {
                        string difference = t.Given("difference", values[2] - values[1], unit);
                        expressions[1] = $"(({remaining}-{difference})/2)";
                        expressions[2] = $"(({remaining}+{difference})/2)";
                        introduction += t.L($"Mục “{labels[2]}” nhiều hơn “{labels[1]}” {difference} {unit}. ",
                            $"Category “{labels[2]}” exceeds “{labels[1]}” by {difference} {unit}. ");
                    }
                }
                scenario = "missing-category-" + level;
            }
            if (type == ElementaryQuizType.ChartTotal)
            {
                expression = string.Join("+", expressions);
                answerLabel = t.L($"Tổng {context.QuantityName}", $"Total {context.QuantityName}");
                problem = introduction + t.L($"Tổng ba mục là bao nhiêu {unit}?", $"What is the three-category total in {unit}?");
            }
            else if (type == ElementaryQuizType.ChartDifference)
            {
                // At five stars, comparing the two hidden categories would merely
                // repeat the supplied difference. Compare across the linked relations instead.
                int firstRow = level == 5 ? 0 : 1, secondRow = level == 5 ? 1 : 2;
                int high = values[firstRow] > values[secondRow] ? firstRow : secondRow;
                int low = high == firstRow ? secondRow : firstRow;
                expression = $"({expressions[high]})-({expressions[low]})";
                answerLabel = t.L("Chênh lệch hai mục", "Difference between categories");
                problem = introduction + t.L($"Mục “{labels[firstRow]}” và “{labels[secondRow]}” chênh lệch bao nhiêu {unit}?",
                    $"What is the difference between “{labels[firstRow]}” and “{labels[secondRow]}” in {unit}?");
            }
            else
            {
                int selected = level >= 3 ? 2 : _random.Next(3);
                expression = expressions[selected];
                answerLabel = t.L($"Số liệu “{labels[selected]}”", $"Value for “{labels[selected]}”");
                problem = introduction + t.L($"Mục “{labels[selected]}” có bao nhiêu {unit}?", $"How many {unit} are in “{labels[selected]}”? ");
                t.RequiresSolution = level >= 2;
            }
            if (level == 2)
            {
                string added = t.Given("added-after-recording", _random.Next(2, 9), unit);
                expression = $"({expression})+{added}";
                problem += t.L($" Cộng thêm {added} {unit} vào lượng vừa hỏi; tính kết quả sau khi thêm.",
                    $" Add {added} {unit} to the requested quantity and report the result after this addition.");
            }
        }
        t.Answer(answerLabel, expression, unit);
        var visual = new QuizVisualData(kind, labels, values.Select(value => (decimal)value).ToArray(), pie ? "%" : context.Unit,
            ScenarioId: context.Description, HiddenValueIndices: hidden);
        return t.Build(scenario, problem, visual) with { StoryContextId = context.ContextId };
    }
}
