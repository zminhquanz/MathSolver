using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Formats a known calculation for display, without modifying its evaluation expression or result.</summary>
internal static partial class QuizMathExpressionFormatter
{
    internal static string Format(string expression, bool preserveFractions = false) =>
        OperatorRegex().Replace(expression, match => match.Groups["operator"].Value switch
        {
            "*" or "×" => " × ",
            "/" when preserveFractions => match.Value,
            _ => " ÷ "
        });

    [GeneratedRegex(@"[ \t]*(?<operator>[*×/÷:])[ \t]*", RegexOptions.CultureInvariant)]
    private static partial Regex OperatorRegex();
}
