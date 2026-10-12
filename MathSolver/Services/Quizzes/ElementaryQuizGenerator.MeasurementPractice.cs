using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    public static bool SupportsMeasurementPractice(ElementaryQuizType? type) => type is
        ElementaryQuizType.LengthConversion or ElementaryQuizType.MassConversion or
        ElementaryQuizType.CapacityConversion or ElementaryQuizType.AreaConversion or ElementaryQuizType.VolumeConversion;

    /// <summary>One exact conversion generated entirely by C#, without narrative contexts or bank templates.</summary>
    public ArithmeticQuizQuestion GenerateMeasurementPractice(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier)
    {
        if (!SupportsMeasurementPractice(type) || !Enum.IsDefined(tier))
            throw new ArgumentOutOfRangeException(nameof(type));

        string category = type switch
        {
            ElementaryQuizType.LengthConversion => "length",
            ElementaryQuizType.MassConversion => "mass",
            ElementaryQuizType.CapacityConversion => "capacity",
            ElementaryQuizType.AreaConversion => "area",
            _ => "volume"
        };
        // Restrict practice to school metric units; volume never crosses into litres.
        string[] ids = category switch
        {
            "length" => ["km", "hm", "dam", "m", "dm", "cm", "mm"],
            "mass" => ["t", "q", "y", "kg", "g", "mg"],
            "capacity" => ["l", "ml"],
            "area" => ["km2", "ha", "m2", "dm2", "cm2", "mm2"],
            _ => ["m3", "dm3", "cm3", "mm3"]
        };
        var units = MeasurementEngine.GetCategory(category).Units.Where(unit => ids.Contains(unit.Id)).ToArray();
        int level = (int)tier;
        decimal maximumFactor = level == 1 ? category == "area" ? 10000m : 1000m
            : level == 2 ? category == "volume" ? 1000000m : 10000m : 1000000000000m;
        var pairs = (from source in units
                     from target in units
                     let factor = MeasurementQuantityRules.Convert(1, category, source.Id, category, target.Id)
                     where source.Id != target.Id && factor != 1 &&
                         (level > 1 || factor > 1) && Math.Max(factor, 1 / factor) <= maximumFactor
                     select (Source: source, Target: target, Factor: factor)).ToArray();
        var pair = pairs[_random.Next(pairs.Length)];
        decimal amount = level == 1 ? _random.Next(1, 10)
            : level == 2 ? _random.Next(1, 51) * (pair.Factor < 1 ? 1 / pair.Factor : 1)
            : _random.Next(1, 200 * level) / (level == 3 ? 10m : level == 4 ? 100m : 1000m);
        decimal multiplier = pair.Factor > 1 ? pair.Factor : 1 / pair.Factor;
        string N(decimal value) => value.ToString("0.################", CultureInfo.InvariantCulture);
        var builder = new DifficultyBuilder(QuizProblemKind.Measurement, type, language, tier)
        {
            RequiresSolution = false,
            Explanation = pair.Factor > 1
                ? $"1 {pair.Source.Symbol} = {N(multiplier)} {pair.Target.Symbol}"
                : $"{N(multiplier)} {pair.Source.Symbol} = 1 {pair.Target.Symbol}"
        };
        string given = builder.Given("source-quantity", amount, pair.Source.Symbol);
        builder.Constants.Add(N(multiplier));
        string expression = $"{given}{(pair.Factor > 1 ? "*" : "/")}{N(multiplier)}";
        builder.Answer(QuizContentCatalog.Text(language, "ElementaryQuizGenerator.CompleteQuestion.071"),
            expression, pair.Target.Symbol);
        var contract = builder.Build("numeric-unit-conversion", $"{given} {pair.Source.Symbol} = ? {pair.Target.Symbol}");
        return CompleteQuestion(mode, contract, [], null);
    }
}
