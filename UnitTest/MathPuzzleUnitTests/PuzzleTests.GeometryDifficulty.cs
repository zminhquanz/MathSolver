using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckGeometryDifficulty()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        var conversionUnits = new HashSet<string>();
        int count = 0;
        foreach (var shape in Enum.GetValues<GeometryQuizShape>())
        foreach (var measurement in GeometryQuizGenerator.GetAvailableMeasurements(shape))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 4; seed++)
        {
            var context = new QuizCurriculumContext(tier, false);
            var algorithm = new GeometryQuizGenerator(new GeometryCalculationEngine(), new Random(seed))
                .GenerateAlgorithm(mode, language, shape, context, measurement);
            var contextual = new GeometryQuizGenerator(new GeometryCalculationEngine(), new Random(seed))
                .Generate(mode, language, shape, context, measurement);
            var geometry = algorithm.GeometryProblem!;
            var reasoning = geometry.Reasoning!;
            string label = $"{shape}/{measurement}/{tier}/{language}/{mode}/{seed}";
            Require(geometry.ShapeId == contextual.GeometryProblem!.ShapeId && geometry.Measurement == measurement &&
                    geometry.Dimensions.SequenceEqual(contextual.GeometryProblem.Dimensions) &&
                    reasoning.Givens.SequenceEqual(contextual.GeometryProblem.Reasoning!.Givens) &&
                    reasoning.CombinedExpression == contextual.GeometryProblem.Reasoning.CombinedExpression,
                $"{label}: sources do not share one mathematical contract.");
            Require(reasoning.Tier == tier && reasoning.Givens.All(fact => fact.Value > 0) &&
                    geometry.Dimensions.Values.All(value => value > 0), $"{label}: invalid given dimensions.");
            BigInteger D(string key) => geometry.Dimensions[key];
            BigInteger expected = (shape, measurement) switch
            {
                (GeometryQuizShape.Square or GeometryQuizShape.Rhombus, GeometryMeasurement.Perimeter) => 4 * D("a"),
                (GeometryQuizShape.Square, _) => D("a") * D("a"),
                (GeometryQuizShape.Rectangle or GeometryQuizShape.Parallelogram, GeometryMeasurement.Perimeter) => 2 * (D("a") + D("b")),
                (GeometryQuizShape.Rectangle, _) => D("a") * D("b"),
                (GeometryQuizShape.Triangle, GeometryMeasurement.Perimeter) => D("a") + D("b") + D("c"),
                (GeometryQuizShape.Triangle, _) => D("a") * D("h") / 2,
                (GeometryQuizShape.Trapezoid, GeometryMeasurement.Perimeter) => D("a") + D("b") + D("c") + D("d"),
                (GeometryQuizShape.Trapezoid, _) => (D("a") + D("b")) * D("h") / 2,
                (GeometryQuizShape.Rhombus, _) => D("d1") * D("d2") / 2,
                (GeometryQuizShape.Parallelogram, _) => D("a") * D("h"),
                (GeometryQuizShape.Circle, GeometryMeasurement.Perimeter) => 628 * D("r") / 100,
                (GeometryQuizShape.Circle, _) => 314 * D("r") * D("r") / 100,
                (GeometryQuizShape.Cube, GeometryMeasurement.Volume) => BigInteger.Pow(D("a"), 3),
                (GeometryQuizShape.Cube, _) => (measurement == GeometryMeasurement.LateralArea ? 4 : 6) * BigInteger.Pow(D("a"), 2),
                (GeometryQuizShape.RectangularPrism, GeometryMeasurement.Volume) => D("a") * D("b") * D("h"),
                (GeometryQuizShape.RectangularPrism, GeometryMeasurement.LateralArea) => 2 * (D("a") + D("b")) * D("h"),
                _ => 2 * (D("a") * D("b") + D("a") * D("h") + D("b") * D("h"))
            };
            Require(expected == geometry.CorrectAnswer, $"{label}: independent formula disagrees.");
            if (shape == GeometryQuizShape.Triangle)
                Require(D("a") * D("a") + D("b") * D("b") == D("c") * D("c") && D("h") == D("b"),
                    $"{label}: impossible right triangle.");
            if (shape == GeometryQuizShape.Trapezoid)
                Require(BigInteger.Pow((D("a") - D("b")) / 2, 2) + D("h") * D("h") == D("c") * D("c") && D("c") == D("d"),
                    $"{label}: legs and height contradict the trapezoid bases.");
            if (shape == GeometryQuizShape.Rhombus)
                Require(D("d1") * D("d1") + D("d2") * D("d2") == 4 * D("a") * D("a"),
                    $"{label}: side and diagonals cannot form a rhombus.");
            Require(EssayCalculationEvaluator.TryEvaluate(reasoning.CombinedExpression, out var value, out _) &&
                    value.Denominator.IsOne && value.Numerator == expected, $"{label}: combined derivation is incorrect.");
            foreach (var step in reasoning.Steps)
                Require(EssayCalculationEvaluator.TryEvaluate(step.Expression, out var intermediate, out _) &&
                        intermediate.Denominator.IsOne && intermediate.Numerator == step.Value,
                    $"{label}: incorrect intermediate step.");
            bool singleDimension = shape is GeometryQuizShape.Square or GeometryQuizShape.Cube or GeometryQuizShape.Circle ||
                shape == GeometryQuizShape.Rhombus && measurement == GeometryMeasurement.Perimeter;
            Require(reasoning.Steps.Count >= ((int)tier switch { 1 => 0, 2 => 1, 3 => 2, 4 => singleDimension ? 1 : 2, _ => singleDimension ? 2 : 3 }),
                $"{label}: difficulty has no added reasoning steps.");
            Require((tier == CurriculumTier.OneStar) == (reasoning.HiddenDimensions.Count == 0),
                $"{label}: inferred dimensions have not been separated from direct givens.");
            if (tier == CurriculumTier.FiveStars)
            {
                var difference = reasoning.Givens.Single(fact => fact.Id == "difference");
                Require(difference.Unit != geometry.LengthUnitSymbol && reasoning.Steps.Any(step => step.Key == "converted"),
                    $"{label}: five stars lacks actual unit conversion.");
                conversionUnits.Add(geometry.LengthUnitSymbol);
            }
            var hidden = QuizDiagramBuilder.Build(algorithm, language)!;
            var revealed = QuizDiagramBuilder.Build(algorithm, language, true)!;
            foreach (string key in reasoning.HiddenDimensions)
                Require(hidden.DimensionLabels![key] == "? " + geometry.LengthUnitSymbol &&
                        revealed.DimensionLabels![key] == D(key) + " " + geometry.LengthUnitSymbol,
                    $"{label}: the unsolved diagram discloses an inferred dimension.");

            if (mode == ArithmeticQuizMode.Essay)
            {
                string answer = expected + " " + geometry.AnswerUnit;
                string sentence = GeometryReasoningText.SolutionLead(geometry, language);
                string final = geometry.SubstitutionExpression + " = " + answer;
                string work = string.Join("\n", reasoning.Steps.Select(step => $"{step.Expression} = {step.Value} {step.Unit}").Append(final));
                var contextualQuestion = contextual with
                {
                    WordProblem = new(reasoning.ProblemText, sentence, geometry.AnswerUnit, geometry.ObjectName)
                };
                foreach (var question in new[] { algorithm, contextualQuestion })
                {
                    Require(grader.Validate(question, sentence, work, answer).IsCorrect, $"{label}: valid split work rejected.");
                    Require(grader.Validate(question, sentence, geometry.EquationText + " " + geometry.AnswerUnit, answer).IsCorrect,
                        $"{label}: valid merged work rejected.");
                    if (tier == CurriculumTier.FourStars && shape == GeometryQuizShape.Square)
                    {
                        var sum = reasoning.Givens.Single(given => given.Id == "sum").Value;
                        var difference = reasoning.Givens.Single(given => given.Id == "difference").Value;
                        string alternative = $"{sum}/2 = {sum}/2 {geometry.LengthUnitSymbol}\n" +
                            $"{difference}/2 = {difference}/2 {geometry.LengthUnitSymbol}\n" +
                            $"{sum}/2+{difference}/2 = {D("a")} {geometry.LengthUnitSymbol}\n" + final;
                        Require(grader.Validate(question, sentence, alternative, answer).IsCorrect,
                            $"{label}: an equivalent solution with fractional intermediate results was rejected.");
                    }
                    Require(grader.Validate(question, sentence, string.Join("\n", work.Split('\n').Reverse()), answer).IsCorrect,
                        $"{label}: reordering valid work was rejected.");
                    string formatted = ElementaryWordProblemSolutionFormatter.Format(question, language, CultureInfo.InvariantCulture);
                    var parts = EssayCombinedInputParser.Parse(formatted, true, preserveAllCalculations: true);
                    Require(grader.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                        $"{label}: combined worked solution rejected.");
                    Require(!grader.Validate(question, sentence, "1+1=3\n" + work, answer).EquationIsCorrect,
                        $"{label}: incorrect earlier step was hidden by a correct final result.");
                    Require(!grader.Validate(question, sentence, final.Replace(geometry.AnswerUnit, "kg"), answer).EquationIsCorrect,
                        $"{label}: incorrect calculation unit accepted.");
                    string wrongPower = geometry.AnswerUnit == geometry.LengthUnitSymbol ? geometry.LengthUnitSymbol + "²" : geometry.LengthUnitSymbol;
                    Require(!grader.Validate(question, sentence, work + "\n" + final.Replace(geometry.AnswerUnit, wrongPower), answer).EquationIsCorrect,
                        $"{label}: a correct line concealed a contradictory result unit.");
                    Require(!grader.Validate(question, sentence, work, expected + " kg").AnswerIsCorrect,
                        $"{label}: incorrect answer unit accepted.");
                    if (reasoning.Steps.Count > 0)
                    {
                        var step = reasoning.Steps[0];
                        Require(!grader.Validate(question, sentence, $"{step.Expression} = {step.Value} {step.Unit}²\n" + final, answer).EquationIsCorrect,
                            $"{label}: a length inference was accepted as area.");
                    }
                }
            }
            count++;
        }
        Require(conversionUnits.SetEquals(new[] { "km", "m", "dm", "cm", "mm" }), "Geometry conversion coverage misses a length unit.");
        Console.WriteLine($"  Checked {count} geometry contracts: physical dimensions, five reasoning tiers, hidden labels, word-problem facts and flexible work.");
    }
}
