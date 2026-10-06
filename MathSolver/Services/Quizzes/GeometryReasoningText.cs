using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

internal static class GeometryReasoningText
{
    internal static string DimensionName(string shape, string key, AppLanguage language)
    {
        bool vi = language == AppLanguage.Vietnamese;
        return (shape, key) switch
        {
            ("rectangle" or "rectangular_prism", "a") => vi ? "chiều dài" : "length",
            ("rectangle" or "rectangular_prism", "b") => vi ? "chiều rộng" : "width",
            ("triangle" or "parallelogram", "a") => vi ? "đáy a" : "base a",
            ("trapezoid", "a") => vi ? "đáy lớn" : "longer base",
            ("trapezoid", "b") => vi ? "đáy nhỏ" : "shorter base",
            (_, "h") => vi ? "chiều cao" : "height",
            (_, "r") => vi ? "bán kính" : "radius",
            (_, "d1") => vi ? "đường chéo lớn" : "longer diagonal",
            (_, "d2") => vi ? "đường chéo nhỏ" : "shorter diagonal",
            _ => (vi ? "cạnh " : "side ") + key
        };
    }

    internal static string MeasurementName(GeometryMeasurement measurement, AppLanguage language) =>
        (measurement, language) switch
        {
            (GeometryMeasurement.Perimeter, AppLanguage.Vietnamese) => "chu vi",
            (GeometryMeasurement.Volume, AppLanguage.Vietnamese) => "thể tích",
            (GeometryMeasurement.LateralArea, AppLanguage.Vietnamese) => "diện tích xung quanh",
            (GeometryMeasurement.TotalArea, AppLanguage.Vietnamese) => "diện tích toàn phần",
            (_, AppLanguage.Vietnamese) => "diện tích",
            (GeometryMeasurement.Perimeter, _) => "perimeter",
            (GeometryMeasurement.Volume, _) => "volume",
            (GeometryMeasurement.LateralArea, _) => "lateral surface area",
            (GeometryMeasurement.TotalArea, _) => "total surface area",
            _ => "area"
        };

    internal static string SolutionLead(GeometryQuizContract contract, AppLanguage language) =>
        language == AppLanguage.Vietnamese
            ? $"{MeasurementName(contract.Measurement, language)} của {contract.ObjectName} là:"
            : $"The {MeasurementName(contract.Measurement, language)} of {contract.ObjectName} is:";

    internal static string FormatSolution(GeometryQuizContract contract, string? lead = null, CultureInfo? culture = null)
    {
        var reasoning = contract.Reasoning!;
        string lines = string.Join(Environment.NewLine, reasoning.Steps.Select(step =>
            $"{step.Label}: {QuizMathExpressionFormatter.Format(step.Expression)} = {step.Value} {step.Unit}"));
        string answer = reasoning.Language == AppLanguage.Vietnamese ? "Đáp số" : "Answer";
        string formattedAnswer = contract.CorrectAnswer.ToString("N0", culture ?? CultureInfo.InvariantCulture);
        return (lines.Length == 0 ? "" : lines + Environment.NewLine) +
            (lead ?? SolutionLead(contract, reasoning.Language)) + Environment.NewLine +
            $"{contract.SubstitutionExpression} = {formattedAnswer} {contract.AnswerUnit}" + Environment.NewLine +
            $"{answer}: {formattedAnswer} {contract.AnswerUnit}";
    }

}
