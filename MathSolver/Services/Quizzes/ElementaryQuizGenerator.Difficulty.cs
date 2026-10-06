using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract? CreateDifficultyContract(QuizProblemKind kind, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier) => kind switch
    {
        QuizProblemKind.TwoNumbers => CreateTwoNumberDifficulty(type, language, tier),
        QuizProblemKind.Data when type == ElementaryQuizType.ReadPictograph => CreatePictograph(language, tier),
        QuizProblemKind.Data => CreateDataDifficulty(type, language, tier),
        QuizProblemKind.Time => AddTimeStory(CreateTimeDifficulty(type, language, tier)),
        QuizProblemKind.Measurement when type == ElementaryQuizType.MapScale => CreateMapScale(language, tier),
        QuizProblemKind.Measurement => CreateMeasurementDifficulty(type, language, tier),
        QuizProblemKind.Remainder => CreateRemainderStory(type, language, tier),
        QuizProblemKind.Decimal => CreateDecimalDifficulty(type, language, tier),
        QuizProblemKind.MultiStep => CreateMultiStep(type, language, tier),
        QuizProblemKind.VisualGeometry when type == ElementaryQuizType.RecognizeShape => CreateShapeRecognition(language, tier),
        QuizProblemKind.Probability => CreateProbabilityDifficulty(type, language, tier),
        QuizProblemKind.FractionSkills => CreateFractionDifficulty(type, language, tier),
        _ => null
    };

    private sealed class DifficultyBuilder(QuizProblemKind kind, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier)
    {
        internal AppLanguage Language => language;
        internal ElementaryQuizType Type => type;
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
            if (!EssayCalculationEvaluator.TryEvaluate(expression, out var result, out _, kind is QuizProblemKind.Measurement or QuizProblemKind.Decimal))
                throw new InvalidOperationException("Invalid elementary difficulty expression: " + expression);
            return new(result.Numerator, result.Denominator);
        }
        private string Display(ReducedFraction value) => kind is QuizProblemKind.Measurement or QuizProblemKind.Decimal
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
                DisplayValue: kind is QuizProblemKind.Measurement or QuizProblemKind.Decimal ? Display(value) : null);
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
        var candidates = QuizStoryContextCatalog.All.Where(c => c.Id is "classroom" or "library" or "craft" or "community" or "events" or "family-age").ToArray();
        var context = candidates[NextContextVariant(type, language, "two-number-story", candidates.Length)];
        if (context.Id == "family-age") return CreateAgeTwoNumbers(type, language, tier);
        var t = new DifficultyBuilder(QuizProblemKind.TwoNumbers, type, language, tier);
        int level = (int)tier, smallParts = level <= 2 ? 1 : _random.Next(2, 5);
        int largeParts = smallParts + _random.Next(1, 4);
        int part = _random.Next(2, Math.Max(3, Math.Min(15, (context.MaximumPerPeriod - 12) / (smallParts + largeParts) + 1)));
        int small = part * smallParts, large = part * largeParts;
        int total = small + large, difference = large - small;
        string quantity, differenceExpression = "", ratioExpression = "", scenario;
        var owners = context.Id switch
        {
            "classroom" => ("Tổ thứ nhất", "Tổ thứ hai", "The first team", "The second team"),
            "library" => ("Kệ thứ nhất", "Kệ thứ hai", "The first shelf", "The second shelf"),
            "events" => ("Khu ghế thứ nhất", "Khu ghế thứ hai", "The first seating section", "The second seating section"),
            _ => ("Nhóm thứ nhất", "Nhóm thứ hai", "The first group", "The second group")
        };
        string smallLabel = t.L(owners.Item1, owners.Item3), largeLabel = t.L(owners.Item2, owners.Item4);
        string unit = context.Unit(language);
        t.Constant(2);
        bool sum = type != ElementaryQuizType.DifferenceRatio;
        int given = sum ? total : difference;
        if (level == 1)
        {
            quantity = t.Given("quantity", given);

            scenario = "direct";
        }
        else if (level == 2)
        {
            string twice = t.Given("double-quantity", 2 * given);
            quantity = $"({twice}/2)";

            t.Step(t.L("Dữ kiện cần dùng", "Required quantity"), quantity, unit);
            scenario = "infer-half-quantity";
        }
        else if (level == 3)
        {
            int x = given / 2;
            string first = t.Given("quantity-first", x), second = t.Given("quantity-second", given - x);
            quantity = $"({first}+{second})";

            t.Step(t.L("Dữ kiện cần dùng", "Required quantity"), quantity, unit);
            scenario = "infer-quantity";
        }
        else
        {
            int increase = _random.Next(3, 12), reduction = level == 5 ? _random.Next(1, 6) : 0;
            string added = t.Given("added", increase);
            string after = t.Given("after", given + (sum ? 2 : 1) * increase - reduction);
            string restored = after;

            if (level == 5)
            {
                string removed = t.Given("previous-reduction", reduction);

                restored = $"({after}+{removed})";
                t.Step(t.L("Khôi phục trước lần giảm", "Undo the decrease"), restored, unit);
            }

            quantity = sum ? $"({restored}-2*{added})" : $"({restored}-{added})";
            t.Step(t.L("Dữ kiện ban đầu", "Original quantity"), quantity, unit);
            scenario = level == 5 ? "two-changes" : "reverse-change";
        }

        if (type == ElementaryQuizType.SumDifference)
        {
            if (level <= 3)
            {
                differenceExpression = t.Given("difference", difference);

            }
            else
            {
                string transfer = t.Given("transfer", _random.Next(1, Math.Max(2, difference / 2)));
                string remaining = t.Given("remaining-difference", difference - 2 * int.Parse(transfer));
                differenceExpression = $"({remaining}+2*{transfer})";

                t.Step(t.L("Hiệu ban đầu", "Original difference"), differenceExpression, unit);
            }
            t.Answer(smallLabel, $"({quantity}-{differenceExpression})/2", unit);
            t.Answer(largeLabel, $"({quantity}+{differenceExpression})/2", unit);
        }
        else
        {
            string r1 = t.Given("ratio-small", smallParts), r2 = t.Given("ratio-large", largeParts);
            if (level < 5)
            {

                ratioExpression = sum ? $"({r1}+{r2})" : $"({r2}-{r1})";
            }
            else
            {
                t.Givens.RemoveAll(fact => fact.Role == "ratio-large");
                string delta = t.Given("ratio-extra", largeParts - smallParts);

                r2 = $"({r1}+{delta})";
                t.Step(t.L("Số phần của số lớn", "Parts in the larger number"), r2, t.L("phần", "parts"));
                ratioExpression = sum ? $"({r1}+{r2})" : $"({r2}-{r1})";
                scenario = "change-and-inferred-ratio";
            }
            t.Step(t.L("Giá trị một phần", "Value of one part"), $"{quantity}/{ratioExpression}", unit);
            t.Answer(smallLabel, $"{quantity}/{ratioExpression}*{r1}", unit);
            t.Answer(largeLabel, $"{quantity}/{ratioExpression}*{r2}", unit);
        }

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
        // Prose is built from semantic fact roles, never by replacing mathematical nouns.
        string text = TwoNumberStory(t, context, smallLabel, largeLabel, sum, level);
        return t.Build(scenario, text, diagram: diagram) with { StoryContextId = context.Id };
    }
}
