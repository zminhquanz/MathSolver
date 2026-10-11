using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckTimeResultInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in new[] { ElementaryQuizType.ElapsedTime, ElementaryQuizType.TimeAddition,
            ElementaryQuizType.Calendar })
        foreach (bool expandedStories in new[] { false, true })
        for (int seed = 0; seed < 20; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed), expandActivityStories: expandedStories)
                .Generate(ArithmeticQuizMode.Essay, QuizProblemKind.Time, type, language, tier);
            var contract = question.ElementaryProblem!;
            Require(EssayAnswerValidator.IsResultOnlyPractice(question),
                "Time practice still requires a worked solution: " + type);
            string answer = contract.AnswerText;
            var worked = EssayCombinedInputParser.Parse(contract.SolutionText, false, true);
            foreach (string input in new[] { answer, "Đáp số: " + answer, "Answer: " + answer,
                contract.Answers[0].Expression + " = " + answer, "Kết quả = " + answer,
                contract.SolutionText, worked.Equation, worked.Solution + "\n" + worked.Equation + "\n" + answer })
            {
                Require(grader.ValidateSubmission(question, input).IsCorrect,
                    "Direct or worked time answer rejected: " + type + "\n" + input);
                count++;
            }

            foreach (string input in new[] { "", contract.Answers[0].Expression, "Kết quả =",
                contract.Answers[0].Value.ToString(), answer + " kg",
                "0 " + contract.Answers[0].Unit, "Answer: " + answer + "\nAnswer: 0 " + contract.Answers[0].Unit,
                worked.Equation + "\nAnswer: 0 " + contract.Answers[0].Unit })
                Require(!grader.ValidateSubmission(question, input).IsCorrect,
                    "Incomplete, wrong, unitless or conflicting time answer passed: " + type + "\n" + input);

            Require(grader.Validate(question, worked.Solution, worked.Equation, worked.Answer).IsCorrect,
                "Generated time solutions lost independent calculation validation.");
            Require(!grader.Validate(question, null, "1 = " + answer, answer).IsCorrect,
                "Independent time validation stopped checking the calculated relation.");
        }

        // Read-clock interval problems remain distinct from the requested types.
        var clock = new ElementaryQuizGenerator(new Random(17)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.Time, ElementaryQuizType.ReadClock, AppLanguage.Vietnamese, CurriculumTier.FiveStars);
        Require(!EssayAnswerValidator.IsResultOnlyPractice(clock)
            && !grader.ValidateSubmission(clock, clock.ElementaryProblem!.AnswerText).IsCorrect
            && grader.ValidateSubmission(clock, clock.ElementaryProblem.SolutionText).IsCorrect,
            "The optional-work policy leaked into a different time subtype.");
        Console.WriteLine($"  Checked {count} direct and worked time answers across both languages, all stars and legacy/expanded contexts; units, wrong values and explicit final answers checked.");
    }
}
