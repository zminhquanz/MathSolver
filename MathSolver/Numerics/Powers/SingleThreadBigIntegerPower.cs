using System.Numerics;

namespace MathSolver.Numerics;

/// <summary>
/// Single-threaded BigInteger power with exponent windows and bounded square
/// batching. Progress and cancellation remain visible between batches.
/// </summary>
internal static class SingleThreadBigIntegerPower
{
    private const int MaximumRuntimeSquareBatchCount = 5;
    private const int RuntimeWindowOptimizationMinimumExponent = 1_000_000;
    private const int MaximumRuntimeExponentWindowBitCount = 5;

    // Preserve the existing runtime-only schedule, expressed directly in bits.
    // Batch after 512 former 16-bit limbs, or earlier when enough squares remain.
    private const int RuntimeBatchingBitThreshold = 8192;
    private const int MillionExponentBatchingBitThreshold = 4336;
    private const int GroupedSquaresBatchingBitThreshold = 7152;

    public static BigInteger Pow(
        long baseValue,
        int exponent,
        Action<int, int> progress,
        int totalOperations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        cancellationToken.ThrowIfCancellationRequested();
        if (exponent == 0) return BigInteger.One;

        BigInteger baseBigInteger = BigInteger.Abs(new BigInteger(baseValue));
        BigInteger resultBigInteger = baseBigInteger;
        bool runtimeBatchingEnabled = false;
        int completedOperations = 0;
        int highestSetBit = BitOperations.Log2((uint)exponent);

        for (int bitIndex = highestSetBit - 1; bitIndex >= 0;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!runtimeBatchingEnabled)
                runtimeBatchingEnabled = ShouldStartRuntimeBatching(
                    resultBigInteger.GetBitLength(), exponent, bitIndex);

            if (runtimeBatchingEnabled)
            {
                int terminalZeroSquareCount =
                    CountTerminalZeroSquares(
                        exponent,
                        bitIndex);

                // Keep the largest final square visible as its own progress and
                // cancellation point, but batch the cheaper prefix more
                // aggressively than the previous 3+2+1+1 schedule. For the
                // important tails this gives:
                //   500,000    -> 3 + 1 + 1 (unchanged; windowing is disabled)
                //   1,000,000  -> 3 + 2 + 1
                //   10,000,000 -> 4 + 2 + 1
                // The final square is necessarily one long operation anyway;
                // keeping it separate avoids the old "stuck before completion"
                // UX while eliminating one or more public BigInteger boundaries.
                if (terminalZeroSquareCount > 1)
                {
                    int terminalBatchCount =
                        terminalZeroSquareCount >= 7
                            ? 4
                            : terminalZeroSquareCount >= 5
                                ? 3
                                : terminalZeroSquareCount >= 3
                                    ? 2
                                    : 1;

                    if (terminalBatchCount >= 2)
                    {
                        resultBigInteger =
                            BigInteger.Pow(
                                resultBigInteger,
                                1 << terminalBatchCount);

                        completedOperations +=
                            terminalBatchCount;

                        progress(
                            completedOperations,
                            totalOperations);

                        bitIndex -=
                            terminalBatchCount;

                        continue;
                    }
                }

                // Million-scale runtime window. Unlike CountRuntimeSquareGroup,
                // this deliberately crosses set bits. k binary steps are:
                //   (((r^2 * a^b1)^2 * a^b2) ...)
                // = r^(2^k) * a^(windowValue).
                // base^windowValue is at most base^31, so no result-sized side
                // buffer is retained. This reduces calculator re-entry and
                // ArrayPool/public-BigInteger boundaries while keeping memory
                // essentially flat.
                if (terminalZeroSquareCount == 0 &&
                    exponent >= RuntimeWindowOptimizationMinimumExponent)
                {
                    int trailingZeroCount =
                        BitOperations.TrailingZeroCount(
                            (uint)exponent);

                    int nonTerminalBitCount =
                        bitIndex - trailingZeroCount + 1;

                    int windowBitCount =
                        Math.Min(
                            MaximumRuntimeExponentWindowBitCount,
                            nonTerminalBitCount);

                    if (windowBitCount >= 2)
                    {
                        int windowShift =
                            bitIndex - windowBitCount + 1;

                        int windowMask =
                            (1 << windowBitCount) - 1;

                        int windowValue =
                            (exponent >> windowShift) &
                            windowMask;

                        // Compute the tiny factor before the large Pow so it is
                        // never formed while another result-sized temporary is
                        // being published by this method.
                        BigInteger windowFactor =
                            windowValue == 0
                                ? BigInteger.One
                                : BigInteger.Pow(
                                    baseBigInteger,
                                    windowValue);

                        resultBigInteger =
                            BigInteger.Pow(
                                resultBigInteger,
                                1 << windowBitCount);

                        if (windowValue != 0)
                        {
                            resultBigInteger *=
                                windowFactor;
                        }

                        completedOperations +=
                            windowBitCount +
                            BitOperations.PopCount(
                                (uint)windowValue);

                        progress(
                            completedOperations,
                            totalOperations);

                        bitIndex -=
                            windowBitCount;

                        continue;
                    }
                }

                // Smaller exponents retain the proven run-based batching path.
                int groupedSquareCount =
                    CountRuntimeSquareGroup(
                        exponent,
                        bitIndex,
                        out bool multiplyAfterGroup);

                if (terminalZeroSquareCount == 0 &&
                    groupedSquareCount >= 2)
                {
                    resultBigInteger =
                        BigInteger.Pow(
                            resultBigInteger,
                            1 << groupedSquareCount);

                    completedOperations +=
                        groupedSquareCount;

                    progress(
                        completedOperations,
                        totalOperations);

                    bitIndex -=
                        groupedSquareCount;

                    if (multiplyAfterGroup)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        resultBigInteger *=
                            baseBigInteger;

                        progress(
                            ++completedOperations,
                            totalOperations);
                    }

                    continue;
                }
            }

