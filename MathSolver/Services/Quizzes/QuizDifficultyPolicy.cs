using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>C# math puzzle data and rules.</summary>
public static class QuizDifficultyPolicy
{
    public static int? Level(QuizCurriculumContext? context) => context.HasValue
        ? Math.Clamp((int)context.Value.Tier, 1, 5) : null;

    public static IReadOnlyList<T> Prefer<T>(IReadOnlyList<T> available, T? requested,
        int? level, Func<int, T[]> progression) where T : struct, Enum
    {
        if (requested.HasValue || !level.HasValue) return available;
        T[] preferred = progression(level.Value).Where(available.Contains).ToArray();
        return preferred.Length > 0 ? preferred : available;
    }

    public static AverageQuizType[] AverageTypes(int level) => level switch
    {
        1 => [AverageQuizType.Direct, AverageQuizType.TotalToAverage],
        2 => [AverageQuizType.Direct, AverageQuizType.AverageToTotal],
        3 => [AverageQuizType.MissingValue, AverageQuizType.AverageToTotal],
        4 => [AverageQuizType.IndirectData, AverageQuizType.TwoGroups],
        _ => [AverageQuizType.IndirectData, AverageQuizType.TwoGroups, AverageQuizType.MissingValue]
    };

    public static MotionQuizType[] MotionTypes(int level) => level switch
    {
        <= 2 => [MotionQuizType.Basic],
        3 => [MotionQuizType.Chasing, MotionQuizType.Meeting],
        _ => [MotionQuizType.Chasing, MotionQuizType.Meeting, MotionQuizType.River]
    };

    public static ProportionQuizType[] ProportionTypes(int level) => level switch
    {
        1 => [ProportionQuizType.Direct],
        5 => [ProportionQuizType.Inverse],
        _ => Enum.GetValues<ProportionQuizType>()
    };

    public static PercentageQuizType[] PercentageTypes(int level) => level switch
    {
        1 => [PercentageQuizType.FindPercentageValue],
        2 => [PercentageQuizType.FindPercentageValue, PercentageQuizType.FindPercentageRatio],
        _ => Enum.GetValues<PercentageQuizType>()
    };

    public static int[] Percentages(int level) => level switch
    {
        1 => [50],
        2 => [10, 20, 25, 50, 75],
        3 => [15, 30, 35, 40, 60, 65, 80, 90],
        4 => [12, 24, 36, 45, 55, 64, 72, 85],
        _ => [7, 13, 17, 23, 37, 43, 57, 73, 87, 93]
    };

    public static int[] ShapeFamilies(ElementaryQuizType type, int level) => type switch
    {
        ElementaryQuizType.CountSides => level switch
        {
            1 => [0, 1, 2], 2 => [1, 2, 3, 4], 3 => [3, 4, 5, 6],
            4 => [5, 6, 7, 8], _ => [6, 7, 8, 9]
        },
        ElementaryQuizType.RectangleSide => level switch
        {
            1 => [0, 2, 5], 2 => [0, 1, 2, 3], 3 => [1, 3, 4, 7],
            4 => [1, 3, 4, 6], _ => [3, 4, 6, 8]
        },
        ElementaryQuizType.CompositeArea => level switch
        {
            <= 2 => [0, 1], 3 => [0, 2, 5], 4 => [2, 3, 5, 6], _ => [3, 4, 6, 7]
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
