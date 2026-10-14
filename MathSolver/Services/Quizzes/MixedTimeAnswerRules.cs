using MathSolver.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Elapsed durations, not time of day: never wrap at 24 hours.</summary>
internal static partial class MixedTimeAnswerRules
{
    internal static string Format(int totalSeconds, bool hours, bool seconds, AppLanguage language) =>
        Parts(hours ? totalSeconds / 3600 : 0, hours ? totalSeconds / 60 % 60 : totalSeconds / 60,
            totalSeconds % 60, hours, seconds, language);

    private static string Parts(int h, int m, int s, bool hours, bool seconds, AppLanguage language)
    {
        string Unit(string key) => QuizContentCatalog.Text(language, "MixedTime." + key);
        var parts = new List<string>();
        if (hours) parts.Add(h.ToString(CultureInfo.InvariantCulture) + " " + Unit("Hours"));
        parts.Add(m.ToString(CultureInfo.InvariantCulture) + " " + Unit("Minutes"));
        if (seconds) parts.Add(s.ToString(CultureInfo.InvariantCulture) + " " + Unit("Seconds"));
        return string.Join(" ", parts);
    }

    internal static bool TryRead(string? input, out int totalSeconds)
    {
        totalSeconds = 0;
        if (input is null || input.Length > 1024) return false;
        string text = Prefix().Replace(input.Normalize().Trim(), "").Trim().TrimEnd('.');
        if (text.Length == 0 || text.Contains('\n') || text.Contains('\r')) return false;
        var matches = Components().Matches(text);
        int position = 0, previousFactor = int.MaxValue;
        long total = 0;
        foreach (Match match in matches)
        {
            if (!Gap(text[position..match.Index])) return false;
            string unit = match.Groups["unit"].Value.ToLowerInvariant();
            int multiplier = unit is "h" or "hr" or "hrs" or "hour" or "hours" or "giờ" or "gio" ? 3600
                : unit is "m" or "min" or "mins" or "minute" or "minutes" or "phút" or "phut" ? 60 : 1;
            if (multiplier >= previousFactor) return false; // Duplicate or reversed units are ambiguous.
            total += long.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture) * multiplier;
            previousFactor = multiplier;
            position = match.Index + match.Length;
        }
        if (matches.Count == 0) return false;
        string tail = text[position..].Trim();
        if (tail.Length > 0 && !Gap(tail))
        {
            // Common keyboard shorthand: 2h30, 2h30m15, 4min20.
            if (previousFactor <= 1 || tail.Length > 2 || !tail.All(char.IsAsciiDigit)
                || !int.TryParse(tail, out int minor) || minor >= 60) return false;
            total += minor * (previousFactor / 60);
        }
        if (total > int.MaxValue) return false;
        totalSeconds = (int)total;
        return true;
    }

    private static bool Gap(string text) => text.All(c => char.IsWhiteSpace(c) || c is ',' or ';');

    internal static string WorkedSolution(ElementaryQuizType type, int left, int right, int factor,
        bool hours, bool seconds, AppLanguage language)
    {
        string F(int total) => Format(total, hours, seconds, language);
        string P(int h, int m, int s) => Parts(h, m, s, hours, seconds, language);
        var work = new List<string>();
        int lh = hours ? left / 3600 : 0, lm = hours ? left / 60 % 60 : left / 60, ls = seconds ? left % 60 : 0;
        int rh = hours ? right / 3600 : 0, rm = hours ? right / 60 % 60 : right / 60, rs = seconds ? right % 60 : 0;
        if (type == ElementaryQuizType.MixedTimeDivide)
        {
            // Converting to the smallest supplied unit makes remainders exact and visible.
            int scale = seconds ? 1 : 60;
            string unit = QuizContentCatalog.Text(language, seconds ? "MixedTime.Seconds" : "MixedTime.Minutes");
            string converted = hours ? $"{lh} × {(seconds ? 3600 : 60)} + {lm}" + (seconds ? $" × 60 + {ls}" : "")
                : $"{lm} × 60 + {ls}";
            work.Add($"{F(left)} = {converted} = {left / scale} {unit}");
            work.Add($"{left / scale} {unit} ÷ {factor} = {left / scale / factor} {unit}");
            work.Add($"{F(left)} ÷ {factor} = {F(left / factor)}");
            return string.Join("\n", work);
        }

        int h, m, s, result;
        string expression;
        if (type == ElementaryQuizType.MixedTimeSubtract)
        {
            int bh = lh, bm = lm, bs = ls;
            if (seconds && bs < rs) { bs += 60; bm--; }
            if (hours && bm < rm) { bm += 60; bh--; }
            if (bh != lh || bm != lm || bs != ls) work.Add($"{F(left)} = {P(bh, bm, bs)}");
            h = bh - rh; m = bm - rm; s = bs - rs;
            result = left - right;
            expression = $"{P(bh, bm, bs)} − {F(right)}";
        }
        else
        {
            bool add = type == ElementaryQuizType.MixedTimeAdd;
            h = add ? lh + rh : lh * factor; m = add ? lm + rm : lm * factor; s = add ? ls + rs : ls * factor;
            result = add ? left + right : left * factor;
            expression = add ? $"{F(left)} + {F(right)}" : $"{F(left)} × {factor}";
        }
        string ungrouped = P(h, m, s);
        work.Add($"{expression} = {ungrouped}");
        if (seconds && s >= 60)
        {
            string unit = QuizContentCatalog.Text(language, "MixedTime.Seconds");
            work.Add($"{s} {unit} = {Parts(0, s / 60, s % 60, false, true, language)}");
            m += s / 60; s %= 60;
        }
        if (hours && m >= 60)
        {
            string unit = QuizContentCatalog.Text(language, "MixedTime.Minutes");
            work.Add($"{m} {unit} = {Parts(m / 60, m % 60, 0, true, false, language)}");
        }
        if (ungrouped != F(result)) work.Add($"{expression} = {F(result)}");
        return string.Join("\n", work);
    }

    [GeneratedRegex(@"^(?:đáp\s*số|đáp\s*án|kết\s*quả|answer|result)\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex Prefix();
    [GeneratedRegex(@"(?<value>[0-9]{1,7})\s*(?<unit>hours?|hrs?|giờ|gio|h|minutes?|mins?|phút|phut|m|seconds?|secs?|giây|giay|s)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex Components();
}
