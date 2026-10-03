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
        string label = t.L("Số đo sau khi đổi", "Converted measurement"), expression, problem;
        string a = t.Given("large-quantity", _random.Next(2, 10), large.Symbol);
        bool mixed = type == ElementaryQuizType.MixedLength;
        string scenario;
        if (level == 1 && !mixed)
        {
            expression = $"{a}*{factor}";
            problem = t.L($"Đổi {a} {large.Symbol} sang {small.Symbol}.", $"Convert {a} {large.Symbol} to {small.Symbol}.");
            scenario = "one-conversion";
        }
        else if (level == 2 && !mixed)
        {
            t.Givens.Clear();
            string value = t.Given("small-quantity", int.Parse(a) * factor + factor / 2, small.Symbol);
            expression = $"{value}/{factor}";
            t.Answer(label, expression, large.Symbol);
            return t.Build("reverse-conversion", t.L($"Đổi {value} {small.Symbol} sang {large.Symbol}.", $"Convert {value} {small.Symbol} to {large.Symbol}."));
        }
        else
        {
            string b = t.Given("small-quantity", _random.Next(2, (int)Math.Min(40, factor)), small.Symbol);
            expression = $"{a}*{factor}+{b}";
            problem = t.L($"Một số đo gồm {a} {large.Symbol} và {b} {small.Symbol}. ",
                $"A measurement consists of {a} {large.Symbol} and {b} {small.Symbol}. ");
            scenario = "mixed-measurement";
            t.Step(t.L("Đổi phần đơn vị lớn", "Convert the larger unit"), $"{a}*{factor}", small.Symbol);
            if (level >= 3)
            {
                string c = t.Given("extra-quantity", _random.Next(1, 8), large.Symbol);
                problem += t.L($"Cộng thêm {c} {large.Symbol}. ", $"Add {c} {large.Symbol}. ");
                expression = $"({expression})+{c}*{factor}";
                t.Step(t.L("Đổi phần thêm vào", "Convert the added quantity"), $"{c}*{factor}", small.Symbol);
                scenario = "combine-converted-quantities";
            }
            if (level >= 4)
            {
                string used = t.Given("removed-quantity", _random.Next(1, int.Parse(b) + 1), small.Symbol);
                problem += t.L($"Sau đó bớt {used} {small.Symbol}. ", $"Then subtract {used} {small.Symbol}. ");
                expression = $"({expression})-{used}";
                scenario = "convert-combine-subtract";
            }
        }
        string answerUnit = level == 5 || mixed && level == 2 ? large.Symbol : small.Symbol;
        if (level == 5 || mixed && level == 2)
        {
            t.Step(t.L("Số đo còn lại ở đơn vị nhỏ", "Remaining measurement in the smaller unit"), expression, small.Symbol);
            expression = $"({expression})/{factor}";
            scenario += "-convert-back";
        }
        if (level > 1 || mixed)
            problem += t.L($"Kết quả là bao nhiêu {answerUnit}?", $"What is the result in {answerUnit}?");
        t.Answer(label, expression, answerUnit);
        return t.Build(scenario, problem);
    }
}
