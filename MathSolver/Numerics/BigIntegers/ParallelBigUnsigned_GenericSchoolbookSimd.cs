using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#if ANDROID
using System.Runtime.Intrinsics.Arm;
#endif

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    // Generic schoolbook products are bounded by SchoolbookWorkLimit.  Below
    // this amount of work, vector setup and repeated coefficient RMW traffic
    // can cost more than the scalar nested loop.  The <=5-limb power-engine
    // case has its own output-centric SIMD path and is dispatched first.
    private const long GenericSchoolbookSimdMinimumWork = 32;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ParallelBigUnsigned MultiplyGenericSchoolbookSimd(
        ParallelBigUnsigned left,
        ParallelBigUnsigned right,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        // Sweep the longer operand with a broadcast limb from the shorter
        // operand.  This minimizes full coefficient-array read/modify/write
        // passes while keeping all source and destination accesses contiguous.
        ParallelBigUnsigned outer =
            left._limbCount <= right._limbCount ? left : right;
        ParallelBigUnsigned inner =
            ReferenceEquals(outer, left) ? right : left;

        int outerCount = outer._limbCount;
        int innerCount = inner._limbCount;
        int coefficientCount = checked(outerCount + innerCount - 1);
        var coefficients = new ulong[coefficientCount];

        uint[] outerLimbs = outer._limbs;
        uint[] innerLimbs = inner._limbs;

        ref uint outerRef =
            ref MemoryMarshal.GetArrayDataReference(outerLimbs);
        ref uint innerRef =
            ref MemoryMarshal.GetArrayDataReference(innerLimbs);
        ref ulong coefficientRef =
            ref MemoryMarshal.GetArrayDataReference(coefficients);

        for (int outerIndex = 0;
             outerIndex < outerCount;
             outerIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            uint multiplier = Unsafe.Add(ref outerRef, outerIndex);

            if (workers.UseAvx512Ntt &&
                Avx512F.IsSupported &&
                Avx2.IsSupported)
            {
                // AVX-512DQ gives a native eight-qword product after one
                // VPMOVZXDQ.  AVX-512F-only CPUs retain the exact AVX2 row
                // kernel rather than introducing scalar qword multiplies.
                if (Avx512DQ.IsSupported)
                {
                    AccumulateGenericSchoolbookRowAvx512(
                        ref innerRef,
                        innerCount,
                        ref coefficientRef,
                        outerIndex,
                        multiplier);
                }
                else
                {
                    AccumulateGenericSchoolbookRowAvx2(
                        ref innerRef,
                        innerCount,
                        ref coefficientRef,
                        outerIndex,
                        multiplier);
                }

                continue;
            }

            if (workers.UseAvx2Ntt && Avx2.IsSupported)
            {
                AccumulateGenericSchoolbookRowAvx2(
                    ref innerRef,
                    innerCount,
                    ref coefficientRef,
                    outerIndex,
                    multiplier);
                continue;
            }

            if (workers.UseSseNtt && Sse2.IsSupported)
            {
                AccumulateGenericSchoolbookRowSse(
                    ref innerRef,
                    innerCount,
                    ref coefficientRef,
                    outerIndex,
                    multiplier);
                continue;
            }

#if ANDROID
            if (workers.UseNeonNtt && AdvSimd.Arm64.IsSupported)
            {
                AccumulateGenericSchoolbookRowNeon(
                    ref innerRef,
                    innerCount,
                    ref coefficientRef,
                    outerIndex,
                    multiplier);
                continue;
            }
#endif

            AccumulateGenericSchoolbookRowScalar(
                ref innerRef,
                innerCount,
                ref coefficientRef,
                outerIndex,
                multiplier);
        }

        // CreateFromCoefficients() now shares the exact SIMD base-10,000
        // carry dispatcher used by CRT, so generic schoolbook no longer falls
        // back to a scalar /10000 pass after the SIMD convolution.
        return CreateFromCoefficients(
            coefficients,
            workers,
            diagnostics,
            cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AccumulateGenericSchoolbookRowScalar(
        ref uint source,
        int count,
        ref ulong destination,
        int destinationOffset,
        uint multiplier)
    {
        for (int index = 0; index < count; index++)
        {
            ref ulong target =
                ref Unsafe.Add(ref destination, destinationOffset + index);
            target +=
                (ulong)Unsafe.Add(ref source, index) * multiplier;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateEightGenericSchoolbookProductsAvx2(
        ref uint source,
        int sourceIndex,
        ref ulong destination,
        int destinationIndex,
        Vector256<uint> multiplier)
    {
        Vector256<uint> values =
            Vector256.LoadUnsafe(ref source, (nuint)sourceIndex);

        // VPMULUDQ multiplies the even dwords into four qword lanes.  Shift
        // each qword by 32 to expose the odd dwords, then multiply those too.
        Vector256<ulong> productEven =
            Avx2.Multiply(values, multiplier);
        Vector256<uint> oddValues =
            Avx2.ShiftRightLogical(values.AsUInt64(), 32).AsUInt32();
        Vector256<ulong> productOdd =
            Avx2.Multiply(oddValues, multiplier);

        // Interleave [p0,p2,p4,p6] and [p1,p3,p5,p7] into two contiguous
        // qword vectors [p0..p3] and [p4..p7].  This avoids gather/scatter and
        // keeps coefficient updates as two ordinary YMM load/add/store chains.
        Vector256<ulong> unpackLow =
            Avx2.UnpackLow(
                    productEven.AsInt64(),
                    productOdd.AsInt64())
                .AsUInt64();
        Vector256<ulong> unpackHigh =
            Avx2.UnpackHigh(
                    productEven.AsInt64(),
                    productOdd.AsInt64())
                .AsUInt64();

        Vector256<ulong> products0 =
            Avx2.Permute2x128(
                    unpackLow.AsInt64(),
                    unpackHigh.AsInt64(),
                    0x20)
                .AsUInt64();
        Vector256<ulong> products1 =
            Avx2.Permute2x128(
                    unpackLow.AsInt64(),
                    unpackHigh.AsInt64(),
                    0x31)
                .AsUInt64();

        Vector256<ulong> destination0 =
            Vector256.LoadUnsafe(
                ref destination,
                (nuint)destinationIndex);
        Vector256<ulong> destination1 =
            Vector256.LoadUnsafe(
                ref destination,
                (nuint)(destinationIndex + 4));

        Avx2.Add(
                destination0.AsInt64(),
                products0.AsInt64())
            .AsUInt64()
            .StoreUnsafe(
                ref destination,
                (nuint)destinationIndex);
        Avx2.Add(
                destination1.AsInt64(),
                products1.AsInt64())
            .AsUInt64()
            .StoreUnsafe(
                ref destination,
                (nuint)(destinationIndex + 4));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AccumulateGenericSchoolbookRowAvx2(
        ref uint source,
        int count,
        ref ulong destination,
        int destinationOffset,
        uint multiplierValue)
    {
        const int Width = 8;
        const int UnrolledWidth = Width * 2;
        int index = 0;
        Vector256<uint> multiplier = Vector256.Create(multiplierValue);

        int unrolledEnd = count - UnrolledWidth + 1;
        for (; index < unrolledEnd; index += UnrolledWidth)
        {
            AccumulateEightGenericSchoolbookProductsAvx2(
                ref source,
                index,
                ref destination,
                destinationOffset + index,
                multiplier);
            AccumulateEightGenericSchoolbookProductsAvx2(
                ref source,
                index + Width,
                ref destination,
                destinationOffset + index + Width,
                multiplier);
        }

        int vectorEnd = count - Width + 1;
        for (; index < vectorEnd; index += Width)
        {
            AccumulateEightGenericSchoolbookProductsAvx2(
                ref source,
                index,
                ref destination,
                destinationOffset + index,
                multiplier);
        }

        if (index < count)
        {
            AccumulateGenericSchoolbookTailAvx2(
                ref source, index, count - index,
                ref destination, destinationOffset + index, multiplierValue);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateGenericSchoolbookTailAvx2(
        ref uint source,
        int sourceIndex,
        int count,
        ref ulong destination,
        int destinationIndex,
        uint multiplierValue)
    {
        Span<uint> sourceScratch = stackalloc uint[8];
        Span<ulong> destinationScratch = stackalloc ulong[8];
        for (int lane = 0; lane < count; lane++)
        {
            sourceScratch[lane] = Unsafe.Add(ref source, sourceIndex + lane);
            destinationScratch[lane] = Unsafe.Add(ref destination, destinationIndex + lane);
        }

        ref uint sourceScratchRef = ref MemoryMarshal.GetReference(sourceScratch);
        ref ulong destinationScratchRef = ref MemoryMarshal.GetReference(destinationScratch);
        AccumulateEightGenericSchoolbookProductsAvx2(
            ref sourceScratchRef, 0, ref destinationScratchRef, 0,
            Vector256.Create(multiplierValue));

        for (int lane = 0; lane < count; lane++)
            Unsafe.Add(ref destination, destinationIndex + lane) = destinationScratch[lane];
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AccumulateGenericSchoolbookRowAvx512(
        ref uint source,
        int count,
        ref ulong destination,
        int destinationOffset,
        uint multiplierValue)
    {
        const int Width = 8;
        const int UnrolledWidth = Width * 2;
        int index = 0;
        Vector512<ulong> multiplier =
            Vector512.Create((ulong)multiplierValue);

        // Eight uint32 inputs zero-extend directly into eight qword lanes.
        // VPMULLQ then computes all products exactly; base-10,000 limbs make
        // each individual product < 1e8, while the accumulated coefficient is
        // kept in ulong for the full generic schoolbook overlap.  Two ZMM
        // chains are issued together so multiply/load latency can overlap.
        int unrolledEnd = count - UnrolledWidth + 1;
        for (; index < unrolledEnd; index += UnrolledWidth)
        {
            Vector512<ulong> widened0 =
                Avx512F.ConvertToVector512UInt64(
                    Vector256.LoadUnsafe(ref source, (nuint)index));
            Vector512<ulong> widened1 =
                Avx512F.ConvertToVector512UInt64(
                    Vector256.LoadUnsafe(ref source, (nuint)(index + Width)));

            Vector512<ulong> products0 =
                Avx512DQ.MultiplyLow(widened0, multiplier);
            Vector512<ulong> products1 =
                Avx512DQ.MultiplyLow(widened1, multiplier);

            int destinationIndex = destinationOffset + index;
            Vector512<ulong> current0 =
                Vector512.LoadUnsafe(ref destination, (nuint)destinationIndex);
            Vector512<ulong> current1 =
                Vector512.LoadUnsafe(ref destination, (nuint)(destinationIndex + Width));

            Vector512.Add(current0, products0).StoreUnsafe(
                ref destination, (nuint)destinationIndex);
            Vector512.Add(current1, products1).StoreUnsafe(
                ref destination, (nuint)(destinationIndex + Width));
        }

        int vectorEnd = count - Width + 1;
        for (; index < vectorEnd; index += Width)
        {
            Vector512<ulong> widened =
                Avx512F.ConvertToVector512UInt64(
                    Vector256.LoadUnsafe(ref source, (nuint)index));
            Vector512<ulong> products =
                Avx512DQ.MultiplyLow(widened, multiplier);
            int destinationIndex = destinationOffset + index;
            Vector512<ulong> current =
                Vector512.LoadUnsafe(ref destination, (nuint)destinationIndex);

            Vector512.Add(current, products).StoreUnsafe(
                ref destination, (nuint)destinationIndex);
        }

        if (index < count)
        {
            // AVX-512 production targets also expose AVX2; use one padded YMM
            // for the <8 residual outputs instead of dropping back to scalar
            // multiplication.
            AccumulateGenericSchoolbookTailAvx2(
                ref source, index, count - index,
                ref destination, destinationOffset + index, multiplierValue);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AccumulateGenericSchoolbookRowSse(
        ref uint source,
        int count,
        ref ulong destination,
        int destinationOffset,
        uint multiplierValue)
    {
        int index = 0;

        if (Sse41.IsSupported)
        {
            const int Width = 4;
            int vectorEnd = count - Width + 1;
            Vector128<int> multiplier =
                Vector128.Create((int)multiplierValue);
            Vector128<uint> zero = Vector128<uint>.Zero;

            for (; index < vectorEnd; index += Width)
            {
                // PMULLD is exact here because one base-10,000 limb product is
                // at most 99,980,001.  Widen before adding to the long-lived
                // ulong coefficient accumulator so hundreds of row products
                // cannot overflow a dword lane.
                Vector128<uint> products32 =
                    Sse41.MultiplyLow(
                            Vector128.LoadUnsafe(
                                    ref source,
                                    (nuint)index)
                                .AsInt32(),
                            multiplier)
                        .AsUInt32();
                Vector128<ulong> productsLow =
                    Sse2.UnpackLow(products32, zero).AsUInt64();
                Vector128<ulong> productsHigh =
                    Sse2.UnpackHigh(products32, zero).AsUInt64();

                int destinationIndex = destinationOffset + index;
                Vector128<ulong> currentLow =
                    Vector128.LoadUnsafe(
                        ref destination,
                        (nuint)destinationIndex);
                Vector128<ulong> currentHigh =
                    Vector128.LoadUnsafe(
                        ref destination,
                        (nuint)(destinationIndex + 2));

                Sse2.Add(
                        currentLow.AsInt64(),
                        productsLow.AsInt64())
                    .AsUInt64()
                    .StoreUnsafe(
                        ref destination,
                        (nuint)destinationIndex);
                Sse2.Add(
                        currentHigh.AsInt64(),
                        productsHigh.AsInt64())
                    .AsUInt64()
                    .StoreUnsafe(
                        ref destination,
                        (nuint)(destinationIndex + 2));
            }
        }
        else
        {
            const int Width = 2;
            int vectorEnd = count - Width + 1;

            for (; index < vectorEnd; index += Width)
            {
                Vector128<ulong> products =
                    MultiplyTwoBaseLimbsSse(
                        ref source,
                        index,
                        multiplierValue);
                int destinationIndex = destinationOffset + index;
                Vector128<ulong> current =
                    Vector128.LoadUnsafe(
                        ref destination,
                        (nuint)destinationIndex);

                Sse2.Add(
                        current.AsInt64(),
                        products.AsInt64())
                    .AsUInt64()
                    .StoreUnsafe(
                        ref destination,
                        (nuint)destinationIndex);
            }
        }

        if (index < count)
        {
            int width = Sse41.IsSupported ? 4 : 2;
            Span<uint> sourceScratch = stackalloc uint[4];
            Span<ulong> destinationScratch = stackalloc ulong[4];
            int remaining = count - index;
            for (int lane = 0; lane < remaining; lane++)
            {
                sourceScratch[lane] = Unsafe.Add(ref source, index + lane);
                destinationScratch[lane] = Unsafe.Add(ref destination, destinationOffset + index + lane);
            }

            ref uint sourceScratchRef = ref MemoryMarshal.GetReference(sourceScratch);
            ref ulong destinationScratchRef = ref MemoryMarshal.GetReference(destinationScratch);
            AccumulateGenericSchoolbookRowSse(
                ref sourceScratchRef, width, ref destinationScratchRef, 0, multiplierValue);

            for (int lane = 0; lane < remaining; lane++)
                Unsafe.Add(ref destination, destinationOffset + index + lane) = destinationScratch[lane];
        }
    }

#if ANDROID
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AccumulateGenericSchoolbookRowNeon(
        ref uint source,
        int count,
        ref ulong destination,
        int destinationOffset,
        uint multiplierValue)
    {
        const int Width = 4;
        int index = 0;
        int vectorEnd = count - Width + 1;
        Vector128<uint> multiplier = Vector128.Create(multiplierValue);
        Vector64<uint> one = Vector64.Create(1u);

        for (; index < vectorEnd; index += Width)
        {
            Vector128<uint> products32 =
                AdvSimd.Multiply(
                    Vector128.LoadUnsafe(
                        ref source,
                        (nuint)index),
                    multiplier);

            Vector128<ulong> productsLow =
                AdvSimd.MultiplyWideningLower(
                    products32.GetLower(),
                    one);
            Vector128<ulong> productsHigh =
                AdvSimd.MultiplyWideningLower(
                    products32.GetUpper(),
                    one);

            int destinationIndex = destinationOffset + index;
            Vector128<ulong> currentLow =
                Vector128.LoadUnsafe(
                    ref destination,
                    (nuint)destinationIndex);
            Vector128<ulong> currentHigh =
                Vector128.LoadUnsafe(
                    ref destination,
                    (nuint)(destinationIndex + 2));

            Vector128.Add(currentLow, productsLow).StoreUnsafe(
                ref destination,
                (nuint)destinationIndex);
            Vector128.Add(currentHigh, productsHigh).StoreUnsafe(
                ref destination,
                (nuint)(destinationIndex + 2));
        }

        if (index < count)
        {
            Span<uint> sourceScratch = stackalloc uint[4];
            Span<ulong> destinationScratch = stackalloc ulong[4];
            int remaining = count - index;
            for (int lane = 0; lane < remaining; lane++)
            {
                sourceScratch[lane] = Unsafe.Add(ref source, index + lane);
                destinationScratch[lane] = Unsafe.Add(ref destination, destinationOffset + index + lane);
            }

            ref uint sourceScratchRef = ref MemoryMarshal.GetReference(sourceScratch);
            ref ulong destinationScratchRef = ref MemoryMarshal.GetReference(destinationScratch);
            AccumulateGenericSchoolbookRowNeon(
                ref sourceScratchRef, 4, ref destinationScratchRef, 0, multiplierValue);

            for (int lane = 0; lane < remaining; lane++)
                Unsafe.Add(ref destination, destinationOffset + index + lane) = destinationScratch[lane];
        }
    }
#endif
}
