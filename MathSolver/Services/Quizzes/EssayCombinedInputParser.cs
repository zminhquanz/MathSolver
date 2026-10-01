using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>
/// Separates a written submission before passing its parts to the independent
/// solution, calculation, and final answer validators.
/// </summary>
public static partial class EssayCombinedInputParser
{
    public static (string Solution, string Equation, string Answer) Parse(
        string? input,
        bool requiresSolution,
        bool preserveAllCalculations = false)
    {
        string[] lines = (input ?? string.Empty).Normalize()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var work = new List<string>();
        var answers = new List<string>();
        foreach (string line in lines)
        {
            Match label = AnswerLabelRegex().Match(line);
            if (!label.Success)
            {
                work.Add(line);
                continue;
            }

            // Support a labeled answer on its own line or after the calculation.
            string prefix = line[..label.Index].Trim();
            if (prefix.Length > 0)
                work.Add(prefix);
            answers.Add(line[(label.Index + label.Length)..].Trim());
        }

        // A bare final value with an optional unit is also accepted. Never
        // infer the answer from an equation: an omitted answer must be reported.
        if (answers.Count == 0 && work.Count > 0 &&
            BareAnswerRegex().IsMatch(work[^1]))
        {
            answers.Add(work[^1]);
            work.RemoveAt(work.Count - 1);
        }

        // Numeric questions can also include an optional written explanation.
        // Whether that explanation is required remains the validator's decision.
        bool includesSolution = requiresSolution || work.Any(line => line.Any(char.IsLetter));
        var parts = Split(string.Join(Environment.NewLine, work),
            includesSolution, preserveAllCalculations);
        // Multiple answers stay visible to the validator rather than silently
        // choosing a correct answer and hiding a conflicting one.
        return (parts.Solution, parts.Equation, string.Join(Environment.NewLine, answers));
    }

    public static (string Solution, string Equation) Split(
        string? input,
        bool requiresSolution,
        bool preserveAllCalculations = false)
    {
        string text = (input ?? string.Empty).Trim();
        if (!requiresSolution || text.Length == 0)
            return (string.Empty, text);

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (preserveAllCalculations)
        {
            var solutions = new List<string>();
            var equations = new List<string>();
            foreach (string line in lines)
            {
                int equals = line.IndexOf('=');
                if (equals < 0)
                {
                    solutions.Add(line);
                    continue;
                }
                string stepEquation = line;
                // A colon may mean division. Only separate a sentence when
                // its suffix is a complete arithmetic expression.
                for (int colon = line.IndexOf(':'); colon >= 0 && colon < equals;
                     colon = line.IndexOf(':', colon + 1))
                {
                    if (line[..colon].Any(char.IsLetter) &&
                        EssayCalculationEvaluator.TryEvaluate(line[(colon + 1)..equals], out _, out bool operation) && operation)
                    {
                        solutions.Add(line[..(colon + 1)]);
                        stepEquation = line[(colon + 1)..].Trim();
                        break;
                    }
                }
                equations.Add(stepEquation);
            }
            return (string.Join(Environment.NewLine, solutions), string.Join(Environment.NewLine, equations));
        }

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

    [GeneratedRegex(@"(?<!\p{L})(?:đáp\s*số|dap\s*so|đáp\s*án|dap\s*an|final\s+answer|answer)\s*[:=]\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnswerLabelRegex();

    [GeneratedRegex(@"^[+\-−]?\d(?:[\d.,\u00A0\u202F ]*\d)?(?:\s*/\s*[+\-−]?\d+)?\s*(?:%|\p{L}[\p{L}\p{N}\s²³^/.-]*)?[.!]?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex BareAnswerRegex();
}
