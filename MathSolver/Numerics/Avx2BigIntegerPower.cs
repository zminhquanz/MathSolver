using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

/// <summary>
/// Experimental single-threaded BigInteger power backend for x86/x64.
///
/// The hot window uses unsigned base-2^16 limbs. AVX-512-capable CPUs batch
/// thirty-two UInt16 products at a time by widening to UInt32 and using
/// VPMULLD; AVX2 remains the exact fallback.
/// Multiplication accumulates carry-independent convolution coefficients in
/// UInt64 slots without putting the carry chain
/// inside the SIMD loop. Squaring uses symmetry (i,j == j,i), so only the upper
/// triangle is multiplied and off-diagonal products are doubled.
///
/// This is deliberately bounded. Left-to-right binary exponentiation keeps
/// each multiply asymmetric (current result x original base). Once the result
/// grows beyond the tuned limb window, execution converts the current magnitude
/// to System.Numerics BigInteger exactly once and finishes with the runtime's
/// mature large-number multiplier. This prevents an O(n^2) schoolbook backend
/// from competing with Karatsuba/other runtime algorithms at large sizes.
///
/// No pointers, Unsafe.Add, native code, Barrett, or Montgomery arithmetic are
/// used. The shared Hardware acceleration switch gates entry to this backend.
/// </summary>
internal static class Avx2BigIntegerPower
{
    private const int VectorUShortCount = 16;
    private const int Vector512UShortCount = 32;

    // Variant 06 widens each 16-lane UInt16 half directly to UInt32 and uses
    // AVX-512F VPMULLD. A UInt16 * UInt16 product always fits in UInt32, so
    // the low 32-bit product is exact. This removes the old low/high UInt16
    // reconstruction, lane-local unpack, and VPERMI2D reorder sequence.
    // Multiplication and squaring deliberately use different crossover
    // windows. Balanced/general multiplication keeps the conservative
    // 256-limb result cap. Squaring keeps the proven 1024-limb result cap.
    //
    // The exponentiation chain also produces occasional highly asymmetric
    // products (small accumulated result x much larger power-of-two factor).
    // Those are still O(short * long), and the existing multiply kernel already
    // keeps the shorter operand outside while vectorizing across the long one.
    // Allow one carefully bounded asymmetric window so AVX2 can handle that
    // shape without opening the door to large balanced schoolbook products.
    private const int MaximumMultiplyResultLimbCount = 256;
    private const int MaximumAsymmetricMultiplyShortLimbCount = 128;
    private const int MaximumAsymmetricMultiplyResultLimbCount = 1152;
    private const int MaximumSquareResultLimbCount = 1024;

    // Variant 02: keep the proven AVX2 square cap at 1024 result limbs, but
    // allow the AVX-512BW path to own one additional medium square stage.
    // The shared accumulator workspace is already 1152 limbs because of the
    // bounded asymmetric-multiply window, so this does not enlarge workspace.
    private const int MaximumAvx512SquareResultLimbCount = 1152;

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

    // Keep AVX2 on the proven predictive handoff at 272 limbs. Variant 02
    // retunes only the AVX-512 crossover to 576 limbs. On the 10,000,000
    // exponent prefix this lets the ZMM path keep the ~285 -> ~569 square and,
    // when the next state remains below 576 limbs, one further square up to
    // roughly the 1152-limb result cap before handing off to BigInteger.
    // AVX2 remains byte-for-byte equivalent in schedule to Predictive272.
    private const int PredictiveRuntimeWindowHandoffMinimumLimbCount = 272;
    private const int PredictiveAvx512RuntimeWindowHandoffMinimumLimbCount = 576;
    private const int PredictiveRuntimeWindowHandoffMinimumRemainingBitCount = 5;
    private const int EarlyRuntimeBatchHandoffMinimumLimbCount = 448;
    private const int EarlyRuntimeBatchHandoffMinimumSquareCount = 4;

    private const int MaximumAccumulatorLimbCount =
        MaximumAsymmetricMultiplyResultLimbCount > MaximumSquareResultLimbCount
            ? MaximumAsymmetricMultiplyResultLimbCount
            : MaximumSquareResultLimbCount;

    public static bool IsSupported =>
        Avx2.IsSupported &&
        Vector256.IsHardwareAccelerated;

    public static bool IsAvx512CustomWindowActive =>
        Avx512BW.IsSupported &&
        Avx512F.IsSupported &&
        Vector512.IsHardwareAccelerated;

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

