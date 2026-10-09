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
        int ConversionFactor, int MaximumQuantity, string Activity = "", string GroupUnit = "", decimal GroupMaximum = 0);
    internal static IReadOnlyList<DecimalStoryContext> DecimalContexts(AppLanguage language, bool expanded = true) =>
        NarrativeContextExpansion.Load<DecimalStoryContext>(DecimalContextsList, language, expanded);

    // A selected activity supplies its own factual clauses, not just a different noun.
    // All clauses use the same C# quantity roles and participate in prose validation.
    internal static readonly string[] DecimalActivityClauses = ["002", "003", "004", "005", "006",
        "007", "010", "012", "015", "016", "017", "018", "019", "021", "029"];
    private static string DecimalStoryText(DecimalStoryContext context, AppLanguage language, string suffix,
        params (string Key, string Value)[] values)
    {
        string section = int.Parse(suffix) <= 6 ? "CreateDecimalStory." : "CreateIndirectDecimalStory.";
        string id = "ElementaryQuizGenerator.DecimalStories." + (context.Activity.Length > 0
            && DecimalActivityClauses.Contains(suffix) ? "Activity." + context.Activity + "." : section) + suffix;
        return QuizContentCatalog.TextForUnit(language, context.Unit, id, values);
    }

    private ElementaryQuizContract CreateDecimalStory(ElementaryQuizType type, AppLanguage language, CurriculumTier tier,
        string contextId = "")
    {
        var t = new DifficultyBuilder(QuizProblemKind.Decimal, type, language, tier);
        var contexts = DecimalContexts(language, _expandNarratives);
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
        if (c.GroupMaximum > 0 && type is ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide)
        {
            // Limit each bottle/bag/uniform, including later additions, before
            // multiplying by the integer group count. The total has its own cap.
            decimal reserve = level < 3 ? 0 : type == ElementaryQuizType.DecimalDivide && level >= 4 ? .75m : .5m;
            int ticks = (int)((c.GroupMaximum - reserve) * scale);
            x = _random.Next(Math.Max(1, ticks / 2), ticks + 1) / (decimal)scale;
        }
        if (type == ElementaryQuizType.DecimalSubtract && x < y) (x, y) = (y, x);
        if (type == ElementaryQuizType.DecimalDivide) x *= y;
        string a = t.Given("quantity", x, unit), b = t.Given("second-quantity", y,
            type is ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide ? (c.GroupUnit.Length > 0 ? c.GroupUnit : DecimalStoryText(c, t.Language, "001")) : unit);
        if (level >= 3) return CreateIndirectDecimalStory(t, c, type, level, x, y);
        string problem, op;
        if (type == ElementaryQuizType.DecimalAdd) {
            op = "+";
            problem = DecimalStoryText(c, t.Language, "002", ("item", $"{item}"), ("a", $"{a}"), ("unit", $"{unit}"), ("b", $"{b}"));
        }
        else if (type == ElementaryQuizType.DecimalSubtract) {
            op = "-";
            problem = DecimalStoryText(c, t.Language, "003", ("a", $"{a}"), ("unit", $"{unit}"), ("item", $"{item}"), ("b", $"{b}"));
        }
        else if (type == ElementaryQuizType.DecimalMultiply) {
            op = "*";
            problem = DecimalStoryText(c, t.Language, "004", ("a", $"{a}"), ("unit", $"{unit}"), ("item", $"{item}"), ("b", $"{b}"));
        }
        else {
            op = "/";
            problem = DecimalStoryText(c, t.Language, "005", ("a", $"{a}"), ("unit", $"{unit}"), ("item", $"{item}"), ("b", $"{b}"));
        }
        t.Answer(DecimalStoryText(c, t.Language, "006", ("item", $"{item}")), $"{a}{op}{b}", unit);
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
        string prefix = DecimalStoryText(context, t.Language,
            context.Activity.Length > 0 && type == ElementaryQuizType.DecimalMultiply ? "029" : "007",
            ("first", $"{first}"), ("conversion_smallUnit_unit", $"{(conversion ? smallUnit : unit)}"), ("item", $"{item}"));
        if (conversion) { t.Constant(factor); t.Step(DecimalStoryText(context, t.Language, "008"), left, unit); }
        string expression, problem;
        if (type == ElementaryQuizType.DecimalAdd)
        {
            string gap = t.Given("difference", y, unit);
            string second = $"({left}+{gap})";
            t.Step(DecimalStoryText(context, t.Language, "009"), second, unit);
            expression = $"{left}+{second}";
            problem = prefix + DecimalStoryText(context, t.Language, "010", ("gap", $"{gap}"), ("unit", $"{unit}"));
        }
        else if (type == ElementaryQuizType.DecimalSubtract)
        {
            // Use two portions from one total; no unmentioned or overlapping amount.
            string used = t.Given("used-first", y / 2, unit), other = t.Given("used-second", y / 2, unit);
            expression = $"{left}-({used}+{other})";
            t.Step(DecimalStoryText(context, t.Language, "011"), $"{used}+{other}", unit);
            problem = DecimalStoryText(context, t.Language, "012", ("first", $"{first}"), ("conversion_smallUnit_unit", $"{(conversion ? smallUnit : unit)}"), ("item", $"{item}"), ("used", $"{used}"), ("unit", $"{unit}"), ("other", $"{other}"));
        }
        else
        {
            string extra = t.Given("extra-quantity", type == ElementaryQuizType.DecimalMultiply ? .5m : y / 2, unit);
            string count = t.Given("portion-count", y, (context.GroupUnit.Length > 0 ? context.GroupUnit : DecimalStoryText(context, t.Language, "013")));
            string combined = $"({left}+{extra})";
            t.Step(DecimalStoryText(context, t.Language, "014"), combined, unit);
            if (type == ElementaryQuizType.DecimalMultiply)
            {
                expression = $"{combined}*{count}";
                problem = prefix + DecimalStoryText(context, t.Language, "015", ("extra", $"{extra}"), ("unit", $"{unit}"), ("count", $"{count}"));
            }
            else
            {
                expression = $"{combined}/{count}";
                problem = prefix + DecimalStoryText(context, t.Language, "016", ("extra", $"{extra}"), ("unit", $"{unit}"), ("count", $"{count}"));
            }
        }
        if (level >= 4)
        {
            string returned = t.Given("adjustment", .25m, unit);
            t.Step((type == ElementaryQuizType.DecimalDivide ? DecimalStoryText(context, t.Language, "027") : DecimalStoryText(context, t.Language, "028")), expression, unit);
            expression = $"({expression})+{returned}";
            problem += type == ElementaryQuizType.DecimalDivide
                ? DecimalStoryText(context, t.Language, "017", ("returned", $"{returned}"), ("unit", $"{unit}"), ("item", $"{item}"))
                : DecimalStoryText(context, t.Language, "018", ("returned", $"{returned}"), ("unit", $"{unit}"), ("item", $"{item}"));
        }
        string label = DecimalStoryText(context, t.Language, "019", ("item", $"{item}"));
        string question = type switch
        {
            ElementaryQuizType.DecimalSubtract => DecimalStoryText(context, t.Language, "020", ("unit", $"{unit}")),
            ElementaryQuizType.DecimalDivide => DecimalStoryText(context, t.Language, "021", ("unit", $"{unit}")),
            _ => DecimalStoryText(context, t.Language, "022", ("unit", $"{unit}"), ("item", $"{item}"))
        };
        t.Answer(label, expression, unit);
        return t.Build("decimal-story-" + type + "-" + level, problem + question) with { StoryContextId = contextId };
    }
}
