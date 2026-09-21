using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
#if ANDROID
using System.Runtime.Intrinsics.Arm;
#endif
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    /// <summary>
    /// Inverse-DIT two-stage fusion for both worker schedules.  Stage S and its parent 2S are
    /// completed while four quarter streams are resident in vector registers.
    /// When <paramref name="normalizeFinal"/> is true the parent is the final
    /// NTT stage and the normalized result is written directly to output.
    /// Persistent teams retain static dispatch over vector-aligned slices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseUncachedStagePairSegmented(
        uint[] values,
        uint[] output,
        int validOutputLength,
        uint modulus,
        uint inversePrimitiveRoot,
        uint inverseLength,
        uint inverseLengthShoup,
        int stageLength,
        bool normalizeFinal,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int parentLength = checked(stageLength << 1);
        int halfLength = stageLength >> 1;
        int parentCount = values.Length / parentLength;
        Debug.Assert(parentCount > 0);
        Debug.Assert(!normalizeFinal || parentLength == values.Length);

        uint firstRoot = (uint)ModPow(
            inversePrimitiveRoot,
            (modulus - 1u) / (uint)stageLength,
            modulus);
        uint secondRoot = (uint)ModPow(
            inversePrimitiveRoot,
            (modulus - 1u) / (uint)parentLength,
            modulus);
        uint secondPhase = (uint)ModPow(secondRoot, (uint)halfLength, modulus);

        int width =
            ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512InverseGlobal) && Avx512F.IsSupported)
                ? Vector512<uint>.Count
                : (workers.UseAvx2Ntt && Avx2.IsSupported)
                    ? Vector256<uint>.Count
                    : (workers.UseSseNtt && Sse2.IsSupported)
                        ? Vector128<uint>.Count
#if ANDROID
                        : (workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated))
                            ? Vector128<uint>.Count
#endif
                            : 1;

        if (width <= 1 || halfLength < width || (halfLength % width) != 0)
            throw new InvalidOperationException("Inverse stage-pair SIMD fusion requires a vector-aligned half stage.");

        int segmentsPerParent = GetFusionAlignedSegmentsPerGroup(
            halfLength, parentCount, workers, width);

        ExecuteRanges(
            checked(parentCount * segmentsPerParent),
            workers,
            cancellationToken,
            (segmentStart, segmentEnd) =>
            {
                for (int segmentIndex = segmentStart; segmentIndex < segmentEnd; segmentIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    GetFusionAlignedSegmentBounds(
                        segmentIndex,
                        segmentsPerParent,
                        halfLength,
                        width,
                        workers,
                        out int parentIndex,
                        out int first,
                        out int last);

                    if ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512InverseGlobal) && Avx512F.IsSupported)
                    {
                        ProcessInverseUncachedStagePairAvx512(
                            values, output, validOutputLength, modulus,
                            firstRoot, secondRoot, secondPhase,
                            inverseLength, inverseLengthShoup,
                            stageLength, parentIndex, first, last,
                            normalizeFinal, cancellationToken);
                    }
                    else if (workers.UseAvx2Ntt && Avx2.IsSupported)
                    {
                        ProcessInverseUncachedStagePairAvx2(
                            values, output, validOutputLength, modulus,
                            firstRoot, secondRoot, secondPhase,
                            inverseLength, inverseLengthShoup,
                            stageLength, parentIndex, first, last,
                            normalizeFinal, cancellationToken);
                    }
                    else if (workers.UseSseNtt && Sse2.IsSupported)
                    {
                        ProcessInverseUncachedStagePairSse(
                            values, output, validOutputLength, modulus,
                            firstRoot, secondRoot, secondPhase,
                            inverseLength, inverseLengthShoup,
                            stageLength, parentIndex, first, last,
                            normalizeFinal, cancellationToken);
                    }
#if ANDROID
                    else if (workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated))
                    {
                        ProcessInverseUncachedStagePairNeon(
                            values, output, validOutputLength, modulus,
                            firstRoot, secondRoot, secondPhase,
                            inverseLength, inverseLengthShoup,
                            stageLength, parentIndex, first, last,
                            normalizeFinal, cancellationToken);
                    }
