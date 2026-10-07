using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateDecimalStory(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Decimal, type, language, tier);
        string[] ids = ["kitchen", "decoration", "water"];
        string id = ids[NextContextVariant(type, language, "decimal-story", ids.Length)];
        var c = QuizStoryContextCatalog.Find(id);
        string unit = (id == "kitchen" ? "kg" : (id == "water" ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.023") : "m"));
        string item = (id == "kitchen" ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.024") : (id == "water" ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.025") : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.026")));
        int level = (int)tier, scale = level <= 2 ? 10 : 100;
        int maximum = id == "decoration" ? 50 : id == "kitchen" ? 100 : 1000;
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
            type is ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.001") : unit);
        if (level >= 3) return CreateIndirectDecimalStory(t, c.Id, item, unit, type, level, x, y);
        string problem, op;
        if (type == ElementaryQuizType.DecimalAdd) {
            op = "+";
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.002", ("item", $"{item}"), ("a", $"{a}"), ("unit", $"{unit}"), ("b", $"{b}"));
        }
        else if (type == ElementaryQuizType.DecimalSubtract) {
            op = "-";
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.003", ("a", $"{a}"), ("unit", $"{unit}"), ("item", $"{item}"), ("b", $"{b}"));
        }
        else if (type == ElementaryQuizType.DecimalMultiply) {
            op = "*";
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.004", ("a", $"{a}"), ("unit", $"{unit}"), ("item", $"{item}"), ("b", $"{b}"));
        }
        else {
            op = "/";
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.005", ("a", $"{a}"), ("unit", $"{unit}"), ("item", $"{item}"), ("b", $"{b}"));
        }
        t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateDecimalStory.006", ("item", $"{item}")), $"{a}{op}{b}", unit);
        return t.Build("decimal-" + type, problem) with { StoryContextId = c.Id };
    }

    private ElementaryQuizContract CreateIndirectDecimalStory(DifficultyBuilder t, string contextId, string item,
        string unit, ElementaryQuizType type, int level, decimal x, decimal y)
    {
        t.Givens.Clear(); t.Units.Clear();
        bool conversion = level == 5;
        int factor = contextId == "decoration" ? 100 : 1000;
        string smallUnit = contextId == "decoration" ? "cm" : contextId == "kitchen" ? "g" : "ml";
        string first = t.Given("quantity", conversion ? x * factor : x, conversion ? smallUnit : unit);
        string left = conversion ? $"({first}/{factor})" : first;
        string prefix = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.007", ("first", $"{first}"), ("conversion_smallUnit_unit", $"{(conversion ? smallUnit : unit)}"), ("item", $"{item}"));
        if (conversion) { t.Constant(factor); t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.008"), left, unit); }
        string expression, problem;
        if (type == ElementaryQuizType.DecimalAdd)
        {
            string gap = t.Given("difference", y, unit);
            string second = $"({left}+{gap})";
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.009"), second, unit);
            expression = $"{left}+{second}";
            problem = prefix + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.010", ("gap", $"{gap}"), ("unit", $"{unit}"));
        }
        else if (type == ElementaryQuizType.DecimalSubtract)
        {
            // Use two portions from one total; no unmentioned or overlapping amount.
            string used = t.Given("used-first", y / 2, unit), other = t.Given("used-second", y / 2, unit);
            expression = $"{left}-({used}+{other})";
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.011"), $"{used}+{other}", unit);
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.012", ("first", $"{first}"), ("conversion_smallUnit_unit", $"{(conversion ? smallUnit : unit)}"), ("item", $"{item}"), ("used", $"{used}"), ("unit", $"{unit}"), ("other", $"{other}"));
        }
        else
        {
            string extra = t.Given("extra-quantity", type == ElementaryQuizType.DecimalMultiply ? .5m : y / 2, unit);
            string count = t.Given("portion-count", y, QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.013"));
            string combined = $"({left}+{extra})";
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.014"), combined, unit);
            if (type == ElementaryQuizType.DecimalMultiply)
            {
                expression = $"{combined}*{count}";
                problem = prefix + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.015", ("extra", $"{extra}"), ("unit", $"{unit}"), ("count", $"{count}"));
            }
            else
            {
                expression = $"{combined}/{count}";
                problem = prefix + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.016", ("extra", $"{extra}"), ("unit", $"{unit}"), ("count", $"{count}"));
            }
        }
        if (level >= 4)
        {
            string returned = t.Given("adjustment", .25m, unit);
            t.Step((type == ElementaryQuizType.DecimalDivide ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.027") : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.028")), expression, unit);
            expression = $"({expression})+{returned}";
            problem += type == ElementaryQuizType.DecimalDivide
                ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.017", ("returned", $"{returned}"), ("unit", $"{unit}"), ("item", $"{item}"))
                : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.018", ("returned", $"{returned}"), ("unit", $"{unit}"), ("item", $"{item}"));
        }
        string label = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.019", ("item", $"{item}"));
        string question = type switch
        {
            ElementaryQuizType.DecimalSubtract => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.020", ("unit", $"{unit}")),
            ElementaryQuizType.DecimalDivide => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.021", ("unit", $"{unit}")),
            _ => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.DecimalStories.CreateIndirectDecimalStory.022", ("unit", $"{unit}"), ("item", $"{item}"))
        };
        t.Answer(label, expression, unit);
        return t.Build("decimal-story-" + type + "-" + level, problem + question) with { StoryContextId = contextId };
    }
}
