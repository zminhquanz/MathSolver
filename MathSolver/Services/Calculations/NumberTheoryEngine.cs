using System.Globalization;
using System.Numerics;

namespace MathSolver.Services.Core;

public sealed record EuclideanDivisionStep(
    BigInteger Dividend, BigInteger Divisor, BigInteger Quotient, BigInteger Remainder);

public sealed record NumberTheoryCalculation(
    BigInteger First, BigInteger Second, BigInteger Gcd, BigInteger Lcm,
    IReadOnlyList<EuclideanDivisionStep> Divisions);

/// <summary>Exact GCD/LCM and the Euclidean divisions used to explain the result.</summary>
public static partial class NumberTheoryEngine
{
    public const int MaximumInputDigits = 128;

    public static bool TryParseInput(string? text, out BigInteger value)
    {
        value = BigInteger.Zero;
        var token = text.AsSpan().Trim();
        if (token.Length is 0 or > MaximumInputDigits) return false;
        foreach (char c in token)
            if (c is < '0' or > '9') return false;
        return BigInteger.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public static NumberTheoryCalculation Calculate(BigInteger first, BigInteger second)
    {
        if (first.Sign < 0) throw new ArgumentOutOfRangeException(nameof(first));
        if (second.Sign < 0) throw new ArgumentOutOfRangeException(nameof(second));
        if (first.IsZero && second.IsZero)
            throw new ArgumentException("At least one input must be positive.");

        BigInteger a = BigInteger.Max(first, second), b = BigInteger.Min(first, second);
        var divisions = new List<EuclideanDivisionStep>();
        while (!b.IsZero)
        {
            BigInteger quotient = BigInteger.DivRem(a, b, out BigInteger remainder);
            divisions.Add(new(a, b, quotient, remainder));
            a = b;
            b = remainder;
        }

        // Divide before multiplying; BigInteger also preserves products beyond Int128.
        return new(first, second, a, first / a * second, divisions.AsReadOnly());
    }
}
