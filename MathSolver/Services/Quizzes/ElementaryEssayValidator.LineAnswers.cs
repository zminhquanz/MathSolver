using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

internal static partial class ElementaryEssayValidator
{
    private static bool MatchesLineNames(ElementaryQuizContract contract, string entered, string expected)
    {
        if (contract.Type is not (ElementaryQuizType.ParallelLines or ElementaryQuizType.PerpendicularLines)
            || contract.Visual?.Kind != "line-pairs") return false;

        string[] ReadNames(string text)
        {
            var match = LineNamesRegex().Match(text.Normalize());
            return match.Success ? match.Groups["name"].Captures.Select(capture => capture.Value).ToArray() : [];
        }

        var expectedNames = ReadNames(expected);
        var enteredNames = ReadNames(entered);
        // Read the entire answer. Do not ignore extra names, repeated names or prose.
        return expectedNames.Length >= 2 && enteredNames.Length == expectedNames.Length
            && enteredNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() == enteredNames.Length
            && new HashSet<string>(expectedNames, StringComparer.OrdinalIgnoreCase).SetEquals(enteredNames);
    }

    [GeneratedRegex(@"^\s*(?:(?:các\s+)?(?:đường(?:\s+thẳng)?|lines?)\s+)?(?<name>[\p{L}\p{N}]+)\s*(?:(?:,\s*(?:(?:và|and)\b)?|(?:và|and)\b|&)\s*(?:(?:đường(?:\s+thẳng)?|lines?)\s+)?(?<name>[\p{L}\p{N}]+)\s*)+[.!]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LineNamesRegex();
}
