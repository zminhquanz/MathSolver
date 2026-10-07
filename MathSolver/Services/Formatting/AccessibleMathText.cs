using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>One spoken expression instead of disconnected numerator/denominator labels.</summary>
internal static partial class AccessibleMathText
{
    internal static string Format(string? text, AppLanguage language)
    {
        bool vi = language == AppLanguage.Vietnamese;
        string result = FractionPattern().Replace(text ?? string.Empty, match =>
            $"{match.Groups[1].Value} {(vi ? "trên" : "over")} {match.Groups[2].Value}");
        return result.Replace("²", vi ? " bình phương" : " squared", StringComparison.Ordinal)
            .Replace("𝑥", "x", StringComparison.Ordinal)
            .Replace("³", vi ? " lập phương" : " cubed", StringComparison.Ordinal)
            .Replace("?", vi ? "chưa biết" : "unknown", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<![\w/])(-?\d[\d.,]*)\s*/\s*(-?\d[\d.,]*)(?![\w/])")]
    private static partial Regex FractionPattern();
}
