using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    /// <summary>
    /// Builds a lazily requested global Shoup-companion row with the widest
    /// active NTT ISA. For x &lt; p &lt; 2^31,
    ///
    ///   Shoup(x) = floor(x * 2^32 / p).
    ///
    /// Let R = floor(2^64 / p) = Rh*2^32 + Rl. Then
    ///
    ///   q0 = x*Rh + high32(x*Rl)
    ///
    /// is either the exact quotient or exactly one below it. The residual of
    /// q0 is below 2p, so one correction bit is enough. This removes the
    /// scalar double estimate/correction loop from global companion setup and
    /// keeps the entire full-vector path integer/exact.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BuildGlobalShoupCompanionRangeSimd(
        uint[] twiddles,
        uint[] shoup,
        int offset,
        int start,
        int end,
        uint modulus,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        if (workers.UseAvx512Ntt &&
            Avx512F.IsSupported &&
            Vector512.IsHardwareAccelerated)
        {
            BuildGlobalShoupCompanionRangeAvx512(
                twiddles,
                shoup,
                offset,
                start,
                end,
                modulus,
                cancellationToken);
            return;
        }

        if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            BuildGlobalShoupCompanionRangeAvx2(
                twiddles,
                shoup,
                offset,
                start,
                end,
                modulus,
                cancellationToken);
            return;
        }

        if (workers.UseSseNtt && Sse2.IsSupported)
        {
            BuildGlobalShoupCompanionRangeSse(
                twiddles,
                shoup,
                offset,
                start,
                end,
                modulus,
                cancellationToken);
            return;
        }

        if (workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated))
        {
            BuildGlobalShoupCompanionRangeNeon(
                twiddles,
                shoup,
                offset,
                start,
                end,
                modulus,
                cancellationToken);
            return;
        }

        double scale = 4_294_967_296.0 / modulus;
        for (int index = start; index < end; index++)
        {
            shoup[offset + index] =
                ComputeShoupCompanion(
                    twiddles[offset + index],
                    modulus,
                    scale);

            if ((index & 0xFFFF) == 0xFFFF)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GetGlobalShoupReciprocal(
        uint modulus,
        out uint reciprocalHigh,
        out uint reciprocalLow)
    {
        // Hoist the production-prime reciprocals into constants so every
        // worker range avoids even the one-off 64-bit divide. The generic
        // branch keeps this helper valid if another odd NTT modulus is added.
        if (modulus == FirstModulus)
        {
            reciprocalHigh = 2u;
            reciprocalLow = 572_662_301u;
            return;
        }

        if (modulus == SecondModulus)
        {
            reciprocalHigh = 9u;
            reciprocalLow = 613_566_672u;
            return;
        }

        // For an odd p, floor((2^64-1)/p) == floor(2^64/p).
        ulong reciprocal = ulong.MaxValue / modulus;
        reciprocalHigh = (uint)(reciprocal >> 32);
        reciprocalLow = (uint)reciprocal;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> MultiplyHighUInt32Avx2(
        Vector256<uint> left,
        Vector256<uint> right)
    {
        Vector256<ulong> even =
            Avx2.ShiftRightLogical(
                Avx2.Multiply(left, right),
                32);

        Vector256<uint> oddLeft =
            Avx2.ShiftRightLogical(
                    left.AsUInt64(),
                    32)
                .AsUInt32();
        Vector256<uint> oddRight =
            Avx2.ShiftRightLogical(
                    right.AsUInt64(),
                    32)
                .AsUInt32();
        Vector256<ulong> odd =
            Avx2.ShiftRightLogical(
                Avx2.Multiply(oddLeft, oddRight),
                32);

        return Avx2.Or(
                even.AsInt32(),
                Avx2.ShiftLeftLogical(odd, 32).AsInt32())
            .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> MultiplyHighUInt32Avx512(
        Vector512<uint> left,
        Vector512<uint> right)
    {
        Vector512<ulong> even =
            Avx512F.ShiftRightLogical(
                Avx512F.Multiply(left, right),
                32);

        Vector512<uint> oddLeft =
            Avx512F.ShiftRightLogical(
                    left.AsUInt64(),
                    32)
                .AsUInt32();
        Vector512<uint> oddRight =
            Avx512F.ShiftRightLogical(
                    right.AsUInt64(),
                    32)
                .AsUInt32();
        Vector512<ulong> odd =
            Avx512F.ShiftRightLogical(
                Avx512F.Multiply(oddLeft, oddRight),
                32);

        return Vector512.BitwiseOr(
            even.AsUInt32(),
            Avx512F.ShiftLeftLogical(odd, 32).AsUInt32());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> MultiplyHighUInt32Sse2(
        Vector128<uint> left,
        Vector128<uint> right)
    {
        Vector128<ulong> even =
            Sse2.ShiftRightLogical(
                Sse2.Multiply(left, right),
                32);

        Vector128<uint> oddLeft =
            Sse2.ShiftRightLogical(
                    left.AsUInt64(),
                    32)
                .AsUInt32();
        Vector128<uint> oddRight =
            Sse2.ShiftRightLogical(
                    right.AsUInt64(),
                    32)
                .AsUInt32();
        Vector128<ulong> odd =
            Sse2.ShiftRightLogical(
                Sse2.Multiply(oddLeft, oddRight),
                32);

        return Sse2.Or(
                even.AsInt32(),
                Sse2.ShiftLeftLogical(odd, 32).AsInt32())
            .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> MultiplyLowUInt32Sse2(
        Vector128<uint> left,
        Vector128<uint> right)
    {
        if (Sse41.IsSupported)
        {
            return Sse41.MultiplyLow(
                    left.AsInt32(),
                    right.AsInt32())
                .AsUInt32();
        }

        Vector128<ulong> evenProduct =
            Sse2.Multiply(left, right);

        Vector128<uint> oddLeft =
            Sse2.ShiftRightLogical(
                    left.AsUInt64(),
                    32)
                .AsUInt32();
        Vector128<uint> oddRight =
            Sse2.ShiftRightLogical(
                    right.AsUInt64(),
                    32)
                .AsUInt32();
        Vector128<ulong> oddProduct =
            Sse2.Multiply(oddLeft, oddRight);

        Vector128<uint> evenDwordMask =
            Vector128.Create(
                uint.MaxValue,
                0u,
                uint.MaxValue,
                0u);

        Vector128<uint> evenLow =
            Sse2.And(
                    evenProduct.AsInt32(),
                    evenDwordMask.AsInt32())
                .AsUInt32();
        Vector128<uint> oddLow =
            Sse2.ShiftLeftLogical(
                    oddProduct,
                    32)
                .AsUInt32();

        return Sse2.Or(
                evenLow.AsInt32(),
                oddLow.AsInt32())
            .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> MultiplyHighUInt32Neon(
        Vector128<uint> left,
        Vector128<uint> right)
    {
        Vector128<ulong> lower =
            AdvSimd.MultiplyWideningLower(
                left.GetLower(),
                right.GetLower());
        Vector128<ulong> upper =
            AdvSimd.MultiplyWideningUpper(
                left,
                right);

        Vector64<uint> lowerHigh =
            AdvSimd.ExtractNarrowingLower(
                AdvSimd.ShiftRightLogical(
                    lower,
                    32));
        Vector64<uint> upperHigh =
            AdvSimd.ExtractNarrowingLower(
                AdvSimd.ShiftRightLogical(
                    upper,
                    32));

        return Vector128.Create(
            lowerHigh,
            upperHigh);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> ComputeShoupCompanionAvx512(
        Vector512<uint> twiddle,
        Vector512<uint> reciprocalHigh,
        Vector512<uint> reciprocalLow,
        Vector512<uint> modulus,
        Vector512<uint> one,
        Vector512<uint> zero)
    {
        Vector512<uint> estimate =
            Vector512.Add(
                Avx512F.MultiplyLow(
                    twiddle,
                    reciprocalHigh),
                MultiplyHighUInt32Avx512(
                    twiddle,
                    reciprocalLow));

        Vector512<uint> quotientTimesModulusLow =
            Avx512F.MultiplyLow(
                estimate,
                modulus);
        Vector512<uint> residual =
            Vector512.Subtract(
                zero,
                quotientTimesModulusLow);

        // residual < 2p. If residual-p underflows, its top bit is 1 because
        // p < 2^31; otherwise the top bit is 0. Thus 1-underflow is the exact
        // one-step quotient correction without an unsigned compare.
        Vector512<uint> underflow =
            Avx512F.ShiftRightLogical(
                Vector512.Subtract(
                    residual,
                    modulus),
                31);
        Vector512<uint> correction =
            Vector512.Subtract(
                one,
                underflow);

        return Vector512.Add(
            estimate,
            correction);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> ComputeShoupCompanionAvx2(
        Vector256<uint> twiddle,
        Vector256<uint> reciprocalHigh,
        Vector256<uint> reciprocalLow,
        Vector256<uint> modulus,
        Vector256<uint> one,
        Vector256<uint> zero)
    {
        Vector256<uint> estimate =
            Avx2.Add(
                    Avx2.MultiplyLow(
                        twiddle.AsInt32(),
                        reciprocalHigh.AsInt32()),
                    MultiplyHighUInt32Avx2(
                            twiddle,
                            reciprocalLow)
                        .AsInt32())
                .AsUInt32();

        Vector256<uint> quotientTimesModulusLow =
            Avx2.MultiplyLow(
                    estimate.AsInt32(),
                    modulus.AsInt32())
                .AsUInt32();
        Vector256<uint> residual =
            Avx2.Subtract(
                    zero.AsInt32(),
                    quotientTimesModulusLow.AsInt32())
                .AsUInt32();
        Vector256<uint> underflow =
            Avx2.ShiftRightLogical(
                    Avx2.Subtract(
                            residual.AsInt32(),
                            modulus.AsInt32())
                        .AsUInt32(),
                    31)
                .AsUInt32();
        Vector256<uint> correction =
            Avx2.Subtract(
                    one.AsInt32(),
                    underflow.AsInt32())
                .AsUInt32();

        return Avx2.Add(
                estimate.AsInt32(),
                correction.AsInt32())
            .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ComputeShoupCompanionSse(
        Vector128<uint> twiddle,
        Vector128<uint> reciprocalHigh,
        Vector128<uint> reciprocalLow,
        Vector128<uint> modulus,
        Vector128<uint> one,
        Vector128<uint> zero)
    {
        Vector128<uint> estimate =
            Sse2.Add(
                    MultiplyLowUInt32Sse2(
                            twiddle,
                            reciprocalHigh)
                        .AsInt32(),
                    MultiplyHighUInt32Sse2(
                            twiddle,
                            reciprocalLow)
                        .AsInt32())
                .AsUInt32();

        Vector128<uint> quotientTimesModulusLow =
            MultiplyLowUInt32Sse2(
                estimate,
                modulus);
        Vector128<uint> residual =
            Sse2.Subtract(
                    zero.AsInt32(),
                    quotientTimesModulusLow.AsInt32())
                .AsUInt32();
        Vector128<uint> underflow =
            Sse2.ShiftRightLogical(
                    Sse2.Subtract(
                            residual.AsInt32(),
                            modulus.AsInt32())
                        .AsUInt32(),
                    31)
                .AsUInt32();
        Vector128<uint> correction =
            Sse2.Subtract(
                    one.AsInt32(),
                    underflow.AsInt32())
                .AsUInt32();

        return Sse2.Add(
                estimate.AsInt32(),
                correction.AsInt32())
            .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ComputeShoupCompanionNeon(
        Vector128<uint> twiddle,
        Vector128<uint> reciprocalHigh,
        Vector128<uint> reciprocalLow,
        Vector128<uint> modulus,
        Vector128<uint> one,
        Vector128<uint> zero)
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            // Exact reciprocal estimate is at most one below the quotient.
            // Unsigned residual <2p<2^32 supplies the correction, including x=0.
            var q = twiddle * reciprocalHigh + MultiplyHighUInt32Portable(twiddle, reciprocalLow);
            var remainder = zero - q * modulus;
            return q + (Vector128.GreaterThanOrEqual(remainder, modulus) & one);
        }
        Vector128<uint> estimate =
            AdvSimd.Add(
                AdvSimd.Multiply(
                    twiddle,
                    reciprocalHigh),
                MultiplyHighUInt32Neon(
                    twiddle,
                    reciprocalLow));

        Vector128<uint> quotientTimesModulusLow =
            AdvSimd.Multiply(
                estimate,
                modulus);
        Vector128<uint> residual =
            AdvSimd.Subtract(
                zero,
                quotientTimesModulusLow);
        Vector128<uint> underflow =
            AdvSimd.ShiftRightLogical(
                AdvSimd.Subtract(
                    residual,
                    modulus),
                31);
        Vector128<uint> correction =
            AdvSimd.Subtract(
                one,
                underflow);

        return AdvSimd.Add(
            estimate,
            correction);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildGlobalShoupCompanionRangeAvx512(
        uint[] twiddles,
        uint[] shoup,
        int offset,
        int start,
        int end,
        uint modulus,
        CancellationToken cancellationToken)
    {
        const int Width = 16;
        const int Unroll = Width * 2;

        GetGlobalShoupReciprocal(
            modulus,
            out uint reciprocalHighScalar,
            out uint reciprocalLowScalar);

        Vector512<uint> reciprocalHigh =
            Vector512.Create(reciprocalHighScalar);
        Vector512<uint> reciprocalLow =
            Vector512.Create(reciprocalLowScalar);
        Vector512<uint> modulusVector =
            Vector512.Create(modulus);
        Vector512<uint> one = Vector512.Create(1u);
        Vector512<uint> zero = Vector512.Create(0u);

        ref uint twiddleRef =
            ref MemoryMarshal.GetArrayDataReference(twiddles);
        ref uint shoupRef =
            ref MemoryMarshal.GetArrayDataReference(shoup);

        int index = start;
        for (; index + Unroll <= end; index += Unroll)
        {
            Vector512<uint> first =
                Vector512.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index));
            Vector512<uint> second =
                Vector512.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index + Width));

            Vector512<uint> firstShoup =
                ComputeShoupCompanionAvx512(
                    first,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero);
            Vector512<uint> secondShoup =
                ComputeShoupCompanionAvx512(
                    second,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero);

            firstShoup.StoreUnsafe(
                ref shoupRef,
                (nuint)(offset + index));
            secondShoup.StoreUnsafe(
                ref shoupRef,
                (nuint)(offset + index + Width));

            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        for (; index + Width <= end; index += Width)
        {
            Vector512<uint> value =
                Vector512.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index));
            ComputeShoupCompanionAvx512(
                    value,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index));
        }

        if (index < end)
        {
            Span<uint> tailInput = stackalloc uint[Width];
            tailInput.Clear();
            Span<uint> tailOutput = stackalloc uint[Width];
            int remaining = end - index;

            for (int lane = 0; lane < remaining; lane++)
            {
                tailInput[lane] = twiddles[offset + index + lane];
            }

            ref uint tailInputRef = ref MemoryMarshal.GetReference(tailInput);
            ref uint tailOutputRef = ref MemoryMarshal.GetReference(tailOutput);
            Vector512<uint> tailVector = Vector512.LoadUnsafe(ref tailInputRef);
            ComputeShoupCompanionAvx512(
                    tailVector,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(ref tailOutputRef);

            for (int lane = 0; lane < remaining; lane++)
            {
                shoup[offset + index + lane] = tailOutput[lane];
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildGlobalShoupCompanionRangeAvx2(
        uint[] twiddles,
        uint[] shoup,
        int offset,
        int start,
        int end,
        uint modulus,
        CancellationToken cancellationToken)
    {
        const int Width = 8;
        const int Unroll = Width * 2;

        GetGlobalShoupReciprocal(
            modulus,
            out uint reciprocalHighScalar,
            out uint reciprocalLowScalar);

        Vector256<uint> reciprocalHigh =
            Vector256.Create(reciprocalHighScalar);
        Vector256<uint> reciprocalLow =
            Vector256.Create(reciprocalLowScalar);
        Vector256<uint> modulusVector =
            Vector256.Create(modulus);
        Vector256<uint> one = Vector256.Create(1u);
        Vector256<uint> zero = Vector256.Create(0u);

        ref uint twiddleRef =
            ref MemoryMarshal.GetArrayDataReference(twiddles);
        ref uint shoupRef =
            ref MemoryMarshal.GetArrayDataReference(shoup);

        int index = start;
        for (; index + Unroll <= end; index += Unroll)
        {
            Vector256<uint> first =
                Vector256.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index));
            Vector256<uint> second =
                Vector256.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index + Width));

            ComputeShoupCompanionAvx2(
                    first,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index));
            ComputeShoupCompanionAvx2(
                    second,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index + Width));

            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        for (; index + Width <= end; index += Width)
        {
            Vector256<uint> value =
                Vector256.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index));
            ComputeShoupCompanionAvx2(
                    value,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index));
        }

        if (index < end)
        {
            Span<uint> tailInput = stackalloc uint[Width];
            tailInput.Clear();
            Span<uint> tailOutput = stackalloc uint[Width];
            int remaining = end - index;

            for (int lane = 0; lane < remaining; lane++)
            {
                tailInput[lane] = twiddles[offset + index + lane];
            }

            ref uint tailInputRef = ref MemoryMarshal.GetReference(tailInput);
            ref uint tailOutputRef = ref MemoryMarshal.GetReference(tailOutput);
            Vector256<uint> tailVector = Vector256.LoadUnsafe(ref tailInputRef);
            ComputeShoupCompanionAvx2(
                    tailVector,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(ref tailOutputRef);

            for (int lane = 0; lane < remaining; lane++)
            {
                shoup[offset + index + lane] = tailOutput[lane];
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildGlobalShoupCompanionRangeSse(
        uint[] twiddles,
        uint[] shoup,
        int offset,
        int start,
        int end,
        uint modulus,
        CancellationToken cancellationToken)
    {
        const int Width = 4;
        const int Unroll = Width * 2;

        GetGlobalShoupReciprocal(
            modulus,
            out uint reciprocalHighScalar,
            out uint reciprocalLowScalar);

        Vector128<uint> reciprocalHigh =
            Vector128.Create(reciprocalHighScalar);
        Vector128<uint> reciprocalLow =
            Vector128.Create(reciprocalLowScalar);
        Vector128<uint> modulusVector =
            Vector128.Create(modulus);
        Vector128<uint> one = Vector128.Create(1u);
        Vector128<uint> zero = Vector128.Create(0u);

        ref uint twiddleRef =
            ref MemoryMarshal.GetArrayDataReference(twiddles);
        ref uint shoupRef =
            ref MemoryMarshal.GetArrayDataReference(shoup);

        int index = start;
        for (; index + Unroll <= end; index += Unroll)
        {
            Vector128<uint> first =
                Vector128.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index));
            Vector128<uint> second =
                Vector128.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index + Width));

            ComputeShoupCompanionSse(
                    first,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index));
            ComputeShoupCompanionSse(
                    second,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index + Width));

            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        for (; index + Width <= end; index += Width)
        {
            Vector128<uint> value =
                Vector128.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index));
            ComputeShoupCompanionSse(
                    value,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index));
        }

        if (index < end)
        {
            Span<uint> tailInput = stackalloc uint[Width];
            tailInput.Clear();
            Span<uint> tailOutput = stackalloc uint[Width];
            int remaining = end - index;

            for (int lane = 0; lane < remaining; lane++)
            {
                tailInput[lane] = twiddles[offset + index + lane];
            }

            ref uint tailInputRef = ref MemoryMarshal.GetReference(tailInput);
            ref uint tailOutputRef = ref MemoryMarshal.GetReference(tailOutput);
            Vector128<uint> tailVector = Vector128.LoadUnsafe(ref tailInputRef);
            ComputeShoupCompanionSse(
                    tailVector,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(ref tailOutputRef);

            for (int lane = 0; lane < remaining; lane++)
            {
                shoup[offset + index + lane] = tailOutput[lane];
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildGlobalShoupCompanionRangeNeon(
        uint[] twiddles,
        uint[] shoup,
        int offset,
        int start,
        int end,
        uint modulus,
        CancellationToken cancellationToken)
    {
        const int Width = 4;
        const int Unroll = Width * 2;

        GetGlobalShoupReciprocal(
            modulus,
            out uint reciprocalHighScalar,
            out uint reciprocalLowScalar);

        Vector128<uint> reciprocalHigh =
            Vector128.Create(reciprocalHighScalar);
        Vector128<uint> reciprocalLow =
            Vector128.Create(reciprocalLowScalar);
        Vector128<uint> modulusVector =
            Vector128.Create(modulus);
        Vector128<uint> one = Vector128.Create(1u);
        Vector128<uint> zero = Vector128.Create(0u);

        ref uint twiddleRef =
            ref MemoryMarshal.GetArrayDataReference(twiddles);
        ref uint shoupRef =
            ref MemoryMarshal.GetArrayDataReference(shoup);

        int index = start;
        for (; index + Unroll <= end; index += Unroll)
        {
            Vector128<uint> first =
                Vector128.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index));
            Vector128<uint> second =
                Vector128.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index + Width));

            ComputeShoupCompanionNeon(
                    first,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index));
            ComputeShoupCompanionNeon(
                    second,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index + Width));

            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        for (; index + Width <= end; index += Width)
        {
            Vector128<uint> value =
                Vector128.LoadUnsafe(
                    ref twiddleRef,
                    (nuint)(offset + index));
            ComputeShoupCompanionNeon(
                    value,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(
                    ref shoupRef,
                    (nuint)(offset + index));
        }

        if (index < end)
        {
            Span<uint> tailInput = stackalloc uint[Width];
            tailInput.Clear();
            Span<uint> tailOutput = stackalloc uint[Width];
            int remaining = end - index;

            for (int lane = 0; lane < remaining; lane++)
            {
                tailInput[lane] = twiddles[offset + index + lane];
            }

            ref uint tailInputRef = ref MemoryMarshal.GetReference(tailInput);
            ref uint tailOutputRef = ref MemoryMarshal.GetReference(tailOutput);
            Vector128<uint> tailVector = Vector128.LoadUnsafe(ref tailInputRef);
            ComputeShoupCompanionNeon(
                    tailVector,
                    reciprocalHigh,
                    reciprocalLow,
                    modulusVector,
                    one,
                    zero)
                .StoreUnsafe(ref tailOutputRef);

            for (int lane = 0; lane < remaining; lane++)
            {
                shoup[offset + index + lane] = tailOutput[lane];
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

}
