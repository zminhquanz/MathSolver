using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private static string TwoNumberStory(DifficultyBuilder t, QuizStoryContext c, string first, string second, bool sum, int level)
    {
        string F(string role) => t.Givens.Single(g => g.Role == role).Value;
        string unit = c.Unit(t.Language);
        string pair = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.001", ("first_ToLowerInvariant", $"{first.ToLowerInvariant()}"), ("second_ToLowerInvariant", $"{second.ToLowerInvariant()}"));
        string relation = (sum ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.013", ("unit", $"{unit}"), ("pair", $"{pair}")) : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.014", ("unit", $"{unit}"), ("second_ToLowerInvariant", $"{second.ToLowerInvariant()}"), ("first_ToLowerInvariant", $"{first.ToLowerInvariant()}")));
        string text = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.012", ("c_ViSetting", $"{c.ViSetting}"), ("pair", $"{pair}"), ("c_EnSetting", $"{c.EnSetting}"));
        if (level == 1)
            text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.002", ("relation", $"{relation}"), ("F_quantity", $"{F("quantity")}"), ("unit", $"{unit}"));
        else if (level == 2)
            text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.003", ("relation", $"{relation}"), ("F_double_quantity", $"{F("double-quantity")}"), ("unit", $"{unit}"));
        else if (level == 3)
            text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.004", ("relation", $"{relation}"), ("F_quantity_first", $"{F("quantity-first")}"), ("unit", $"{unit}"), ("F_quantity_second", $"{F("quantity-second")}"));
        else
        {
            text += (sum ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.015", ("F_added", $"{F("added")}"), ("unit", $"{unit}")) : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.016", ("second_ToLowerInvariant", $"{second.ToLowerInvariant()}"), ("F_added", $"{F("added")}"), ("unit", $"{unit}")));
            if (level == 5) text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.005", ("second_ToLowerInvariant", $"{second.ToLowerInvariant()}"), ("F_previous_reduction", $"{F("previous-reduction")}"), ("unit", $"{unit}"));
            text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.006", ("relation", $"{relation}"), ("F_after", $"{F("after")}"), ("unit", $"{unit}"));
        }
        if (t.Type == ElementaryQuizType.SumDifference)
            text += level <= 3
                ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.007", ("second", $"{second}"), ("first_ToLowerInvariant", $"{first.ToLowerInvariant()}"), ("F_difference", $"{F("difference")}"), ("unit", $"{unit}"))
                : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.008", ("F_transfer", $"{F("transfer")}"), ("unit", $"{unit}"), ("second_ToLowerInvariant", $"{second.ToLowerInvariant()}"), ("first_ToLowerInvariant", $"{first.ToLowerInvariant()}"), ("F_remaining_difference", $"{F("remaining-difference")}"));
        else
            text += level < 5
                ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.009", ("pair", $"{pair}"), ("F_ratio_small", $"{F("ratio-small")}"), ("F_ratio_large", $"{F("ratio-large")}"))
                : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.010", ("F_ratio_small", $"{F("ratio-small")}"), ("F_ratio_extra", $"{F("ratio-extra")}"));
        return text + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TwoNumberStories.TwoNumberStory.011");
    }
}
