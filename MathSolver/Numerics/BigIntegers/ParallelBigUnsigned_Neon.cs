using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    // Android ARM64 NEON backend through exponent 100M. Cached forward DIF /
    // inverse DIT use four uint32 lanes; the large-mode extension below adds
    // global cached/uncached stages, pointwise products and CRT. Final inverse
    // prefix normalization and the radix-4 leaf stages now stay in four-lane
    // NEON registers on ARM64, with the existing scalar/portable fallbacks.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TransposeRadix4Neon(
        Vector128<uint> row0,
        Vector128<uint> row1,
        Vector128<uint> row2,
        Vector128<uint> row3,
        out Vector128<uint> column0,
        out Vector128<uint> column1,
        out Vector128<uint> column2,
        out Vector128<uint> column3)
    {
        Vector128<uint> low01 = AdvSimd.Arm64.ZipLow(row0, row1);
        Vector128<uint> high01 = AdvSimd.Arm64.ZipHigh(row0, row1);
        Vector128<uint> low23 = AdvSimd.Arm64.ZipLow(row2, row3);
        Vector128<uint> high23 = AdvSimd.Arm64.ZipHigh(row2, row3);

        column0 = AdvSimd.Arm64.ZipLow(low01.AsUInt64(), low23.AsUInt64()).AsUInt32();
        column1 = AdvSimd.Arm64.ZipHigh(low01.AsUInt64(), low23.AsUInt64()).AsUInt32();
        column2 = AdvSimd.Arm64.ZipLow(high01.AsUInt64(), high23.AsUInt64()).AsUInt32();
        column3 = AdvSimd.Arm64.ZipHigh(high01.AsUInt64(), high23.AsUInt64()).AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardLengthFourAndTwoFusedBlockNeon(
        uint[] values,
        uint modulus,
        uint quarterTurnTwiddle,
        uint quarterTurnShoup,
        int blockOffset,
        int blockEnd)
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            ExecuteForwardLengthFourAndTwoFusedBlockShoup(
                values, modulus, quarterTurnTwiddle, quarterTurnShoup,
                blockOffset, blockEnd);
            return;
        }

        Debug.Assert(((blockEnd - blockOffset) & 3) == 0);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> twiddle = Vector128.Create(quarterTurnTwiddle);
        Vector128<uint> shoup = Vector128.Create(quarterTurnShoup);

        int index = blockOffset;
        int vectorEnd = blockEnd - 15;
        for (; index <= vectorEnd; index += 16)
        {
            TransposeRadix4Neon(
                Vector128.LoadUnsafe(ref data, (nuint)index),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 4)),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 8)),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 12)),
                out Vector128<uint> value0, out Vector128<uint> value1,
                out Vector128<uint> value2, out Vector128<uint> value3);

            Vector128<uint> topSum0 = AddModuloNeon(value0, value2, mod);
            Vector128<uint> topSum1 = AddModuloNeon(value1, value3, mod);
            Vector128<uint> lower0 = SubtractModuloNeon(value0, value2, mod);
            Vector128<uint> lower1 = MultiplyShoupNeon(
                SubtractModuloNeon(value1, value3, mod), twiddle, shoup, mod);

            TransposeRadix4Neon(
                AddModuloNeon(topSum0, topSum1, mod),
                SubtractModuloNeon(topSum0, topSum1, mod),
                AddModuloNeon(lower0, lower1, mod),
                SubtractModuloNeon(lower0, lower1, mod),
                out Vector128<uint> output0, out Vector128<uint> output1,
                out Vector128<uint> output2, out Vector128<uint> output3);

            output0.StoreUnsafe(ref data, (nuint)index);
            output1.StoreUnsafe(ref data, (nuint)(index + 4));
            output2.StoreUnsafe(ref data, (nuint)(index + 8));
            output3.StoreUnsafe(ref data, (nuint)(index + 12));
        }

        if (index < blockEnd)
        {
            ExecuteForwardLengthFourAndTwoFusedBlockShoup(
                values, modulus, quarterTurnTwiddle, quarterTurnShoup,
                index, blockEnd);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseLengthTwoAndFourFusedBlockNeon(
        uint[] values,
        uint modulus,
        uint quarterTurnTwiddle,
        uint quarterTurnShoup,
        int blockOffset,
        int blockEnd)
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            ExecuteInverseLengthTwoAndFourFusedBlock(
                values, modulus, quarterTurnTwiddle, quarterTurnShoup,
                blockOffset, blockEnd);
            return;
        }

        Debug.Assert(((blockEnd - blockOffset) & 3) == 0);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> twiddle = Vector128.Create(quarterTurnTwiddle);
        Vector128<uint> shoup = Vector128.Create(quarterTurnShoup);

        int index = blockOffset;
        int vectorEnd = blockEnd - 15;
        for (; index <= vectorEnd; index += 16)
        {
            TransposeRadix4Neon(
                Vector128.LoadUnsafe(ref data, (nuint)index),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 4)),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 8)),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 12)),
                out Vector128<uint> value0, out Vector128<uint> value1,
                out Vector128<uint> value2, out Vector128<uint> value3);

            Vector128<uint> leftSum = AddModuloNeon(value0, value1, mod);
            Vector128<uint> rightSum = AddModuloNeon(value2, value3, mod);
            Vector128<uint> leftDifference = SubtractModuloNeon(value0, value1, mod);
            Vector128<uint> rightDifference = MultiplyShoupNeon(
                SubtractModuloNeon(value2, value3, mod), twiddle, shoup, mod);

            TransposeRadix4Neon(
                AddModuloNeon(leftSum, rightSum, mod),
                AddModuloNeon(leftDifference, rightDifference, mod),
                SubtractModuloNeon(leftSum, rightSum, mod),
                SubtractModuloNeon(leftDifference, rightDifference, mod),
                out Vector128<uint> output0, out Vector128<uint> output1,
                out Vector128<uint> output2, out Vector128<uint> output3);

            output0.StoreUnsafe(ref data, (nuint)index);
            output1.StoreUnsafe(ref data, (nuint)(index + 4));
            output2.StoreUnsafe(ref data, (nuint)(index + 8));
            output3.StoreUnsafe(ref data, (nuint)(index + 12));
        }

        if (index < blockEnd)
        {
            ExecuteInverseLengthTwoAndFourFusedBlock(
                values, modulus, quarterTurnTwiddle, quarterTurnShoup,
                index, blockEnd);
        }
    }

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
            ExecuteInverseLengthTwoAndFourFusedBlockNeon(values, modulus,
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
            ExecuteForwardLengthFourAndTwoFusedBlockNeon(values, modulus,
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

    // ---------------------------------------------------------------------
    // Android >10M / <=100M NEON backend
    //
    // The cache-resident kernels above were originally restricted to <=10M.
    // Large mode reuses the same 128-bit uint32 butterflies and adds global
    // cached/uncached stages, pointwise products and exact CRT reconstruction.
    // ---------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ReduceOnceNeon(
        Vector128<uint> value,
        Vector128<uint> modulus)
    {
        if (AdvSimd.IsSupported)
        {
            Vector128<uint> reduced = AdvSimd.Subtract(value, modulus);
            return AdvSimd.Min(value, reduced);
        }

        Vector128<uint> portableReduced = Vector128.Subtract(value, modulus);
        return Vector128.Add(
            portableReduced,
            Vector128.BitwiseAnd(
                (portableReduced.AsInt32() >> 31).AsUInt32(),
                modulus));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> AddModuloNeon(
        Vector128<uint> left,
        Vector128<uint> right,
        Vector128<uint> modulus) =>
        ReduceOnceNeon(
            AdvSimd.IsSupported
                ? AdvSimd.Add(left, right)
                : Vector128.Add(left, right),
            modulus);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> SubtractModuloNeon(
        Vector128<uint> left,
        Vector128<uint> right,
        Vector128<uint> modulus)
    {
        Vector128<uint> raw =
            AdvSimd.IsSupported
                ? AdvSimd.Subtract(left, right)
                : Vector128.Subtract(left, right);

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.Add(
                raw,
                AdvSimd.And(
                    AdvSimd.ShiftRightArithmetic(raw.AsInt32(), 31).AsUInt32(),
                    modulus));
        }

        return Vector128.Add(
            raw,
            Vector128.BitwiseAnd(
                (raw.AsInt32() >> 31).AsUInt32(),
                modulus));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> MultiplyShoupNeon(
        Vector128<uint> value,
        Vector128<uint> multiplier,
        Vector128<uint> multiplierShoup,
        Vector128<uint> modulus)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            Vector128<ulong> qLow =
                AdvSimd.ShiftRightLogical(
                    AdvSimd.MultiplyWideningLower(
                        value.GetLower(),
                        multiplierShoup.GetLower()),
                    32);

            Vector128<ulong> qHigh =
                AdvSimd.ShiftRightLogical(
                    AdvSimd.MultiplyWideningUpper(value, multiplierShoup),
                    32);

            Vector128<uint> quotient =
                Vector128.Create(
                    AdvSimd.ExtractNarrowingLower(qLow),
                    AdvSimd.ExtractNarrowingLower(qHigh));

            Vector128<uint> product =
                AdvSimd.Subtract(
                    AdvSimd.Multiply(value, multiplier),
                    AdvSimd.Multiply(quotient, modulus));

            return ReduceOnceNeon(product, modulus);
        }

        Vector128<uint> qPortable =
            Vector128.Create(
                (uint)(((ulong)value.GetElement(0) * multiplierShoup.GetElement(0)) >> 32),
                (uint)(((ulong)value.GetElement(1) * multiplierShoup.GetElement(1)) >> 32),
                (uint)(((ulong)value.GetElement(2) * multiplierShoup.GetElement(2)) >> 32),
                (uint)(((ulong)value.GetElement(3) * multiplierShoup.GetElement(3)) >> 32));

        return ReduceOnceNeon(
            Vector128.Subtract(
                Vector128.Multiply(value, multiplier),
                Vector128.Multiply(qPortable, modulus)),
            modulus);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> CorrectResidueBorrowNeon(
        Vector128<ulong> product,
        Vector128<ulong> quotientTimesModulus,
        ulong modulus)
    {
        Vector128<ulong> remainder =
            AdvSimd.Subtract(product, quotientTimesModulus);

        Vector128<ulong> borrowBit =
            AdvSimd.ShiftRightLogical(remainder, 63);

        Vector128<ulong> borrowMask =
            AdvSimd.Subtract(Vector128.Create(0UL), borrowBit);

        return AdvSimd.Add(
            remainder,
            AdvSimd.And(
                borrowMask,
                Vector128.Create(modulus)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> ReduceResidueProductFirstModulusNeon(
        Vector128<ulong> product)
    {
        Vector128<ulong> high = AdvSimd.ShiftRightLogical(product, 27);
        Vector128<ulong> highWord = AdvSimd.ShiftRightLogical(high, 32);
        Vector128<ulong> lowWord =
            AdvSimd.And(high, Vector128.Create((ulong)uint.MaxValue));

        Vector128<ulong> lowProduct =
            AdvSimd.MultiplyWideningLower(
                AdvSimd.ExtractNarrowingLower(lowWord),
                Vector128.Create(0x8888_8889u).GetLower());

        Vector128<ulong> lowQuotient =
            AdvSimd.ShiftRightLogical(lowProduct, 35);

        Vector128<ulong> lowQuotientTimes15 =
            AdvSimd.Subtract(
                AdvSimd.ShiftLeftLogical(lowQuotient, 4),
                lowQuotient);

        Vector128<ulong> lowRemainder =
            AdvSimd.Subtract(lowWord, lowQuotientTimes15);

        Vector128<ulong> carry =
            AdvSimd.ShiftRightLogical(
                AdvSimd.Add(
                    AdvSimd.Add(lowRemainder, highWord),
                    Vector128.Create(1UL)),
                4);

        Vector128<ulong> highContribution =
            AdvSimd.MultiplyWideningLower(
                AdvSimd.ExtractNarrowingLower(highWord),
                Vector128.Create(0x1111_1111u).GetLower());

        Vector128<ulong> quotient =
            AdvSimd.Add(
                AdvSimd.Add(highContribution, lowQuotient),
                carry);

        Vector128<ulong> quotientTimesModulus =
            AdvSimd.MultiplyWideningLower(
                AdvSimd.ExtractNarrowingLower(quotient),
                Vector128.Create(FirstModulus).GetLower());

        return CorrectResidueBorrowNeon(
            product,
            quotientTimesModulus,
            FirstModulus);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> ReduceResidueProductSecondModulusNeon(
        Vector128<ulong> product)
    {
        Vector128<ulong> high = AdvSimd.ShiftRightLogical(product, 26);

        Vector128<ulong> initialProduct =
            AdvSimd.MultiplyWideningLower(
                AdvSimd.ExtractNarrowingLower(high),
                Vector128.Create(0x2492_4925u).GetLower());

        Vector128<ulong> initialQuotient =
            AdvSimd.ShiftRightLogical(initialProduct, 32);

        Vector128<ulong> quotient =
            AdvSimd.ShiftRightLogical(
                AdvSimd.Add(
                    initialQuotient,
                    AdvSimd.ShiftRightLogical(
                        AdvSimd.Subtract(high, initialQuotient),
                        1)),
                2);

        Vector128<ulong> quotientTimesModulus =
            AdvSimd.MultiplyWideningLower(
                AdvSimd.ExtractNarrowingLower(quotient),
                Vector128.Create(SecondModulus).GetLower());

        return CorrectResidueBorrowNeon(
            product,
            quotientTimesModulus,
            SecondModulus);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> MultiplyResiduesNeon(
        Vector128<uint> left,
        Vector128<uint> right,
        uint modulus)
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            return Vector128.Create(
                (uint)((ulong)left.GetElement(0) * right.GetElement(0) % modulus),
                (uint)((ulong)left.GetElement(1) * right.GetElement(1) % modulus),
                (uint)((ulong)left.GetElement(2) * right.GetElement(2) % modulus),
                (uint)((ulong)left.GetElement(3) * right.GetElement(3) % modulus));
        }

        Vector128<ulong> productLow =
            AdvSimd.MultiplyWideningLower(left.GetLower(), right.GetLower());
        Vector128<ulong> productHigh =
            AdvSimd.MultiplyWideningUpper(left, right);

        Vector128<ulong> remainderLow = modulus == FirstModulus
            ? ReduceResidueProductFirstModulusNeon(productLow)
            : ReduceResidueProductSecondModulusNeon(productLow);

        Vector128<ulong> remainderHigh = modulus == FirstModulus
            ? ReduceResidueProductFirstModulusNeon(productHigh)
            : ReduceResidueProductSecondModulusNeon(productHigh);

        return Vector128.Create(
            AdvSimd.ExtractNarrowingLower(remainderLow),
            AdvSimd.ExtractNarrowingLower(remainderHigh));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> CreateTwiddleSequenceNeon(
        uint root,
        int first,
        uint modulus)
    {
        // ARM64 seed/setup mirrors the 128-bit x86 path: keep the lane basis
        // independent of root^first and broadcast the segment seed through one
        // packed NEON modular multiply.
        uint r2 = (uint)((ulong)root * root % modulus);
        uint r3 = (uint)((ulong)r2 * root % modulus);
        Vector128<uint> lanePowers = Vector128.Create(1u, root, r2, r3);
        uint firstPower = (uint)ModPow(root, (uint)first, modulus);
        return MultiplyResiduesNeon(
            lanePowers,
            Vector128.Create(firstPower),
            modulus);
    }



    /// <summary>
    /// ARM64 NEON version of the fused uncached global Forward-DIF stage pair
    /// S and S/2. Four adjacent butterflies are processed per Vector128 batch,
    /// with all quarter streams kept in registers and the three twiddle vectors
    /// advanced by root^4 using exact Shoup multiplication. Only the residual
    /// segment shorter than four butterflies uses the scalar dual-lane fallback.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessForwardUncachedStagePairNeon(
        uint[] values,
        uint modulus,
        uint firstRoot,
        uint secondRoot,
        uint quarterPhase,
        int stageLength,
        int groupIndex,
        int first,
        int last,
        CancellationToken cancellationToken)
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            ProcessForwardUncachedStagePairSegmentByrefDualLane(
                values, modulus, firstRoot, secondRoot, quarterPhase,
                stageLength, groupIndex, first, last, cancellationToken);
            return;
        }

        int quarterLength = stageLength >> 2;
        int groupOffset = groupIndex * stageLength;
        Vector128<uint> mod = Vector128.Create(modulus);

        Vector128<uint> twiddle0 =
            CreateTwiddleSequenceNeon(firstRoot, first, modulus);

        Vector128<uint> twiddle1 =
            MultiplyResiduesNeon(
                twiddle0,
                Vector128.Create(quarterPhase),
                modulus);

        Vector128<uint> twiddle2 =
            CreateTwiddleSequenceNeon(secondRoot, first, modulus);

        uint step0 = (uint)ModPow(firstRoot, 4u, modulus);
        uint step2 = (uint)ModPow(secondRoot, 4u, modulus);

        Vector128<uint> advance0 = Vector128.Create(step0);
        Vector128<uint> advance2 = Vector128.Create(step2);
        Vector128<uint> advance0Shoup =
            Vector128.Create((uint)(((ulong)step0 << 32) / modulus));
        Vector128<uint> advance2Shoup =
            Vector128.Create((uint)(((ulong)step2 << 32) / modulus));

        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
        int i = first;
        int sinceCancellation = 0;

        for (; i + 3 < last; i += 4)
        {
            int index0 = groupOffset + i;
            int index1 = index0 + quarterLength;
            int index2 = index1 + quarterLength;
            int index3 = index2 + quarterLength;

            Vector128<uint> value0 = Vector128.LoadUnsafe(ref data, (nuint)index0);
            Vector128<uint> value1 = Vector128.LoadUnsafe(ref data, (nuint)index1);
            Vector128<uint> value2 = Vector128.LoadUnsafe(ref data, (nuint)index2);
            Vector128<uint> value3 = Vector128.LoadUnsafe(ref data, (nuint)index3);

            Vector128<uint> topSum0 = AddModuloNeon(value0, value2, mod);
            Vector128<uint> topSum1 = AddModuloNeon(value1, value3, mod);

            Vector128<uint> lower0 =
                MultiplyResiduesNeon(
                    SubtractModuloNeon(value0, value2, mod),
                    twiddle0,
                    modulus);

            Vector128<uint> lower1 =
                MultiplyResiduesNeon(
                    SubtractModuloNeon(value1, value3, mod),
                    twiddle1,
                    modulus);

            Vector128<uint> upperSum = AddModuloNeon(topSum0, topSum1, mod);
            Vector128<uint> upperDifference = SubtractModuloNeon(topSum0, topSum1, mod);
            Vector128<uint> lowerSum = AddModuloNeon(lower0, lower1, mod);
            Vector128<uint> lowerDifference = SubtractModuloNeon(lower0, lower1, mod);

            upperSum.StoreUnsafe(ref data, (nuint)index0);
            lowerSum.StoreUnsafe(ref data, (nuint)index2);

            MultiplyResiduesNeon(upperDifference, twiddle2, modulus)
                .StoreUnsafe(ref data, (nuint)index1);
            MultiplyResiduesNeon(lowerDifference, twiddle2, modulus)
                .StoreUnsafe(ref data, (nuint)index3);

            twiddle0 = MultiplyShoupNeon(twiddle0, advance0, advance0Shoup, mod);
            twiddle1 = MultiplyShoupNeon(twiddle1, advance0, advance0Shoup, mod);
            twiddle2 = MultiplyShoupNeon(twiddle2, advance2, advance2Shoup, mod);

            sinceCancellation += 4;
            if (sinceCancellation >= (1 << 14))
            {
                cancellationToken.ThrowIfCancellationRequested();
                sinceCancellation = 0;
            }
        }

        if (i < last)
        {
            ProcessForwardUncachedStagePairSegmentByrefDualLane(
                values,
                modulus,
                firstRoot,
                secondRoot,
                quarterPhase,
                stageLength,
                groupIndex,
                i,
                last,
                cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Four-lane SIMD Forward-DIF cached global S + S/2 pair.  Each worker
    /// owns an independent quarter-stream slice, loads the four value streams
    /// once, completes both stages while resident in Vector128 registers, and
    /// consumes the existing cached twiddle + Shoup rows.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardCachedStagePairByGroupsNeon(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int firstTwiddleOffset,
        int secondTwiddleOffset,
        int stageLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        const int CancellationStride = 1 << 15;
        int halfLength = stageLength >> 1;
        int quarterLength = halfLength >> 1;
        int groupCount = values.Length / stageLength;
        int segmentsPerGroup =
            GetWorkerAlignedSegmentsPerGroup(
                quarterLength,
                groupCount,
                workers.WorkerCount,
                GetSegmentsPerGroup(
                    quarterLength,
                    groupCount,
                    workers.WorkerCount));
        Vector128<uint> mod = Vector128.Create(modulus);

        ExecuteRanges(
            checked(groupCount * segmentsPerGroup),
            workers,
            cancellationToken,
            (segmentStart, segmentEnd) =>
            {
                ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
                ref uint roots = ref MemoryMarshal.GetArrayDataReference(twiddles);
                ref uint quotients = ref MemoryMarshal.GetArrayDataReference(shoupTwiddles);

                for (int segment = segmentStart; segment < segmentEnd; segment++)
                {
                    GetSegmentBounds(
                        segment,
                        segmentsPerGroup,
                        quarterLength,
                        out int group,
                        out int first,
                        out int last);

                    int groupOffset = group * stageLength;
                    int index0 = groupOffset + first;
                    int index1 = groupOffset + quarterLength + first;
                    int index2 = groupOffset + halfLength + first;
                    int index3 = groupOffset + halfLength + quarterLength + first;
                    int firstTwiddleIndex0 = firstTwiddleOffset + first;
                    int firstTwiddleIndex1 = firstTwiddleOffset + quarterLength + first;
                    int secondTwiddleIndex = secondTwiddleOffset + first;
                    int remaining = last - first;

                    while (remaining > 0)
                    {
                        int chunkLength = Math.Min(remaining, CancellationStride);
                        int chunkEnd = index0 + chunkLength;

                        while (index0 + 3 < chunkEnd)
                        {
                            Vector128<uint> value0 = Vector128.LoadUnsafe(ref data, (nuint)index0);
                            Vector128<uint> value1 = Vector128.LoadUnsafe(ref data, (nuint)index1);
                            Vector128<uint> value2 = Vector128.LoadUnsafe(ref data, (nuint)index2);
                            Vector128<uint> value3 = Vector128.LoadUnsafe(ref data, (nuint)index3);

                            Vector128<uint> topSum0 = AddModuloNeon(value0, value2, mod);
                            Vector128<uint> topSum1 = AddModuloNeon(value1, value3, mod);
                            Vector128<uint> topDifference0 = SubtractModuloNeon(value0, value2, mod);
                            Vector128<uint> topDifference1 = SubtractModuloNeon(value1, value3, mod);

                            Vector128<uint> firstTwiddle0 =
                                Vector128.LoadUnsafe(ref roots, (nuint)firstTwiddleIndex0);
                            Vector128<uint> firstShoup0 =
                                Vector128.LoadUnsafe(ref quotients, (nuint)firstTwiddleIndex0);
                            Vector128<uint> firstTwiddle1 =
                                Vector128.LoadUnsafe(ref roots, (nuint)firstTwiddleIndex1);
                            Vector128<uint> firstShoup1 =
                                Vector128.LoadUnsafe(ref quotients, (nuint)firstTwiddleIndex1);

                            Vector128<uint> lower0 =
                                MultiplyShoupNeon(topDifference0, firstTwiddle0, firstShoup0, mod);
                            Vector128<uint> lower1 =
                                MultiplyShoupNeon(topDifference1, firstTwiddle1, firstShoup1, mod);

                            Vector128<uint> upperSum = AddModuloNeon(topSum0, topSum1, mod);
                            Vector128<uint> upperDifference = SubtractModuloNeon(topSum0, topSum1, mod);
                            Vector128<uint> lowerSum = AddModuloNeon(lower0, lower1, mod);
                            Vector128<uint> lowerDifference = SubtractModuloNeon(lower0, lower1, mod);

                            Vector128<uint> secondTwiddle =
                                Vector128.LoadUnsafe(ref roots, (nuint)secondTwiddleIndex);
                            Vector128<uint> secondShoup =
                                Vector128.LoadUnsafe(ref quotients, (nuint)secondTwiddleIndex);

                            Vector128<uint> output1 =
                                MultiplyShoupNeon(upperDifference, secondTwiddle, secondShoup, mod);
                            Vector128<uint> output3 =
                                MultiplyShoupNeon(lowerDifference, secondTwiddle, secondShoup, mod);

                            upperSum.StoreUnsafe(ref data, (nuint)index0);
                            output1.StoreUnsafe(ref data, (nuint)index1);
                            lowerSum.StoreUnsafe(ref data, (nuint)index2);
                            output3.StoreUnsafe(ref data, (nuint)index3);

                            index0 += 4;
                            index1 += 4;
                            index2 += 4;
                            index3 += 4;
                            firstTwiddleIndex0 += 4;
                            firstTwiddleIndex1 += 4;
                            secondTwiddleIndex += 4;
                        }

                        for (; index0 < chunkEnd;
                             index0++, index1++, index2++, index3++,
                             firstTwiddleIndex0++, firstTwiddleIndex1++, secondTwiddleIndex++)
                        {
                            uint value0 = values[index0];
                            uint value1 = values[index1];
                            uint value2 = values[index2];
                            uint value3 = values[index3];

                            uint topSum0 = value0 + value2;
                            uint topSum1 = value1 + value3;
                            if (topSum0 >= modulus) topSum0 -= modulus;
                            if (topSum1 >= modulus) topSum1 -= modulus;

                            uint topDifference0 =
                                value0 >= value2 ? value0 - value2 : value0 + modulus - value2;
                            uint topDifference1 =
                                value1 >= value3 ? value1 - value3 : value1 + modulus - value3;

                            uint lower0 = MultiplyShoupScalar(
                                topDifference0, twiddles[firstTwiddleIndex0],
                                shoupTwiddles[firstTwiddleIndex0], modulus);
                            uint lower1 = MultiplyShoupScalar(
                                topDifference1, twiddles[firstTwiddleIndex1],
                                shoupTwiddles[firstTwiddleIndex1], modulus);

                            uint upperSum = topSum0 + topSum1;
                            if (upperSum >= modulus) upperSum -= modulus;
                            uint upperDifference =
                                topSum0 >= topSum1 ? topSum0 - topSum1 : topSum0 + modulus - topSum1;

                            uint lowerSum = lower0 + lower1;
                            if (lowerSum >= modulus) lowerSum -= modulus;
                            uint lowerDifference =
                                lower0 >= lower1 ? lower0 - lower1 : lower0 + modulus - lower1;

                            values[index0] = upperSum;
                            values[index1] = MultiplyShoupScalar(
                                upperDifference, twiddles[secondTwiddleIndex],
                                shoupTwiddles[secondTwiddleIndex], modulus);
                            values[index2] = lowerSum;
                            values[index3] = MultiplyShoupScalar(
                                lowerDifference, twiddles[secondTwiddleIndex],
                                shoupTwiddles[secondTwiddleIndex], modulus);
                        }

                        remaining -= chunkLength;
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteCachedGlobalStageNeon(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int twiddleOffset,
        int stageLength,
        bool inverse,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        int halfLength = stageLength >> 1;
        int groupCount = values.Length / stageLength;
        int segments =
            GetWorkerAlignedSegmentsPerGroup(
                halfLength,
                groupCount,
                workers.WorkerCount,
                GetSegmentsPerGroup(halfLength, groupCount, workers.WorkerCount));

        Vector128<uint> mod = Vector128.Create(modulus);

        ExecuteRanges(
            checked(groupCount * segments),
            workers,
            cancellationToken,
            (start, end) =>
            {
                ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
                ref uint roots = ref MemoryMarshal.GetArrayDataReference(twiddles);
                ref uint quotients = ref MemoryMarshal.GetArrayDataReference(shoupTwiddles);

                for (int segment = start; segment < end; segment++)
                {
                    GetSegmentBounds(
                        segment,
                        segments,
                        halfLength,
                        out int group,
                        out int first,
                        out int last);

                    int groupOffset = group * stageLength;
                    int i = first;

                    for (; i + 3 < last; i += 4)
                    {
                        int leftIndex = groupOffset + i;
                        int rightIndex = leftIndex + halfLength;

                        Vector128<uint> left =
                            Vector128.LoadUnsafe(ref data, (nuint)leftIndex);
                        Vector128<uint> right =
                            Vector128.LoadUnsafe(ref data, (nuint)rightIndex);
                        Vector128<uint> rootVector =
                            Vector128.LoadUnsafe(ref roots, (nuint)(twiddleOffset + i));
                        Vector128<uint> shoupVector =
                            Vector128.LoadUnsafe(ref quotients, (nuint)(twiddleOffset + i));

                        if (inverse)
                        {
                            right = MultiplyShoupNeon(
                                right,
                                rootVector,
                                shoupVector,
                                mod);

                            AddModuloNeon(left, right, mod)
                                .StoreUnsafe(ref data, (nuint)leftIndex);
                            SubtractModuloNeon(left, right, mod)
                                .StoreUnsafe(ref data, (nuint)rightIndex);
                        }
                        else
                        {
                            AddModuloNeon(left, right, mod)
                                .StoreUnsafe(ref data, (nuint)leftIndex);
                            MultiplyShoupNeon(
                                    SubtractModuloNeon(left, right, mod),
                                    rootVector,
                                    shoupVector,
                                    mod)
                                .StoreUnsafe(ref data, (nuint)rightIndex);
                        }
                    }

                    for (; i < last; i++)
                    {
                        int leftIndex = groupOffset + i;
                        int rightIndex = leftIndex + halfLength;
                        uint left = values[leftIndex];
                        uint right = values[rightIndex];
                        uint rootValue = twiddles[twiddleOffset + i];
                        uint rootShoup = shoupTwiddles[twiddleOffset + i];

                        if (inverse)
                            right = MultiplyShoupScalar(right, rootValue, rootShoup, modulus);

                        uint sum = left + right;
                        if (sum >= modulus) sum -= modulus;
                        uint difference = left >= right ? left - right : left + modulus - right;

                        values[leftIndex] = sum;
                        values[rightIndex] = inverse
                            ? difference
                            : MultiplyShoupScalar(difference, rootValue, rootShoup, modulus);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardUncachedStageNeon(
        uint[] values,
        uint modulus,
        uint root,
        int stageLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        int halfLength = stageLength >> 1;
        int groupCount = values.Length / stageLength;
        int segments =
            GetWorkerAlignedSegmentsPerGroup(
                halfLength,
                groupCount,
                workers.WorkerCount,
                GetSegmentsPerGroup(halfLength, groupCount, workers.WorkerCount));

        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> advance = Vector128.Create((uint)ModPow(root, 4, modulus));

        ExecuteRanges(
            checked(groupCount * segments),
            workers,
            cancellationToken,
            (start, end) =>
            {
                ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

                for (int segment = start; segment < end; segment++)
                {
                    GetSegmentBounds(
                        segment,
                        segments,
                        halfLength,
                        out int group,
                        out int first,
                        out int last);

                    int groupOffset = group * stageLength;
                    Vector128<uint> twiddle = CreateTwiddleSequenceNeon(root, first, modulus);
                    int i = first;

                    for (; i + 3 < last; i += 4)
                    {
                        int leftIndex = groupOffset + i;
                        int rightIndex = leftIndex + halfLength;
                        Vector128<uint> left = Vector128.LoadUnsafe(ref data, (nuint)leftIndex);
                        Vector128<uint> right = Vector128.LoadUnsafe(ref data, (nuint)rightIndex);

                        AddModuloNeon(left, right, mod)
                            .StoreUnsafe(ref data, (nuint)leftIndex);
                        MultiplyResiduesNeon(
                                SubtractModuloNeon(left, right, mod),
                                twiddle,
                                modulus)
                            .StoreUnsafe(ref data, (nuint)rightIndex);

                        twiddle = MultiplyResiduesNeon(twiddle, advance, modulus);
                    }

                    uint scalarTwiddle = (uint)ModPow(root, (uint)i, modulus);
                    for (; i < last; i++)
                    {
                        int leftIndex = groupOffset + i;
                        int rightIndex = leftIndex + halfLength;
                        uint left = values[leftIndex];
                        uint right = values[rightIndex];
                        uint sum = left + right;
                        if (sum >= modulus) sum -= modulus;
                        uint difference = left >= right ? left - right : left + modulus - right;
                        values[leftIndex] = sum;
                        values[rightIndex] = (uint)((ulong)difference * scalarTwiddle % modulus);
                        scalarTwiddle = (uint)((ulong)scalarTwiddle * root % modulus);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseUncachedStageNeon(
        uint[] values,
        uint modulus,
        uint root,
        int stageLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        int halfLength = stageLength >> 1;
        int groupCount = values.Length / stageLength;
        int segments =
            GetWorkerAlignedSegmentsPerGroup(
                halfLength,
                groupCount,
                workers.WorkerCount,
                GetSegmentsPerGroup(halfLength, groupCount, workers.WorkerCount));

        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> advance = Vector128.Create((uint)ModPow(root, 4, modulus));

        ExecuteRanges(
            checked(groupCount * segments),
            workers,
            cancellationToken,
            (start, end) =>
            {
                ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

                for (int segment = start; segment < end; segment++)
                {
                    GetSegmentBounds(
                        segment,
                        segments,
                        halfLength,
                        out int group,
                        out int first,
                        out int last);

                    int groupOffset = group * stageLength;
                    Vector128<uint> twiddle = CreateTwiddleSequenceNeon(root, first, modulus);
                    int i = first;

                    for (; i + 3 < last; i += 4)
                    {
                        int leftIndex = groupOffset + i;
                        int rightIndex = leftIndex + halfLength;
                        Vector128<uint> left = Vector128.LoadUnsafe(ref data, (nuint)leftIndex);
                        Vector128<uint> right = MultiplyResiduesNeon(
                            Vector128.LoadUnsafe(ref data, (nuint)rightIndex),
                            twiddle,
                            modulus);

                        AddModuloNeon(left, right, mod)
                            .StoreUnsafe(ref data, (nuint)leftIndex);
                        SubtractModuloNeon(left, right, mod)
                            .StoreUnsafe(ref data, (nuint)rightIndex);

                        twiddle = MultiplyResiduesNeon(twiddle, advance, modulus);
                    }

                    if (i < last)
                    {
                        ProcessInverseUncachedStageSegmentByrefDualLane(
                            values,
                            modulus,
                            root,
                            stageLength,
                            group,
                            i,
                            last,
                            cancellationToken);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecutePointwiseProductNeon(
        uint[] destination,
        uint[] left,
        uint[] right,
        int length,
        uint modulus,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        ExecuteRanges(
            length,
            workers,
            cancellationToken,
            (start, end) =>
            {
                ref uint destinationRef = ref MemoryMarshal.GetArrayDataReference(destination);
                ref uint leftRef = ref MemoryMarshal.GetArrayDataReference(left);
                ref uint rightRef = ref MemoryMarshal.GetArrayDataReference(right);

                int i = start;
                for (; i + 3 < end; i += 4)
                {
                    Vector128<uint> leftVector =
                        Vector128.LoadUnsafe(ref leftRef, (nuint)i);
                    Vector128<uint> rightVector =
                        Vector128.LoadUnsafe(ref rightRef, (nuint)i);

                    MultiplyResiduesNeon(leftVector, rightVector, modulus)
                        .StoreUnsafe(ref destinationRef, (nuint)i);
                }

                for (; i < end; i++)
                {
                    destination[i] =
                        (uint)((ulong)left[i] * right[i] % modulus);
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessFinalInversePrefixNeon(
        uint[] values,
        uint[] output,
        int halfLength,
        int start,
        int end,
        bool writeRight,
        uint modulus,
        uint root,
        uint inverseLength,
        uint inverseLengthShoup,
        CancellationToken cancellationToken)
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            uint rootSquared = (uint)((ulong)root * root % modulus);
            uint rootFourth = (uint)((ulong)rootSquared * rootSquared % modulus);
            if (writeRight)
            {
                ExecuteFinalInverseBothOutputsRange(values, output, halfLength, start, end,
                    modulus, root, rootSquared, rootFourth, inverseLength,
                    inverseLengthShoup, cancellationToken);
            }
            else
            {
                ExecuteFinalInverseLeftOnlyRange(values, output, halfLength, start, end,
                    modulus, root, rootSquared, rootFourth, inverseLength,
                    inverseLengthShoup, cancellationToken);
            }
            return;
        }

        ref uint valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        ref uint outputRef = ref MemoryMarshal.GetArrayDataReference(output);
        Vector128<uint> modulusVector = Vector128.Create(modulus);
        Vector128<uint> inverseVector = Vector128.Create(inverseLength);
        Vector128<uint> inverseShoupVector = Vector128.Create(inverseLengthShoup);
        Vector128<uint> advance = Vector128.Create((uint)ModPow(root, 4, modulus));

        int i = start;
        if (i + 4 <= end)
        {
            Vector128<uint> twiddle = CreateTwiddleSequenceNeon(root, i, modulus);
            for (; i + 4 <= end; i += 4)
            {
                int rightIndex = i + halfLength;
                Vector128<uint> left = Vector128.LoadUnsafe(ref valuesRef, (nuint)i);
                Vector128<uint> right = MultiplyResiduesNeon(
                    Vector128.LoadUnsafe(ref valuesRef, (nuint)rightIndex),
                    twiddle, modulus);

                Vector128<uint> sum = AddModuloNeon(left, right, modulusVector);
                Vector128<uint> difference = SubtractModuloNeon(left, right, modulusVector);

                MultiplyShoupNeon(sum, inverseVector, inverseShoupVector, modulusVector)
                    .StoreUnsafe(ref outputRef, (nuint)i);
                if (writeRight)
                {
                    MultiplyShoupNeon(difference, inverseVector, inverseShoupVector, modulusVector)
                        .StoreUnsafe(ref outputRef, (nuint)rightIndex);
                }

                if (i + 4 < end)
                    twiddle = MultiplyResiduesNeon(twiddle, advance, modulus);

                if (((i - start) & 0x7FFF) == 0x7FFC)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }

        if (i < end)
        {
            uint rootSquared = (uint)((ulong)root * root % modulus);
            uint rootFourth = (uint)((ulong)rootSquared * rootSquared % modulus);
            if (writeRight)
            {
                ExecuteFinalInverseBothOutputsRange(values, output, halfLength, i, end,
                    modulus, root, rootSquared, rootFourth, inverseLength,
                    inverseLengthShoup, cancellationToken);
            }
            else
            {
                ExecuteFinalInverseLeftOnlyRange(values, output, halfLength, i, end,
                    modulus, root, rootSquared, rootFourth, inverseLength,
                    inverseLengthShoup, cancellationToken);
            }
        }
    }

    private static int ReconstructCrtRangeNeon(
        ReadOnlySpan<uint> firstSpan,
        ReadOnlySpan<uint> secondSpan,
        Span<ulong> scratchSpan)
    {
        int count = firstSpan.Length;
        int vectorEnd = count & ~(Vector128<uint>.Count - 1);

        ref uint firstReference = ref MemoryMarshal.GetReference(firstSpan);
        ref uint secondReference = ref MemoryMarshal.GetReference(secondSpan);
        ref ulong scratchReference = ref MemoryMarshal.GetReference(scratchSpan);

        Vector128<uint> secondModulusVector = Vector128.Create(SecondModulus);
        Vector128<uint> inverseVector = Vector128.Create((uint)FirstModulusInverseInSecond);
        Vector128<uint> inverseShoupVector = Vector128.Create(FirstModulusInverseInSecondShoup);
        Vector128<uint> oneVector = Vector128.Create(1u);
        Vector128<uint> firstModulusVector = Vector128.Create(FirstModulus);

        int offset = 0;

        for (; offset < vectorEnd; offset += Vector128<uint>.Count)
        {
            Vector128<uint> first =
                Vector128.LoadUnsafe(ref firstReference, (nuint)offset);

            // FirstModulus < 5 * SecondModulus, so four unsigned reductions
            // canonicalize every P1 residue in P2 without division.
            Vector128<uint> reducedFirst = ReduceOnceNeon(first, secondModulusVector);
            reducedFirst = ReduceOnceNeon(reducedFirst, secondModulusVector);
            reducedFirst = ReduceOnceNeon(reducedFirst, secondModulusVector);
            reducedFirst = ReduceOnceNeon(reducedFirst, secondModulusVector);

            Vector128<uint> second =
                Vector128.LoadUnsafe(ref secondReference, (nuint)offset);

            Vector128<uint> difference =
                SubtractModuloNeon(second, reducedFirst, secondModulusVector);

            Vector128<uint> multiplier =
                MultiplyShoupNeon(
                    difference,
                    inverseVector,
                    inverseShoupVector,
                    secondModulusVector);

            if (AdvSimd.Arm64.IsSupported)
            {
                Vector128<ulong> firstLow =
                    AdvSimd.MultiplyWideningLower(
                        first.GetLower(),
                        oneVector.GetLower());
                Vector128<ulong> firstHigh =
                    AdvSimd.MultiplyWideningUpper(first, oneVector);

                Vector128<ulong> productLow =
                    AdvSimd.MultiplyWideningLower(
                        multiplier.GetLower(),
                        firstModulusVector.GetLower());
                Vector128<ulong> productHigh =
                    AdvSimd.MultiplyWideningUpper(
                        multiplier,
                        firstModulusVector);

                AdvSimd.Add(firstLow, productLow)
                    .StoreUnsafe(ref scratchReference, (nuint)offset);
                AdvSimd.Add(firstHigh, productHigh)
                    .StoreUnsafe(ref scratchReference, (nuint)(offset + 2));
            }
            else
            {
                // Managed Vector128 compatibility path for Mono runtimes that
                // advertise accelerated Vector128 but not AdvSimd.Arm64.
                for (int lane = 0; lane < Vector128<uint>.Count; lane++)
                {
                    scratchSpan[offset + lane] =
                        first.GetElement(lane) +
                        (ulong)FirstModulus * multiplier.GetElement(lane);
                }
            }
        }

        return offset;
    }

}
