using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract AddTimeStory(ElementaryQuizContract contract)
    {
        bool late = contract.Reasoning?.Tier >= CurriculumTier.ThreeStars;
        string[] ids = late ? ["tourism", "events", "schedule"] : ["library", "sports", "craft", "schedule"];
        string id = ids[NextContextVariant(contract.Type, contract.Language, "time-story", ids.Length)];
        string introduction = contract.Language == AppLanguage.Vietnamese ? id switch
        {
            "library" => "Một nhóm ghi lại lịch đọc sách. ", "sports" => "Đội thể thao theo dõi lịch luyện tập. ",
            "craft" => "Tổ thủ công ghi lại thời gian làm việc. ", "events" => "Ban tổ chức theo dõi lịch chương trình văn nghệ. ",
            "tourism" => "Đoàn tham quan ghi lại lịch trình chuyến đi. ", _ => "Một nhóm ghi lại lịch hoạt động. "
        } : id switch
        {
            "library" => "A group records its reading schedule. ", "sports" => "A sports team records its training schedule. ",
            "craft" => "A craft team records its working times. ", "events" => "The organisers record the event schedule. ",
            "tourism" => "A tour group records its itinerary. ", _ => "A group records its activity schedule. "
        };
        return contract with { ProblemText = introduction + contract.ProblemText, StoryContextId = id };
    }

    private sealed record PackingStory(string Id, string ViItem, string EnItem,
        string ViContainer, string EnContainer, int Capacity, int MaximumSize);
    private static readonly PackingStory[] PackingStories = [
        new("traffic", "người", "people", "xe", "vehicles", 500, 45),
        new("distribution", "quyển sách", "books", "hộp", "boxes", 1000, 30),
        new("kitchen", "chiếc bánh", "cakes", "khay", "trays", 300, 12),
        new("events", "khách", "guests", "hàng ghế", "seating rows", 500, 20),
        new("community", "phần quà", "gifts", "thùng", "boxes", 500, 20)
    ];
    private ElementaryQuizContract CreateRemainderStory(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Remainder, type, language, tier);
        var s = PackingStories[NextContextVariant(type, language, "packing-story", PackingStories.Length)];
        int level = (int)tier, size = _random.Next(2, Math.Min(s.MaximumSize - (level == 5 ? 3 : 0), 4 + level * 4) + 1);
        int full = _random.Next(1, Math.Min(2 + level * 3, s.Capacity / size));
        int remainder = level == 1 ? 0 : _random.Next(1, size), total = full * size + remainder;
        string item = t.L(s.ViItem, s.EnItem), container = t.L(s.ViContainer, s.EnContainer);
        string amount, problem;
        if (level <= 2)
        {
            amount = t.Given("total", total, item);
            problem = t.L($"Có {amount} {item} cần xếp vào {container}. ", $"There are {amount} {item} to allocate to {container}. ");
        }
        else
        {
            int first = Math.Max(1, total / 2);
            string a = t.Given("first-batch", first, item), b = t.Given("second-batch", total - first, item);
            amount = $"({a}+{b})";
            problem = t.L($"Có hai đợt riêng biệt với {a} và {b} {item}. ", $"Two separate batches contain {a} and {b} {item}. ");
            if (level >= 4)
            {
                int removed = _random.Next(1, Math.Max(2, total / 3));
                total -= removed; full = Math.DivRem(total, size, out remainder);
                string used = t.Given("already-accommodated", removed, item);
                problem += t.L($"Đã bố trí riêng {used} {item}, không tính vào số cần xếp tiếp. ",
                    $"{used} {item} have already been accommodated separately and are excluded from the remaining allocation. ");
                amount = $"({amount}-{used})";
            }
            t.Step(t.L("Số lượng cần xếp", "Quantity to allocate"), amount, item);
        }
        string per;
        if (level == 5) {
            int reserved = _random.Next(1, 4);
            string nominal = t.Given("nominal-capacity", size + reserved, item), excluded = t.Given("reserved-capacity", reserved, item);
            per = $"({nominal}-{excluded})";
            problem += t.L($"Mỗi {container} có sức chứa {nominal} {item}, nhưng dành riêng chỗ cho {excluded} {item} khác nên không dùng phần chỗ đó. ",
                $"Each {container} has capacity for {nominal} {item}, but space for {excluded} other {item} is reserved and unavailable. ");
            // Keep nominal physical capacities within the selected context.
            // The working capacity is derived rather than stated as another given.
        }
        else {
            per = t.Given("capacity", size, item);
            problem += t.L($"Mỗi {container} chứa tối đa {per} {item}. ", $"Each {container} holds at most {per} {item}. ");
        }
        // Record the quotient/remainder as derived constants, never as supplied facts.
        t.Constant(full, remainder);
        string q = $"({amount}-{remainder})/{per}", r = $"{amount}-{full}*{per}";
        if (type == ElementaryQuizType.MinimumGroups)
        {
            problem += t.L($"Cần ít nhất bao nhiêu {container} để xếp hết?", $"How many {container} are needed to accommodate everyone or everything?");
            t.Answer(t.L("Số nhóm tối thiểu", "Minimum groups"), q + (remainder > 0 ? "+1" : ""), container);
        }
        else
        {
            problem += t.L($"Xếp đầy được bao nhiêu {container} và còn lại bao nhiêu {item}?",
                $"How many full {container} can be filled, and how many {item} remain?");
            t.Answer(t.L("Số nhóm đầy", "Full groups"), q, container);
            t.Answer(t.L("Số lượng còn lại", "Quantity left over"), r, item);
        }
        return t.Build("packing-" + level, problem) with { StoryContextId = s.Id };
    }
}
