using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    internal static readonly ElementaryQuizType[] MeasurementStoryTypes =
        [ElementaryQuizType.MassConversion, ElementaryQuizType.CapacityConversion, ElementaryQuizType.LengthConversion];
    internal const string MeasurementContextsList = "MeasurementStoryContexts";
    internal sealed record MeasurementStoryContext(string Id, string Category, string Subject);
    internal static string MeasurementCategory(ElementaryQuizType type) => type switch
    {
        ElementaryQuizType.MassConversion => "mass",
        ElementaryQuizType.CapacityConversion => "capacity",
        ElementaryQuizType.LengthConversion => "length",
        _ => throw new ArgumentException("InvalidMeasurementStoryProfile")
    };
    internal static IReadOnlyList<MeasurementStoryContext> MeasurementContexts(AppLanguage language, ElementaryQuizType type) =>
        QuizContentCatalog.LoadList<MeasurementStoryContext>(MeasurementContextsList, QuizContentCatalog.Culture(language))
            .Where(context => context.Category == MeasurementCategory(type)).ToArray();

    public ArithmeticQuizQuestion GenerateMeasurementStory(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, string contextId = "")
    {
        if (!Enum.IsDefined(tier) || !MeasurementStoryTypes.Contains(type))
            throw new ArgumentException("InvalidMeasurementStoryProfile");
        var contract = CreateMeasurementStory(type, language, tier, contextId);
        return CompleteQuestion(mode, contract, [], null);
    }

    private ElementaryQuizContract CreateMeasurementStory(ElementaryQuizType type, AppLanguage language,
        CurriculumTier tier, string contextId = "")
    {
        var contexts = MeasurementContexts(language, type);
        var context = contextId.Length == 0 ? contexts[NextContextVariant(type, language, "measurement-story", contexts.Count)]
            : contexts.FirstOrDefault(context => context.Id == contextId)
                ?? throw new ArgumentException("InvalidMeasurementStoryProfile");
        var t = new DifficultyBuilder(QuizProblemKind.Measurement, type, language, tier);
        int level = (int)tier;
        (string largeId, string smallId) = context.Category switch
        {
            "mass" => ("kg", "g"), "capacity" => ("l", "ml"), _ => ("m", level == 1 ? "dm" : "cm")
        };
        var units = MeasurementEngine.GetCategory(context.Category).Units;
        var large = units.Single(unit => unit.Id == largeId);
        var small = units.Single(unit => unit.Id == smallId);
        decimal factor = MeasurementEngine.Convert(1, large, small);
        t.Constants.Add(factor.ToString(CultureInfo.InvariantCulture));
        string Text(string id, params (string Key, string Value)[] values) =>
            QuizContentCatalog.TextForUnit(language, context.Category, "ElementaryQuizGenerator.MeasurementStories." + id, values);
        string label = Text("001"), expression, problem;
        string a = t.Given("large-quantity", _random.Next(2, 10), large.Symbol);
        if (level == 1)
        {
            expression = $"{a}*{factor}";
            problem = Text("002", ("subject", context.Subject), ("amount", a),
                ("large_unit", large.Symbol), ("small_unit", small.Symbol));
        }
        else if (level == 2)
        {
            t.Givens.Clear();
            string value = t.Given("small-quantity", decimal.Parse(a, CultureInfo.InvariantCulture) * factor + factor / 2, small.Symbol);
            expression = $"{value}/{factor}";
            problem = Text("003", ("subject", context.Subject), ("amount", value),
                ("small_unit", small.Symbol), ("large_unit", large.Symbol));
            t.Answer(label, expression, large.Symbol);
            return t.Build("measurement-story-" + level, problem) with { StoryContextId = context.Id };
        }
        else
        {
            string b = t.Given("small-quantity", _random.Next(2, (int)Math.Min(40, factor)), small.Symbol);
            expression = $"{a}*{factor}+{b}";
            problem = Text("004", ("subject", context.Subject), ("large_amount", a),
                ("large_unit", large.Symbol), ("small_amount", b), ("small_unit", small.Symbol));
            t.Step(Text("005"), $"{a}*{factor}", small.Symbol);
            string extra = t.Given("extra-quantity", _random.Next(1, 8), large.Symbol);
            problem += Text("006", ("extra", extra), ("large_unit", large.Symbol));
            expression = $"({expression})+{extra}*{factor}";
            t.Step(Text("007"), $"{extra}*{factor}", small.Symbol);
            if (level >= 4)
            {
                string used = t.Given("removed-quantity", _random.Next(1, int.Parse(b, CultureInfo.InvariantCulture) + 1), small.Symbol);
                problem += Text("008", ("used", used), ("small_unit", small.Symbol));
                expression = $"({expression})-{used}";
            }
        }
        string answerUnit = level == 5 ? large.Symbol : small.Symbol;
        if (level == 5)
        {
            t.Step(Text("009"), expression, small.Symbol);
            expression = $"({expression})/{factor}";
        }
        if (level > 1) problem += Text("010", ("answer_unit", answerUnit));
        t.Answer(label, expression, answerUnit);
        return t.Build("measurement-story-" + level, problem) with { StoryContextId = context.Id };
    }
}
