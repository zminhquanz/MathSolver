using MathSolver.Models;
using MathSolver.Services.Core;

namespace MathSolver.Services.QuestionBank;

public static partial class ReasoningStoryCatalogue
{
    // Append-only family IDs; encode both shape and requested quantity so a
    // saved perimeter lesson can never replace an area/volume selection.
    public static int GeometryVariant(GeometryQuizShape shape, GeometryMeasurement measurement) => (int)shape * 10 + (int)measurement;

    public static (GeometryQuizShape Shape, GeometryMeasurement Measurement) GeometryProfile(int variant) =>
        ((GeometryQuizShape)(variant / 10), (GeometryMeasurement)(variant % 10));

    private static int[] GeometryVariants(CurriculumTier tier)
    {
        var rules = QuizCurriculumLayer.GetGeometryRules(new(tier, false));
        return rules.AllowedShapes.SelectMany(shape => GeometryQuizGenerator.GetAvailableMeasurements(shape)
            .Where(measurement => rules.AllowedMeasurements is null || rules.AllowedMeasurements[shape].Contains(measurement))
            .Select(measurement => GeometryVariant(shape, measurement))).ToArray();
    }

    private static ArithmeticQuizQuestion GenerateGeometry(BasicQuestionContract c, ArithmeticQuizMode mode, Random random)
    {
        var profile = GeometryProfile(c.Story!.Variant);
        return new GeometryQuizGenerator(new GeometryCalculationEngine(), random).GenerateAlgorithm(mode, c.Language,
            profile.Shape, new(c.Tier, false), profile.Measurement);
    }

    internal static bool MatchesVisualProfile(BasicQuestionContract c, ArithmeticQuizQuestion generated)
    {
        if (c.Family != BankQuestionFamily.Data) return true;
        var saved = c.Story!.ChartProfile;
        var selected = generated.ElementaryProblem?.DataChart?.Profile;
        return saved is not null && selected is not null && IQuestionBankStore.ChartProfilesMatch(saved, selected);
    }

}
