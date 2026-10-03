using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateTimeDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Time, type, language, tier);
        int level = (int)tier;
        string min = t.L("phút", "minutes"), hours = t.L("giờ", "hours"), days = t.L("ngày", "days");
        t.Constant(60, 24);
        if (type == ElementaryQuizType.Calendar)
        {
            int year = level >= 4 ? 2024 : 2025;
            int month = level switch { 1 or 2 => 10, 3 => 9, 4 => 2, _ => 1 };
            int endMonth = level switch { 1 => month, 2 => 11, 3 => 11, _ => 3 };
            int firstDay = level == 1 ? _random.Next(2, 12) : _random.Next(22, 27);
            int lastDay = level == 1 ? firstDay + _random.Next(3, 12) : _random.Next(3, 10);
            string start = t.Given("start-day", firstDay), end = t.Given("end-day", lastDay);
            t.Given("year", year); t.Given("start-month", month); t.Given("end-month", endMonth);
            var from = new DateTime(year, month, firstDay);
            var to = new DateTime(year, endMonth, lastDay);
            string expression = $"{end}-{start}";
            if (endMonth > month)
            {
                int length = DateTime.DaysInMonth(year, month);
                t.Constant(length);
                expression = $"({length}-{start})";
                t.Step(t.L("Số ngày còn lại trong tháng đầu", "Remaining days in the starting month"), expression, days);
                for (int current = month + 1; current < endMonth; current++)
                {
                    int fullMonth = DateTime.DaysInMonth(year, current);
                    t.Constant(fullMonth);
                    expression += $"+{fullMonth}";
                    t.Step(t.L("Cộng tháng trọn vẹn", "Include the full month"), expression, days);
                }
                expression += $"+{end}";
            }
            string text = t.L($"Từ đầu ngày {from:dd/MM/yyyy} đến đầu ngày {to:dd/MM/yyyy} có bao nhiêu ngày?",
                $"How many days pass from the start of {from:yyyy-MM-dd} to the start of {to:yyyy-MM-dd}?");
            if (level == 5)
            {
                string paused = t.Given("paused-days", _random.Next(2, 7), days);
                t.Step(t.L("Toàn bộ khoảng thời gian", "Full date interval"), expression, days);
                expression = $"({expression})-{paused}";
                text = t.L($"Một hoạt động diễn ra từ đầu ngày {from:dd/MM/yyyy} đến đầu ngày {to:dd/MM/yyyy}, có {paused} ngày tạm dừng trong khoảng này. Có bao nhiêu ngày hoạt động?",
                    $"An activity runs from the start of {from:yyyy-MM-dd} to the start of {to:yyyy-MM-dd}, with {paused} paused days within that interval. How many days are active?");
            }
            t.Answer(t.L("Số ngày", "Days elapsed"), expression, days);
            return t.Build(level switch { 1 => "same-month", 2 => "adjacent-months", 3 => "multiple-months", 4 => "leap-february", _ => "leap-interval-minus-pauses" }, text);
        }
        if (type == ElementaryQuizType.ReadClock)
        {
            int hour = _random.Next(1, level == 5 ? 9 : 11);
            int minute = level switch { 1 => 0, 2 => _random.Next(1, 4) * 15, 3 => _random.Next(12) * 5, 4 => _random.Next(45, 60), _ => _random.Next(40, 60) };
            string h = t.Given("start-hour", hour), m = t.Given("start-minute", minute);
            var clock = new QuizVisualData("clock", [], [hour, minute], "", ScenarioId: "starting-clock");
            string problem = t.L("Đồng hồ chỉ mấy giờ, bao nhiêu phút?", "What hour and minute does the clock show?");
            if (level <= 3)
            {
                t.Answer(t.L("Giờ", "Hour"), h);
                t.Answer(t.L("Phút", "Minute"), m);
                t.RequiresSolution = false;
            }
            else
            {
                string elapsed;
                int carry = level == 4 ? 60 : 120;
                t.Constant(carry);
                if (level == 4)
                {
                    elapsed = t.Given("advance-minutes", 15, min);
                    problem = t.L($"Đồng hồ chỉ giờ bắt đầu. Sau {elapsed} phút, đồng hồ chỉ mấy giờ, bao nhiêu phút?",
                        $"The clock shows the start time. What hour and minute will it show {elapsed} minutes later?");
                }
                else
                {
                    string hourPart = t.Given("advance-hours", 1, hours), minutePart = t.Given("advance-minutes", 30, min);
                    string reduction = t.Given("subtract-minutes", 10, min);
                    elapsed = $"({hourPart}*60+{minutePart}-{reduction})";
                    problem = t.L($"Đồng hồ chỉ giờ bắt đầu. Thời gian trôi qua bằng {hourPart} giờ {minutePart} phút bớt {reduction} phút. Sau đó đồng hồ chỉ mấy giờ, bao nhiêu phút?",
                        $"The clock shows the start time. The elapsed interval is {hourPart} hour {minutePart} minutes minus {reduction} minutes. What hour and minute will it show afterwards?");
                    t.Step(t.L("Thời gian trôi qua", "Elapsed interval"), elapsed, min);
                }
                t.Step(t.L("Tổng phút trước khi chuyển giờ", "Minutes before carrying into hours"), $"{m}+{elapsed}", min);
                t.Answer(t.L("Giờ", "Hour"), $"{h}+{carry}/60");
                t.Answer(t.L("Phút", "Minute"), $"{m}+{elapsed}-{carry}");
            }
            return t.Build(level <= 3 ? "read-clock-precision-" + level : "clock-after-interval-" + level, problem, clock);
        }
        if (type == ElementaryQuizType.TimeAddition)
        {
            string h = t.Given("first-hours", _random.Next(1, 4), hours), m = t.Given("first-minutes", _random.Next(1, 5) * 10, min);
            string expression = $"{h}*60+{m}";
            string problem = t.L($"Khoảng đầu kéo dài {h} giờ {m} phút. ", $"The first interval lasts {h} hours {m} minutes. ");
            t.Step(t.L("Đổi khoảng đầu ra phút", "First interval in minutes"), expression, min);
            if (level >= 2)
            {
                string extra = t.Given("extra-minutes", _random.Next(1, 5) * 10, min);
                expression = $"({expression})+{extra}";
                problem += t.L($"Tiếp theo là {extra} phút. ", $"Then another {extra} minutes follow. ");
            }
            if (level >= 4)
            {
                string other = t.Given("second-hours", _random.Next(1, 3), hours);
                expression = $"({expression})+{other}*60";
                problem += t.L($"Khoảng cuối kéo dài {other} giờ. ", $"The last interval lasts {other} hours. ");
                t.Step(t.L("Tổng các khoảng", "Sum of the intervals"), expression, min);
            }
            if (level >= 3)
            {
                string pause = t.Given("pause-minutes", _random.Next(1, 4) * 5, min);
                expression = $"({expression})-{pause}";
                problem += t.L($"Trong các khoảng trên có tổng cộng {pause} phút nghỉ cần loại ra. ", $"The intervals include {pause} minutes of rest to exclude. ");
            }
            string unit = level == 5 ? hours : min;
            if (level == 5)
            {
                t.Step(t.L("Thời gian hoạt động theo phút", "Active time in minutes"), expression, min);
                expression = $"({expression})/60";
            }
            t.Answer(t.L("Thời gian hoạt động", "Active time"), expression, unit);
            return t.Build("intervals-and-conversion-" + level,
                problem + t.L($"Thời gian cần tính là bao nhiêu {unit}?", $"What is the requested duration in {unit}?"));
        }
        // Elapsed time uses explicit next-day wording, then excludes pauses or combines sessions.
        int startHour = level <= 2 ? _random.Next(7, 10) : _random.Next(21, 24);
        int startMinute = level == 1 ? 10 : _random.Next(25, 50);
        int duration = level == 1 ? 20 : level == 2 ? 95 : 180;
        int ending = startHour * 60 + startMinute + duration;
        int endHour = ending / 60 % 24, endMinute = ending % 60;
        string sh = t.Given("start-hour", startHour), sm = t.Given("start-minute", startMinute);
        string eh = t.Given("end-hour", endHour), em = t.Given("end-minute", endMinute);
        string endExpression = $"{eh}*60+{em}";
        if (ending >= 24 * 60) endExpression = $"24*60+{endExpression}";
        string elapsedExpression = $"({endExpression})-({sh}*60+{sm})";
        string wording = t.L($"Bắt đầu lúc {startHour}:{startMinute:00}, kết thúc lúc {endHour}:{endMinute:00} {(ending >= 24 * 60 ? "ngày hôm sau" : "cùng ngày")}. ",
            $"Start at {startHour}:{startMinute:00} and finish at {endHour}:{endMinute:00} {(ending >= 24 * 60 ? "the next day" : "on the same day")}. ");
        t.Step(t.L("Mốc bắt đầu theo phút", "Start time in minutes"), $"{sh}*60+{sm}", min);
        t.Step(t.L("Mốc kết thúc theo phút", "End time in minutes"), endExpression, min);
        if (level >= 4)
        {
            t.Step(t.L("Thời gian từ đầu đến cuối", "Elapsed duration"), elapsedExpression, min);
            string pause = t.Given("pause-minutes", _random.Next(10, 31), min);
            elapsedExpression = $"({elapsedExpression})-{pause}";
            wording += t.L($"Có {pause} phút nghỉ trong khoảng này. ", $"This interval includes {pause} minutes of rest. ");
        }
        if (level == 5)
        {
            string second = t.Given("second-session", _random.Next(20, 61), min);
            t.Step(t.L("Thời gian hoạt động buổi đầu", "Active time in the first session"), elapsedExpression, min);
            elapsedExpression = $"({elapsedExpression})+{second}";
            wording += t.L($"Sau đó có buổi khác hoạt động thêm {second} phút, không nghỉ. ", $"A later session adds {second} active minutes without rest. ");
        }
        t.Answer(t.L("Thời gian hoạt động", "Active duration"), elapsedExpression, min);
        return t.Build("elapsed-" + level, wording + t.L("Tổng thời gian hoạt động là bao nhiêu phút?", "How many active minutes are there in total?"));
    }
}
