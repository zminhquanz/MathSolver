using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Separates multiple final answers without changing decimal notation in a single answer.</summary>
internal static partial class EssayAnswerListParser
{
    internal static bool PreserveDecimalCommas(ElementaryQuizContract contract) =>
        contract.Kind is QuizProblemKind.Decimal or QuizProblemKind.Measurement
        || contract.Answers.Any(answer => !answer.IsText && !answer.Value.Denominator.IsOne
            && !answer.RequiredDenominator.HasValue && !answer.RequireMixedNumber);

    internal static string[] Split(string? text, int expectedCount, bool preserveDecimalCommas)
    {
        string[] entries = (text ?? "").Replace("\r", "", StringComparison.Ordinal)
            .Split([';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        // Explicit separators take priority: 1,5; 2,5 must retain its decimals,
        // and a one-answer task may itself contain a comma-separated ordered list.
        if (expectedCount <= 1 || entries.Length >= expectedCount) return entries;
        entries = entries.SelectMany(entry => ClearCommaSeparatorRegex().Split(entry))
            .Select(entry => entry.Trim()).ToArray();
        if (entries.Length >= expectedCount || preserveDecimalCommas) return entries;
        // Compact commas are separators for integer answers and requested fractions.
        // Decimal pairs use a comma followed by whitespace, labels, or a semicolon.
        return entries.SelectMany(entry => entry.Split(',', StringSplitOptions.TrimEntries)).ToArray();
    }

    [GeneratedRegex(@"(?<![0-9]),|,(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex ClearCommaSeparatorRegex();
}
