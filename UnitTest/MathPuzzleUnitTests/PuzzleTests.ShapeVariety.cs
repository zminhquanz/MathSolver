using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Text.Json;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckShapeProblemVariety()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int checkedCount = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in new[] { ElementaryQuizType.CountSides, ElementaryQuizType.RectangleSide, ElementaryQuizType.CompositeArea })
        {
            var generator = new ElementaryQuizGenerator(new Random(280));
            int level = (int)tier;
            int bankSize = type == ElementaryQuizType.CountSides ? (level == 1 ? 3 : 4)
                : type == ElementaryQuizType.RectangleSide ? (level == 1 ? 3 : 4)
                : level <= 2 ? 2 : level == 3 ? 3 : 4;
            var contexts = new HashSet<string>();
            var cycle = new HashSet<string>();
            var units = new HashSet<string>();
            var rotations = new HashSet<decimal>();
            var counts = new HashSet<int>();
            var questions = new HashSet<string>();
            string? previous = null;
            for (int iteration = 0; iteration < 70; iteration++)
            {
                var mode = Enum.GetValues<ArithmeticQuizMode>()[iteration % 3];
                var question = generator.Generate(mode, QuizProblemKind.VisualGeometry, type, language, tier);
                var contract = question.ElementaryProblem!;
                var visual = contract.Visual!;
                string id = visual.ScenarioId!;
                Require(visual.Polygons is { Count: > 0 }, "Every new shape task needs an actual polygon drawing.");
                Require(id != previous && cycle.Add(id), "A shape repeated before its rotation completed.");
                previous = id;
                if (cycle.Count == bankSize) cycle.Clear();
                contexts.Add(id);
                units.Add(visual.Unit);
                rotations.Add(visual.RotationDegrees);
                questions.Add(contract.ProblemText);
                foreach (var polygon in visual.Polygons!)
                    CheckSimplePolygon(polygon);
                ReducedFraction expected;
                if (type == ElementaryQuizType.CountSides)
                {
                    var polygon = visual.Polygons.Single();
                    int sideCount = polygon.Vertices.Count;
                    int independentCount = id switch
                    {
                        "triangle" => 3,
                        "square" or "rectangle" or "trapezoid" or "parallelogram" => 4,
                        "pentagon" => 5,
                        "hexagon" or "concave-l" => 6,
                        "octagon" or "concave-step" => 8,
                        _ => throw new InvalidOperationException("Unknown counting shape.")
                    };
                    Require(sideCount == independentCount && polygon.Vertices.Select(point => (point.X, point.Y)).Distinct().Count() == sideCount,
                        "Counting shape contains repeated vertices or the wrong number of sides.");
                    // A straight continuation is not an extra corner for these counting questions.
                    for (int vertex = 0; vertex < sideCount; vertex++)
                        Require(Cross(polygon.Vertices[vertex], polygon.Vertices[(vertex + 1) % sideCount],
                            polygon.Vertices[(vertex + 2) % sideCount]) != 0, "A collinear vertex created an ambiguous side count.");
                    expected = new(sideCount, 1);
                    counts.Add(sideCount);
                    Require(!contract.RequiresSolution && contract.Answers.Single().Unit == "", "Counting sides retains its short numeric response.");
                }
                else if (type == ElementaryQuizType.RectangleSide)
                {
                    decimal[] facts = contract.Facts.Select(value => decimal.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                    decimal length = id switch
                    {
                        "rectangle-area" => facts[0] / facts[1],
                        "rectangle-perimeter" or "parallelogram-perimeter" => (facts[0] - 2 * facts[1]) / 2,
                        "square-perimeter" or "rhombus-perimeter" => facts[0] / 4,
                        "triangle-perimeter" => facts[0] - (facts[1] + facts[2]),
                        "split-side" or "l-shape-side" => facts[0] - facts[1],
                        "split-side-perimeter" => facts[0] / 2 - facts[1] - facts[2],
                        _ => throw new InvalidOperationException("Unknown missing-side shape.")
                    };
                    expected = new((int)length, 1);
                    Require(length > 0 && length == decimal.Truncate(length), "Missing-side data must have a positive integer answer.");
                    Require(visual.Annotations!.Count(label => label.Text.Contains('?')) == 1,
                        "A missing-side drawing must mark exactly one unknown measurement.");
                    Require(contract.RequiresSolution && contract.Answers.Single().Unit == visual.Unit,
                        "Missing-side solution/unit requirements changed.");
                    if (id == "triangle-perimeter")
                    {
                        decimal first = facts[1], second = facts[2];
                        Require(first + second > length && first + length > second && second + length > first,
                            "A missing-side triangle violated a triangle inequality.");
                    }
                    if (id == "split-side")
                    {
                        var points = visual.Polygons.Single().Vertices;
                        var left = points.Single(point => point.Label == "A");
                        var mid = points.Single(point => point.Label == "M");
                        var right = points.Single(point => point.Label == "N");
                        Require(Cross(left, mid, right) == 0 && left.X < mid.X && mid.X < right.X,
                            "M must actually lie between A and N in the illustration.");
                    }
                    if (id == "l-shape-side")
                    {
                        var points = visual.Polygons.Single().Vertices;
                        var left = points.Single(point => point.Label == "B");
                        var right = points.Single(point => point.Label == "C");
                        Require(left.Y == right.Y, "The requested BC side must be horizontal.");
                    }
                }
                else
                {
                    // Shoelace integration of the drawn regions is independent of the chosen formula.
                    decimal area = visual.Polygons.Sum(part => PolygonArea(part) * (part.IsCutout ? -1 : 1));
                    Require(area > 0 && area == decimal.Truncate(area), "Composite drawings must have a positive integer area.");
                    expected = new((int)area, 1);
                    var solids = visual.Polygons.Where(part => !part.IsCutout).ToArray();
                    for (int first = 0; first < solids.Length; first++)
                    for (int second = first + 1; second < solids.Length; second++)
                        Require(!BoxesOverlap(solids[first], solids[second]), "Composite pieces overlap despite a non-overlap premise.");
                    if (id is "frame" or "two-openings")
                    {
                        var outer = visual.Polygons.Single(part => !part.IsCutout);
                        var openings = visual.Polygons.Where(part => part.IsCutout).ToArray();
                        if (openings.Length == 2) Require(!BoxesOverlap(openings[0], openings[1]), "Openings overlap.");
                        foreach (var inner in openings)
                        Require(inner.Vertices.All(point => point.X > outer.Vertices.Min(p => p.X) && point.X < outer.Vertices.Max(p => p.X)
                            && point.Y > outer.Vertices.Min(p => p.Y) && point.Y < outer.Vertices.Max(p => p.Y)),
                            "The frame opening must lie strictly inside the outer rectangle.");
                    }
                    Require(contract.RequiresSolution && contract.Answers.Single().Unit == visual.Unit + "²",
                        "Composite answers need an area unit, not a length unit.");
                }
                Require(contract.Answers.Single().Value == expected, "The answer disagrees with independent geometry.");
                Require(contract.ChoiceTexts!.Count == 4 && contract.ChoiceTexts.Distinct().Count() == 4 &&
                    contract.ChoiceTexts.Count(choice => ElementaryEssayValidator.CheckAnswers(question, choice)) == 1,
                    "Shape choices must have exactly one correct result.");
                Require(ElementaryEssayValidator.CheckAnswers(question, contract.PresentedText) == question.PresentedEquationIsCorrect,
                    "True/false shape grading disagrees with the answer.");
                var parts = EssayCombinedInputParser.Parse(contract.SolutionText, contract.RequiresSolution, true);
                Require(grader.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                    "Shape example was rejected: " + contract.SolutionText);
                if (type != ElementaryQuizType.CountSides)
                {
                    Require(!grader.Validate(question, parts.Solution, parts.Equation, expected.ToString()).IsCorrect,
                        "A measurement answer without its unit must be rejected.");
                    string wrongUnit = type == ElementaryQuizType.CompositeArea ? visual.Unit : visual.Unit + "²";
                    Require(!ElementaryEssayValidator.CheckAnswers(question, expected + " " + wrongUnit),
                        "Length and area units must not be interchangeable.");
                }
                checkedCount++;
            }
            Require(contexts.Count == bankSize, "The shape bank failed to cover every scenario.");
            Require(questions.Count > (type == ElementaryQuizType.CountSides ? 2 : 50), "Question wording/data remains too repetitive.");
            if (type == ElementaryQuizType.CountSides)
                Require(rotations.Count == (level <= 2 ? 1 : level == 3 ? 4 : 8) && counts.SetEquals(level switch
                {
                    1 => new[] { 3, 4 }, 2 => new[] { 4 }, 3 => new[] { 4, 5, 6 },
                    4 => new[] { 5, 6, 8 }, _ => new[] { 6, 8 }
                }), "Counting shapes missed a tier-appropriate orientation or side count.");
            else Require(units.SetEquals(new[] { "cm", "dm", "m" }), "Shape scenarios missed a measurement unit.");
        }
        CheckAlternativeShapeWork(grader);
        Console.WriteLine($"  Checked {checkedCount} varied shape contracts, polygons, independent answers, units and grading.");
    }

    private static void CheckAlternativeShapeWork(EssayAnswerValidator grader)
    {
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            var generator = new ElementaryQuizGenerator(new Random(53));
            for (int iteration = 0; iteration < 14; iteration++)
            {
                var question = generator.Generate(ArithmeticQuizMode.Essay, QuizProblemKind.VisualGeometry,
                    ElementaryQuizType.CompositeArea, language, CurriculumTier.ThreeStars);
                var contract = question.ElementaryProblem!;
                var facts = contract.Facts;
                string alternative = contract.Visual!.ScenarioId switch
                {
                    "corner-cutout" => $"({facts[0]}-{facts[2]})*{facts[3]}+{facts[0]}*({facts[1]}-{facts[3]})",
                    "frame" => $"{facts[0]}*({facts[1]}-{facts[3]})+({facts[0]}-{facts[2]})*{facts[3]}",
                    "rectangle-triangle" => $"{facts[1]}*({facts[0]}+{facts[2]}/2)",
                    "two-rectangles" => $"{facts[0]}*({facts[1]}+{facts[2]})",
                    "square-rectangle" => $"{facts[2]}*{facts[1]}+{facts[0]}*{facts[0]}",
                    "t-shape" => $"{facts[2]}*{facts[3]}+{facts[0]}*{facts[1]}",
                    _ => $"{facts[0]}*({facts[1]}+{facts[2]}+{facts[3]})"
                };
                foreach (var sourceQuestion in new[] { question, question with { WordProblem = new(contract.ProblemText, "", contract.Answers[0].Unit, "") } })
                    Require(grader.Validate(sourceQuestion, contract.Answers[0].Label + ":",
                        alternative + " = " + contract.AnswerText, contract.AnswerText).IsCorrect,
                        "A valid decomposition, reordered sum, or factored area expression was rejected: " + alternative);
            }
        }
    }

    private static decimal Cross(QuizVisualPoint a, QuizVisualPoint b, QuizVisualPoint c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static decimal PolygonArea(QuizVisualPolygon polygon)
    {
        decimal twiceArea = 0;
        for (int index = 0; index < polygon.Vertices.Count; index++)
        {
            var a = polygon.Vertices[index]; var b = polygon.Vertices[(index + 1) % polygon.Vertices.Count];
            twiceArea += a.X * b.Y - a.Y * b.X;
        }
        return Math.Abs(twiceArea) / 2;
    }

    private static bool BoxesOverlap(QuizVisualPolygon a, QuizVisualPolygon b) =>
        Math.Max(a.Vertices.Min(p => p.X), b.Vertices.Min(p => p.X)) < Math.Min(a.Vertices.Max(p => p.X), b.Vertices.Max(p => p.X)) &&
        Math.Max(a.Vertices.Min(p => p.Y), b.Vertices.Min(p => p.Y)) < Math.Min(a.Vertices.Max(p => p.Y), b.Vertices.Max(p => p.Y));

    private static void CheckSimplePolygon(QuizVisualPolygon polygon)
    {
        var vertices = polygon.Vertices;
        Require(vertices.Count >= 3 && PolygonArea(polygon) > 0, "A drawn region has no area.");
        for (int left = 0; left < vertices.Count; left++)
        for (int right = left + 2; right < vertices.Count; right++)
        {
            if (left == 0 && right == vertices.Count - 1) continue;
            var a = vertices[left]; var b = vertices[(left + 1) % vertices.Count];
            var c = vertices[right]; var d = vertices[(right + 1) % vertices.Count];
            Require(!(Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0),
                "A polygon boundary crosses itself.");
        }
    }
}
