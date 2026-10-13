using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>Small language grammar from JSON; C# owns the digits and class decomposition.</summary>
public sealed record NumberNameGrammar(string Style, string[] Digits, string[] SmallNumbers, string[] Tens,
    string[] Scales, string Hundred, string HundredJoin, string TensJoin, string ZeroTens,
    string Ten, string TensMarker, string OneAfterTens, string FiveAfterTens,
    string ClassJoin, bool PadLowerHundreds, string[] PlaceNames, Dictionary<string, string> ReadingAliases);

public static class PracticeNumberNames
{
    public const int Maximum = 999_999_999;

    public static int Next(Random random, CurriculumTier tier, int maximum = Maximum)
    {
        if (maximum is < 1 or > Maximum) throw new ArgumentOutOfRangeException(nameof(maximum));
        int digits = random.Next(1, maximum.ToString(System.Globalization.CultureInfo.InvariantCulture).Length + 1);
        int minimum = (int)Math.Pow(10, digits - 1);
        int cap = (int)Math.Min(maximum, (long)minimum * 10 - 1);
        int value = random.Next(minimum, cap + 1);
        if (tier == CurriculumTier.FiveStars && digits >= 7 && random.Next(3) == 0)
        {
            int wholeClass = value / 1_000_000 * 1_000_000;
            int available = Math.Min(99, maximum - wholeClass);
            value = wholeClass + (available > 0 ? random.Next(1, available + 1) : 0);
        }
        return value;
    }

    public static NumberNameGrammar Grammar(AppLanguage language) =>
        QuizContentCatalog.LoadList<NumberNameGrammar>("Foundation.NumberNameGrammar", QuizContentCatalog.Culture(language)).Single();

    public static string Format(int value, AppLanguage language)
    {
        if (value is < 0 or > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
        var g = Grammar(language);
        if (value == 0) return g.Digits[0];
        var parts = new List<string>();
        for (int scale = 2; scale >= 0; scale--)
        {
            int divisor = scale == 2 ? 1_000_000 : scale == 1 ? 1_000 : 1;
            int group = value / divisor % 1_000;
            if (group == 0) continue;
            string words = Group(group, parts.Count > 0 && g.PadLowerHundreds, g);
            if (scale > 0) words += " " + g.Scales[scale];
            // English joins a final sub-hundred group with 'and'.
            string join = g.Style == "English" && scale == 0 && group < 100 ? g.HundredJoin : g.ClassJoin;
            parts.Add((parts.Count > 0 ? join : "") + words);
        }
        return string.Concat(parts).Trim();
    }

    public static string[] Aliases(string canonical, AppLanguage language)
    {
        var aliases = new HashSet<string> { canonical };
        foreach (var (from, to) in Grammar(language).ReadingAliases)
            foreach (string text in aliases.ToArray()) aliases.Add(text.Replace(from, to, StringComparison.Ordinal));
        return aliases.Where(s => s != canonical).ToArray();
    }

    private static string Group(int value, bool pad, NumberNameGrammar g)
    {
        int hundreds = value / 100, tens = value / 10 % 10, ones = value % 10;
        string head = hundreds > 0 || pad ? g.Digits[hundreds] + " " + g.Hundred : "";
        int rest = value % 100;
        if (rest == 0) return head;
        if (g.Style == "English")
        {
            string tail = rest < 20 ? g.SmallNumbers[rest] : g.Tens[tens] + (ones > 0 ? g.TensJoin + g.Digits[ones] : "");
            return head + (head.Length > 0 ? g.HundredJoin : "") + tail;
        }
        string result = tens >= 2 ? g.Digits[tens] + " " + g.TensMarker : tens == 1 ? g.Ten : "";
        if (tens == 0 && head.Length > 0) result = g.ZeroTens;
        if (ones > 0) result += (result.Length > 0 ? " " : "") + (ones == 1 && tens >= 2 ? g.OneAfterTens
            : ones == 5 && tens >= 1 ? g.FiveAfterTens : g.Digits[ones]);
        return head + (head.Length > 0 ? " " : "") + result;
    }

    public static void Validate(NumberNameGrammar g)
    {
        if (g.Style is not ("English" or "Vietnamese") || g.Digits is null || g.Scales is null || g.PlaceNames is null
            || g.SmallNumbers is null || g.Tens is null || g.ReadingAliases is null || g.Digits.Length != 10 || g.Scales.Length != 3
            || g.PlaceNames.Length != 9 || g.Digits.Concat(g.PlaceNames).Any(string.IsNullOrWhiteSpace)
            || g.Style == "English" && (g.SmallNumbers.Length != 20 || g.Tens.Length != 10)
            || string.IsNullOrWhiteSpace(g.Hundred) || g.Scales.Skip(1).Any(string.IsNullOrWhiteSpace)
            || g.ReadingAliases.Any(pair => string.IsNullOrEmpty(pair.Key) || pair.Value is null)
            || g.Style == "Vietnamese" && new[] { g.Ten, g.TensMarker, g.ZeroTens, g.OneAfterTens, g.FiveAfterTens }.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Invalid number-name grammar.");
    }
}
