using MathSolver.Models;

namespace MathSolver.Services;

public static class AverageIndirectSolutionFormatter
{
    public static string Format(AverageQuizContract contract, AppLanguage language, string? finalLead = null)
    {
        AverageIndirectData data = contract.IndirectData ??
            new(contract.Facts[0], contract.Facts[1], contract.Facts[2]);
        int second = data.FirstQuantity + data.Increase;
        int third = second - data.Decrease;
        bool vi = language == AppLanguage.Vietnamese;
        string unit = contract.AnswerUnit;
        string lead = finalLead ?? (vi ? $"Trung bình mỗi bạn ({unit}) là:" : $"The average per person ({unit}) is:");
        return string.Join(Environment.NewLine,
            vi ? $"Số {unit} của bạn thứ hai là:" : $"The amount for the second person ({unit}) is:",
            $"{data.FirstQuantity} + {data.Increase} = {second} {unit}",
            vi ? $"Số {unit} của bạn thứ ba là:" : $"The amount for the third person ({unit}) is:",
            $"{second} − {data.Decrease} = {third} {unit}",
            ElementaryWordProblemSolutionFormatter.NormalizeSolutionLeadPunctuation(lead),
            $"({data.FirstQuantity} + {second} + {third}) ÷ {data.PersonCount} = {contract.CorrectAnswer} {unit}",
            $"{(vi ? "Đáp số" : "Answer")}: {contract.CorrectAnswer} {unit}");
    }
}
