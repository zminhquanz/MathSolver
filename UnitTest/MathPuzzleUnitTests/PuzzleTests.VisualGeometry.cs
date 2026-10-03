using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckVisualGeometryVariety()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in new[] { ElementaryQuizType.ClassifyAngle, ElementaryQuizType.ParallelLines, ElementaryQuizType.PerpendicularLines })
        {
            bool vi = language == AppLanguage.Vietnamese;
            var angles = new HashSet<decimal>();
            var rotations = new HashSet<decimal>();
            var relations = new HashSet<string>();
            var angleKinds = new HashSet<string>();
            int pairTasks = 0, noPairTasks = 0;
            for (int seed = 0; seed < 80; seed++)
            {
                var mode = Enum.GetValues<ArithmeticQuizMode>()[seed % 3];
                var question = new ElementaryQuizGenerator(new Random(seed)).Generate(
                    mode, QuizProblemKind.VisualGeometry, type, language, tier);
                var contract = question.ElementaryProblem!;
                var visual = contract.Visual!;
                string expected;
                if (type == ElementaryQuizType.ClassifyAngle)
                {
                    decimal degrees = visual.Values[0];
                    Require(degrees > 0 && degrees <= 180 && visual.Labels.Count == 3, "Invalid angle or missing vertex labels.");
                    expected = degrees < 90 ? (vi ? "Góc Nhọn" : "Acute") : degrees == 90 ? (vi ? "Góc Vuông" : "Right")
                        : degrees < 180 ? (vi ? "Góc Tù" : "Obtuse") : vi ? "Góc Bẹt" : "Straight";
                    angles.Add(degrees);
                    angleKinds.Add(expected);
                    rotations.Add(visual.RotationDegrees);
                    Require(contract.Facts.Single() == degrees.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "The displayed angle measure differs from the given facts.");
                }
                else
                {
                    var lines = visual.Lines!;
                    Require(lines.Count >= 2 && lines.Count <= 5 && lines.Select(line => line.Label).Distinct().Count() == lines.Count,
                        "Line tasks require distinct labels and explicit drawing directions.");
                    rotations.Add(lines[0].DirectionDegrees % 180);
                    if (lines.Count == 2)
                    {
                        string relation = Relation(lines[0], lines[1]);
                        Require(visual.Kind == relation, "The line drawing kind contradicts its actual directions.");
                        if (relation == "parallel")
                            Require(lines[0].OffsetRatio != lines[1].OffsetRatio, "Parallel lines must not coincide.");
                        relations.Add(relation);
                        expected = relation switch
                        {
                            "parallel" => vi ? "Song Song" : "Parallel",
                            "perpendicular" => vi ? "Vuông Góc" : "Perpendicular",
                            _ => vi ? "Cắt Nhau Nhưng Không Vuông Góc" : "Intersecting but not perpendicular"
                        };
                    }
                    else
                    {
                        pairTasks++;
                        Require((int)tier >= 3, "Pair selection should not replace beginner two-line tasks.");
                        Require(lines.Count == (int)tier, "The line search does not grow with its tier.");
                        string target = type == ElementaryQuizType.ParallelLines ? "parallel" : "perpendicular";
                        var matches = (from left in Enumerable.Range(0, lines.Count)
                            from right in Enumerable.Range(left + 1, lines.Count - left - 1)
                            where Relation(lines[left], lines[right]) == target
                            select (Left: left, Right: right)).ToArray();
                        Require(matches.Length <= 1, "A pair-selection question must not have two correct pairs.");
                        if (matches.Length == 0)
                        {
                            noPairTasks++;
                            expected = target == "parallel" ? (vi ? "Không Có Cặp Song Song" : "No parallel pair")
                                : vi ? "Không Có Cặp Vuông Góc" : "No perpendicular pair";
                            Require(ElementaryEssayValidator.CheckAnswers(question, vi ? "không có" : "none"),
                                "The short no-pair answer should be accepted.");
                        }
                        else
                        {
                            var pair = matches[0];
                            string left = lines[pair.Left].Label, right = lines[pair.Right].Label;
                            expected = vi ? $"Đường {left} Và Đường {right}" : $"Lines {left} and {right}";
                            Require(ElementaryEssayValidator.CheckAnswers(question, vi ? $"{right} và {left}" : $"{right} and {left}"),
                                "Reversing the two line names must remain a valid answer.");
                            if (target == "parallel")
                                Require(lines[pair.Left].OffsetRatio != lines[pair.Right].OffsetRatio,
                                    "A highlighted parallel pair must not be coincident.");
                        }
                    }
                }
                Require(contract.AnswerText == expected, "The answer contradicts the independently classified drawing.");
                Require(contract.ChoiceTexts!.Count == 4 && contract.ChoiceTexts.Distinct().Count() == 4 &&
                    contract.ChoiceTexts.Count(choice => ElementaryEssayValidator.CheckAnswers(question, choice)) == 1,
                    "Visual geometry must have four distinct choices and exactly one correct answer.");
                Require(ElementaryEssayValidator.CheckAnswers(question, contract.PresentedText) == question.PresentedEquationIsCorrect,
                    "Visual geometry true/false grading disagrees with the drawing.");
                var parts = EssayCombinedInputParser.Parse(contract.SolutionText, contract.RequiresSolution, true);
                Require(validator.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                    "The visual geometry worked answer was rejected.");
                count++;
            }
            Require(rotations.Count >= ((int)tier == 1 ? 2 : (int)tier == 2 ? 4 : 8), "Visual geometry drawings still use a fixed orientation.");
            if (type == ElementaryQuizType.ClassifyAngle)
                Require(angles.Count >= ((int)tier == 1 ? 4 : 13) && angleKinds.Count == ((int)tier == 1 ? 2 : 4),
                    "Angle measures and classifications do not match their learning tier.");
            else
            {
                if ((int)tier <= 3)
                    Require(relations.SetEquals(new[] { "parallel", "perpendicular", "intersecting" }), "Two-line answers remain constant.");
                if ((int)tier >= 3) Require(pairTasks > 0 && noPairTasks > 0 && noPairTasks < pairTasks,
                    "Higher tiers need both existing-pair and no-pair questions.");
            }
        }
        Console.WriteLine($"  Checked {count} varied angle/line drawings, independent geometry, choices and bilingual grading.");

        static string Relation(QuizVisualLine first, QuizVisualLine second)
        {
            double a = (double)first.DirectionDegrees * Math.PI / 180;
            double b = (double)second.DirectionDegrees * Math.PI / 180;
            double cross = Math.Cos(a) * Math.Sin(b) - Math.Sin(a) * Math.Cos(b);
            double dot = Math.Cos(a) * Math.Cos(b) + Math.Sin(a) * Math.Sin(b);
            return Math.Abs(cross) < 1e-8 ? "parallel" : Math.Abs(dot) < 1e-8 ? "perpendicular" : "intersecting";
        }
    }
}
