#if ANDROID && NET11_0_OR_GREATER
#pragma warning disable SYSLIB5003

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecutePointwiseProductSve<T>(
        uint[] destination,
        uint[] left,
        uint[] right,
        int length,
        uint modulus,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
        where T : struct
    {
        GetGlobalShoupReciprocal(modulus, out uint reciprocalHigh, out uint reciprocalLow);

        ExecuteVectorAlignedRanges(
            length,
            Vector<uint>.Count,
            workers,
            cancellationToken,
            (start, end) =>
            {
                int width = Vector<uint>.Count;
                int i = start;
                if (i <= end - width)
                {
                    NttKernelExecution.Record(NttKernelPhase.Pointwise, SveIsa<T>());

                    ref uint destinationRef = ref MemoryMarshal.GetArrayDataReference(destination);
                    ref uint leftRef = ref MemoryMarshal.GetArrayDataReference(left);
                    ref uint rightRef = ref MemoryMarshal.GetArrayDataReference(right);
                    Vector<uint> modulusVector = Vector.Create(modulus);
                    Vector<uint> high = Vector.Create(reciprocalHigh);
                    Vector<uint> low = Vector.Create(reciprocalLow);
                    Vector<uint> one = Vector.Create(1u);

                    for (; i <= end - width; i += width)
                    {
                        Vector<uint> leftVector = Vector.LoadUnsafe(ref leftRef, (nuint)i);
                        Vector<uint> rightVector = Vector.LoadUnsafe(ref rightRef, (nuint)i);

                        // For canonical residues and p < 2^31, the reciprocal estimate
                        // undercounts floor(right * 2^32 / p) by at most one. The exact
                        // residual is below 2p, so its low word suffices for correction.
                        Vector<uint> estimate = Sve.MultiplyAdd(
                            MultiplyHighSve<T>(rightVector, low), rightVector, high);
                        Vector<uint> residual = Sve.MultiplySubtract(Vector<uint>.Zero, estimate, modulusVector);
                        Vector<uint> shoup = Sve.ConditionalSelect(
                            Sve.CompareGreaterThanOrEqual(residual, modulusVector),
                            Sve.Add(estimate, one),
                            estimate);

                        MultiplyShoupSve<T>(leftVector, rightVector, shoup, modulusVector)
                            .StoreUnsafe(ref destinationRef, (nuint)i);
                    }
                }

                if (i < end) NttKernelExecution.Record(NttKernelPhase.Pointwise, NttKernelIsa.Scalar);
                for (; i < end; i++)
                {
                    destination[i] = (uint)((ulong)left[i] * right[i] % modulus);
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int ReconstructCrtRangeSve<T>(
        ReadOnlySpan<uint> firstSpan,
        ReadOnlySpan<uint> secondSpan,
        Span<ulong> scratchSpan)
        where T : struct
    {
        int count = firstSpan.Length;
        Debug.Assert(secondSpan.Length >= count);
        Debug.Assert(scratchSpan.Length >= count);

        int width = Vector<uint>.Count;
        if (count < width) return 0;

        NttKernelExecution.Record(NttKernelPhase.Crt, SveIsa<T>());

        ref uint firstRef = ref MemoryMarshal.GetReference(firstSpan);
        ref uint secondRef = ref MemoryMarshal.GetReference(secondSpan);
        ref ulong scratchRef = ref MemoryMarshal.GetReference(scratchSpan);
        Vector<uint> secondModulus = Vector.Create(SecondModulus);
        Vector<uint> inverse = Vector.Create((uint)FirstModulusInverseInSecond);
        Vector<uint> inverseShoup = Vector.Create(FirstModulusInverseInSecondShoup);
        Vector<uint> firstModulus = Vector.Create(FirstModulus);
        Vector<ulong> widenedFirstModulus = Vector.Create((ulong)FirstModulus);
        Vector<ulong> lowWordMask = Vector.Create((ulong)uint.MaxValue);

        int offset = 0;
        for (; offset <= count - width; offset += width)
        {
            Vector<uint> first = Vector.LoadUnsafe(ref firstRef, (nuint)offset);
            Vector<uint> second = Vector.LoadUnsafe(ref secondRef, (nuint)offset);

            // FirstModulus < 5 * SecondModulus.
            Vector<uint> reducedFirst = ReduceOnceSve(first, secondModulus);
            reducedFirst = ReduceOnceSve(reducedFirst, secondModulus);
            reducedFirst = ReduceOnceSve(reducedFirst, secondModulus);
            reducedFirst = ReduceOnceSve(reducedFirst, secondModulus);

            Vector<uint> difference = SubtractModuloSve(second, reducedFirst, secondModulus);
            Vector<uint> multiplier = MultiplyShoupSve<T>(difference, inverse, inverseShoup, secondModulus);

            Vector<ulong> lower;
            Vector<ulong> upper;
            if (typeof(T) == typeof(Sve2Ntt))
            {
                // Android ARM64 is little-endian. Reinterpreting pairs gives the even
                // first residue in each low word and the odd residue in each high word.
                Vector<ulong> firstPairs = Vector.AsVectorUInt64(first);
                Vector<ulong> even = Sve.Add(
                    Sve2.MultiplyWideningEven(multiplier, firstModulus),
                    Sve.And(firstPairs, lowWordMask));
                Vector<ulong> odd = Sve.Add(
                    Sve2.MultiplyWideningOdd(multiplier, firstModulus),
                    firstPairs >> 32);

                // Restore coefficient order for the two contiguous stores.
                lower = Sve.ZipLow(even, odd);
                upper = Sve.ZipHigh(even, odd);
            }
            else
            {
                lower = Sve.MultiplyAdd(
                    Sve.ZeroExtendWideningLower(first),
                    Sve.ZeroExtendWideningLower(multiplier),
                    widenedFirstModulus);
                upper = Sve.MultiplyAdd(
                    Sve.ZeroExtendWideningUpper(first),
                    Sve.ZeroExtendWideningUpper(multiplier),
                    widenedFirstModulus);
            }

            lower.StoreUnsafe(ref scratchRef, (nuint)offset);
            upper.StoreUnsafe(ref scratchRef, (nuint)(offset + Vector<ulong>.Count));
        }

        return offset;
    }
}

#pragma warning restore SYSLIB5003
#endif
