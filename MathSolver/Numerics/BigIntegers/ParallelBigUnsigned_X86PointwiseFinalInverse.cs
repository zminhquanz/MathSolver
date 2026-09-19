using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    // AVX2/SSE pointwise kernels complete the SIMD pipeline between forward
    // and inverse NTT.  The exact residue reducers are shared with the global
    // tail kernels, so no floating-point approximation or ulong % appears in
    // the vector hot loop.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecutePointwiseProductAvx2(
        uint[] destination,
        uint[] left,
        uint[] right,
        int length,
        uint modulus,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        ExecuteVectorAlignedRanges(
            length,
            Vector256<uint>.Count,
            workers,
            cancellationToken,
            (start, end) =>
            {
                ref uint destinationRef = ref MemoryMarshal.GetArrayDataReference(destination);
                ref uint leftRef = ref MemoryMarshal.GetArrayDataReference(left);
                ref uint rightRef = ref MemoryMarshal.GetArrayDataReference(right);

                int i = start;
                for (; i + (Vector256<uint>.Count - 1) < end; i += Vector256<uint>.Count)
                {
                    Vector256<uint> leftVector =
                        Vector256.LoadUnsafe(ref leftRef, (nuint)i);
                    Vector256<uint> rightVector =
                        Vector256.LoadUnsafe(ref rightRef, (nuint)i);

                    MultiplyResiduesAvx2(leftVector, rightVector, modulus)
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
    private static void ExecutePointwiseProductSse(
        uint[] destination,
        uint[] left,
        uint[] right,
        int length,
        uint modulus,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        ExecuteVectorAlignedRanges(
            length,
            Vector128<uint>.Count,
            workers,
            cancellationToken,
            (start, end) =>
            {
                ref uint destinationRef = ref MemoryMarshal.GetArrayDataReference(destination);
                ref uint leftRef = ref MemoryMarshal.GetArrayDataReference(left);
                ref uint rightRef = ref MemoryMarshal.GetArrayDataReference(right);

                int i = start;
                for (; i + (Vector128<uint>.Count - 1) < end; i += Vector128<uint>.Count)
                {
                    Vector128<uint> leftVector =
                        Vector128.LoadUnsafe(ref leftRef, (nuint)i);
                    Vector128<uint> rightVector =
                        Vector128.LoadUnsafe(ref rightRef, (nuint)i);

                    MultiplyResiduesSse(leftVector, rightVector, modulus)
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
    private static void ProcessFinalInversePrefixAvx2(
        uint[] values,
        uint[] output,
        int halfLength,
        int start,
        int end,
        bool writeRight,
        bool allowPaddedTail,
        uint modulus,
        uint root,
        uint inverseLength,
        uint inverseLengthShoup,
        CancellationToken cancellationToken)
    {
        ref uint valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        ref uint outputRef = ref MemoryMarshal.GetArrayDataReference(output);

        int i = start;
        int width = Vector256<uint>.Count;

        Avx2NttModContext context = new(modulus);
        Vector256<uint> inverseVector = Vector256.Create(inverseLength);
        Vector256<uint> inverseShoupVector = Vector256.Create(inverseLengthShoup);
        Vector256<uint> advance = Vector256.Create((uint)ModPow(root, (uint)width, modulus));

        if (i + width <= end)
        {
            Vector256<uint> twiddle = CreateTwiddleSequenceAvx2(root, i, modulus);

            for (; i + width <= end; i += width)
            {
                int rightIndex = i + halfLength;

                Vector256<uint> leftVector =
                    Vector256.LoadUnsafe(ref valuesRef, (nuint)i);
                Vector256<uint> rightVector =
                    Vector256.LoadUnsafe(ref valuesRef, (nuint)rightIndex);

                rightVector = MultiplyResiduesAvx2(rightVector, twiddle, modulus);

                Vector256<uint> sum = AddModuloAvx2(leftVector, rightVector, context);
                Vector256<uint> difference = SubtractModuloAvx2(leftVector, rightVector, context);

                MultiplyShoupAvx2(
                        sum,
                        inverseVector,
                        inverseShoupVector,
                        context)
                    .StoreUnsafe(ref outputRef, (nuint)i);

                if (writeRight)
                {
                    MultiplyShoupAvx2(
                            difference,
                            inverseVector,
                            inverseShoupVector,
                            context)
                        .StoreUnsafe(ref outputRef, (nuint)rightIndex);
                }

                if (i + width < end)
                {
                    twiddle = MultiplyResiduesAvx2(twiddle, advance, modulus);
                }

                if (((i - start) & 0x7FFF) == 0x7FFF - (width - 1))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        if (i < end && allowPaddedTail)
        {
            int remaining = end - i;
            Span<uint> leftScratch = stackalloc uint[Vector256<uint>.Count];
            Span<uint> rightScratch = stackalloc uint[Vector256<uint>.Count];
            Span<uint> leftOutputScratch = stackalloc uint[Vector256<uint>.Count];
            Span<uint> rightOutputScratch = stackalloc uint[Vector256<uint>.Count];

            for (int lane = 0; lane < remaining; lane++)
            {
                leftScratch[lane] = values[i + lane];
                rightScratch[lane] = values[i + halfLength + lane];
            }

            ref uint leftScratchRef = ref MemoryMarshal.GetReference(leftScratch);
            ref uint rightScratchRef = ref MemoryMarshal.GetReference(rightScratch);
            ref uint leftOutputRef = ref MemoryMarshal.GetReference(leftOutputScratch);
            ref uint rightOutputRef = ref MemoryMarshal.GetReference(rightOutputScratch);

            Vector256<uint> leftVector = Vector256.LoadUnsafe(ref leftScratchRef);
            Vector256<uint> rightVector = MultiplyResiduesAvx2(
                Vector256.LoadUnsafe(ref rightScratchRef),
                CreateTwiddleSequenceAvx2(root, i, modulus),
                modulus);
            Vector256<uint> sum = AddModuloAvx2(leftVector, rightVector, context);
            Vector256<uint> difference = SubtractModuloAvx2(leftVector, rightVector, context);

            MultiplyShoupAvx2(sum, inverseVector, inverseShoupVector, context)
                .StoreUnsafe(ref leftOutputRef);
            if (writeRight)
            {
                MultiplyShoupAvx2(difference, inverseVector, inverseShoupVector, context)
                    .StoreUnsafe(ref rightOutputRef);
            }

            for (int lane = 0; lane < remaining; lane++)
            {
                output[i + lane] = leftOutputScratch[lane];
                if (writeRight)
                    output[i + halfLength + lane] = rightOutputScratch[lane];
            }
        }
        if (i < end && !allowPaddedTail)
        {
            uint rootSquared = (uint)((ulong)root * root % modulus);
            uint rootFourth = (uint)((ulong)rootSquared * rootSquared % modulus);
            if (writeRight)
            {
                ExecuteFinalInverseBothOutputsRange(
                    values, output, halfLength, i, end, modulus, root, rootSquared,
                    rootFourth, inverseLength, inverseLengthShoup, cancellationToken);
            }
            else
            {
                ExecuteFinalInverseLeftOnlyRange(
                    values, output, halfLength, i, end, modulus, root, rootSquared,
                    rootFourth, inverseLength, inverseLengthShoup, cancellationToken);
            }
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessFinalInversePrefixSse(
        uint[] values,
        uint[] output,
        int halfLength,
        int start,
        int end,
        bool writeRight,
        bool allowPaddedTail,
        uint modulus,
        uint root,
        uint inverseLength,
        uint inverseLengthShoup,
        CancellationToken cancellationToken)
    {
        ref uint valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        ref uint outputRef = ref MemoryMarshal.GetArrayDataReference(output);

        int i = start;
        int width = Vector128<uint>.Count;

        Vector128<uint> modulusVector = Vector128.Create(modulus);
        Vector128<uint> inverseVector = Vector128.Create(inverseLength);
        Vector128<uint> inverseShoupVector = Vector128.Create(inverseLengthShoup);
        Vector128<uint> advance = Vector128.Create((uint)ModPow(root, (uint)width, modulus));

        if (i + width <= end)
        {
            Vector128<uint> twiddle = CreateTwiddleSequenceSse(root, i, modulus);

            for (; i + width <= end; i += width)
            {
                int rightIndex = i + halfLength;

                Vector128<uint> leftVector =
                    Vector128.LoadUnsafe(ref valuesRef, (nuint)i);
                Vector128<uint> rightVector =
                    Vector128.LoadUnsafe(ref valuesRef, (nuint)rightIndex);

                rightVector = MultiplyResiduesSse(rightVector, twiddle, modulus);

                Vector128<uint> sum = AddModuloSse(leftVector, rightVector, modulusVector);
                Vector128<uint> difference = SubtractModuloSse(leftVector, rightVector, modulusVector);

                MultiplyShoupSse(
                        sum,
                        inverseVector,
                        inverseShoupVector,
                        modulusVector)
                    .StoreUnsafe(ref outputRef, (nuint)i);

                if (writeRight)
                {
                    MultiplyShoupSse(
                            difference,
                            inverseVector,
                            inverseShoupVector,
                            modulusVector)
                        .StoreUnsafe(ref outputRef, (nuint)rightIndex);
                }

                if (i + width < end)
                {
                    twiddle = MultiplyResiduesSse(twiddle, advance, modulus);
                }

                if (((i - start) & 0x7FFF) == 0x7FFF - (width - 1))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        if (i < end && allowPaddedTail)
        {
            int remaining = end - i;
            Span<uint> leftScratch = stackalloc uint[Vector128<uint>.Count];
            Span<uint> rightScratch = stackalloc uint[Vector128<uint>.Count];
            Span<uint> leftOutputScratch = stackalloc uint[Vector128<uint>.Count];
            Span<uint> rightOutputScratch = stackalloc uint[Vector128<uint>.Count];

            for (int lane = 0; lane < remaining; lane++)
            {
                leftScratch[lane] = values[i + lane];
                rightScratch[lane] = values[i + halfLength + lane];
            }

            ref uint leftScratchRef = ref MemoryMarshal.GetReference(leftScratch);
            ref uint rightScratchRef = ref MemoryMarshal.GetReference(rightScratch);
            ref uint leftOutputRef = ref MemoryMarshal.GetReference(leftOutputScratch);
            ref uint rightOutputRef = ref MemoryMarshal.GetReference(rightOutputScratch);

            Vector128<uint> leftVector = Vector128.LoadUnsafe(ref leftScratchRef);
            Vector128<uint> rightVector = MultiplyResiduesSse(
                Vector128.LoadUnsafe(ref rightScratchRef),
                CreateTwiddleSequenceSse(root, i, modulus),
                modulus);
            Vector128<uint> sum = AddModuloSse(leftVector, rightVector, modulusVector);
            Vector128<uint> difference = SubtractModuloSse(leftVector, rightVector, modulusVector);

            MultiplyShoupSse(sum, inverseVector, inverseShoupVector, modulusVector)
                .StoreUnsafe(ref leftOutputRef);
            if (writeRight)
            {
                MultiplyShoupSse(difference, inverseVector, inverseShoupVector, modulusVector)
                    .StoreUnsafe(ref rightOutputRef);
            }

            for (int lane = 0; lane < remaining; lane++)
            {
                output[i + lane] = leftOutputScratch[lane];
                if (writeRight)
                    output[i + halfLength + lane] = rightOutputScratch[lane];
            }
        }
        if (i < end && !allowPaddedTail)
        {
            uint rootSquared = (uint)((ulong)root * root % modulus);
            uint rootFourth = (uint)((ulong)rootSquared * rootSquared % modulus);
            if (writeRight)
            {
                ExecuteFinalInverseBothOutputsRange(
                    values, output, halfLength, i, end, modulus, root, rootSquared,
                    rootFourth, inverseLength, inverseLengthShoup, cancellationToken);
            }
            else
            {
                ExecuteFinalInverseLeftOnlyRange(
                    values, output, halfLength, i, end, modulus, root, rootSquared,
                    rootFourth, inverseLength, inverseLengthShoup, cancellationToken);
            }
        }

    }
}
