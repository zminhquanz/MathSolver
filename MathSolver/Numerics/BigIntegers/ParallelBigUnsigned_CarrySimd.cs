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
        Vector64<uint> packed = Vector64.Create(words.GetElement(0), words.GetElement(2));
        return AdvSimd.MultiplyWideningLower(packed, Vector64.Create(multiplier));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> DividePackedUInt32By10000Neon(
        Vector128<ulong> value) =>
        AdvSimd.ShiftRightLogical(
            MultiplyPackedUInt32Neon(value, CarryDivide10000Magic),
            45);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecomposeBase10000Neon(
        Vector128<ulong> value,
        out Vector128<ulong> quotient,
        out Vector128<ulong> remainder)
    {
        Vector128<ulong> lowMask = Vector128.Create((ulong)uint.MaxValue);
        Vector128<ulong> high = AdvSimd.ShiftRightLogical(value, 32);
        Vector128<ulong> low = AdvSimd.And(value.AsByte(), lowMask.AsByte()).AsUInt64();
        Vector128<ulong> s = Vector128.Add(
            low,
            MultiplyPackedUInt32Neon(high, (uint)CarryTwo32Remainder));
        Vector128<ulong> sHigh = AdvSimd.ShiftRightLogical(s, 32);
        Vector128<ulong> sLow = AdvSimd.And(s.AsByte(), lowMask.AsByte()).AsUInt64();
        Vector128<ulong> t = Vector128.Add(
            sLow,
            MultiplyPackedUInt32Neon(sHigh, (uint)CarryTwo32Remainder));
        Vector128<ulong> overflow = AdvSimd.ShiftRightLogical(t, 32);
        Vector128<ulong> tLow = AdvSimd.And(t.AsByte(), lowMask.AsByte()).AsUInt64();

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
        else if (workers.UseNeonNtt && AdvSimd.Arm64.IsSupported && count >= Vector128<ulong>.Count)
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
                 workers.UseNeonNtt && AdvSimd.Arm64.IsSupported &&
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

}
