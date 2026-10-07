using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateTimeDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Time, type, language, tier);
        int level = (int)tier;
        string min = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.001"), hours = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.002"), days = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.003");
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
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.004"), expression, days);
                for (int current = month + 1; current < endMonth; current++)
                {
                    int fullMonth = DateTime.DaysInMonth(year, current);
                    t.Constant(fullMonth);
                    expression += $"+{fullMonth}";
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.005"), expression, days);
                }
                expression += $"+{end}";
            }
            string text = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.034", ("from_dd_MM_yyyy", $"{from:dd/MM/yyyy}"), ("to_dd_MM_yyyy", $"{to:dd/MM/yyyy}"), ("from_yyyy_MM_dd", $"{from:yyyy-MM-dd}"), ("to_yyyy_MM_dd", $"{to:yyyy-MM-dd}"));
            if (level == 5)
            {
                string paused = t.Given("paused-days", _random.Next(2, 7), days);
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.006"), expression, days);
                expression = $"({expression})-{paused}";
                text = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.035", ("paused", $"{paused}"), ("from_dd_MM_yyyy", $"{from:dd/MM/yyyy}"), ("to_dd_MM_yyyy", $"{to:dd/MM/yyyy}"), ("from_yyyy_MM_dd", $"{from:yyyy-MM-dd}"), ("to_yyyy_MM_dd", $"{to:yyyy-MM-dd}"));
            }
            t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.007"), expression, days);
            return t.Build(level switch { 1 => "same-month", 2 => "adjacent-months", 3 => "multiple-months", 4 => "leap-february", _ => "leap-interval-minus-pauses" }, text);
        }
        if (type == ElementaryQuizType.ReadClock)
        {
            int hour = _random.Next(1, level == 5 ? 9 : 11);
            int minute = level switch { 1 => 0, 2 => _random.Next(1, 4) * 15, 3 => _random.Next(12) * 5, 4 => _random.Next(45, 60), _ => _random.Next(40, 60) };
            string h = t.Given("start-hour", hour), m = t.Given("start-minute", minute);
            var clock = new QuizVisualData("clock", [], [hour, minute], "", ScenarioId: "starting-clock");
            string problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.008");
            if (level <= 3)
            {
                t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.009"), h);
                t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.010"), m);
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
                    problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.011", ("elapsed", $"{elapsed}"));
                }
                else
                {
                    string hourPart = t.Given("advance-hours", 1, hours), minutePart = t.Given("advance-minutes", 30, min);
                    string reduction = t.Given("subtract-minutes", 10, min);
                    elapsed = $"({hourPart}*60+{minutePart}-{reduction})";
                    problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.012", ("hourPart", $"{hourPart}"), ("minutePart", $"{minutePart}"), ("reduction", $"{reduction}"));
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.013"), elapsed, min);
                }
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.014"), $"{m}+{elapsed}", min);
                t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.015"), $"{h}+{carry}/60");
                t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.016"), $"{m}+{elapsed}-{carry}");
            }
            return t.Build(level <= 3 ? "read-clock-precision-" + level : "clock-after-interval-" + level, problem, clock);
        }
        if (type == ElementaryQuizType.TimeAddition)
        {
            string h = t.Given("first-hours", _random.Next(1, 4), hours), m = t.Given("first-minutes", _random.Next(1, 5) * 10, min);
            string expression = $"{h}*60+{m}";
            string problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.017", ("h", $"{h}"), ("m", $"{m}"));
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.018"), expression, min);
            if (level >= 2)
            {
                string extra = t.Given("extra-minutes", _random.Next(1, 5) * 10, min);
                expression = $"({expression})+{extra}";
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.019", ("extra", $"{extra}"));
            }
            if (level >= 4)
            {
                string other = t.Given("second-hours", _random.Next(1, 3), hours);
                expression = $"({expression})+{other}*60";
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.020", ("other", $"{other}"));
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.021"), expression, min);
            }
            if (level >= 3)
            {
                string pause = t.Given("pause-minutes", _random.Next(1, 4) * 5, min);
                expression = $"({expression})-{pause}";
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.022", ("pause", $"{pause}"));
            }
            string unit = level == 5 ? hours : min;
            if (level == 5)
            {
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.023"), expression, min);
                expression = $"({expression})/60";
            }
            t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.024"), expression, unit);
            return t.Build("intervals-and-conversion-" + level,
                problem + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.025", ("unit", $"{unit}")));
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
        string wording = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.036", ("startHour", $"{startHour}"), ("endHour", $"{endHour}"), ("ending_24_60_ng_y_h_m_sau_c_ng_ng_y", $"{(ending >= 24 * 60 ? "ngày hôm sau" : "cùng ngày")}"), ("ending_24_60_the_next_day_on_the_same_day", $"{(ending >= 24 * 60 ? "the next day" : "on the same day")}"), ("startMinute_00", $"{startMinute:00}"), ("endMinute_00", $"{endMinute:00}"));
        t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.026"), $"{sh}*60+{sm}", min);
        t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.027"), endExpression, min);
        if (level >= 4)
        {
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.028"), elapsedExpression, min);
            string pause = t.Given("pause-minutes", _random.Next(10, 31), min);
            elapsedExpression = $"({elapsedExpression})-{pause}";
            wording += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.029", ("pause", $"{pause}"));
        }
        if (level == 5)
        {
            string second = t.Given("second-session", _random.Next(20, 61), min);
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.030"), elapsedExpression, min);
            elapsedExpression = $"({elapsedExpression})+{second}";
            wording += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.031", ("second", $"{second}"));
        }
        t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.032"), elapsedExpression, min);
        return t.Build("elapsed-" + level, wording + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.033"));
    }
}
