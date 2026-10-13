using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

internal static class TimeFractionAnswerRules
{
    internal static string NormalizeCentury(string text) => Regex.Replace(
        Regex.Replace(text.Trim(), @"^(?:thế\s*k[ỷỉ]|century)\s*:?\s*", "", RegexOptions.IgnoreCase),
        @"(?:st|nd|rd|th)?\s*(?:thế\s*k[ỷỉ]|century)\s*$", "", RegexOptions.IgnoreCase).Trim();

    internal static bool MatchesOrder(string entered, string expected)
    {
        entered = entered.Trim().TrimEnd('.');
        if (entered.Length > 4096) return false;
        string[] actual = Regex.Split(entered, @"\s*(?:->|→|,|;|<|>|-)\s*");
        string[] required = expected.Split(" -> ");
        if (actual.Length != required.Length) return false;
        string relations = entered.Replace("->", "", StringComparison.Ordinal).Replace("\u2192", "", StringComparison.Ordinal);
        if (relations.Contains('<') || relations.Contains('>'))
        {
            ReducedFraction.TryParse(required[0], out var first); ReducedFraction.TryParse(required[1], out var second);
            bool ascending = first.Numerator * second.Denominator < second.Numerator * first.Denominator;
            if (ascending && relations.Contains('>') || !ascending && relations.Contains('<')) return false;
        }
        for (int i = 0; i < actual.Length; i++)
            if (!Regex.IsMatch(actual[i], @"^\d+\s*/\s*\d+$")
                || !ReducedFraction.TryParse(actual[i], out var a) || !ReducedFraction.TryParse(required[i], out var b) || a != b)
                return false;
        return true;
    }
}
