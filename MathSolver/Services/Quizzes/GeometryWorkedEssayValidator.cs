using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Checks all written arithmetic, accepting merged, split or reordered derivations.</summary>
internal static class GeometryWorkedEssayValidator
{
    internal static (bool IsCorrect, EssayAnswerError Error) Validate(ArithmeticQuizQuestion question,
        string? text, out IReadOnlyList<EssayStepValidationResult> reports)
    {
        string[] lines = (text ?? "").Replace("\r", "", StringComparison.Ordinal)
            .Split(['\n', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var results = new List<EssayStepValidationResult>();
        reports = results;
        if (lines.Length is 0 or > 64 || text!.Length > 32768)
            return (false, EssayAnswerError.InvalidEquationFormat);
        var geometry = question.GeometryProblem!;
        bool reachedAnswer = false;
        foreach (string line in lines)
        {
            bool fractionResult = line[(line.LastIndexOf('=') + 1)..].Contains('/');
            var (calculation, unit) = EssayAnswerValidator.SplitEquationResult(question, line, allowFractionResult: fractionResult);
            string[] members = calculation.Split('=', StringSplitOptions.TrimEntries);
            EssayAnswerError error = EssayAnswerError.None;
            EssayCalculationEvaluator.Value? value = null;
            bool operation = false;
            int start = members.Length > 2 && members[0].All(character => char.IsLetter(character) || char.IsWhiteSpace(character)) ? 1 : 0;
            if (members.Length < 2) error = EssayAnswerError.InvalidEquationFormat;
            else for (int index = start; index < members.Length; index++)
            {
                if (!EssayCalculationEvaluator.TryEvaluate(members[index], out var current, out bool hasOperation))
                { error = EssayAnswerError.InvalidEquationFormat; break; }
                operation |= hasOperation;
                if (value.HasValue && value != current) { error = EssayAnswerError.WrongEquationResult; break; }
                value = current;
            }
            if (error == EssayAnswerError.None && !operation) error = EssayAnswerError.InvalidEquationFormat;
            string? expectedUnit = null;
            if (error == EssayAnswerError.None && value.HasValue)
            {
                var canonical = geometry.Reasoning!.Steps.FirstOrDefault(step =>
                    Normalize(step.Expression) == Normalize(members[start]));
                bool finalValue = value.Value.Denominator.IsOne && value.Value.Numerator == geometry.CorrectAnswer;
                if (finalValue && EssayAnswerValidator.IsExpectedUnitForFeedback(question, unit)) reachedAnswer = true;
                else if (finalValue && unit.Length > 0 &&
                    (canonical is null || !MatchesUnit(question, unit, canonical.Unit)))
                { error = EssayAnswerError.WrongEquationUnit; expectedUnit = geometry.AnswerUnit; }
                if (unit.Length > 0 && !IsMetricUnit(question, unit))
                    error = EssayAnswerError.WrongEquationUnit;
                // A recognized canonical dimension/conversion step must also retain its unit.
                if (canonical is not null && unit.Length > 0 && !MatchesUnit(question, unit, canonical.Unit))
                { error = EssayAnswerError.WrongEquationUnit; expectedUnit = canonical.Unit; }
            }
            results.Add(new(results.Count + 1, line, error == EssayAnswerError.None, error,
                value.HasValue ? EssayCalculationEvaluator.Format(value.Value) : null,
                members.LastOrDefault(), unit, expectedUnit));
        }
        var failure = results.FirstOrDefault(step => !step.IsCorrect);
        if (failure is not null) return (false, failure.Error);
        return reachedAnswer ? (true, EssayAnswerError.None) : (false, EssayAnswerError.WrongEquationUnit);
    }

    private static string Normalize(string expression) => Regex.Replace(expression, @"\s", "")
        .Replace("×", "*", StringComparison.Ordinal).Replace("÷", "/", StringComparison.Ordinal).Replace("−", "-", StringComparison.Ordinal);

    private static bool MatchesUnit(ArithmeticQuizQuestion question, string entered, string expected)
    {
        foreach (var length in Enum.GetValues<GeometryLengthUnit>())
        foreach (var measurement in new[] { GeometryMeasurement.Perimeter, GeometryMeasurement.Area, GeometryMeasurement.Volume })
        {
            var adapted = question with { WordProblem = null, GeometryProblem = question.GeometryProblem! with { LengthUnit = length, Measurement = measurement } };
            if (adapted.GeometryProblem.AnswerUnit == expected)
                return EssayAnswerValidator.IsExpectedUnitForFeedback(adapted, entered);
        }
        return false;
    }

    private static bool IsMetricUnit(ArithmeticQuizQuestion question, string entered) =>
        Enum.GetValues<GeometryLengthUnit>().Any(length =>
            new[] { GeometryMeasurement.Perimeter, GeometryMeasurement.Area, GeometryMeasurement.Volume }.Any(measurement =>
                EssayAnswerValidator.IsExpectedUnitForFeedback(question with
                { WordProblem = null, GeometryProblem = question.GeometryProblem! with { LengthUnit = length, Measurement = measurement } }, entered)));
}
