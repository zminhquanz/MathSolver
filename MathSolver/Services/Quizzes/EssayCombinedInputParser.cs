namespace MathSolver.Services;

/// <summary>
/// Separates the two parts of the combined essay editor before passing them
/// to the existing, independent solution and equation validators.
/// </summary>
public static class EssayCombinedInputParser
{
    public static (string Solution, string Equation) Split(
        string? input,
        bool requiresSolution)
    {
        string text = (input ?? string.Empty).Trim();
        if (!requiresSolution || text.Length == 0)
            return (string.Empty, text);

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // A solution sentence may occupy more than one line. The calculation
        // is the last line containing an equality, regardless of line order.
        int equationIndex = Array.FindLastIndex(lines, line => line.Contains('='));
        if (equationIndex < 0)
        {
            equationIndex = Array.FindLastIndex(lines, LooksLikeCalculation);
        }

        if (equationIndex < 0)
            return (string.Join(Environment.NewLine, lines), string.Empty);

        string equation = lines[equationIndex];
        string solution = string.Join(Environment.NewLine,
            lines.Where((_, index) => index != equationIndex));

        // Also accept a short one-line submission such as
        // "Số quả cam là: 2 + 3 = 5 quả cam".
        if (solution.Length == 0)
        {
            int colon = equation.IndexOf(':');
            if (colon >= 0 && equation[(colon + 1)..].Contains('='))
            {
                solution = equation[..(colon + 1)].Trim();
                equation = equation[(colon + 1)..].Trim();
            }
        }

        return (solution, equation);
    }

    private static bool LooksLikeCalculation(string line)
    {
        string trimmed = line.TrimStart();
        return trimmed.Length > 0 &&
               (char.IsDigit(trimmed[0]) || trimmed[0] == '(') &&
               line.Any(character => character is '+' or '-' or '−' or '×' or '*' or '÷' or '/');
    }
}
