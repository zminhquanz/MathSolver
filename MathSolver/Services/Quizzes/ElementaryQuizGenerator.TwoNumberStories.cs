using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private static string TwoNumberStory(DifficultyBuilder t, QuizStoryContext c, string first, string second, bool sum, int level)
    {
        string F(string role) => t.Givens.Single(g => g.Role == role).Value;
        string unit = c.Unit(t.Language);
        string pair = t.L($"{first.ToLowerInvariant()} và {second.ToLowerInvariant()}", $"{first.ToLowerInvariant()} and {second.ToLowerInvariant()}");
        string relation = t.L(sum ? $"tổng số {unit} của {pair}" : $"số {unit} mà {second.ToLowerInvariant()} nhiều hơn {first.ToLowerInvariant()}",
            sum ? $"the combined quantity for {pair}" : $"the excess quantity in {second.ToLowerInvariant()} over {first.ToLowerInvariant()}");
        string text = t.L($"Trong hoạt động {c.ViSetting}, xét {pair}. ", $"For {c.EnSetting}, consider {pair}. ");
        if (level == 1)
            text += t.L($"Biết {relation} là {F("quantity")} {unit}. ", $"We know that {relation} is {F("quantity")} {unit}. ");
        else if (level == 2)
            text += t.L($"Nếu nhân đôi {relation} thì được {F("double-quantity")} {unit}. ",
                $"Doubling {relation} gives {F("double-quantity")} {unit}. ");
        else if (level == 3)
            text += t.L($"Theo bản ghi, {relation} bằng {F("quantity-first")} {unit} cộng với {F("quantity-second")} {unit}. ",
                $"The record shows that {relation} equals {F("quantity-first")} {unit} plus {F("quantity-second")} {unit}. ");
        else
        {
            text += t.L(sum ? $"Mỗi bên được bổ sung {F("added")} {unit}. " : $"Riêng {second.ToLowerInvariant()} được bổ sung {F("added")} {unit}, bên kia giữ nguyên. ",
                sum ? $"Each receives {F("added")} additional {unit}. " : $"Only {second.ToLowerInvariant()} receives {F("added")} additional {unit}; the other is unchanged. ");
            if (level == 5) text += t.L($"Sau đó {second.ToLowerInvariant()} chuyển ra ngoài {F("previous-reduction")} {unit}. ",
                $"Then {second.ToLowerInvariant()} sends {F("previous-reduction")} {unit} elsewhere. ");
            text += t.L($"Lúc này {relation} là {F("after")} {unit}. ", $"After these changes, {relation} is {F("after")} {unit}. ");
        }
        if (t.Type == ElementaryQuizType.SumDifference)
            text += level <= 3
                ? t.L($"{second} nhiều hơn {first.ToLowerInvariant()} {F("difference")} {unit}. ",
                    $"{second} has {F("difference")} more {unit} than {first.ToLowerInvariant()}. ")
                : t.L($"Xét riêng số lượng ban đầu: nếu chuyển {F("transfer")} {unit} từ {second.ToLowerInvariant()} sang {first.ToLowerInvariant()} thì bên thứ hai còn nhiều hơn bên thứ nhất {F("remaining-difference")} {unit}. ",
                    $"For the original quantities separately, transferring {F("transfer")} {unit} from {second.ToLowerInvariant()} to {first.ToLowerInvariant()} leaves the second with {F("remaining-difference")} more {unit} than the first. ");
        else
            text += level < 5
                ? t.L($"Ban đầu, tỉ số số lượng của {pair} là {F("ratio-small")}/{F("ratio-large")}. ",
                    $"Originally, the ratio of quantities in {pair} is {F("ratio-small")}/{F("ratio-large")}. ")
                : t.L($"Ban đầu, khi số lượng bên thứ nhất biểu diễn bằng {F("ratio-small")} phần bằng nhau thì bên thứ hai có nhiều hơn {F("ratio-extra")} phần cùng cỡ. ",
                    $"Originally, when the first quantity is represented by {F("ratio-small")} equal parts, the second has {F("ratio-extra")} more parts of the same size. ");
        return text + t.L("Hỏi ban đầu mỗi bên có bao nhiêu?", "What was each original quantity?");
    }
}
