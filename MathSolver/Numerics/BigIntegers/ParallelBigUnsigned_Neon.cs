using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    // Android <=10M only (selected once by the Pow-scoped twiddle plan).
    // Cached forward DIF / inverse DIT use four uint32 lanes. Global stages,
    // pointwise products, final normalization and CRT retain their scalar path.
    private static void ExecuteCachedTilesNeon(
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
                ExecuteCachedTileNeon(values, modulus, twiddles, shoup, plan,
                    tile * tileLength, tileLength, l2Length, l1Length, inverse);
            }
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteCachedTileNeon(
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
                ExecuteCachedTileNeon(values, modulus, twiddles, shoup, plan,
                    child, childLength, l2Length, l1Length, inverse);

        for (int stage = inverse ? childLength * 2 : length;
             inverse ? stage <= length : stage > childLength;
             stage = inverse ? stage << 1 : stage >> 1)
        {
            int half = stage >> 1;
            int twiddleOffset = plan.GetOffset(half);
            if (AdvSimd.Arm64.IsSupported)
                ExecuteCachedGroupNeon(values, modulus, twiddles, shoup,
                    offset, half, twiddleOffset, inverse, offset + length);
            else
                ExecuteCachedGroupNeonPortable(values, modulus, twiddles, shoup,
                    offset, half, twiddleOffset, inverse, offset + length);
        }

        if (!inverse && !leaf)
            for (int child = offset; child < offset + length; child += childLength)
                ExecuteCachedTileNeon(values, modulus, twiddles, shoup, plan,
                    child, childLength, l2Length, l1Length, inverse);
        if (!inverse && leaf)
        {
            int quarterTurn = plan.GetOffset(2) + 1;
            ExecuteForwardLengthFourAndTwoFusedBlockShoup(values, modulus,
                twiddles[quarterTurn], shoup[quarterTurn], offset, offset + length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteCachedGroupNeon(
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
                    var raw = AdvSimd.Subtract(a, b);
                    value = AdvSimd.Add(raw, AdvSimd.And(
                        AdvSimd.ShiftRightArithmetic(raw.AsInt32(), 31).AsUInt32(), mod));
                }
                // q = high32(value * floor(root * 2^32 / p)), preserving lane order.
                var qLow = AdvSimd.ShiftRightLogical(
                    AdvSimd.MultiplyWideningLower(value.GetLower(), quotient.GetLower()), 32);
                var qHigh = AdvSimd.ShiftRightLogical(
                    AdvSimd.MultiplyWideningUpper(value, quotient), 32);
                var q = Vector128.Create(AdvSimd.ExtractNarrowingLower(qLow),
                    AdvSimd.ExtractNarrowingLower(qHigh));
                var product = AdvSimd.Subtract(AdvSimd.Multiply(value, root), AdvSimd.Multiply(q, mod));
                var reduced = AdvSimd.Subtract(product, mod);
                product = AdvSimd.Min(product, reduced);
                if (inverse) b = product;
                var sum = AdvSimd.Add(a, b);
                reduced = AdvSimd.Subtract(sum, mod);
                sum = AdvSimd.Min(sum, reduced);
                var difference = product;
                if (inverse)
                {
                    var raw = AdvSimd.Subtract(a, b);
                    difference = AdvSimd.Add(raw, AdvSimd.And(
                        AdvSimd.ShiftRightArithmetic(raw.AsInt32(), 31).AsUInt32(), mod));
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
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteCachedGroupNeonPortable(
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
                    var raw = Vector128.Subtract(a, b);
                    value = Vector128.Add(raw, Vector128.BitwiseAnd(
                        (raw.AsInt32() >> 31).AsUInt32(), mod));
                }
                // On the tested Mono runtime AdvSimd is unavailable even though
                // Vector128 is accelerated. Four scalar high32 products avoid
                // the slower widening / split16 emulation; low products, sums,
                // differences and modular correction remain vector operations.
                var q = Vector128.Create(
                    (uint)(((ulong)value.GetElement(0) * quotient.GetElement(0)) >> 32),
                    (uint)(((ulong)value.GetElement(1) * quotient.GetElement(1)) >> 32),
                    (uint)(((ulong)value.GetElement(2) * quotient.GetElement(2)) >> 32),
                    (uint)(((ulong)value.GetElement(3) * quotient.GetElement(3)) >> 32));
                var product = Vector128.Subtract(Vector128.Multiply(value, root), Vector128.Multiply(q, mod));
                var reduced = Vector128.Subtract(product, mod);
                product = reduced + ((reduced.AsInt32() >> 31).AsUInt32() & mod);
                if (inverse) b = product;
                var sum = Vector128.Add(a, b);
                reduced = Vector128.Subtract(sum, mod);
                sum = reduced + ((reduced.AsInt32() >> 31).AsUInt32() & mod);
                var difference = product;
                if (inverse)
                {
                    var raw = Vector128.Subtract(a, b);
                    difference = Vector128.Add(raw, Vector128.BitwiseAnd(
                        (raw.AsInt32() >> 31).AsUInt32(), mod));
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
