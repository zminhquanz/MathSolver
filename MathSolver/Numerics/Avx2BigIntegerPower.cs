using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

/// <summary>
/// Experimental single-threaded BigInteger power backend for x86/x64 AVX2.
///
/// The hot window uses unsigned base-2^16 limbs. Multiplication accumulates
/// carry-independent convolution coefficients in UInt64 slots, allowing AVX2
/// to batch sixteen UInt16 products at a time without putting the carry chain
/// inside the SIMD loop. Squaring uses symmetry (i,j == j,i), so only the upper
/// triangle is multiplied and off-diagonal products are doubled.
///
/// This is deliberately bounded. Once either operand grows beyond the tuned
/// limb window, execution converts the current magnitude to System.Numerics
/// BigInteger exactly once and finishes with the runtime's mature large-number
/// multiplier. This prevents an O(n^2) schoolbook backend from competing with
/// Karatsuba/other runtime algorithms at large sizes.
///
/// No pointers, Unsafe.Add, native code, Barrett, or Montgomery arithmetic are
/// used. The shared Hardware acceleration switch gates entry to this backend.
/// </summary>
internal static class Avx2BigIntegerPower
{
    private const int VectorUShortCount = 16;

    // Multiplication and squaring deliberately use different crossover
    // windows. General multiplication keeps the conservative 256-limb result
    // cap, while symmetry makes squaring cheap enough to test one additional
    // growth step at 512 result limbs. This lets exponentiation-by-squaring
    // retain the AVX2 path slightly longer without allowing general O(n^2)
    // products to compete with the runtime at large sizes.
    private const int MaximumMultiplyResultLimbCount = 256;
    private const int MaximumSquareResultLimbCount = 512;

    public static bool IsSupported =>
        Avx2.IsSupported &&
        Vector256.IsHardwareAccelerated;

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

        ushort[] factorMagnitude =
            FromUInt64(magnitude);

        ushort[] resultMagnitude =
            OneMagnitude;

        bool resultInitialized = false;
        bool runtimeBigIntegerMode = false;

        BigInteger factorBigInteger =
            BigInteger.Zero;

        BigInteger resultBigInteger =
            BigInteger.One;

        int remainingExponent = exponent;
        int completedOperations = 0;

        void SwitchToRuntimeBigInteger()
        {
            if (runtimeBigIntegerMode)
            {
                return;
            }

            factorBigInteger =
                ToBigInteger(factorMagnitude);

            resultBigInteger =
                resultInitialized
                    ? ToBigInteger(resultMagnitude)
                    : BigInteger.One;

            runtimeBigIntegerMode = true;
        }

        while (remainingExponent > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((remainingExponent & 1) != 0)
            {
                if (!resultInitialized)
                {
                    if (runtimeBigIntegerMode)
                    {
                        resultBigInteger =
                            factorBigInteger;
                    }
                    else
                    {
                        resultMagnitude =
                            factorMagnitude;
                    }

                    resultInitialized = true;
                }
                else
                {
                    if (!runtimeBigIntegerMode &&
                        CanMultiplyInCustomWindow(
                            resultMagnitude.Length,
                            factorMagnitude.Length))
                    {
                        resultMagnitude =
                            MultiplyMagnitude(
                                resultMagnitude,
                                factorMagnitude,
                                cancellationToken);
                    }
                    else
                    {
                        SwitchToRuntimeBigInteger();

                        resultBigInteger *=
                            factorBigInteger;
                    }

                    progress(
                        ++completedOperations,
                        totalOperations);
                }
            }

            remainingExponent >>= 1;

            if (remainingExponent > 0)
            {
                if (!runtimeBigIntegerMode &&
                    CanSquareInCustomWindow(
                        factorMagnitude.Length))
                {
                    factorMagnitude =
                        SquareMagnitude(
                            factorMagnitude,
                            cancellationToken);
                }
                else
                {
                    SwitchToRuntimeBigInteger();

                    factorBigInteger *=
                        factorBigInteger;
                }

                progress(
                    ++completedOperations,
                    totalOperations);
            }
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

    private static bool CanMultiplyInCustomWindow(
        int leftLength,
        int rightLength) =>
        checked(leftLength + rightLength) <=
        MaximumMultiplyResultLimbCount;

    private static bool CanSquareInCustomWindow(
        int length) =>
        checked(length * 2) <=
        MaximumSquareResultLimbCount;

    private static ushort[] MultiplyMagnitude(
        ushort[] left,
        ushort[] right,
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

        ulong[] rentedAccumulator =
            ArrayPool<ulong>.Shared.Rent(
                coefficientCount);

        Span<ulong> accumulator =
            rentedAccumulator.AsSpan(
                0,
                coefficientCount);

        accumulator.Clear();

        try
        {
            ReadOnlySpan<ushort> rightSpan =
                right;

            // Hoist the 256-bit reinterpretation out of the outer schoolbook
            // loop. The previous version rebuilt Slice+Cast for every vector
            // batch of every scalar row. The data is read-only and stable, so
            // one Vector256 view can be reused for all rows.
            int vector256Count =
                right.Length /
                VectorUShortCount;

            int vector256Length =
                vector256Count *
                VectorUShortCount;

            ReadOnlySpan<Vector256<ushort>> rightVectors256 =
                vector256Count != 0
                    ? MemoryMarshal.Cast<ushort, Vector256<ushort>>(
                        rightSpan.Slice(
                            0,
                            vector256Length))
                    : ReadOnlySpan<Vector256<ushort>>.Empty;

            int tailStart =
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

                Vector256<ushort> scalarVector256 =
                    Vector256.Create(scalar);

                for (int vectorIndex = 0;
                     vectorIndex < vector256Count;
                     vectorIndex++)
                {
                    AccumulateSixteenProductsAvx2(
                        accumulator,
                        i +
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
        finally
        {
            accumulator.Clear();

            ArrayPool<ulong>.Shared.Return(
                rentedAccumulator);
        }
    }

    private static ushort[] SquareMagnitude(
        ushort[] value,
        CancellationToken cancellationToken)
    {
        if (IsZero(value))
        {
            return ZeroMagnitude;
        }

        int coefficientCount =
            checked(
                value.Length * 2);

        ulong[] rentedAccumulator =
            ArrayPool<ulong>.Shared.Rent(
                coefficientCount);

        Span<ulong> accumulator =
            rentedAccumulator.AsSpan(
                0,
                coefficientCount);

        accumulator.Clear();

        try
        {
            ReadOnlySpan<ushort> valueSpan =
                value;

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
        finally
        {
            accumulator.Clear();

            ArrayPool<ulong>.Shared.Return(
                rentedAccumulator);
        }
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
        ReadOnlySpan<ulong> accumulator,
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

        for (int i = 0;
             i < coefficientCount;
             i++)
        {
            ulong total =
                accumulator[i] +
                carry;

            result[i] =
                (ushort)total;

            carry =
                total >> 16;
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
