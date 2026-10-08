using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    // Explicit entry point: stored prose never randomly becomes a bare calculation.
    public ArithmeticQuizQuestion GenerateDecimalStory(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, string contextId = "")
    {
        if (!Enum.IsDefined(tier) || !DecimalStoryTypes.Contains(type)) throw new ArgumentException("InvalidDecimalStoryProfile");
        var contract = CreateDecimalStory(type, language, tier, contextId);
        return CompleteQuestion(mode, contract, [], null);
    }

    internal static readonly ElementaryQuizType[] DecimalStoryTypes = [ElementaryQuizType.DecimalAdd,
        ElementaryQuizType.DecimalSubtract, ElementaryQuizType.DecimalMultiply, ElementaryQuizType.DecimalDivide];
    internal const string DecimalContextsList = "DecimalStoryContexts";
    internal sealed record DecimalStoryContext(string Id, string Item, string Unit, string SmallUnit,
        int ConversionFactor, int MaximumQuantity);
    internal static IReadOnlyList<DecimalStoryContext> DecimalContexts(AppLanguage language) =>
        QuizContentCatalog.LoadList<DecimalStoryContext>(DecimalContextsList, QuizContentCatalog.Culture(language));

    private ElementaryQuizContract CreateDecimalStory(ElementaryQuizType type, AppLanguage language, CurriculumTier tier,
        string contextId = "")
    {
        var t = new DifficultyBuilder(QuizProblemKind.Decimal, type, language, tier);
        var contexts = DecimalContexts(language);
        var c = contextId.Length == 0 ? contexts[NextContextVariant(type, language, "decimal-story", contexts.Count)]
            : contexts.FirstOrDefault(c => c.Id == contextId) ?? throw new ArgumentException("InvalidDecimalStoryProfile");
        string unit = c.Unit, item = c.Item;
        int level = (int)tier, scale = level <= 2 ? 10 : 100;
        int maximum = c.MaximumQuantity;
        decimal x = _random.Next(2, Math.Min(maximum / 6, 5 + 2 * level)) + _random.Next(1, scale) / (decimal)scale;
        decimal y = _random.Next(2, 6);
        if (type is ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract)
            y += _random.Next(1, scale) / (decimal)scale;
        if (level <= 2)
        {
            x = _random.Next(6, 9) + (level == 1 || type == ElementaryQuizType.DecimalSubtract ? .2m : .8m);
            if (type is ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract)
                y = _random.Next(2, 6) + (level == 1 ? .1m : .7m);
            if (type == ElementaryQuizType.DecimalDivide && level == 1) x = _random.Next(2, 6);
        }
        if (type == ElementaryQuizType.DecimalSubtract && x < y) (x, y) = (y, x);
        if (type == ElementaryQuizType.DecimalDivide) x *= y;
        string a = t.Given("quantity", x, unit), b = t.Given("second-quantity", y,
            type is ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide ? QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.001") : unit);
        if (level >= 3) return CreateIndirectDecimalStory(t, c, type, level, x, y);
        string problem, op;
        if (type == ElementaryQuizType.DecimalAdd) {
            op = "+";
            problem = QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.002", ("item", $"{item}"), ("a", $"{a}"), ("unit", $"{unit}"), ("b", $"{b}"));
        }
        else if (type == ElementaryQuizType.DecimalSubtract) {
            op = "-";
            problem = QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.003", ("a", $"{a}"), ("unit", $"{unit}"), ("item", $"{item}"), ("b", $"{b}"));
        }
        else if (type == ElementaryQuizType.DecimalMultiply) {
            op = "*";
            problem = QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.004", ("a", $"{a}"), ("unit", $"{unit}"), ("item", $"{item}"), ("b", $"{b}"));
        }
        else {
            op = "/";
            problem = QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.005", ("a", $"{a}"), ("unit", $"{unit}"), ("item", $"{item}"), ("b", $"{b}"));
        }
        t.Answer(QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.006", ("item", $"{item}")), $"{a}{op}{b}", unit);
        return t.Build("decimal-" + type, problem) with { StoryContextId = c.Id };
    }

    private ElementaryQuizContract CreateIndirectDecimalStory(DifficultyBuilder t, DecimalStoryContext context,
        ElementaryQuizType type, int level, decimal x, decimal y)
    {
        string contextId = context.Id, item = context.Item, unit = context.Unit;
        t.Givens.Clear(); t.Units.Clear();
        bool conversion = level == 5;
        int factor = context.ConversionFactor;
        string smallUnit = context.SmallUnit;
        string first = t.Given("quantity", conversion ? x * factor : x, conversion ? smallUnit : unit);
        string left = conversion ? $"({first}/{factor})" : first;
        string prefix = QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.007", ("first", $"{first}"), ("conversion_smallUnit_unit", $"{(conversion ? smallUnit : unit)}"), ("item", $"{item}"));
        if (conversion) { t.Constant(factor); t.Step(QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.008"), left, unit); }
        string expression, problem;
        if (type == ElementaryQuizType.DecimalAdd)
        {
            string gap = t.Given("difference", y, unit);
            string second = $"({left}+{gap})";
            t.Step(QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.009"), second, unit);
            expression = $"{left}+{second}";
            problem = prefix + QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.010", ("gap", $"{gap}"), ("unit", $"{unit}"));
        }
        else if (type == ElementaryQuizType.DecimalSubtract)
        {
            // Use two portions from one total; no unmentioned or overlapping amount.
            string used = t.Given("used-first", y / 2, unit), other = t.Given("used-second", y / 2, unit);
            expression = $"{left}-({used}+{other})";
            t.Step(QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.011"), $"{used}+{other}", unit);
            problem = QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.012", ("first", $"{first}"), ("conversion_smallUnit_unit", $"{(conversion ? smallUnit : unit)}"), ("item", $"{item}"), ("used", $"{used}"), ("unit", $"{unit}"), ("other", $"{other}"));
        }
        else
        {
            string extra = t.Given("extra-quantity", type == ElementaryQuizType.DecimalMultiply ? .5m : y / 2, unit);
            string count = t.Given("portion-count", y, QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.013"));
            string combined = $"({left}+{extra})";
            t.Step(QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.014"), combined, unit);
            if (type == ElementaryQuizType.DecimalMultiply)
            {
                expression = $"{combined}*{count}";
                problem = prefix + QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.015", ("extra", $"{extra}"), ("unit", $"{unit}"), ("count", $"{count}"));
            }
            else
            {
                expression = $"{combined}/{count}";
                problem = prefix + QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.016", ("extra", $"{extra}"), ("unit", $"{unit}"), ("count", $"{count}"));
            }
        }
        if (level >= 4)
        {
            string returned = t.Given("adjustment", .25m, unit);
            t.Step((type == ElementaryQuizType.DecimalDivide ? QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.027") : QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.028")), expression, unit);
            expression = $"({expression})+{returned}";
            problem += type == ElementaryQuizType.DecimalDivide
                ? QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.017", ("returned", $"{returned}"), ("unit", $"{unit}"), ("item", $"{item}"))
                : QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.018", ("returned", $"{returned}"), ("unit", $"{unit}"), ("item", $"{item}"));
        }
        string label = QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.019", ("item", $"{item}"));
        string question = type switch
        {
            ElementaryQuizType.DecimalSubtract => QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.020", ("unit", $"{unit}")),
            ElementaryQuizType.DecimalDivide => QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.021", ("unit", $"{unit}")),
            _ => QuizContentCatalog.TextForUnit(t.Language, unit, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.022", ("unit", $"{unit}"), ("item", $"{item}"))
        };
        t.Answer(label, expression, unit);
        return t.Build("decimal-story-" + type + "-" + level, problem + question) with { StoryContextId = contextId };
    }
}
