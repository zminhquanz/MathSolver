using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckResponsivePresentation()
    {
        // A zoomed picture retains its aspect ratio through rotation, split-screen
        // and desktop resize. Fit must not require scrolling on either axis.
        foreach (double width in new[] { 280d, 320, 600, 768, 1366, 1920, 3840 })
        foreach (double height in new[] { 180d, 390, 768, 1080, 2160 })
        foreach (var source in new[] { (360d, 260d), (800d, 260d), (300d, 600d) })
        foreach (double zoom in new[] { -1d, 1, 1.25, 2, 4, 20 })
        {
            var viewport = DiagramPreviewViewport.Fit(width, height, source.Item1, source.Item2, zoom);
            Require(Math.Abs(viewport.ImageWidth / viewport.ImageHeight - source.Item1 / source.Item2) < .001,
                "Preview zoom must preserve the diagram's aspect ratio.");
            Require(viewport.Width >= width && viewport.Height >= height && viewport.Scale > 0,
                "The scroll canvas must contain the picture and fill the viewport.");
            if (zoom <= 1)
                Require(viewport.ImageWidth <= width - 20 + .001 && viewport.ImageHeight <= height - 20 + .001,
                    "Fit must leave space for scrollbar overlays and must not crop the picture.");
            var panel = DiagramPreviewViewport.Panel(width, height);
            Require(panel.Width < width && panel.Height < height && panel.Width <= 1440 && panel.Height <= 1100,
                "The preview panel must stay inside phones, tablets and desktop windows.");
        }
        var beforeZoom = DiagramPreviewViewport.Fit(800, 600, 360, 260, 1);
        var afterZoom = DiagramPreviewViewport.Fit(800, 600, 360, 260, 2);
        double x = DiagramPreviewViewport.AnchorOffset(0, 400, beforeZoom.Width, beforeZoom.ImageWidth,
            afterZoom.Width, afterZoom.ImageWidth, 800);
        double y = DiagramPreviewViewport.AnchorOffset(0, 300, beforeZoom.Height, beforeZoom.ImageHeight,
            afterZoom.Height, afterZoom.ImageHeight, 600);
        Require(Math.Abs(x - (afterZoom.Width - 800) / 2) < .001 && Math.Abs(y - (afterZoom.Height - 600) / 2) < .001,
            "Toolbar zoom must keep the centered diagram centered, including Fit margins.");
        double anchor = 250;
        double offset = DiagramPreviewViewport.AnchorOffset(100, anchor, 1400, 1400, 2100, 2100, 800);
        Require(Math.Abs((100 + anchor) / 1400 - (offset + anchor) / 2100) < .001,
            "Wheel/pinch zoom must preserve the image point under the pointer.");
        Require(DiagramPreviewViewport.AnchorOffset(700, 400, 2100, 2100, 800, 780, 800) == 0,
            "Returning to Fit must clear the previous pan offset.");
        var rotated = DiagramPreviewViewport.Fit(600, 800, 360, 260, 2);
        double rotatedX = DiagramPreviewViewport.AnchorOffset(x, 400, afterZoom.Width, afterZoom.ImageWidth,
            rotated.Width, rotated.ImageWidth, 600, 300);
        double rotatedY = DiagramPreviewViewport.AnchorOffset(y, 300, afterZoom.Height, afterZoom.ImageHeight,
            rotated.Height, rotated.ImageHeight, 800, 400);
        Require(Math.Abs(rotatedX - (rotated.Width - 600) / 2) < .001 && Math.Abs(rotatedY - (rotated.Height - 800) / 2) < .001,
            "Rotation must preserve the middle of a zoomed diagram even when Fit scale changes.");

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
