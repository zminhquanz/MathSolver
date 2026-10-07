using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Styles the unknown variable for display; calculations and stored prose keep their original text.</summary>
internal static partial class FindXDisplayText
{
    internal static string Format(string text) => VariablePattern().Replace(text, "𝑥");

    [GeneratedRegex(@"(?<![\p{L}\p{N}_])[xX](?![\p{L}\p{N}_])", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();
}
