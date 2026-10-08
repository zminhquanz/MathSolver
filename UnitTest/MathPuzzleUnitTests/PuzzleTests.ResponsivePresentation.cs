using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckResponsivePresentation()
    {
        foreach (double width in new[] { 280d, 320, 360, 480, 600, 768, 1024, 1440, 1920, 3840 })
        foreach (double height in new[] { 160d, 240, 320, 640, 900, 2160 })
        foreach (double scale in new[] { 1d, 1.5, 2, 3 })
        foreach (double gutter in new[] { 0d, 24 })
        foreach (int count in new[] { 0, 1, 2, 4, 9, 19, 40 })
        {
            var dialog = QuizChoiceLayout.Calculate(width, height, count, scale, scrollbarGutter: gutter);
            Require(dialog.Width > 0 && dialog.Width <= width - 24 && dialog.Height > 0 && dialog.Height <= height - 24,
                "Choice dialog must stay inside the viewport, including landscape and enlarged text.");
            Require(dialog.Columns >= 1 && dialog.Columns <= Math.Max(1, Math.Min(count, 3)),
                "Choice dialog must not create unused columns.");
            if (dialog.Columns > 1)
                Require((dialog.Width - 2 * dialog.Padding - 2 - gutter - (dialog.Columns - 1) * 10)
                    / dialog.Columns >= 320 * scale - 0.01,
                    "Every choice column needs enough room for scaled text and the scrollbar.");
            if (width <= 600) Require(dialog.Columns == 1, "Phone choices must use one column.");
            if (height < 400) Require(dialog.UseList, "A keyboard or short landscape viewport needs a readable list.");
        }
        var fourChoices = QuizChoiceLayout.Calculate(1920, 1080, 4, scrollbarGutter: 24);
        Require(fourChoices.Columns == 2 && fourChoices.Height < 500,
            "Four desktop choices should form a compact two-by-two dialog.");
        var manyChoices = QuizChoiceLayout.Calculate(1920, 1080, 19, scrollbarGutter: 24);
        Require(manyChoices.Columns == 3 && manyChoices.Height == 780,
            "Long desktop lists need three columns and a bounded scrolling viewport.");
        var singleChoice = QuizChoiceLayout.Calculate(1920, 1080, 1, scrollbarGutter: 24);
        Require(singleChoice.Height < fourChoices.Height && singleChoice.Width < fourChoices.Width,
            "A catalogue with one choice should start with a smaller dialog.");
        Require(QuizChoiceLayout.Calculate(1920, 1080, 0).Height < singleChoice.Height,
            "An empty catalogue should not start with a tall blank list.");
        Require(QuizChoiceLayout.Calculate(768, 1024, 19).Columns == 2,
            "A portrait tablet should support two comfortable columns.");
        Require(QuizChoiceLayout.Calculate(1366, 768, 19).Columns == 3,
            "A laptop should support three columns inside a bounded panel.");
        var phoneLandscape = QuizChoiceLayout.Calculate(844, 390, 19, forceSingleColumn: true);
        Require(phoneLandscape.UseList && phoneLandscape.Width == 820,
            "A rotated phone must retain a single list and use its available width.");
        var keyboard = QuizChoiceLayout.Calculate(844, 180, 19, headerHeight: 56, forceSingleColumn: true);
        Require(keyboard.UseList && keyboard.Height - (2 * keyboard.Padding + 2 + 24 + 56) >= 48,
            "A short keyboard viewport must retain at least a touch target of list height.");

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
