using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    private static void ExecuteCachedTilesSse(
        uint[] values, uint modulus, FixedWorkerTeam workers, NttTwiddlePlan plan,
        int tileLength, int l2Length, int l1Length, bool inverse,
        CancellationToken cancellationToken)
    {
        uint[] twiddles = inverse ? plan.InverseTwiddles : plan.ForwardTwiddles;
        uint[] shoup = (inverse ? plan.InverseShoupTwiddles : plan.ForwardShoupTwiddles)!;
        ExecuteRanges(values.Length / tileLength, workers, cancellationToken, (start, end) =>
        {
            for (int tile = start; tile < end; tile++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExecuteCachedTileSse(values, modulus, twiddles, shoup, plan,
                    tile * tileLength, tileLength, l2Length, l1Length, inverse);
            }
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteCachedTileSse(
        uint[] values, uint modulus, uint[] twiddles, uint[] shoup, NttTwiddlePlan plan,
        int offset, int length, int l2Length, int l1Length, bool inverse)
    {
        if (length == 2)
        {
            ExecuteLengthTwoSequentialBlock(values, modulus, offset, offset + length);
            return;
        }

        // Keep stages 2/4 in one block-wide radix-4 pass. Calling a generic
        // group kernel for every two/four values dominated the Debug path.
        bool leaf = length <= l1Length;
        int childLength = length > l2Length ? l2Length : !leaf ? l1Length : 4;
        if (inverse && leaf)
        {
            int quarterTurn = plan.GetOffset(2) + 1;
            ExecuteInverseLengthTwoAndFourFusedBlock(values, modulus,
                twiddles[quarterTurn], shoup[quarterTurn], offset, offset + length);
        }
        if (inverse && !leaf)
            for (int child = offset; child < offset + length; child += childLength)
                ExecuteCachedTileSse(values, modulus, twiddles, shoup, plan,
                    child, childLength, l2Length, l1Length, inverse);

        for (int stage = inverse ? childLength * 2 : length;
             inverse ? stage <= length : stage > childLength;
             stage = inverse ? stage << 1 : stage >> 1)
        {
            int half = stage >> 1;
            int twiddleOffset = plan.GetOffset(half);
            ExecuteCachedGroupSse(values, modulus, twiddles, shoup,
                offset, half, twiddleOffset, inverse, offset + length);
        }

        if (!inverse && !leaf)
            for (int child = offset; child < offset + length; child += childLength)
                ExecuteCachedTileSse(values, modulus, twiddles, shoup, plan,
                    child, childLength, l2Length, l1Length, inverse);
        if (!inverse && leaf)
        {
            int quarterTurn = plan.GetOffset(2) + 1;
            ExecuteForwardLengthFourAndTwoFusedBlockShoup(values, modulus,
                twiddles[quarterTurn], shoup[quarterTurn], offset, offset + length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteCachedGroupSse(
        uint[] values, uint modulus, uint[] twiddles, uint[] shoup,
        int group, int half, int twiddleOffset, bool inverse, int groupEnd = 0)
    {
        var mod = Vector128.Create(modulus);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
        ref uint roots = ref MemoryMarshal.GetArrayDataReference(twiddles);
        ref uint quotients = ref MemoryMarshal.GetArrayDataReference(shoup);
        // One call and one set of array references for the whole stage in this
        // tile, instead of a call for each 8/16/... element group.
        if (groupEnd == 0) groupEnd = group + half * 2;
        for (; group < groupEnd; group += half * 2)
        {
            int j = 0;
            for (; j <= half - 4; j += 4)
            {
                var a = Vector128.LoadUnsafe(ref data, (nuint)(group + j));
                var b = Vector128.LoadUnsafe(ref data, (nuint)(group + half + j));
                var root = Vector128.LoadUnsafe(ref roots, (nuint)(twiddleOffset + j));
                var quotient = Vector128.LoadUnsafe(ref quotients, (nuint)(twiddleOffset + j));
                // Keep vector arithmetic inside the loop: Debug JIT does not honor
                // helper inlining, and per-vector calls/spills outweighed SIMD gains.
                // Both primes are <2^31; canonical inputs keep differences in the
                // signed range and Shoup remainders below 2p<2^32.
                var value = b;
                if (!inverse)
                {
                    var raw = Sse2.Subtract(a, b);
                    value = Sse2.Add(raw, Sse2.And(
                        Sse2.ShiftRightArithmetic(raw.AsInt32(), 31).AsUInt32(), mod));
                }
                var oddValue = Sse2.ShiftRightLogical(value.AsUInt64(), 32).AsUInt32();
                var oddShoup = Sse2.ShiftRightLogical(quotient.AsUInt64(), 32).AsUInt32();
                var evenQ = Sse2.ShiftRightLogical(Sse2.Multiply(value, quotient), 32);
                var oddQ = Sse2.ShiftRightLogical(Sse2.Multiply(oddValue, oddShoup), 32);
                Vector128<uint> product;
                if (Sse41.IsSupported)
                {
                    var q = Sse2.Or(evenQ, Sse2.ShiftLeftLogical(oddQ, 32)).AsUInt32();
                    product = Sse2.Subtract(Sse41.MultiplyLow(value, root), Sse41.MultiplyLow(q, mod));
                }
                else
                {
                    var even = Sse2.Subtract(Sse2.Multiply(value, root), Sse2.Multiply(evenQ.AsUInt32(), mod));
                    var oddRoot = Sse2.ShiftRightLogical(root.AsUInt64(), 32).AsUInt32();
                    var odd = Sse2.Subtract(Sse2.Multiply(oddValue, oddRoot), Sse2.Multiply(oddQ.AsUInt32(), mod));
                    product = Sse2.Or(even, Sse2.ShiftLeftLogical(odd, 32)).AsUInt32();
                }
                var reduced = Sse2.Subtract(product, mod);
                product = Sse41.IsSupported ? Sse41.Min(product, reduced) :
                    Sse2.Add(reduced, Sse2.And(Sse2.ShiftRightArithmetic(reduced.AsInt32(), 31).AsUInt32(), mod));
                if (inverse) b = product;
                var sum = Sse2.Add(a, b);
                reduced = Sse2.Subtract(sum, mod);
                sum = Sse41.IsSupported ? Sse41.Min(sum, reduced) :
                    Sse2.Add(reduced, Sse2.And(Sse2.ShiftRightArithmetic(reduced.AsInt32(), 31).AsUInt32(), mod));
                var difference = product;
                if (inverse)
                {
                    var raw = Sse2.Subtract(a, b);
                    difference = Sse2.Add(raw, Sse2.And(
                        Sse2.ShiftRightArithmetic(raw.AsInt32(), 31).AsUInt32(), mod));
                }
                sum.StoreUnsafe(ref data, (nuint)(group + j));
                difference.StoreUnsafe(ref data, (nuint)(group + half + j));
            }
            for (; j < half; j++)
            {
                uint a = values[group + j], b = values[group + half + j];
                uint root = half == 1 ? 1u : twiddles[twiddleOffset + j];
                if (inverse) b = (uint)((ulong)b * root % modulus);
                uint sum = a + b;
                if (sum >= modulus) sum -= modulus;
                uint difference = a >= b ? a - b : a + modulus - b;
                if (!inverse) difference = (uint)((ulong)difference * root % modulus);
                values[group + j] = sum;
                values[group + half + j] = difference;
            }
        }
    }
}
