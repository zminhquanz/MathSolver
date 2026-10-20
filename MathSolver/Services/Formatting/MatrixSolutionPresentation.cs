using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

internal sealed record MatrixSolutionTerm(string Formula, string Calculation, BigInteger Value);
internal sealed record MatrixCellSolution(string Cell, string Formula, IReadOnlyList<MatrixSolutionTerm> Terms,
    BigInteger LeadingSum, int RemainingCount, BigInteger RemainingSum, string Calculation, BigInteger Answer);

/// <summary>Bounded, exact worked steps for one selected result cell; independent of UI and language.</summary>
internal static class MatrixSolutionPresentation
{
    public static MatrixCellSolution ForCell(IntegerMatrix a, IntegerMatrix b, MatrixCalculation result, int row, int column)
    {
        BigInteger answer = result.GetValue(row, column);
        string cell = $"C[{row + 1}, {column + 1}]";
        if (result.Operation != MatrixOperation.Multiply)
        {
            string op = result.Operation == MatrixOperation.Add ? "+" : "−";
            return new(cell, $"{cell} = A[{row + 1}, {column + 1}] {op} B[{row + 1}, {column + 1}]", [],
                answer, 0, 0, $"{Number(a.Data[row * a.Columns + column])} {op} {Number(b.Data[row * b.Columns + column])} = {Number(answer, false)}", answer);
        }

        var terms = new List<MatrixSolutionTerm>(Math.Min(4, a.Columns));
        BigInteger leadingSum = 0;
        for (int k = 0; k < Math.Min(4, a.Columns); k++)
        {
            long left = a.Data[row * a.Columns + k], right = b.Data[k * b.Columns + column];
            BigInteger value = (BigInteger)left * right;
            leadingSum += value;
            terms.Add(new($"A[{row + 1}, {k + 1}] × B[{k + 1}, {column + 1}]",
                $"{Number(left)} × {Number(right)} = {Number(value, false)}", value));
        }
        int remainingCount = a.Columns - terms.Count;
        BigInteger remainingSum = answer - leadingSum;
        string addition = remainingCount > 0
            ? $"{Number(leadingSum)} + {Number(remainingSum)}"
            : string.Join(" + ", terms.Select(term => Number(term.Value)));
        return new(cell, "", terms, leadingSum, remainingCount, remainingSum,
            $"{addition} = {Number(answer, false)}", answer);
    }

    private static string Number(BigInteger value, bool wrapNegative = true)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        return wrapNegative && value.Sign < 0 ? $"({text})" : text;
    }
}
