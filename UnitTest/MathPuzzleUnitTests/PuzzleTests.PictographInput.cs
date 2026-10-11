using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckPictographInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        bool checkedSixGifts = false;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var context in DataChartStoryContextCatalog.GetProfile(language))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 10; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).GenerateDataChart(ArithmeticQuizMode.Essay,
                ElementaryQuizType.ReadPictograph, language, tier, context.ContextId);
            var contract = question.ElementaryProblem!;
            Require(EssayAnswerValidator.IsResultOnlyPractice(question), "Pictographs still require a worked solution.");
            string answer = contract.AnswerText;
            string number = contract.Answers[0].Value.ToString();
            var work = EssayCombinedInputParser.Parse(contract.SolutionText, false, true);
            foreach (string final in new[] { number, answer })
            foreach (string input in new[] { final, "Đáp số: " + final, "Answer: " + final,
                "Kết quả = " + final, contract.Answers[0].Expression + " = " + final,
                work.Solution + "\n" + work.Equation + "\nAnswer: " + final })
            {
                Require(grader.ValidateSubmission(question, input).IsCorrect,
                    "Direct or worked pictograph answer rejected: " + input);
                count++;
            }
            Require(grader.ValidateSubmission(question, contract.SolutionText).IsCorrect
                && grader.ValidateSubmission(question, work.Equation).IsCorrect,
                "Generated worked pictograph submissions were rejected.");
            foreach (string input in new[] { "", contract.Answers[0].Expression, "Kết quả =",
                "0", "0 " + contract.Answers[0].Unit,
                contract.Answers[0].Value + " wrongunit", "Answer: " + answer + "\nAnswer: 0 " + contract.Answers[0].Unit,
                work.Equation + "\nAnswer: 0 " + contract.Answers[0].Unit,
                "Answer: " + number + "\nAnswer: 0" })
                Require(!grader.ValidateSubmission(question, input).IsCorrect,
                    "Wrong, missing or conflicting pictograph answer accepted: " + input);
            Require(grader.Validate(question, work.Solution, work.Equation, work.Answer).IsCorrect,
                "Generated pictograph solutions failed independent validation.");
            Require(grader.Validate(question, work.Solution, work.Equation, number).IsCorrect,
                "Worked pictograph solution rejected a unitless final answer.");
            Require(!grader.Validate(question, null, "1 = " + answer, answer).IsCorrect,
                "Independent validation stopped checking the legend and arithmetic.");
            if (tier == CurriculumTier.TwoStars && contract.Answers[0].Unit == "gói quà"
                && contract.Answers[0].Value == new ReducedFraction(6, 1))
            {
                Require(contract.Visual!.PictographKey == 2
                    && grader.ValidateSubmission(question, "6").IsCorrect
                    && grader.ValidateSubmission(question, "6 gói quà").IsCorrect
                    && grader.ValidateSubmission(question, "3 × 2 = 6").IsCorrect
                    && grader.ValidateSubmission(question, "3 × 2 = 6 gói quà").IsCorrect,
                    "The reported three icons with two gifts each was rejected.");
                checkedSixGifts = true;
            }
        }
        Require(checkedSixGifts, "No six-gift pictograph was checked.");

        foreach (var type in new[] { ElementaryQuizType.ChartTotal, ElementaryQuizType.ChartDifference })
        {
            var question = new ElementaryQuizGenerator(new Random(47)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Data, type, AppLanguage.Vietnamese, CurriculumTier.FiveStars);
            Require(!EssayAnswerValidator.IsResultOnlyPractice(question)
                && !grader.ValidateSubmission(question, question.ElementaryProblem!.AnswerText).IsCorrect,
                "The pictograph optional-work policy changed a different chart subtype.");
        }
        Console.WriteLine($"  Checked {count} direct and worked pictograph submissions with and without units across all contexts, both languages and five stars; legend-based calculations, wrong units and incorrect results checked.");
    }
}
