using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckSpatialPositionInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        var directions = new HashSet<int>();
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 20; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(mode,
                QuizProblemKind.VisualGeometry, ElementaryQuizType.SpatialPosition, language, tier);
            var contract = question.ElementaryProblem!;
            int position = (int)contract.Visual!.Values[0];
            directions.Add(position);
            string[] shortNames = language == AppLanguage.Vietnamese
                ? ["trái", "phải", "trên", "dưới"] : ["Left", "Right", "Above", "Below"];
            string[] fullNames = language == AppLanguage.Vietnamese
                ? ["Bên trái", "Bên phải", "Phía trên", "Phía dưới"] : shortNames;
            Require(contract.AnswerText == fullNames[position], "Spatial answer disagrees with the displayed direction.");
            foreach (string answer in new[] { shortNames[position], fullNames[position] })
            foreach (string input in new[] { answer, "Đáp số: " + answer, "Answer: " + answer,
                answer.ToUpperInvariant(), "  " + answer + "  ", answer + "." })
            {
                Require(grader.ValidateSubmission(question, input).IsCorrect,
                    "Spatial direction answer rejected: " + input);
                count++;
            }
            for (int wrong = 0; wrong < 4; wrong++)
            {
                if (wrong == position) continue;
                foreach (string answer in new[] { shortNames[wrong], fullNames[wrong],
                    shortNames[position] + " " + shortNames[wrong],
                    "Answer: " + shortNames[position] + "\nAnswer: " + shortNames[wrong] })
                    Require(!grader.ValidateSubmission(question, answer).IsCorrect,
                        "Wrong, ambiguous or conflicting spatial direction accepted: " + answer);
            }
            Require(!grader.ValidateSubmission(question, "").IsCorrect
                && !grader.ValidateSubmission(question, "không " + shortNames[position]).IsCorrect,
                "A blank or negated spatial direction was accepted.");
            Require(contract.ChoiceTexts!.Count(choice => ElementaryEssayValidator.CheckAnswers(question, choice)) == 1,
                "Short direction aliases made multiple choices correct.");
            Require(grader.ValidateSubmission(question, contract.SolutionText).IsCorrect,
                "Spatial position generated example was rejected.");
        }
        Require(directions.Count == 4, "Spatial input tests missed a direction.");
        var other = new ElementaryQuizGenerator(new Random(47)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.VisualGeometry, ElementaryQuizType.ClassifyAngle, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        foreach (string input in new[] { "trái", "phải", "trên", "dưới" })
            Require(!grader.ValidateSubmission(other, input).IsCorrect, "Spatial aliases affected an unrelated identification task.");
        Console.WriteLine($"  Checked {count} short/full spatial direction submissions across four directions, both languages, five stars and all modes; wrong, ambiguous and conflicting directions rejected.");
    }
}
