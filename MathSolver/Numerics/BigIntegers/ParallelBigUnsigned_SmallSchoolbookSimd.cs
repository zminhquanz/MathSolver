using System.Diagnostics;
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
    // Below this size the scalar schoolbook loop avoids worker/vector setup
    // more cheaply.  The left-to-right power path quickly grows far beyond
    // this threshold, which is where the output-centric SIMD path matters.
    private const int SmallBaseSchoolbookSimdMinimumLargeLimbs = 8;
    private const int SmallBaseSchoolbookParallelMinimumOutputs = 16 * 1024;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ParallelBigUnsigned MultiplySmallBaseSchoolbookSimd(
        ParallelBigUnsigned left,
        ParallelBigUnsigned right,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        ParallelBigUnsigned small =
            left._limbCount <= right._limbCount ? left : right;
        ParallelBigUnsigned large =
            ReferenceEquals(small, left) ? right : left;

        int smallCount = small._limbCount;
        int largeCount = large._limbCount;
        Debug.Assert(smallCount is >= 1 and <= 5);
        Debug.Assert(largeCount >= smallCount);

        int coefficientCount =
            checked(smallCount + largeCount - 1);
        var coefficients = new ulong[coefficientCount];

        uint[] smallLimbs = small._limbs;
        uint[] largeLimbs = large._limbs;

        // Only smallCount-1 coefficients exist on either edge.  Compute those
        // triangular boundaries with padded SIMD (out-of-range multiplicands are
        // zero) and give the long full-overlap middle to the regular SIMD kernel.
        int interiorStart = smallCount - 1;
        int interiorEnd = largeCount; // exclusive; k <= largeCount - 1

        ref uint initialSmallRef =
            ref MemoryMarshal.GetArrayDataReference(smallLimbs);
        ref uint initialLargeRef =
            ref MemoryMarshal.GetArrayDataReference(largeLimbs);
        ref ulong initialCoefficientRef =
            ref MemoryMarshal.GetArrayDataReference(coefficients);

        FillSmallBaseCoefficientBoundaryRangeSimd(
            ref initialSmallRef,
            smallCount,
            ref initialLargeRef,
            largeCount,
            ref initialCoefficientRef,
            0,
            interiorStart,
            workers,
            cancellationToken);

        int interiorCount = interiorEnd - interiorStart;
        void ProcessInterior(int rangeStart, int rangeEnd)
        {
            ref uint smallRef =
                ref MemoryMarshal.GetArrayDataReference(smallLimbs);
            ref uint largeRef =
                ref MemoryMarshal.GetArrayDataReference(largeLimbs);
            ref ulong coefficientRef =
                ref MemoryMarshal.GetArrayDataReference(coefficients);
            int start = interiorStart + rangeStart;
            int end = interiorStart + rangeEnd;

            if (workers.UseAvx512Ntt &&
                Avx512F.IsSupported &&
                Avx2.IsSupported)
            {
                FillSmallBaseCoefficientRangeAvx512(
                    ref smallRef, smallCount, ref largeRef, largeCount,
                    ref coefficientRef, start, end, cancellationToken);
                return;
            }

            if (workers.UseAvx2Ntt && Avx2.IsSupported)
            {
                FillSmallBaseCoefficientRangeAvx2(
                    ref smallRef, smallCount, ref largeRef, largeCount,
                    ref coefficientRef, start, end, cancellationToken);
                return;
            }

            if (workers.UseSseNtt && Sse2.IsSupported)
            {
                FillSmallBaseCoefficientRangeSse(
                    ref smallRef, smallCount, ref largeRef, largeCount,
                    ref coefficientRef, start, end, cancellationToken);
                return;
            }

#if ANDROID
            if (workers.UseNeonNtt && AdvSimd.Arm64.IsSupported)
            {
                FillSmallBaseCoefficientRangeNeon(
                    ref smallRef, smallCount, ref largeRef, largeCount,
                    ref coefficientRef, start, end, cancellationToken);
                return;
            }
#endif

            FillSmallBaseCoefficientRangeScalar(
                ref smallRef, smallCount, ref largeRef, largeCount,
                ref coefficientRef, start, end, cancellationToken);
        }

        int interiorVectorWidth =
            SelectSmallBaseSchoolbookSimdWidth(workers);

        if (workers.WorkerCount > 1 &&
            interiorCount >= SmallBaseSchoolbookParallelMinimumOutputs)
        {
            ExecuteVectorAlignedRanges(
                interiorCount,
                interiorVectorWidth,
                workers,
                cancellationToken,
                ProcessInterior);
        }
        else
        {
            ProcessInterior(0, interiorCount);
        }

        ref uint finalSmallRef =
            ref MemoryMarshal.GetArrayDataReference(smallLimbs);
        ref uint finalLargeRef =
            ref MemoryMarshal.GetArrayDataReference(largeLimbs);
        ref ulong finalCoefficientRef =
            ref MemoryMarshal.GetArrayDataReference(coefficients);
        FillSmallBaseCoefficientBoundaryRangeSimd(
            ref finalSmallRef,
            smallCount,
            ref finalLargeRef,
            largeCount,
            ref finalCoefficientRef,
            interiorEnd,
            coefficientCount,
            workers,
            cancellationToken);

        return CreateFromSmallBaseCoefficientsSimd(
            coefficients,
            workers,
            diagnostics,
            cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectSmallBaseSchoolbookSimdWidth(
        FixedWorkerTeam workers)
    {
        if (workers.UseAvx512Ntt && Avx512F.IsSupported && Avx2.IsSupported)
            return Vector512<uint>.Count;
        if (workers.UseAvx2Ntt && Avx2.IsSupported)
            return Vector256<uint>.Count;
        if (workers.UseSseNtt && Sse2.IsSupported)
            return Sse41.IsSupported ? Vector128<uint>.Count : 2;
#if ANDROID
        if (workers.UseNeonNtt && AdvSimd.Arm64.IsSupported)
            return Vector128<uint>.Count;
#endif
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillSmallBasePaddedInput(
        Span<uint> scratch,
        ref uint large,
        int largeCount,
        int outputStart,
        int outputCount,
        int smallIndex)
    {
        scratch.Clear();
        for (int lane = 0; lane < outputCount; lane++)
        {
            int sourceIndex = outputStart + lane - smallIndex;
            if ((uint)sourceIndex < (uint)largeCount)
            {
                scratch[lane] = Unsafe.Add(ref large, sourceIndex);
            }
        }
    }

    private static void FillSmallBaseCoefficientBoundaryRangeSimd(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        if (start >= end)
            return;

        if (workers.UseAvx512Ntt && Avx512F.IsSupported && Avx2.IsSupported)
        {
            FillSmallBaseCoefficientPaddedAvx512(
                ref small, smallCount, ref large, largeCount,
                ref destination, start, end, cancellationToken);
            return;
        }

        if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            FillSmallBaseCoefficientPaddedAvx2(
                ref small, smallCount, ref large, largeCount,
                ref destination, start, end, cancellationToken);
            return;
        }

        if (workers.UseSseNtt && Sse2.IsSupported)
        {
            FillSmallBaseCoefficientPaddedSse(
                ref small, smallCount, ref large, largeCount,
                ref destination, start, end, cancellationToken);
            return;
        }

#if ANDROID
        if (workers.UseNeonNtt && AdvSimd.Arm64.IsSupported)
        {
            FillSmallBaseCoefficientPaddedNeon(
                ref small, smallCount, ref large, largeCount,
                ref destination, start, end, cancellationToken);
            return;
        }
#endif

        FillSmallBaseCoefficientRangeScalar(
            ref small, smallCount, ref large, largeCount,
            ref destination, start, end, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void FillSmallBaseCoefficientPaddedAvx512(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        const int Width = 16;
        Span<uint> inputScratch = stackalloc uint[Width];
        Span<uint> resultScratch = stackalloc uint[Width];
        ref uint inputRef = ref MemoryMarshal.GetReference(inputScratch);
        ref uint resultRef = ref MemoryMarshal.GetReference(resultScratch);

        for (int outputStart = start; outputStart < end; outputStart += Width)
        {
            int count = Math.Min(Width, end - outputStart);
            Vector512<uint> sum = Vector512<uint>.Zero;
            for (int smallIndex = 0; smallIndex < smallCount; smallIndex++)
            {
                FillSmallBasePaddedInput(
                    inputScratch, ref large, largeCount,
                    outputStart, count, smallIndex);
                Vector512<uint> source = Vector512.LoadUnsafe(ref inputRef);
                sum = Vector512.Add(
                    sum,
                    Avx512F.MultiplyLow(
                        source,
                        Vector512.Create(Unsafe.Add(ref small, smallIndex))));
            }

            sum.StoreUnsafe(ref resultRef);
            for (int lane = 0; lane < count; lane++)
                Unsafe.Add(ref destination, outputStart + lane) = resultScratch[lane];
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void FillSmallBaseCoefficientPaddedAvx2(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        const int Width = 8;
        Span<uint> inputScratch = stackalloc uint[Width];
        Span<uint> resultScratch = stackalloc uint[Width];
        ref uint inputRef = ref MemoryMarshal.GetReference(inputScratch);
        ref uint resultRef = ref MemoryMarshal.GetReference(resultScratch);

        for (int outputStart = start; outputStart < end; outputStart += Width)
        {
            int count = Math.Min(Width, end - outputStart);
            Vector256<int> sum = Vector256<int>.Zero;
            for (int smallIndex = 0; smallIndex < smallCount; smallIndex++)
            {
                FillSmallBasePaddedInput(
                    inputScratch, ref large, largeCount,
                    outputStart, count, smallIndex);
                Vector256<int> source = Vector256.LoadUnsafe(ref inputRef).AsInt32();
                sum = Avx2.Add(
                    sum,
                    Avx2.MultiplyLow(
                        source,
                        Vector256.Create((int)Unsafe.Add(ref small, smallIndex))));
            }

            sum.AsUInt32().StoreUnsafe(ref resultRef);
            for (int lane = 0; lane < count; lane++)
                Unsafe.Add(ref destination, outputStart + lane) = resultScratch[lane];
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void FillSmallBaseCoefficientPaddedSse(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        if (Sse41.IsSupported)
        {
            const int Width = 4;
            Span<uint> inputScratch = stackalloc uint[Width];
            Span<uint> resultScratch = stackalloc uint[Width];
            ref uint inputRef = ref MemoryMarshal.GetReference(inputScratch);
            ref uint resultRef = ref MemoryMarshal.GetReference(resultScratch);

            for (int outputStart = start; outputStart < end; outputStart += Width)
            {
                int count = Math.Min(Width, end - outputStart);
                Vector128<int> sum = Vector128<int>.Zero;
                for (int smallIndex = 0; smallIndex < smallCount; smallIndex++)
                {
                    FillSmallBasePaddedInput(
                        inputScratch, ref large, largeCount,
                        outputStart, count, smallIndex);
                    Vector128<int> source = Vector128.LoadUnsafe(ref inputRef).AsInt32();
                    sum = Sse2.Add(
                        sum,
                        Sse41.MultiplyLow(
                            source,
                            Vector128.Create((int)Unsafe.Add(ref small, smallIndex))));
                }

                sum.AsUInt32().StoreUnsafe(ref resultRef);
                for (int lane = 0; lane < count; lane++)
                    Unsafe.Add(ref destination, outputStart + lane) = resultScratch[lane];
            }
        }
        else
        {
            const int Width = 2;
            Span<uint> inputScratch = stackalloc uint[Width];
            Span<ulong> resultScratch = stackalloc ulong[Width];
            ref ulong resultRef = ref MemoryMarshal.GetReference(resultScratch);

            for (int outputStart = start; outputStart < end; outputStart += Width)
            {
                int count = Math.Min(Width, end - outputStart);
                Vector128<ulong> sum = Vector128<ulong>.Zero;
                for (int smallIndex = 0; smallIndex < smallCount; smallIndex++)
                {
                    FillSmallBasePaddedInput(
                        inputScratch, ref large, largeCount,
                        outputStart, count, smallIndex);
                    Vector128<uint> widened = Vector128.Create(
                        inputScratch[0], 0u, inputScratch[1], 0u);
                    Vector128<ulong> product =
                        Sse2.Multiply(
                            widened,
                            Vector128.Create(Unsafe.Add(ref small, smallIndex)));
                    sum = Sse2.Add(sum.AsInt64(), product.AsInt64()).AsUInt64();
                }

                sum.StoreUnsafe(ref resultRef);
                for (int lane = 0; lane < count; lane++)
                    Unsafe.Add(ref destination, outputStart + lane) = resultScratch[lane];
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

#if ANDROID
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void FillSmallBaseCoefficientPaddedNeon(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        const int Width = 4;
        Span<uint> inputScratch = stackalloc uint[Width];
        Span<uint> resultScratch = stackalloc uint[Width];
        ref uint inputRef = ref MemoryMarshal.GetReference(inputScratch);
        ref uint resultRef = ref MemoryMarshal.GetReference(resultScratch);

        for (int outputStart = start; outputStart < end; outputStart += Width)
        {
            int count = Math.Min(Width, end - outputStart);
            Vector128<uint> sum = Vector128<uint>.Zero;
            for (int smallIndex = 0; smallIndex < smallCount; smallIndex++)
            {
                FillSmallBasePaddedInput(
                    inputScratch, ref large, largeCount,
                    outputStart, count, smallIndex);
                Vector128<uint> source = Vector128.LoadUnsafe(ref inputRef);
                sum = AdvSimd.Add(
                    sum,
                    AdvSimd.Multiply(
                        source,
                        Vector128.Create(Unsafe.Add(ref small, smallIndex))));
            }

            sum.StoreUnsafe(ref resultRef);
            for (int lane = 0; lane < count; lane++)
                Unsafe.Add(ref destination, outputStart + lane) = resultScratch[lane];
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ComputeSmallBaseCoefficient(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        int outputIndex)
    {
        int firstSmallIndex =
            Math.Max(0, outputIndex - (largeCount - 1));
        int lastSmallIndex =
            Math.Min(smallCount - 1, outputIndex);
        ulong sum = 0;

        for (int smallIndex = firstSmallIndex;
             smallIndex <= lastSmallIndex;
             smallIndex++)
        {
            sum +=
                (ulong)Unsafe.Add(ref small, smallIndex) *
                Unsafe.Add(ref large, outputIndex - smallIndex);
        }

        return sum;
    }

    private static void FillSmallBaseCoefficientRangeScalar(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        for (int outputIndex = start;
             outputIndex < end;
             outputIndex++)
        {
            if ((outputIndex & 0xFFFF) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            Unsafe.Add(ref destination, outputIndex) =
                ComputeSmallBaseCoefficient(
                    ref small,
                    smallCount,
                    ref large,
                    largeCount,
                    outputIndex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> MultiplyTwoBaseLimbsSse(
        ref uint source,
        int sourceIndex,
        uint multiplier)
    {
        Vector128<uint> widenedLayout = Vector128.Create(
            Unsafe.Add(ref source, sourceIndex),
            0u,
            Unsafe.Add(ref source, sourceIndex + 1),
            0u);
        return Sse2.Multiply(
            widenedLayout,
            Vector128.Create(multiplier));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreFourUInt32AsUInt64Sse(
        Vector128<uint> value,
        ref ulong destination,
        int destinationIndex)
    {
        Vector128<uint> zero = Vector128<uint>.Zero;
        Sse2.UnpackLow(value, zero).AsUInt64().StoreUnsafe(
            ref destination, (nuint)destinationIndex);
        Sse2.UnpackHigh(value, zero).AsUInt64().StoreUnsafe(
            ref destination, (nuint)(destinationIndex + 2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreEightUInt32AsUInt64Avx2(
        Vector256<uint> value,
        ref ulong destination,
        int destinationIndex)
    {
        Vector256<ulong> low =
            Avx2.ConvertToVector256Int64(value.GetLower().AsInt32()).AsUInt64();
        Vector256<ulong> high =
            Avx2.ConvertToVector256Int64(value.GetUpper().AsInt32()).AsUInt64();
        low.StoreUnsafe(ref destination, (nuint)destinationIndex);
        high.StoreUnsafe(ref destination, (nuint)(destinationIndex + 4));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreSixteenUInt32AsUInt64Avx512(
        Vector512<uint> value,
        ref ulong destination,
        int destinationIndex)
    {
        Vector256<uint> low = value.GetLower();
        Vector256<uint> high = value.GetUpper();

        Avx2.ConvertToVector256Int64(low.GetLower().AsInt32()).AsUInt64().StoreUnsafe(
            ref destination, (nuint)destinationIndex);
        Avx2.ConvertToVector256Int64(low.GetUpper().AsInt32()).AsUInt64().StoreUnsafe(
            ref destination, (nuint)(destinationIndex + 4));
        Avx2.ConvertToVector256Int64(high.GetLower().AsInt32()).AsUInt64().StoreUnsafe(
            ref destination, (nuint)(destinationIndex + 8));
        Avx2.ConvertToVector256Int64(high.GetUpper().AsInt32()).AsUInt64().StoreUnsafe(
            ref destination, (nuint)(destinationIndex + 12));
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void FillSmallBaseCoefficientRangeAvx512(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        const int Width = 16;
        int index = start;
        int vectorEnd = end - Width + 1;

        Vector512<uint> m0 = Vector512.Create(Unsafe.Add(ref small, 0));
        Vector512<uint> m1 = smallCount > 1 ? Vector512.Create(Unsafe.Add(ref small, 1)) : Vector512<uint>.Zero;
        Vector512<uint> m2 = smallCount > 2 ? Vector512.Create(Unsafe.Add(ref small, 2)) : Vector512<uint>.Zero;
        Vector512<uint> m3 = smallCount > 3 ? Vector512.Create(Unsafe.Add(ref small, 3)) : Vector512<uint>.Zero;
        Vector512<uint> m4 = smallCount > 4 ? Vector512.Create(Unsafe.Add(ref small, 4)) : Vector512<uint>.Zero;

        for (; index < vectorEnd; index += Width)
        {
            if ((index & 0xFFFF) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            Vector512<uint> sum = Avx512F.MultiplyLow(
                Vector512.LoadUnsafe(ref large, (nuint)index), m0);
            if (smallCount > 1)
                sum = Vector512.Add(sum, Avx512F.MultiplyLow(
                    Vector512.LoadUnsafe(ref large, (nuint)(index - 1)), m1));
            if (smallCount > 2)
                sum = Vector512.Add(sum, Avx512F.MultiplyLow(
                    Vector512.LoadUnsafe(ref large, (nuint)(index - 2)), m2));
            if (smallCount > 3)
                sum = Vector512.Add(sum, Avx512F.MultiplyLow(
                    Vector512.LoadUnsafe(ref large, (nuint)(index - 3)), m3));
            if (smallCount > 4)
                sum = Vector512.Add(sum, Avx512F.MultiplyLow(
                    Vector512.LoadUnsafe(ref large, (nuint)(index - 4)), m4));

            StoreSixteenUInt32AsUInt64Avx512(sum, ref destination, index);
        }

        if (index < end)
        {
            FillSmallBaseCoefficientPaddedAvx512(
                ref small, smallCount, ref large, largeCount,
                ref destination, index, end, cancellationToken);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void FillSmallBaseCoefficientRangeAvx2(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        const int Width = 8;
        int index = start;
        int vectorEnd = end - Width + 1;

        Vector256<int> m0 = Vector256.Create((int)Unsafe.Add(ref small, 0));
        Vector256<int> m1 = smallCount > 1 ? Vector256.Create((int)Unsafe.Add(ref small, 1)) : Vector256<int>.Zero;
        Vector256<int> m2 = smallCount > 2 ? Vector256.Create((int)Unsafe.Add(ref small, 2)) : Vector256<int>.Zero;
        Vector256<int> m3 = smallCount > 3 ? Vector256.Create((int)Unsafe.Add(ref small, 3)) : Vector256<int>.Zero;
        Vector256<int> m4 = smallCount > 4 ? Vector256.Create((int)Unsafe.Add(ref small, 4)) : Vector256<int>.Zero;

        for (; index < vectorEnd; index += Width)
        {
            if ((index & 0xFFFF) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            Vector256<int> sum = Avx2.MultiplyLow(
                Vector256.LoadUnsafe(ref large, (nuint)index).AsInt32(), m0);
            if (smallCount > 1)
                sum = Avx2.Add(sum, Avx2.MultiplyLow(
                    Vector256.LoadUnsafe(ref large, (nuint)(index - 1)).AsInt32(), m1));
            if (smallCount > 2)
                sum = Avx2.Add(sum, Avx2.MultiplyLow(
                    Vector256.LoadUnsafe(ref large, (nuint)(index - 2)).AsInt32(), m2));
            if (smallCount > 3)
                sum = Avx2.Add(sum, Avx2.MultiplyLow(
                    Vector256.LoadUnsafe(ref large, (nuint)(index - 3)).AsInt32(), m3));
            if (smallCount > 4)
                sum = Avx2.Add(sum, Avx2.MultiplyLow(
                    Vector256.LoadUnsafe(ref large, (nuint)(index - 4)).AsInt32(), m4));

            StoreEightUInt32AsUInt64Avx2(sum.AsUInt32(), ref destination, index);
        }

        if (index < end)
        {
            FillSmallBaseCoefficientPaddedAvx2(
                ref small, smallCount, ref large, largeCount,
                ref destination, index, end, cancellationToken);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void FillSmallBaseCoefficientRangeSse(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        int index = start;

        if (Sse41.IsSupported)
        {
            const int Width = 4;
            int vectorEnd = end - Width + 1;
            Vector128<int> m0 = Vector128.Create((int)Unsafe.Add(ref small, 0));
            Vector128<int> m1 = smallCount > 1 ? Vector128.Create((int)Unsafe.Add(ref small, 1)) : Vector128<int>.Zero;
            Vector128<int> m2 = smallCount > 2 ? Vector128.Create((int)Unsafe.Add(ref small, 2)) : Vector128<int>.Zero;
            Vector128<int> m3 = smallCount > 3 ? Vector128.Create((int)Unsafe.Add(ref small, 3)) : Vector128<int>.Zero;
            Vector128<int> m4 = smallCount > 4 ? Vector128.Create((int)Unsafe.Add(ref small, 4)) : Vector128<int>.Zero;

            for (; index < vectorEnd; index += Width)
            {
                if ((index & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                Vector128<int> sum = Sse41.MultiplyLow(
                    Vector128.LoadUnsafe(ref large, (nuint)index).AsInt32(), m0);
                if (smallCount > 1)
                    sum = Sse2.Add(sum, Sse41.MultiplyLow(
                        Vector128.LoadUnsafe(ref large, (nuint)(index - 1)).AsInt32(), m1));
                if (smallCount > 2)
                    sum = Sse2.Add(sum, Sse41.MultiplyLow(
                        Vector128.LoadUnsafe(ref large, (nuint)(index - 2)).AsInt32(), m2));
                if (smallCount > 3)
                    sum = Sse2.Add(sum, Sse41.MultiplyLow(
                        Vector128.LoadUnsafe(ref large, (nuint)(index - 3)).AsInt32(), m3));
                if (smallCount > 4)
                    sum = Sse2.Add(sum, Sse41.MultiplyLow(
                        Vector128.LoadUnsafe(ref large, (nuint)(index - 4)).AsInt32(), m4));

                StoreFourUInt32AsUInt64Sse(sum.AsUInt32(), ref destination, index);
            }
        }
        else
        {
            const int Width = 2;
            int vectorEnd = end - Width + 1;
            for (; index < vectorEnd; index += Width)
            {
                if ((index & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                Vector128<ulong> sum =
                    MultiplyTwoBaseLimbsSse(ref large, index, Unsafe.Add(ref small, 0));
                if (smallCount > 1)
                    sum = Sse2.Add(sum.AsInt64(), MultiplyTwoBaseLimbsSse(
                        ref large, index - 1, Unsafe.Add(ref small, 1)).AsInt64()).AsUInt64();
                if (smallCount > 2)
                    sum = Sse2.Add(sum.AsInt64(), MultiplyTwoBaseLimbsSse(
                        ref large, index - 2, Unsafe.Add(ref small, 2)).AsInt64()).AsUInt64();
                if (smallCount > 3)
                    sum = Sse2.Add(sum.AsInt64(), MultiplyTwoBaseLimbsSse(
                        ref large, index - 3, Unsafe.Add(ref small, 3)).AsInt64()).AsUInt64();
                if (smallCount > 4)
                    sum = Sse2.Add(sum.AsInt64(), MultiplyTwoBaseLimbsSse(
                        ref large, index - 4, Unsafe.Add(ref small, 4)).AsInt64()).AsUInt64();

                sum.StoreUnsafe(ref destination, (nuint)index);
            }
        }

        if (index < end)
        {
            FillSmallBaseCoefficientPaddedSse(
                ref small, smallCount, ref large, largeCount,
                ref destination, index, end, cancellationToken);
        }
    }

#if ANDROID
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void FillSmallBaseCoefficientRangeNeon(
        ref uint small,
        int smallCount,
        ref uint large,
        int largeCount,
        ref ulong destination,
        int start,
        int end,
        CancellationToken cancellationToken)
    {
        const int Width = 4;
        int index = start;
        int vectorEnd = end - Width + 1;
        Vector128<uint> m0 = Vector128.Create(Unsafe.Add(ref small, 0));
        Vector128<uint> m1 = smallCount > 1 ? Vector128.Create(Unsafe.Add(ref small, 1)) : Vector128<uint>.Zero;
        Vector128<uint> m2 = smallCount > 2 ? Vector128.Create(Unsafe.Add(ref small, 2)) : Vector128<uint>.Zero;
        Vector128<uint> m3 = smallCount > 3 ? Vector128.Create(Unsafe.Add(ref small, 3)) : Vector128<uint>.Zero;
        Vector128<uint> m4 = smallCount > 4 ? Vector128.Create(Unsafe.Add(ref small, 4)) : Vector128<uint>.Zero;
        Vector64<uint> one = Vector64.Create(1u);

        for (; index < vectorEnd; index += Width)
        {
            if ((index & 0xFFFF) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            Vector128<uint> sum = AdvSimd.Multiply(
                Vector128.LoadUnsafe(ref large, (nuint)index), m0);
            if (smallCount > 1)
                sum = AdvSimd.Add(sum, AdvSimd.Multiply(
                    Vector128.LoadUnsafe(ref large, (nuint)(index - 1)), m1));
            if (smallCount > 2)
                sum = AdvSimd.Add(sum, AdvSimd.Multiply(
                    Vector128.LoadUnsafe(ref large, (nuint)(index - 2)), m2));
            if (smallCount > 3)
                sum = AdvSimd.Add(sum, AdvSimd.Multiply(
                    Vector128.LoadUnsafe(ref large, (nuint)(index - 3)), m3));
            if (smallCount > 4)
                sum = AdvSimd.Add(sum, AdvSimd.Multiply(
                    Vector128.LoadUnsafe(ref large, (nuint)(index - 4)), m4));

            Vector128<ulong> low = AdvSimd.MultiplyWideningLower(sum.GetLower(), one);
            Vector128<ulong> high = AdvSimd.MultiplyWideningLower(sum.GetUpper(), one);
            low.StoreUnsafe(ref destination, (nuint)index);
            high.StoreUnsafe(ref destination, (nuint)(index + 2));
        }

        if (index < end)
        {
            FillSmallBaseCoefficientPaddedNeon(
                ref small, smallCount, ref large, largeCount,
                ref destination, index, end, cancellationToken);
        }
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ParallelBigUnsigned CreateFromSmallBaseCoefficientsSimd(
        ulong[] coefficients,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        long carryStarted =
            Stopwatch.GetTimestamp();

        var limbs =
            new uint[coefficients.Length + 8];

        // Share the production carry dispatcher with the generic schoolbook
        // path.  AVX2 / SSE2+ / NEON now use the same exact packed
        // base-10,000 quotient/remainder decomposition as CRT carry; only
        // the ordered dependency reconciliation remains scalar.
        ulong carry =
            NormalizeCoefficientCarryBlockParallel(
                coefficients,
                0,
                limbs,
                0,
                coefficients.Length,
                0,
                workers,
                cancellationToken);

        int limbCount =
            coefficients.Length;

        while (carry > 0)
        {
            ulong quotient =
                carry / LimbBase;

            limbs[limbCount++] =
                (uint)(carry - quotient * LimbBase);

            carry =
                quotient;
        }

        // The highest convolution coefficients can normalize to zero. Match
        // the canonical representation produced by the scalar constructor.
        while (limbCount > 1 && limbs[limbCount - 1] == 0)
            limbCount--;

        if (limbCount != limbs.Length)
            Array.Resize(ref limbs, limbCount);

        diagnostics.CarryTicks +=
            Stopwatch.GetTimestamp() -
            carryStarted;

        return new ParallelBigUnsigned(
            limbs,
            takeOwnership: true);
    }
}
