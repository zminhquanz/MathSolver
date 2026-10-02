using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Adapts indirect averages to the common derivation grader; legacy sentence/answer checks remain intact.</summary>
internal static class AverageIndirectEssayValidator
{
    internal static (bool IsCorrect, EssayAnswerError Error) Validate(
        ArithmeticQuizQuestion question, string? equations, out IReadOnlyList<EssayStepValidationResult> steps)
    {
        AverageQuizContract average = question.AverageProblem!;
        var data = average.IndirectData ?? (average.Facts.Count == 3
            ? new(average.Facts[0], average.Facts[1], average.Facts[2]) : null);
        if (data is null || data.PersonCount != 3 || string.IsNullOrWhiteSpace(equations))
        {
            steps = [];
            return (false, EssayAnswerError.InvalidEquationFormat);
        }
        var answer = new ElementaryAnswer(average.SubjectName, new(average.CorrectAnswer, 1),
            average.AnswerUnit, average.EquationText.Split('=')[0].Trim());
        var contract = new ElementaryQuizContract(QuizProblemKind.Average, default, AppLanguageManager.CurrentLanguage,
            average.ProblemText, average.SolutionText,
            [data.FirstQuantity.ToString(CultureInfo.InvariantCulture), data.Increase.ToString(CultureInfo.InvariantCulture), data.Decrease.ToString(CultureInfo.InvariantCulture)],
            ["0", "1", "2", "3"], [answer], RequiresSolution: false);
        var result = ElementaryEssayValidator.Validate(question with { ElementaryProblem = contract },
            null, equations, ElementaryQuizContract.FormatAnswer(answer));
        steps = result.Steps;
        return (result.EquationIsCorrect, result.EquationError);
    }

}
