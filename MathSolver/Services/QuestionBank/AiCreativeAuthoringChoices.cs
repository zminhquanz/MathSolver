using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>
/// AI authoring offers story problems where fresh, coherent prose adds value.
/// The full catalogue remains available for C# practice and saved questions.
/// </summary>
public static class AiCreativeAuthoringChoices
{
    public static IReadOnlyList<BankQuestionFamily> Families { get; } = Array.AsReadOnly(new[]
    {
        BankQuestionFamily.Arithmetic, BankQuestionFamily.FindX, BankQuestionFamily.TwoNumbers,
        BankQuestionFamily.Average, BankQuestionFamily.Percentage, BankQuestionFamily.MultiStep,
        BankQuestionFamily.Motion, BankQuestionFamily.Proportion, BankQuestionFamily.Decimal,
        BankQuestionFamily.Measurement, BankQuestionFamily.Remainder, BankQuestionFamily.Time,
        BankQuestionFamily.FractionQuantity, BankQuestionFamily.Geometry
    });

    public static bool Supports(BankQuestionFamily family) => Families.Contains(family);

    public static int SelectionIndex(BankQuestionFamily family)
    {
        for (int index = 0; index < Families.Count; index++)
            if (Families[index] == family) return index;
        return 0;
    }

    public static BankQuestionFamily FamilyAt(int index) => index >= 0 && index < Families.Count
        ? Families[index] : BankQuestionFamily.Arithmetic;

    public static int[] Variants(BankQuestionFamily family, CurriculumTier tier)
    {
        if (!Supports(family)) return [];
        int[] available = ReasoningStoryCatalogue.Variants(family, tier);
        return family switch
        {
            BankQuestionFamily.Average => available.Where(variant =>
                variant != (int)AverageQuizType.IndirectData).ToArray(),
            BankQuestionFamily.MultiStep => available.Where(variant =>
                variant == (int)ElementaryQuizType.MultiStepRemaining).ToArray(),
            BankQuestionFamily.Decimal => available.Where(variant => (ElementaryQuizType)variant is
                ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract or
                ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide).ToArray(),
            BankQuestionFamily.Measurement => available.Where(variant =>
                variant == (int)ElementaryQuizType.MapScale).ToArray(),
            BankQuestionFamily.Remainder => available.Where(variant => (ElementaryQuizType)variant is
                ElementaryQuizType.MinimumGroups or ElementaryQuizType.FullGroups).ToArray(),
            BankQuestionFamily.Time => available.Where(variant => (ElementaryQuizType)variant is
                ElementaryQuizType.SameDayElapsed or ElementaryQuizType.SameDayStart or
                ElementaryQuizType.SameDayEnd or ElementaryQuizType.SameDayActiveDuration or
                ElementaryQuizType.OvernightElapsed or ElementaryQuizType.SameMonthElapsed or
                ElementaryQuizType.SameMonthInclusive or ElementaryQuizType.AdjacentMonthElapsed or
                ElementaryQuizType.AdjacentMonthInclusive or ElementaryQuizType.FebruaryCommonInclusive or
                ElementaryQuizType.FebruaryLeapInclusive).ToArray(),
            BankQuestionFamily.FractionQuantity => available.Where(variant => (ElementaryQuizType)variant is
                ElementaryQuizType.FractionOfNumber or ElementaryQuizType.WholeFromFraction or
                ElementaryQuizType.FractionRemaining).ToArray(),
            BankQuestionFamily.Geometry => available.Where(variant =>
                variant == ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Rectangle, GeometryMeasurement.Area) ||
                variant == ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Rectangle, GeometryMeasurement.Perimeter) ||
                variant == ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Triangle, GeometryMeasurement.Area) ||
                variant == ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Parallelogram, GeometryMeasurement.Area) ||
                variant == ReasoningStoryCatalogue.GeometryVariant(GeometryQuizShape.Trapezoid, GeometryMeasurement.Area)).ToArray(),
            _ => available
        };
    }
}
