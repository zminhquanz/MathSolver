using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using MathSolver.Services;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    /// <summary>
    /// Converts actual packed binary words by recursively evaluating
    /// high * 2^(32*lowWordCount) + low. No original base/exponent is accepted.
    /// BigInteger is limited to 512-word leaves; neither the root nor a large
    /// power-of-two conversion factor is ever represented as BigInteger.
    /// </summary>
    internal static ParallelBigUnsigned FromBinaryMagnitude(LargeBinaryUnsigned binary, int workerCount,
        Action<int, int>? progress, CancellationToken token, int leafWords = 512)
    {
        ArgumentNullException.ThrowIfNull(binary);
        ArgumentOutOfRangeException.ThrowIfLessThan(leafWords, 2);
        if (!BitOperations.IsPow2(leafWords) || leafWords > 512)
            throw new ArgumentOutOfRangeException(nameof(leafWords));
        token.ThrowIfCancellationRequested();
        workerCount = Math.Clamp(workerCount, 1, Math.Max(1, Environment.ProcessorCount));
        ReadOnlyMemory<uint> words = binary.Words;
        // With one worker no NTT pool, plan or team is created, including export.
        using var pool = workerCount > 1 ? new NttBufferPool(3, 4) : null;
        using var twiddles = workerCount > 1 ? new NttTwiddleBufferPool() : null;
        bool avx2 = CalculationAccelerationManager.UsePowerNttAvx2;
        using var plans = workerCount > 1
            ? new SharedNttTwiddlePlans(twiddles!, avx2, avx2 && Avx512F.IsSupported, 1 << 18) : null;
        using var workers = workerCount > 1 ? new FixedWorkerTeam(workerCount, pool!, plans!) : null;
        var diagnostics = new PowerDiagnosticsCollector();
        var powers = new Dictionary<int, ParallelBigUnsigned>();
        int leaves = Math.Max(1, checked((int)(((long)words.Length + leafWords - 1) / leafWords)));
        int completed = 0, total = checked(leaves * 2 - 1);
        ParallelBigUnsigned result = ConvertRange(0, words.Length);
        token.ThrowIfCancellationRequested();
        progress?.Invoke(total, total);
        token.ThrowIfCancellationRequested();
        return result;

        ParallelBigUnsigned MultiplyValues(ParallelBigUnsigned left, ParallelBigUnsigned right)
        {
            token.ThrowIfCancellationRequested();
            if (workers is not null) return MultiplyMemoryBounded(left, right, workers, diagnostics, token);
            uint[] product = LimbKaratsuba.MultiplyDecimal(left._limbs.AsSpan(0, left._limbCount),
                right._limbs.AsSpan(0, right._limbCount), token);
            return new(product.Length == 0 ? [0] : product, takeOwnership: true);
        }

        ParallelBigUnsigned Leaf(ReadOnlySpan<uint> value)
        {
            var native = new BigInteger(MemoryMarshal.AsBytes(value), isUnsigned: true, isBigEndian: false);
            int digits = Math.Max(1, (int)Math.Ceiling(native.GetBitLength() * Math.Log10(2)) + 1);
            return FromBigInteger(native, digits, 1, null, token);
        }

        ParallelBigUnsigned PowerOfTwoWords(int count)
        {
            token.ThrowIfCancellationRequested();
            if (powers.TryGetValue(count, out var cached)) return cached;
            ParallelBigUnsigned power;
            if (count < leafWords)
            {
                uint[] unit = new uint[count + 1];
                unit[^1] = 1;
                power = Leaf(unit);
            }
            else
            {
                var half = PowerOfTwoWords(count / 2);
                power = MultiplyValues(half, half);
            }
            powers.Add(count, power);
            return power;
        }

        ParallelBigUnsigned ConvertRange(int start, int count)
        {
            token.ThrowIfCancellationRequested();
            var span = words.Span.Slice(start, count);
            if (count <= leafWords || !span.ContainsAnyExcept(0U))
            {
                var leaf = count <= leafWords ? Leaf(span) : FromUInt64(0);
                completed += Math.Max(1, (count + leafWords - 1) / leafWords) * 2 - 1;
                progress?.Invoke(Math.Min(completed, total), total);
                token.ThrowIfCancellationRequested();
                return leaf;
            }
            // A power-of-two low width lets every branch share logarithmically
            // many conversion factors, even when the source length is irregular.
            int lowCount = (int)BitOperations.RoundUpToPowerOf2((uint)count) / 2;
            var high = ConvertRange(start + lowCount, count - lowCount);
            var low = ConvertRange(start, lowCount);
            var shifted = MultiplyValues(high, PowerOfTwoWords(lowCount));
            // Multiply may alias a cached factor for an operand of one. Addition
            // therefore owns its output and never writes into cached powers.
            uint[] sum = LimbKaratsuba.Add(shifted._limbs.AsSpan(0, shifted._limbCount),
                low._limbs.AsSpan(0, low._limbCount), LimbBase, token);
            progress?.Invoke(++completed, total);
            token.ThrowIfCancellationRequested();
            return new(sum.Length == 0 ? [0] : sum, takeOwnership: true);
        }
    }
}
