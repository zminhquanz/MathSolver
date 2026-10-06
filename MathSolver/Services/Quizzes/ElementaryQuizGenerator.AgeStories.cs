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
        string unit = t.L("tuổi", "years"), first = t.L("Tuổi của An", "Alex's age"), second = t.L("Tuổi của Bình", "Sam's age");
        string text, quantity;
        t.Constant(2);
        if (level == 1)
        {
            quantity = t.Given("age-quantity", sum ? younger + older : older - younger, unit);
            text = t.L(sum ? $"Tổng tuổi hiện nay của An và Bình là {quantity} tuổi. " : $"Bình hơn An {quantity} tuổi. ",
                sum ? $"Alex and Sam's current ages sum to {quantity} years. " : $"Sam is {quantity} years older than Alex. ");
        }
        else
        {
            string elapsed = t.Given("elapsed-years", years, t.L("năm", "years"));
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
            string quantityName = t.L(sum ? "tổng tuổi của An và Bình" : "hiệu tuổi của Bình và An",
                sum ? "their combined age" : "the difference between Sam's and Alex's ages");
            text = level >= 4
                ? t.L($"Sau {elapsed} năm nữa, hai lần {quantityName} bằng {F("double-future-age-quantity")} tuổi. ",
                    $"In {elapsed} years, twice {quantityName} will be {F("double-future-age-quantity")} years. ")
                : level == 3
                ? t.L($"Sau {elapsed} năm nữa, {quantityName} bằng tổng của {F("age-quantity-first")} tuổi và {F("age-quantity-second")} tuổi. ",
                    $"In {elapsed} years, {quantityName} equals {F("age-quantity-first")} years plus {F("age-quantity-second")} years. ")
                : t.L($"Sau {elapsed} năm nữa, {quantityName} là {future} tuổi. ",
                    $"In {elapsed} years, {quantityName} will be {future} years. ");
            if (level >= 3) t.Step(t.L("Đại lượng tuổi ở mốc đã cho", "Age quantity at the given time"), future, unit);
            if (sum) t.Step(t.L("Tổng tuổi hiện nay", "Current combined age"), quantity, unit);
        }
        if (type == ElementaryQuizType.SumDifference)
        {
            string gap;
            if (level == 5) {
                string gapTwice = t.Given("double-age-gap", 2 * (older - younger), unit);
                gap = $"({gapTwice}/2)";
                text += t.L($"Hai lần hiệu tuổi của Bình và An là {gapTwice} tuổi. ", $"Twice the difference in their ages is {gapTwice} years. ");
                t.Step(t.L("Hiệu tuổi", "Age difference"), gap, unit);
            }
            else {
                gap = t.Given("age-gap", older - younger, unit);
                text += t.L($"Bình hơn An {gap} tuổi. ", $"Sam is {gap} years older than Alex. ");
            }
            t.Answer(first, $"({quantity}-{gap})/2", unit);
            t.Answer(second, $"({quantity}+{gap})/2", unit);
        }
        else
        {
            string p = t.Given("ratio-small", youngerParts), q = t.Given("ratio-large", olderParts);
            if (level == 5)
            {
                string past = t.Given("past-years", years, t.L("năm", "years"));
                text += t.L($"Cách đây {past} năm, tỉ số tuổi của An và Bình là {p}/{q}. ",
                    $"At a time {past} years ago, the ratio of Alex's age to Sam's age was {p}/{q}. ");
                if (sum) quantity = $"({quantity}-2*{past})";
                string per = sum ? $"{quantity}/({p}+{q})" : $"{quantity}/({q}-{p})";
                t.Step(t.L("Giá trị một phần tuổi trước đây", "One part of the past ages"), per, unit);
                t.Answer(first, $"({per})*{p}+{past}", unit);
                t.Answer(second, $"({per})*{q}+{past}", unit);
            }
            else
            {
                text += t.L($"Tỉ số tuổi hiện nay của An và Bình là {p}/{q}. ",
                    $"The ratio of Alex's current age to Sam's is {p}/{q}. ");
                string per = sum ? $"{quantity}/({p}+{q})" : $"{quantity}/({q}-{p})";
                t.Answer(first, $"({per})*{p}", unit);
                t.Answer(second, $"({per})*{q}", unit);
            }
        }
        text += t.L("Hỏi hiện nay mỗi người bao nhiêu tuổi?", "How old is each person now?");
        return t.Build("age-" + level, text, diagram: new("bars", t.L("Tuổi hiện nay", "Current ages"),
            [new(first, [new("?")]), new(second, [new("?")])])) with { StoryContextId = "family-age" };
        string F(string role) => t.Givens.Single(g => g.Role == role).Value;
    }
}
