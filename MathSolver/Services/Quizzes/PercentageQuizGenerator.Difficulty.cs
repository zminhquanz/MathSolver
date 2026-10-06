using MathSolver.Models;
using System.Numerics;

namespace MathSolver.Services;

public sealed partial class PercentageQuizGenerator
{
    private int PickPercentageWhole(int percentage, int level)
    {
        int quantum = 100 / (int)BigInteger.GreatestCommonDivisor(100, percentage);
        int maximum = new[] { 0, 20, 100, 300, 700, 1200 }[level];
        return quantum * _random.Next(2, Math.Max(3, maximum / quantum + 1));
    }

    private PercentageQuizContract CombineKnownQuantity(PercentageQuizContract contract,
        AppLanguage language, int level)
    {
        int quantity = contract.Facts[0];
        int[] parts = level == 5 && quantity >= 3 ? new int[3] : new int[2];
        int remaining = quantity;
        for (int index = 0; index < parts.Length - 1; index++)
        {
            parts[index] = _random.Next(1, remaining - (parts.Length - index - 2));
            remaining -= parts[index];
        }
        parts[^1] = remaining;
        bool vi = language == AppLanguage.Vietnamese;
        string list = string.Join(vi ? " và " : " and ", parts);
        string prefix = vi ? $"Các nhóm có lần lượt {list}" : $"The groups have {list}";
        // Replace only the supplied quantity; inferred totals never become extra facts.
        int quantityPosition = contract.ProblemText.IndexOf(quantity.ToString(), StringComparison.Ordinal);
        int suffixPosition = quantityPosition + quantity.ToString().Length;
        string problem = contract.CombinedProblemTemplate is { } template
            ? template.Replace("{quantity}", list)
            : prefix + contract.ProblemText[suffixPosition..];
        string sum = $"({string.Join(" + ", parts)})";
        string equation = contract.Type == PercentageQuizType.FindPercentageRatio
            ? contract.EquationText.Replace($"÷ {quantity} ×", $"÷ {sum} ×", StringComparison.Ordinal)
            : sum + contract.EquationText[quantity.ToString().Length..];
        return contract with
        {
            Facts = [.. parts, contract.Facts[1]],
            ProblemText = problem,
            EquationText = equation,
            SolutionText = contract.SolutionText.Replace(contract.EquationText, equation, StringComparison.Ordinal),
            CombinedQuantities = parts
        };
    }
}
