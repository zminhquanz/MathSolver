using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectCachedTwiddleSimdWidth(FixedWorkerTeam workers)
    {
        if (workers.UseAvx512Ntt &&
            Avx512F.IsSupported &&
            Avx2.IsSupported &&
            Vector512.IsHardwareAccelerated)
        {
            return Vector512<uint>.Count;
        }

        if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            return Vector256<uint>.Count;
        }

        if (workers.UseSseNtt && Sse2.IsSupported)
        {
            return Vector128<uint>.Count;
        }

        if (workers.UseNeonNtt && AdvSimd.Arm64.IsSupported)
        {
            return Vector128<uint>.Count;
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildCachedTwiddleRangeSimd(
        uint[] forwardTwiddles,
        uint[] inverseTwiddles,
        uint[]? forwardShoupTwiddles,
        uint[]? inverseShoupTwiddles,
        int offset,
        int halfLength,
        int firstIndex,
        int endIndex,
        uint root,
        uint modulus,
        bool buildShoupCompanions,
        double shoupScale,
        uint advance,
        uint advanceShoup,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        if (workers.UseAvx512Ntt &&
            Avx512F.IsSupported &&
            Avx2.IsSupported &&
            Vector512.IsHardwareAccelerated)
        {
            BuildCachedTwiddleRangeAvx512(
                forwardTwiddles,
                inverseTwiddles,
                forwardShoupTwiddles,
                inverseShoupTwiddles,
                offset,
                halfLength,
                firstIndex,
                endIndex,
                root,
                modulus,
                buildShoupCompanions,
                shoupScale,
                advance,
                advanceShoup,
                cancellationToken);
            return;
        }

        if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            BuildCachedTwiddleRangeAvx2(
                forwardTwiddles,
                inverseTwiddles,
                forwardShoupTwiddles,
                inverseShoupTwiddles,
                offset,
                halfLength,
                firstIndex,
                endIndex,
                root,
                modulus,
                buildShoupCompanions,
                shoupScale,
                advance,
                advanceShoup,
                cancellationToken);
            return;
        }

        if (workers.UseSseNtt && Sse2.IsSupported)
        {
            BuildCachedTwiddleRangeSse(
                forwardTwiddles,
                inverseTwiddles,
                forwardShoupTwiddles,
                inverseShoupTwiddles,
                offset,
                halfLength,
                firstIndex,
                endIndex,
                root,
                modulus,
                buildShoupCompanions,
                shoupScale,
                advance,
                advanceShoup,
                cancellationToken);
            return;
        }

        BuildCachedTwiddleRangeNeon(
            forwardTwiddles,
            inverseTwiddles,
            forwardShoupTwiddles,
            inverseShoupTwiddles,
            offset,
            halfLength,
            firstIndex,
            endIndex,
            root,
            modulus,
            buildShoupCompanions,
            shoupScale,
            advance,
            advanceShoup,
            cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetTwiddleAtIndex(
        uint root,
        int index,
        uint modulus) =>
        index == 1
            ? root
            : ModPow(root, (uint)index, modulus);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillTwiddleSeed(
        Span<uint> seed,
        uint root,
        int firstIndex,
        uint modulus)
    {
        ulong twiddle =
            GetTwiddleAtIndex(root, firstIndex, modulus);

        for (int lane = 0; lane < seed.Length; lane++)
        {
            seed[lane] = (uint)twiddle;
            if (lane + 1 < seed.Length)
            {
                twiddle =
                    twiddle * root % modulus;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreScalarTwiddleTail(
        uint[] forwardTwiddles,
        uint[] inverseTwiddles,
        uint[]? forwardShoupTwiddles,
        uint[]? inverseShoupTwiddles,
        int offset,
        int halfLength,
        int index,
        int endIndex,
        uint root,
        uint modulus,
        bool buildShoupCompanions,
        double shoupScale,
        ulong twiddle,
        CancellationToken cancellationToken)
    {
        for (; index < endIndex; index++)
        {
            uint current = (uint)twiddle;
            forwardTwiddles[offset + index] = current;

            int inverseIndex =
                offset + halfLength - index;
            inverseTwiddles[inverseIndex] =
                modulus - current;

            if (buildShoupCompanions)
            {
                uint shoup =
                    ComputeShoupCompanion(
                        current,
                        modulus,
                        shoupScale);
                forwardShoupTwiddles![offset + index] = shoup;
                inverseShoupTwiddles![inverseIndex] =
                    uint.MaxValue - shoup;
            }

            if (index + 1 < endIndex)
            {
                twiddle =
                    twiddle * root % modulus;
            }

            if ((index & 0xFFFF) == 0xFFFF)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> ReverseUInt32LanesAvx2(
        Vector256<uint> value)
    {
        Vector256<int> indices =
            Vector256.Create(7, 6, 5, 4, 3, 2, 1, 0);
        return Avx2.PermuteVar8x32(
                value.AsInt32(),
                indices)
            .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> ReverseUInt32LanesAvx512(
        Vector512<uint> value)
    {
        Vector256<uint> lower =
            ReverseUInt32LanesAvx2(value.GetLower());
        Vector256<uint> upper =
            ReverseUInt32LanesAvx2(value.GetUpper());
        return Vector512.Create(upper, lower);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ReverseUInt32LanesSse(
        Vector128<uint> value) =>
        Sse2.Shuffle(value.AsInt32(), 0x1B).AsUInt32();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ReverseUInt32LanesNeon(
        Vector128<uint> value)
    {
        // Keep the reversal explicit at the Vector128 boundary.  On ARM64 the
        // JIT lowers this fixed four-lane pattern without touching the scalar
        // modular recurrence that this kernel is designed to remove.
        return Vector128.Create(
            value.GetElement(3),
            value.GetElement(2),
            value.GetElement(1),
            value.GetElement(0));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildCachedTwiddleRangeAvx512(
        uint[] forwardTwiddles,
        uint[] inverseTwiddles,
        uint[]? forwardShoupTwiddles,
        uint[]? inverseShoupTwiddles,
        int offset,
        int halfLength,
        int firstIndex,
        int endIndex,
        uint root,
        uint modulus,
        bool buildShoupCompanions,
        double shoupScale,
        uint advance,
        uint advanceShoup,
        CancellationToken cancellationToken)
    {
        const int Width = 16;
        Span<uint> seed = stackalloc uint[Width];
        FillTwiddleSeed(seed, root, firstIndex, modulus);
        ref uint seedRef = ref MemoryMarshal.GetReference(seed);

        Vector512<uint> twiddle =
            Vector512.LoadUnsafe(ref seedRef);
        var context = new Avx512NttModContext(modulus);
        Vector512<uint> advanceVector = Vector512.Create(advance);
        Vector512<uint> advanceShoupVector = Vector512.Create(advanceShoup);
        Vector512<uint> uintMax = Vector512.Create(uint.MaxValue);

        ref uint forwardRef =
            ref MemoryMarshal.GetArrayDataReference(forwardTwiddles);
        ref uint inverseRef =
            ref MemoryMarshal.GetArrayDataReference(inverseTwiddles);

        Span<uint> shoupScratch = stackalloc uint[Width];
        ref uint shoupScratchRef =
            ref MemoryMarshal.GetReference(shoupScratch);
        ref uint forwardShoupRef = ref shoupScratchRef;
        ref uint inverseShoupRef = ref shoupScratchRef;
        if (buildShoupCompanions)
        {
            forwardShoupRef =
                ref MemoryMarshal.GetArrayDataReference(forwardShoupTwiddles!);
            inverseShoupRef =
                ref MemoryMarshal.GetArrayDataReference(inverseShoupTwiddles!);
        }

        int index = firstIndex;
        while (index + Width <= endIndex)
        {
            twiddle.StoreUnsafe(
                ref forwardRef,
                (nuint)(offset + index));

            Vector512<uint> inverse =
                ReverseUInt32LanesAvx512(
                    Vector512.Subtract(context.Modulus, twiddle));
            int inverseStart =
                offset + halfLength - (index + Width - 1);
            inverse.StoreUnsafe(
                ref inverseRef,
                (nuint)inverseStart);

            if (buildShoupCompanions)
            {
                for (int lane = 0; lane < Width; lane++)
                {
                    shoupScratch[lane] =
                        ComputeShoupCompanion(
                            twiddle.GetElement(lane),
                            modulus,
                            shoupScale);
                }

                Vector512<uint> shoup =
                    Vector512.LoadUnsafe(ref shoupScratchRef);
                shoup.StoreUnsafe(
                    ref forwardShoupRef,
                    (nuint)(offset + index));

                Vector512<uint> inverseShoup =
                    ReverseUInt32LanesAvx512(
                        Vector512.Subtract(uintMax, shoup));
                inverseShoup.StoreUnsafe(
                    ref inverseShoupRef,
                    (nuint)inverseStart);
            }

            index += Width;
            if (index < endIndex)
            {
                twiddle =
                    MultiplyShoupAvx512(
                        twiddle,
                        advanceVector,
                        advanceShoupVector,
                        context);
            }

            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (index < endIndex)
        {
            StoreScalarTwiddleTail(
                forwardTwiddles,
                inverseTwiddles,
                forwardShoupTwiddles,
                inverseShoupTwiddles,
                offset,
                halfLength,
                index,
                endIndex,
                root,
                modulus,
                buildShoupCompanions,
                shoupScale,
                twiddle.GetElement(0),
                cancellationToken);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildCachedTwiddleRangeAvx2(
        uint[] forwardTwiddles,
        uint[] inverseTwiddles,
        uint[]? forwardShoupTwiddles,
        uint[]? inverseShoupTwiddles,
        int offset,
        int halfLength,
        int firstIndex,
        int endIndex,
        uint root,
        uint modulus,
        bool buildShoupCompanions,
        double shoupScale,
        uint advance,
        uint advanceShoup,
        CancellationToken cancellationToken)
    {
        const int Width = 8;
        Span<uint> seed = stackalloc uint[Width];
        FillTwiddleSeed(seed, root, firstIndex, modulus);
        ref uint seedRef = ref MemoryMarshal.GetReference(seed);

        Vector256<uint> twiddle =
            Vector256.LoadUnsafe(ref seedRef);
        var context = new Avx2NttModContext(modulus);
        Vector256<uint> advanceVector = Vector256.Create(advance);
        Vector256<uint> advanceShoupVector = Vector256.Create(advanceShoup);
        Vector256<uint> uintMax = Vector256.Create(uint.MaxValue);

        ref uint forwardRef =
            ref MemoryMarshal.GetArrayDataReference(forwardTwiddles);
        ref uint inverseRef =
            ref MemoryMarshal.GetArrayDataReference(inverseTwiddles);

        Span<uint> shoupScratch = stackalloc uint[Width];
        ref uint shoupScratchRef =
            ref MemoryMarshal.GetReference(shoupScratch);
        ref uint forwardShoupRef = ref shoupScratchRef;
        ref uint inverseShoupRef = ref shoupScratchRef;
        if (buildShoupCompanions)
        {
            forwardShoupRef =
                ref MemoryMarshal.GetArrayDataReference(forwardShoupTwiddles!);
            inverseShoupRef =
                ref MemoryMarshal.GetArrayDataReference(inverseShoupTwiddles!);
        }

        int index = firstIndex;
        while (index + Width <= endIndex)
        {
            twiddle.StoreUnsafe(
                ref forwardRef,
                (nuint)(offset + index));

            Vector256<uint> inverse =
                ReverseUInt32LanesAvx2(
                    Avx2.Subtract(context.Modulus, twiddle));
            int inverseStart =
                offset + halfLength - (index + Width - 1);
            inverse.StoreUnsafe(
                ref inverseRef,
                (nuint)inverseStart);

            if (buildShoupCompanions)
            {
                for (int lane = 0; lane < Width; lane++)
                {
                    shoupScratch[lane] =
                        ComputeShoupCompanion(
                            twiddle.GetElement(lane),
                            modulus,
                            shoupScale);
                }

                Vector256<uint> shoup =
                    Vector256.LoadUnsafe(ref shoupScratchRef);
                shoup.StoreUnsafe(
                    ref forwardShoupRef,
                    (nuint)(offset + index));

                Vector256<uint> inverseShoup =
                    ReverseUInt32LanesAvx2(
                        Avx2.Subtract(uintMax, shoup));
                inverseShoup.StoreUnsafe(
                    ref inverseShoupRef,
                    (nuint)inverseStart);
            }

            index += Width;
            if (index < endIndex)
            {
                twiddle =
                    MultiplyShoupAvx2(
                        twiddle,
                        advanceVector,
                        advanceShoupVector,
                        context);
            }

            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (index < endIndex)
        {
            StoreScalarTwiddleTail(
                forwardTwiddles,
                inverseTwiddles,
                forwardShoupTwiddles,
                inverseShoupTwiddles,
                offset,
                halfLength,
                index,
                endIndex,
                root,
                modulus,
                buildShoupCompanions,
                shoupScale,
                twiddle.GetElement(0),
                cancellationToken);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildCachedTwiddleRangeSse(
        uint[] forwardTwiddles,
        uint[] inverseTwiddles,
        uint[]? forwardShoupTwiddles,
        uint[]? inverseShoupTwiddles,
        int offset,
        int halfLength,
        int firstIndex,
        int endIndex,
        uint root,
        uint modulus,
        bool buildShoupCompanions,
        double shoupScale,
        uint advance,
        uint advanceShoup,
        CancellationToken cancellationToken)
    {
        const int Width = 4;
        Span<uint> seed = stackalloc uint[Width];
        FillTwiddleSeed(seed, root, firstIndex, modulus);
        ref uint seedRef = ref MemoryMarshal.GetReference(seed);

        Vector128<uint> twiddle =
            Vector128.LoadUnsafe(ref seedRef);
        Vector128<uint> modulusVector = Vector128.Create(modulus);
        Vector128<uint> advanceVector = Vector128.Create(advance);
        Vector128<uint> advanceShoupVector = Vector128.Create(advanceShoup);
        Vector128<uint> uintMax = Vector128.Create(uint.MaxValue);

        ref uint forwardRef =
            ref MemoryMarshal.GetArrayDataReference(forwardTwiddles);
        ref uint inverseRef =
            ref MemoryMarshal.GetArrayDataReference(inverseTwiddles);

        Span<uint> shoupScratch = stackalloc uint[Width];
        ref uint shoupScratchRef =
            ref MemoryMarshal.GetReference(shoupScratch);
        ref uint forwardShoupRef = ref shoupScratchRef;
        ref uint inverseShoupRef = ref shoupScratchRef;
        if (buildShoupCompanions)
        {
            forwardShoupRef =
                ref MemoryMarshal.GetArrayDataReference(forwardShoupTwiddles!);
            inverseShoupRef =
                ref MemoryMarshal.GetArrayDataReference(inverseShoupTwiddles!);
        }

        int index = firstIndex;
        while (index + Width <= endIndex)
        {
            twiddle.StoreUnsafe(
                ref forwardRef,
                (nuint)(offset + index));

            Vector128<uint> inverse =
                ReverseUInt32LanesSse(
                    Sse2.Subtract(modulusVector, twiddle));
            int inverseStart =
                offset + halfLength - (index + Width - 1);
            inverse.StoreUnsafe(
                ref inverseRef,
                (nuint)inverseStart);

            if (buildShoupCompanions)
            {
                for (int lane = 0; lane < Width; lane++)
                {
                    shoupScratch[lane] =
                        ComputeShoupCompanion(
                            twiddle.GetElement(lane),
                            modulus,
                            shoupScale);
                }

                Vector128<uint> shoup =
                    Vector128.LoadUnsafe(ref shoupScratchRef);
                shoup.StoreUnsafe(
                    ref forwardShoupRef,
                    (nuint)(offset + index));

                Vector128<uint> inverseShoup =
                    ReverseUInt32LanesSse(
                        Sse2.Subtract(uintMax, shoup));
                inverseShoup.StoreUnsafe(
                    ref inverseShoupRef,
                    (nuint)inverseStart);
            }

            index += Width;
            if (index < endIndex)
            {
                twiddle =
                    MultiplyShoupSse(
                        twiddle,
                        advanceVector,
                        advanceShoupVector,
                        modulusVector);
            }

            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (index < endIndex)
        {
            StoreScalarTwiddleTail(
                forwardTwiddles,
                inverseTwiddles,
                forwardShoupTwiddles,
                inverseShoupTwiddles,
                offset,
                halfLength,
                index,
                endIndex,
                root,
                modulus,
                buildShoupCompanions,
                shoupScale,
                twiddle.GetElement(0),
                cancellationToken);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildCachedTwiddleRangeNeon(
        uint[] forwardTwiddles,
        uint[] inverseTwiddles,
        uint[]? forwardShoupTwiddles,
        uint[]? inverseShoupTwiddles,
        int offset,
        int halfLength,
        int firstIndex,
        int endIndex,
        uint root,
        uint modulus,
        bool buildShoupCompanions,
        double shoupScale,
        uint advance,
        uint advanceShoup,
        CancellationToken cancellationToken)
    {
        const int Width = 4;
        Span<uint> seed = stackalloc uint[Width];
        FillTwiddleSeed(seed, root, firstIndex, modulus);
        ref uint seedRef = ref MemoryMarshal.GetReference(seed);

        Vector128<uint> twiddle =
            Vector128.LoadUnsafe(ref seedRef);
        Vector128<uint> modulusVector = Vector128.Create(modulus);
        Vector128<uint> advanceVector = Vector128.Create(advance);
        Vector128<uint> advanceShoupVector = Vector128.Create(advanceShoup);
        Vector128<uint> uintMax = Vector128.Create(uint.MaxValue);

        ref uint forwardRef =
            ref MemoryMarshal.GetArrayDataReference(forwardTwiddles);
        ref uint inverseRef =
            ref MemoryMarshal.GetArrayDataReference(inverseTwiddles);

        Span<uint> shoupScratch = stackalloc uint[Width];
        ref uint shoupScratchRef =
            ref MemoryMarshal.GetReference(shoupScratch);
        ref uint forwardShoupRef = ref shoupScratchRef;
        ref uint inverseShoupRef = ref shoupScratchRef;
        if (buildShoupCompanions)
        {
            forwardShoupRef =
                ref MemoryMarshal.GetArrayDataReference(forwardShoupTwiddles!);
            inverseShoupRef =
                ref MemoryMarshal.GetArrayDataReference(inverseShoupTwiddles!);
        }

        int index = firstIndex;
        while (index + Width <= endIndex)
        {
            twiddle.StoreUnsafe(
                ref forwardRef,
                (nuint)(offset + index));

            Vector128<uint> inverse =
                ReverseUInt32LanesNeon(
                    AdvSimd.Subtract(modulusVector, twiddle));
            int inverseStart =
                offset + halfLength - (index + Width - 1);
            inverse.StoreUnsafe(
                ref inverseRef,
                (nuint)inverseStart);

            if (buildShoupCompanions)
            {
                for (int lane = 0; lane < Width; lane++)
                {
                    shoupScratch[lane] =
                        ComputeShoupCompanion(
                            twiddle.GetElement(lane),
                            modulus,
                            shoupScale);
                }

                Vector128<uint> shoup =
                    Vector128.LoadUnsafe(ref shoupScratchRef);
                shoup.StoreUnsafe(
                    ref forwardShoupRef,
                    (nuint)(offset + index));

                Vector128<uint> inverseShoup =
                    ReverseUInt32LanesNeon(
                        AdvSimd.Subtract(uintMax, shoup));
                inverseShoup.StoreUnsafe(
                    ref inverseShoupRef,
                    (nuint)inverseStart);
            }

            index += Width;
            if (index < endIndex)
            {
                twiddle =
                    MultiplyShoupNeon(
                        twiddle,
                        advanceVector,
                        advanceShoupVector,
                        modulusVector);
            }

            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (index < endIndex)
        {
            StoreScalarTwiddleTail(
                forwardTwiddles,
                inverseTwiddles,
                forwardShoupTwiddles,
                inverseShoupTwiddles,
                offset,
                halfLength,
                index,
                endIndex,
                root,
                modulus,
                buildShoupCompanions,
                shoupScale,
                twiddle.GetElement(0),
                cancellationToken);
        }
    }
}