            resultBigInteger *= resultBigInteger;
            progress(++completedOperations, totalOperations);

            if (((exponent >> bitIndex) & 1) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                resultBigInteger *= baseBigInteger;
                progress(++completedOperations, totalOperations);
            }
            bitIndex--;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return baseValue < 0 && (exponent & 1) != 0
            ? BigInteger.Negate(resultBigInteger)
            : resultBigInteger;
    }

    private static bool ShouldStartRuntimeBatching(long bits, int exponent, int bitIndex) =>
        bits > RuntimeBatchingBitThreshold ||
        (exponent >= RuntimeWindowOptimizationMinimumExponent &&
         bits > MillionExponentBatchingBitThreshold && bitIndex + 1 >= 5) ||
        (bits > GroupedSquaresBatchingBitThreshold &&
         CountRuntimeSquareGroup(exponent, bitIndex, out _) >= 4);

    private static int CountTerminalZeroSquares(
        int exponent,
        int bitIndex)
    {
        // Pow() handles exponent == 0 before entering the main loop, so the
        // trailing-zero count here always describes a non-zero exponent. If
        // bitIndex lies inside that trailing-zero suffix, every remaining bit
        // is zero and each one corresponds to one final square operation.
        int trailingZeroCount =
            BitOperations.TrailingZeroCount(
                (uint)exponent);

        return bitIndex < trailingZeroCount
            ? bitIndex + 1
            : 0;
    }

    private static int CountRuntimeSquareGroup(
        int exponent,
        int bitIndex,
        out bool multiplyAfterGroup)
    {
        int groupedSquareCount = 1;

        multiplyAfterGroup =
            ((exponent >> bitIndex) & 1) != 0;

        while (!multiplyAfterGroup &&
               groupedSquareCount < MaximumRuntimeSquareBatchCount &&
               bitIndex - groupedSquareCount >= 0)
        {
            int scannedBitIndex =
                bitIndex - groupedSquareCount;

            groupedSquareCount++;

            if (((exponent >> scannedBitIndex) & 1) != 0)
            {
                multiplyAfterGroup = true;
            }
        }

        return groupedSquareCount;
    }

}
