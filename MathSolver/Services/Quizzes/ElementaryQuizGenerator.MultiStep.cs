using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateMultiStep(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.MultiStep, type, language, tier);
        int level = (int)tier;
        string[] ids = ["library", "craft", "community", "distribution"];
        var context = QuizStoryContextCatalog.Find(ids[NextContextVariant(type, language, "multi-step", ids.Length)]);
        string unit = context.Unit(language);
        int cap = Math.Min(context.MaximumPerPeriod, level == 1 ? 24 : level == 2 ? 96 : 200);
        int per = _random.Next(2, Math.Max(3, cap / 12));
        int groups = _random.Next(2, 4), secondGroups = _random.Next(2, 4);
        int baseTotal = per * groups, extra = per * secondGroups;
        string label = t.L("Số lượng cần tìm", "Requested quantity"), expression, problem;
        string intro = context.Id switch
        {
            "library" => t.L("Tại thư viện, ", "At the library, "),
            "craft" => t.L("Trong buổi gấp hoa giấy, ", "During a paper flower workshop, "),
            "community" => t.L("Tại điểm quyên góp sách, ", "At a book donation point, "),
            _ => t.L("Trong đợt chuẩn bị quà, ", "During gift preparation, ")
        };
        if (type == ElementaryQuizType.MultiStepAddSubtract)
        {
            if (level <= 3)
            {
                string first = t.Given("first", baseTotal, unit), second = t.Given("second", extra, unit);
                string combined = $"{first}+{second}";
                t.Step(t.L("Tổng hai phần", "Total of two portions"), combined, unit);
                problem = intro + t.L($"hai phần riêng biệt có {first} và {second} {unit}. ", $"two separate portions contain {first} and {second} {unit}. ");
                if (level == 3)
                {
                    string third = t.Given("third", per, unit);
                    combined = $"({combined})+{third}";
                    t.Step(t.L("Tổng ba phần", "Total of three portions"), combined, unit);
                    problem += t.L($"Gộp thêm phần thứ ba có {third} {unit}. ", $"A third portion of {third} {unit} is added. ");
                }
                string used = t.Given("used", per, unit);
                expression = $"({combined})-{used}";
                problem += t.L($"Sau đó lấy ra {used} {unit}. Hỏi còn lại bao nhiêu {unit}?", $"Then {used} {unit} are removed. How many {unit} remain?");
            }
            else
            {
                string remaining = t.Given("remaining", baseTotal + extra - per, unit), removed = t.Given("removed", per, unit);
                problem = intro + t.L($"ban đầu có hai phần riêng biệt. Sau khi lấy ra {removed} {unit}, còn {remaining} {unit}. ",
                    $"there are initially two separate portions. After removing {removed} {unit}, {remaining} {unit} remain. ");
                string original = $"{remaining}+{removed}";
                if (level == 5)
                {
                    string added = t.Given("added", per, unit);
                    // Remaining above is after adding then removing; the second initial portion is smaller by per.
                    original = $"({original})-{added}";
                    problem += t.L($"Trước khi lấy ra đã gộp thêm {added} {unit} từ bên ngoài. ", $"Before removing anything, {added} {unit} were added from outside. ");
                    t.Step(t.L("Tổng trước khi loại phần thêm", "Total before excluding the addition"), $"{remaining}+{removed}", unit);
                }
                t.Step(t.L("Tổng ban đầu", "Original total"), original, unit);
                {
                    string each = t.Given("each", per, unit), count = t.Given("groups", groups, t.L("nhóm", "groups"));
                    string first = $"{each}*{count}";
                    t.Step(t.L("Phần thứ nhất", "First portion"), first, unit);
                    expression = $"({original})-({first})";
                    problem += t.L($"Phần thứ nhất gồm {count} nhóm bằng nhau, mỗi nhóm {each} {unit}. ", $"The first portion has {count} equal groups of {each} {unit}. ");
                }
                problem += t.L($"Hỏi phần thứ hai ban đầu có bao nhiêu {unit}?", $"How many {unit} were in the second initial portion?");
            }
        }
        else
        {
            string each = t.Given("each", per, unit), count = t.Given("groups", level == 5 ? groups + 1 : groups, t.L("nhóm", "groups"));
            string active = count;
            if (level == 5)
            {
                string unused = t.Given("unused-groups", 1, t.L("nhóm", "groups"));
                active = $"({count}-{unused})";
                t.Step(t.L("Số nhóm được sử dụng", "Number of groups used"), active, t.L("nhóm", "groups"));
            }
            string combined = $"{each}*{active}";
            t.Step(t.L("Lượng của các nhóm đầu", "Amount in the first groups"), combined, unit);
            problem = intro + t.L($"có {count} nhóm, mỗi nhóm {each} {unit}. ", $"there are {count} groups of {each} {unit}. ");
            if (level == 5) problem += t.L("Một nhóm không được sử dụng; chỉ lấy lượng của các nhóm còn lại. ", "One group is not used; only take the amount in the remaining groups. ");
            if (level >= 3)
            {
                string second;
                if (level == 3)
                {
                    second = t.Given("second", extra, unit);
                    problem += t.L($"Một đợt riêng khác có {second} {unit}. ", $"A separate batch contains {second} {unit}. ");
                }
                else
                {
                    string eachSecond = t.Given("each-second", per, unit), countSecond = t.Given("groups-second", secondGroups, t.L("nhóm", "groups"));
                    second = $"{eachSecond}*{countSecond}";
                    t.Step(t.L("Lượng của các nhóm sau", "Amount in the second groups"), second, unit);
                    problem += t.L($"Một đợt riêng khác có {countSecond} nhóm, mỗi nhóm {eachSecond} {unit}. ", $"A separate batch has {countSecond} groups of {eachSecond} {unit}. ");
                }
                combined = $"({combined})+({second})";
                t.Step(t.L("Tổng các đợt", "Combined batches"), combined, unit);
            }
            bool subtract = type == ElementaryQuizType.MultiStepRemaining || level >= 4;
            int adjustment = per;
            if (type != ElementaryQuizType.MultiStepShare || level > 1)
            {
                string delta = t.Given(subtract ? "used" : "extra", adjustment, unit);
                combined = $"({combined}){(subtract ? "-" : "+")}{delta}";
                problem += subtract ? t.L($"Lấy ra {delta} {unit} từ tổng lượng đó. ", $"Remove {delta} {unit} from that total. ")
                    : t.L($"Gộp thêm {delta} {unit} riêng lẻ. ", $"Add another {delta} individual {unit}. ");
            }
            if (type == ElementaryQuizType.MultiStepShare)
            {
                if (t.Steps[^1].Expression != combined) t.Step(t.L("Lượng cần chia", "Amount to share"), combined, unit);
                string shares = t.Given("shares", per, t.L("phần", "shares"));
                expression = $"({combined})/{shares}";
                problem += t.L($"Chia đều lượng đó thành {shares} phần. Hỏi mỗi phần có bao nhiêu {unit}?", $"Share the resulting amount equally into {shares} portions. How many {unit} are in each portion?");
            }
            else
            {
                expression = combined;
                problem += t.L($"Hỏi sau các hoạt động trên có bao nhiêu {unit}?", $"How many {unit} are there after these activities?");
            }
        }
        t.Answer(label, expression, unit);
        return t.Build("multi-step-" + type + "-" + level, problem) with { StoryContextId = context.Id };
    }
}
