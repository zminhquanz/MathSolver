using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private sealed record ProbabilityTask(string ProblemText, string Reason,
        string[] Facts, ProbabilityQuizScenario Scenario, string? Expression = null, string? SetupText = null, string? ActionText = null, string? ResultText = null);

    private static string ProbabilityNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

    private ProbabilityTask CreateLikelihoodTask(AppLanguage language, int a, int b)
    {
        bool vi = language == AppLanguage.Vietnamese;
        string L(string vietnamese, string english) => vi ? vietnamese : english;
        int family = NextContextVariant(ElementaryQuizType.Likelihood, language, "context", 10);
        int category = NextContextVariant(ElementaryQuizType.Likelihood, language, "category", 3);
        string setup, eventText, contextId;
        int total, favorable;
        string[] facts;
        if (family <= 6)
        {
            string[] colors = vi ? ["đỏ", "xanh dương", "vàng", "xanh lá", "tím"]
                : ["red", "blue", "yellow", "green", "purple"];
            _random.Shuffle(colors);
            (contextId, string container, string item) = family switch
            {
                0 => ("marbles", L("Một túi", "A bag"), L("viên bi", "marbles")),
                1 => ("pencils", L("Một hộp", "A box"), L("bút chì", "pencils")),
                2 => ("blocks", L("Một rổ", "A basket"), L("khối gỗ", "wooden blocks")),
                3 => ("color-cards", L("Một bộ thẻ", "A pack"), L("thẻ", "cards")),
                4 => ("buttons", L("Một hộp", "A box"), L("chiếc cúc áo", "buttons")),
                5 => ("stickers", L("Một phong bì", "An envelope"), L("nhãn dán", "stickers")),
                _ => ("spinner", "", "")
            };
            bool mixed = category == 2;
            total = mixed ? a + b : a;
            favorable = category == 0 ? total : category == 1 ? 0 : a;
            string target = category == 1 ? colors[2] : colors[0];
            if (family == 6)
            {
                setup = L($"Một vòng quay có {total} ô bằng nhau: {a} ô màu {colors[0]}" +
                    (mixed ? $" và {b} ô màu {colors[1]}" : "") + ". Không có ô màu khác. Quay một lần; kim luôn dừng trong một ô.",
                    $"A spinner has {total} equal sectors: {a} {colors[0]} sectors" +
                    (mixed ? $" and {b} {colors[1]} sectors" : "") + ". There are no other colors. Spin once; the pointer always stops inside a sector.");
                eventText = L($"kim dừng ở ô màu {target}", $"the pointer stops in a {target} sector");
            }
            else
            {
                setup = L($"{container} có {a} {item} màu {colors[0]}" + (mixed ? $" và {b} {item} màu {colors[1]}" : "") +
                    ". Không có đồ vật nào khác. Lấy ngẫu nhiên một đồ vật mà không nhìn vào trong.",
                    $"{container} contains {a} {colors[0]} {item}" + (mixed ? $" and {b} {colors[1]} {item}" : "") +
                    ". It contains nothing else. Select one item at random without looking.");
                eventText = L($"lấy được {item} màu {target}", $"the selected item is {target}");
            }
            facts = mixed ? [ProbabilityNumber(a), ProbabilityNumber(b), ProbabilityNumber(total)] : [ProbabilityNumber(a)];
        }
        else if (family == 7)
        {
            contextId = "die";
            total = 6;
            bool alternate = _random.Next(2) == 0;
            favorable = category == 0 ? 6 : category == 1 ? 0 : 3;
            setup = L("Gieo một xúc xắc có sáu mặt, số chấm lần lượt từ 1 đến 6, rồi quan sát mặt trên.",
                "Roll a six-sided die with faces numbered 1 to 6 and observe the top face.");
            eventText = category switch
            {
                0 => alternate ? L("số chấm nhỏ hơn 7", "the number is less than 7")
                    : L("số chấm từ 1 đến 6", "the number is between 1 and 6 inclusive"),
                1 => alternate ? L("xuất hiện 7 chấm", "the die shows 7") : L("xuất hiện 0 chấm", "the die shows 0"),
                _ => alternate ? L("số chấm là số chẵn", "the number is even") : L("số chấm lớn hơn 3", "the number is greater than 3")
            };
            facts = ["1", "6", alternate ? "7" : "3", "0"];
        }
        else if (family == 8)
        {
            contextId = "number-cards";
            total = a + b;
            bool even = _random.Next(2) == 0;
            favorable = category == 0 ? total : category == 1 ? 0 : even ? total / 2 : (total + 1) / 2;
            setup = L($"Có {total} thẻ, ghi lần lượt các số từ 1 đến {total}, mỗi số trên một thẻ. Rút ngẫu nhiên một thẻ.",
                $"There are {total} cards numbered 1 to {total}, with each number on exactly one card. Draw one card at random.");
            eventText = category switch
            {
                0 => L($"số trên thẻ không lớn hơn {total}", $"the number on the card is at most {total}"),
                1 => L($"rút được thẻ ghi số {total + 1}", $"the card shows {total + 1}"),
                _ => even ? L("số trên thẻ là số chẵn", "the number on the card is even")
                    : L("số trên thẻ là số lẻ", "the number on the card is odd")
            };
            facts = ["1", ProbabilityNumber(total), ProbabilityNumber(total + 1)];
        }
        else
        {
            contextId = "coin";
            total = 2;
            favorable = category == 0 ? 2 : category == 1 ? 0 : 1;
            setup = L("Tung một đồng xu có mặt ngửa và mặt sấp. Khi đồng xu nằm phẳng, quan sát mặt ở phía trên.",
                "Toss a coin with a heads side and a tails side. When it lies flat, observe the upper side.");
            eventText = category switch
            {
                0 => L("mặt trên là mặt ngửa hoặc mặt sấp", "the upper side is either heads or tails"),
                1 => L("cả mặt ngửa và mặt sấp cùng ở phía trên", "both heads and tails are uppermost at the same time"),
                _ => _random.Next(2) == 0 ? L("mặt ngửa ở phía trên", "heads is uppermost") : L("mặt sấp ở phía trên", "tails is uppermost")
            };
            facts = [];
        }
        string[] prompts = vi
            ? [$"Sự kiện “{eventText}” là chắc chắn, có thể nhưng không chắc chắn, hay không thể?",
               $"Hãy phân loại khả năng xảy ra của sự kiện “{eventText}”: chắc chắn, có thể nhưng không chắc chắn, hoặc không thể.",
               $"Chọn nhận xét đúng cho sự kiện “{eventText}”: chắc chắn / có thể nhưng không chắc chắn / không thể."]
            : [$"Is the event “{eventText}” certain, possible but not certain, or impossible?",
               $"Classify the event “{eventText}” as certain, possible but not certain, or impossible.",
               $"Choose the correct likelihood for “{eventText}”: certain / possible but not certain / impossible."];
        string reason = favorable == total ? L("Mọi kết quả có thể xảy ra đều thỏa mãn sự kiện đã hỏi.", "Every possible outcome satisfies the requested event.")
            : favorable == 0 ? L("Không có kết quả nào thỏa mãn sự kiện đã hỏi.", "No possible outcome satisfies the requested event.")
            : L("Có kết quả thỏa mãn và có kết quả không thỏa mãn sự kiện đã hỏi.", "Some possible outcomes satisfy the event and others do not.");
        return new(setup + " " + prompts[_random.Next(prompts.Length)], reason, facts,
            new(contextId, eventText, favorable, total, false), SetupText: setup);
    }

    private ProbabilityTask CreateExperimentalProbabilityTask(AppLanguage language, int total)
    {
        bool vi = language == AppLanguage.Vietnamese;
        string L(string vietnamese, string english) => vi ? vietnamese : english;
        int family = NextContextVariant(ElementaryQuizType.ExperimentalProbability, language, "context", 10);
        (string contextId, string action, string result) = family switch
        {
            0 => ("coin", L("Tung một đồng xu", "Toss a coin"), L("mặt ngửa xuất hiện", "heads appears")),
            1 => ("die-six", L("Gieo một xúc xắc sáu mặt", "Roll a six-sided die"), L("xuất hiện mặt 6 chấm", "the die shows 6")),
            2 => ("die-even", L("Gieo một xúc xắc sáu mặt", "Roll a six-sided die"), L("xuất hiện số chấm chẵn", "the die shows an even number")),
            3 => ("spinner", L("Quay một vòng quay có ô đỏ và ô xanh", "Spin a spinner with red and blue sectors"), L("kim dừng ở ô đỏ", "the pointer stops in a red sector")),
            4 => ("marbles", L("Rút một viên bi từ túi có bi đỏ và bi xanh rồi bỏ lại vào túi sau mỗi lần rút", "Draw a marble from a bag of red and blue marbles, replacing it after every draw"), L("rút được bi đỏ", "a red marble is drawn")),
            5 => ("number-cards", L("Rút một thẻ số từ bộ thẻ ghi số chẵn và số lẻ rồi trả lại thẻ sau mỗi lần rút", "Draw a number card from a pack with even and odd numbers, replacing it after every draw"), L("rút được thẻ ghi số lẻ", "an odd-numbered card is drawn")),
            6 => ("tokens", L("Rút một thẻ màu từ hộp có thẻ vàng và thẻ tím rồi trả lại thẻ sau mỗi lần rút", "Draw a colored token from a box of yellow and purple tokens, replacing it after every draw"), L("rút được thẻ màu vàng", "a yellow token is drawn")),
            7 => ("basketball", L("Thực hiện các lượt ném bóng vào rổ", "Take basketball shots"), L("bóng vào rổ", "the shot goes into the basket")),
            8 => ("ring-toss", L("Thực hiện các lượt ném vòng vào cọc", "Toss rings at a peg"), L("vòng trúng cọc", "the ring lands on the peg")),
            _ => ("target", L("Thực hiện các lượt ném túi cát vào vùng đích", "Throw beanbags at a target area"), L("túi cát nằm trong vùng đích", "the beanbag lands in the target area"))
        };
        // Include none/all observed successes without implying the next trial is impossible/certain.
        int observed = _random.Next(total + 1);
        bool complement = _random.Next(2) == 0;
        int successes = complement ? total - observed : observed;
        string eventText = complement ? L($"sự kiện “{result}” không xảy ra", $"the event “{result}” does not occur") : result;
        string setup = L($"Một nhóm thực hiện thí nghiệm sau {total} lần: {action}. Kết quả đã ghi nhận có {observed} lần “{result}”.",
            $"A group carries out this experiment {total} times: {action}. The recorded results contain {observed} trials where “{result}”.");
        string[] prompts = vi
            ? [$"Dựa vào kết quả đã ghi nhận, viết phân số chỉ số lần {eventText} so với tổng số lần thử.",
               $"Trong các lần thử đã thực hiện, phân số biểu thị số lần {eventText} trên tổng số lần thử là bao nhiêu?",
               $"Hãy biểu diễn bằng phân số tỉ lệ số lần {eventText} trong kết quả đã ghi nhận trên."]
            : [$"Using the recorded results, express the number of trials where {eventText} as a fraction of all trials.",
               $"Write the fraction of completed trials satisfying “{eventText}”.",
               $"Write the observed fraction of trials where {eventText}."];
        string numerator = complement ? $"({total}-{observed})" : ProbabilityNumber(observed);
        string reason = L($"Số lần thỏa mãn sự kiện đã hỏi là {successes}, tổng số lần thử là {total}.",
            $"The requested event occurred in {successes} of the {total} recorded trials.");
        return new(setup + " " + prompts[_random.Next(prompts.Length)], reason,
            [ProbabilityNumber(observed), ProbabilityNumber(total)],
            new(contextId, eventText, successes, total, true), $"{numerator}/{total}", ActionText: action, ResultText: result);
    }
}
