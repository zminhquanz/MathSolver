using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckIllustratedChoices()
    {
        var catalog = new QuizProblemTypeCatalog();
        foreach (var option in catalog.Options)
            if (!QuizChoiceCatalog.Definitions.ContainsKey(option.LocalizationKey))
                throw new Exception("Missing topic illustration: " + option.LocalizationKey);
        foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
        foreach (var type in ElementaryQuizGenerator.Types(kind))
            if (!QuizChoiceCatalog.Definitions.ContainsKey("Quiz.Elementary." + type))
                throw new Exception("Missing skill illustration: " + type);

        var illustrationIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "mixed", "arithmetic", "fraction", "find-x", "geometry", "geometry-square", "geometry-rectangle",
            "geometry-triangle", "geometry-trapezoid", "geometry-rhombus", "geometry-parallelogram", "geometry-circle",
            "geometry-cube", "geometry-prism", "visual-angle", "visual-parallel", "visual-perpendicular", "two-numbers",
            "geometry-right-triangle", "geometry-equilateral-triangle", "geometry-isosceles-trapezoid",
            "geometry-right-trapezoid", "geometry-sphere", "geometry-cylinder", "geometry-cone",
            "measurement", "clock", "motion", "percentage", "probability", "number-place-value", "number-counting",
            "remainder", "decimal", "proportion", "average", "data", "multi-step", "expression",
            "find-x-sum", "find-x-difference", "find-x-minuend", "find-x-product", "find-x-quotient", "find-x-dividend",
            "arithmetic-add", "arithmetic-subtract", "arithmetic-multiply", "arithmetic-divide"
        };
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            foreach (var definition in QuizChoiceCatalog.Definitions.Values)
            {
                var choice = QuizChoiceCatalog.Create(7, definition.Key, "label", language, true);
                if (string.IsNullOrWhiteSpace(choice.Description) || !illustrationIds.Contains(choice.IllustrationId))
                    throw new Exception("Invalid choice content: " + definition.Key);
            }
            foreach (string key in new[] { "Choice.SelectProblem", "Choice.SelectSubtype", "Choice.SelectShape", "Choice.Search",
                         "Choice.Empty", "Choice.Close", "Choice.Selected" })
                if (string.IsNullOrWhiteSpace(QuizContentCatalog.Text(language, key))) throw new Exception("Missing chooser text: " + key);
        }

        QuizChoiceOption[] examples =
        [
            new(12, "measurement", "Đo lường", "Đổi đơn vị độ dài", "measurement", false),
            new(38, "fraction", "Phân số", "Các phần bằng nhau", "fraction", true),
            new(4, "clock", "Thời gian", "Đọc đồng hồ", "clock", false)
        ];
        var fractions = QuizChoiceCatalog.Filter(examples, "phan so");
        if (fractions.Count != 1 || fractions[0].Index != 38 || !fractions[0].IsSelected)
            throw new Exception("Filtering changed the chosen question route.");
        if (QuizChoiceCatalog.Filter(examples, "doi dai").Single().Index != 12)
            throw new Exception("Vietnamese search must match descriptions without diacritics.");
        if (QuizChoiceCatalog.Filter(examples, "not available").Count != 0 || QuizChoiceCatalog.Filter(examples, " ").Count != 3)
            throw new Exception("Empty search must not drop selections or invent matches.");
        Console.WriteLine($"  {QuizChoiceCatalog.Definitions.Count} illustrated choices validated in both languages; filtered routes stay stable.");
    }
}
