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
            $"{step.Label}: {step.Expression} = {step.Value} {step.Unit}"));
        string answer = reasoning.Language == AppLanguage.Vietnamese ? "Đáp số" : "Answer";
        string formattedAnswer = contract.CorrectAnswer.ToString("N0", culture ?? CultureInfo.InvariantCulture);
        return (lines.Length == 0 ? "" : lines + Environment.NewLine) +
            (lead ?? SolutionLead(contract, reasoning.Language)) + Environment.NewLine +
            $"{contract.SubstitutionExpression} = {formattedAnswer} {contract.AnswerUnit}" + Environment.NewLine +
            $"{answer}: {formattedAnswer} {contract.AnswerUnit}";
    }

    internal static string BuildPrompt(GeometryQuizContract contract, AppLanguage language)
    {
        var reasoning = contract.Reasoning!;
        return language == AppLanguage.Vietnamese
            ? $"Viết bằng tiếng Việt một bối cảnh ngắn tự nhiên cho bài hình học sau. Giữ đúng đồ vật {contract.ObjectName}, hình {contract.ShapeName} và đại lượng cần tìm.\n" +
              "Các câu định lượng dưới đây đã được C# kiểm tra. Giữ nguyên từng câu dữ kiện, quan hệ và đơn vị. Có thể đổi thứ tự các câu dữ kiện độc lập, thêm lời dẫn không chứa số và viết lại câu hỏi cùng đại lượng; không đưa kích thước suy ra vào đề:\n" +
              reasoning.ProblemText + "\nKhông tính sẵn kết quả. solution_lead chỉ là câu dẫn lời giải, không chứa số hoặc phép tính. " +
              $"Chỉ trả JSON bốn trường problem_text, subject_name, answer_unit (phải là {contract.AnswerUnit}), solution_lead."
            : $"Write in English a brief natural context for this geometry problem. Preserve object {contract.ObjectName}, shape {contract.ShapeName}, and the requested measurement.\n" +
              "C# has checked the following quantitative sentences. Preserve each factual sentence, relationship and unit. You may reorder independent fact sentences, add a nonnumeric introduction and reword the question about the same measurement; never supply inferred dimensions:\n" +
              reasoning.ProblemText + "\nDo not precompute the result. solution_lead is a short sentence with no number or calculation. " +
              $"Return only four-field JSON: problem_text, subject_name, answer_unit (exactly {contract.AnswerUnit}), solution_lead.";
    }
}
