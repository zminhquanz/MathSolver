using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateDecimalStory(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Decimal, type, language, tier);
        string[] ids = ["kitchen", "decoration", "water"];
        string id = ids[NextContextVariant(type, language, "decimal-story", ids.Length)];
        var c = QuizStoryContextCatalog.Find(id);
        string unit = t.L(id == "kitchen" ? "kg" : id == "water" ? "l" : "m", id == "kitchen" ? "kg" : id == "water" ? "litres" : "m");
        string item = t.L(id == "kitchen" ? "gạo" : id == "water" ? "nước" : "ruy băng", id == "kitchen" ? "rice" : id == "water" ? "water" : "ribbon");
        int level = (int)tier, scale = level <= 2 ? 10 : 100;
        int maximum = id == "decoration" ? 50 : id == "kitchen" ? 100 : 1000;
        decimal x = _random.Next(2, Math.Min(maximum / 6, 5 + 2 * level)) + _random.Next(1, scale) / (decimal)scale;
        decimal y = _random.Next(2, 6);
        if (type is ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract)
            y += _random.Next(1, scale) / (decimal)scale;
        if (level <= 2)
        {
            x = _random.Next(6, 9) + (level == 1 || type == ElementaryQuizType.DecimalSubtract ? .2m : .8m);
            if (type is ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract)
                y = _random.Next(2, 6) + (level == 1 ? .1m : .7m);
            if (type == ElementaryQuizType.DecimalDivide && level == 1) x = _random.Next(2, 6);
        }
        if (type == ElementaryQuizType.DecimalSubtract && x < y) (x, y) = (y, x);
        if (type == ElementaryQuizType.DecimalDivide) x *= y;
        string a = t.Given("quantity", x, unit), b = t.Given("second-quantity", y,
            type is ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide ? t.L("phần", "portions") : unit);
        if (level >= 3) return CreateIndirectDecimalStory(t, c.Id, item, unit, type, level, x, y);
        string problem, op;
        if (type == ElementaryQuizType.DecimalAdd) {
            op = "+";
            problem = t.L($"Hai phần {item} riêng biệt có số đo {a} {unit} và {b} {unit}. Hỏi tổng số đo của hai phần là bao nhiêu {unit}?",
                $"Two separate portions of {item} measure {a} {unit} and {b} {unit}. What is their combined measurement in {unit}?");
        }
        else if (type == ElementaryQuizType.DecimalSubtract) {
            op = "-";
            problem = t.L($"Ban đầu có {a} {unit} {item}. Đã dùng {b} {unit} từ lượng đó. Hỏi còn lại bao nhiêu {unit}?",
                $"There are initially {a} {unit} of {item}. {b} {unit} are used from that amount. How many {unit} remain?");
        }
        else if (type == ElementaryQuizType.DecimalMultiply) {
            op = "*";
            problem = t.L($"Mỗi phần có {a} {unit} {item}. Chuẩn bị {b} phần bằng nhau như vậy. Hỏi cần tất cả bao nhiêu {unit}?",
                $"Each portion contains {a} {unit} of {item}. {b} equal portions are prepared. How many {unit} are needed in total?");
        }
        else {
            op = "/";
            problem = t.L($"Có {a} {unit} {item}, chia đều thành {b} phần. Hỏi mỗi phần có bao nhiêu {unit}?",
                $"There are {a} {unit} of {item}, shared equally into {b} portions. How many {unit} are in each portion?");
        }
        t.Answer(t.L($"Số đo {item} cần tìm", $"Required measurement of {item}"), $"{a}{op}{b}", unit);
        return t.Build("decimal-" + type, problem) with { StoryContextId = c.Id };
    }

    private ElementaryQuizContract CreateIndirectDecimalStory(DifficultyBuilder t, string contextId, string item,
        string unit, ElementaryQuizType type, int level, decimal x, decimal y)
    {
        t.Givens.Clear(); t.Units.Clear();
        bool conversion = level == 5;
        int factor = contextId == "decoration" ? 100 : 1000;
        string smallUnit = contextId == "decoration" ? "cm" : contextId == "kitchen" ? "g" : "ml";
        string first = t.Given("quantity", conversion ? x * factor : x, conversion ? smallUnit : unit);
        string left = conversion ? $"({first}/{factor})" : first;
        string prefix = t.L($"Phần đầu có {first} {(conversion ? smallUnit : unit)} {item}. ",
            $"The first portion contains {first} {(conversion ? smallUnit : unit)} of {item}. ");
        if (conversion) { t.Constant(factor); t.Step(t.L("Đổi số đo phần đầu", "Convert the first measurement"), left, unit); }
        string expression, problem;
        if (type == ElementaryQuizType.DecimalAdd)
        {
            string gap = t.Given("difference", y, unit);
            string second = $"({left}+{gap})";
            t.Step(t.L("Số đo phần thứ hai", "Second portion measurement"), second, unit);
            expression = $"{left}+{second}";
            problem = prefix + t.L($"Phần thứ hai nhiều hơn phần đầu {gap} {unit}. ", $"The second portion exceeds the first by {gap} {unit}. ");
        }
        else if (type == ElementaryQuizType.DecimalSubtract)
        {
            // Use two portions from one total; no unmentioned or overlapping amount.
            string used = t.Given("used-first", y / 2, unit), other = t.Given("used-second", y / 2, unit);
            expression = $"{left}-({used}+{other})";
            t.Step(t.L("Tổng lượng đã dùng", "Total amount used"), $"{used}+{other}", unit);
            problem = t.L($"Ban đầu có {first} {(conversion ? smallUnit : unit)} {item}. Đã dùng lần lượt {used} {unit} và {other} {unit} từ lượng đó. ",
                $"Initially there are {first} {(conversion ? smallUnit : unit)} of {item}. {used} {unit} and {other} {unit} are used from that amount. ");
        }
        else
        {
            string extra = t.Given("extra-quantity", type == ElementaryQuizType.DecimalMultiply ? .5m : y / 2, unit);
            string count = t.Given("portion-count", y, t.L("phần", "portions"));
            string combined = $"({left}+{extra})";
            t.Step(t.L("Lượng trước khi tính", "Amount before calculating"), combined, unit);
            if (type == ElementaryQuizType.DecimalMultiply)
            {
                expression = $"{combined}*{count}";
                problem = prefix + t.L($"Mỗi phần cần thêm {extra} {unit} nữa. Chuẩn bị {count} phần giống nhau. ",
                    $"Each portion needs an additional {extra} {unit}. Prepare {count} identical portions. ");
            }
            else
            {
                expression = $"{combined}/{count}";
                problem = prefix + t.L($"Gộp với phần thứ hai có {extra} {unit}, rồi chia đều thành {count} phần. ",
                    $"Combine it with a second portion of {extra} {unit}, then share equally into {count} portions. ");
            }
        }
        if (level >= 4)
        {
            string returned = t.Given("adjustment", .25m, unit);
            t.Step(t.L(type == ElementaryQuizType.DecimalDivide ? "Lượng mỗi phần sau khi chia" : "Lượng trước khi bổ sung", type == ElementaryQuizType.DecimalDivide ? "Amount in each share" : "Amount before the addition"), expression, unit);
            expression = $"({expression})+{returned}";
            problem += type == ElementaryQuizType.DecimalDivide
                ? t.L($"Sau khi chia, bổ sung riêng {returned} {unit} {item} vào phần thứ nhất. ", $"After sharing, add another {returned} {unit} of {item} to the first share only. ")
                : t.L($"Sau đó gộp thêm {returned} {unit} {item} từ bên ngoài vào lượng đang có. ", $"Then add another {returned} {unit} of {item} from outside to the amount available. ");
        }
        string label = t.L($"Số đo {item} cần tìm", $"Required measurement of {item}");
        string question = type switch
        {
            ElementaryQuizType.DecimalSubtract => t.L($"Hỏi còn lại bao nhiêu {unit}?", $"How many {unit} remain?"),
            ElementaryQuizType.DecimalDivide => t.L($"Hỏi phần thứ nhất cuối cùng có bao nhiêu {unit}?", $"How many {unit} are in the first share at the end?"),
            _ => t.L($"Hỏi cuối cùng có tất cả bao nhiêu {unit} {item}?", $"How many {unit} of {item} are there in total at the end?")
        };
        t.Answer(label, expression, unit);
        return t.Build("decimal-story-" + type + "-" + level, problem + question) with { StoryContextId = contextId };
    }
}
