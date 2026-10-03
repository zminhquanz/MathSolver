using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckGeometryMeasurements()
    {
        var generator = new GeometryQuizGenerator(new GeometryCalculationEngine(), new Random(91001));
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        var catalog = new QuizProblemTypeCatalog(new Random(91002));
        int geometryIndex = catalog.Options.ToList().FindIndex(option => option.FixedRequest?.Kind == QuizProblemKind.Geometry);
        int count = 0;
        foreach (GeometryQuizShape shape in Enum.GetValues<GeometryQuizShape>())
        {
            bool solid = shape is GeometryQuizShape.Cube or GeometryQuizShape.RectangularPrism;
            GeometryMeasurement[] expected = solid
                ? [GeometryMeasurement.Volume, GeometryMeasurement.LateralArea, GeometryMeasurement.TotalArea]
                : [GeometryMeasurement.Perimeter, GeometryMeasurement.Area];
            Require(GeometryQuizGenerator.GetAvailableMeasurements(shape).SequenceEqual(expected),
                $"{shape}: picker offers measurements for the wrong dimension.");
            foreach (GeometryMeasurement measurement in expected)
            {
                QuizProblemRequest request = catalog.Resolve(geometryIndex, null, null, ProportionQuizType.Direct,
                    null, null, null, shape, null, CurriculumTier.OneStar, geometryMeasurement: measurement);
                Require(request.GeometryShape == shape && request.GeometryMeasurement == measurement,
                    $"{shape}/{measurement}: catalog lost the selected measurement.");
                foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
                foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
                foreach (ArithmeticQuizMode mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    AppLanguageManager.CurrentLanguage = language;
                    var curriculum = new QuizCurriculumContext(tier, false);
                    ArithmeticQuizQuestion question = generator.GenerateAlgorithm(mode, language,
                        request.GeometryShape, curriculum, request.GeometryMeasurement);
                    GeometryQuizContract contract = question.GeometryProblem!;
                    string label = $"{shape}/{measurement}/{tier}/{language}/{mode}";
                    Require(contract.Measurement == measurement, $"{label}: requested measurement changed.");
                    Require(contract.UnitPower == (measurement == GeometryMeasurement.Perimeter ? 1 :
                            measurement == GeometryMeasurement.Volume ? 3 : 2),
                        $"{label}: incorrect unit power.");
                    Require(EssayCalculationEvaluator.TryEvaluate(contract.SubstitutionExpression,
                            out var calculated, out _) && calculated.Denominator.IsOne &&
                            calculated.Numerator == contract.CorrectAnswer,
                        $"{label}: formula and answer disagree.");
                    if (measurement == GeometryMeasurement.LateralArea)
                    {
                        BigInteger a = contract.Dimensions["a"];
                        BigInteger expectedArea = shape == GeometryQuizShape.Cube ? 4 * a * a :
                            2 * (a + contract.Dimensions["b"]) * contract.Dimensions["h"];
                        Require(contract.CorrectAnswer == expectedArea,
                            $"{label}: lateral area used a different solid formula.");
                    }
                    if (mode == ArithmeticQuizMode.Essay)
                    {
                        string answer = contract.CorrectAnswer + contract.AnswerUnit;
                        Require(grader.Validate(question, BuildSolutionSentence(question, language),
                                contract.EquationText + contract.AnswerUnit, answer).IsCorrect,
                            $"{label}: correct essay rejected.");
                        string wrongUnit = contract.LengthUnitSymbol +
                            (contract.UnitPower == 3 ? "²" : "³");
                        Require(!grader.Validate(question, BuildSolutionSentence(question, language),
                                contract.EquationText + wrongUnit, contract.CorrectAnswer + wrongUnit).IsCorrect,
                            $"{label}: incorrect unit power accepted.");
                    }

                    // word-problem receives the same requested contract before it writes the story.
                    ArithmeticQuizQuestion contextual = generator.Generate(mode, language, shape, curriculum, measurement);
                    Require(contextual.GeometryProblem!.Measurement == measurement,
                        $"{label}: word-problem contract ignored the selection.");
                    count++;
                }
            }

            var mixedMeasurements = new HashSet<GeometryMeasurement>();
            for (int sample = 0; sample < 128; sample++)
                mixedMeasurements.Add(generator.Generate(ArithmeticQuizMode.Essay, AppLanguage.English, shape)
                    .GeometryProblem!.Measurement);
            Require(mixedMeasurements.SetEquals(expected), $"{shape}: mixed mode omitted a supported measurement.");
        }

        foreach (GeometryMeasurement measurement in GeometryQuizGenerator.GetAvailableMeasurements(null))
        {
            for (int sample = 0; sample < 20; sample++)
                Require(generator.GenerateAlgorithm(ArithmeticQuizMode.Essay, AppLanguage.English,
                        requestedMeasurement: measurement).GeometryProblem!.Measurement == measurement,
                    $"Mixed shapes/{measurement}: requested measurement changed.");
        }
        try
        {
            generator.Generate(ArithmeticQuizMode.Essay, AppLanguage.English, GeometryQuizShape.Square,
                requestedMeasurement: GeometryMeasurement.Volume);
            throw new Exception("A planar shape silently generated a volume question.");
        }
        catch (InvalidOperationException)
        {
            // Unsupported shape/measurement combinations cannot generate a different question.
        }
        QuizProblemRequest mixedRequest = catalog.Resolve(0, null, null, ProportionQuizType.Direct,
            null, null, null, GeometryQuizShape.Cube, null, CurriculumTier.FiveStars,
            geometryMeasurement: GeometryMeasurement.LateralArea);
        Require(mixedRequest.GeometryMeasurement is null, "Global mixed mode inherited a hidden geometry selection.");
        Console.WriteLine($"  Checked {count} geometry selections, exact formulas, unit powers and bilingual contracts.");
    }
}
