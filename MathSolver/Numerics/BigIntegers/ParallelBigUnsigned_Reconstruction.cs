using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    #region CRT reconstruction
    /// <summary>
    /// Exact eight-coefficient CRT reconstruction for AVX2-only x86 paths.
    /// The modular inverse is constant for the whole CRT pass, so the same
    /// Shoup multiplier used by the AVX2 NTT kernels removes UInt64 remainder
    /// operations. VPMULUDQ widens even and odd dword lanes independently;
    /// VPUNPCKLQ/HQ + VPERM2I128 restore coefficient order before two YMM
    /// stores. No unsafe pointer arithmetic or coefficient-sized side table is
    /// introduced.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int ReconstructCrtRangeAvx2(
        ReadOnlySpan<uint> firstSpan,
        ReadOnlySpan<uint> secondSpan,
        Span<ulong> scratchSpan)
    {
        int count = firstSpan.Length;
        int vectorEnd = count & ~(Vector256<uint>.Count - 1);

        ref uint firstReference = ref MemoryMarshal.GetReference(firstSpan);
        ref uint secondReference = ref MemoryMarshal.GetReference(secondSpan);
        ref ulong scratchReference = ref MemoryMarshal.GetReference(scratchSpan);

        var context = new Avx2NttModContext(SecondModulus);
        Vector256<uint> inverseVector =
            Vector256.Create((uint)FirstModulusInverseInSecond);
        Vector256<uint> inverseShoupVector =
            Vector256.Create(FirstModulusInverseInSecondShoup);
        Vector256<uint> firstModulusVector =
            Vector256.Create(FirstModulus);
        Vector256<uint> oneVector =
            Vector256.Create(1u);

        int offset = 0;

        for (; offset < vectorEnd; offset += Vector256<uint>.Count)
        {
            Vector256<uint> first =
                Vector256.LoadUnsafe(ref firstReference, (nuint)offset);

            // FirstModulus < 5 * SecondModulus. Four unsigned conditional
            // subtracts therefore reduce every P1 residue into P2 exactly.
            Vector256<uint> reducedFirst = ReduceOnceAvx2(first, context);
            reducedFirst = ReduceOnceAvx2(reducedFirst, context);
            reducedFirst = ReduceOnceAvx2(reducedFirst, context);
            reducedFirst = ReduceOnceAvx2(reducedFirst, context);

            Vector256<uint> second =
                Vector256.LoadUnsafe(ref secondReference, (nuint)offset);

            Vector256<uint> difference =
                SubtractModuloAvx2(second, reducedFirst, context);

            Vector256<uint> multiplier =
                MultiplyShoupAvx2(
                    difference,
                    inverseVector,
                    inverseShoupVector,
                    context);

            // VPMULUDQ consumes dword lanes 0/2/4/6 and produces four exact
            // qword products. Shift each qword to expose lanes 1/3/5/7 for the
            // second independent chain.
            Vector256<uint> oddFirst =
                Avx2.ShiftRightLogical(first.AsUInt64(), 32).AsUInt32();
            Vector256<uint> oddMultiplier =
                Avx2.ShiftRightLogical(multiplier.AsUInt64(), 32).AsUInt32();

            Vector256<ulong> firstEven64 =
                Avx2.Multiply(first, oneVector);
            Vector256<ulong> firstOdd64 =
                Avx2.Multiply(oddFirst, oneVector);

            Vector256<ulong> productEven =
                Avx2.Multiply(multiplier, firstModulusVector);
            Vector256<ulong> productOdd =
                Avx2.Multiply(oddMultiplier, firstModulusVector);

            Vector256<ulong> reconstructedEven =
                Avx2.Add(firstEven64.AsInt64(), productEven.AsInt64())
                    .AsUInt64();
            Vector256<ulong> reconstructedOdd =
                Avx2.Add(firstOdd64.AsInt64(), productOdd.AsInt64())
                    .AsUInt64();

            // AVX2 unpack operations are lane-local. First interleave the even
            // and odd qwords, then cross the 128-bit lane boundary once so the
            // two stores preserve scalar coefficient order 0..7.
            Vector256<ulong> interleavedLow =
                Avx2.UnpackLow(
                        reconstructedEven.AsInt64(),
                        reconstructedOdd.AsInt64())
                    .AsUInt64();
            Vector256<ulong> interleavedHigh =
                Avx2.UnpackHigh(
                        reconstructedEven.AsInt64(),
                        reconstructedOdd.AsInt64())
                    .AsUInt64();

            Vector256<ulong> reconstructedLow =
                Avx2.Permute2x128(
                        interleavedLow.AsInt64(),
                        interleavedHigh.AsInt64(),
                        0x20)
                    .AsUInt64();
            Vector256<ulong> reconstructedHigh =
                Avx2.Permute2x128(
                        interleavedLow.AsInt64(),
                        interleavedHigh.AsInt64(),
                        0x31)
                    .AsUInt64();

            reconstructedLow.StoreUnsafe(
                ref scratchReference,
                (nuint)offset);
            reconstructedHigh.StoreUnsafe(
                ref scratchReference,
                (nuint)(offset + 4));
        }

        return offset;
    }
    #endregion

    #region Carry normalization
    // Exact base-10,000 quotient helper for packed uint32 values stored in the
    // low dword of each qword lane. ceil(2^45 / 10000) = 0xD1B71759 and
    // floor(n/10000) = (n * magic) >> 45 for every uint32 n.
    private const uint CarryDivide10000Magic = 0xD1B7_1759u;
    private const ulong CarryTwo32Quotient = 429_496UL;
    private const ulong CarryTwo32Remainder = 7_296UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ulong> MultiplyPackedUInt32Avx512(
        Vector512<ulong> value,
        uint multiplier) =>
        Avx512F.Multiply(
            value.AsUInt32(),
            Vector512.Create(multiplier));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ulong> DividePackedUInt32By10000Avx512(
        Vector512<ulong> value) =>
        Avx512F.ShiftRightLogical(
            MultiplyPackedUInt32Avx512(value, CarryDivide10000Magic),
            45);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecomposeBase10000Avx512(
        Vector512<ulong> value,
        out Vector512<ulong> quotient,
        out Vector512<ulong> remainder)
    {
        Vector512<ulong> lowMask = Vector512.Create((ulong)uint.MaxValue);
        Vector512<ulong> high = Avx512F.ShiftRightLogical(value, 32);
        Vector512<ulong> low = Vector512.BitwiseAnd(value, lowMask);
        Vector512<ulong> s = Vector512.Add(
            low,
            MultiplyPackedUInt32Avx512(high, (uint)CarryTwo32Remainder));
        Vector512<ulong> sHigh = Avx512F.ShiftRightLogical(s, 32);
        Vector512<ulong> sLow = Vector512.BitwiseAnd(s, lowMask);
        Vector512<ulong> t = Vector512.Add(
            sLow,
            MultiplyPackedUInt32Avx512(sHigh, (uint)CarryTwo32Remainder));
        Vector512<ulong> overflow = Avx512F.ShiftRightLogical(t, 32);
        Vector512<ulong> tLow = Vector512.BitwiseAnd(t, lowMask);

        Vector512<ulong> q0 = DividePackedUInt32By10000Avx512(tLow);
        Vector512<ulong> r0 = Vector512.Subtract(
            tLow,
            MultiplyPackedUInt32Avx512(q0, LimbBase));
        Vector512<ulong> correctedR = Vector512.Add(
            r0,
            MultiplyPackedUInt32Avx512(overflow, (uint)CarryTwo32Remainder));
        Vector512<ulong> correction = DividePackedUInt32By10000Avx512(correctedR);
        remainder = Vector512.Subtract(
            correctedR,
            MultiplyPackedUInt32Avx512(correction, LimbBase));

        Vector512<ulong> qFromT = Vector512.Add(
            Vector512.Add(
                q0,
                MultiplyPackedUInt32Avx512(overflow, (uint)CarryTwo32Quotient)),
            correction);
        Vector512<ulong> qFromS = Vector512.Add(
            MultiplyPackedUInt32Avx512(sHigh, (uint)CarryTwo32Quotient),
            qFromT);
        quotient = Vector512.Add(
            MultiplyPackedUInt32Avx512(high, (uint)CarryTwo32Quotient),
            qFromS);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> MultiplyPackedUInt32Avx2(
        Vector256<ulong> value,
        uint multiplier) =>
        Avx2.Multiply(value.AsUInt32(), Vector256.Create(multiplier));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> DividePackedUInt32By10000Avx2(
        Vector256<ulong> value) =>
        Avx2.ShiftRightLogical(
            MultiplyPackedUInt32Avx2(value, CarryDivide10000Magic),
            45);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecomposeBase10000Avx2(
        Vector256<ulong> value,
        out Vector256<ulong> quotient,
        out Vector256<ulong> remainder)
    {
        Vector256<ulong> lowMask = Vector256.Create((ulong)uint.MaxValue);
        Vector256<ulong> high = Avx2.ShiftRightLogical(value, 32);
        Vector256<ulong> low = Avx2.And(value.AsInt64(), lowMask.AsInt64()).AsUInt64();
        Vector256<ulong> s = Avx2.Add(
            low.AsInt64(),
            MultiplyPackedUInt32Avx2(high, (uint)CarryTwo32Remainder).AsInt64()).AsUInt64();
        Vector256<ulong> sHigh = Avx2.ShiftRightLogical(s, 32);
        Vector256<ulong> sLow = Avx2.And(s.AsInt64(), lowMask.AsInt64()).AsUInt64();
        Vector256<ulong> t = Avx2.Add(
            sLow.AsInt64(),
            MultiplyPackedUInt32Avx2(sHigh, (uint)CarryTwo32Remainder).AsInt64()).AsUInt64();
        Vector256<ulong> overflow = Avx2.ShiftRightLogical(t, 32);
        Vector256<ulong> tLow = Avx2.And(t.AsInt64(), lowMask.AsInt64()).AsUInt64();

        Vector256<ulong> q0 = DividePackedUInt32By10000Avx2(tLow);
        Vector256<ulong> r0 = Avx2.Subtract(
            tLow.AsInt64(),
            MultiplyPackedUInt32Avx2(q0, LimbBase).AsInt64()).AsUInt64();
        Vector256<ulong> correctedR = Avx2.Add(
            r0.AsInt64(),
            MultiplyPackedUInt32Avx2(overflow, (uint)CarryTwo32Remainder).AsInt64()).AsUInt64();
        Vector256<ulong> correction = DividePackedUInt32By10000Avx2(correctedR);
        remainder = Avx2.Subtract(
            correctedR.AsInt64(),
            MultiplyPackedUInt32Avx2(correction, LimbBase).AsInt64()).AsUInt64();

        Vector256<ulong> qFromT = Avx2.Add(
            Avx2.Add(
                q0.AsInt64(),
                MultiplyPackedUInt32Avx2(overflow, (uint)CarryTwo32Quotient).AsInt64()),
            correction.AsInt64()).AsUInt64();
        Vector256<ulong> qFromS = Avx2.Add(
            MultiplyPackedUInt32Avx2(sHigh, (uint)CarryTwo32Quotient).AsInt64(),
            qFromT.AsInt64()).AsUInt64();
        quotient = Avx2.Add(
            MultiplyPackedUInt32Avx2(high, (uint)CarryTwo32Quotient).AsInt64(),
            qFromS.AsInt64()).AsUInt64();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> MultiplyPackedUInt32Sse(
        Vector128<ulong> value,
        uint multiplier) =>
        Sse2.Multiply(value.AsUInt32(), Vector128.Create(multiplier));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> DividePackedUInt32By10000Sse(
        Vector128<ulong> value) =>
        Sse2.ShiftRightLogical(
            MultiplyPackedUInt32Sse(value, CarryDivide10000Magic),
            45);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecomposeBase10000Sse(
        Vector128<ulong> value,
        out Vector128<ulong> quotient,
        out Vector128<ulong> remainder)
    {
        Vector128<ulong> lowMask = Vector128.Create((ulong)uint.MaxValue);
        Vector128<ulong> high = Sse2.ShiftRightLogical(value, 32);
        Vector128<ulong> low = Sse2.And(value, lowMask);
        Vector128<ulong> s = Sse2.Add(
            low.AsInt64(),
            MultiplyPackedUInt32Sse(high, (uint)CarryTwo32Remainder).AsInt64()).AsUInt64();
        Vector128<ulong> sHigh = Sse2.ShiftRightLogical(s, 32);
        Vector128<ulong> sLow = Sse2.And(s, lowMask);
        Vector128<ulong> t = Sse2.Add(
            sLow.AsInt64(),
            MultiplyPackedUInt32Sse(sHigh, (uint)CarryTwo32Remainder).AsInt64()).AsUInt64();
        Vector128<ulong> overflow = Sse2.ShiftRightLogical(t, 32);
        Vector128<ulong> tLow = Sse2.And(t, lowMask);

        Vector128<ulong> q0 = DividePackedUInt32By10000Sse(tLow);
        Vector128<ulong> r0 = Sse2.Subtract(
            tLow.AsInt64(),
            MultiplyPackedUInt32Sse(q0, LimbBase).AsInt64()).AsUInt64();
        Vector128<ulong> correctedR = Sse2.Add(
            r0.AsInt64(),
            MultiplyPackedUInt32Sse(overflow, (uint)CarryTwo32Remainder).AsInt64()).AsUInt64();
        Vector128<ulong> correction = DividePackedUInt32By10000Sse(correctedR);
        remainder = Sse2.Subtract(
            correctedR.AsInt64(),
            MultiplyPackedUInt32Sse(correction, LimbBase).AsInt64()).AsUInt64();

        Vector128<ulong> qFromT = Sse2.Add(
            Sse2.Add(
                q0.AsInt64(),
                MultiplyPackedUInt32Sse(overflow, (uint)CarryTwo32Quotient).AsInt64()),
            correction.AsInt64()).AsUInt64();
        Vector128<ulong> qFromS = Sse2.Add(
            MultiplyPackedUInt32Sse(sHigh, (uint)CarryTwo32Quotient).AsInt64(),
            qFromT.AsInt64()).AsUInt64();
        quotient = Sse2.Add(
            MultiplyPackedUInt32Sse(high, (uint)CarryTwo32Quotient).AsInt64(),
            qFromS.AsInt64()).AsUInt64();
    }

#if ANDROID
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> MultiplyPackedUInt32Neon(
        Vector128<ulong> value,
        uint multiplier)
    {
        Vector128<uint> words = value.AsUInt32();
        if (AdvSimd.Arm64.IsSupported)
        {
            Vector64<uint> packed = AdvSimd.ExtractNarrowingLower(value);
            return AdvSimd.MultiplyWideningLower(packed, Vector64.Create(multiplier));
        }

        // Portable uint32 products, assembled into two exact uint64 lanes.
        // Only the even words are significant; no vector uint64 multiply.
        Vector128<uint> factor = Vector128.Create(multiplier);
        Vector128<ulong> mask = Vector128.Create((ulong)uint.MaxValue);
        Vector128<ulong> low = (words * factor).AsUInt64() & mask;
        Vector128<ulong> high = MultiplyHighUInt32Portable(words, factor).AsUInt64() & mask;
        return low | (high << 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> DividePackedUInt32By10000Neon(
        Vector128<ulong> value) =>
        Vector128.ShiftRightLogical(
            MultiplyPackedUInt32Neon(value, CarryDivide10000Magic),
            45);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecomposeBase10000Neon(
        Vector128<ulong> value,
        out Vector128<ulong> quotient,
        out Vector128<ulong> remainder)
    {
        Vector128<ulong> lowMask = Vector128.Create((ulong)uint.MaxValue);
        Vector128<ulong> high = Vector128.ShiftRightLogical(value, 32);
        Vector128<ulong> low = value & lowMask;
        Vector128<ulong> s = Vector128.Add(
            low,
            MultiplyPackedUInt32Neon(high, (uint)CarryTwo32Remainder));
        Vector128<ulong> sHigh = Vector128.ShiftRightLogical(s, 32);
        Vector128<ulong> sLow = s & lowMask;
        Vector128<ulong> t = Vector128.Add(
            sLow,
            MultiplyPackedUInt32Neon(sHigh, (uint)CarryTwo32Remainder));
        Vector128<ulong> overflow = Vector128.ShiftRightLogical(t, 32);
        Vector128<ulong> tLow = t & lowMask;

        Vector128<ulong> q0 = DividePackedUInt32By10000Neon(tLow);
        Vector128<ulong> r0 = Vector128.Subtract(
            tLow,
            MultiplyPackedUInt32Neon(q0, LimbBase));
        Vector128<ulong> correctedR = Vector128.Add(
            r0,
            MultiplyPackedUInt32Neon(overflow, (uint)CarryTwo32Remainder));
        Vector128<ulong> correction = DividePackedUInt32By10000Neon(correctedR);
        remainder = Vector128.Subtract(
            correctedR,
            MultiplyPackedUInt32Neon(correction, LimbBase));

        Vector128<ulong> qFromT = Vector128.Add(
            Vector128.Add(
                q0,
                MultiplyPackedUInt32Neon(overflow, (uint)CarryTwo32Quotient)),
            correction);
        Vector128<ulong> qFromS = Vector128.Add(
            MultiplyPackedUInt32Neon(sHigh, (uint)CarryTwo32Quotient),
            qFromT);
        quotient = Vector128.Add(
            MultiplyPackedUInt32Neon(high, (uint)CarryTwo32Quotient),
            qFromS);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReconcileCarryDigits(
        ReadOnlySpan<ulong> quotient,
        ReadOnlySpan<ulong> remainder,
        uint[] destination,
        int destinationStart,
        ulong carry)
    {
        for (int lane = 0; lane < quotient.Length; lane++)
        {
            ulong carryQuotient = carry / LimbBase;
            ulong carryRemainder = carry - carryQuotient * LimbBase;
            ulong digit = remainder[lane] + carryRemainder;
            ulong overflow = digit >= LimbBase ? 1UL : 0UL;
            destination[destinationStart + lane] =
                (uint)(digit - overflow * LimbBase);
            carry = quotient[lane] + carryQuotient + overflow;
        }

        return carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong NormalizeCrtCarryRangeSimd(
        uint[] transformedSecond,
        ulong[]? crtScratch,
        bool useInverseTailScratch,
        int inverseTailScratchStart,
        int relativeStart,
        uint[] destination,
        int destinationStart,
        int count,
        ulong incomingCarry,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
            return incomingCarry;

        if (!BitConverter.IsLittleEndian)
            return NormalizeCrtCarryRangeScalar(
                transformedSecond, crtScratch, useInverseTailScratch,
                inverseTailScratchStart, relativeStart, destination,
                destinationStart, count, incomingCarry, cancellationToken);

        ref ulong source = ref Unsafe.NullRef<ulong>();
        if (useInverseTailScratch)
        {
            source = ref Unsafe.As<uint, ulong>(
                ref Unsafe.Add(
                    ref MemoryMarshal.GetArrayDataReference(transformedSecond),
                    inverseTailScratchStart + relativeStart * 2));
        }
        else
        {
            source = ref Unsafe.Add(
                ref MemoryMarshal.GetArrayDataReference(crtScratch!),
                relativeStart);
        }

        ulong carry = incomingCarry;
        int offset = 0;

        if ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512Crt) &&
            Avx512F.IsSupported && count >= Vector512<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector512<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector512<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            int end = count & ~(Vector512<ulong>.Count - 1);
            for (; offset < end; offset += Vector512<ulong>.Count)
            {
                Vector512<ulong> values = Vector512.LoadUnsafe(ref source, (nuint)offset);
                DecomposeBase10000Avx512(values, out var qv, out var rv);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileCarryDigits(q, r, destination, destinationStart + offset, carry);
            }
        }
        else if (workers.UseAvx2Ntt && Avx2.IsSupported && count >= Vector256<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector256<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector256<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            int end = count & ~(Vector256<ulong>.Count - 1);
            for (; offset < end; offset += Vector256<ulong>.Count)
            {
                Vector256<ulong> values = Vector256.LoadUnsafe(ref source, (nuint)offset);
                DecomposeBase10000Avx2(values, out var qv, out var rv);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileCarryDigits(q, r, destination, destinationStart + offset, carry);
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported && count >= Vector128<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector128<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector128<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            int end = count & ~(Vector128<ulong>.Count - 1);
            for (; offset < end; offset += Vector128<ulong>.Count)
            {
                Vector128<ulong> values = Vector128.LoadUnsafe(ref source, (nuint)offset);
                DecomposeBase10000Sse(values, out var qv, out var rv);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileCarryDigits(q, r, destination, destinationStart + offset, carry);
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated) && count >= Vector128<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector128<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector128<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            int end = count & ~(Vector128<ulong>.Count - 1);
            for (; offset < end; offset += Vector128<ulong>.Count)
            {
                Vector128<ulong> values = Vector128.LoadUnsafe(ref source, (nuint)offset);
                DecomposeBase10000Neon(values, out var qv, out var rv);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileCarryDigits(q, r, destination, destinationStart + offset, carry);
            }
        }
#endif

        for (; offset < count; offset++)
        {
            ulong coefficient = Unsafe.Add(ref source, offset);
            ulong value = coefficient + carry;
            ulong quotient = value / LimbBase;
            destination[destinationStart + offset] =
                (uint)(value - quotient * LimbBase);
            carry = quotient;
        }

        return carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong NormalizeCoefficientCarryRangeSimd(
        ulong[] sourceArray,
        int sourceStart,
        uint[] destination,
        int destinationStart,
        int count,
        ulong incomingCarry,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
            return incomingCarry;

        ref ulong source = ref Unsafe.Add(
            ref MemoryMarshal.GetArrayDataReference(sourceArray),
            sourceStart);

        ulong carry = incomingCarry;
        int offset = 0;

        if (BitConverter.IsLittleEndian &&
            (workers.UseAvx512Ntt || workers.UseLargeModeAvx512Crt) &&
            Avx512F.IsSupported && count >= Vector512<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector512<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector512<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            int end = count & ~(Vector512<ulong>.Count - 1);

            for (; offset < end; offset += Vector512<ulong>.Count)
            {
                if ((offset & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                Vector512<ulong> values =
                    Vector512.LoadUnsafe(ref source, (nuint)offset);
                DecomposeBase10000Avx512(values, out var qv, out var rv);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileCarryDigits(
                    q, r, destination, destinationStart + offset, carry);
            }
        }
        else if (BitConverter.IsLittleEndian &&
                 workers.UseAvx2Ntt && Avx2.IsSupported &&
                 count >= Vector256<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector256<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector256<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            int end = count & ~(Vector256<ulong>.Count - 1);

            for (; offset < end; offset += Vector256<ulong>.Count)
            {
                if ((offset & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                Vector256<ulong> values =
                    Vector256.LoadUnsafe(ref source, (nuint)offset);
                DecomposeBase10000Avx2(values, out var qv, out var rv);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileCarryDigits(
                    q, r, destination, destinationStart + offset, carry);
            }
        }
        else if (BitConverter.IsLittleEndian &&
                 workers.UseSseNtt && Sse2.IsSupported &&
                 count >= Vector128<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector128<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector128<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            int end = count & ~(Vector128<ulong>.Count - 1);

            for (; offset < end; offset += Vector128<ulong>.Count)
            {
                if ((offset & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                Vector128<ulong> values =
                    Vector128.LoadUnsafe(ref source, (nuint)offset);
                DecomposeBase10000Sse(values, out var qv, out var rv);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileCarryDigits(
                    q, r, destination, destinationStart + offset, carry);
            }
        }
#if ANDROID
        else if (BitConverter.IsLittleEndian &&
                 workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated) &&
                 count >= Vector128<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector128<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector128<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            int end = count & ~(Vector128<ulong>.Count - 1);

            for (; offset < end; offset += Vector128<ulong>.Count)
            {
                if ((offset & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                Vector128<ulong> values =
                    Vector128.LoadUnsafe(ref source, (nuint)offset);
                DecomposeBase10000Neon(values, out var qv, out var rv);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileCarryDigits(
                    q, r, destination, destinationStart + offset, carry);
            }
        }
#endif

        for (; offset < count; offset++)
        {
            if ((offset & 0xFFFF) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            ulong value =
                Unsafe.Add(ref source, offset) + carry;
            ulong quotient = value / LimbBase;
            destination[destinationStart + offset] =
                (uint)(value - quotient * LimbBase);
            carry = quotient;
        }

        return carry;
    }

    private static ulong NormalizeCrtCarryRangeScalar(
        uint[] transformedSecond,
        ulong[]? crtScratch,
        bool useInverseTailScratch,
        int inverseTailScratchStart,
        int relativeStart,
        uint[] destination,
        int destinationStart,
        int count,
        ulong incomingCarry,
        CancellationToken cancellationToken)
    {
        ulong carry = incomingCarry;
        for (int offset = 0; offset < count; offset++)
        {
            if ((offset & 0xFFFF) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            int relativeIndex = relativeStart + offset;
            ulong coefficient = useInverseTailScratch
                ? ReadPackedUInt64(transformedSecond, inverseTailScratchStart + relativeIndex * 2)
                : crtScratch![relativeIndex];
            ulong value = coefficient + carry;
            ulong quotient = value / LimbBase;
            destination[destinationStart + offset] = (uint)(value - quotient * LimbBase);
            carry = quotient;
        }
        return carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool WouldNormalizedRangeOverflow(
        uint[] destination,
        int destinationStart,
        int count,
        ulong carry,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        if (carry == 0 || count <= 0)
            return false;

        int end = checked(destinationStart + count);
        int index = destinationStart;

        // Reduce a multi-limb carry exactly as AddCarryToNormalizedRange does,
        // but do not mutate the already-normalized tile. A UInt64 carry falls
        // to one/zero after only a handful of base-10,000 limbs.
        while (carry > 1 && index < end)
        {
            ulong value = destination[index] + carry;
            carry = value / LimbBase;
            index++;
        }

        if (carry == 0)
            return false;

        if (index >= end)
            return true;

        // carry == 1 here. It escapes the tile iff every remaining digit is
        // 9,999. Scan read-only with the same ISA preference used by the
        // mutating carry injector; AVX-512 deliberately uses the compact YMM
        // compare because this prefix scan only needs equality + movemask.
        ref uint destinationRef =
            ref MemoryMarshal.GetArrayDataReference(destination);

        if ((workers.UseAvx512Ntt || workers.UseAvx2Ntt) && Avx2.IsSupported)
        {
            Vector256<int> max = Vector256.Create((int)(LimbBase - 1));
            int vectorEnd = end - Vector256<uint>.Count + 1;
            while (index < vectorEnd)
            {
                Vector256<uint> values =
                    Vector256.LoadUnsafe(ref destinationRef, (nuint)index);
                Vector256<int> equal =
                    Avx2.CompareEqual(values.AsInt32(), max);
                if (Avx2.MoveMask(equal.AsByte()) != -1)
                    return false;

                index += Vector256<uint>.Count;
                if ((index & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported)
        {
            Vector128<int> max = Vector128.Create((int)(LimbBase - 1));
            int vectorEnd = end - Vector128<uint>.Count + 1;
            while (index < vectorEnd)
            {
                Vector128<uint> values =
                    Vector128.LoadUnsafe(ref destinationRef, (nuint)index);
                Vector128<int> equal =
                    Sse2.CompareEqual(values.AsInt32(), max);
                if (Sse2.MoveMask(equal.AsByte()) != 0xFFFF)
                    return false;

                index += Vector128<uint>.Count;
                if ((index & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && AdvSimd.IsSupported)
        {
            Vector128<uint> max = Vector128.Create(LimbBase - 1);
            int vectorEnd = end - Vector128<uint>.Count + 1;
            while (index < vectorEnd)
            {
                Vector128<uint> values =
                    Vector128.LoadUnsafe(ref destinationRef, (nuint)index);
                Vector128<uint> equal = AdvSimd.CompareEqual(values, max);
                if (equal.GetElement(0) != uint.MaxValue ||
                    equal.GetElement(1) != uint.MaxValue ||
                    equal.GetElement(2) != uint.MaxValue ||
                    equal.GetElement(3) != uint.MaxValue)
                {
                    return false;
                }

                index += Vector128<uint>.Count;
                if ((index & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }
#endif

        while (index < end)
        {
            if (destination[index] != LimbBase - 1)
                return false;
            index++;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int SkipNormalizedMaxLimbsSimd(
        uint[] destination,
        int index,
        int end,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        ref uint destinationRef =
            ref MemoryMarshal.GetArrayDataReference(destination);

        // AVX-512 machines also expose AVX2 on the supported x64 targets used
        // by this application.  Using the YMM compare here avoids depending on
        // AVX-512 mask extraction APIs for a tiny carry-prefix scan while still
        // skipping eight normalized limbs per instruction group.
        if ((workers.UseAvx512Ntt || workers.UseAvx2Ntt) && Avx2.IsSupported)
        {
            Vector256<int> max = Vector256.Create((int)(LimbBase - 1));
            Vector256<uint> zero = Vector256<uint>.Zero;
            int vectorEnd = end - Vector256<uint>.Count + 1;
            while (index < vectorEnd)
            {
                Vector256<uint> values =
                    Vector256.LoadUnsafe(ref destinationRef, (nuint)index);
                Vector256<int> equal =
                    Avx2.CompareEqual(values.AsInt32(), max);
                if (Avx2.MoveMask(equal.AsByte()) != -1)
                    break;

                zero.StoreUnsafe(ref destinationRef, (nuint)index);
                index += Vector256<uint>.Count;
                if ((index & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported)
        {
            Vector128<int> max = Vector128.Create((int)(LimbBase - 1));
            Vector128<uint> zero = Vector128<uint>.Zero;
            int vectorEnd = end - Vector128<uint>.Count + 1;
            while (index < vectorEnd)
            {
                Vector128<uint> values =
                    Vector128.LoadUnsafe(ref destinationRef, (nuint)index);
                Vector128<int> equal =
                    Sse2.CompareEqual(values.AsInt32(), max);
                if (Sse2.MoveMask(equal.AsByte()) != 0xFFFF)
                    break;

                zero.StoreUnsafe(ref destinationRef, (nuint)index);
                index += Vector128<uint>.Count;
                if ((index & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && AdvSimd.IsSupported)
        {
            Vector128<uint> max = Vector128.Create(LimbBase - 1);
            Vector128<uint> zero = Vector128<uint>.Zero;
            int vectorEnd = end - Vector128<uint>.Count + 1;
            while (index < vectorEnd)
            {
                Vector128<uint> values =
                    Vector128.LoadUnsafe(ref destinationRef, (nuint)index);
                Vector128<uint> equal = AdvSimd.CompareEqual(values, max);
                if (equal.GetElement(0) != uint.MaxValue ||
                    equal.GetElement(1) != uint.MaxValue ||
                    equal.GetElement(2) != uint.MaxValue ||
                    equal.GetElement(3) != uint.MaxValue)
                {
                    break;
                }

                zero.StoreUnsafe(ref destinationRef, (nuint)index);
                index += Vector128<uint>.Count;
                if ((index & 0xFFFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }
#endif

        // At most one vector-width remains before the first non-9,999 limb.
        // Keep this bounded scalar cleanup instead of issuing masked stores,
        // which are unavailable on SSE2/NEON and slower for such a short tail.
        while (index < end && destination[index] == LimbBase - 1)
        {
            destination[index] = 0;
            index++;
        }

        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetPackedCarryBit(ulong[] words, int bitIndex) =>
        (words[bitIndex >> 6] >> (bitIndex & 63)) & 1UL;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void PrefixGeneratePropagatePackedWords(
        ulong[] generateWords,
        ulong[] propagateWords,
        int count)
    {
        if (count <= 1)
            return;

        int wordCount = (count + 63) >> 6;
        int validTailBits = count & 63;
        ulong tailMask = validTailBits == 0
            ? ulong.MaxValue
            : (1UL << validTailBits) - 1UL;

        for (int distance = 1; distance < count; distance <<= 1)
        {
            int wordShift = distance >> 6;
            int bitShift = distance & 63;

            // High-to-low keeps every lower source word on the previous prefix
            // round while composing the destination word in place.
            for (int word = wordCount - 1; word >= 0; word--)
            {
                ulong oldG = generateWords[word];
                ulong oldP = propagateWords[word];
                ulong shiftedG = 0;
                ulong shiftedP = 0;
                int sourceWord = word - wordShift;
                if (sourceWord >= 0)
                {
                    shiftedG = generateWords[sourceWord] << bitShift;
                    shiftedP = propagateWords[sourceWord] << bitShift;
                    if (bitShift != 0 && sourceWord > 0)
                    {
                        shiftedG |= generateWords[sourceWord - 1] >> (64 - bitShift);
                        shiftedP |= propagateWords[sourceWord - 1] >> (64 - bitShift);
                    }
                }

                int wordStart = word << 6;
                ulong targetMask;
                if (distance <= wordStart)
                {
                    targetMask = ulong.MaxValue;
                }
                else if (distance >= wordStart + 64)
                {
                    targetMask = 0;
                }
                else
                {
                    int preserved = distance - wordStart;
                    targetMask = ~((1UL << preserved) - 1UL);
                }

                ulong newG = oldG | (oldP & shiftedG & targetMask);
                ulong newP = (oldP & ~targetMask) |
                             ((oldP & shiftedP) & targetMask);
                if (word == wordCount - 1)
                {
                    newG &= tailMask;
                    newP &= tailMask;
                }

                generateWords[word] = newG;
                propagateWords[word] = newP;
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetCarryLeafLength(FixedWorkerTeam workers)
    {
        if ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512Crt) &&
            Avx512F.IsSupported)
            return Vector512<ulong>.Count; // 8 exact coefficients
        if (workers.UseAvx2Ntt && Avx2.IsSupported)
            return Vector256<ulong>.Count; // 4
        if (workers.UseSseNtt && Sse2.IsSupported)
            return 4; // Two XMM vectors: base-10,000 carry needs >=4 digits to collapse to a 0/1 transfer.
#if ANDROID
        if (workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated))
            return 4; // Two NEON vectors for the same carry-absorption guarantee as SSE.
#endif
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetNormalizedRangeOverflowExact(
        uint[] destination,
        int destinationStart,
        int count,
        ulong carry)
    {
        int end = checked(destinationStart + count);
        for (int index = destinationStart; index < end && carry != 0; index++)
        {
            ulong value = destination[index] + carry;
            carry = value / LimbBase;
        }

        return carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong ReconcileSmallNormalizedSubtilesSequential(
        uint[] destination,
        int destinationStart,
        int count,
        ulong incomingCarry,
        ReadOnlySpan<ulong> subtileCarries,
        int subtileLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        ulong carry = incomingCarry;
        for (int subtile = 0; subtile < subtileCarries.Length; subtile++)
        {
            int relativeStart = checked(subtile * subtileLength);
            int localCount = Math.Min(subtileLength, count - relativeStart);
            ulong overflow = carry == 0
                ? 0
                : AddCarryToNormalizedRange(
                    destination, destinationStart + relativeStart, localCount,
                    carry, workers, cancellationToken);
            carry = checked(subtileCarries[subtile] + overflow);
        }

        return carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong ReconcileSmallNormalizedSubtiles(
        uint[] destination,
        int destinationStart,
        int count,
        ulong incomingCarry,
        ReadOnlySpan<ulong> subtileCarries,
        int subtileLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        int subtileCount = subtileCarries.Length;
        if (subtileCount == 1)
        {
            ulong overflow = incomingCarry == 0 ? 0 : AddCarryToNormalizedRange(
                destination, destinationStart, count, incomingCarry,
                workers, cancellationToken);
            return checked(subtileCarries[0] + overflow);
        }

        int carryBitCount = subtileCount - 1;
        Debug.Assert(carryBitCount <= 63);
        ulong generateMask = 0;
        ulong propagateMask = 0;
        ulong activeMask = (1UL << carryBitCount) - 1UL;

        int firstCount = Math.Min(subtileLength, count);
        ulong firstOverflow = GetNormalizedRangeOverflowExact(
            destination, destinationStart, firstCount, incomingCarry);
        if (firstOverflow > 1)
        {
            return ReconcileSmallNormalizedSubtilesSequential(
                destination, destinationStart, count, incomingCarry,
                subtileCarries, subtileLength, workers, cancellationToken);
        }
        if (firstOverflow != 0)
            generateMask |= 1UL;

        for (int subtile = 1; subtile < carryBitCount; subtile++)
        {
            int relativeStart = subtile * subtileLength;
            int localCount = Math.Min(subtileLength, count - relativeStart);
            ulong baseCarry = subtileCarries[subtile - 1];
            ulong overflow0 = GetNormalizedRangeOverflowExact(
                destination, destinationStart + relativeStart, localCount, baseCarry);
            ulong overflow1 = GetNormalizedRangeOverflowExact(
                destination, destinationStart + relativeStart, localCount,
                checked(baseCarry + 1UL));

            // A one-bit G/P transfer is valid only after the incoming carry has
            // collapsed to 0/1 across this subtile.  This is guaranteed for the
            // normal >=4-digit leaves used by x86/ARM SIMD, but keep an exact
            // local fallback for unusually large coefficients or short tails.
            if (overflow0 > 1 || overflow1 > 1)
            {
                return ReconcileSmallNormalizedSubtilesSequential(
                    destination, destinationStart, count, incomingCarry,
                    subtileCarries, subtileLength, workers, cancellationToken);
            }

            ulong bit = 1UL << subtile;
            if (overflow0 != 0)
            {
                generateMask |= bit;
            }
            else if (overflow1 != 0)
            {
                propagateMask |= bit;
            }
        }

        for (int distance = 1; distance < carryBitCount; distance <<= 1)
        {
            ulong oldG = generateMask;
            ulong oldP = propagateMask;
            ulong shiftedG = (oldG << distance) & activeMask;
            ulong shiftedP = (oldP << distance) & activeMask;
            ulong lowerMask = (1UL << distance) - 1UL;
            generateMask = (oldG | (oldP & shiftedG)) & activeMask;
            propagateMask = ((oldP & shiftedP) | (oldP & lowerMask)) & activeMask;
        }

        ulong finalOverflow = 0;
        int last = subtileCount - 1;
        for (int subtile = 0; subtile < subtileCount; subtile++)
        {
            int relativeStart = subtile * subtileLength;
            int localCount = Math.Min(subtileLength, count - relativeStart);
            ulong subtileInput = subtile == 0
                ? incomingCarry
                : checked(subtileCarries[subtile - 1] +
                          ((generateMask >> (subtile - 1)) & 1UL));
            ulong overflow = subtileInput == 0 ? 0 : AddCarryToNormalizedRange(
                destination, destinationStart + relativeStart, localCount,
                subtileInput, workers, cancellationToken);
            if (subtile < last)
            {
                Debug.Assert(overflow == ((generateMask >> subtile) & 1UL));
            }
            else
            {
                finalOverflow = overflow;
            }
        }

        return checked(subtileCarries[last] + finalOverflow);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong NormalizeCrtCarryRangeLeafLookahead(
        uint[] transformedSecond,
        ulong[]? crtScratch,
        bool useInverseTailScratch,
        int inverseTailScratchStart,
        int relativeStart,
        uint[] destination,
        int destinationStart,
        int count,
        ulong incomingCarry,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        Debug.Assert(count <= HierarchicalCarryTileLength);
        int leafLength = GetCarryLeafLength(workers);
        if (leafLength <= 1 || count <= leafLength)
        {
            return NormalizeCrtCarryRangeSimd(
                transformedSecond, crtScratch, useInverseTailScratch,
                inverseTailScratchStart, relativeStart, destination,
                destinationStart, count, incomingCarry, workers, cancellationToken);
        }

        int subtileCount = (count + leafLength - 1) / leafLength;
        Span<ulong> carries = stackalloc ulong[64];
        Debug.Assert(subtileCount <= carries.Length);
        for (int subtile = 0; subtile < subtileCount; subtile++)
        {
            int localStart = subtile * leafLength;
            int localCount = Math.Min(leafLength, count - localStart);
            carries[subtile] = NormalizeCrtCarryRangeSimd(
                transformedSecond, crtScratch, useInverseTailScratch,
                inverseTailScratchStart, relativeStart + localStart,
                destination, destinationStart + localStart, localCount, 0,
                workers, cancellationToken);
        }

        return ReconcileSmallNormalizedSubtiles(
            destination, destinationStart, count, incomingCarry,
            carries[..subtileCount], leafLength, workers, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong NormalizeCoefficientCarryRangeLeafLookahead(
        ulong[] coefficients,
        int sourceStart,
        uint[] destination,
        int destinationStart,
        int count,
        ulong incomingCarry,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        Debug.Assert(count <= HierarchicalCarryTileLength);
        int leafLength = GetCarryLeafLength(workers);
        if (leafLength <= 1 || count <= leafLength)
        {
            return NormalizeCoefficientCarryRangeSimd(
                coefficients, sourceStart, destination, destinationStart,
                count, incomingCarry, workers, cancellationToken);
        }

        int subtileCount = (count + leafLength - 1) / leafLength;
        Span<ulong> carries = stackalloc ulong[64];
        Debug.Assert(subtileCount <= carries.Length);
        for (int subtile = 0; subtile < subtileCount; subtile++)
        {
            int localStart = subtile * leafLength;
            int localCount = Math.Min(leafLength, count - localStart);
            carries[subtile] = NormalizeCoefficientCarryRangeSimd(
                coefficients, sourceStart + localStart,
                destination, destinationStart + localStart,
                localCount, 0, workers, cancellationToken);
        }

        return ReconcileSmallNormalizedSubtiles(
            destination, destinationStart, count, incomingCarry,
            carries[..subtileCount], leafLength, workers, cancellationToken);
    }
    #endregion

    #region Segment accumulation
    // Inputs are normalized base-10,000 limbs and multiplicity is 1 or 2.
    // Raw sums <=29,997 need at most two subtractions. Carry stays <=2.
    // Only the independent raw sum/decomposition is vectorized; inter-lane
    // carry and inter-tile reconciliation preserve the original ordering.
    private static ulong AccumulateNormalizedSegmentTile(
        uint[] destination, int destinationStart, uint[] product, int productStart,
        int count, int multiplicity, FixedWorkerTeam workers, CancellationToken token)
    {
        if (destinationStart < 0 || count > destination.Length - destinationStart)
            throw new InvalidOperationException("Segmented NTT accumulation exceeded the result buffer.");
        int offset = 0;
        ulong carry = 0;
        bool vector = Vector128.IsHardwareAccelerated &&
            ((workers.UseAvx2Ntt && Avx2.IsSupported) ||
             (workers.UseSseNtt && Sse2.IsSupported) || workers.UseNeonNtt);
        if ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512Pointwise) && Avx512F.IsSupported && count >= 16)
        {
            ref uint dst = ref MemoryMarshal.GetArrayDataReference(destination);
            ref uint src = ref MemoryMarshal.GetArrayDataReference(product);
            var limit = Vector512.Create((int)LimbBase - 1);
            var radix = Vector512.Create(LimbBase);
            var one = Vector512.Create(1u);
            Span<uint> remainders = stackalloc uint[16];
            Span<uint> quotients = stackalloc uint[16];
            for (; offset <= count - 16; offset += 16)
            {
                if ((offset & 0xFFFF) == 0) token.ThrowIfCancellationRequested();
                var p = Vector512.LoadUnsafe(ref src, (nuint)(productStart + offset));
                if (multiplicity == 2) p += p;
                var sum = Vector512.LoadUnsafe(ref dst, (nuint)(destinationStart + offset)) + p;
                var first = Vector512.GreaterThan(sum.AsInt32(), limit).AsUInt32();
                sum -= first & radix;
                var second = Vector512.GreaterThan(sum.AsInt32(), limit).AsUInt32();
                sum -= second & radix;
                var q = (first & one) + (second & one);
                sum.CopyTo(remainders);
                q.CopyTo(quotients);
                for (int lane = 0; lane < 16; lane++)
                {
                    ulong digit = remainders[lane] + carry;
                    bool overflow = digit >= LimbBase;
                    destination[destinationStart + offset + lane] =
                        (uint)(overflow ? digit - LimbBase : digit);
                    carry = quotients[lane] + (overflow ? 1UL : 0UL);
                }
            }
        }
        if (workers.UseAvx2Ntt && Avx2.IsSupported && count - offset >= 8)
        {
            ref uint dst = ref MemoryMarshal.GetArrayDataReference(destination);
            ref uint src = ref MemoryMarshal.GetArrayDataReference(product);
            var limit = Vector256.Create((int)LimbBase - 1);
            var radix = Vector256.Create(LimbBase);
            var one = Vector256.Create(1u);
            Span<uint> remainders = stackalloc uint[8];
            Span<uint> quotients = stackalloc uint[8];
            for (; offset <= count - 8; offset += 8)
            {
                if ((offset & 0xFFFF) == 0) token.ThrowIfCancellationRequested();
                var p = Vector256.LoadUnsafe(ref src, (nuint)(productStart + offset));
                if (multiplicity == 2) p += p;
                var sum = Vector256.LoadUnsafe(ref dst, (nuint)(destinationStart + offset)) + p;
                var first = Vector256.GreaterThan(sum.AsInt32(), limit).AsUInt32();
                sum -= first & radix;
                var second = Vector256.GreaterThan(sum.AsInt32(), limit).AsUInt32();
                sum -= second & radix;
                var q = (first & one) + (second & one);
                sum.CopyTo(remainders);
                q.CopyTo(quotients);
                for (int lane = 0; lane < 8; lane++)
                {
                    ulong digit = remainders[lane] + carry;
                    bool overflow = digit >= LimbBase;
                    destination[destinationStart + offset + lane] =
                        (uint)(overflow ? digit - LimbBase : digit);
                    carry = quotients[lane] + (overflow ? 1UL : 0UL);
                }
            }
        }
        if (vector && count - offset >= 4)
        {
            ref uint dst = ref MemoryMarshal.GetArrayDataReference(destination);
            ref uint src = ref MemoryMarshal.GetArrayDataReference(product);
            var limit = Vector128.Create((int)LimbBase - 1);
            var radix = Vector128.Create(LimbBase);
            var one = Vector128.Create(1u);
            Span<uint> remainders = stackalloc uint[4];
            Span<uint> quotients = stackalloc uint[4];
            for (; offset <= count - 4; offset += 4)
            {
                if ((offset & 0xFFFF) == 0) token.ThrowIfCancellationRequested();
                var p = Vector128.LoadUnsafe(ref src, (nuint)(productStart + offset));
                if (multiplicity == 2) p += p;
                var sum = Vector128.LoadUnsafe(ref dst, (nuint)(destinationStart + offset)) + p;
                var first = Vector128.GreaterThan(sum.AsInt32(), limit).AsUInt32();
                sum -= first & radix;
                var second = Vector128.GreaterThan(sum.AsInt32(), limit).AsUInt32();
                sum -= second & radix;
                var q = (first & one) + (second & one);
                sum.CopyTo(remainders);
                q.CopyTo(quotients);
                for (int lane = 0; lane < 4; lane++)
                {
                    ulong digit = remainders[lane] + carry;
                    bool overflow = digit >= LimbBase;
                    destination[destinationStart + offset + lane] =
                        (uint)(overflow ? digit - LimbBase : digit);
                    carry = quotients[lane] + (overflow ? 1UL : 0UL);
                }
            }
        }
        for (; offset < count; offset++)
        {
            if ((offset & 0xFFFF) == 0) token.ThrowIfCancellationRequested();
            ulong value = destination[destinationStart + offset] +
                (ulong)product[productStart + offset] * (uint)multiplicity + carry;
            carry = value / LimbBase;
            destination[destinationStart + offset] = (uint)(value - carry * LimbBase);
        }
        return carry;
    }
    #endregion
}
