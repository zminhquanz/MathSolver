using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
        // Match the accepted large decimal engine: static scheduling enables
        // its individually validated AVX-512 kernels rather than the legacy gate.
        bool avx512 = !largeSchedule && avx2 && Avx512F.IsSupported && Vector512.IsHardwareAccelerated;
        using var pool = new NttBufferPool(maximumRetainedBufferCount: cacheForward ? 3 : 2,
            maximumLeasedBufferCount: cacheForward ? 4 : 2);
        using var twiddlePool = new NttTwiddleBufferPool();
        // Small powers retain the 16 MiB cache; large powers use up to 128 MiB
        // to match the original engine's cached global-stage coverage.
        int predictedLimbs = checked((int)(((long)exponent * 232193 / 100000 + 31) / 16 + 2));
        int predictedTransform = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(maximumTransformLength, predictedLimbs));
        int cachedHalf = Math.Max(2, Math.Min(largeSchedule ? 1 << 21 : 1 << 18, predictedTransform / 2));
        using var plans = new SharedNttTwiddlePlans(twiddlePool, avx2, avx512, cachedHalf);
        var diagnostics = new PowerDiagnosticsCollector();
        diagnostics.ConfigureNttAvx2(avx2);
        BinaryMagnitude magnitude = new([5], 1);
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
                    magnitude = MultiplyBinaryByFive(magnitude, token);
                    progress?.Invoke(++done, total);
                }
            }
            arithmetic.ReleaseScratch();
            workers.ReleaseCrtCarryScratch();
            if (largeSchedule)
                diagnostics.ConfigureLargePersistentStaticScheduler(workers.PersistentGenerationCount,
                    workers.PersistentStaticRangeCount, cacheForward ? 4 : 2);
        }
        // Drop all transform, CRT and twiddle references before packing the final
        // number. Packing combines conversion and shifting, avoiding a BigInteger
        // for 5^m plus another full-size shifted intermediate byte representation.
        diagnostics.ConfigureNttBufferPool(pool.CreateStatisticsSnapshot());
        pool.ReleaseCachedBuffers();
        plans.ReleasePlans();
        twiddlePool.ReleaseCachedBuffers();
        bool large = forceLargeResult || !PowerOfTenArithmetic.CanUseBigInteger(exponent);
        LargeBinaryUnsigned? largeValue = large
            ? LargeBinaryUnsigned.ShiftRadix16(magnitude.Limbs.AsSpan(0, magnitude.Count), exponent, token) : null;
        BigInteger? value = large ? null : PackShiftedBinary(magnitude, exponent, token);
        int operations = PowerOfTenArithmetic.OperationCount(exponent);
        progress?.Invoke(operations, operations);
        token.ThrowIfCancellationRequested();
        return new(value, workerCount, diagnostics.CreateSnapshot(workerCount),
            diagnostics.NttMultiplicationCount == 0 ? 0 : cachedHalf * (avx2 ? 64L : 32L), largeValue,
            NttTransformLimit: maximumTransformLength);
    }

    private static BinaryMagnitude MultiplyBinaryByFive(BinaryMagnitude value, CancellationToken token)
    {
        uint[] result = value.Limbs;
        if (result.Length <= value.Count) Array.Resize(ref result, checked(value.Count + 1));
        uint carry = 0;
        for (int i = 0; i < value.Count; i++)
        {
            if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
            uint product = result[i] * 5 + carry;
            result[i] = product & 0xffff;
            carry = product >> 16;
        }
        int count = value.Count;
        if (carry != 0) result[count++] = carry;
        return new(result, count);
    }

    private static BigInteger PackShiftedBinary(BinaryMagnitude value, int shift, CancellationToken token)
    {
        int limbOffset = shift / 16, bits = shift % 16;
        byte[] bytes = new byte[checked((limbOffset + value.Count + 1) * 2)];
        uint carry = 0;
        for (int i = 0; i < value.Count; i++)
        {
            if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
            uint word = (value.Limbs[i] << bits) | carry;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan((limbOffset + i) * 2), (ushort)word);
            carry = word >> 16;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan((limbOffset + value.Count) * 2), (ushort)carry);
        token.ThrowIfCancellationRequested();
        var result = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
        token.ThrowIfCancellationRequested();
        return result;
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
                for (int i = 0; i < value.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    coefficients[i * 2] += (ulong)value.Limbs[i] * value.Limbs[i];
                    for (int j = i + 1; j < value.Count; j++)
                        coefficients[i + j] += 2UL * value.Limbs[i] * value.Limbs[j];
                }
                uint[] result = new uint[coefficients.Length];
                ulong carry = 0;
                for (int i = 0; i < result.Length; i++)
                {
                    ulong sum = coefficients[i] + carry;
                    result[i] = (uint)(sum & 0xffff); carry = sum >> 16;
                }
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
            ulong carry = 0;
            int i = 0;
            for (; i < pair.Count; i++)
            {
                if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
                ulong sum = destination[offset + i] + (ulong)pair.Limbs[i] * multiplier + carry;
                destination[offset + i] = (uint)(sum & 0xffff); carry = sum >> 16;
            }
            while (carry != 0)
            {
                token.ThrowIfCancellationRequested();
                ulong sum = destination[offset + i] + carry;
                destination[offset + i++] = (uint)(sum & 0xffff); carry = sum >> 16;
            }
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
                    int blockLength = maximumTransformLength > SmallBinaryTransformLength ? 1 << 20 : 1 << 16;
                    ulong[] scratch = workers.GetCrtCarryScratch(Math.Min(coefficients, blockLength));
                    for (int start = 0; start < coefficients; start += scratch.Length)
                    {
                        token.ThrowIfCancellationRequested();
                        int count = Math.Min(scratch.Length, coefficients - start);
                        long stamp = Stopwatch.GetTimestamp();
                        ExecuteRanges(count, workers, token, (from, to) =>
                            ReconstructCrtRange(first!.AsSpan(start + from, to - from),
                                second.AsSpan(start + from, to - from), scratch.AsSpan(from, to - from),
                                (workers.UseAvx512Ntt || workers.UseLargeModeAvx512Crt) && Avx512DQ.IsSupported));
                        diagnostics.CrtTicks += Stopwatch.GetTimestamp() - stamp;
                        stamp = Stopwatch.GetTimestamp();
                        for (int i = 0; i < count; i++)
                        {
                            ulong sum = scratch[i] + carry;
                            first![start + i] = (uint)(sum & 0xffff); carry = sum >> 16;
                        }
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