        if (exponent == 0)
        {
            return BigInteger.One;
        }

        ulong magnitude =
            baseValue < 0
                ? (ulong)(-(baseValue + 1L)) + 1UL
                : (ulong)baseValue;

        ushort[] baseMagnitude =
            FromUInt64(magnitude);

        // Single-threaded backend: keep one small accumulator workspace for
        // the entire custom-SIMD window instead of renting/returning a pooled
        // UInt64 buffer for every square/multiply. NormalizeAccumulator clears
        // each consumed coefficient as it propagates carry, so the workspace
        // is already zeroed for the next operation without a separate Clear().
        ulong[] accumulatorWorkspace =
            new ulong[MaximumAccumulatorLimbCount];

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

        bool runtimeBigIntegerMode = false;

        BigInteger baseBigInteger =
            BigInteger.Zero;

        BigInteger resultBigInteger =
            BigInteger.One;

        int completedOperations = 0;

        void SwitchToRuntimeBigInteger()
        {
            if (runtimeBigIntegerMode)
            {
                return;
            }

            baseBigInteger =
                ToBigInteger(baseMagnitude);

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

            // Normally the custom AVX2 square window is used all the way to
            // its proven crossover. There is one conservative exception: when
            // the current result is already close to that crossover and the
            // exponent has a sufficiently long square run, hand off one step
            // early so that BigInteger.Pow can own the whole run in one internal
            // workspace. For the 500,000 benchmark this turns the final custom
            // square + runtime Pow(..., 8) boundary into one runtime Pow(..., 16)
            // call while converting a roughly half-sized magnitude.
            if (!runtimeBigIntegerMode)
            {
                if (!CanSquareInCustomWindow(
                        resultMagnitude.Length))
                {
                    SwitchToRuntimeBigInteger();
                }
                else if (exponent >= RuntimeWindowOptimizationMinimumExponent &&
                         resultMagnitude.Length >=
                             (IsAvx512CustomWindowActive
                                 ? PredictiveAvx512RuntimeWindowHandoffMinimumLimbCount
                                 : PredictiveRuntimeWindowHandoffMinimumLimbCount) &&
                         bitIndex + 1 >=
                             PredictiveRuntimeWindowHandoffMinimumRemainingBitCount)
                {
                    // Million-scale path: switch before the next custom square
                    // so the upcoming exponent window can be evaluated inside
                    // one BigInteger.Pow workspace. For 10,000,000 this catches
                    // the ~285-limb state instead of first converting at ~569.
                    SwitchToRuntimeBigInteger();
                }
                else if (resultMagnitude.Length >=
                             EarlyRuntimeBatchHandoffMinimumLimbCount &&
                         CountRuntimeSquareGroup(
                             exponent,
                             bitIndex,
                             out _) >=
                             EarlyRuntimeBatchHandoffMinimumSquareCount)
                {
                    SwitchToRuntimeBigInteger();
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
            if (runtimeBigIntegerMode)
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

    private static bool CanMultiplyInCustomWindow(
        int leftLength,
        int rightLength)
    {
        int resultLength =
            checked(leftLength + rightLength);

        if (resultLength <=
            MaximumMultiplyResultLimbCount)
        {
            return true;
        }

        int shorterLength =
            Math.Min(
                leftLength,
                rightLength);

        return
            shorterLength <=
                MaximumAsymmetricMultiplyShortLimbCount &&
            resultLength <=
                MaximumAsymmetricMultiplyResultLimbCount;
    }

    private static bool CanSquareInCustomWindow(
        int length)
    {
        int maximumResultLimbCount =
            IsAvx512CustomWindowActive
                ? MaximumAvx512SquareResultLimbCount
                : MaximumSquareResultLimbCount;

        return checked(length * 2) <=
            maximumResultLimbCount;
    }

    private static ushort[] MultiplyMagnitude(
        ushort[] left,
        ushort[] right,
        ulong[] accumulatorWorkspace,
        CancellationToken cancellationToken)
    {
        if (IsZero(left) ||
            IsZero(right))
        {
            return ZeroMagnitude;
        }

        // Keep the shorter operand outside. It reduces scalar broadcasts and
        // cancellation checks without changing the number of products.
        if (left.Length > right.Length)
        {
            (left, right) =
                (right, left);
        }

        int coefficientCount =
            checked(
                left.Length +
                right.Length);

        Span<ulong> accumulator =
            accumulatorWorkspace.AsSpan(
                0,
                coefficientCount);

        ReadOnlySpan<ushort> rightSpan =
            right;

        // AVX-512 is an internal fast path only; AVX2 remains the portable
        // x86 fallback and the externally visible feature gate. On Zen 5 the
        // ZMM path is full-width, so consume 32 UInt16 products per batch.
        bool useAvx512 =
            IsAvx512CustomWindowActive;

        int vector512Count =
            useAvx512
                ? right.Length / Vector512UShortCount
                : 0;

        int vector512Length =
            vector512Count * Vector512UShortCount;

        // Feed AVX-512 through two contiguous 256-bit UInt16 halves per
        // 32-product block. VPMOVZXWD can widen each half directly to one
        // Vector512<UInt32>, avoiding the previous ZMM UInt16 unpack/permute
        // reconstruction path.
        ReadOnlySpan<Vector256<ushort>> rightVectorsForAvx512 =
            vector512Count != 0
                ? MemoryMarshal.Cast<ushort, Vector256<ushort>>(
                    rightSpan.Slice(0, vector512Length))
                : ReadOnlySpan<Vector256<ushort>>.Empty;

        // Keep the proven AVX2 path for the remainder and for CPUs without
        // AVX-512BW. The reinterpretations are hoisted out of the outer row.
        int vector256Count =
            (right.Length - vector512Length) /
            VectorUShortCount;

        int vector256Length =
            vector256Count *
            VectorUShortCount;

        ReadOnlySpan<Vector256<ushort>> rightVectors256 =
            vector256Count != 0
                ? MemoryMarshal.Cast<ushort, Vector256<ushort>>(
                    rightSpan.Slice(
                        vector512Length,
                        vector256Length))
                : ReadOnlySpan<Vector256<ushort>>.Empty;

        int tailStart =
            vector512Length +
            vector256Length;

        bool hasVector128Tail =
            right.Length - tailStart >= 8;

        Vector128<ushort> tailValues128 =
            hasVector128Tail
                ? MemoryMarshal.Cast<ushort, Vector128<ushort>>(
                    rightSpan.Slice(
                        tailStart,
                        8))[0]
                : Vector128<ushort>.Zero;

        int scalarTailStart =
            tailStart +
            (hasVector128Tail ? 8 : 0);

        for (int i = 0;
             i < left.Length;
             i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ushort scalar =
                left[i];

            if (scalar == 0)
            {
                continue;
            }

            if (vector512Count != 0)
            {
                Vector512<uint> scalarVector512 =
                    Vector512.Create((uint)scalar);

                for (int vectorIndex = 0;
                     vectorIndex < vector512Count;
                     vectorIndex++)
                {
                    int sourceVectorIndex =
                        vectorIndex * 2;

                    AccumulateThirtyTwoProductsAvx512UInt32(
                        accumulator,
                        i +
                        vectorIndex * Vector512UShortCount,
                        rightVectorsForAvx512[sourceVectorIndex],
                        rightVectorsForAvx512[sourceVectorIndex + 1],
                        scalarVector512,
                        doubleProducts: false);
                }
            }

            Vector256<ushort> scalarVector256 =
                Vector256.Create(scalar);

            for (int vectorIndex = 0;
                 vectorIndex < vector256Count;
                 vectorIndex++)
            {
                AccumulateSixteenProductsAvx2(
                    accumulator,
                    i +
                    vector512Length +
                    vectorIndex * VectorUShortCount,
                    rightVectors256[vectorIndex],
                    scalarVector256,
                    doubleProducts: false);
            }

            int j =
                tailStart;

            if (hasVector128Tail)
            {
                Vector128<ushort> scalarVector128 =
                    Vector128.Create(scalar);

                AccumulateEightProductsSse2(
                    accumulator,
                    i + tailStart,
                    tailValues128,
                    scalarVector128,
                    doubleProducts: false);

                j =
                    scalarTailStart;
            }

            for (;
                 j < right.Length;
                 j++)
            {
                accumulator[i + j] +=
                    (ulong)scalar *
                    right[j];
            }
        }

        return NormalizeAccumulator(
            accumulator,
            coefficientCount);
    }

    private static ushort[] SquareMagnitude(
        ushort[] value,
        ulong[] accumulatorWorkspace,
        CancellationToken cancellationToken)
    {
        if (IsZero(value))
        {
            return ZeroMagnitude;
        }

        int coefficientCount =
            checked(
                value.Length * 2);

        Span<ulong> accumulator =
            accumulatorWorkspace.AsSpan(
                0,
                coefficientCount);

        ReadOnlySpan<ushort> valueSpan =
            value;

        bool useAvx512 =
            IsAvx512CustomWindowActive;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ushort scalar =
                value[i];

            accumulator[i + i] +=
                (ulong)scalar *
                scalar;

            if (scalar == 0)
            {
                continue;
            }

            int j =
                i + 1;

            int remaining =
                value.Length - j;

            if (useAvx512)
            {
                int vector512Count =
                    remaining / Vector512UShortCount;

                if (vector512Count != 0)
                {
                    int vector512Length =
                        vector512Count * Vector512UShortCount;

                    ReadOnlySpan<Vector256<ushort>> valuesForAvx512 =
                        MemoryMarshal.Cast<ushort, Vector256<ushort>>(
                            valueSpan.Slice(j, vector512Length));

                    Vector512<uint> scalarVector512 =
                        Vector512.Create((uint)scalar);

                    for (int vectorIndex = 0;
                         vectorIndex < vector512Count;
                         vectorIndex++)
                    {
                        int sourceVectorIndex =
                            vectorIndex * 2;

                        AccumulateThirtyTwoProductsAvx512UInt32(
                            accumulator,
                            i + j +
                            vectorIndex * Vector512UShortCount,
                            valuesForAvx512[sourceVectorIndex],
                            valuesForAvx512[sourceVectorIndex + 1],
                            scalarVector512,
                            doubleProducts: true);
                    }

                    j += vector512Length;
                    remaining -= vector512Length;
                }
            }

            int vector256Count =
                remaining /
                VectorUShortCount;

            if (vector256Count != 0)
            {
                int vector256Length =
                    vector256Count *
                    VectorUShortCount;

                ReadOnlySpan<Vector256<ushort>> values256 =
                    MemoryMarshal.Cast<ushort, Vector256<ushort>>(
                        valueSpan.Slice(
                            j,
                            vector256Length));

                Vector256<ushort> scalarVector256 =
                    Vector256.Create(scalar);

                for (int vectorIndex = 0;
                     vectorIndex < vector256Count;
                     vectorIndex++)
                {
                    AccumulateSixteenProductsAvx2(
                        accumulator,
                        i + j +
                        vectorIndex * VectorUShortCount,
                        values256[vectorIndex],
                        scalarVector256,
                        doubleProducts: true);
                }

                j +=
                    vector256Length;
            }

            // AVX2 has no masked UInt16 tail load, but an AVX2-capable x86
            // processor also has SSE2. Consume another eight products with
            // a 128-bit vector before falling back to scalar. This cuts the
            // average scalar triangular tail roughly in half without any
            // pack/extract gymnastics or extra 256-bit register pressure.
            if (value.Length - j >= 8)
            {
                Vector128<ushort> values128 =
                    MemoryMarshal.Cast<ushort, Vector128<ushort>>(
                        valueSpan.Slice(
                            j,
                            8))[0];

                Vector128<ushort> scalarVector128 =
                    Vector128.Create(scalar);

                AccumulateEightProductsSse2(
                    accumulator,
                    i + j,
                    values128,
                    scalarVector128,
                    doubleProducts: true);

                j += 8;
            }

            for (;
                 j < value.Length;
                 j++)
            {
                accumulator[i + j] +=
                    ((ulong)scalar *
                     value[j]) << 1;
            }
        }

        return NormalizeAccumulator(
            accumulator,
            coefficientCount);
    }

    private static void AccumulateThirtyTwoProductsAvx512UInt32(
        Span<ulong> accumulator,
        int destinationStart,
        Vector256<ushort> values0To15UShort,
        Vector256<ushort> values16To31UShort,
        Vector512<uint> scalar,
        bool doubleProducts)
    {
        // Variant 06: widen the two contiguous UInt16 halves directly to
        // UInt32, then multiply with VPMULLD. Since both operands are <= 65535,
        // every product is <= 0xFFFE0001 and therefore the low UInt32 result is
        // the complete product. This removes two UInt16 multiply streams,
        // two lane-local unpacks, and two VPERMI2D reorder operations from the
        // old AVX-512BW reconstruction path.
        Vector512<uint> values0To15 =
            Avx512F.ConvertToVector512UInt32(
                values0To15UShort);

        Vector512<uint> values16To31 =
            Avx512F.ConvertToVector512UInt32(
                values16To31UShort);

        Vector512<uint> products0To15 =
            Avx512F.MultiplyLow(
                values0To15,
                scalar);

        Vector512<uint> products16To31 =
            Avx512F.MultiplyLow(
                values16To31,
                scalar);

        Vector512<ulong> products0To7 =
            Vector512.WidenLower(products0To15);

        Vector512<ulong> products8To15 =
            Vector512.WidenUpper(products0To15);

        Vector512<ulong> products16To23 =
            Vector512.WidenLower(products16To31);

        Vector512<ulong> products24To31 =
            Vector512.WidenUpper(products16To31);

        if (doubleProducts)
        {
            products0To7 =
                Avx512F.ShiftLeftLogical(products0To7, 1);

            products8To15 =
                Avx512F.ShiftLeftLogical(products8To15, 1);

            products16To23 =
                Avx512F.ShiftLeftLogical(products16To23, 1);

            products24To31 =
                Avx512F.ShiftLeftLogical(products24To31, 1);
        }

        Span<Vector512<ulong>> accumulatorVectors =
            MemoryMarshal.Cast<ulong, Vector512<ulong>>(
                accumulator.Slice(
                    destinationStart,
                    Vector512UShortCount));

        accumulatorVectors[0] =
            Avx512F.Add(
                accumulatorVectors[0],
                products0To7);

        accumulatorVectors[1] =
            Avx512F.Add(
                accumulatorVectors[1],
                products8To15);

        accumulatorVectors[2] =
            Avx512F.Add(
                accumulatorVectors[2],
                products16To23);

        accumulatorVectors[3] =
            Avx512F.Add(
                accumulatorVectors[3],
                products24To31);
    }

    private static void AccumulateSixteenProductsAvx2(
        Span<ulong> accumulator,
        int destinationStart,
        Vector256<ushort> values,
        Vector256<ushort> scalar,
        bool doubleProducts)
    {
        // Recover all 32 product bits from sixteen UInt16 lanes. The previous
        // implementation then extracted every lane with GetElement and added
        // sixteen UInt64 values scalarly. On AVX2 that creates a long chain of
        // lane-extract instructions and scalar load/add/store operations.
        //
        // Keep the products in registers instead. Zero-extend four UInt32
        // products at a time to UInt64, repair AVX2's 128-bit-lane-local
        // unpack order with two VPERM2I128 operations per packed vector, then
        // accumulate four contiguous UInt64 coefficients with VPADDQ. The
        // carry chain remains entirely outside this loop and is normalized
        // once after the convolution, exactly as before.
        Vector256<ushort> low =
            Avx2.MultiplyLow(
                values,
                scalar);

        Vector256<ushort> high =
            Avx2.MultiplyHigh(
                values,
                scalar);

        Vector256<uint> lowPacked =
            Avx2.UnpackLow(
                    low,
                    high)
                .AsUInt32();

        Vector256<uint> highPacked =
            Avx2.UnpackHigh(
                    low,
                    high)
                .AsUInt32();

        Vector256<uint> zero32 =
            Vector256<uint>.Zero;

        // lowPacked is p0..p3,p8..p11 because VPUNPCK is lane-local.
        Vector256<ulong> low01And89 =
            Avx2.UnpackLow(
                    lowPacked,
                    zero32)
                .AsUInt64();

        Vector256<ulong> low23And1011 =
            Avx2.UnpackHigh(
                    lowPacked,
                    zero32)
                .AsUInt64();

        // highPacked is p4..p7,p12..p15.
        Vector256<ulong> high45And1213 =
            Avx2.UnpackLow(
                    highPacked,
                    zero32)
                .AsUInt64();

        Vector256<ulong> high67And1415 =
            Avx2.UnpackHigh(
                    highPacked,
                    zero32)
                .AsUInt64();

        Vector256<ulong> products0To3 =
            Avx2.Permute2x128(
                    low01And89.AsInt64(),
                    low23And1011.AsInt64(),
                    0x20)
                .AsUInt64();

        Vector256<ulong> products4To7 =
            Avx2.Permute2x128(
                    high45And1213.AsInt64(),
                    high67And1415.AsInt64(),
                    0x20)
                .AsUInt64();

        Vector256<ulong> products8To11 =
            Avx2.Permute2x128(
                    low01And89.AsInt64(),
                    low23And1011.AsInt64(),
                    0x31)
                .AsUInt64();

        Vector256<ulong> products12To15 =
            Avx2.Permute2x128(
                    high45And1213.AsInt64(),
                    high67And1415.AsInt64(),
                    0x31)
                .AsUInt64();

        if (doubleProducts)
        {
            products0To3 =
                Avx2.ShiftLeftLogical(
                    products0To3,
                    1);

            products4To7 =
                Avx2.ShiftLeftLogical(
                    products4To7,
                    1);

            products8To11 =
                Avx2.ShiftLeftLogical(
                    products8To11,
                    1);

            products12To15 =
                Avx2.ShiftLeftLogical(
                    products12To15,
                    1);
        }

        Span<Vector256<ulong>> accumulatorVectors =
            MemoryMarshal.Cast<ulong, Vector256<ulong>>(
                accumulator.Slice(
                    destinationStart,
                    VectorUShortCount));

        accumulatorVectors[0] =
            Avx2.Add(
                accumulatorVectors[0],
                products0To3);

        accumulatorVectors[1] =
            Avx2.Add(
                accumulatorVectors[1],
                products4To7);

        accumulatorVectors[2] =
            Avx2.Add(
                accumulatorVectors[2],
                products8To11);

        accumulatorVectors[3] =
            Avx2.Add(
                accumulatorVectors[3],
                products12To15);
    }

    private static void AccumulateEightProductsSse2(
        Span<ulong> accumulator,
        int destinationStart,
        Vector128<ushort> values,
        Vector128<ushort> scalar,
        bool doubleProducts)
    {
        Vector128<ushort> low =
            Sse2.MultiplyLow(
                values,
                scalar);

        Vector128<ushort> high =
            Sse2.MultiplyHigh(
                values,
                scalar);

        Vector128<uint> lowPacked =
            Sse2.UnpackLow(
                    low,
                    high)
                .AsUInt32();

        Vector128<uint> highPacked =
            Sse2.UnpackHigh(
                    low,
                    high)
                .AsUInt32();

        Vector128<uint> zero32 =
            Vector128<uint>.Zero;

        Vector128<ulong> products0To1 =
            Sse2.UnpackLow(
                    lowPacked,
                    zero32)
                .AsUInt64();

        Vector128<ulong> products2To3 =
            Sse2.UnpackHigh(
                    lowPacked,
                    zero32)
                .AsUInt64();

        Vector128<ulong> products4To5 =
            Sse2.UnpackLow(
                    highPacked,
                    zero32)
                .AsUInt64();

        Vector128<ulong> products6To7 =
            Sse2.UnpackHigh(
                    highPacked,
                    zero32)
                .AsUInt64();

        if (doubleProducts)
        {
            products0To1 =
                Sse2.ShiftLeftLogical(
                    products0To1,
                    1);

            products2To3 =
                Sse2.ShiftLeftLogical(
                    products2To3,
                    1);

            products4To5 =
                Sse2.ShiftLeftLogical(
                    products4To5,
                    1);

            products6To7 =
                Sse2.ShiftLeftLogical(
                    products6To7,
                    1);
        }

        Span<Vector128<ulong>> accumulatorVectors =
            MemoryMarshal.Cast<ulong, Vector128<ulong>>(
                accumulator.Slice(
                    destinationStart,
                    8));

        accumulatorVectors[0] =
            Sse2.Add(
                accumulatorVectors[0],
                products0To1);

        accumulatorVectors[1] =
            Sse2.Add(
                accumulatorVectors[1],
                products2To3);

        accumulatorVectors[2] =
            Sse2.Add(
                accumulatorVectors[2],
                products4To5);

        accumulatorVectors[3] =
            Sse2.Add(
                accumulatorVectors[3],
                products6To7);
    }

    private static ushort[] NormalizeAccumulator(
        Span<ulong> accumulator,
        int coefficientCount)
    {
        // The convolution reserves one zero coefficient at the top: for an
        // n-by-m product coefficientCount is n+m, while the highest raw
        // product lands at n+m-2. Normal carry propagation therefore fits in
        // exactly coefficientCount base-2^16 limbs. Allocate that exact
        // capacity instead of coefficientCount+1; the old shape guaranteed a
        // second trim/copy allocation on virtually every custom multiply or
        // square even when the magnitude already occupied its full length.
        ushort[] result =
            GC.AllocateUninitializedArray<ushort>(
                coefficientCount);

        ulong carry = 0;
        int i = 0;
        int unrolledEnd =
            coefficientCount & ~3;

        // Carry is inherently serial, but four-way unrolling removes most of
        // the loop/index bookkeeping. Clear each accumulator coefficient at
        // the point it is consumed; because this backend is single-threaded,
        // the same workspace can immediately be reused by the next operation
        // without another full-memory clear pass.
        for (;
             i < unrolledEnd;
             i += 4)
        {
            ulong total0 =
                accumulator[i] +
                carry;
            accumulator[i] = 0;
            result[i] = (ushort)total0;
            carry = total0 >> 16;

            ulong total1 =
                accumulator[i + 1] +
                carry;
            accumulator[i + 1] = 0;
            result[i + 1] = (ushort)total1;
            carry = total1 >> 16;

            ulong total2 =
                accumulator[i + 2] +
                carry;
            accumulator[i + 2] = 0;
            result[i + 2] = (ushort)total2;
            carry = total2 >> 16;

            ulong total3 =
                accumulator[i + 3] +
                carry;
            accumulator[i + 3] = 0;
            result[i + 3] = (ushort)total3;
            carry = total3 >> 16;
        }

        for (;
             i < coefficientCount;
             i++)
        {
            ulong total =
                accumulator[i] +
                carry;

            accumulator[i] = 0;
            result[i] = (ushort)total;
            carry = total >> 16;
        }

        // A mathematically valid product/square cannot overflow the reserved
        // top coefficient. Keep a defensive path in case a future convolution
        // kernel changes that invariant, without penalizing the normal hot path.
        if (carry != 0)
        {
            int extraLimbCount = 0;
            ulong remainingCarry = carry;

            while (remainingCarry != 0)
            {
                extraLimbCount++;
                remainingCarry >>= 16;
            }

            ushort[] expanded =
                GC.AllocateUninitializedArray<ushort>(
                    checked(
                        coefficientCount +
                        extraLimbCount));

            result.AsSpan().CopyTo(expanded);

            int writeIndex =
                coefficientCount;

            while (carry != 0)
            {
                expanded[writeIndex++] =
                    (ushort)carry;

                carry >>= 16;
            }

            return expanded;
        }

        int written =
            coefficientCount;

        while (written > 1 &&
               result[written - 1] == 0)
        {
            written--;
        }

        if (written == coefficientCount)
        {
            return result;
        }

        ushort[] trimmed =
            GC.AllocateUninitializedArray<ushort>(
                written);

        result.AsSpan(
                0,
                written)
            .CopyTo(trimmed);

        return trimmed;
    }

    private static ushort[] FromUInt64(
        ulong value)
    {
        if (value == 0)
        {
            return ZeroMagnitude;
        }

        int length =
            value <= ushort.MaxValue
                ? 1
                : value <= uint.MaxValue
                    ? 2
                    : value <= 0x0000_FFFF_FFFF_FFFFUL
                        ? 3
                        : 4;

        ushort[] limbs =
            new ushort[length];

        for (int i = 0;
             i < length;
             i++)
        {
            limbs[i] =
                (ushort)value;

            value >>= 16;
        }

        return limbs;
    }

    private static BigInteger ToBigInteger(
        ushort[] magnitude)
    {
        if (IsZero(magnitude))
        {
            return BigInteger.Zero;
        }

        byte[] bytes =
            new byte[
                checked(
                    magnitude.Length * 2)];

        for (int i = 0;
             i < magnitude.Length;
             i++)
        {
            ushort limb =
                magnitude[i];

            int byteIndex =
                i * 2;

            bytes[byteIndex] =
                (byte)limb;

            bytes[byteIndex + 1] =
                (byte)(limb >> 8);
        }

        return new BigInteger(
            bytes,
            isUnsigned: true,
            isBigEndian: false);
    }

    private static bool IsZero(
        ushort[] magnitude) =>
        magnitude.Length == 1 &&
        magnitude[0] == 0;

    private static ushort[] ZeroMagnitude =>
        [0];

    private static ushort[] OneMagnitude =>
        [1];
}
