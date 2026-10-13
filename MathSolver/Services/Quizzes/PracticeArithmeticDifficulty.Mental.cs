using MathSolver.Models;

namespace MathSolver.Services;

public static partial class PracticeArithmeticDifficulty
{
    /// <summary>Operate on small coefficients and a common decimal scale, including round hundred millions at four stars.</summary>
    public static IntegerArithmeticExpression CreateMentalExpression(Random random, CurriculumTier tier,
        ArithmeticOperation operation, IReadOnlyList<int>? allowedFactors = null, int? maximum = null)
    {
        if (tier is not (CurriculumTier.FourStars or CurriculumTier.FiveStars))
            throw new ArgumentOutOfRangeException(nameof(tier));
        int cap = Math.Min(Maximum(tier, PracticeArithmeticStyle.Mental), maximum ?? int.MaxValue);
        if (cap < 2) throw new ArgumentOutOfRangeException(nameof(maximum));
        int[] scales = [1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000];
        var eligibleScales = scales.Where(s => s <= cap / 2).ToArray();
        int scale = eligibleScales[random.Next(eligibleScales.Length)];
        int coefficientCap = Math.Min(99, cap / scale);
        int a, b;
        if (operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract)
        {
            a = random.Next(1, coefficientCap);
            b = random.Next(1, operation == ArithmeticOperation.Add ? coefficientCap - a + 1 : a + 1);
            // Apply the same grade-four carry/borrow rule to the small coefficients.
            for (int attempt = 0; attempt < 24 && !HasAppropriateRegrouping(a, b, operation, tier); attempt++)
                b = random.Next(1, operation == ArithmeticOperation.Add ? coefficientCap - a + 1 : a + 1);
            if (!HasAppropriateRegrouping(a, b, operation, tier)) { a = Math.Max(1, a / 10 * 10); b = 1; }
            return new(a * scale, operation, b * scale);
        }
        if (operation is not (ArithmeticOperation.Multiply or ArithmeticOperation.Divide))
            throw new ArgumentOutOfRangeException(nameof(operation));
        int[] usualFactors = [2, 3, 4, 5, 6, 7, 8, 9, 10, 100, 1_000];
        var factors = (allowedFactors ?? usualFactors).Where(f => f >= 1 && f <= cap / scale).ToArray();
        // A large scale may leave no allowed factor; reduce it instead of changing the curriculum's factors.
        if (factors.Length == 0)
        {
            scale = 1;
            factors = (allowedFactors ?? usualFactors).Where(f => f >= 1 && f <= cap).ToArray();
        }
        if (factors.Length == 0) throw new InvalidOperationException("No mental arithmetic factor fits the context.");
        b = factors[random.Next(factors.Length)];
        a = random.Next(1, Math.Min(99, cap / scale / b) + 1) * scale;
        return operation == ArithmeticOperation.Multiply ? new(a, operation, b) : new(a * b, operation, b);
    }
}
