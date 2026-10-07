using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private sealed record ShapeTask(string ProblemText, string AnswerLabel, string Expression,
        string Unit, QuizVisualData Visual, string[] Facts, string[] Constants,
        bool RequiresSolution = true, string? Hint = null);

    private static QuizVisualPolygon ShapeRectangle(decimal x, decimal y, decimal width, decimal height,
        string label = "", bool cutout = false) =>
        new([new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)], label, cutout);

    private static string[] ShapeFacts(params decimal[] values) =>
        values.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();

    private ShapeTask CreateCountSidesTask(AppLanguage language, int level)
    {
        bool vi = language == AppLanguage.Vietnamese;
        int[] families = QuizDifficultyPolicy.ShapeFamilies(ElementaryQuizType.CountSides, level);
        int family = families[NextContextVariant(ElementaryQuizType.CountSides, language, $"shape-{level}", families.Length)];
        string id;
        QuizVisualPoint[] vertices;
        switch (family)
        {
            case 0: id = "triangle"; vertices = [new(0, 3), new(2, 0), new(4, 3)]; break;
            case 1: id = "square"; vertices = ShapeRectangle(0, 0, 3, 3).Vertices.ToArray(); break;
            case 2: id = "rectangle"; vertices = ShapeRectangle(0, 0, 5, 3).Vertices.ToArray(); break;
            case 3: id = "trapezoid"; vertices = [new(1, 0), new(4, 0), new(5, 3), new(0, 3)]; break;
            case 4: id = "parallelogram"; vertices = [new(1, 0), new(5, 0), new(4, 3), new(0, 3)]; break;
            case 5: id = "pentagon"; vertices = [new(2, 0), new(4, 1.5m), new(3.3m, 4), new(.7m, 4), new(0, 1.5m)]; break;
            case 6: id = "hexagon"; vertices = [new(1, 0), new(3, 0), new(4, 2), new(3, 4), new(1, 4), new(0, 2)]; break;
            case 7: id = "octagon"; vertices = [new(1, 0), new(3, 0), new(4, 1), new(4, 3), new(3, 4), new(1, 4), new(0, 3), new(0, 1)]; break;
            case 8: id = "concave-l"; vertices = [new(0, 0), new(2, 0), new(2, 2), new(4, 2), new(4, 4), new(0, 4)]; break;
            default: id = "concave-step"; vertices = [new(0, 0), new(2, 0), new(2, 1), new(3, 1), new(3, 2), new(4, 2), new(4, 4), new(0, 4)]; break;
        }
        bool labelVertices = _random.Next(2) == 0;
        if (labelVertices)
            vertices = vertices.Select((point, index) => point with { Label = ((char)('A' + index)).ToString() }).ToArray();
        string[] prompts = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCountSidesTask.001"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCountSidesTask.002"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCountSidesTask.003"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCountSidesTask.004") };
        var visual = new QuizVisualData("polygon", vertices.Select(point => point.Label).ToArray(), [], "",
            RotationDegrees: level <= 2 ? 0 : level == 3 ? _random.Next(4) * 90 : _random.Next(8) * 45, Polygons: [new(vertices)], ScenarioId: id);
        return new(prompts[_random.Next(prompts.Length)], QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCountSidesTask.005"), vertices.Length.ToString(), "",
            visual, [], ShapeFacts(vertices.Length), false,
            QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCountSidesTask.006"));
    }

    private ShapeTask CreateMissingSideTask(AppLanguage language, int a, int b, int level)
    {
        bool vi = language == AppLanguage.Vietnamese;
        int[] families = QuizDifficultyPolicy.ShapeFamilies(ElementaryQuizType.RectangleSide, level);
        int family = families[NextContextVariant(ElementaryQuizType.RectangleSide, language, $"shape-{level}", families.Length)];
        string unit = new[] { "cm", "dm", "m" }[_random.Next(3)];
        string areaUnit = unit + "²";
        string problem, expression, answerLabel, id;
        string[] facts, constants = [];
        IReadOnlyList<QuizVisualPolygon> polygons;
        QuizVisualAnnotation[] annotations;
        if (a < b) (a, b) = (b, a);
        bool askLength = _random.Next(2) == 0;
        int given = askLength ? b : a;
        string known = (askLength ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.037") : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.038"));
        string missing = (askLength ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.039") : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.040"));
        answerLabel = char.ToUpperInvariant(missing[0]) + missing[1..];
        switch (family)
        {
            case 0:
                id = "rectangle-area";
                facts = ShapeFacts(a * b, given);
                expression = $"{a * b}/{given}";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.007", ("a_b", $"{a * b}"), ("areaUnit", $"{areaUnit}"), ("known", $"{known}"), ("given", $"{given}"), ("unit", $"{unit}"), ("missing", $"{missing}"));
                polygons = [ShapeRectangle(0, 0, 6, 4)];
                annotations = [new(askLength ? "?" : $"{given} {unit}", 3, -.5m),
                    new(askLength ? $"{given} {unit}" : "?", 6.8m, 2), new($"S = {a * b} {areaUnit}", 3, 2)];
                break;
            case 1:
                id = "rectangle-perimeter";
                int perimeter = 2 * (a + b);
                facts = ShapeFacts(perimeter, given); constants = ["2"];
                expression = $"{perimeter}/2-{given}";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.008", ("perimeter", $"{perimeter}"), ("unit", $"{unit}"), ("known", $"{known}"), ("given", $"{given}"), ("missing", $"{missing}"));
                polygons = [ShapeRectangle(0, 0, 6, 4)];
                annotations = [new(askLength ? "?" : $"{given} {unit}", 3, -.5m),
                    new(askLength ? $"{given} {unit}" : "?", 6.8m, 2), new($"P = {perimeter} {unit}", 3, 2)];
                break;
            case 2:
            case 7:
                bool square = family == 2;
                id = square ? "square-perimeter" : "rhombus-perimeter";
                string shape = square ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.009") : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.010");
                facts = ShapeFacts(4 * a); constants = ["4"];
                expression = $"{4 * a}/4";
                answerLabel = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.011");
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.012", ("shape", $"{shape}"), ("4_a", $"{4 * a}"), ("unit", $"{unit}"));
                polygons = square ? [ShapeRectangle(0, 0, 4, 4)]
                    : [new([new(3, 0), new(6, 2), new(3, 4), new(0, 2)])];
                annotations = [new("?", square ? 2 : 4.8m, square ? -.5m : .8m), new($"P = {4 * a} {unit}", square ? 2 : 3, 2)];
                break;
            case 3:
                id = "triangle-perimeter";
                int third = a, trianglePerimeter = a + b + third;
                facts = ShapeFacts(trianglePerimeter, a, b);
                expression = $"{trianglePerimeter}-{a}-{b}";
                answerLabel = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.013");
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.014", ("trianglePerimeter", $"{trianglePerimeter}"), ("unit", $"{unit}"), ("a", $"{a}"), ("b", $"{b}"));
                polygons = [new([new(0, 4, "A"), new(3, 0, "B"), new(6, 4, "C")])];
                annotations = [new($"{a} {unit}", .8m, 1.7m), new($"{b} {unit}", 5.2m, 1.7m), new("?", 3, 4.5m)];
                break;
            case 4:
                id = "parallelogram-perimeter";
                int p = 2 * (a + b);
                facts = ShapeFacts(p, a); constants = ["2"];
                expression = $"{p}/2-{a}";
                answerLabel = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.015");
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.016", ("p", $"{p}"), ("unit", $"{unit}"), ("a", $"{a}"));
                polygons = [new([new(1, 0, "A"), new(6, 0, "B"), new(5, 4, "C"), new(0, 4, "D")])];
                annotations = [new($"{a} {unit}", 3.5m, -.5m), new("?", 6.3m, 2)];
                break;
            case 5:
                id = "split-side";
                facts = ShapeFacts(a + b, a);
                expression = $"{a + b}-{a}";
                answerLabel = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.017");
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.018", ("a_b", $"{a + b}"), ("unit", $"{unit}"), ("a", $"{a}"));
                polygons = [new([new(0, 0, "A"), new(3, 0, "M"), new(6, 0, "N"), new(6, 4), new(0, 4)])];
                annotations = [new($"AM = {a} {unit}", 1.5m, -.9m), new("MN = ?", 4.5m, -.9m), new($"AN = {a + b} {unit}", 3, 4.6m)];
                break;
            case 8:
                id = "split-side-perimeter";
                int fullWidth = a + b + 2, fullPerimeter = 2 * (fullWidth + b);
                facts = ShapeFacts(fullPerimeter, b, a); constants = ["2"];
                expression = $"{fullPerimeter}/2-{b}-{a}";
                answerLabel = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.019");
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.020", ("fullPerimeter", $"{fullPerimeter}"), ("unit", $"{unit}"), ("b", $"{b}"), ("a", $"{a}"));
                polygons = [new([new(0, 0, "A"), new(3, 0, "M"), new(6, 0, "N"), new(6, 4, "C"), new(0, 4, "D")])];
                annotations = [new($"AM = {a} {unit}", 1.5m, -.9m), new("MN = ?", 4.5m, -.9m),
                    new($"{b} {unit}", 6.8m, 2), new($"P = {fullPerimeter} {unit}", 3, 2)];
                break;
            default:
                id = "l-shape-side";
                facts = ShapeFacts(a + b, a);
                expression = $"{a + b}-{a}";
                answerLabel = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.021");
                polygons = [new([new(0, 0, "F"), new(2, 0, "A"), new(2, 2, "B"), new(6, 2, "C"), new(6, 4, "D"), new(0, 4, "E")])];
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.022", ("a_b", $"{a + b}"), ("unit", $"{unit}"), ("a", $"{a}"));
                annotations = [new($"{a} {unit}", 1, -.5m), new("?", 4, 1.5m), new($"{a + b} {unit}", 3, 4.7m)];
                break;
        }
        string[] leads = new[] { "", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.023"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateMissingSideTask.024") };
        var visual = new QuizVisualData("missing-side", [], facts.Select(value => decimal.Parse(value, CultureInfo.InvariantCulture)).ToArray(), unit,
            Polygons: polygons, Annotations: annotations, ScenarioId: id);
        return new(leads[_random.Next(leads.Length)] + problem, answerLabel, expression, unit, visual, facts, constants);
    }

    private ShapeTask CreateCompositeAreaTask(AppLanguage language, int a, int b, int c, int level)
    {
        bool vi = language == AppLanguage.Vietnamese;
        int[] families = QuizDifficultyPolicy.ShapeFamilies(ElementaryQuizType.CompositeArea, level);
        int family = families[NextContextVariant(ElementaryQuizType.CompositeArea, language, $"shape-{level}", families.Length)];
        string unit = new[] { "cm", "dm", "m" }[_random.Next(3)];
        string problem, expression, id;
        string[] facts, constants = [];
        IReadOnlyList<QuizVisualPolygon> polygons;
        QuizVisualAnnotation[] annotations;
        switch (family)
        {
            case 0:
                id = "two-rectangles";
                facts = ShapeFacts(a, b, c);
                expression = $"{a}*{b}+{c}*{a}";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.025", ("a", $"{a}"), ("unit", $"{unit}"), ("b", $"{b}"), ("c", $"{c}"));
                polygons = [ShapeRectangle(0, 0, a, b, "I"), ShapeRectangle(a, 0, c, a, "II")];
                annotations = [new($"{a} {unit}", a / 2m, -.5m), new($"{b} {unit}", -.8m, b / 2m),
                    new($"{c} {unit}", a + c / 2m, -.5m), new($"{a} {unit}", a + c + .8m, a / 2m)];
                break;
            case 1:
                id = "square-rectangle";
                facts = ShapeFacts(a, b, c);
                expression = $"{a}*{a}+{b}*{c}";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.026", ("a", $"{a}"), ("unit", $"{unit}"), ("b", $"{b}"), ("c", $"{c}"));
                polygons = [ShapeRectangle(0, 0, a, a, "I"), ShapeRectangle(a, 0, b, c, "II")];
                annotations = [new($"{a} {unit}", a / 2m, -.5m), new($"{b} {unit}", a + b / 2m, -.5m), new($"{c} {unit}", a + b + .8m, c / 2m)];
                break;
            case 2:
                id = "corner-cutout";
                int w = a + b, h = b + c;
                facts = ShapeFacts(w, h, b, c);
                expression = $"{w}*{h}-{b}*{c}";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.027", ("w", $"{w}"), ("unit", $"{unit}"), ("h", $"{h}"), ("b", $"{b}"), ("c", $"{c}"));
                polygons = [ShapeRectangle(0, 0, a, c, "I"), ShapeRectangle(0, c, w, b, "II")];
                annotations = [new($"{w} {unit}", w / 2m, h + .7m), new($"{h} {unit}", -.8m, h / 2m),
                    new($"{b} {unit}", a + b / 2m, c - .5m), new($"{c} {unit}", a + .8m, c / 2m)];
                break;
            case 3:
                id = "frame";
                int outerW = a + 2 * c, outerH = b + 2 * c;
                facts = ShapeFacts(outerW, outerH, a, b);
                expression = $"{outerW}*{outerH}-{a}*{b}";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.028", ("outerW", $"{outerW}"), ("unit", $"{unit}"), ("outerH", $"{outerH}"), ("a", $"{a}"), ("b", $"{b}"));
                polygons = [ShapeRectangle(0, 0, outerW, outerH), ShapeRectangle(c, c, a, b, QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.029"), true)];
                annotations = [new($"{outerW} {unit}", outerW / 2m, -.6m), new($"{outerH} {unit}", outerW + .8m, outerH / 2m),
                    new($"{a} {unit}", c + a / 2m, c - .5m), new($"{b} {unit}", c - .8m, c + b / 2m)];
                break;
            case 4:
                id = "rectangle-triangle";
                int height = b * 2;
                facts = ShapeFacts(a, height, c); constants = ["2"];
                expression = $"{a}*{height}+{c}*{height}/2";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.030", ("a", $"{a}"), ("unit", $"{unit}"), ("height", $"{height}"), ("c", $"{c}"));
                polygons = [ShapeRectangle(0, 0, a, height, "I"), new([new(a, 0), new(a + c, height), new(a, height)], "II")];
                annotations = [new($"{a} {unit}", a / 2m, height + .7m), new($"{height} {unit}", -.8m, height / 2m), new($"{c} {unit}", a + c / 2m, height + .7m)];
                break;
            case 5:
                id = "t-shape";
                int topWidth = a + 2 * b;
                facts = ShapeFacts(topWidth, c, b, a);
                expression = $"{topWidth}*{c}+{b}*{a}";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.031", ("topWidth", $"{topWidth}"), ("unit", $"{unit}"), ("c", $"{c}"), ("b", $"{b}"), ("a", $"{a}"));
                decimal stemX = (topWidth - b) / 2m;
                polygons = [ShapeRectangle(0, 0, topWidth, c, "I"), ShapeRectangle(stemX, c, b, a, "II")];
                annotations = [new($"{topWidth} {unit}", topWidth / 2m, -.6m), new($"{c} {unit}", -.8m, c / 2m),
                    new($"{b} {unit}", stemX + b / 2m, c + a + .7m), new($"{a} {unit}", stemX + b + .8m, c + a / 2m)];
                break;
            case 7:
                id = "two-openings";
                int boardWidth = a + b + 3 * c, boardHeight = b + 2 * c;
                facts = ShapeFacts(boardWidth, boardHeight, a, b, b, c);
                expression = $"{boardWidth}*{boardHeight}-{a}*{b}-{b}*{c}";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.032", ("boardWidth", $"{boardWidth}"), ("unit", $"{unit}"), ("boardHeight", $"{boardHeight}"), ("a", $"{a}"), ("b", $"{b}"), ("c", $"{c}"));
                polygons = [ShapeRectangle(0, 0, boardWidth, boardHeight),
                    ShapeRectangle(c, c, a, b, "I", true), ShapeRectangle(a + 2 * c, c, b, c, "II", true)];
                annotations = [new($"{boardWidth} {unit}", boardWidth / 2m, -.6m), new($"{boardHeight} {unit}", boardWidth + .8m, boardHeight / 2m),
                    new($"{a} {unit}", c + a / 2m, c - .5m), new($"{b} {unit}", c - .8m, c + b / 2m),
                    new($"{b} {unit}", a + 2 * c + b / 2m, c - .5m), new($"{c} {unit}", a + 2 * c + b + .8m, 1.5m * c)];
                break;
            default:
                id = "three-rectangles";
                int strip = _random.Next(2, 6);
                facts = ShapeFacts(strip, a, b, c);
                expression = $"{strip}*{a}+{strip}*{b}+{strip}*{c}";
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.033", ("strip", $"{strip}"), ("unit", $"{unit}"), ("a", $"{a}"), ("b", $"{b}"), ("c", $"{c}"));
                polygons = [ShapeRectangle(0, 0, strip, a, "I"), ShapeRectangle(strip, 0, strip, b, "II"), ShapeRectangle(2 * strip, 0, strip, c, "III")];
                annotations = [new($"{strip} {unit}", strip / 2m, -.6m), new($"{strip} {unit}", 1.5m * strip, -.6m), new($"{strip} {unit}", 2.5m * strip, -.6m),
                    new($"{a} {unit}", strip / 2m, a + .7m), new($"{b} {unit}", 1.5m * strip, b + .7m), new($"{c} {unit}", 2.5m * strip, c + .7m)];
                break;
        }
        var visual = new QuizVisualData("composite-polygons", [], facts.Select(value => decimal.Parse(value, CultureInfo.InvariantCulture)).ToArray(), unit,
            Polygons: polygons, Annotations: annotations, ScenarioId: id);
        string[] leads = new[] { "", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.034"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.035") };
        return new(leads[_random.Next(leads.Length)] + problem, QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Shapes.CreateCompositeAreaTask.036"), expression, unit + "²", visual, facts, constants);
    }
}
