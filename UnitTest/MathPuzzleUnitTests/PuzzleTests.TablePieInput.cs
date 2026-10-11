using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckTablePieInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var type in new[] { ElementaryQuizType.ReadTable, ElementaryQuizType.ReadPieChart })
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var context in DataChartStoryContextCatalog.GetProfile(language))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 5; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).GenerateDataChart(ArithmeticQuizMode.Essay,
                type, language, tier, context.ContextId);
            var contract = question.ElementaryProblem!;
            string number = contract.Answers[0].Value.ToString();
            string answer = contract.AnswerText;
            var work = EssayCombinedInputParser.Parse(contract.SolutionText, false, true);
            Require(EssayAnswerValidator.IsResultOnlyPractice(question), type + " still requires a worked solution.");
            foreach (string final in new[] { number, answer })
            foreach (string input in new[] { final, "Đáp số: " + final, "Answer: " + final,
                "Kết quả = " + final, contract.Answers[0].Expression + " = " + final,
                work.Solution + "\n" + work.Equation + "\nAnswer: " + final })
            {
                Require(grader.ValidateSubmission(question, input).IsCorrect,
                    type + " rejected direct or worked input: " + input);
                count++;
            }
            Require(grader.ValidateSubmission(question, contract.SolutionText).IsCorrect,
                type + " rejected its full generated solution.");
            foreach (string input in new[] { "", "Kết quả =", "0", "0 " + contract.Answers[0].Unit,
                number + " wrongunit", "Answer: " + number + "\nAnswer: 0",
                work.Equation + "\nAnswer: 0" })
                Require(!grader.ValidateSubmission(question, input).IsCorrect,
                    type + " accepted a missing, wrong or conflicting final result: " + input);
            Require(grader.Validate(question, work.Solution, work.Equation, number).IsCorrect,
                type + " rejected a worked solution with a unitless final answer.");
            if (tier > CurriculumTier.OneStar)
                Require(!grader.Validate(question, work.Solution, "1 = " + answer, number).IsCorrect,
                    type + " stopped checking calculations during independent validation.");
        }
        Console.WriteLine($"  Checked {count} direct and worked table/pie-chart submissions with and without units across both languages, all contexts and five stars; wrong results, units and conflicting answers rejected.");
    }
}
