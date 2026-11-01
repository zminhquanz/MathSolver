using MathSolver.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public sealed partial class EssayAnswerValidator
{
    /// <summary>
    /// A remainder-derived constant may coincide with a given. Require the actual
    /// total/capacity division before treating its quotient as a reusable result.
    /// The elementary validator remains responsible for arithmetic and units.
    /// </summary>
    private static bool ValidateRemainderGroupsWork(ArithmeticQuizQuestion q, string? equations)
    {
        var p = q.ElementaryProblem;
        if (p?.StoryContextId?.StartsWith(RemainderGroupsPilot.Prefix, StringComparison.Ordinal) != true) return true;
        if (!RemainderGroupsPilot.IsValidModel(p)) return false;
        int total = int.Parse(p.Facts[0], CultureInfo.InvariantCulture);
        int capacity = int.Parse(p.Facts[1], CultureInfo.InvariantCulture);
        int full = Math.DivRem(total, capacity, out int remainder);
        int extra = remainder == 0 ? 0 : 1;
        bool minimum = p.Type == ElementaryQuizType.MinimumGroups;
        string quotient = $"({total}-{remainder})/{capacity}";
        string[] lines = (equations ?? "").Replace("\r", "", StringComparison.Ordinal)
            .Split(['\n', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length is < 1 or > 64 || (equations?.Length ?? 0) > 32768) return false;
        bool fullReached = false, targetReached = false;
        bool SameStructure(string actual, string expected) =>
            EssayCalculationEvaluator.TryGetStructure(actual, out string a, fractionLiterals: false)
            && EssayCalculationEvaluator.TryGetStructure(expected, out string b, fractionLiterals: false) && a == b;
        foreach (string line in lines)
        {
            var division = Regex.Match(line,
                @"\A\s*(?<a>\d+)\s*(?:÷|/|:)\s*(?<b>\d+)\s*=\s*(?<q>\d+)\s*(?:dư|du|remainder|r)\s*(?<r>\d+)\s*\z",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (division.Success)
            {
                bool Is(string field, int expected) => division.Groups[field].Value == expected.ToString(CultureInfo.InvariantCulture);
                if (!Is("a", total) || !Is("b", capacity) || !Is("q", full) || !Is("r", remainder)) return false;
                fullReached = true;
                targetReached |= !minimum || remainder == 0;
                continue;
            }
            var (calculation, _) = SplitEquationResult(q, line, allowFractionResult: true);
            int equals = calculation.IndexOf('=');
            if (equals <= 0) return false;
            string expression = calculation[..equals];
            bool source = SameStructure(expression, quotient)
                || remainder == 0 && SameStructure(expression, $"{total}/{capacity}");
            bool combined = minimum && (SameStructure(expression, $"{quotient}+{extra}")
                || SameStructure(expression, $"{extra}+{quotient}")
                || remainder == 0 && (SameStructure(expression, $"{total}/{capacity}+0")
                    || SameStructure(expression, $"0+{total}/{capacity}")));
            bool reused = minimum && fullReached && (SameStructure(expression, $"{full}+{extra}")
                || SameStructure(expression, $"{extra}+{full}"));
            if (!source && !combined && !reused) return false;
            if (source) fullReached = true;
            targetReached |= combined || reused || source && (!minimum || remainder == 0);
        }
        return targetReached;
    }
}
