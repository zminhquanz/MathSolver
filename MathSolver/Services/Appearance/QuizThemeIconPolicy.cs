namespace MathSolver.Services;

/// <summary>Presentation only; never changes group identity or a mathematical diagram.</summary>
internal static class QuizThemeIconPolicy
{
    internal static bool IsGroupKey(string? key) => key?.StartsWith("Learning.Group.", StringComparison.Ordinal) == true;

    internal static float StrokeForSize(float size)
    {
        if (!float.IsFinite(size) || size <= 0) return 0;
        if (size <= 32) return 1.5f;
        if (size <= 44) return 1.5f + (size - 32) / 12 * .25f;
        if (size <= 54) return 1.75f + (size - 44) / 10 * .25f;
        return 2;
    }

    internal static ReadableRgb InkOn(ReadableRgb candidate, ReadableRgb background) =>
        ReadableColorPolicy.EnsureText(candidate, background);
}
