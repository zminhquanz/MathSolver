using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    // SSSE3 PSHUFB extracts lanes 1/3 into the even 32-bit positions consumed
    // by PMULUDQ. SSE3 itself has no packed-integer primitive that improves
    // this NTT kernel, so SSE3 CPUs correctly execute the SSE2 arithmetic path.
    // SSE4.2 likewise inherits the SSE4.1 integer core (PMULLD/PMINUD).

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TransposeRadix4Sse(
        Vector128<uint> row0,
        Vector128<uint> row1,
        Vector128<uint> row2,
        Vector128<uint> row3,
        out Vector128<uint> column0,
        out Vector128<uint> column1,
        out Vector128<uint> column2,
        out Vector128<uint> column3)
    {
        Vector128<uint> low01 = Sse2.UnpackLow(row0, row1);
        Vector128<uint> high01 = Sse2.UnpackHigh(row0, row1);
        Vector128<uint> low23 = Sse2.UnpackLow(row2, row3);
        Vector128<uint> high23 = Sse2.UnpackHigh(row2, row3);

        column0 = Sse2.UnpackLow(low01.AsUInt64(), low23.AsUInt64()).AsUInt32();
        column1 = Sse2.UnpackHigh(low01.AsUInt64(), low23.AsUInt64()).AsUInt32();
        column2 = Sse2.UnpackLow(high01.AsUInt64(), high23.AsUInt64()).AsUInt32();
        column3 = Sse2.UnpackHigh(high01.AsUInt64(), high23.AsUInt64()).AsUInt32();
    }

    private static readonly Vector128<uint> LengthTwoEvenLaneMaskSse =
        Vector128.Create(
            uint.MaxValue, 0u, uint.MaxValue, 0u);

    private static readonly Vector128<uint> LengthTwoOddLaneMaskSse =
        Vector128.Create(
            0u, uint.MaxValue, 0u, uint.MaxValue);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteLengthTwoButterfliesSse(
        uint[] values,
        uint modulus,
        bool normalize,
        uint inverseLength,
        int pairStart,
        int pairEnd)
    {
        Debug.Assert(Sse2.IsSupported);

        ref uint data =
            ref MemoryMarshal.GetArrayDataReference(values);

        Vector128<uint> mod =
            Vector128.Create(modulus);

        Vector128<uint> inverse =
            default;
        Vector128<uint> inverseShoup =
            default;

        if (normalize)
        {
            uint shoup =
                (uint)(((ulong)inverseLength << 32) / modulus);

            inverse =
                Vector128.Create(inverseLength);
            inverseShoup =
                Vector128.Create(shoup);
        }

        int pairIndex =
            pairStart;

        for (; pairIndex + 1 < pairEnd; pairIndex += 2)
        {
            int valueIndex =
                pairIndex << 1;

            Vector128<uint> value =
                Vector128.LoadUnsafe(
                    ref data,
                    (nuint)valueIndex);

            Vector128<uint> swapped =
                Sse2.Shuffle(
                        value.AsInt32(),
                        0xB1)
                    .AsUInt32();

            Vector128<uint> sum =
                AddModuloSse(
                    value,
                    swapped,
                    mod);

            Vector128<uint> difference =
                SubtractModuloSse(
                    swapped,
                    value,
                    mod);

            Vector128<uint> output =
                Sse2.Or(
                        Sse2.And(
                            sum,
                            LengthTwoEvenLaneMaskSse),
                        Sse2.And(
                            difference,
                            LengthTwoOddLaneMaskSse))
                    .AsUInt32();

            if (normalize)
            {
                output =
                    MultiplyShoupSse(
                        output,
                        inverse,
                        inverseShoup,
                        mod);
            }

            output.StoreUnsafe(
                ref data,
                (nuint)valueIndex);
        }

        if (pairIndex < pairEnd)
        {
            ExecuteLengthTwoButterfliesScalarRange(
                values, modulus, normalize, inverseLength, pairIndex, pairEnd);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardLengthFourAndTwoFusedBlockSse(
        uint[] values,
        uint modulus,
        uint quarterTurnTwiddle,
        uint quarterTurnShoup,
        int blockOffset,
        int blockEnd)
    {
        Debug.Assert(Sse2.IsSupported);
        Debug.Assert(((blockEnd - blockOffset) & 3) == 0);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> twiddle = Vector128.Create(quarterTurnTwiddle);
        Vector128<uint> shoup = Vector128.Create(quarterTurnShoup);
        int index = blockOffset;
        int vectorEnd = blockEnd - 15;

        for (; index <= vectorEnd; index += 16)
        {
            TransposeRadix4Sse(
                Vector128.LoadUnsafe(ref data, (nuint)index),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 4)),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 8)),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 12)),
                out Vector128<uint> value0, out Vector128<uint> value1,
                out Vector128<uint> value2, out Vector128<uint> value3);

            Vector128<uint> topSum0 = AddModuloSse(value0, value2, mod);
            Vector128<uint> topSum1 = AddModuloSse(value1, value3, mod);
            Vector128<uint> lower0 = SubtractModuloSse(value0, value2, mod);
            Vector128<uint> lower1 = MultiplyShoupSse(
                SubtractModuloSse(value1, value3, mod), twiddle, shoup, mod);

            TransposeRadix4Sse(
                AddModuloSse(topSum0, topSum1, mod),
                SubtractModuloSse(topSum0, topSum1, mod),
                AddModuloSse(lower0, lower1, mod),
                SubtractModuloSse(lower0, lower1, mod),
                out Vector128<uint> output0, out Vector128<uint> output1,
                out Vector128<uint> output2, out Vector128<uint> output3);

            output0.StoreUnsafe(ref data, (nuint)index);
            output1.StoreUnsafe(ref data, (nuint)(index + 4));
            output2.StoreUnsafe(ref data, (nuint)(index + 8));
            output3.StoreUnsafe(ref data, (nuint)(index + 12));
        }

        if (index < blockEnd)
        {
            ExecuteLengthFourAndTwoFusedTailSse(
                values, index, blockEnd - index, modulus,
                quarterTurnTwiddle, quarterTurnShoup, inverse: false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseLengthTwoAndFourFusedBlockSse(
        uint[] values,
        uint modulus,
        uint quarterTurnTwiddle,
        uint quarterTurnShoup,
        int blockOffset,
        int blockEnd)
    {
        Debug.Assert(Sse2.IsSupported);
        Debug.Assert(((blockEnd - blockOffset) & 3) == 0);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> twiddle = Vector128.Create(quarterTurnTwiddle);
        Vector128<uint> shoup = Vector128.Create(quarterTurnShoup);
        int index = blockOffset;
        int vectorEnd = blockEnd - 15;

        for (; index <= vectorEnd; index += 16)
        {
            TransposeRadix4Sse(
                Vector128.LoadUnsafe(ref data, (nuint)index),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 4)),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 8)),
                Vector128.LoadUnsafe(ref data, (nuint)(index + 12)),
                out Vector128<uint> value0, out Vector128<uint> value1,
                out Vector128<uint> value2, out Vector128<uint> value3);

            Vector128<uint> leftSum = AddModuloSse(value0, value1, mod);
            Vector128<uint> rightSum = AddModuloSse(value2, value3, mod);
            Vector128<uint> leftDifference = SubtractModuloSse(value0, value1, mod);
            Vector128<uint> rightDifference = MultiplyShoupSse(
                SubtractModuloSse(value2, value3, mod), twiddle, shoup, mod);

            TransposeRadix4Sse(
                AddModuloSse(leftSum, rightSum, mod),
                AddModuloSse(leftDifference, rightDifference, mod),
                SubtractModuloSse(leftSum, rightSum, mod),
                SubtractModuloSse(leftDifference, rightDifference, mod),
                out Vector128<uint> output0, out Vector128<uint> output1,
                out Vector128<uint> output2, out Vector128<uint> output3);

            output0.StoreUnsafe(ref data, (nuint)index);
            output1.StoreUnsafe(ref data, (nuint)(index + 4));
            output2.StoreUnsafe(ref data, (nuint)(index + 8));
            output3.StoreUnsafe(ref data, (nuint)(index + 12));
        }

        if (index < blockEnd)
        {
            ExecuteLengthFourAndTwoFusedTailSse(
                values, index, blockEnd - index, modulus,
                quarterTurnTwiddle, quarterTurnShoup, inverse: true);
        }
    }

    private static readonly Vector128<byte> SseOddLaneShuffleMask =
        Vector128.Create(
            (byte)4, (byte)5, (byte)6, (byte)7,
            (byte)0x80, (byte)0x80, (byte)0x80, (byte)0x80,
            (byte)12, (byte)13, (byte)14, (byte)15,
            (byte)0x80, (byte)0x80, (byte)0x80, (byte)0x80);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ExtractOddLanesForMultiplySse(
        Vector128<uint> value)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.Shuffle(
                    value.AsByte(),
                    SseOddLaneShuffleMask)
                .AsUInt32();
        }

        return Sse2.ShiftRightLogical(
                value.AsUInt64(),
                32)
            .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ReduceOnceSse(
        Vector128<uint> value,
        Vector128<uint> modulus)
    {
        Vector128<uint> reduced =
            Sse2.Subtract(value, modulus);

        if (Sse41.IsSupported)
        {
            return Sse41.Min(value, reduced);
        }

        return Sse2.Add(
            reduced,
            Sse2.And(
                Sse2.ShiftRightArithmetic(
                    reduced.AsInt32(),
                    31)
                .AsUInt32(),
                modulus));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> MultiplyShoupSse(
        Vector128<uint> value,
        Vector128<uint> multiplier,
        Vector128<uint> multiplierShoup,
        Vector128<uint> modulus)
    {
        Vector128<uint> oddValue =
            ExtractOddLanesForMultiplySse(value);
        Vector128<uint> oddMultiplier =
            ExtractOddLanesForMultiplySse(multiplier);
        Vector128<uint> oddShoup =
            ExtractOddLanesForMultiplySse(multiplierShoup);

        Vector128<ulong> evenQ =
            Sse2.ShiftRightLogical(
                Sse2.Multiply(value, multiplierShoup),
                32);
        Vector128<ulong> oddQ =
            Sse2.ShiftRightLogical(
                Sse2.Multiply(oddValue, oddShoup),
                32);

        Vector128<uint> product;

        if (Sse41.IsSupported)
        {
            Vector128<uint> q =
                Sse2.Or(
                        evenQ,
                        Sse2.ShiftLeftLogical(oddQ, 32))
                    .AsUInt32();

            product = Sse2.Subtract(
                Sse41.MultiplyLow(value, multiplier),
                Sse41.MultiplyLow(q, modulus));
        }
        else
        {
            Vector128<ulong> even =
                Sse2.Subtract(
                    Sse2.Multiply(value, multiplier),
                    Sse2.Multiply(evenQ.AsUInt32(), modulus));

            Vector128<ulong> odd =
                Sse2.Subtract(
                    Sse2.Multiply(oddValue, oddMultiplier),
                    Sse2.Multiply(oddQ.AsUInt32(), modulus));

            product = Sse2.Or(
                    even,
                    Sse2.ShiftLeftLogical(odd, 32))
                .AsUInt32();
        }

        return ReduceOnceSse(product, modulus);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> AddModuloSse(
        Vector128<uint> left,
        Vector128<uint> right,
        Vector128<uint> modulus) =>
        ReduceOnceSse(Sse2.Add(left, right), modulus);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> SubtractModuloSse(
        Vector128<uint> left,
        Vector128<uint> right,
        Vector128<uint> modulus)
    {
        Vector128<uint> raw = Sse2.Subtract(left, right);
        return Sse2.Add(
            raw,
            Sse2.And(
                Sse2.ShiftRightArithmetic(
                    raw.AsInt32(),
                    31)
                .AsUInt32(),
                modulus));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> CorrectResidueBorrowSse(
        Vector128<ulong> product,
        Vector128<ulong> quotientTimesModulus,
        ulong modulus)
    {
        Vector128<long> remainder =
            Sse2.Subtract(
                product.AsInt64(),
                quotientTimesModulus.AsInt64());

        // q0 is exact or one too high. A high-qword underflow leaves the high
        // dword negative; duplicate that high-dword sign across its qword and
        // add one modulus. This works on plain SSE2 (no PCMPGTQ required).
        Vector128<int> signDwords =
            Sse2.ShiftRightArithmetic(
                remainder.AsInt32(),
                31);
        Vector128<int> borrowDwords =
            Sse2.Shuffle(signDwords, 0xF5);
        Vector128<ulong> correction =
            Sse2.And(
                borrowDwords.AsUInt64(),
                Vector128.Create(modulus));

        return Sse2.Add(
                remainder,
                correction.AsInt64())
            .AsUInt64();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> ReduceResidueProductFirstModulusSse(
        Vector128<ulong> product)
    {
        Vector128<ulong> high =
            Sse2.ShiftRightLogical(product, 27);
        Vector128<ulong> highWord =
            Sse2.ShiftRightLogical(high, 32);
        Vector128<ulong> lowWord =
            Sse2.And(
                high,
                Vector128.Create((ulong)uint.MaxValue));
        Vector128<ulong> lowQuotient =
            Sse2.ShiftRightLogical(
                Sse2.Multiply(
                    lowWord.AsUInt32(),
                    Vector128.Create(0x8888_8889u)),
                35);
        Vector128<ulong> lowQuotientTimes15 =
            Sse2.Subtract(
                    Sse2.ShiftLeftLogical(lowQuotient, 4).AsInt64(),
                    lowQuotient.AsInt64())
                .AsUInt64();
        Vector128<ulong> lowRemainder =
            Sse2.Subtract(
                    lowWord.AsInt64(),
                    lowQuotientTimes15.AsInt64())
                .AsUInt64();
        Vector128<ulong> carry =
            Sse2.ShiftRightLogical(
                Sse2.Add(
                    Sse2.Add(
                        lowRemainder.AsInt64(),
                        highWord.AsInt64()),
                    Vector128.Create(1UL).AsInt64())
                .AsUInt64(),
                4);
        Vector128<ulong> highContribution =
            Sse2.Multiply(
                highWord.AsUInt32(),
                Vector128.Create(0x1111_1111u));
        Vector128<ulong> quotient =
            Sse2.Add(
                Sse2.Add(
                    highContribution.AsInt64(),
                    lowQuotient.AsInt64()),
                carry.AsInt64())
            .AsUInt64();
        Vector128<ulong> quotientTimesModulus =
            Sse2.Multiply(
                quotient.AsUInt32(),
                Vector128.Create(FirstModulus));

        return CorrectResidueBorrowSse(
            product,
            quotientTimesModulus,
            FirstModulus);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> ReduceResidueProductSecondModulusSse(
        Vector128<ulong> product)
    {
        Vector128<ulong> high =
            Sse2.ShiftRightLogical(product, 26);
        Vector128<ulong> initialQuotient =
            Sse2.ShiftRightLogical(
                Sse2.Multiply(
                    high.AsUInt32(),
                    Vector128.Create(0x2492_4925u)),
                32);
        Vector128<ulong> quotient =
            Sse2.ShiftRightLogical(
                Sse2.Add(
                    initialQuotient.AsInt64(),
                    Sse2.ShiftRightLogical(
                        Sse2.Subtract(
                            high.AsInt64(),
                            initialQuotient.AsInt64())
                        .AsUInt64(),
                        1)
                    .AsInt64())
                .AsUInt64(),
                2);
        Vector128<ulong> quotientTimesModulus =
            Sse2.Multiply(
                quotient.AsUInt32(),
                Vector128.Create(SecondModulus));

        return CorrectResidueBorrowSse(
            product,
            quotientTimesModulus,
            SecondModulus);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> MultiplyResiduesSse(
        Vector128<uint> left,
        Vector128<uint> right,
        uint modulus)
    {
        Vector128<ulong> productEven =
            Sse2.Multiply(left, right);
        Vector128<uint> oddLeft =
            ExtractOddLanesForMultiplySse(left);
        Vector128<uint> oddRight =
            ExtractOddLanesForMultiplySse(right);
        Vector128<ulong> productOdd =
            Sse2.Multiply(oddLeft, oddRight);

        Vector128<ulong> remainderEven = modulus == FirstModulus
            ? ReduceResidueProductFirstModulusSse(productEven)
            : ReduceResidueProductSecondModulusSse(productEven);
        Vector128<ulong> remainderOdd = modulus == FirstModulus
            ? ReduceResidueProductFirstModulusSse(productOdd)
            : ReduceResidueProductSecondModulusSse(productOdd);

        return Sse2.Or(
                remainderEven,
                Sse2.ShiftLeftLogical(remainderOdd, 32))
            .AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> CreateTwiddleSequenceSse(
        uint root,
        int first,
        uint modulus)
    {
        // SSE2 baseline (SSSE3/SSE4.x inherit this setup): calculate the tiny
        // lane-power basis once, then apply root^first to all four lanes with
        // the packed residue multiplier instead of a dependent scalar chain.
        uint r2 = (uint)((ulong)root * root % modulus);
        uint r3 = (uint)((ulong)r2 * root % modulus);
        Vector128<uint> lanePowers = Vector128.Create(1u, root, r2, r3);
        uint firstPower = (uint)ModPow(root, (uint)first, modulus);
        return MultiplyResiduesSse(
            lanePowers,
            Vector128.Create(firstPower),
            modulus);
    }



    /// <summary>
    /// SSE2 baseline version of the fused uncached global Forward-DIF stage
    /// pair S and S/2. Four adjacent butterflies are processed per XMM batch
    /// while all four quarter streams stay resident in registers. SSSE3 and
    /// SSE4.x automatically inherit the faster helpers already used by this
    /// backend; the recurrence itself advances by root^4 through exact Shoup
    /// multiplication. A residual segment shorter than four butterflies finishes
    /// in one zero-padded XMM batch instead of re-entering scalar modular math.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessForwardUncachedStagePairSse(
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
        Debug.Assert(Sse2.IsSupported);

        int quarterLength = stageLength >> 2;
        int groupOffset = groupIndex * stageLength;
        Vector128<uint> mod = Vector128.Create(modulus);

        Vector128<uint> twiddle0 =
            CreateTwiddleSequenceSse(firstRoot, first, modulus);

        Vector128<uint> twiddle1 =
            MultiplyResiduesSse(
                twiddle0,
                Vector128.Create(quarterPhase),
                modulus);

        Vector128<uint> twiddle2 =
            CreateTwiddleSequenceSse(secondRoot, first, modulus);

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

            Vector128<uint> topSum0 = AddModuloSse(value0, value2, mod);
            Vector128<uint> topSum1 = AddModuloSse(value1, value3, mod);

            Vector128<uint> lower0 =
                MultiplyResiduesSse(
                    SubtractModuloSse(value0, value2, mod),
                    twiddle0,
                    modulus);

            Vector128<uint> lower1 =
                MultiplyResiduesSse(
                    SubtractModuloSse(value1, value3, mod),
                    twiddle1,
                    modulus);

            Vector128<uint> upperSum = AddModuloSse(topSum0, topSum1, mod);
            Vector128<uint> upperDifference = SubtractModuloSse(topSum0, topSum1, mod);
            Vector128<uint> lowerSum = AddModuloSse(lower0, lower1, mod);
            Vector128<uint> lowerDifference = SubtractModuloSse(lower0, lower1, mod);

            upperSum.StoreUnsafe(ref data, (nuint)index0);
            lowerSum.StoreUnsafe(ref data, (nuint)index2);

            MultiplyResiduesSse(upperDifference, twiddle2, modulus)
                .StoreUnsafe(ref data, (nuint)index1);
            MultiplyResiduesSse(lowerDifference, twiddle2, modulus)
                .StoreUnsafe(ref data, (nuint)index3);

            // Both S-stage streams use the same root^4 recurrence. Advance the
            // packed lanes independently; SSE4.1+ automatically benefits from
            // PMULLD inside MultiplyShoupSse while SSE2 remains exact.
            twiddle0 = MultiplyShoupSse(twiddle0, advance0, advance0Shoup, mod);
            twiddle1 = MultiplyShoupSse(twiddle1, advance0, advance0Shoup, mod);
            twiddle2 = MultiplyShoupSse(twiddle2, advance2, advance2Shoup, mod);

            sinceCancellation += 4;
            if (sinceCancellation >= (1 << 14))
            {
                cancellationToken.ThrowIfCancellationRequested();
                sinceCancellation = 0;
            }
        }

        if (i < last)
        {
            ProcessForwardUncachedStagePairTailSse(
                values, groupOffset, quarterLength, i, last - i,
                twiddle0, twiddle1, twiddle2, modulus);
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
    private static void ExecuteForwardCachedStagePairByGroupsSse(
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
        Debug.Assert(Sse2.IsSupported);
        const int CancellationStride = 1 << 15;
        int halfLength = stageLength >> 1;
        int quarterLength = halfLength >> 1;
        int groupCount = values.Length / stageLength;
        int segmentsPerGroup =
            GetVectorAlignedSegmentsPerGroup(
                quarterLength,
                groupCount,
                workers,
                Vector128<uint>.Count);
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
                    GetVectorAlignedSegmentBounds(
                        segment,
                        segmentsPerGroup,
                        quarterLength,
                        Vector128<uint>.Count,
                        workers,
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

                            Vector128<uint> topSum0 = AddModuloSse(value0, value2, mod);
                            Vector128<uint> topSum1 = AddModuloSse(value1, value3, mod);
                            Vector128<uint> topDifference0 = SubtractModuloSse(value0, value2, mod);
                            Vector128<uint> topDifference1 = SubtractModuloSse(value1, value3, mod);

                            Vector128<uint> firstTwiddle0 =
                                Vector128.LoadUnsafe(ref roots, (nuint)firstTwiddleIndex0);
                            Vector128<uint> firstShoup0 =
                                Vector128.LoadUnsafe(ref quotients, (nuint)firstTwiddleIndex0);
                            Vector128<uint> firstTwiddle1 =
                                Vector128.LoadUnsafe(ref roots, (nuint)firstTwiddleIndex1);
                            Vector128<uint> firstShoup1 =
                                Vector128.LoadUnsafe(ref quotients, (nuint)firstTwiddleIndex1);

                            Vector128<uint> lower0 =
                                MultiplyShoupSse(topDifference0, firstTwiddle0, firstShoup0, mod);
                            Vector128<uint> lower1 =
                                MultiplyShoupSse(topDifference1, firstTwiddle1, firstShoup1, mod);

                            Vector128<uint> upperSum = AddModuloSse(topSum0, topSum1, mod);
                            Vector128<uint> upperDifference = SubtractModuloSse(topSum0, topSum1, mod);
                            Vector128<uint> lowerSum = AddModuloSse(lower0, lower1, mod);
                            Vector128<uint> lowerDifference = SubtractModuloSse(lower0, lower1, mod);

                            Vector128<uint> secondTwiddle =
                                Vector128.LoadUnsafe(ref roots, (nuint)secondTwiddleIndex);
                            Vector128<uint> secondShoup =
                                Vector128.LoadUnsafe(ref quotients, (nuint)secondTwiddleIndex);

                            Vector128<uint> output1 =
                                MultiplyShoupSse(upperDifference, secondTwiddle, secondShoup, mod);
                            Vector128<uint> output3 =
                                MultiplyShoupSse(lowerDifference, secondTwiddle, secondShoup, mod);

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
    private static void ExecuteCachedGlobalStageSse(
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
        int segments = GetVectorAlignedSegmentsPerGroup(
            halfLength,
            groupCount,
            workers,
            Vector128<uint>.Count);
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
                    GetVectorAlignedSegmentBounds(
                        segment,
                        segments,
                        halfLength,
                        Vector128<uint>.Count,
                        workers,
                        out int group,
                        out int first,
                        out int last);
                    int groupOffset = group * stageLength;
                    int i = first;

                    for (; i + 3 < last; i += 4)
                    {
                        int leftIndex = groupOffset + i;
                        int rightIndex = leftIndex + halfLength;
                        Vector128<uint> left = Vector128.LoadUnsafe(ref data, (nuint)leftIndex);
                        Vector128<uint> right = Vector128.LoadUnsafe(ref data, (nuint)rightIndex);
                        Vector128<uint> rootVector = Vector128.LoadUnsafe(ref roots, (nuint)(twiddleOffset + i));
                        Vector128<uint> shoupVector = Vector128.LoadUnsafe(ref quotients, (nuint)(twiddleOffset + i));

                        if (inverse)
                        {
                            right = MultiplyShoupSse(right, rootVector, shoupVector, mod);
                            AddModuloSse(left, right, mod).StoreUnsafe(ref data, (nuint)leftIndex);
                            SubtractModuloSse(left, right, mod).StoreUnsafe(ref data, (nuint)rightIndex);
                        }
                        else
                        {
                            AddModuloSse(left, right, mod).StoreUnsafe(ref data, (nuint)leftIndex);
                            MultiplyShoupSse(
                                    SubtractModuloSse(left, right, mod),
                                    rootVector,
                                    shoupVector,
                                    mod)
                                .StoreUnsafe(ref data, (nuint)rightIndex);
                        }
                    }

                    if (i < last)
                    {
                        ExecuteCachedButterflyTailSse(
                            values, twiddles, shoupTwiddles,
                            groupOffset + i, groupOffset + halfLength + i,
                            twiddleOffset + i, last - i, inverse, modulus);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardUncachedStageSse(
        uint[] values,
        uint modulus,
        uint root,
        int stageLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        int halfLength = stageLength >> 1;
        int groupCount = values.Length / stageLength;
        int segments = GetVectorAlignedSegmentsPerGroup(
            halfLength,
            groupCount,
            workers,
            Vector128<uint>.Count);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> advance = Vector128.Create((uint)ModPow(root, 4, modulus));

        ExecuteRanges(checked(groupCount * segments), workers, cancellationToken, (start, end) =>
        {
            ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
            for (int segment = start; segment < end; segment++)
            {
                GetVectorAlignedSegmentBounds(
                    segment,
                    segments,
                    halfLength,
                    Vector128<uint>.Count,
                    workers,
                    out int group,
                    out int first,
                    out int last);
                int groupOffset = group * stageLength;
                Vector128<uint> twiddle = CreateTwiddleSequenceSse(root, first, modulus);
                int i = first;
                for (; i + 3 < last; i += 4)
                {
                    int leftIndex = groupOffset + i;
                    int rightIndex = leftIndex + halfLength;
                    Vector128<uint> left = Vector128.LoadUnsafe(ref data, (nuint)leftIndex);
                    Vector128<uint> right = Vector128.LoadUnsafe(ref data, (nuint)rightIndex);
                    AddModuloSse(left, right, mod).StoreUnsafe(ref data, (nuint)leftIndex);
                    MultiplyResiduesSse(
                            SubtractModuloSse(left, right, mod),
                            twiddle,
                            modulus)
                        .StoreUnsafe(ref data, (nuint)rightIndex);
                    twiddle = MultiplyResiduesSse(twiddle, advance, modulus);
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
    private static void ExecuteInverseUncachedStageSse(
        uint[] values,
        uint modulus,
        uint root,
        int stageLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        int halfLength = stageLength >> 1;
        int groupCount = values.Length / stageLength;
        int segments = GetVectorAlignedSegmentsPerGroup(
            halfLength,
            groupCount,
            workers,
            Vector128<uint>.Count);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> advance = Vector128.Create((uint)ModPow(root, 4, modulus));

        ExecuteRanges(checked(groupCount * segments), workers, cancellationToken, (start, end) =>
        {
            ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
            for (int segment = start; segment < end; segment++)
            {
                GetVectorAlignedSegmentBounds(
                    segment,
                    segments,
                    halfLength,
                    Vector128<uint>.Count,
                    workers,
                    out int group,
                    out int first,
                    out int last);
                int groupOffset = group * stageLength;
                Vector128<uint> twiddle = CreateTwiddleSequenceSse(root, first, modulus);
                int i = first;
                for (; i + 3 < last; i += 4)
                {
                    int leftIndex = groupOffset + i;
                    int rightIndex = leftIndex + halfLength;
                    Vector128<uint> left = Vector128.LoadUnsafe(ref data, (nuint)leftIndex);
                    Vector128<uint> right = MultiplyResiduesSse(
                        Vector128.LoadUnsafe(ref data, (nuint)rightIndex),
                        twiddle,
                        modulus);
                    AddModuloSse(left, right, mod).StoreUnsafe(ref data, (nuint)leftIndex);
                    SubtractModuloSse(left, right, mod).StoreUnsafe(ref data, (nuint)rightIndex);
                    twiddle = MultiplyResiduesSse(twiddle, advance, modulus);
                }
                if (i < last)
                {
                    ProcessInverseUncachedStageSegmentByrefDualLane(
                        values, modulus, root, stageLength, group, i, last, cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
        });
    }

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
            ExecuteInverseLengthTwoAndFourFusedBlockSse(values, modulus,
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
            ExecuteForwardLengthFourAndTwoFusedBlockSse(values, modulus,
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
                Vector128<uint> product =
                    MultiplyShoupSse(
                        value,
                        root,
                        quotient,
                        mod);
                if (inverse) b = product;
                var sum = ReduceOnceSse(
                    Sse2.Add(a, b),
                    mod);
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
            if (j < half)
            {
                int residualCount = half - j;
                if (half == 1)
                {
                    // Length-2 is handled by the dedicated SIMD S=2 kernel in
                    // production. Keep this single-butterfly safety fallback for
                    // callers that enter the cached tile helper directly.
                    uint a = values[group];
                    uint b = values[group + half];
                    uint sum = a + b;
                    if (sum >= modulus) sum -= modulus;
                    values[group] = sum;
                    values[group + half] =
                        a >= b ? a - b : a + modulus - b;
                }
                else
                {
                    ExecuteCachedButterflyTailSse(
                        values, twiddles, shoup,
                        group + j, group + half + j, twiddleOffset + j,
                        residualCount, inverse, modulus);
                }
            }
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int ReconstructCrtRangeSse(
        ReadOnlySpan<uint> firstSpan,
        ReadOnlySpan<uint> secondSpan,
        Span<ulong> scratchSpan)
    {
        int count = firstSpan.Length;
        int vectorEnd = count & ~(Vector128<uint>.Count - 1);

        ref uint firstReference = ref MemoryMarshal.GetReference(firstSpan);
        ref uint secondReference = ref MemoryMarshal.GetReference(secondSpan);
        ref ulong scratchReference = ref MemoryMarshal.GetReference(scratchSpan);

        Vector128<uint> secondModulus = Vector128.Create(SecondModulus);
        Vector128<uint> inverse = Vector128.Create((uint)FirstModulusInverseInSecond);
        Vector128<uint> inverseShoup = Vector128.Create(FirstModulusInverseInSecondShoup);
        Vector128<uint> firstModulus32 = Vector128.Create(FirstModulus);
        Vector128<uint> one32 = Vector128.Create(1u);

        int offset = 0;

        for (; offset < vectorEnd; offset += Vector128<uint>.Count)
        {
            Vector128<uint> first =
                Vector128.LoadUnsafe(ref firstReference, (nuint)offset);

            Vector128<uint> reducedFirst = first;
            reducedFirst = ReduceOnceSse(reducedFirst, secondModulus);
            reducedFirst = ReduceOnceSse(reducedFirst, secondModulus);
            reducedFirst = ReduceOnceSse(reducedFirst, secondModulus);
            reducedFirst = ReduceOnceSse(reducedFirst, secondModulus);

            Vector128<uint> second =
                Vector128.LoadUnsafe(ref secondReference, (nuint)offset);

            Vector128<uint> rawDifference =
                Sse2.Subtract(second, reducedFirst);

            Vector128<uint> difference =
                Sse2.Add(
                    rawDifference,
                    Sse2.And(
                        Sse2.ShiftRightArithmetic(
                            rawDifference.AsInt32(),
                            31)
                        .AsUInt32(),
                        secondModulus));

            Vector128<uint> multiplier =
                MultiplyShoupSse(
                    difference,
                    inverse,
                    inverseShoup,
                    secondModulus);

            Vector128<uint> oddFirst =
                ExtractOddLanesForMultiplySse(first);
            Vector128<uint> oddMultiplier =
                ExtractOddLanesForMultiplySse(multiplier);

            Vector128<ulong> firstEven64 =
                Sse2.Multiply(first, one32);
            Vector128<ulong> firstOdd64 =
                Sse2.Multiply(oddFirst, one32);

            Vector128<ulong> productEven =
                Sse2.Multiply(multiplier, firstModulus32);
            Vector128<ulong> productOdd =
                Sse2.Multiply(oddMultiplier, firstModulus32);

            Vector128<ulong> reconstructedEven =
                Sse2.Add(
                    firstEven64.AsInt64(),
                    productEven.AsInt64())
                .AsUInt64();

            Vector128<ulong> reconstructedOdd =
                Sse2.Add(
                    firstOdd64.AsInt64(),
                    productOdd.AsInt64())
                .AsUInt64();

            Vector128<ulong> low =
                Sse2.UnpackLow(
                    reconstructedEven.AsInt64(),
                    reconstructedOdd.AsInt64())
                .AsUInt64();

            Vector128<ulong> high =
                Sse2.UnpackHigh(
                    reconstructedEven.AsInt64(),
                    reconstructedOdd.AsInt64())
                .AsUInt64();

            low.StoreUnsafe(ref scratchReference, (nuint)offset);
            high.StoreUnsafe(ref scratchReference, (nuint)(offset + 2));
        }

        return offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseCachedStagePairByGroupsSse(
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
        const int Width = 4;
        int halfLength = stageLength >> 1;
        int parentLength = stageLength << 1;
        int parentCount = values.Length / parentLength;
        int segmentsPerParent = GetVectorAlignedSegmentsPerGroup(
            halfLength, parentCount, workers, Width);
        Vector128<uint> mod = Vector128.Create(modulus);

        ExecuteRanges(checked(parentCount * segmentsPerParent), workers, cancellationToken,
            (segmentStart, segmentEnd) =>
            {
                ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
                ref uint tw = ref MemoryMarshal.GetArrayDataReference(twiddles);
                ref uint sh = ref MemoryMarshal.GetArrayDataReference(shoupTwiddles);
                for (int segment = segmentStart; segment < segmentEnd; segment++)
                {
                    GetVectorAlignedSegmentBounds(
                        segment, segmentsPerParent, halfLength, Width, workers,
                        out int parent, out int first, out int last);
                    int index0 = parent * parentLength + first;
                    int index1 = index0 + halfLength;
                    int index2 = index0 + stageLength;
                    int index3 = index2 + halfLength;
                    int t1Index = firstTwiddleOffset + first;
                    int t20Index = secondTwiddleOffset + first;
                    int t21Index = secondTwiddleOffset + halfLength + first;

                    for (int i = first; i < last; i += Width)
                    {
                        Vector128<uint> a = Vector128.LoadUnsafe(ref data, (nuint)index0);
                        Vector128<uint> b = Vector128.LoadUnsafe(ref data, (nuint)index1);
                        Vector128<uint> c = Vector128.LoadUnsafe(ref data, (nuint)index2);
                        Vector128<uint> d = Vector128.LoadUnsafe(ref data, (nuint)index3);
                        Vector128<uint> t1 = Vector128.LoadUnsafe(ref tw, (nuint)t1Index);
                        Vector128<uint> s1 = Vector128.LoadUnsafe(ref sh, (nuint)t1Index);
                        Vector128<uint> br = MultiplyShoupSse(b, t1, s1, mod);
                        Vector128<uint> dr = MultiplyShoupSse(d, t1, s1, mod);
                        Vector128<uint> u0 = AddModuloSse(a, br, mod);
                        Vector128<uint> v0 = SubtractModuloSse(a, br, mod);
                        Vector128<uint> u1 = AddModuloSse(c, dr, mod);
                        Vector128<uint> v1 = SubtractModuloSse(c, dr, mod);

                        Vector128<uint> t20 = Vector128.LoadUnsafe(ref tw, (nuint)t20Index);
                        Vector128<uint> s20 = Vector128.LoadUnsafe(ref sh, (nuint)t20Index);
                        Vector128<uint> t21 = Vector128.LoadUnsafe(ref tw, (nuint)t21Index);
                        Vector128<uint> s21 = Vector128.LoadUnsafe(ref sh, (nuint)t21Index);
                        Vector128<uint> m0 = MultiplyShoupSse(u1, t20, s20, mod);
                        Vector128<uint> m1 = MultiplyShoupSse(v1, t21, s21, mod);

                        AddModuloSse(u0, m0, mod).StoreUnsafe(ref data, (nuint)index0);
                        AddModuloSse(v0, m1, mod).StoreUnsafe(ref data, (nuint)index1);
                        SubtractModuloSse(u0, m0, mod).StoreUnsafe(ref data, (nuint)index2);
                        SubtractModuloSse(v0, m1, mod).StoreUnsafe(ref data, (nuint)index3);

                        index0 += Width; index1 += Width; index2 += Width; index3 += Width;
                        t1Index += Width; t20Index += Width; t21Index += Width;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                }
            });
    }

}
