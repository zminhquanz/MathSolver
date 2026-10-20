using System.Numerics;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckNumberTheory()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        for (int a = 0; a <= 80; a++)
        for (int b = 0; b <= 80; b++)
        {
            if (a == 0 && b == 0) continue;
            var result = NumberTheoryEngine.Calculate(a, b);
            int expectedGcd = Enumerable.Range(1, Math.Max(a, b)).Last(d => a % d == 0 && b % d == 0);
            int expectedLcm = a == 0 || b == 0 ? 0
                : Enumerable.Range(1, a * b).First(n => n % a == 0 && n % b == 0);
            Require(result.Gcd == expectedGcd && result.Lcm == expectedLcm, $"Wrong result for {a}, {b}");
            Require(result.First == a && result.Second == b, "Input values changed");
            BigInteger dividend = Math.Max(a, b), divisor = Math.Min(a, b);
            foreach (var step in result.Divisions)
            {
                Require(step.Dividend == dividend && step.Divisor == divisor, "Broken Euclidean chain");
                Require(step.Dividend == step.Divisor * step.Quotient + step.Remainder
                    && step.Remainder >= 0 && step.Remainder < step.Divisor, "Invalid worked division");
                dividend = divisor;
                divisor = step.Remainder;
            }
            Require(divisor == 0 && dividend == result.Gcd, "Wrong stopping condition");
        }

        BigInteger huge = BigInteger.Pow(10, 127);
        var large = NumberTheoryEngine.Calculate(huge, huge + 1);
        Require(large.Gcd == 1 && large.Lcm == huge * (huge + 1), "Large exact product lost");
        foreach (string? invalid in new string?[] { null, "", " ", "-1", "+1", "1.5", "1,000", "1 2", "1e3", "١", new('9', 129) })
            Require(!NumberTheoryEngine.TryParseInput(invalid, out _), "Accepted invalid input: " + invalid);
        Require(NumberTheoryEngine.TryParseInput(" 00048 ", out var parsed) && parsed == 48, "Leading zero/trim parsing");
        Require(NumberTheoryEngine.TryParseInput(new string('9', 128), out _), "128-digit input rejected");

        foreach (var (a, b) in new[] { (-1, 2), (2, -1), (0, 0) })
        {
            bool rejected = false;
            try { NumberTheoryEngine.Calculate(a, b); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, $"Invalid pair {a}, {b} accepted");
        }
        Console.WriteLine("Checked 6,560 pairs against independent divisor/multiple searches, Euclidean steps, zero cases and 128-digit inputs.");
    }

    public static void CheckPrimeFactorLessons()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        static BigInteger Product(IEnumerable<PrimePower> factors) =>
            factors.Aggregate(BigInteger.One, (product, f) => product * BigInteger.Pow(f.Prime, f.Exponent));
        static bool IsPrime(int n) => n >= 2 && !Enumerable.Range(2, Math.Max(0, (int)Math.Sqrt(n) - 1)).Any(d => n % d == 0);

        for (int a = 1; a <= 80; a++)
        for (int b = 1; b <= 80; b++)
        {
            var lesson = NumberTheoryEngine.ExplainByPrimeFactors(a, b);
            Require(lesson.IsComplete, "Small factorization incomplete");
            Require(Product(lesson.First.Factors) == a && Product(lesson.Second.Factors) == b, "Factor product changed input");
            foreach (var factorization in new[] { lesson.First, lesson.Second })
            {
                Require(factorization.Factors.Select(f => f.Prime).SequenceEqual(factorization.Factors.Select(f => f.Prime).Distinct().Order()), "Unordered/duplicate primes");
                Require(factorization.Factors.All(f => f.Exponent > 0 && IsPrime((int)f.Prime)), "Non-prime factor or invalid exponent");
            }
            // Independent divisor search also tests the empty product for input 1 / coprime pairs.
            int gcd = Enumerable.Range(1, Math.Min(a, b)).Last(d => a % d == 0 && b % d == 0);
            Require(Product(lesson.CommonFactors) == gcd, "Wrong minimum exponents");
            Require(Product(lesson.AllFactors) == (BigInteger)a * b / gcd, "Wrong maximum exponents");
        }

        var example = NumberTheoryEngine.ExplainByPrimeFactors(48, 18);
        Require(example.First.Factors.SequenceEqual(new[] { new PrimePower(2, 4), new PrimePower(3, 1) })
            && example.Second.Factors.SequenceEqual(new[] { new PrimePower(2, 1), new PrimePower(3, 2) }), "Worked example powers incorrect");
        var smooth = NumberTheoryEngine.Factorize(BigInteger.Pow(2, 400));
        Require(smooth.IsComplete && smooth.Factors.Single() == new PrimePower(2, 400), "Large smooth input should complete");
        var prime = NumberTheoryEngine.Factorize(1_000_000_007);
        Require(prime.IsComplete && prime.Factors.Single() == new PrimePower(1_000_000_007, 1), "Proven residual prime not recognized");
        var boundary = NumberTheoryEngine.Factorize((BigInteger)99_991 * 100_003);
        Require(boundary.IsComplete && boundary.Factors.SequenceEqual(new[] { new PrimePower(99_991, 1), new PrimePower(100_003, 1) }), "Residual after final trial prime not recognized");
        BigInteger hardCofactor = (BigInteger)1_000_000_007 * 1_000_000_009;
        var partial = NumberTheoryEngine.ExplainByPrimeFactors(12 * hardCofactor, 18);
        Require(!partial.IsComplete && !partial.First.IsComplete && partial.First.UnfactoredPart == hardCofactor
            && Product(partial.First.Factors) * partial.First.UnfactoredPart == partial.First.Value
            && partial.CommonFactors.Count == 0 && partial.AllFactors.Count == 0, "Unfinished cofactor passed as prime / complete lesson");
        foreach (int invalid in new[] { 0, -1 })
        {
            bool rejected = false;
            try { NumberTheoryEngine.Factorize(invalid); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "Non-positive input factorized");
        }
        Console.WriteLine("Checked 6,400 grade-six factor lessons, minimum/maximum powers, 1, coprime inputs, large powers and bounded incomplete factorization.");
    }
}
