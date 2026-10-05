using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract? CreateDifficultyContract(QuizProblemKind kind, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier) => kind switch
    {
        QuizProblemKind.TwoNumbers => CreateTwoNumberDifficulty(type, language, tier),
        QuizProblemKind.Data => CreateDataDifficulty(type, language, tier),
        QuizProblemKind.Time => CreateTimeDifficulty(type, language, tier),
        QuizProblemKind.Measurement => CreateMeasurementDifficulty(type, language, tier),
        QuizProblemKind.Probability => CreateProbabilityDifficulty(type, language, tier),
        QuizProblemKind.FractionSkills => CreateFractionDifficulty(type, language, tier),
        _ => null
    };

    private sealed class DifficultyBuilder(QuizProblemKind kind, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier)
    {
        internal readonly List<ElementaryGivenValue> Givens = [];
        internal readonly List<ElementaryInferenceStep> Steps = [];
        internal readonly List<ElementaryAnswer> Answers = [];
        internal readonly HashSet<string> Constants = ["0", "1"];
        internal readonly HashSet<string> Units = [];
        internal bool RequiresSolution = true;
        internal string? Explanation;
        internal string L(string vi, string en) => language == AppLanguage.Vietnamese ? vi : en;
        internal string Given(string role, decimal value, string unit = "")
        {
            string text = value.ToString("0.################", CultureInfo.InvariantCulture);
            Givens.Add(new(role, text, unit));
            if (unit.Length > 0) Units.Add(unit);
            return text;
        }
        internal void Constant(params int[] values)
        { foreach (int value in values) Constants.Add(value.ToString(CultureInfo.InvariantCulture)); }
        private ReducedFraction Evaluate(string expression)
        {
            if (!EssayCalculationEvaluator.TryEvaluate(expression, out var result, out _, kind == QuizProblemKind.Measurement))
                throw new InvalidOperationException("Invalid elementary difficulty expression: " + expression);
            return new(result.Numerator, result.Denominator);
        }
        private string Display(ReducedFraction value) => kind == QuizProblemKind.Measurement
            ? ((decimal)value.Numerator / (decimal)value.Denominator).ToString("0.################", CultureInfo.InvariantCulture)
            : value.ToString();
        internal void Step(string label, string expression, string unit = "")
        {
            var value = Evaluate(expression);
            Steps.Add(new(label, expression, value, unit, Display(value)));
            if (unit.Length > 0) Units.Add(unit);
        }
        internal void Answer(string label, string expression, string unit = "", bool reduced = false,
            int? denominator = null, bool mixed = false)
        {
            var value = Evaluate(expression);
            var answer = new ElementaryAnswer(label, value, unit, expression, RequireReduced: reduced,
                RequiredDenominator: denominator, RequireMixedNumber: mixed,
                DisplayValue: kind == QuizProblemKind.Measurement ? Display(value) : null);
            if (mixed) answer = answer with { Text = Mixed(value) };
            if (denominator.HasValue) answer = answer with { Text = $"{value.Numerator * denominator.Value / value.Denominator}/{denominator}" };
            Answers.Add(answer);
            if (expression.Any(character => "+-*/".Contains(character)))
                Steps.Add(new(label, expression, value, unit, answer.DisplayValue ?? answer.Text ?? value.ToString()));
            if (unit.Length > 0) Units.Add(unit);
        }
        internal void TextAnswer(string label, string text, params string[] aliases)
        { Answers.Add(new(label, new(0, 1), "", "", text, aliases)); RequiresSolution = false; }
        internal ElementaryQuizContract Build(string scenario, string problem, QuizVisualData? visual = null,
            ProbabilityQuizScenario? probability = null, QuizDiagram? diagram = null) =>
            new(kind, type, language, problem, "", Givens.Select(given => given.Value).ToArray(),
                Constants.ToArray(), Answers.ToArray(), RequiresSolution, visual)
            {
                ProbabilityScenario = probability,
                Reasoning = new(tier, scenario, Givens.ToArray(), Steps.ToArray(), Units.ToArray(), diagram, Explanation)
            };
    }

    private ElementaryQuizContract CreateTwoNumberDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.TwoNumbers, type, language, tier);
        int level = (int)tier, part = _random.Next(4, 15), smallParts = level <= 2 ? 1 : _random.Next(2, 5);
        int largeParts = smallParts + _random.Next(1, 4), small = part * smallParts, large = part * largeParts;
        int total = small + large, difference = large - small;
        string quantity, differenceExpression = "", ratioExpression = "", text, scenario;
        string smallLabel = t.L("Số bé", "Smaller number"), largeLabel = t.L("Số lớn", "Larger number");
        t.Constant(2);
        bool sum = type != ElementaryQuizType.DifferenceRatio;
        int given = sum ? total : difference;
        string quantityName = t.L(sum ? "tổng" : "hiệu số lớn và số bé", sum ? "sum" : "difference (larger minus smaller)");
        string sentenceQuantityName = char.ToUpperInvariant(quantityName[0]) + quantityName[1..];
        if (level == 1)
        {
            quantity = t.Given("quantity", given);
            text = t.L($"Hai số có {quantityName} là {quantity}. ", $"Two numbers have a {quantityName} of {quantity}. ");
            scenario = "direct";
        }
        else if (level == 2)
        {
            string twice = t.Given("double-quantity", 2 * given);
            quantity = $"({twice}/2)";
            text = t.L($"Hai lần {quantityName} của hai số bằng {twice}. ", $"Twice the {quantityName} of the two numbers equals {twice}. ");
            t.Step(t.L("Dữ kiện cần dùng", "Required quantity"), quantity);
            scenario = "infer-half-quantity";
        }
        else if (level == 3)
        {
            int x = given / 2;
            string first = t.Given("quantity-first", x), second = t.Given("quantity-second", given - x);
            quantity = $"({first}+{second})";
            text = t.L($"{sentenceQuantityName} của hai số bằng tổng của {first} và {second}. ",
                $"The {quantityName} of the two numbers equals {first} plus {second}. ");
            t.Step(t.L("Dữ kiện cần dùng", "Required quantity"), quantity);
            scenario = "infer-quantity";
        }
        else
        {
            int increase = _random.Next(3, 12), reduction = level == 5 ? _random.Next(1, 6) : 0;
            string added = t.Given("added", increase);
            string after = t.Given("after", given + (sum ? 2 : 1) * increase - reduction);
            string restored = after;
            text = t.L(sum ? $"Tăng mỗi số thêm {added}. "
                    : $"Tăng số lớn thêm {added} và giữ nguyên số bé. ",
                sum ? $"Increase each number by {added}. " : $"Increase only the larger number by {added}. ");
            if (level == 5)
            {
                string removed = t.Given("previous-reduction", reduction);
                text += t.L(sum ? $"Sau đó giảm số lớn đi {removed}. " : $"Sau đó giảm số lớn đi {removed}. ",
                    $"Then decrease the larger number by {removed}. ");
                restored = $"({after}+{removed})";
                t.Step(t.L("Khôi phục trước lần giảm", "Undo the decrease"), restored);
            }
            text += t.L($"{sentenceQuantityName} cuối cùng là {after}. ", $"The final {quantityName} is {after}. ");
            quantity = sum ? $"({restored}-2*{added})" : $"({restored}-{added})";
            t.Step(t.L("Dữ kiện ban đầu", "Original quantity"), quantity);
            scenario = level == 5 ? "two-changes" : "reverse-change";
        }

        if (type == ElementaryQuizType.SumDifference)
        {
            if (level <= 3)
            {
                differenceExpression = t.Given("difference", difference);
                text += t.L($"Số lớn hơn số bé {differenceExpression}. ", $"The larger exceeds the smaller by {differenceExpression}. ");
            }
            else
            {
                string transfer = t.Given("transfer", _random.Next(1, Math.Max(2, difference / 2)));
                string remaining = t.Given("remaining-difference", difference - 2 * int.Parse(transfer));
                differenceExpression = $"({remaining}+2*{transfer})";
                text += t.L($"Riêng với hai số ban đầu, nếu chuyển {transfer} từ số lớn sang số bé thì số lớn còn hơn số bé {remaining}. ",
                    $"For the original pair, transferring {transfer} from the larger to the smaller leaves a difference of {remaining}. ");
                t.Step(t.L("Hiệu ban đầu", "Original difference"), differenceExpression);
            }
            t.Answer(smallLabel, $"({quantity}-{differenceExpression})/2");
            t.Answer(largeLabel, $"({quantity}+{differenceExpression})/2");
        }
        else
        {
            string r1 = t.Given("ratio-small", smallParts), r2 = t.Given("ratio-large", largeParts);
            if (level < 5)
            {
                text += t.L($"Tỉ số số bé và số lớn là {r1}/{r2}. ", $"The smaller-to-larger ratio is {r1}/{r2}. ");
                ratioExpression = sum ? $"({r1}+{r2})" : $"({r2}-{r1})";
            }
            else
            {
                t.Givens.RemoveAll(fact => fact.Role == "ratio-large");
                string delta = t.Given("ratio-extra", largeParts - smallParts);
                text += t.L($"Cứ {r1} phần bằng nhau của số bé thì số lớn có nhiều hơn {delta} phần cùng cỡ. ",
                    $"For {r1} equal parts in the smaller number, the larger has {delta} more parts of the same size. ");
                r2 = $"({r1}+{delta})";
                t.Step(t.L("Số phần của số lớn", "Parts in the larger number"), r2, t.L("phần", "parts"));
                ratioExpression = sum ? $"({r1}+{r2})" : $"({r2}-{r1})";
                scenario = "change-and-inferred-ratio";
            }
            t.Step(t.L("Giá trị một phần", "Value of one part"), $"{quantity}/{ratioExpression}");
            t.Answer(smallLabel, $"{quantity}/{ratioExpression}*{r1}");
            t.Answer(largeLabel, $"{quantity}/{ratioExpression}*{r2}");
        }
        text += t.L("Tìm hai số ban đầu.", "Find the two original numbers.");
        QuizDiagramRow[] rows;
        if (type != ElementaryQuizType.SumDifference && level < 5)
        {
            QuizDiagramSegment[] Parts(int count) => Enumerable.Range(0, count).Select(_ => new QuizDiagramSegment("?")).ToArray();
            rows = [new(smallLabel, Parts(smallParts)), new(largeLabel, Parts(largeParts))];
        }
        else if (type == ElementaryQuizType.SumDifference)
            rows = [new(smallLabel, [new("?", 2)]), new(largeLabel,
                [new("?", 2), new(level <= 3 ? differenceExpression : "?", Highlight: true)])];
        else rows = [new(smallLabel, [new("?")]), new(largeLabel, [new("?")])];
        var diagram = new QuizDiagram("bars", t.L("Sơ đồ hai số ban đầu (minh họa)", "Original numbers (schematic)"), rows);
        return t.Build(scenario, text, diagram: diagram);
    }
}
