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
            ("rectangle" or "rectangular_prism", "a") => QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.001"),
            ("rectangle" or "rectangular_prism", "b") => QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.002"),
            ("triangle" or "parallelogram", "a") => QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.003"),
            ("trapezoid", "a") => QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.004"),
            ("trapezoid", "b") => QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.005"),
            (_, "h") => QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.006"),
            (_, "r") => QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.007"),
            (_, "d1") => QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.008"),
            (_, "d2") => QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.009"),
            _ => (QuizContentCatalog.Text(language, "GeometryReasoningText.DimensionName.010")) + key
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
        QuizContentCatalog.Text(language, "GeometryReasoningText.SolutionLead.011", ("MeasurementName_contract_Measurement_language", $"{MeasurementName(contract.Measurement, language)}"), ("contract_ObjectName", $"{contract.ObjectName}"));

    internal static string FormatSolution(GeometryQuizContract contract, string? lead = null, CultureInfo? culture = null)
    {
        var reasoning = contract.Reasoning!;
        string lines = string.Join(Environment.NewLine, reasoning.Steps.Select(step =>
            $"{step.Label}: {QuizMathExpressionFormatter.Format(step.Expression)} = {step.Value} {step.Unit}"));
        string answer = QuizContentCatalog.Text(reasoning.Language, "GeometryReasoningText.FormatSolution.012");
        string formattedAnswer = contract.CorrectAnswer.ToString("N0", culture ?? CultureInfo.InvariantCulture);
        return (lines.Length == 0 ? "" : lines + Environment.NewLine) +
            (lead ?? SolutionLead(contract, reasoning.Language)) + Environment.NewLine +
            $"{contract.SubstitutionExpression} = {formattedAnswer} {contract.AnswerUnit}" + Environment.NewLine +
            $"{answer}: {formattedAnswer} {contract.AnswerUnit}";
    }

}
