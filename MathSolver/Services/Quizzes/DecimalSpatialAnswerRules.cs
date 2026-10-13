using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

internal static class DecimalSpatialAnswerRules
{
    internal static bool MatchesOrder(string entered, string expected)
    {
        entered = entered.Trim().TrimEnd('.');
        if (entered.Length > 4096) return false;
        // Decimal commas remain inside a number. A list comma must have following whitespace.
        const string number = @"\d+(?:[.,]\d+)?";
        const string separator = @"\s*(?:->|→|;|,\s+|-|<|>)\s*";
        if (!Regex.IsMatch(entered, "^" + number + "(?:" + separator + number + ")+$")) return false;
        decimal[] required = expected.Split(';').Select(Parse).ToArray();
        string direction = entered.Replace("->", "").Replace("→", "");
        if (direction.Contains('<') && required[0] > required[^1] || direction.Contains('>') && required[0] < required[^1]) return false;
        return Regex.Split(entered, separator).Select(Parse).SequenceEqual(required);
    }

    private static decimal Parse(string value) => decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
        CultureInfo.InvariantCulture, out decimal result) ? result : -1;
}
