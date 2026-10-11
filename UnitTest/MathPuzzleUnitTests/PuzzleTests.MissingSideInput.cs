using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMissingSideInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        var scenarios = new HashSet<string>();
        var units = new HashSet<string>();
        int count = 0;
        bool checkedThreeMetres = false;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 20; seed++)
        {
            var generator = new ElementaryQuizGenerator(new Random(seed));
            for (int iteration = 0; iteration < 9; iteration++)
            {
                var question = generator.Generate(ArithmeticQuizMode.Essay, QuizProblemKind.VisualGeometry,
                    ElementaryQuizType.RectangleSide, language, tier);
                var contract = question.ElementaryProblem!;
                Require(EssayAnswerValidator.IsResultOnlyPractice(question), "Missing sides still require a worked solution.");
                scenarios.Add(contract.Visual!.ScenarioId!);
                units.Add(contract.Answers[0].Unit);
                string number = contract.Answers[0].Value.ToString();
                string answer = contract.AnswerText;
                string compact = answer.Replace(" ", "");
                var work = EssayCombinedInputParser.Parse(contract.SolutionText, false, true);
                foreach (string final in new[] { number, answer, compact })
                foreach (string input in new[] { final, "Đáp số: " + final, "Answer: " + final,
                    "Kết quả = " + final, contract.Answers[0].Expression + " = " + final,
                    work.Solution + "\n" + work.Equation + "\nAnswer: " + final })
                {
                    Require(grader.ValidateSubmission(question, input).IsCorrect,
                        "Direct or worked missing-side result rejected: " + input);
                    count++;
                }
                Require(grader.ValidateSubmission(question, contract.SolutionText).IsCorrect,
                    "Full generated missing-side solution was rejected.");
                foreach (string input in new[] { "", "Kết quả =", contract.Answers[0].Expression + " =",
                    "0", "0 " + contract.Answers[0].Unit, number + " kg", number + contract.Answers[0].Unit + "²",
                    "Answer: " + number + "\nAnswer: 0", work.Equation + "\nAnswer: 0" })
                    Require(!grader.ValidateSubmission(question, input).IsCorrect,
                        "Missing, wrong, conflicting or dimensionally incorrect missing-side answer accepted: " + input);
                Require(grader.Validate(question, work.Solution, work.Equation, number).IsCorrect,
                    "Worked missing-side solution rejected a unitless final answer.");
                Require(!grader.Validate(question, work.Solution, "1 = " + answer, number).IsCorrect,
                    "Independent missing-side validation stopped checking arithmetic.");
                if (number == "3" && contract.Answers[0].Unit == "m")
                {
                    foreach (string input in new[] { "3", "3m", "3 m" })
                        Require(grader.ValidateSubmission(question, input).IsCorrect,
                            "Reported three-metre input rejected: " + input);
                    checkedThreeMetres = true;
                }
            }
        }
        Require(scenarios.Count == 9 && units.SetEquals(new[] { "cm", "dm", "m" }) && checkedThreeMetres,
            "Missing-side input tests did not cover every scenario, unit and the three-metre case.");
        var area = new ElementaryQuizGenerator(new Random(47)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.VisualGeometry, ElementaryQuizType.CompositeArea, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        Require(!EssayAnswerValidator.IsResultOnlyPractice(area)
            && !grader.ValidateSubmission(area, area.ElementaryProblem!.AnswerText).IsCorrect
            && !ElementaryEssayValidator.CheckAnswers(area, area.ElementaryProblem.Answers[0].Value.ToString()),
            "Missing-side optional work/units changed composite-area grading.");
        Console.WriteLine($"  Checked {count} direct and worked missing-side submissions with optional units across nine scenarios, three length units, both languages and five stars; wrong results, units and conflicts checked.");
    }
}
