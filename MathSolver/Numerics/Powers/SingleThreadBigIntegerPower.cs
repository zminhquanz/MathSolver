using System.Numerics;
using static MathSolver.Numerics.Avx2BigIntegerPower;

namespace MathSolver.Numerics;

/// <summary>
/// Single-threaded power scheduling shared by the runtime and bounded SIMD backends.
/// Hardware acceleration changes only the small arithmetic window; the runtime
/// square batching, exponent windows, progress and cancellation policy are shared.
/// </summary>
internal static class SingleThreadBigIntegerPower
{
    // Once the custom UInt16 window hands off to System.Numerics.BigInteger,
    // consecutive left-to-right square steps can be folded into one runtime
    // Pow(value, 2^k) call. .NET 10's BigInteger.Pow allocates one bounded
    // result workspace and performs the internal square chain there, avoiding
    // repeated public BigInteger construction / ArrayPool rent-return cycles
    // between adjacent squares. Keep the batch deliberately small so
    // cancellation latency remains close to the old per-square schedule.
    private const int MaximumRuntimeSquareBatchCount = 5;

    // Runtime windowing is intentionally enabled only for million-scale
    // exponents. Smaller powers keep the proven RuntimeSquareBatch5 path.
    // Once the result is large, process up to five exponent bits at a time:
    //     r <- r^(2^k) * base^window
    // This is algebraically identical to k left-to-right binary steps, but it
    // lets BigInteger.Pow keep all k squares inside one calculator workspace
    // even when the bit window contains 1 bits. The window factor is tiny
    // (base^31 at most for k=5) compared with the multi-megabyte result.
    private const int RuntimeWindowOptimizationMinimumExponent = 1_000_000;
    private const int MaximumRuntimeExponentWindowBitCount = 5;

    // With window batching available, hand off before the custom 285 -> ~569
    // limb square seen by the 10,000,000 path. 272 is deliberately below that
    // observed point but still far above the small-input AVX2 region. Requiring
    // at least five remaining bits ensures the earlier conversion is paid back
    // by an immediate full runtime window. 500,000 keeps the old 448x4 policy.
    private const int PredictiveRuntimeWindowHandoffMinimumLimbCount = 272;
    private const int PredictiveRuntimeWindowHandoffMinimumRemainingBitCount = 5;
    private const int EarlyRuntimeBatchHandoffMinimumLimbCount = 448;
    private const int EarlyRuntimeBatchHandoffMinimumSquareCount = 4;

    public static BigInteger Pow(
        long baseValue,
        int exponent,
        Action<int, int> progress,
        int totalOperations,
        CancellationToken cancellationToken,
        bool useSimd)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        cancellationToken.ThrowIfCancellationRequested();

        if (exponent == 0)
        {
            return BigInteger.One;
        }

        ulong magnitude =
            baseValue < 0
                ? (ulong)(-(baseValue + 1L)) + 1UL
                : (ulong)baseValue;

        bool useCustomWindow = useSimd && IsSupported;
        ushort[] baseMagnitude = useCustomWindow ? FromUInt64(magnitude) : [];

        // Single-threaded backend: keep one small accumulator workspace for
        // the entire custom-SIMD window instead of renting/returning a pooled
        // UInt64 buffer for every square/multiply. NormalizeAccumulator clears
        // each consumed coefficient as it propagates carry, so the workspace
        // is already zeroed for the next operation without a separate Clear().
        ulong[] accumulatorWorkspace =
            useCustomWindow ? new ulong[MaximumAccumulatorLimbCount] : [];

        // Left-to-right binary exponentiation keeps the multiply operand equal
        // to the original base. That is a much better fit for the asymmetric
        // AVX2 kernel than the old right-to-left schedule, which accumulated
        // separately squared factors and later multiplied two large magnitudes.
        //
        // The operation count is unchanged: initialize from the leading 1 bit,
        // then perform one square for every remaining bit and one multiply for
        // every remaining set bit. Therefore existing progress accounting stays
        // exactly compatible with the previous square/multiply implementation.
        ushort[] resultMagnitude =
            baseMagnitude;

        bool runtimeBigIntegerMode = !useCustomWindow;
        bool runtimeBatchingEnabled = false;

