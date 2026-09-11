using System.Numerics;

namespace MathSolver.Numerics;

internal sealed record PowerOfTenResult(
    BigInteger Value, int WorkerCount, ParallelPowerDiagnostics? Diagnostics,
    long TwiddleCapacityBytes = 0);

/// <summary>Materializes (10^k)^n as 5^(kn) shifted left by kn bits.</summary>
internal static class PowerOfTenArithmetic
{
    // Matched 1M measurements favor eight workers for these short transforms.
    // Below 500K decimal zeros, thread/plan setup can outweigh the arithmetic.
    internal static int SelectWorkerCount(int decimalDigitCount, int requestedWorkers) =>
        decimalDigitCount <= 500_000 || requestedWorkers <= 1 ? 1 :
        Math.Clamp(requestedWorkers, 1, Math.Min(Environment.ProcessorCount,
            decimalDigitCount <= 3_000_001 ? 8 : Environment.ProcessorCount));

    internal static int GetBinaryExponent(long baseValue, int exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        ulong magnitude = baseValue < 0 ? (ulong)(-(baseValue + 1)) + 1 : (ulong)baseValue;
        int k = 0;
        while (magnitude >= 10 && magnitude % 10 == 0) { magnitude /= 10; k++; }
        if (magnitude != 1 || k == 0)
            throw new ArgumentException("The base must have magnitude 10^k, k >= 1.", nameof(baseValue));
        long m = (long)k * exponent;
        // Conservative integer bound above log2(10), including packing padding.
        // Reject before computing 5^m if the final BigInteger cannot be represented.
        if (m * 332193 / 100000 + 17 > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(exponent),
                "The materialized power exceeds the BigInteger bit-length limit.");
        return checked((int)m);
    }

    internal static int OperationCount(int m) => m == 0 ? 0 :
        BitOperations.Log2((uint)m) + BitOperations.PopCount((uint)m); // includes final shift

    internal static PowerOfTenResult Pow(long baseValue, int exponent, int workerCount,
        Action<int, int>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        int m = GetBinaryExponent(baseValue, exponent);
        if (m == 0) return new(BigInteger.One, 1, null);
        PowerOfTenResult result;
        if (workerCount > 1)
            result = ParallelBigUnsigned.PowFiveAndShift(m, workerCount, progress, token);
        else
        {
            int total = OperationCount(m);
            BigInteger power = SingleThreadBigIntegerPower.Pow(5, m,
                (done, _) => progress?.Invoke(done, total), total - 1, token, useSimd: false);
            token.ThrowIfCancellationRequested();
            BigInteger shifted = power << m;
            token.ThrowIfCancellationRequested();
            progress?.Invoke(total, total);
            token.ThrowIfCancellationRequested();
            result = new(shifted, 1, null);
        }
        return baseValue < 0 && (exponent & 1) != 0
            ? result with { Value = BigInteger.Negate(result.Value) } : result;
    }
}
