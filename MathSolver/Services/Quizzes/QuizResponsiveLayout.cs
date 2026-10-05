namespace MathSolver.Services;

/// <summary>Layout decisions use logical content width, including split-screen and enlarged text.</summary>
internal static class QuizResponsiveLayout
{
    internal static bool UseCompactSettings(double width, double textScale) =>
        width < 600 * Math.Max(1, textScale);

    internal static bool PlaceDiagramBesideQuestion(double width, double textScale) =>
        width >= 920 * Math.Max(1, textScale);

    internal static bool UseSingleColumnChoices(double width, double textScale, int longestChoice, bool multipleAnswers)
    {
        double columnWidth = Math.Max(multipleAnswers ? 300 : 240,
            Math.Clamp(longestChoice, 0, 60) * 7 + 32);
        return width < columnWidth * Math.Max(1, textScale) * 2 + 12;
    }
}
