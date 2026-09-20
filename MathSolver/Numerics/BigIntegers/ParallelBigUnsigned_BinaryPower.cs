using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#if ANDROID
using System.Runtime.Intrinsics.Arm;
#endif
using MathSolver.Services;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    // Separate representation: these arrays NEVER enter decimal-limb arithmetic.
    // Both primes support 2^26. Even 2^25 terms of 65535^2 fit below P1*P2.
    internal const int MaximumBinaryTransformLength = 1 << 26;
    internal const int SmallBinaryTransformLength = 1 << 22;
    private readonly record struct BinaryMagnitude(uint[] Limbs, int Count);

    internal static int SelectBinaryTransformLength(int exponent, long availableBytes)
    {
        long limbs = ((long)exponent * 232193 / 100000 + 31) / 16 + 2;
        int length = SmallBinaryTransformLength;
        // Account for live magnitudes, four transform slots, pair output and
        // twiddles. Reserve half the available memory for runtime/UI/GC. This
        // is a conservative selection policy, not a hard process memory limit.
        while (length < MaximumBinaryTransformLength && length < limbs)
        {
            int next = length * 2;
            long workingBytes = limbs * 12 + next * 20L + (128L << 20);
            if (workingBytes > availableBytes / 2) break;
            length = next;
        }
        return length;
    }

    internal static PowerOfTenResult PowFiveAndShift(int exponent, int workerCount,
        Action<int, int>? progress, CancellationToken token,
        int maximumTransformLength = 0, bool forceLargeResult = false,
        bool cacheForward = true, bool? persistentScheduling = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        if (maximumTransformLength == 0)
        {
            var memory = GC.GetGCMemoryInfo();
            long available = Math.Max(0, memory.TotalAvailableMemoryBytes -
                Math.Max(memory.MemoryLoadBytes, GC.GetTotalMemory(false)));
            maximumTransformLength = SelectBinaryTransformLength(exponent, available);
        }
        if (maximumTransformLength < 1024 || maximumTransformLength > MaximumBinaryTransformLength ||
            !BitOperations.IsPow2(maximumTransformLength))
            throw new ArgumentOutOfRangeException(nameof(maximumTransformLength));
        // Also guard direct callers before allocating any magnitude or worker.
        PowerOfTenArithmetic.GetBinaryExponent(10, exponent);
        token.ThrowIfCancellationRequested();
        if (exponent == 0) return new(BigInteger.One, 1, null);
        workerCount = Math.Clamp(workerCount, 1, Math.Max(1, Environment.ProcessorCount));
        bool largeSchedule = persistentScheduling ?? maximumTransformLength > SmallBinaryTransformLength;
        bool avx2 = CalculationAccelerationManager.UsePowerNttAvx2;
        bool neon = !avx2 && CalculationAccelerationManager.UsePowerNttNeon;
        bool sse = !avx2 && !neon && CalculationAccelerationManager.UsePowerNttSse;
        // Match the accepted large decimal engine: static scheduling enables
        // its individually validated AVX-512 kernels rather than the legacy gate.
        bool avx512 = !largeSchedule && avx2 && (CalculationAccelerationManager.AllowAvx512 && Avx512F.IsSupported) && Vector512.IsHardwareAccelerated;
        using var pool = new NttBufferPool(maximumRetainedBufferCount: cacheForward ? 3 : 2,
            maximumLeasedBufferCount: cacheForward ? 4 : 2);
        using var twiddlePool = new NttTwiddleBufferPool();
        // Small powers retain the 16 MiB cache; large powers use up to 128 MiB
        // to match the original engine's cached global-stage coverage.
        int predictedLimbs = checked((int)(((long)exponent * 232193 / 100000 + 31) / 16 + 2));
        int predictedTransform = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(maximumTransformLength, predictedLimbs));
        int cachedHalf = Math.Max(2, Math.Min(largeSchedule ? 1 << 21 : 1 << 18, predictedTransform / 2));
        using var plans = new SharedNttTwiddlePlans(
            twiddlePool, avx2, avx512, cachedHalf,
            useSseNtt: sse, useNeonNtt: neon);
        var diagnostics = new PowerDiagnosticsCollector();
        diagnostics.ConfigureNttBackends(avx2, useSse: sse, useNeon: neon);
        BinaryMagnitude magnitude = new([5], 1);
        uint[] packedWords;
        using (var workers = new FixedWorkerTeam(workerCount, pool, plans, largeSchedule))
        {
            var arithmetic = new BinaryPowerWorkspace(workers, diagnostics, maximumTransformLength, token, cacheForward);
            int total = PowerOfTenArithmetic.OperationCount(exponent), done = 0;
            for (int bit = BitOperations.Log2((uint)exponent) - 1; bit >= 0; bit--)
            {
                token.ThrowIfCancellationRequested();
                magnitude = arithmetic.Square(magnitude);
                progress?.Invoke(++done, total);
                if (((exponent >> bit) & 1) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    magnitude = MultiplyBinaryByFive(magnitude, workers, token);
                    progress?.Invoke(++done, total);
                }
            }
            arithmetic.ReleaseScratch();
            workers.ReleaseCrtCarryScratch();
            // Release the NTT working set before packing, but keep the same
            // worker team alive to parallelize the final shift as well.
            diagnostics.ConfigureNttBufferPool(pool.CreateStatisticsSnapshot());
            pool.ReleaseCachedBuffers();
            plans.ReleasePlans();
            twiddlePool.ReleaseCachedBuffers();
            packedWords = BinaryRadix16.PackShifted(
                magnitude.Limbs, magnitude.Count, exponent,
                (count, body) => ExecuteBinaryRanges(count, workers, token, body),
                useAvx2: !workers.UsesPersistentStaticScheduling &&
                    (workers.UseAvx512Ntt || workers.UseAvx2Ntt) && Avx2.IsSupported,
                useSse2: !workers.UsesPersistentStaticScheduling &&
                    workers.UseSseNtt && Sse2.IsSupported,
                useNeon: !workers.UsesPersistentStaticScheduling && workers.UseNeonNtt,
                token: token);
            if (largeSchedule)
                diagnostics.ConfigureLargePersistentStaticScheduler(workers.PersistentGenerationCount,
                    workers.PersistentStaticRangeCount, cacheForward ? 4 : 2);
        }
        // Packing already combined conversion and shifting, avoiding an
        // intermediate BigInteger for 5^m. Large results take array ownership.
        bool large = forceLargeResult || !PowerOfTenArithmetic.CanUseBigInteger(exponent);
        LargeBinaryUnsigned? largeValue = large
            ? LargeBinaryUnsigned.FromOwnedWords(packedWords) : null;
        BigInteger? value = large ? null : new BigInteger(MemoryMarshal.AsBytes(packedWords.AsSpan()), isUnsigned: true);
        token.ThrowIfCancellationRequested();
        int operations = PowerOfTenArithmetic.OperationCount(exponent);
        progress?.Invoke(operations, operations);
        token.ThrowIfCancellationRequested();
        return new(value, workerCount, diagnostics.CreateSnapshot(workerCount),
            diagnostics.NttMultiplicationCount == 0 ? 0 : cachedHalf * (avx2 ? 64L : 32L), largeValue,
            NttTransformLimit: maximumTransformLength);
    }

    // Avoid waking a full team for tiny memory-only operations. Tile counts
    // are dispatched separately below, after checking the original limb count.
    private static void ExecuteBinaryRanges(int count, FixedWorkerTeam workers,
        CancellationToken token, Action<int, int> body)
    {
        if (count < BinaryRadix16.TileLength * 2)
        {
            token.ThrowIfCancellationRequested();
            body(0, count);
        }
        else ExecuteRanges(count, workers, token, body);
    }

    private static ulong NormalizeBinaryTiles(uint[] destination, int offset, int count,
        ulong incomingCarry, FixedWorkerTeam workers, CancellationToken token,
        Func<int, int, ulong> normalize) =>
        BinaryRadix16.NormalizeTiles(destination, offset, count, incomingCarry,
            (tiles, body) =>
            {
                int carryTileLength = workers.UsesPersistentStaticScheduling
                    ? BinaryRadix16.LegacyCarryLookaheadTileLength
                    : BinaryRadix16.CarryLookaheadTileLength;
                if (count < carryTileLength * workers.WorkerCount)
                    body(0, tiles);
                else
                    ExecuteRanges(tiles, workers, token, body);
            }, normalize, token,
            workers.UsesPersistentStaticScheduling
                ? BinaryRadix16.LegacyCarryLookaheadTileLength
                : BinaryRadix16.CarryLookaheadTileLength,
            !workers.UsesPersistentStaticScheduling &&
                workers.UseAvx512Ntt && Avx512F.IsSupported,
            !workers.UsesPersistentStaticScheduling &&
                workers.UseAvx2Ntt && Avx2.IsSupported,
            !workers.UsesPersistentStaticScheduling &&
                workers.UseSseNtt && Sse2.IsSupported,
            !workers.UsesPersistentStaticScheduling &&
                workers.UseNeonNtt);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReconcileBinaryCarryDigits(
        ReadOnlySpan<ulong> quotient,
        ReadOnlySpan<ulong> remainder,
        uint[] destination,
        int destinationStart,
        ulong carry)
    {
        for (int lane = 0; lane < quotient.Length; lane++)
        {
            ulong carryQuotient = carry >> 16;
            ulong sum = remainder[lane] + (carry & 0xffffUL);
            destination[destinationStart + lane] = (uint)(sum & 0xffffUL);
            carry = quotient[lane] + carryQuotient + (sum >> 16);
        }
        return carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong NormalizeBinaryCoefficientRangeSimd(
        ulong[] source,
        int sourceStart,
        uint[] destination,
        int destinationStart,
        int count,
        ulong incomingCarry,
        FixedWorkerTeam workers,
        CancellationToken token)
    {
        ulong carry = incomingCarry;
        int offset = 0;
        ref ulong sourceRef = ref Unsafe.Add(
            ref MemoryMarshal.GetArrayDataReference(source), sourceStart);

        if ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512Crt) &&
            Avx512F.IsSupported && count >= Vector512<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector512<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector512<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            Vector512<ulong> mask = Vector512.Create(0xffffUL);
            int end = count & ~(Vector512<ulong>.Count - 1);
            for (; offset < end; offset += Vector512<ulong>.Count)
            {
                Vector512<ulong> values = Vector512.LoadUnsafe(ref sourceRef, (nuint)offset);
                Vector512<ulong> qv = Avx512F.ShiftRightLogical(values, 16);
                Vector512<ulong> rv = Vector512.BitwiseAnd(values, mask);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileBinaryCarryDigits(q, r, destination,
                    destinationStart + offset, carry);
            }
        }
        else if (workers.UseAvx2Ntt && Avx2.IsSupported &&
                 count >= Vector256<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector256<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector256<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            Vector256<ulong> mask = Vector256.Create(0xffffUL);
            int end = count & ~(Vector256<ulong>.Count - 1);
            for (; offset < end; offset += Vector256<ulong>.Count)
            {
                Vector256<ulong> values = Vector256.LoadUnsafe(ref sourceRef, (nuint)offset);
                Vector256<ulong> qv = Avx2.ShiftRightLogical(values, 16);
                Vector256<ulong> rv = Vector256.BitwiseAnd(values, mask);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileBinaryCarryDigits(q, r, destination,
                    destinationStart + offset, carry);
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported &&
                 count >= Vector128<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector128<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector128<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            Vector128<ulong> mask = Vector128.Create(0xffffUL);
            int end = count & ~(Vector128<ulong>.Count - 1);
            for (; offset < end; offset += Vector128<ulong>.Count)
            {
                Vector128<ulong> values = Vector128.LoadUnsafe(ref sourceRef, (nuint)offset);
                Vector128<ulong> qv = Sse2.ShiftRightLogical(values, 16);
                Vector128<ulong> rv = Vector128.BitwiseAnd(values, mask);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileBinaryCarryDigits(q, r, destination,
                    destinationStart + offset, carry);
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && AdvSimd.Arm64.IsSupported &&
                 count >= Vector128<ulong>.Count)
        {
            Span<ulong> q = stackalloc ulong[Vector128<ulong>.Count];
            Span<ulong> r = stackalloc ulong[Vector128<ulong>.Count];
            ref ulong qRef = ref MemoryMarshal.GetReference(q);
            ref ulong rRef = ref MemoryMarshal.GetReference(r);
            Vector128<ulong> mask = Vector128.Create(0xffffUL);
            int end = count & ~(Vector128<ulong>.Count - 1);
            for (; offset < end; offset += Vector128<ulong>.Count)
            {
                Vector128<ulong> values = Vector128.LoadUnsafe(ref sourceRef, (nuint)offset);
                Vector128<ulong> qv = AdvSimd.ShiftRightLogical(values, 16);
                Vector128<ulong> rv = Vector128.BitwiseAnd(values, mask);
                qv.StoreUnsafe(ref qRef);
                rv.StoreUnsafe(ref rRef);
                carry = ReconcileBinaryCarryDigits(q, r, destination,
                    destinationStart + offset, carry);
            }
        }
#endif

        for (; offset < count; offset++)
        {
            if ((offset & 0x3fff) == 0) token.ThrowIfCancellationRequested();
            ulong sum = Unsafe.Add(ref sourceRef, offset) + carry;
            destination[destinationStart + offset] = (uint)(sum & 0xffffUL);
            carry = sum >> 16;
        }
        return carry;
    }

    // Preserve the accepted persistent >10M BinaryPower carry kernels.
    // The lookahead replacements below are intentionally scoped to <=10M.
[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong MultiplyBinaryByFiveRangeSimdLegacy(
        uint[] destination,
        int from,
        int to,
        FixedWorkerTeam workers,
        CancellationToken token)
    {
        uint carry = 0;
        int i = from;
        ref uint data = ref MemoryMarshal.GetArrayDataReference(destination);

        if (workers.UseAvx512Ntt && Avx512F.IsSupported && Vector512.IsHardwareAccelerated)
        {
            Span<uint> products = stackalloc uint[Vector512<uint>.Count];
            ref uint productsRef = ref MemoryMarshal.GetReference(products);
            Vector512<uint> five = Vector512.Create(5u);
            int end = to - Vector512<uint>.Count + 1;
            for (; i < end; i += Vector512<uint>.Count)
            {
                Vector512<uint> product = Avx512F.MultiplyLow(
                    Vector512.LoadUnsafe(ref data, (nuint)i), five);
                product.StoreUnsafe(ref productsRef);
                for (int lane = 0; lane < Vector512<uint>.Count; lane++)
                {
                    uint value = products[lane] + carry;
                    destination[i + lane] = value & 0xffff;
                    carry = value >> 16;
                }
            }
        }
        else if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            Span<uint> products = stackalloc uint[Vector256<uint>.Count];
            ref uint productsRef = ref MemoryMarshal.GetReference(products);
            Vector256<int> five = Vector256.Create(5);
            int end = to - Vector256<uint>.Count + 1;
            for (; i < end; i += Vector256<uint>.Count)
            {
                Vector256<uint> product = Avx2.MultiplyLow(
                    Vector256.LoadUnsafe(ref data, (nuint)i).AsInt32(), five).AsUInt32();
                product.StoreUnsafe(ref productsRef);
                for (int lane = 0; lane < Vector256<uint>.Count; lane++)
                {
                    uint value = products[lane] + carry;
                    destination[i + lane] = value & 0xffff;
                    carry = value >> 16;
                }
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported)
        {
            Span<uint> products = stackalloc uint[Vector128<uint>.Count];
            ref uint productsRef = ref MemoryMarshal.GetReference(products);
            int end = to - Vector128<uint>.Count + 1;
            for (; i < end; i += Vector128<uint>.Count)
            {
                Vector128<uint> values = Vector128.LoadUnsafe(ref data, (nuint)i);
                // SSE2 has no packed 32-bit PMULLD, but x * 5 is exactly
                // x + (x << 2) here because every radix-16 limb is <= 65535.
                Vector128<uint> product = Sse2.Add(
                    values.AsInt32(), Sse2.ShiftLeftLogical(values, 2).AsInt32()).AsUInt32();
                product.StoreUnsafe(ref productsRef);
                for (int lane = 0; lane < Vector128<uint>.Count; lane++)
                {
                    uint value = products[lane] + carry;
                    destination[i + lane] = value & 0xffff;
                    carry = value >> 16;
                }
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && AdvSimd.IsSupported)
        {
            Span<uint> products = stackalloc uint[Vector128<uint>.Count];
            ref uint productsRef = ref MemoryMarshal.GetReference(products);
            Vector128<uint> five = Vector128.Create(5u);
            int end = to - Vector128<uint>.Count + 1;
            for (; i < end; i += Vector128<uint>.Count)
            {
                Vector128<uint> product = AdvSimd.Multiply(
                    Vector128.LoadUnsafe(ref data, (nuint)i), five);
                product.StoreUnsafe(ref productsRef);
                for (int lane = 0; lane < Vector128<uint>.Count; lane++)
                {
                    uint value = products[lane] + carry;
                    destination[i + lane] = value & 0xffff;
                    carry = value >> 16;
                }
            }
        }
#endif

        for (; i < to; i++)
        {
            if (((i - from) & 0x3fff) == 0) token.ThrowIfCancellationRequested();
            uint product = destination[i] * 5 + carry;
            destination[i] = product & 0xffff;
            carry = product >> 16;
        }
        return carry;
    }

[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong AddBinaryPairRangeSimdLegacy(
        uint[] destination,
        uint[] pair,
        int destinationOffset,
        int from,
        int to,
        uint multiplier,
        FixedWorkerTeam workers,
        CancellationToken token)
    {
        ulong carry = 0;
        int i = from;
        ref uint destinationRef = ref MemoryMarshal.GetArrayDataReference(destination);
        ref uint pairRef = ref MemoryMarshal.GetArrayDataReference(pair);

        if (workers.UseAvx512Ntt && Avx512F.IsSupported && Vector512.IsHardwareAccelerated)
        {
            Span<uint> sums = stackalloc uint[Vector512<uint>.Count];
            ref uint sumsRef = ref MemoryMarshal.GetReference(sums);
            Vector512<uint> multiplierVector = Vector512.Create(multiplier);
            int end = to - Vector512<uint>.Count + 1;
            for (; i < end; i += Vector512<uint>.Count)
            {
                Vector512<uint> pairVector = Avx512F.MultiplyLow(
                    Vector512.LoadUnsafe(ref pairRef, (nuint)i),
                    multiplierVector);
                Vector512<uint> sumVector = Vector512.Add(
                    Vector512.LoadUnsafe(ref destinationRef, (nuint)(destinationOffset + i)),
                    pairVector);
                sumVector.StoreUnsafe(ref sumsRef);
                for (int lane = 0; lane < Vector512<uint>.Count; lane++)
                {
                    ulong sum = sums[lane] + carry;
                    destination[destinationOffset + i + lane] = (uint)(sum & 0xffffUL);
                    carry = sum >> 16;
                }
            }
        }
        else if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            Span<uint> sums = stackalloc uint[Vector256<uint>.Count];
            ref uint sumsRef = ref MemoryMarshal.GetReference(sums);
            Vector256<int> multiplierVector = Vector256.Create((int)multiplier);
            int end = to - Vector256<uint>.Count + 1;
            for (; i < end; i += Vector256<uint>.Count)
            {
                Vector256<uint> pairVector = Avx2.MultiplyLow(
                    Vector256.LoadUnsafe(ref pairRef, (nuint)i).AsInt32(),
                    multiplierVector).AsUInt32();
                Vector256<uint> sumVector = Avx2.Add(
                    Vector256.LoadUnsafe(ref destinationRef, (nuint)(destinationOffset + i)),
                    pairVector);
                sumVector.StoreUnsafe(ref sumsRef);
                for (int lane = 0; lane < Vector256<uint>.Count; lane++)
                {
                    ulong sum = sums[lane] + carry;
                    destination[destinationOffset + i + lane] = (uint)(sum & 0xffffUL);
                    carry = sum >> 16;
                }
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported)
        {
            Span<uint> sums = stackalloc uint[Vector128<uint>.Count];
            ref uint sumsRef = ref MemoryMarshal.GetReference(sums);
            int end = to - Vector128<uint>.Count + 1;
            for (; i < end; i += Vector128<uint>.Count)
            {
                Vector128<uint> pairVector = Vector128.LoadUnsafe(ref pairRef, (nuint)i);
                // multiplier is 1 for diagonal pairs and 2 for mirrored pairs,
                // so SSE2 shift is exact and avoids requiring SSE4.1 PMULLD.
                if (multiplier == 2)
                    pairVector = Sse2.ShiftLeftLogical(pairVector, 1);
                Vector128<uint> sumVector = Sse2.Add(
                    Vector128.LoadUnsafe(ref destinationRef, (nuint)(destinationOffset + i)).AsInt32(),
                    pairVector.AsInt32()).AsUInt32();
                sumVector.StoreUnsafe(ref sumsRef);
                for (int lane = 0; lane < Vector128<uint>.Count; lane++)
                {
                    ulong sum = sums[lane] + carry;
                    destination[destinationOffset + i + lane] = (uint)(sum & 0xffffUL);
                    carry = sum >> 16;
                }
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && AdvSimd.IsSupported)
        {
            Span<uint> sums = stackalloc uint[Vector128<uint>.Count];
            ref uint sumsRef = ref MemoryMarshal.GetReference(sums);
            Vector128<uint> multiplierVector = Vector128.Create(multiplier);
            int end = to - Vector128<uint>.Count + 1;
            for (; i < end; i += Vector128<uint>.Count)
            {
                Vector128<uint> pairVector = AdvSimd.Multiply(
                    Vector128.LoadUnsafe(ref pairRef, (nuint)i), multiplierVector);
                Vector128<uint> sumVector = AdvSimd.Add(
                    Vector128.LoadUnsafe(ref destinationRef, (nuint)(destinationOffset + i)),
                    pairVector);
                sumVector.StoreUnsafe(ref sumsRef);
                for (int lane = 0; lane < Vector128<uint>.Count; lane++)
                {
                    ulong sum = sums[lane] + carry;
                    destination[destinationOffset + i + lane] = (uint)(sum & 0xffffUL);
                    carry = sum >> 16;
                }
            }
        }
#endif

        for (; i < to; i++)
        {
            if (((i - from) & 0x3fff) == 0) token.ThrowIfCancellationRequested();
            ulong sum = destination[destinationOffset + i] + (ulong)pair[i] * multiplier + carry;
            destination[destinationOffset + i] = (uint)(sum & 0xffffUL);
            carry = sum >> 16;
        }
        return carry;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong NormalizeBoundedBinaryRawZeroCarry(
        ReadOnlySpan<uint> raw,
        uint[] destination,
        int destinationStart,
        FixedWorkerTeam workers)
    {
        int count = raw.Length;
        if (count <= 0) return 0;
        Debug.Assert(count <= BinaryRadix16.CarryLookaheadTileLength);

        Span<uint> quotient = stackalloc uint[BinaryRadix16.CarryLookaheadTileLength];
        Span<uint> carryInputs = stackalloc uint[BinaryRadix16.CarryLookaheadTileLength];
        ref uint rawRef = ref MemoryMarshal.GetReference(raw);
        ref uint quotientRef = ref MemoryMarshal.GetReference(quotient);
        int offset = 0;

        // SIMD phase 1: extract the radix-2^16 quotient for the complete
        // micro-tile.  The G/P relation between neighboring digits remains a
        // compact 64-bit control problem; the expensive data arithmetic stays
        // in vectors.
        if (workers.UseAvx512Ntt && Avx512F.IsSupported &&
            Vector512.IsHardwareAccelerated)
        {
            int end = count & ~(Vector512<uint>.Count - 1);
            for (; offset < end; offset += Vector512<uint>.Count)
            {
                Vector512<uint> values = Vector512.LoadUnsafe(ref rawRef, (nuint)offset);
                Avx512F.ShiftRightLogical(values, 16)
                    .StoreUnsafe(ref quotientRef, (nuint)offset);
            }
        }
        else if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            int end = count & ~(Vector256<uint>.Count - 1);
            for (; offset < end; offset += Vector256<uint>.Count)
            {
                Vector256<uint> values = Vector256.LoadUnsafe(ref rawRef, (nuint)offset);
                Avx2.ShiftRightLogical(values, 16)
                    .StoreUnsafe(ref quotientRef, (nuint)offset);
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported)
        {
            int end = count & ~(Vector128<uint>.Count - 1);
            for (; offset < end; offset += Vector128<uint>.Count)
            {
                Vector128<uint> values = Vector128.LoadUnsafe(ref rawRef, (nuint)offset);
                Sse2.ShiftRightLogical(values, 16)
                    .StoreUnsafe(ref quotientRef, (nuint)offset);
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && AdvSimd.IsSupported)
        {
            int end = count & ~(Vector128<uint>.Count - 1);
            for (; offset < end; offset += Vector128<uint>.Count)
            {
                Vector128<uint> values = Vector128.LoadUnsafe(ref rawRef, (nuint)offset);
                AdvSimd.ShiftRightLogical(values, 16)
                    .StoreUnsafe(ref quotientRef, (nuint)offset);
            }
        }
#endif
        for (; offset < count; offset++)
            quotient[offset] = raw[offset] >> 16;

        ulong generateMask = 0;
        ulong propagateMask = 0;
        for (int lane = 1; lane < count; lane++)
        {
            uint baseCarry = quotient[lane - 1];
            uint remainder = raw[lane] & 0xffffU;
            uint baseSum = remainder + baseCarry;
            ulong bit = 1UL << lane;
            if (baseSum >= 0x10000U)
                generateMask |= bit;
            else if (baseSum + 1U >= 0x10000U)
                propagateMask |= bit;
        }

        ulong activeMask = count == 64 ? ulong.MaxValue : (1UL << count) - 1UL;
        for (int distance = 1; distance < count; distance <<= 1)
        {
            ulong oldGenerate = generateMask;
            ulong oldPropagate = propagateMask;
            ulong shiftedGenerate = (oldGenerate << distance) & activeMask;
            ulong shiftedPropagate = (oldPropagate << distance) & activeMask;
            ulong lowerMask = (1UL << distance) - 1UL;
            generateMask = (oldGenerate | (oldPropagate & shiftedGenerate)) & activeMask;
            propagateMask = ((oldPropagate & shiftedPropagate) |
                             (oldPropagate & lowerMask)) & activeMask;
        }

        carryInputs[0] = 0;
        for (int lane = 1; lane < count; lane++)
        {
            carryInputs[lane] = quotient[lane - 1] +
                (uint)((generateMask >> (lane - 1)) & 1UL);
        }

        // SIMD phase 2: add the resolved carry-ins and mask back to radix 2^16.
        ref uint carryRef = ref MemoryMarshal.GetReference(carryInputs);
        ref uint destinationRef = ref MemoryMarshal.GetArrayDataReference(destination);
        offset = 0;
        if (workers.UseAvx512Ntt && Avx512F.IsSupported &&
            Vector512.IsHardwareAccelerated)
        {
            Vector512<uint> mask = Vector512.Create(0xffffU);
            int end = count & ~(Vector512<uint>.Count - 1);
            for (; offset < end; offset += Vector512<uint>.Count)
            {
                Vector512<uint> sum = Vector512.Add(
                    Vector512.LoadUnsafe(ref rawRef, (nuint)offset),
                    Vector512.LoadUnsafe(ref carryRef, (nuint)offset));
                Vector512.BitwiseAnd(sum, mask).StoreUnsafe(
                    ref destinationRef, (nuint)(destinationStart + offset));
            }
        }
        else if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            Vector256<uint> mask = Vector256.Create(0xffffU);
            int end = count & ~(Vector256<uint>.Count - 1);
            for (; offset < end; offset += Vector256<uint>.Count)
            {
                Vector256<uint> sum = Avx2.Add(
                    Vector256.LoadUnsafe(ref rawRef, (nuint)offset).AsInt32(),
                    Vector256.LoadUnsafe(ref carryRef, (nuint)offset).AsInt32()).AsUInt32();
                Vector256.BitwiseAnd(sum, mask).StoreUnsafe(
                    ref destinationRef, (nuint)(destinationStart + offset));
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported)
        {
            Vector128<uint> mask = Vector128.Create(0xffffU);
            int end = count & ~(Vector128<uint>.Count - 1);
            for (; offset < end; offset += Vector128<uint>.Count)
            {
                Vector128<uint> sum = Sse2.Add(
                    Vector128.LoadUnsafe(ref rawRef, (nuint)offset).AsInt32(),
                    Vector128.LoadUnsafe(ref carryRef, (nuint)offset).AsInt32()).AsUInt32();
                Vector128.BitwiseAnd(sum, mask).StoreUnsafe(
                    ref destinationRef, (nuint)(destinationStart + offset));
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && AdvSimd.IsSupported)
        {
            Vector128<uint> mask = Vector128.Create(0xffffU);
            int end = count & ~(Vector128<uint>.Count - 1);
            for (; offset < end; offset += Vector128<uint>.Count)
            {
                Vector128<uint> sum = AdvSimd.Add(
                    Vector128.LoadUnsafe(ref rawRef, (nuint)offset),
                    Vector128.LoadUnsafe(ref carryRef, (nuint)offset));
                Vector128.BitwiseAnd(sum, mask).StoreUnsafe(
                    ref destinationRef, (nuint)(destinationStart + offset));
            }
        }
#endif
        for (; offset < count; offset++)
            destination[destinationStart + offset] =
                (raw[offset] + carryInputs[offset]) & 0xffffU;

        return quotient[count - 1] + ((generateMask >> (count - 1)) & 1UL);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong MultiplyBinaryByFiveRangeSimd(
        uint[] destination,
        int from,
        int to,
        FixedWorkerTeam workers,
        CancellationToken token)
    {
        if (workers.UsesPersistentStaticScheduling)
            return MultiplyBinaryByFiveRangeSimdLegacy(
                destination, from, to, workers, token);

        int count = to - from;
        Debug.Assert(count <= BinaryRadix16.CarryLookaheadTileLength);
        Span<uint> raw = stackalloc uint[BinaryRadix16.CarryLookaheadTileLength];
        ref uint rawRef = ref MemoryMarshal.GetReference(raw);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(destination);
        int i = 0;

        if (workers.UseAvx512Ntt && Avx512F.IsSupported && Vector512.IsHardwareAccelerated)
        {
            Vector512<uint> five = Vector512.Create(5u);
            for (; i + Vector512<uint>.Count <= count; i += Vector512<uint>.Count)
            {
                Avx512F.MultiplyLow(
                    Vector512.LoadUnsafe(ref data, (nuint)(from + i)), five)
                    .StoreUnsafe(ref rawRef, (nuint)i);
            }
        }
        else if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            Vector256<int> five = Vector256.Create(5);
            for (; i + Vector256<uint>.Count <= count; i += Vector256<uint>.Count)
            {
                Avx2.MultiplyLow(
                    Vector256.LoadUnsafe(ref data, (nuint)(from + i)).AsInt32(), five)
                    .AsUInt32().StoreUnsafe(ref rawRef, (nuint)i);
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported)
        {
            for (; i + Vector128<uint>.Count <= count; i += Vector128<uint>.Count)
            {
                Vector128<uint> values = Vector128.LoadUnsafe(ref data, (nuint)(from + i));
                Sse2.Add(values.AsInt32(), Sse2.ShiftLeftLogical(values, 2).AsInt32())
                    .AsUInt32().StoreUnsafe(ref rawRef, (nuint)i);
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && AdvSimd.IsSupported)
        {
            Vector128<uint> five = Vector128.Create(5u);
            for (; i + Vector128<uint>.Count <= count; i += Vector128<uint>.Count)
            {
                AdvSimd.Multiply(
                    Vector128.LoadUnsafe(ref data, (nuint)(from + i)), five)
                    .StoreUnsafe(ref rawRef, (nuint)i);
            }
        }
#endif
        for (; i < count; i++)
            raw[i] = destination[from + i] * 5U;

        token.ThrowIfCancellationRequested();
        return NormalizeBoundedBinaryRawZeroCarry(raw[..count], destination, from, workers);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ulong AddBinaryPairRangeSimd(
        uint[] destination,
        uint[] pair,
        int destinationOffset,
        int from,
        int to,
        uint multiplier,
        FixedWorkerTeam workers,
        CancellationToken token)
    {
        if (workers.UsesPersistentStaticScheduling)
            return AddBinaryPairRangeSimdLegacy(
                destination, pair, destinationOffset, from, to, multiplier, workers, token);

        int count = to - from;
        Debug.Assert(count <= BinaryRadix16.CarryLookaheadTileLength);
        Span<uint> raw = stackalloc uint[BinaryRadix16.CarryLookaheadTileLength];
        ref uint rawRef = ref MemoryMarshal.GetReference(raw);
        ref uint destinationRef = ref MemoryMarshal.GetArrayDataReference(destination);
        ref uint pairRef = ref MemoryMarshal.GetArrayDataReference(pair);
        int i = 0;

        if (workers.UseAvx512Ntt && Avx512F.IsSupported && Vector512.IsHardwareAccelerated)
        {
            Vector512<uint> multiplierVector = Vector512.Create(multiplier);
            for (; i + Vector512<uint>.Count <= count; i += Vector512<uint>.Count)
            {
                Vector512<uint> pairVector = Avx512F.MultiplyLow(
                    Vector512.LoadUnsafe(ref pairRef, (nuint)(from + i)), multiplierVector);
                Vector512.Add(
                    Vector512.LoadUnsafe(ref destinationRef, (nuint)(destinationOffset + from + i)),
                    pairVector).StoreUnsafe(ref rawRef, (nuint)i);
            }
        }
        else if (workers.UseAvx2Ntt && Avx2.IsSupported)
        {
            Vector256<int> multiplierVector = Vector256.Create((int)multiplier);
            for (; i + Vector256<uint>.Count <= count; i += Vector256<uint>.Count)
            {
                Vector256<uint> pairVector = Avx2.MultiplyLow(
                    Vector256.LoadUnsafe(ref pairRef, (nuint)(from + i)).AsInt32(),
                    multiplierVector).AsUInt32();
                Avx2.Add(
                    Vector256.LoadUnsafe(ref destinationRef, (nuint)(destinationOffset + from + i)),
                    pairVector).StoreUnsafe(ref rawRef, (nuint)i);
            }
        }
        else if (workers.UseSseNtt && Sse2.IsSupported)
        {
            for (; i + Vector128<uint>.Count <= count; i += Vector128<uint>.Count)
            {
                Vector128<uint> pairVector = Vector128.LoadUnsafe(ref pairRef, (nuint)(from + i));
                if (multiplier == 2U)
                    pairVector = Sse2.ShiftLeftLogical(pairVector, 1);
                Sse2.Add(
                    Vector128.LoadUnsafe(ref destinationRef, (nuint)(destinationOffset + from + i)).AsInt32(),
                    pairVector.AsInt32()).AsUInt32().StoreUnsafe(ref rawRef, (nuint)i);
            }
        }
#if ANDROID
        else if (workers.UseNeonNtt && AdvSimd.IsSupported)
        {
            Vector128<uint> multiplierVector = Vector128.Create(multiplier);
            for (; i + Vector128<uint>.Count <= count; i += Vector128<uint>.Count)
            {
                Vector128<uint> pairVector = AdvSimd.Multiply(
                    Vector128.LoadUnsafe(ref pairRef, (nuint)(from + i)), multiplierVector);
                AdvSimd.Add(
                    Vector128.LoadUnsafe(ref destinationRef, (nuint)(destinationOffset + from + i)),
                    pairVector).StoreUnsafe(ref rawRef, (nuint)i);
            }
        }
#endif
        for (; i < count; i++)
        {
            raw[i] = checked(destination[destinationOffset + from + i] +
                             pair[from + i] * multiplier);
        }

        token.ThrowIfCancellationRequested();
        return NormalizeBoundedBinaryRawZeroCarry(
            raw[..count], destination, destinationOffset + from, workers);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AccumulateBinarySchoolbookRowSseExact(
        ref uint source,
        int count,
        ref ulong destination,
        uint multiplier)
    {
        int index = 0;
        for (; index + 1 < count; index += 2)
        {
            Vector128<ulong> products = MultiplyTwoBaseLimbsSse(
                ref source, index, multiplier);
            Vector128<ulong> current = Vector128.LoadUnsafe(
                ref destination, (nuint)index);
            Sse2.Add(current.AsInt64(), products.AsInt64())
                .AsUInt64()
                .StoreUnsafe(ref destination, (nuint)index);
        }

        if (index < count)
        {
            Unsafe.Add(ref destination, index) +=
                (ulong)Unsafe.Add(ref source, index) * multiplier;
        }
    }

#if ANDROID
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void AccumulateBinarySchoolbookRowNeonExact(
        ref uint source,
        int count,
        ref ulong destination,
        uint multiplier)
    {
        int index = 0;
        Vector64<uint> multiplier64 = Vector64.Create(multiplier);
        for (; index + 3 < count; index += 4)
        {
            Vector128<uint> values = Vector128.LoadUnsafe(ref source, (nuint)index);
            Vector128<ulong> lowProducts = AdvSimd.MultiplyWideningLower(
                values.GetLower(), multiplier64);
            Vector128<ulong> highProducts = AdvSimd.MultiplyWideningLower(
                values.GetUpper(), multiplier64);

            Vector128<ulong> currentLow = Vector128.LoadUnsafe(
                ref destination, (nuint)index);
            Vector128<ulong> currentHigh = Vector128.LoadUnsafe(
                ref destination, (nuint)(index + 2));

            Vector128.Add(currentLow, lowProducts)
                .StoreUnsafe(ref destination, (nuint)index);
            Vector128.Add(currentHigh, highProducts)
                .StoreUnsafe(ref destination, (nuint)(index + 2));
        }

        for (; index < count; index++)
        {
            Unsafe.Add(ref destination, index) +=
                (ulong)Unsafe.Add(ref source, index) * multiplier;
        }
    }
#endif

    private static BinaryMagnitude MultiplyBinaryByFive(BinaryMagnitude value, FixedWorkerTeam workers,
        CancellationToken token)
    {
        uint[] result = value.Limbs;
        if (result.Length <= value.Count) Array.Resize(ref result, checked(value.Count + 1));
        ulong carry = NormalizeBinaryTiles(
            result, 0, value.Count, 0, workers, token,
            (from, to) => MultiplyBinaryByFiveRangeSimd(
                result, from, to, workers, token));
        int count = value.Count;
        if (carry != 0) result[count++] = checked((uint)carry);
        return new(result, count);
    }

    private sealed class BinaryPowerWorkspace(FixedWorkerTeam workers, PowerDiagnosticsCollector diagnostics,
        int maximumTransformLength, CancellationToken token, bool cacheForward = true)
    {
        private uint[]? pairBuffer;

        internal void ReleaseScratch() => pairBuffer = null;

        internal BinaryMagnitude Square(BinaryMagnitude value)
        {
            if ((long)value.Count * value.Count <= SchoolbookWorkLimit)
            {
                var coefficients = new ulong[value.Count * 2];
                ref uint limbRef = ref MemoryMarshal.GetArrayDataReference(value.Limbs);
                ref ulong coefficientRef = ref MemoryMarshal.GetArrayDataReference(coefficients);

                for (int i = 0; i < value.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    uint limb = Unsafe.Add(ref limbRef, i);
                    Unsafe.Add(ref coefficientRef, i * 2) += (ulong)limb * limb;

                    int offDiagonalCount = value.Count - i - 1;
                    if (offDiagonalCount <= 0)
                        continue;

                    ref uint source = ref Unsafe.Add(ref limbRef, i + 1);
                    ref ulong destination = ref Unsafe.Add(ref coefficientRef, i * 2 + 1);
                    uint multiplier = checked(limb * 2U);

                    if (workers.UseAvx512Ntt && Avx512F.IsSupported &&
                        Avx512DQ.IsSupported && Avx2.IsSupported)
                    {
                        AccumulateGenericSchoolbookRowAvx512(
                            ref source, offDiagonalCount, ref destination, 0, multiplier);
                    }
                    else if (workers.UseAvx2Ntt && Avx2.IsSupported)
                    {
                        AccumulateGenericSchoolbookRowAvx2(
                            ref source, offDiagonalCount, ref destination, 0, multiplier);
                    }
                    else if (workers.UseSseNtt && Sse2.IsSupported)
                    {
                        AccumulateBinarySchoolbookRowSseExact(
                            ref source, offDiagonalCount, ref destination, multiplier);
                    }
#if ANDROID
                    else if (workers.UseNeonNtt && AdvSimd.Arm64.IsSupported)
                    {
                        AccumulateBinarySchoolbookRowNeonExact(
                            ref source, offDiagonalCount, ref destination, multiplier);
                    }
#endif
                    else
                    {
                        AccumulateGenericSchoolbookRowScalar(
                            ref source, offDiagonalCount, ref destination, 0, multiplier);
                    }
                }

                uint[] result = new uint[coefficients.Length];
                ulong carry = NormalizeBinaryTiles(
                    result, 0, coefficients.Length, 0, workers, token,
                    (from, to) => NormalizeBinaryCoefficientRangeSimd(
                        coefficients, from, result, from, to - from, 0, workers, token));
                Debug.Assert(carry == 0);
                return Trim(result, result.Length);
            }
            if (value.Count <= maximumTransformLength / 2)
                return Convolve(value, 0, value.Count, 0, value.Count, null);

            // Square symmetry halves the number of off-diagonal segment pairs.
            // One reusable compact residue/result buffer serves every pair.
            int segmentLength = maximumTransformLength / 2;
            pairBuffer ??= new uint[maximumTransformLength];
            var accumulated = new uint[checked(value.Count * 2 + 1)];
            int pairs = 0, saved = 0;
            for (int left = 0; left < value.Count; left += segmentLength)
            {
                uint[]? firstSpectrum = null, secondSpectrum = null;
                try
                {
                    int leftCount = Math.Min(segmentLength, value.Count - left);
                    // Cache only when this outer segment participates in more
                    // than its diagonal. The last short diagonal stays compact.
                    if (cacheForward && value.Count - left > segmentLength)
                    {
                        firstSpectrum = CreateSegmentForwardSpectrum(value.Limbs, left, leftCount,
                            maximumTransformLength, FirstModulus, FirstPrimitiveRoot, workers, diagnostics, token);
                        secondSpectrum = CreateSegmentForwardSpectrum(value.Limbs, left, leftCount,
                            maximumTransformLength, SecondModulus, SecondPrimitiveRoot, workers, diagnostics, token);
                    }
                    for (int right = left; right < value.Count; right += segmentLength)
                    {
                        token.ThrowIfCancellationRequested();
                        var pair = Convolve(value, left, leftCount,
                            right, Math.Min(segmentLength, value.Count - right), pairBuffer,
                            firstSpectrum, secondSpectrum);
                        AddPair(accumulated, pair, left + right, left == right ? 1U : 2U);
                        pairs++;
                        if (firstSpectrum is not null && right != left) saved += 2;
                    }
                }
                finally
                {
                    if (secondSpectrum is not null) workers.ReturnNttBuffer(secondSpectrum);
                    if (firstSpectrum is not null) workers.ReturnNttBuffer(firstSpectrum);
                }
            }
            diagnostics.ConfigureSegmentedNttMultiplication(pairs);
            diagnostics.ConfigureLargeForwardSpectrumCache(saved);
            return Trim(accumulated, accumulated.Length);
        }

        private void AddPair(uint[] destination, BinaryMagnitude pair, int offset, uint multiplier)
        {
            ulong carry = NormalizeBinaryTiles(
                destination, offset, pair.Count, 0, workers, token,
                (from, to) => AddBinaryPairRangeSimd(
                    destination, pair.Limbs, offset, from, to, multiplier, workers, token));
            int end = offset + pair.Count;
            if (BinaryRadix16.AddCarry(destination, end, destination.Length - end, carry, token) != 0)
                throw new InvalidOperationException("Binary segment accumulation overflowed.");
        }

        private BinaryMagnitude Convolve(BinaryMagnitude value, int left, int leftCount,
            int right, int rightCount, uint[]? destination,
            uint[]? firstSpectrum = null, uint[]? secondSpectrum = null)
        {
            int coefficients = leftCount + rightCount - 1;
            if (coefficients == 1)
            {
                // A segmented square can end in a one-limb diagonal pair.
                // N=1 has no inverse stage to publish a compact output buffer.
                uint product = value.Limbs[left] * value.Limbs[right];
                uint[] scalar = destination ?? new uint[2];
                scalar[0] = product & 0xffff;
                scalar[1] = product >> 16;
                return Trim(scalar, 2);
            }
            int length = (int)BitOperations.RoundUpToPowerOf2((uint)coefficients);
            if ((ulong)Math.Min(leftCount, rightCount) * 65535 * 65535 >= (ulong)FirstModulus * SecondModulus)
                throw new InvalidOperationException("Binary CRT coefficient bound exceeded.");
            bool square = left == right && leftCount == rightCount;
            diagnostics.NttMultiplicationCount++;
            uint[]? first = null;
            RunModulus(FirstModulus, FirstPrimitiveRoot, firstSpectrum, true, destination,
                (_, compact) => first = compact ?? throw new InvalidOperationException("Missing binary P1 residues."));
            ulong carry = 0;
            RunModulus(SecondModulus, SecondPrimitiveRoot, secondSpectrum, false, null,
                (second, _) =>
                {
                    // At large transforms, 64K blocks launch thousands of
                    // worker generations. Use the original engine's 1M block
                    // size (8 MiB scratch) to amortize that synchronization.
                    int blockLength = CrtCarryStreamingBlockLength;
                    ulong[] scratch = workers.GetCrtCarryScratch(Math.Min(coefficients, blockLength));
                    for (int start = 0; start < coefficients; start += scratch.Length)
                    {
                        token.ThrowIfCancellationRequested();
                        int count = Math.Min(scratch.Length, coefficients - start);
                        long stamp = Stopwatch.GetTimestamp();
                        bool useAvx512Crt =
                            (workers.UseAvx512Ntt || workers.UseLargeModeAvx512Crt) &&
                            Avx512DQ.IsSupported;

                        bool useAvx2Crt =
                            !useAvx512Crt &&
                            workers.UseAvx2Ntt &&
                            Avx2.IsSupported;

                        bool useSseCrt =
                            !useAvx512Crt &&
                            !useAvx2Crt &&
                            workers.UseSseNtt &&
                            Sse2.IsSupported;

                        bool useNeonCrt =
                            !useAvx512Crt &&
                            !useAvx2Crt &&
                            !useSseCrt &&
                            workers.UseNeonNtt;

                        int crtVectorWidth = useAvx512Crt
                            ? Vector512<uint>.Count
                            : useAvx2Crt
                                ? Vector256<uint>.Count
                                : (useSseCrt || useNeonCrt)
                                    ? Vector128<uint>.Count
                                    : 1;

                        ExecuteVectorAlignedRanges(
                            count, crtVectorWidth, workers, token, (from, to) =>
                                ReconstructCrtRange(
                                    first!.AsSpan(start + from, to - from),
                                    second.AsSpan(start + from, to - from),
                                    scratch.AsSpan(from, to - from),
                                    true,
                                    useAvx512Crt, useAvx2Crt, useSseCrt, useNeonCrt));
                        diagnostics.CrtTicks += Stopwatch.GetTimestamp() - stamp;
                        stamp = Stopwatch.GetTimestamp();
                        carry = NormalizeBinaryTiles(
                            first!, start, count, carry, workers, token,
                            (from, to) => NormalizeBinaryCoefficientRangeSimd(
                                scratch, from, first!, start + from, to - from, 0, workers, token));
                        diagnostics.CarryTicks += Stopwatch.GetTimestamp() - stamp;
                    }
                });
            if (carry > 65535) throw new InvalidOperationException("Binary carry exceeded one limb.");
            first![coefficients] = (uint)carry; // Always overwrite reused scratch's spare slot.
            return Trim(first, coefficients + 1);

            void RunModulus(uint modulus, uint root, uint[]? spectrum, bool compact, uint[]? target,
                Action<uint[], uint[]?> consume)
            {
                if (spectrum is null)
                    ConvolveModulusCore(value.Limbs, left, leftCount, value.Limbs, right, rightCount,
                        length, modulus, root, square, compact, target, workers, diagnostics, token, consume);
                else
                    ConvolveModulusWithCachedLeftSpectrum(spectrum, value.Limbs, right, rightCount,
                        coefficients, modulus, root, square, compact, target, workers, diagnostics, token, consume);
            }
        }

        private static BinaryMagnitude Trim(uint[] limbs, int count)
        {
            while (count > 1 && limbs[count - 1] == 0) count--;
            return new(limbs, count);
        }
    }
}