        BigInteger baseBigInteger = new(magnitude);

        BigInteger resultBigInteger = baseBigInteger;

        int completedOperations = 0;

        void SwitchToRuntimeBigInteger()
        {
            if (runtimeBigIntegerMode)
            {
                return;
            }

            resultBigInteger =
                ToBigInteger(resultMagnitude);

            runtimeBigIntegerMode = true;
        }

        int highestSetBit =
            BitOperations.Log2(
                (uint)exponent);

        for (int bitIndex = highestSetBit - 1;
             bitIndex >= 0;)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Keep the binary prefix and batching boundary independent of SIMD.
            // The runtime backend already owns a BigInteger here; only a custom
            // kernel needs a representation conversion at this boundary.
            if (!runtimeBatchingEnabled)
            {
                int length = runtimeBigIntegerMode
                    ? Math.Max(1, (int)((resultBigInteger.GetBitLength() + 15) / 16))
                    : resultMagnitude.Length;
                if (ShouldStartRuntimeBatching(length, exponent, bitIndex))
                {
                    SwitchToRuntimeBigInteger();
                    runtimeBatchingEnabled = true;
                }
            }

            // After the custom AVX2 window has handed off to BigInteger, group
            // a short run of consecutive square steps. For a left-to-right
            // exponent bit run, k consecutive squares are exactly
            // result <- result^(2^k). BigInteger.Pow in .NET 10 computes that
            // chain inside one calculator workspace, while repeated public
            // result *= result creates/disposes an intermediate BigInteger and
            // rented buffer at every square boundary.
            //
            // Include the first set bit at the end of the run when it fits in
            // the batch. Its required multiply-by-base is still performed once
            // after the grouped squares, preserving the exact binary schedule.
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

            // Every remaining exponent bit first squares the accumulated
            // result. Keep the proven Square1024 crossover; if that window is
            // exceeded, switch once to System.Numerics.BigInteger and continue
            // with the same left-to-right schedule.
            if (!runtimeBigIntegerMode &&
                CanSquareInCustomWindow(
                    resultMagnitude.Length))
            {
                resultMagnitude =
                    SquareMagnitude(
                        resultMagnitude,
                        accumulatorWorkspace,
                        cancellationToken);
            }
            else
            {
                SwitchToRuntimeBigInteger();

                resultBigInteger *=
                    resultBigInteger;
            }

            progress(
                ++completedOperations,
                totalOperations);

            if (((exponent >> bitIndex) & 1) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // In left-to-right form the second operand is always the
                // original base (at most four base-2^16 limbs for Int64 input).
                if (!runtimeBigIntegerMode &&
                    CanMultiplyInCustomWindow(
                        resultMagnitude.Length,
                        baseMagnitude.Length))
                {
                    resultMagnitude =
                        MultiplyMagnitude(
                            resultMagnitude,
                            baseMagnitude,
                            accumulatorWorkspace,
                            cancellationToken);
                }
                else
                {
                    SwitchToRuntimeBigInteger();

                    resultBigInteger *=
                        baseBigInteger;
                }

                progress(
                    ++completedOperations,
                    totalOperations);
            }

            bitIndex--;
        }

        cancellationToken.ThrowIfCancellationRequested();

        BigInteger result =
            runtimeBigIntegerMode
                ? resultBigInteger
                : ToBigInteger(resultMagnitude);

        if (baseValue < 0 &&
            (exponent & 1) != 0)
        {
            result =
                BigInteger.Negate(result);
        }

        return result;
    }


    private static bool ShouldStartRuntimeBatching(int length, int exponent, int bitIndex) =>
        !CanSquareInCustomWindow(length) ||
        (exponent >= RuntimeWindowOptimizationMinimumExponent &&
         length >= PredictiveRuntimeWindowHandoffMinimumLimbCount &&
         bitIndex + 1 >= PredictiveRuntimeWindowHandoffMinimumRemainingBitCount) ||
        (length >= EarlyRuntimeBatchHandoffMinimumLimbCount &&
         CountRuntimeSquareGroup(exponent, bitIndex, out _) >= EarlyRuntimeBatchHandoffMinimumSquareCount);

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
