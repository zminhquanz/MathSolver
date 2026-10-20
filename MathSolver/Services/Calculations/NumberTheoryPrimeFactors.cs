using System.Numerics;

namespace MathSolver.Services.Core;

public sealed record PrimePower(BigInteger Prime, int Exponent);

/// <summary>Only Factors are proven prime; an unfinished cofactor is never labelled prime.</summary>
public sealed record PrimeFactorization(
    BigInteger Value, IReadOnlyList<PrimePower> Factors, BigInteger UnfactoredPart)
{
    public bool IsComplete => UnfactoredPart.IsOne;
}

public sealed record PrimeFactorLesson(
    PrimeFactorization First, PrimeFactorization Second,
    IReadOnlyList<PrimePower> CommonFactors, IReadOnlyList<PrimePower> AllFactors)
{
    public bool IsComplete => First.IsComplete && Second.IsComplete;
}

public static partial class NumberTheoryEngine
{
    // A bounded, exact teaching algorithm. Inputs with large, difficult factors
    // retain an explicitly unfinished cofactor instead of blocking on factorization.
    public const int MaximumTrialPrime = 100_000;
    private static readonly Lazy<int[]> TrialPrimes = new(() =>
    {
        var composite = new bool[MaximumTrialPrime + 1];
        var primes = new List<int>();
        for (int candidate = 2; candidate <= MaximumTrialPrime; candidate++)
        {
            if (composite[candidate]) continue;
            primes.Add(candidate);
            if (candidate > MaximumTrialPrime / candidate) continue;
            for (int multiple = candidate * candidate; multiple <= MaximumTrialPrime; multiple += candidate)
                composite[multiple] = true;
        }
        return primes.ToArray();
    });

    public static PrimeFactorization Factorize(BigInteger value)
    {
        if (value.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        var remaining = value;
        var factors = new List<PrimePower>();
        foreach (int prime in TrialPrimes.Value)
        {
            if (remaining.IsOne) break;
            if ((BigInteger)prime * prime > remaining)
            {
                factors.Add(new(remaining, 1));
                remaining = BigInteger.One;
                break;
            }
            int exponent = 0;
            while (remaining % prime == 0)
            {
                remaining /= prime;
                exponent++;
            }
            if (exponent > 0) factors.Add(new(prime, exponent));
        }
        // All prime divisors up to the bound have been eliminated. A smaller
        // residual cannot be composite; this also covers a residual after the last trial.
        if (remaining > 1 && remaining <= (BigInteger)MaximumTrialPrime * MaximumTrialPrime)
        {
            factors.Add(new(remaining, 1));
            remaining = BigInteger.One;
        }
        return new(value, factors.AsReadOnly(), remaining);
    }

    public static PrimeFactorLesson ExplainByPrimeFactors(BigInteger first, BigInteger second)
    {
        var a = Factorize(first);
        var b = first == second ? a : Factorize(second);
        if (!a.IsComplete || !b.IsComplete) return new(a, b, [], []);
        var aExponents = a.Factors.ToDictionary(f => f.Prime, f => f.Exponent);
        var bExponents = b.Factors.ToDictionary(f => f.Prime, f => f.Exponent);
        var common = new List<PrimePower>();
        var all = new List<PrimePower>();
        foreach (var prime in aExponents.Keys.Union(bExponents.Keys).Order())
        {
            int left = aExponents.GetValueOrDefault(prime), right = bExponents.GetValueOrDefault(prime);
            if (left > 0 && right > 0) common.Add(new(prime, Math.Min(left, right)));
            all.Add(new(prime, Math.Max(left, right)));
        }
        return new(a, b, common.AsReadOnly(), all.AsReadOnly());
    }
}
