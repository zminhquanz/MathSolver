using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckResponsivePresentation()
    {
        foreach (double width in new[] { 320d, 360, 480, 600, 720, 920, 1024, 1280, 1440 })
        {
            Require(QuizResponsiveLayout.UseSingleColumnChoices(width, 1, 60, true) || width >= 916,
                "Long two-answer choices must not be squeezed into phone columns.");
            if (width < 920) Require(!QuizResponsiveLayout.PlaceDiagramBesideQuestion(width, 1),
                "A diagram must stay below the question in a narrow viewport.");
        }
        Require(!QuizResponsiveLayout.UseSingleColumnChoices(1024, 1, 18, true),
            "Tablet width should allow two columns for short two-answer choices.");
        Require(QuizResponsiveLayout.UseSingleColumnChoices(1024, 2, 60, true),
            "Large text must reflow tablet choices too.");
        Require(QuizResponsiveLayout.PlaceDiagramBesideQuestion(1024, 1) &&
            !QuizResponsiveLayout.PlaceDiagramBesideQuestion(1024, 1.5), "Enlarged text needs more room per diagram column.");
        Require(QuizResponsiveLayout.UseCompactSettings(720, 1.5) &&
            !QuizResponsiveLayout.UseCompactSettings(1024, 1), "Settings must adapt to logical width and text scale.");

        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            string fraction = AccessibleMathText.Format("2 3/4 m² = ?", language);
            Require(fraction.Contains(language == AppLanguage.Vietnamese ? "3 trên 4" : "3 over 4"),
                "A stacked fraction must be read as one fraction.");
            Require(!fraction.Contains('?') && !fraction.Contains('²'), "Unknowns and powers need spoken descriptions.");
            Require(AccessibleMathText.Format("3 m/s", language) == "3 m/s", "A speed unit must not be parsed as a fraction.");
            var visual = new QuizVisualData("bar", ["A", "B"], [7, 987654321], "kg", HiddenValueIndices: new HashSet<int> { 1 });
            string before = QuizDiagramDescriptionFormatter.Format(null, visual, false, language);
            Require(before.Contains('7') && !before.Contains("987654321"), "Screen reader exposed a hidden chart value.");
            Require(QuizDiagramDescriptionFormatter.Format(null, visual, true, language).Contains("987654321"),
                "A graded chart should describe its revealed value.");
            var diagram = new QuizDiagram("geometry", "a = ?", [], DimensionLabels: new Dictionary<string, string>
                { ["a"] = "? cm", ["b"] = "12 cm" }, Explanation: "SECRET");
            before = QuizDiagramDescriptionFormatter.Format(diagram, null, false, language);
            Require(before.Contains("12 cm") && !before.Contains("SECRET"), "Screen reader exposed an ungraded explanation.");
            var clock = new QuizVisualData("clock", [], [3, 15], "");
            Require(!string.IsNullOrWhiteSpace(QuizDiagramDescriptionFormatter.Format(null, clock, false, language)),
                "A clock without printed labels still needs descriptions of its hands.");
            var lines = new QuizVisualData("line-pairs", [], [], "", Lines: [new("AB", 30), new("CD", 120)]);
            before = QuizDiagramDescriptionFormatter.Format(null, lines, false, language);
            Require(before.Contains("AB") && before.Contains("30") && before.Contains("CD") && before.Contains("120"),
                "Line diagrams need orientation data as well as their names.");
        }
    }
}
