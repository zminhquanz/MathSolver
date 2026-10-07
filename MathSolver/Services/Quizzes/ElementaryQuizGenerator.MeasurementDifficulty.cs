using MathSolver.Models;
using MathSolver.Services.Core;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateMeasurementDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Measurement, type, language, tier);
        int level = (int)tier;
        (string category, string largeId, string smallId) = type switch
        {
            ElementaryQuizType.MassConversion => ("mass", "kg", "g"),
            ElementaryQuizType.CapacityConversion => ("capacity", "l", "ml"),
            ElementaryQuizType.AreaConversion => ("area", level <= 2 ? "dm2" : "m2", "cm2"),
            ElementaryQuizType.VolumeConversion => ("volume", level <= 2 ? "dm3" : "m3", "cm3"),
            _ => ("length", "m", level == 1 ? "dm" : "cm")
        };
        var units = MeasurementEngine.GetCategory(category).Units;
        var large = units.Single(unit => unit.Id == largeId);
        var small = units.Single(unit => unit.Id == smallId);
        decimal factor = MeasurementEngine.Convert(1, large, small);
        t.Constants.Add(factor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string label = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.001"), expression, problem;
        var context = type switch
        {
            ElementaryQuizType.MassConversion => QuizStoryContextCatalog.Find("kitchen"),
            ElementaryQuizType.CapacityConversion => QuizStoryContextCatalog.Find("water"),
            ElementaryQuizType.AreaConversion or ElementaryQuizType.VolumeConversion => QuizStoryContextCatalog.Find("construction"),
            _ => QuizStoryContextCatalog.Find("decoration")
        };
        string subject = type switch {
            ElementaryQuizType.MassConversion => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.011"),            ElementaryQuizType.CapacityConversion => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.012"),            ElementaryQuizType.AreaConversion => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.013"),            ElementaryQuizType.VolumeConversion => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.014"),            _ => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.015")};
        string a = t.Given("large-quantity", _random.Next(2, 10), large.Symbol);
        bool mixed = type == ElementaryQuizType.MixedLength;
        string scenario;
        if (level == 1 && !mixed)
        {
            expression = $"{a}*{factor}";
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.002", ("subject", $"{subject}"), ("a", $"{a}"), ("large_Symbol", $"{large.Symbol}"), ("small_Symbol", $"{small.Symbol}"));
            scenario = "one-conversion";
        }
        else if (level == 2 && !mixed)
        {
            t.Givens.Clear();
            string value = t.Given("small-quantity", int.Parse(a) * factor + factor / 2, small.Symbol);
            expression = $"{value}/{factor}";
            t.Answer(label, expression, large.Symbol);
            return t.Build("reverse-conversion", QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.003", ("subject", $"{subject}"), ("value", $"{value}"), ("small_Symbol", $"{small.Symbol}"), ("large_Symbol", $"{large.Symbol}")))
                with { StoryContextId = context.Id };
        }
        else
        {
            string b = t.Given("small-quantity", _random.Next(2, (int)Math.Min(40, factor)), small.Symbol);
            expression = $"{a}*{factor}+{b}";
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.004", ("subject", $"{subject}"), ("a", $"{a}"), ("large_Symbol", $"{large.Symbol}"), ("b", $"{b}"), ("small_Symbol", $"{small.Symbol}"));
            scenario = "mixed-measurement";
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.005"), $"{a}*{factor}", small.Symbol);
            if (level >= 3)
            {
                string c = t.Given("extra-quantity", _random.Next(1, 8), large.Symbol);
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.006", ("c", $"{c}"), ("large_Symbol", $"{large.Symbol}"));
                expression = $"({expression})+{c}*{factor}";
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.007"), $"{c}*{factor}", small.Symbol);
                scenario = "combine-converted-quantities";
            }
            if (level >= 4)
            {
                string used = t.Given("removed-quantity", _random.Next(1, int.Parse(b) + 1), small.Symbol);
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.008", ("used", $"{used}"), ("small_Symbol", $"{small.Symbol}"));
                expression = $"({expression})-{used}";
                scenario = "convert-combine-subtract";
            }
        }
        string answerUnit = level == 5 || mixed && level == 2 ? large.Symbol : small.Symbol;
        if (level == 5 || mixed && level == 2)
        {
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.009"), expression, small.Symbol);
            expression = $"({expression})/{factor}";
            scenario += "-convert-back";
        }
        if (level > 1 || mixed)
            problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MeasurementDifficulty.CreateMeasurementDifficulty.010", ("answerUnit", $"{answerUnit}"));
        t.Answer(label, expression, answerUnit);
        return t.Build(scenario, problem) with { StoryContextId = context.Id };
    }
}
