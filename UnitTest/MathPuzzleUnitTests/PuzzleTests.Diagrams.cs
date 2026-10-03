using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Numerics;
using System.Text.Json;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckQuizDiagrams()
    {
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            // Geometry contracts contain extra generated dimensions that were NOT given.
            foreach (var shape in Enum.GetValues<GeometryQuizShape>())
            foreach (var measurement in new[] { GeometryMeasurement.Perimeter, GeometryMeasurement.Area,
                         GeometryMeasurement.Volume, GeometryMeasurement.LateralArea, GeometryMeasurement.TotalArea })
            {
                bool solid = shape is GeometryQuizShape.Cube or GeometryQuizShape.RectangularPrism;
                if (solid != (measurement is GeometryMeasurement.Volume or GeometryMeasurement.LateralArea or GeometryMeasurement.TotalArea)) continue;
                var question = new GeometryQuizGenerator(new GeometryCalculationEngine(), new Random(4))
                    .GenerateAlgorithm(ArithmeticQuizMode.Essay, language, shape, requestedMeasurement: measurement);
                var diagram = QuizDiagramBuilder.Build(question, language)!;
                Require(diagram.Explanation is null && diagram.Caption.Contains('?'), "Ungraded geometry leaked a solution.");
                var geometry = question.GeometryProblem!;
                foreach (var label in diagram.DimensionLabels!)
                    Require(label.Value == geometry.Dimensions[label.Key] + " " + geometry.LengthUnitSymbol, "Geometry used an unrelated dimension.");
                if (shape == GeometryQuizShape.Triangle && measurement == GeometryMeasurement.Area)
                    Require(diagram.DimensionLabels.Keys.Order().SequenceEqual(new[] { "a", "h" }), "Triangle area revealed extra side lengths.");
                if (shape == GeometryQuizShape.Rhombus && measurement == GeometryMeasurement.Perimeter)
                    Require(diagram.DimensionLabels.Count == 1 && diagram.DimensionLabels.ContainsKey("a"), "Rhombus perimeter leaked diagonals.");
                var poisoned = question with { GeometryProblem = geometry with { CorrectAnswer = BigInteger.Parse("98765432109876543210") } };
                Require(JsonSerializer.Serialize(diagram) == JsonSerializer.Serialize(QuizDiagramBuilder.Build(poisoned, language)),
                    "Geometry presentation depended on a hidden answer.");
                Require(!string.IsNullOrWhiteSpace(QuizDiagramBuilder.Build(question, language, true)!.Explanation), "Geometry explanation missing after grading.");
            }
            foreach (var kind in new[] { QuizProblemKind.TwoNumbers, QuizProblemKind.FractionSkills })
            foreach (var type in ElementaryQuizGenerator.Types(kind))
            {
                var question = new ElementaryQuizGenerator(new Random(12)).Generate(ArithmeticQuizMode.Essay,
                    kind, type, language, CurriculumTier.ThreeStars);
                CheckSourceAndAnswerIndependence(question, language);
            }
            var seenMotion = new HashSet<MotionQuestionKind>();
            for (int seed = 0; seed < 80; seed++)
            foreach (var type in Enum.GetValues<MotionQuizType>())
            {
                var question = new MotionQuizGenerator(new Random(seed)).GenerateAlgorithm(ArithmeticQuizMode.Essay, language, type);
                seenMotion.Add(question.MotionProblem!.QuestionKind);
                CheckSourceAndAnswerIndependence(question, language);
                var diagram = QuizDiagramBuilder.Build(question, language)!;
                Require(diagram.Rows.Count >= 2, "Motion diagram did not represent the given facts.");
                if (question.MotionProblem.QuestionKind == MotionQuestionKind.MeetingTime)
                    Require(diagram.Rows[0].Direction == 1 && diagram.Rows[1].Direction == -1, "Meeting arrows point the wrong way.");
                if (question.MotionProblem.QuestionKind == MotionQuestionKind.CatchUpTime)
                    Require(diagram.Rows.All(row => row.Direction == 1), "Chasing arrows point the wrong way.");
            }
            Require(seenMotion.Count == Enum.GetValues<MotionQuestionKind>().Length, "Not all motion kinds were checked.");
            var average = new AverageQuizGenerator(new Random(4)).GenerateAlgorithm(ArithmeticQuizMode.Essay, AverageQuizType.IndirectData, language);
            CheckSourceAndAnswerIndependence(average, language);
            Require(QuizDiagramBuilder.Build(average, language)!.Rows.Count == 3, "Indirect average needs three related quantities.");
            var fraction = new FractionQuizGenerator(new FractionCalculationEngine(), new Random(4))
                .Generate(ArithmeticQuizMode.Essay, FractionOperation.Add);
            CheckSourceAndAnswerIndependence(fraction, language);
            foreach (var key in new[] { "ShowDiagram", "HideDiagram", "EnlargeDiagram", "CloseDiagram", "DiagramZoom", "DiagramNotToScale" })
                Require(MathSolver.Services.Localization.QuizLocalizationOverrides.TryGetValue("Quiz." + key,
                    language == AppLanguage.Vietnamese ? "vi" : "en", out var value) && value.Length > 0, "Missing diagram translation.");
        }
    }

    private static void CheckSourceAndAnswerIndependence(ArithmeticQuizQuestion question, AppLanguage language)
    {
        QuizDiagram before = QuizDiagramBuilder.Build(question, language)!;
        Require(before is not null && before.Explanation is null, "Diagram leaked the worked solution before grading.");
        string original = JsonSerializer.Serialize(before);
        var changed = question with { CorrectAnswer = 987654321, WordProblem = new("word-problem prose 987654321", "word-problem solution 987654321", "wrong", "wrong") };
        if (question.MotionProblem is { } motion) changed = changed with { MotionProblem = motion with { CorrectAnswer = 987654321, SolutionText = "SECRET" } };
        if (question.AverageProblem is { } average) changed = changed with { AverageProblem = average with { CorrectAnswer = 987654321, SolutionText = "SECRET" } };
        if (question.FractionProblem is { } fraction) changed = changed with { FractionProblem = fraction with { CorrectAnswer = new(987654321, 1) } };
        if (question.ElementaryProblem is { } elementary) changed = changed with { ElementaryProblem = elementary with
            { SolutionText = "SECRET", Answers = elementary.Answers.Select(answer => answer with { Value = new(987654321, 1) }).ToArray() } };
        Require(original == JsonSerializer.Serialize(QuizDiagramBuilder.Build(changed, language)), "Diagram used word-problem prose or hidden answer values.");
        Require(!string.IsNullOrWhiteSpace(QuizDiagramBuilder.Build(question, language, true)!.Explanation), "Diagram explanation missing after grading.");
    }
}
