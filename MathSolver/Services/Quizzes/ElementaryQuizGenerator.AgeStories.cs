using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateAgeTwoNumbers(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.TwoNumbers, type, language, tier);
        int level = (int)tier, youngerParts = _random.Next(1, 4), olderParts = youngerParts + _random.Next(1, 3);
        int onePart = _random.Next(3, 9), years = level >= 2 ? _random.Next(1, 4) : 0;
        // At five stars the ratio applies to past ages, not the current ages being asked for.
        int younger = youngerParts * onePart + (level == 5 ? years : 0);
        int older = olderParts * onePart + (level == 5 ? years : 0);
        bool sum = type != ElementaryQuizType.DifferenceRatio;
        string unit = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.001"), first = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.002"), second = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.003");
        string text, quantity;
        t.Constant(2);
        if (level == 1)
        {
            quantity = t.Given("age-quantity", sum ? younger + older : older - younger, unit);
            text = (sum ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.019", ("quantity", $"{quantity}")) : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.020", ("quantity", $"{quantity}")));
        }
        else
        {
            string elapsed = t.Given("elapsed-years", years, QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.004"));
            int futureValue = sum ? younger + older + 2 * years : older - younger;
            string future;
            if (level == 3) {
                int half = futureValue / 2;
                string a = t.Given("age-quantity-first", half, unit), b = t.Given("age-quantity-second", futureValue - half, unit);
                future = $"({a}+{b})";
            }
            else if (level >= 4) {
                string doubled = t.Given("double-future-age-quantity", futureValue * 2, unit);
                future = $"({doubled}/2)";
            }
            else future = t.Given("future-age-quantity", futureValue, unit);
            quantity = sum ? $"({future}-2*{elapsed})" : future;
            string quantityName = (sum ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.021") : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.022"));
            text = level >= 4
                ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.005", ("elapsed", $"{elapsed}"), ("quantityName", $"{quantityName}"), ("F_double_future_age_quantity", $"{F("double-future-age-quantity")}"))
                : level == 3
                ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.006", ("elapsed", $"{elapsed}"), ("quantityName", $"{quantityName}"), ("F_age_quantity_first", $"{F("age-quantity-first")}"), ("F_age_quantity_second", $"{F("age-quantity-second")}"))
                : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.007", ("elapsed", $"{elapsed}"), ("quantityName", $"{quantityName}"), ("future", $"{future}"));
            if (level >= 3) t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.008"), future, unit);
            if (sum) t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.009"), quantity, unit);
        }
        if (type == ElementaryQuizType.SumDifference)
        {
            string gap;
            if (level == 5) {
                string gapTwice = t.Given("double-age-gap", 2 * (older - younger), unit);
                gap = $"({gapTwice}/2)";
                text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.010", ("gapTwice", $"{gapTwice}"));
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.011"), gap, unit);
            }
            else {
                gap = t.Given("age-gap", older - younger, unit);
                text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.012", ("gap", $"{gap}"));
            }
            t.Answer(first, $"({quantity}-{gap})/2", unit);
            t.Answer(second, $"({quantity}+{gap})/2", unit);
        }
        else
        {
            string p = t.Given("ratio-small", youngerParts), q = t.Given("ratio-large", olderParts);
            if (level == 5)
            {
                string past = t.Given("past-years", years, QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.013"));
                text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.014", ("past", $"{past}"), ("p", $"{p}"), ("q", $"{q}"));
                if (sum) quantity = $"({quantity}-2*{past})";
                string per = sum ? $"{quantity}/({p}+{q})" : $"{quantity}/({q}-{p})";
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.015"), per, unit);
                t.Answer(first, $"({per})*{p}+{past}", unit);
                t.Answer(second, $"({per})*{q}+{past}", unit);
            }
            else
            {
                text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.016", ("p", $"{p}"), ("q", $"{q}"));
                string per = sum ? $"{quantity}/({p}+{q})" : $"{quantity}/({q}-{p})";
                t.Answer(first, $"({per})*{p}", unit);
                t.Answer(second, $"({per})*{q}", unit);
            }
        }
        text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.017");
        return t.Build("age-" + level, text, diagram: new("bars", QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.AgeStories.CreateAgeTwoNumbers.018"),
            [new(first, [new("?")]), new(second, [new("?")])])) with { StoryContextId = "family-age" };
        string F(string role) => t.Givens.Single(g => g.Role == role).Value;
    }
}