#endif
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreFinalAvx512(
        Vector512<uint> value, uint[] output, int index, int validOutputLength)
    {
        if (index >= validOutputLength) return;
        ref uint outputRef = ref MemoryMarshal.GetArrayDataReference(output);
        if (index + Vector512<uint>.Count <= validOutputLength)
        {
            value.StoreUnsafe(ref outputRef, (nuint)index);
            return;
        }

        Span<uint> scratch = stackalloc uint[Vector512<uint>.Count];
        ref uint scratchRef = ref MemoryMarshal.GetReference(scratch);
        value.StoreUnsafe(ref scratchRef);
        int remaining = validOutputLength - index;
        for (int lane = 0; lane < remaining; lane++)
            output[index + lane] = scratch[lane];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreFinalAvx2(
        Vector256<uint> value, uint[] output, int index, int validOutputLength)
    {
        if (index >= validOutputLength) return;
        ref uint outputRef = ref MemoryMarshal.GetArrayDataReference(output);
        if (index + Vector256<uint>.Count <= validOutputLength)
        {
            value.StoreUnsafe(ref outputRef, (nuint)index);
            return;
        }

        Span<uint> scratch = stackalloc uint[Vector256<uint>.Count];
        ref uint scratchRef = ref MemoryMarshal.GetReference(scratch);
        value.StoreUnsafe(ref scratchRef);
        int remaining = validOutputLength - index;
        for (int lane = 0; lane < remaining; lane++)
            output[index + lane] = scratch[lane];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreFinal128(
        Vector128<uint> value, uint[] output, int index, int validOutputLength)
    {
        if (index >= validOutputLength) return;
        ref uint outputRef = ref MemoryMarshal.GetArrayDataReference(output);
        if (index + Vector128<uint>.Count <= validOutputLength)
        {
            value.StoreUnsafe(ref outputRef, (nuint)index);
            return;
        }

        Span<uint> scratch = stackalloc uint[Vector128<uint>.Count];
        ref uint scratchRef = ref MemoryMarshal.GetReference(scratch);
        value.StoreUnsafe(ref scratchRef);
        int remaining = validOutputLength - index;
        for (int lane = 0; lane < remaining; lane++)
            output[index + lane] = scratch[lane];
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessInverseUncachedStagePairAvx512(
        uint[] values, uint[] output, int validOutputLength, uint modulus,
        uint firstRoot, uint secondRoot, uint secondPhase,
        uint inverseLength, uint inverseLengthShoup,
        int stageLength, int parentIndex, int first, int last,
        bool normalizeFinal, CancellationToken cancellationToken)
    {
        const int Width = 16;
        int halfLength = stageLength >> 1;
        int parentOffset = parentIndex * (stageLength << 1);
        var context = new Avx512NttModContext(modulus);
        Vector512<uint> t1 = CreateTwiddleSequenceAvx512(firstRoot, first, modulus);
        Vector512<uint> t20 = CreateTwiddleSequenceAvx512(secondRoot, first, modulus);
        Vector512<uint> t21 = MultiplyResiduesAvx512(t20, Vector512.Create(secondPhase), modulus);
        uint step1 = (uint)ModPow(firstRoot, Width, modulus);
        uint step2 = (uint)ModPow(secondRoot, Width, modulus);
        Vector512<uint> advance1 = Vector512.Create(step1);
        Vector512<uint> advance2 = Vector512.Create(step2);
        Vector512<uint> inv = Vector512.Create(inverseLength);
        Vector512<uint> invShoup = Vector512.Create(inverseLengthShoup);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

        for (int i = first; i < last; i += Width)
        {
            int index0 = parentOffset + i;
            int index1 = index0 + halfLength;
            int index2 = index0 + stageLength;
            int index3 = index2 + halfLength;
            Vector512<uint> a = Vector512.LoadUnsafe(ref data, (nuint)index0);
            Vector512<uint> b = Vector512.LoadUnsafe(ref data, (nuint)index1);
            Vector512<uint> c = Vector512.LoadUnsafe(ref data, (nuint)index2);
            Vector512<uint> d = Vector512.LoadUnsafe(ref data, (nuint)index3);

            Vector512<uint> br = MultiplyResiduesAvx512(b, t1, modulus);
            Vector512<uint> dr = MultiplyResiduesAvx512(d, t1, modulus);
            Vector512<uint> s0 = AddModuloAvx512(a, br, context);
            Vector512<uint> q0 = SubtractModuloAvx512(a, br, context);
            Vector512<uint> s1 = AddModuloAvx512(c, dr, context);
            Vector512<uint> q1 = SubtractModuloAvx512(c, dr, context);
            Vector512<uint> m0 = MultiplyResiduesAvx512(s1, t20, modulus);
            Vector512<uint> m1 = MultiplyResiduesAvx512(q1, t21, modulus);
            Vector512<uint> o0 = AddModuloAvx512(s0, m0, context);
            Vector512<uint> o1 = AddModuloAvx512(q0, m1, context);
            Vector512<uint> o2 = SubtractModuloAvx512(s0, m0, context);
            Vector512<uint> o3 = SubtractModuloAvx512(q0, m1, context);

            if (normalizeFinal)
            {
                o0 = MultiplyShoupAvx512(o0, inv, invShoup, context);
                o1 = MultiplyShoupAvx512(o1, inv, invShoup, context);
                o2 = MultiplyShoupAvx512(o2, inv, invShoup, context);
                o3 = MultiplyShoupAvx512(o3, inv, invShoup, context);
                StoreFinalAvx512(o0, output, index0, validOutputLength);
                StoreFinalAvx512(o1, output, index1, validOutputLength);
                StoreFinalAvx512(o2, output, index2, validOutputLength);
                StoreFinalAvx512(o3, output, index3, validOutputLength);
            }
            else
            {
                o0.StoreUnsafe(ref data, (nuint)index0);
                o1.StoreUnsafe(ref data, (nuint)index1);
                o2.StoreUnsafe(ref data, (nuint)index2);
                o3.StoreUnsafe(ref data, (nuint)index3);
            }

            if (i + Width < last)
            {
                t1 = MultiplyResiduesAvx512(t1, advance1, modulus);
                t20 = MultiplyResiduesAvx512(t20, advance2, modulus);
                t21 = MultiplyResiduesAvx512(t21, advance2, modulus);
            }
            if (((i - first) & 0x3FFF) == 0x3FF0)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessInverseUncachedStagePairAvx2(
        uint[] values, uint[] output, int validOutputLength, uint modulus,
        uint firstRoot, uint secondRoot, uint secondPhase,
        uint inverseLength, uint inverseLengthShoup,
        int stageLength, int parentIndex, int first, int last,
        bool normalizeFinal, CancellationToken cancellationToken)
    {
        const int Width = 8;
        int halfLength = stageLength >> 1;
        int parentOffset = parentIndex * (stageLength << 1);
        var context = new Avx2NttModContext(modulus);
        Vector256<uint> t1 = CreateTwiddleSequenceAvx2(firstRoot, first, modulus);
        Vector256<uint> t20 = CreateTwiddleSequenceAvx2(secondRoot, first, modulus);
        Vector256<uint> t21 = MultiplyResiduesAvx2(t20, Vector256.Create(secondPhase), modulus);
        Vector256<uint> advance1 = Vector256.Create((uint)ModPow(firstRoot, Width, modulus));
        Vector256<uint> advance2 = Vector256.Create((uint)ModPow(secondRoot, Width, modulus));
        Vector256<uint> inv = Vector256.Create(inverseLength);
        Vector256<uint> invShoup = Vector256.Create(inverseLengthShoup);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

        for (int i = first; i < last; i += Width)
        {
            int index0 = parentOffset + i;
            int index1 = index0 + halfLength;
            int index2 = index0 + stageLength;
            int index3 = index2 + halfLength;
            Vector256<uint> a = Vector256.LoadUnsafe(ref data, (nuint)index0);
            Vector256<uint> b = Vector256.LoadUnsafe(ref data, (nuint)index1);
            Vector256<uint> c = Vector256.LoadUnsafe(ref data, (nuint)index2);
            Vector256<uint> d = Vector256.LoadUnsafe(ref data, (nuint)index3);
            Vector256<uint> br = MultiplyResiduesAvx2(b, t1, modulus);
            Vector256<uint> dr = MultiplyResiduesAvx2(d, t1, modulus);
            Vector256<uint> s0 = AddModuloAvx2(a, br, context);
            Vector256<uint> q0 = SubtractModuloAvx2(a, br, context);
            Vector256<uint> s1 = AddModuloAvx2(c, dr, context);
            Vector256<uint> q1 = SubtractModuloAvx2(c, dr, context);
            Vector256<uint> m0 = MultiplyResiduesAvx2(s1, t20, modulus);
            Vector256<uint> m1 = MultiplyResiduesAvx2(q1, t21, modulus);
            Vector256<uint> o0 = AddModuloAvx2(s0, m0, context);
            Vector256<uint> o1 = AddModuloAvx2(q0, m1, context);
            Vector256<uint> o2 = SubtractModuloAvx2(s0, m0, context);
            Vector256<uint> o3 = SubtractModuloAvx2(q0, m1, context);

            if (normalizeFinal)
            {
                o0 = MultiplyShoupAvx2(o0, inv, invShoup, context);
                o1 = MultiplyShoupAvx2(o1, inv, invShoup, context);
                o2 = MultiplyShoupAvx2(o2, inv, invShoup, context);
                o3 = MultiplyShoupAvx2(o3, inv, invShoup, context);
                StoreFinalAvx2(o0, output, index0, validOutputLength);
                StoreFinalAvx2(o1, output, index1, validOutputLength);
                StoreFinalAvx2(o2, output, index2, validOutputLength);
                StoreFinalAvx2(o3, output, index3, validOutputLength);
            }
            else
            {
                o0.StoreUnsafe(ref data, (nuint)index0);
                o1.StoreUnsafe(ref data, (nuint)index1);
                o2.StoreUnsafe(ref data, (nuint)index2);
                o3.StoreUnsafe(ref data, (nuint)index3);
            }

            if (i + Width < last)
            {
                t1 = MultiplyResiduesAvx2(t1, advance1, modulus);
                t20 = MultiplyResiduesAvx2(t20, advance2, modulus);
                t21 = MultiplyResiduesAvx2(t21, advance2, modulus);
            }
            if (((i - first) & 0x3FFF) == 0x3FF8)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessInverseUncachedStagePairSse(
        uint[] values, uint[] output, int validOutputLength, uint modulus,
        uint firstRoot, uint secondRoot, uint secondPhase,
        uint inverseLength, uint inverseLengthShoup,
        int stageLength, int parentIndex, int first, int last,
        bool normalizeFinal, CancellationToken cancellationToken)
    {
        const int Width = 4;
        int halfLength = stageLength >> 1;
        int parentOffset = parentIndex * (stageLength << 1);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> t1 = CreateTwiddleSequenceSse(firstRoot, first, modulus);
        Vector128<uint> t20 = CreateTwiddleSequenceSse(secondRoot, first, modulus);
        Vector128<uint> t21 = MultiplyResiduesSse(t20, Vector128.Create(secondPhase), modulus);
        // The stride factors are invariant for this segment. Build their
        // Shoup companions once instead of reducing three general products
        // on every four-butterfly iteration.
        uint step1 = (uint)ModPow(firstRoot, Width, modulus);
        uint step2 = (uint)ModPow(secondRoot, Width, modulus);
        Vector128<uint> advance1 = Vector128.Create(step1);
        Vector128<uint> advance2 = Vector128.Create(step2);
        Vector128<uint> advanceShoup1 = Vector128.Create((uint)(((ulong)step1 << 32) / modulus));
        Vector128<uint> advanceShoup2 = Vector128.Create((uint)(((ulong)step2 << 32) / modulus));
        Vector128<uint> inv = Vector128.Create(inverseLength);
        Vector128<uint> invShoup = Vector128.Create(inverseLengthShoup);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

        for (int i = first; i < last; i += Width)
        {
            int index0 = parentOffset + i;
            int index1 = index0 + halfLength;
            int index2 = index0 + stageLength;
            int index3 = index2 + halfLength;
            Vector128<uint> a = Vector128.LoadUnsafe(ref data, (nuint)index0);
            Vector128<uint> b = Vector128.LoadUnsafe(ref data, (nuint)index1);
            Vector128<uint> c = Vector128.LoadUnsafe(ref data, (nuint)index2);
            Vector128<uint> d = Vector128.LoadUnsafe(ref data, (nuint)index3);
            Vector128<uint> br = MultiplyResiduesSse(b, t1, modulus);
            Vector128<uint> dr = MultiplyResiduesSse(d, t1, modulus);
            Vector128<uint> s0 = AddModuloSse(a, br, mod);
            Vector128<uint> q0 = SubtractModuloSse(a, br, mod);
            Vector128<uint> s1 = AddModuloSse(c, dr, mod);
            Vector128<uint> q1 = SubtractModuloSse(c, dr, mod);
            Vector128<uint> m0 = MultiplyResiduesSse(s1, t20, modulus);
            Vector128<uint> m1 = MultiplyResiduesSse(q1, t21, modulus);
            Vector128<uint> o0 = AddModuloSse(s0, m0, mod);
            Vector128<uint> o1 = AddModuloSse(q0, m1, mod);
            Vector128<uint> o2 = SubtractModuloSse(s0, m0, mod);
            Vector128<uint> o3 = SubtractModuloSse(q0, m1, mod);

            if (normalizeFinal)
            {
                o0 = MultiplyShoupSse(o0, inv, invShoup, mod);
                o1 = MultiplyShoupSse(o1, inv, invShoup, mod);
                o2 = MultiplyShoupSse(o2, inv, invShoup, mod);
                o3 = MultiplyShoupSse(o3, inv, invShoup, mod);
                StoreFinal128(o0, output, index0, validOutputLength);
                StoreFinal128(o1, output, index1, validOutputLength);
                StoreFinal128(o2, output, index2, validOutputLength);
                StoreFinal128(o3, output, index3, validOutputLength);
            }
            else
            {
                o0.StoreUnsafe(ref data, (nuint)index0);
                o1.StoreUnsafe(ref data, (nuint)index1);
                o2.StoreUnsafe(ref data, (nuint)index2);
                o3.StoreUnsafe(ref data, (nuint)index3);
            }

            if (i + Width < last)
            {
                t1 = MultiplyShoupSse(t1, advance1, advanceShoup1, mod);
                t20 = MultiplyShoupSse(t20, advance2, advanceShoup2, mod);
                t21 = MultiplyShoupSse(t21, advance2, advanceShoup2, mod);
            }
            if (((i - first) & 0x3FFF) == 0x3FFC)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }

#if ANDROID
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessInverseUncachedStagePairNeon(
        uint[] values, uint[] output, int validOutputLength, uint modulus,
        uint firstRoot, uint secondRoot, uint secondPhase,
        uint inverseLength, uint inverseLengthShoup,
        int stageLength, int parentIndex, int first, int last,
        bool normalizeFinal, CancellationToken cancellationToken)
    {
        const int Width = 4;
        int halfLength = stageLength >> 1;
        int parentOffset = parentIndex * (stageLength << 1);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> t1 = CreateTwiddleSequenceNeon(firstRoot, first, modulus);
        Vector128<uint> t20 = CreateTwiddleSequenceNeon(secondRoot, first, modulus);
        Vector128<uint> t21 = MultiplyResiduesNeon(t20, Vector128.Create(secondPhase), modulus);
        // The stride factors are invariant for this segment. Build their
        // Shoup companions once instead of reducing three general products
        // on every four-butterfly iteration.
        uint step1 = (uint)ModPow(firstRoot, Width, modulus);
        uint step2 = (uint)ModPow(secondRoot, Width, modulus);
        Vector128<uint> advance1 = Vector128.Create(step1);
        Vector128<uint> advance2 = Vector128.Create(step2);
        Vector128<uint> advanceShoup1 = Vector128.Create((uint)(((ulong)step1 << 32) / modulus));
        Vector128<uint> advanceShoup2 = Vector128.Create((uint)(((ulong)step2 << 32) / modulus));
        Vector128<uint> inv = Vector128.Create(inverseLength);
        Vector128<uint> invShoup = Vector128.Create(inverseLengthShoup);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

        for (int i = first; i < last; i += Width)
        {
            int index0 = parentOffset + i;
            int index1 = index0 + halfLength;
            int index2 = index0 + stageLength;
            int index3 = index2 + halfLength;
            Vector128<uint> a = Vector128.LoadUnsafe(ref data, (nuint)index0);
            Vector128<uint> b = Vector128.LoadUnsafe(ref data, (nuint)index1);
            Vector128<uint> c = Vector128.LoadUnsafe(ref data, (nuint)index2);
            Vector128<uint> d = Vector128.LoadUnsafe(ref data, (nuint)index3);
            Vector128<uint> br = MultiplyResiduesNeon(b, t1, modulus);
            Vector128<uint> dr = MultiplyResiduesNeon(d, t1, modulus);
            Vector128<uint> s0 = AddModuloNeon(a, br, mod);
            Vector128<uint> q0 = SubtractModuloNeon(a, br, mod);
            Vector128<uint> s1 = AddModuloNeon(c, dr, mod);
            Vector128<uint> q1 = SubtractModuloNeon(c, dr, mod);
            Vector128<uint> m0 = MultiplyResiduesNeon(s1, t20, modulus);
            Vector128<uint> m1 = MultiplyResiduesNeon(q1, t21, modulus);
            Vector128<uint> o0 = AddModuloNeon(s0, m0, mod);
            Vector128<uint> o1 = AddModuloNeon(q0, m1, mod);
            Vector128<uint> o2 = SubtractModuloNeon(s0, m0, mod);
            Vector128<uint> o3 = SubtractModuloNeon(q0, m1, mod);

            if (normalizeFinal)
            {
                o0 = MultiplyShoupNeon(o0, inv, invShoup, mod);
                o1 = MultiplyShoupNeon(o1, inv, invShoup, mod);
                o2 = MultiplyShoupNeon(o2, inv, invShoup, mod);
                o3 = MultiplyShoupNeon(o3, inv, invShoup, mod);
                StoreFinal128(o0, output, index0, validOutputLength);
                StoreFinal128(o1, output, index1, validOutputLength);
                StoreFinal128(o2, output, index2, validOutputLength);
                StoreFinal128(o3, output, index3, validOutputLength);
            }
            else
            {
                o0.StoreUnsafe(ref data, (nuint)index0);
                o1.StoreUnsafe(ref data, (nuint)index1);
                o2.StoreUnsafe(ref data, (nuint)index2);
                o3.StoreUnsafe(ref data, (nuint)index3);
            }

            if (i + Width < last)
            {
                t1 = MultiplyShoupNeon(t1, advance1, advanceShoup1, mod);
                t20 = MultiplyShoupNeon(t20, advance2, advanceShoup2, mod);
                t21 = MultiplyShoupNeon(t21, advance2, advanceShoup2, mod);
            }
            if (((i - first) & 0x3FFF) == 0x3FFC)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanUseInverseStagePairSimd(FixedWorkerTeam workers, int halfLength)
    {
        int width =
            ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512InverseGlobal) && Avx512F.IsSupported)
                ? Vector512<uint>.Count
                : (workers.UseAvx2Ntt && Avx2.IsSupported)
                    ? Vector256<uint>.Count
                    : (workers.UseSseNtt && Sse2.IsSupported)
                        ? Vector128<uint>.Count
#if ANDROID
                        : (workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated))
                            ? Vector128<uint>.Count
#endif
                            : 1;
        return width > 1 && halfLength >= width && (halfLength % width) == 0;
    }

}
