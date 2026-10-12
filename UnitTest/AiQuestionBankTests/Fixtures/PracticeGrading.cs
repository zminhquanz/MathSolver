using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

internal static class PracticeGrading
{
    internal static void CheckWorkedSolution(ArithmeticQuizQuestion question, string solution, string diagnostic)
    {
        var input = EssayCombinedInputParser.Parse(solution, true, preserveAllCalculations: true);
        var result = new EssayAnswerValidator(new BasicArithmeticEngine())
            .Validate(question, input.Solution, input.Equation, input.Answer);
        Check(result.IsCorrect, diagnostic + "\n" + solution + "\n" + result);
    }
}
