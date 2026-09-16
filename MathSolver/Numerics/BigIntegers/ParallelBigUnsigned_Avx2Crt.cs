using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
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
}
