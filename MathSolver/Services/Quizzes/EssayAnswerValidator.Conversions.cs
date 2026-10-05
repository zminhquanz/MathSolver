using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public sealed partial class EssayAnswerValidator
{
    /// <summary>Check optional conversion steps and the student's chosen arithmetic, never a fixed worked method.</summary>
    private static (bool IsCorrect, EssayAnswerError Error) ValidateDimensionCalculations(
        ArithmeticQuizQuestion question, string? equations, out IReadOnlyList<EssayStepValidationResult> steps)
    {
        steps = [];
        string[] lines = (equations ?? "").Replace('\r', '\n').Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length is 0 or > 20 || lines.Any(l => l.Length > 512)) return (false, EssayAnswerError.InvalidEquationFormat);
        var results = new List<EssayStepValidationResult>();
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            var (calculation, unit) = SplitEquationResult(question, line);
            bool okay;
            EssayAnswerError error;
            if (index == lines.Length - 1)
                (okay, error) = ValidateCalculatedEquation(question, calculation);
            else if (TryConversion(line, question.WordProblem!, out bool converted))
                (okay, error) = (converted, converted ? EssayAnswerError.None : EssayAnswerError.WrongEquationResult);
            else
            {
                var parts = calculation.Split('=', StringSplitOptions.TrimEntries);
                EssayCalculationEvaluator.Value? value = null;
                okay = parts.Length >= 2;
                foreach (string part in parts)
                {
                    if (!EssayCalculationEvaluator.TryEvaluate(part, out var evaluated, out _)
                        || value is { } previous && previous != evaluated) { okay = false; break; }
                    value = evaluated;
                }
                error = okay ? EssayAnswerError.None : EssayAnswerError.WrongEquationResult;
                if (okay && unit.Length > 0 && !AllowedIntermediateUnit(unit, question.WordProblem!))
                    (okay, error) = (false, EssayAnswerError.WrongEquationUnit);
            }
            results.Add(new(index + 1, line, okay, error));
            if (!okay) { steps = results; return (false, error); }
        }
        steps = results;
        return (true, EssayAnswerError.None);
    }

    private static bool TryConversion(string line, MathWordProblem word, out bool correct)
    {
        correct = false;
        string[] parts = line.Split('=', StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;
        var a = ConversionQuantity().Match(parts[0]);
        var b = ConversionQuantity().Match(parts[1]);
        if (!a.Success || !b.Success) return false;
        var (familyA, factorA) = UnitScale(a.Groups["unit"].Value);
        var (familyB, factorB) = UnitScale(b.Groups["unit"].Value);
        if (familyA != familyB || !AllowedFamily(familyA, word)) return true;
        if (!EssayCalculationEvaluator.TryEvaluate(a.Groups["value"].Value.Replace(',', '.'), out var x, out _, true)
            || !EssayCalculationEvaluator.TryEvaluate(b.Groups["value"].Value.Replace(',', '.'), out var y, out _, true)) return true;
        correct = x.Numerator * factorA * y.Denominator == y.Numerator * factorB * x.Denominator;
        return true;
    }
    private static (string Family, int Scale) UnitScale(string unit) => unit.ToLowerInvariant() switch
    {
        "kg" => ("mass", 1000), "g" => ("mass", 1), "km" => ("length", 1000), "m" => ("length", 1),
        "l" => ("capacity", 1000), "ml" => ("capacity", 1),
        "h" or "giờ" or "hour" or "hours" => ("time", 60),
        "min" or "phút" or "minute" or "minutes" => ("time", 1), _ => ("", 0)
    };
    private static bool AllowedFamily(string family, MathWordProblem word) => family switch
    {
        "mass" => word.Quantity == WordProblemQuantity.Mass,
        "length" => word.Quantity is WordProblemQuantity.Distance or WordProblemQuantity.Speed,
        "capacity" => word.Quantity == WordProblemQuantity.Capacity,
        "time" => word.ConversionStep?.Contains("phút", StringComparison.Ordinal) == true
            || word.ConversionStep?.Contains("minutes", StringComparison.Ordinal) == true,
        _ => false
    };
    private static bool AllowedIntermediateUnit(string unit, MathWordProblem word)
        => unit == word.AnswerUnit || AllowedFamily(UnitScale(unit).Family, word);

    [GeneratedRegex(@"^\s*(?:(?:Đổi|Quy đổi|Ta có|Convert|Conversion|We have)\s*:?\s*)?(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>kg|g|km|ml|m|l|h|giờ|phút|hours?|minutes?|min)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex ConversionQuantity();
}
