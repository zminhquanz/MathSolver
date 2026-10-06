using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateDecimalDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        int level = (int)tier;
        var t = new DifficultyBuilder(QuizProblemKind.Decimal, type, language, tier) { RequiresSolution = false };
        string N(decimal value) => value.ToString("0.################", System.Globalization.CultureInfo.InvariantCulture);
        if (type == ElementaryQuizType.DecimalRound)
        {
            int digits = level <= 2 ? 0 : level == 3 ? 1 : 2;
            int scale = level == 1 ? 10 : level <= 3 ? 100 : 1000;
            decimal value = level == 5 ? _random.Next(1, 40) + .995m + _random.Next(0, 5) / 1000m
                : _random.Next(1, 20 * level) + _random.Next(1, scale) / (decimal)scale;
            string a = t.Given("number", value);
            decimal rounded = decimal.Round(value, digits, MidpointRounding.AwayFromZero);
            t.Constants.Add(N(rounded));
            t.Answer(t.L("Số sau khi làm tròn", "Rounded number"), N(rounded));
            string destination = digits == 0 ? t.L("số nguyên gần nhất", "the nearest integer")
                : t.L(digits == 1 ? "hàng phần mười" : "hàng phần trăm", digits == 1 ? "one decimal place" : "two decimal places");
            return t.Build("decimal-round-" + level, t.L($"Làm tròn {a} đến {destination}.", $"Round {a} to {destination} (halves round up)."))
                with { RoundingDecimalPlaces = digits };
        }
        if (type == ElementaryQuizType.DecimalCompare)
        {
            int scale = level <= 2 ? 10 : level <= 4 ? 100 : 1000;
            decimal a = _random.Next(1, 5 + level * 5) + _random.Next(1, scale) / (decimal)scale;
            int relation = _random.Next(3);
            decimal gap = (level <= 2 ? _random.Next(1, 5) : 1) / (decimal)scale;
            if (level == 1) gap = _random.Next(1, 4); // Distinct integer parts before close fractional comparisons.
            decimal b = relation == 0 ? a : relation == 1 ? a - gap : a + gap;
            if (b <= 0) { a += 4; b += 4; }
            string first = t.Given("first", a), second = t.Given("second", b);
            t.TextAnswer(t.L("Dấu so sánh", "Comparison"), a < b ? "<" : a > b ? ">" : "=");
            return t.Build("decimal-compare-" + level,
                t.L($"Điền dấu so sánh giữa {first} và {second}.", $"Compare {first} and {second}."));
        }
        if (_random.Next(2) == 0) return CreateDecimalStory(type, language, tier);

        int precision = level <= 2 ? 10 : 100;
        int integerLimit = level <= 2 ? 10 : level == 3 ? 30 : 100;
        decimal left = _random.Next(1, integerLimit) + _random.Next(1, precision) / (decimal)precision;
        decimal right = _random.Next(1, integerLimit) + _random.Next(1, precision) / (decimal)precision;
        if (type is ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract)
        {
            if (level == 1) { left = _random.Next(6, 10) + .2m; right = _random.Next(1, 5) + .1m; }
            if (level == 2) { left = _random.Next(6, 10) + (type == ElementaryQuizType.DecimalSubtract ? .2m : .8m); right = _random.Next(1, 5) + (type == ElementaryQuizType.DecimalSubtract ? .7m : .6m); }
            if (level == 3) right = _random.Next(1, 20) + _random.Next(1, 10) / 10m;
            if (level >= 4) { left = _random.Next(61, 100) + (type == ElementaryQuizType.DecimalSubtract ? .12m : .92m); right = _random.Next(1, 60) + .89m; }
            if (level == 5) { left += .006m; right += .007m; }
            if (type == ElementaryQuizType.DecimalSubtract && left < right) (left, right) = (right, left);
        }
        else if (type == ElementaryQuizType.DecimalMultiply)
        {
            if (level == 1) left = _random.Next(1, 5) + .2m;
            if (level == 2) left = _random.Next(5, 10) + .8m;
            right = level <= 2 ? _random.Next(2, 6) : level == 3 ? _random.Next(2, 8) + .5m
                : level == 4 ? _random.Next(1, 5) + .25m : _random.Next(1, 9) / 100m;
        }
        else
        {
            decimal quotient = _random.Next(2, 20) + (level >= 2 ? _random.Next(1, 10) / 10m : 0);
            right = level <= 2 ? _random.Next(2, 6) : level == 3 ? _random.Next(1, 5) + .5m
                : level == 4 ? _random.Next(1, 4) + .25m : _random.Next(1, 9) / 100m;
            left = quotient * right; // Generate an exact terminating quotient, never round an answer.
        }
        string x = t.Given("quantity", left), y = t.Given("second-quantity", right);
        string op = type switch { ElementaryQuizType.DecimalAdd => "+", ElementaryQuizType.DecimalSubtract => "-", ElementaryQuizType.DecimalMultiply => "*", _ => "/" };
        t.Answer(t.L("Kết quả", "Result"), $"{x}{op}{y}");
        string expression = QuizMathExpressionFormatter.Format($"{x}{op}{y}");
        return t.Build("decimal-calculation-" + level, t.L($"Tính {expression}.", $"Calculate {expression}."));
    }
}
