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
        string lead = finalLead ?? (QuizContentCatalog.Text(language, "AverageIndirectSolutionFormatter.Format.001", ("unit", $"{unit}")));
        return string.Join(Environment.NewLine,
            QuizContentCatalog.Text(language, "AverageIndirectSolutionFormatter.Format.002", ("unit", $"{unit}")),
            $"{data.FirstQuantity} + {data.Increase} = {second} {unit}",
            QuizContentCatalog.Text(language, "AverageIndirectSolutionFormatter.Format.003", ("unit", $"{unit}")),
            $"{second} − {data.Decrease} = {third} {unit}",
            ElementaryWordProblemSolutionFormatter.NormalizeSolutionLeadPunctuation(lead),
            $"({data.FirstQuantity} + {second} + {third}) ÷ {data.PersonCount} = {contract.CorrectAnswer} {unit}",
            $"{(QuizContentCatalog.Text(language, "AverageIndirectSolutionFormatter.Format.004"))}: {contract.CorrectAnswer} {unit}");
    }
}
