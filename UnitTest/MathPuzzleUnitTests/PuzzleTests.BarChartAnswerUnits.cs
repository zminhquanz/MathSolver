using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckBarChartAnswerUnits()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        bool checkedTenPoints = false;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var context in DataChartStoryContextCatalog.GetProfile(language))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 5; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).GenerateDataChart(ArithmeticQuizMode.Essay,
                ElementaryQuizType.ReadBarChart, language, tier, context.ContextId);
            var contract = question.ElementaryProblem!;
            string number = contract.Answers[0].Value.ToString();
            var work = EssayCombinedInputParser.Parse(contract.SolutionText, contract.RequiresSolution, true);
            Require(ElementaryEssayValidator.CheckAnswers(question, number), "Bar chart rejected a unitless answer: " + number);
            Require(ElementaryEssayValidator.CheckAnswers(question, contract.AnswerText), "Bar chart lost its answer with a unit.");
            foreach (string answer in new[] { number, contract.AnswerText })
            {
                Require(grader.Validate(question, work.Solution, work.Equation, answer).IsCorrect,
                    "Worked chart reading rejected an optional answer unit.");
                foreach (string input in new[] { answer, "Đáp số: " + answer, "Answer: " + answer })
                {
                    var result = grader.ValidateSubmission(question, input);
                    Require(result.IsCorrect == !contract.RequiresSolution,
                        "Bar chart optional unit changed the requirement to infer hidden data: " + input);
                    count++;
                }
            }
            Require(!ElementaryEssayValidator.CheckAnswers(question, "0"), "Bar chart accepted a wrong unitless value.");
            Require(!ElementaryEssayValidator.CheckAnswers(question, number + " wrongunit"), "Bar chart accepted an incorrect explicit unit.");
            Require(!grader.ValidateSubmission(question, "Answer: " + number + "\nAnswer: 0").IsCorrect,
                "A correct chart reading hid a conflicting final answer.");
            if (number == "10" && contract.Answers[0].Unit == "điểm" && !contract.RequiresSolution)
            {
                Require(grader.ValidateSubmission(question, "10").IsCorrect
                    && grader.ValidateSubmission(question, "10 điểm").IsCorrect,
                    "The reported ten-point chart rejected either answer spelling.");
                checkedTenPoints = true;
            }
        }
        Require(checkedTenPoints, "No direct ten-point bar chart was checked.");

        // Calculated chart totals and differences still require their answer units.
        foreach (var type in new[] { ElementaryQuizType.ChartTotal,
            ElementaryQuizType.ChartDifference })
        {
            var question = new ElementaryQuizGenerator(new Random(47)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Data, type, AppLanguage.Vietnamese, CurriculumTier.OneStar);
            Require(!ElementaryEssayValidator.CheckAnswers(question, question.ElementaryProblem!.Answers[0].Value.ToString()),
                "Optional bar-chart units affected a different subtype: " + type);
        }
        Console.WriteLine($"  Checked {count} chart answers with and without units across all contexts, languages and stars; wrong values, explicit units and hidden-data reasoning checked.");
    }
}
