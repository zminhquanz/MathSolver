using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LoadPaddedUInt32(
        uint[] source,
        int sourceIndex,
        int count,
        Span<uint> scratch)
    {
        scratch.Clear();
        source.AsSpan(sourceIndex, count).CopyTo(scratch);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StorePaddedUInt32(
        ReadOnlySpan<uint> scratch,
        uint[] destination,
        int destinationIndex,
        int count) =>
        scratch[..count].CopyTo(destination.AsSpan(destinationIndex, count));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteCachedButterflyTailAvx512(
        uint[] values,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int leftIndex,
        int rightIndex,
        int twiddleIndex,
        int count,
        bool inverse,
        in Avx512NttModContext context)
    {
        Debug.Assert(count is > 0 and < 16);

        Span<uint> leftScratch = stackalloc uint[16];
        Span<uint> rightScratch = stackalloc uint[16];
        Span<uint> twiddleScratch = stackalloc uint[16];
        Span<uint> shoupScratch = stackalloc uint[16];
        Span<uint> leftOutput = stackalloc uint[16];
        Span<uint> rightOutput = stackalloc uint[16];

        LoadPaddedUInt32(values, leftIndex, count, leftScratch);
        LoadPaddedUInt32(values, rightIndex, count, rightScratch);
        LoadPaddedUInt32(twiddles, twiddleIndex, count, twiddleScratch);
        LoadPaddedUInt32(shoupTwiddles, twiddleIndex, count, shoupScratch);

        ref uint leftRef = ref MemoryMarshal.GetReference(leftScratch);
        ref uint rightRef = ref MemoryMarshal.GetReference(rightScratch);
        ref uint twiddleRef = ref MemoryMarshal.GetReference(twiddleScratch);
        ref uint shoupRef = ref MemoryMarshal.GetReference(shoupScratch);
        ref uint leftOutputRef = ref MemoryMarshal.GetReference(leftOutput);
        ref uint rightOutputRef = ref MemoryMarshal.GetReference(rightOutput);

        Vector512<uint> left = Vector512.LoadUnsafe(ref leftRef);
        Vector512<uint> right = Vector512.LoadUnsafe(ref rightRef);
        Vector512<uint> twiddle = Vector512.LoadUnsafe(ref twiddleRef);
        Vector512<uint> shoup = Vector512.LoadUnsafe(ref shoupRef);

        Vector512<uint> outputLeft;
        Vector512<uint> outputRight;
        if (inverse)
        {
            right = MultiplyShoupAvx512(right, twiddle, shoup, context);
            outputLeft = AddModuloAvx512(left, right, context);
            outputRight = SubtractModuloAvx512(left, right, context);
        }
        else
        {
            outputLeft = AddModuloAvx512(left, right, context);
            outputRight = MultiplyShoupAvx512(
                SubtractModuloAvx512(left, right, context),
                twiddle,
                shoup,
                context);
        }

        outputLeft.StoreUnsafe(ref leftOutputRef);
        outputRight.StoreUnsafe(ref rightOutputRef);
        StorePaddedUInt32(leftOutput, values, leftIndex, count);
        StorePaddedUInt32(rightOutput, values, rightIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteCachedButterflyTailAvx2(
        uint[] values,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int leftIndex,
        int rightIndex,
        int twiddleIndex,
        int count,
        bool inverse,
        in Avx2NttModContext context)
    {
        Debug.Assert(count is > 0 and < 8);

        Span<uint> leftScratch = stackalloc uint[8];
        Span<uint> rightScratch = stackalloc uint[8];
        Span<uint> twiddleScratch = stackalloc uint[8];
        Span<uint> shoupScratch = stackalloc uint[8];
        Span<uint> leftOutput = stackalloc uint[8];
        Span<uint> rightOutput = stackalloc uint[8];

        LoadPaddedUInt32(values, leftIndex, count, leftScratch);
        LoadPaddedUInt32(values, rightIndex, count, rightScratch);
        LoadPaddedUInt32(twiddles, twiddleIndex, count, twiddleScratch);
        LoadPaddedUInt32(shoupTwiddles, twiddleIndex, count, shoupScratch);

        ref uint leftRef = ref MemoryMarshal.GetReference(leftScratch);
        ref uint rightRef = ref MemoryMarshal.GetReference(rightScratch);
        ref uint twiddleRef = ref MemoryMarshal.GetReference(twiddleScratch);
        ref uint shoupRef = ref MemoryMarshal.GetReference(shoupScratch);
        ref uint leftOutputRef = ref MemoryMarshal.GetReference(leftOutput);
        ref uint rightOutputRef = ref MemoryMarshal.GetReference(rightOutput);

        Vector256<uint> left = Vector256.LoadUnsafe(ref leftRef);
        Vector256<uint> right = Vector256.LoadUnsafe(ref rightRef);
        Vector256<uint> twiddle = Vector256.LoadUnsafe(ref twiddleRef);
        Vector256<uint> shoup = Vector256.LoadUnsafe(ref shoupRef);

        Vector256<uint> outputLeft;
        Vector256<uint> outputRight;
        if (inverse)
        {
            right = MultiplyShoupAvx2(right, twiddle, shoup, context);
            outputLeft = AddModuloAvx2(left, right, context);
            outputRight = SubtractModuloAvx2(left, right, context);
        }
        else
        {
            outputLeft = AddModuloAvx2(left, right, context);
            outputRight = MultiplyShoupAvx2(
                SubtractModuloAvx2(left, right, context),
                twiddle,
                shoup,
                context);
        }

        outputLeft.StoreUnsafe(ref leftOutputRef);
        outputRight.StoreUnsafe(ref rightOutputRef);
        StorePaddedUInt32(leftOutput, values, leftIndex, count);
        StorePaddedUInt32(rightOutput, values, rightIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteCachedButterflyTailSse(
        uint[] values,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int leftIndex,
        int rightIndex,
        int twiddleIndex,
        int count,
        bool inverse,
        uint modulus)
    {
        Debug.Assert(count is > 0 and < 4);

        Span<uint> leftScratch = stackalloc uint[4];
        Span<uint> rightScratch = stackalloc uint[4];
        Span<uint> twiddleScratch = stackalloc uint[4];
        Span<uint> shoupScratch = stackalloc uint[4];
        Span<uint> leftOutput = stackalloc uint[4];
        Span<uint> rightOutput = stackalloc uint[4];

        LoadPaddedUInt32(values, leftIndex, count, leftScratch);
        LoadPaddedUInt32(values, rightIndex, count, rightScratch);
        LoadPaddedUInt32(twiddles, twiddleIndex, count, twiddleScratch);
        LoadPaddedUInt32(shoupTwiddles, twiddleIndex, count, shoupScratch);

        ref uint leftRef = ref MemoryMarshal.GetReference(leftScratch);
        ref uint rightRef = ref MemoryMarshal.GetReference(rightScratch);
        ref uint twiddleRef = ref MemoryMarshal.GetReference(twiddleScratch);
        ref uint shoupRef = ref MemoryMarshal.GetReference(shoupScratch);
        ref uint leftOutputRef = ref MemoryMarshal.GetReference(leftOutput);
        ref uint rightOutputRef = ref MemoryMarshal.GetReference(rightOutput);

        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> left = Vector128.LoadUnsafe(ref leftRef);
        Vector128<uint> right = Vector128.LoadUnsafe(ref rightRef);
        Vector128<uint> twiddle = Vector128.LoadUnsafe(ref twiddleRef);
        Vector128<uint> shoup = Vector128.LoadUnsafe(ref shoupRef);

        Vector128<uint> outputLeft;
        Vector128<uint> outputRight;
        if (inverse)
        {
            right = MultiplyShoupSse(right, twiddle, shoup, mod);
            outputLeft = AddModuloSse(left, right, mod);
            outputRight = SubtractModuloSse(left, right, mod);
        }
        else
        {
            outputLeft = AddModuloSse(left, right, mod);
            outputRight = MultiplyShoupSse(
                SubtractModuloSse(left, right, mod),
                twiddle,
                shoup,
                mod);
        }

        outputLeft.StoreUnsafe(ref leftOutputRef);
        outputRight.StoreUnsafe(ref rightOutputRef);
        StorePaddedUInt32(leftOutput, values, leftIndex, count);
        StorePaddedUInt32(rightOutput, values, rightIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteCachedButterflyTailNeon(
        uint[] values,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int leftIndex,
        int rightIndex,
        int twiddleIndex,
        int count,
        bool inverse,
        uint modulus)
    {
        Debug.Assert(count is > 0 and < 4);

        Span<uint> leftScratch = stackalloc uint[4];
        Span<uint> rightScratch = stackalloc uint[4];
        Span<uint> twiddleScratch = stackalloc uint[4];
        Span<uint> shoupScratch = stackalloc uint[4];
        Span<uint> leftOutput = stackalloc uint[4];
        Span<uint> rightOutput = stackalloc uint[4];

        LoadPaddedUInt32(values, leftIndex, count, leftScratch);
        LoadPaddedUInt32(values, rightIndex, count, rightScratch);
        LoadPaddedUInt32(twiddles, twiddleIndex, count, twiddleScratch);
        LoadPaddedUInt32(shoupTwiddles, twiddleIndex, count, shoupScratch);

        ref uint leftRef = ref MemoryMarshal.GetReference(leftScratch);
        ref uint rightRef = ref MemoryMarshal.GetReference(rightScratch);
        ref uint twiddleRef = ref MemoryMarshal.GetReference(twiddleScratch);
        ref uint shoupRef = ref MemoryMarshal.GetReference(shoupScratch);
        ref uint leftOutputRef = ref MemoryMarshal.GetReference(leftOutput);
        ref uint rightOutputRef = ref MemoryMarshal.GetReference(rightOutput);

        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> left = Vector128.LoadUnsafe(ref leftRef);
        Vector128<uint> right = Vector128.LoadUnsafe(ref rightRef);
        Vector128<uint> twiddle = Vector128.LoadUnsafe(ref twiddleRef);
        Vector128<uint> shoup = Vector128.LoadUnsafe(ref shoupRef);

        Vector128<uint> outputLeft;
        Vector128<uint> outputRight;
        if (inverse)
        {
            right = MultiplyShoupNeon(right, twiddle, shoup, mod);
            outputLeft = AddModuloNeon(left, right, mod);
            outputRight = SubtractModuloNeon(left, right, mod);
        }
        else
        {
            outputLeft = AddModuloNeon(left, right, mod);
            outputRight = MultiplyShoupNeon(
                SubtractModuloNeon(left, right, mod),
                twiddle,
                shoup,
                mod);
        }

        outputLeft.StoreUnsafe(ref leftOutputRef);
        outputRight.StoreUnsafe(ref rightOutputRef);
        StorePaddedUInt32(leftOutput, values, leftIndex, count);
        StorePaddedUInt32(rightOutput, values, rightIndex, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteForwardCachedStagePairTailAvx512(
        uint[] values,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int index0,
        int index1,
        int index2,
        int index3,
        int firstTwiddleIndex0,
        int firstTwiddleIndex1,
        int secondTwiddleIndex,
        int count,
        in Avx512NttModContext context)
    {
        Debug.Assert(count is > 0 and < 16);
        Span<uint> v0Scratch = stackalloc uint[16];
        Span<uint> v1Scratch = stackalloc uint[16];
        Span<uint> v2Scratch = stackalloc uint[16];
        Span<uint> v3Scratch = stackalloc uint[16];
        Span<uint> t0Scratch = stackalloc uint[16];
        Span<uint> t1Scratch = stackalloc uint[16];
        Span<uint> t2Scratch = stackalloc uint[16];
        Span<uint> s0Scratch = stackalloc uint[16];
        Span<uint> s1Scratch = stackalloc uint[16];
        Span<uint> s2Scratch = stackalloc uint[16];
        Span<uint> outputScratch = stackalloc uint[16];

        LoadPaddedUInt32(values, index0, count, v0Scratch);
        LoadPaddedUInt32(values, index1, count, v1Scratch);
        LoadPaddedUInt32(values, index2, count, v2Scratch);
        LoadPaddedUInt32(values, index3, count, v3Scratch);
        LoadPaddedUInt32(twiddles, firstTwiddleIndex0, count, t0Scratch);
        LoadPaddedUInt32(twiddles, firstTwiddleIndex1, count, t1Scratch);
        LoadPaddedUInt32(twiddles, secondTwiddleIndex, count, t2Scratch);
        LoadPaddedUInt32(shoupTwiddles, firstTwiddleIndex0, count, s0Scratch);
        LoadPaddedUInt32(shoupTwiddles, firstTwiddleIndex1, count, s1Scratch);
        LoadPaddedUInt32(shoupTwiddles, secondTwiddleIndex, count, s2Scratch);

        Vector512<uint> value0 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v0Scratch));
        Vector512<uint> value1 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v1Scratch));
        Vector512<uint> value2 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v2Scratch));
        Vector512<uint> value3 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v3Scratch));
        Vector512<uint> topSum0 = AddModuloAvx512(value0, value2, context);
        Vector512<uint> topSum1 = AddModuloAvx512(value1, value3, context);
        Vector512<uint> lower0 = MultiplyShoupAvx512(
            SubtractModuloAvx512(value0, value2, context),
            Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(t0Scratch)),
            Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(s0Scratch)),
            context);
        Vector512<uint> lower1 = MultiplyShoupAvx512(
            SubtractModuloAvx512(value1, value3, context),
            Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(t1Scratch)),
            Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(s1Scratch)),
            context);
        Vector512<uint> secondTwiddle = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(t2Scratch));
        Vector512<uint> secondShoup = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(s2Scratch));

        Vector512<uint> out0 = AddModuloAvx512(topSum0, topSum1, context);
        Vector512<uint> out1 = MultiplyShoupAvx512(
            SubtractModuloAvx512(topSum0, topSum1, context), secondTwiddle, secondShoup, context);
        Vector512<uint> out2 = AddModuloAvx512(lower0, lower1, context);
        Vector512<uint> out3 = MultiplyShoupAvx512(
            SubtractModuloAvx512(lower0, lower1, context), secondTwiddle, secondShoup, context);

        ref uint outputRef = ref MemoryMarshal.GetReference(outputScratch);
        out0.StoreUnsafe(ref outputRef);
        StorePaddedUInt32(outputScratch, values, index0, count);
        out1.StoreUnsafe(ref outputRef);
        StorePaddedUInt32(outputScratch, values, index1, count);
        out2.StoreUnsafe(ref outputRef);
        StorePaddedUInt32(outputScratch, values, index2, count);
        out3.StoreUnsafe(ref outputRef);
        StorePaddedUInt32(outputScratch, values, index3, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteForwardCachedStagePairTailAvx2(
        uint[] values,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int index0,
        int index1,
        int index2,
        int index3,
        int firstTwiddleIndex0,
        int firstTwiddleIndex1,
        int secondTwiddleIndex,
        int count,
        in Avx2NttModContext context)
    {
        Debug.Assert(count is > 0 and < 8);
        Span<uint> v0Scratch = stackalloc uint[8];
        Span<uint> v1Scratch = stackalloc uint[8];
        Span<uint> v2Scratch = stackalloc uint[8];
        Span<uint> v3Scratch = stackalloc uint[8];
        Span<uint> t0Scratch = stackalloc uint[8];
        Span<uint> t1Scratch = stackalloc uint[8];
        Span<uint> t2Scratch = stackalloc uint[8];
        Span<uint> s0Scratch = stackalloc uint[8];
        Span<uint> s1Scratch = stackalloc uint[8];
        Span<uint> s2Scratch = stackalloc uint[8];
        Span<uint> outputScratch = stackalloc uint[8];

        LoadPaddedUInt32(values, index0, count, v0Scratch);
        LoadPaddedUInt32(values, index1, count, v1Scratch);
        LoadPaddedUInt32(values, index2, count, v2Scratch);
        LoadPaddedUInt32(values, index3, count, v3Scratch);
        LoadPaddedUInt32(twiddles, firstTwiddleIndex0, count, t0Scratch);
        LoadPaddedUInt32(twiddles, firstTwiddleIndex1, count, t1Scratch);
        LoadPaddedUInt32(twiddles, secondTwiddleIndex, count, t2Scratch);
        LoadPaddedUInt32(shoupTwiddles, firstTwiddleIndex0, count, s0Scratch);
        LoadPaddedUInt32(shoupTwiddles, firstTwiddleIndex1, count, s1Scratch);
        LoadPaddedUInt32(shoupTwiddles, secondTwiddleIndex, count, s2Scratch);

        Vector256<uint> value0 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v0Scratch));
        Vector256<uint> value1 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v1Scratch));
        Vector256<uint> value2 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v2Scratch));
        Vector256<uint> value3 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v3Scratch));
        Vector256<uint> topSum0 = AddModuloAvx2(value0, value2, context);
        Vector256<uint> topSum1 = AddModuloAvx2(value1, value3, context);
        Vector256<uint> lower0 = MultiplyShoupAvx2(
            SubtractModuloAvx2(value0, value2, context),
            Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(t0Scratch)),
            Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(s0Scratch)),
            context);
        Vector256<uint> lower1 = MultiplyShoupAvx2(
            SubtractModuloAvx2(value1, value3, context),
            Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(t1Scratch)),
            Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(s1Scratch)),
            context);
        Vector256<uint> secondTwiddle = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(t2Scratch));
        Vector256<uint> secondShoup = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(s2Scratch));

        Vector256<uint> out0 = AddModuloAvx2(topSum0, topSum1, context);
        Vector256<uint> out1 = MultiplyShoupAvx2(
            SubtractModuloAvx2(topSum0, topSum1, context), secondTwiddle, secondShoup, context);
        Vector256<uint> out2 = AddModuloAvx2(lower0, lower1, context);
        Vector256<uint> out3 = MultiplyShoupAvx2(
            SubtractModuloAvx2(lower0, lower1, context), secondTwiddle, secondShoup, context);

        ref uint outputRef = ref MemoryMarshal.GetReference(outputScratch);
        out0.StoreUnsafe(ref outputRef);
        StorePaddedUInt32(outputScratch, values, index0, count);
        out1.StoreUnsafe(ref outputRef);
        StorePaddedUInt32(outputScratch, values, index1, count);
        out2.StoreUnsafe(ref outputRef);
        StorePaddedUInt32(outputScratch, values, index2, count);
        out3.StoreUnsafe(ref outputRef);
        StorePaddedUInt32(outputScratch, values, index3, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteInverseCachedStagePairTailAvx512(
        uint[] values,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int index0,
        int index1,
        int index2,
        int index3,
        int firstTwiddleIndex,
        int secondTwiddleIndex0,
        int secondTwiddleIndex1,
        int count,
        in Avx512NttModContext context)
    {
        Debug.Assert(count is > 0 and < 16);
        Span<uint> v0Scratch = stackalloc uint[16];
        Span<uint> v1Scratch = stackalloc uint[16];
        Span<uint> v2Scratch = stackalloc uint[16];
        Span<uint> v3Scratch = stackalloc uint[16];
        Span<uint> t0Scratch = stackalloc uint[16];
        Span<uint> t1Scratch = stackalloc uint[16];
        Span<uint> t2Scratch = stackalloc uint[16];
        Span<uint> s0Scratch = stackalloc uint[16];
        Span<uint> s1Scratch = stackalloc uint[16];
        Span<uint> s2Scratch = stackalloc uint[16];
        Span<uint> outputScratch = stackalloc uint[16];

        LoadPaddedUInt32(values, index0, count, v0Scratch);
        LoadPaddedUInt32(values, index1, count, v1Scratch);
        LoadPaddedUInt32(values, index2, count, v2Scratch);
        LoadPaddedUInt32(values, index3, count, v3Scratch);
        LoadPaddedUInt32(twiddles, firstTwiddleIndex, count, t0Scratch);
        LoadPaddedUInt32(twiddles, secondTwiddleIndex0, count, t1Scratch);
        LoadPaddedUInt32(twiddles, secondTwiddleIndex1, count, t2Scratch);
        LoadPaddedUInt32(shoupTwiddles, firstTwiddleIndex, count, s0Scratch);
        LoadPaddedUInt32(shoupTwiddles, secondTwiddleIndex0, count, s1Scratch);
        LoadPaddedUInt32(shoupTwiddles, secondTwiddleIndex1, count, s2Scratch);

        Vector512<uint> value0 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v0Scratch));
        Vector512<uint> value1 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v1Scratch));
        Vector512<uint> value2 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v2Scratch));
        Vector512<uint> value3 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v3Scratch));
        Vector512<uint> firstTwiddle = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(t0Scratch));
        Vector512<uint> firstShoup = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(s0Scratch));
        Vector512<uint> right0 = MultiplyShoupAvx512(value1, firstTwiddle, firstShoup, context);
        Vector512<uint> right1 = MultiplyShoupAvx512(value3, firstTwiddle, firstShoup, context);
        Vector512<uint> firstSum0 = AddModuloAvx512(value0, right0, context);
        Vector512<uint> firstDifference0 = SubtractModuloAvx512(value0, right0, context);
        Vector512<uint> firstSum1 = AddModuloAvx512(value2, right1, context);
        Vector512<uint> firstDifference1 = SubtractModuloAvx512(value2, right1, context);
        Vector512<uint> mergedRight0 = MultiplyShoupAvx512(
            firstSum1,
            Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(t1Scratch)),
            Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(s1Scratch)),
            context);
        Vector512<uint> mergedRight1 = MultiplyShoupAvx512(
            firstDifference1,
            Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(t2Scratch)),
            Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(s2Scratch)),
            context);
        Vector512<uint> out0 = AddModuloAvx512(firstSum0, mergedRight0, context);
        Vector512<uint> out1 = AddModuloAvx512(firstDifference0, mergedRight1, context);
        Vector512<uint> out2 = SubtractModuloAvx512(firstSum0, mergedRight0, context);
        Vector512<uint> out3 = SubtractModuloAvx512(firstDifference0, mergedRight1, context);

        ref uint outputRef = ref MemoryMarshal.GetReference(outputScratch);
        out0.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index0, count);
        out1.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index1, count);
        out2.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index2, count);
        out3.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index3, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteInverseCachedStagePairTailAvx2(
        uint[] values,
        uint[] twiddles,
        uint[] shoupTwiddles,
        int index0,
        int index1,
        int index2,
        int index3,
        int firstTwiddleIndex,
        int secondTwiddleIndex0,
        int secondTwiddleIndex1,
        int count,
        in Avx2NttModContext context)
    {
        Debug.Assert(count is > 0 and < 8);
        Span<uint> v0Scratch = stackalloc uint[8];
        Span<uint> v1Scratch = stackalloc uint[8];
        Span<uint> v2Scratch = stackalloc uint[8];
        Span<uint> v3Scratch = stackalloc uint[8];
        Span<uint> t0Scratch = stackalloc uint[8];
        Span<uint> t1Scratch = stackalloc uint[8];
        Span<uint> t2Scratch = stackalloc uint[8];
        Span<uint> s0Scratch = stackalloc uint[8];
        Span<uint> s1Scratch = stackalloc uint[8];
        Span<uint> s2Scratch = stackalloc uint[8];
        Span<uint> outputScratch = stackalloc uint[8];

        LoadPaddedUInt32(values, index0, count, v0Scratch);
        LoadPaddedUInt32(values, index1, count, v1Scratch);
        LoadPaddedUInt32(values, index2, count, v2Scratch);
        LoadPaddedUInt32(values, index3, count, v3Scratch);
        LoadPaddedUInt32(twiddles, firstTwiddleIndex, count, t0Scratch);
        LoadPaddedUInt32(twiddles, secondTwiddleIndex0, count, t1Scratch);
        LoadPaddedUInt32(twiddles, secondTwiddleIndex1, count, t2Scratch);
        LoadPaddedUInt32(shoupTwiddles, firstTwiddleIndex, count, s0Scratch);
        LoadPaddedUInt32(shoupTwiddles, secondTwiddleIndex0, count, s1Scratch);
        LoadPaddedUInt32(shoupTwiddles, secondTwiddleIndex1, count, s2Scratch);

        Vector256<uint> value0 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v0Scratch));
        Vector256<uint> value1 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v1Scratch));
        Vector256<uint> value2 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v2Scratch));
        Vector256<uint> value3 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v3Scratch));
        Vector256<uint> firstTwiddle = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(t0Scratch));
        Vector256<uint> firstShoup = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(s0Scratch));
        Vector256<uint> right0 = MultiplyShoupAvx2(value1, firstTwiddle, firstShoup, context);
        Vector256<uint> right1 = MultiplyShoupAvx2(value3, firstTwiddle, firstShoup, context);
        Vector256<uint> firstSum0 = AddModuloAvx2(value0, right0, context);
        Vector256<uint> firstDifference0 = SubtractModuloAvx2(value0, right0, context);
        Vector256<uint> firstSum1 = AddModuloAvx2(value2, right1, context);
        Vector256<uint> firstDifference1 = SubtractModuloAvx2(value2, right1, context);
        Vector256<uint> mergedRight0 = MultiplyShoupAvx2(
            firstSum1,
            Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(t1Scratch)),
            Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(s1Scratch)),
            context);
        Vector256<uint> mergedRight1 = MultiplyShoupAvx2(
            firstDifference1,
            Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(t2Scratch)),
            Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(s2Scratch)),
            context);
        Vector256<uint> out0 = AddModuloAvx2(firstSum0, mergedRight0, context);
        Vector256<uint> out1 = AddModuloAvx2(firstDifference0, mergedRight1, context);
        Vector256<uint> out2 = SubtractModuloAvx2(firstSum0, mergedRight0, context);
        Vector256<uint> out3 = SubtractModuloAvx2(firstDifference0, mergedRight1, context);

        ref uint outputRef = ref MemoryMarshal.GetReference(outputScratch);
        out0.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index0, count);
        out1.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index1, count);
        out2.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index2, count);
        out3.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index3, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessForwardUncachedStagePairTailAvx512(
        uint[] values,
        int groupOffset,
        int quarterLength,
        int i,
        int count,
        Vector512<uint> twiddle0,
        Vector512<uint> twiddle1,
        Vector512<uint> twiddle2,
        uint modulus,
        in Avx512NttModContext context)
    {
        Span<uint> v0Scratch = stackalloc uint[16];
        Span<uint> v1Scratch = stackalloc uint[16];
        Span<uint> v2Scratch = stackalloc uint[16];
        Span<uint> v3Scratch = stackalloc uint[16];
        Span<uint> outputScratch = stackalloc uint[16];
        int index0 = groupOffset + i;
        int index1 = index0 + quarterLength;
        int index2 = index1 + quarterLength;
        int index3 = index2 + quarterLength;
        LoadPaddedUInt32(values, index0, count, v0Scratch);
        LoadPaddedUInt32(values, index1, count, v1Scratch);
        LoadPaddedUInt32(values, index2, count, v2Scratch);
        LoadPaddedUInt32(values, index3, count, v3Scratch);
        Vector512<uint> value0 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v0Scratch));
        Vector512<uint> value1 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v1Scratch));
        Vector512<uint> value2 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v2Scratch));
        Vector512<uint> value3 = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(v3Scratch));
        Vector512<uint> sum0 = AddModuloAvx512(value0, value2, context);
        Vector512<uint> sum1 = AddModuloAvx512(value1, value3, context);
        Vector512<uint> lower0 = MultiplyResiduesAvx512(SubtractModuloAvx512(value0, value2, context), twiddle0, modulus);
        Vector512<uint> lower1 = MultiplyResiduesAvx512(SubtractModuloAvx512(value1, value3, context), twiddle1, modulus);
        Vector512<uint> out0 = AddModuloAvx512(sum0, sum1, context);
        Vector512<uint> out1 = MultiplyResiduesAvx512(SubtractModuloAvx512(sum0, sum1, context), twiddle2, modulus);
        Vector512<uint> out2 = AddModuloAvx512(lower0, lower1, context);
        Vector512<uint> out3 = MultiplyResiduesAvx512(SubtractModuloAvx512(lower0, lower1, context), twiddle2, modulus);
        ref uint outputRef = ref MemoryMarshal.GetReference(outputScratch);
        out0.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index0, count);
        out1.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index1, count);
        out2.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index2, count);
        out3.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index3, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessForwardUncachedStagePairTailAvx2(
        uint[] values,
        int groupOffset,
        int quarterLength,
        int i,
        int count,
        Vector256<uint> twiddle0,
        Vector256<uint> twiddle1,
        Vector256<uint> twiddle2,
        uint modulus,
        in Avx2NttModContext context)
    {
        Span<uint> v0Scratch = stackalloc uint[8];
        Span<uint> v1Scratch = stackalloc uint[8];
        Span<uint> v2Scratch = stackalloc uint[8];
        Span<uint> v3Scratch = stackalloc uint[8];
        Span<uint> outputScratch = stackalloc uint[8];
        int index0 = groupOffset + i;
        int index1 = index0 + quarterLength;
        int index2 = index1 + quarterLength;
        int index3 = index2 + quarterLength;
        LoadPaddedUInt32(values, index0, count, v0Scratch);
        LoadPaddedUInt32(values, index1, count, v1Scratch);
        LoadPaddedUInt32(values, index2, count, v2Scratch);
        LoadPaddedUInt32(values, index3, count, v3Scratch);
        Vector256<uint> value0 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v0Scratch));
        Vector256<uint> value1 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v1Scratch));
        Vector256<uint> value2 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v2Scratch));
        Vector256<uint> value3 = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(v3Scratch));
        Vector256<uint> sum0 = AddModuloAvx2(value0, value2, context);
        Vector256<uint> sum1 = AddModuloAvx2(value1, value3, context);
        Vector256<uint> lower0 = MultiplyResiduesAvx2(SubtractModuloAvx2(value0, value2, context), twiddle0, modulus);
        Vector256<uint> lower1 = MultiplyResiduesAvx2(SubtractModuloAvx2(value1, value3, context), twiddle1, modulus);
        Vector256<uint> out0 = AddModuloAvx2(sum0, sum1, context);
        Vector256<uint> out1 = MultiplyResiduesAvx2(SubtractModuloAvx2(sum0, sum1, context), twiddle2, modulus);
        Vector256<uint> out2 = AddModuloAvx2(lower0, lower1, context);
        Vector256<uint> out3 = MultiplyResiduesAvx2(SubtractModuloAvx2(lower0, lower1, context), twiddle2, modulus);
        ref uint outputRef = ref MemoryMarshal.GetReference(outputScratch);
        out0.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index0, count);
        out1.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index1, count);
        out2.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index2, count);
        out3.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index3, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessForwardUncachedStagePairTailSse(
        uint[] values,
        int groupOffset,
        int quarterLength,
        int i,
        int count,
        Vector128<uint> twiddle0,
        Vector128<uint> twiddle1,
        Vector128<uint> twiddle2,
        uint modulus)
    {
        Span<uint> v0Scratch = stackalloc uint[4];
        Span<uint> v1Scratch = stackalloc uint[4];
        Span<uint> v2Scratch = stackalloc uint[4];
        Span<uint> v3Scratch = stackalloc uint[4];
        Span<uint> outputScratch = stackalloc uint[4];
        int index0 = groupOffset + i;
        int index1 = index0 + quarterLength;
        int index2 = index1 + quarterLength;
        int index3 = index2 + quarterLength;
        LoadPaddedUInt32(values, index0, count, v0Scratch);
        LoadPaddedUInt32(values, index1, count, v1Scratch);
        LoadPaddedUInt32(values, index2, count, v2Scratch);
        LoadPaddedUInt32(values, index3, count, v3Scratch);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> value0 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(v0Scratch));
        Vector128<uint> value1 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(v1Scratch));
        Vector128<uint> value2 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(v2Scratch));
        Vector128<uint> value3 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(v3Scratch));
        Vector128<uint> sum0 = AddModuloSse(value0, value2, mod);
        Vector128<uint> sum1 = AddModuloSse(value1, value3, mod);
        Vector128<uint> lower0 = MultiplyResiduesSse(SubtractModuloSse(value0, value2, mod), twiddle0, modulus);
        Vector128<uint> lower1 = MultiplyResiduesSse(SubtractModuloSse(value1, value3, mod), twiddle1, modulus);
        Vector128<uint> out0 = AddModuloSse(sum0, sum1, mod);
        Vector128<uint> out1 = MultiplyResiduesSse(SubtractModuloSse(sum0, sum1, mod), twiddle2, modulus);
        Vector128<uint> out2 = AddModuloSse(lower0, lower1, mod);
        Vector128<uint> out3 = MultiplyResiduesSse(SubtractModuloSse(lower0, lower1, mod), twiddle2, modulus);
        ref uint outputRef = ref MemoryMarshal.GetReference(outputScratch);
        out0.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index0, count);
        out1.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index1, count);
        out2.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index2, count);
        out3.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index3, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessForwardUncachedStagePairTailNeon(
        uint[] values,
        int groupOffset,
        int quarterLength,
        int i,
        int count,
        Vector128<uint> twiddle0,
        Vector128<uint> twiddle1,
        Vector128<uint> twiddle2,
        uint modulus)
    {
        Span<uint> v0Scratch = stackalloc uint[4];
        Span<uint> v1Scratch = stackalloc uint[4];
        Span<uint> v2Scratch = stackalloc uint[4];
        Span<uint> v3Scratch = stackalloc uint[4];
        Span<uint> outputScratch = stackalloc uint[4];
        int index0 = groupOffset + i;
        int index1 = index0 + quarterLength;
        int index2 = index1 + quarterLength;
        int index3 = index2 + quarterLength;
        LoadPaddedUInt32(values, index0, count, v0Scratch);
        LoadPaddedUInt32(values, index1, count, v1Scratch);
        LoadPaddedUInt32(values, index2, count, v2Scratch);
        LoadPaddedUInt32(values, index3, count, v3Scratch);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> value0 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(v0Scratch));
        Vector128<uint> value1 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(v1Scratch));
        Vector128<uint> value2 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(v2Scratch));
        Vector128<uint> value3 = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(v3Scratch));
        Vector128<uint> sum0 = AddModuloNeon(value0, value2, mod);
        Vector128<uint> sum1 = AddModuloNeon(value1, value3, mod);
        Vector128<uint> lower0 = MultiplyResiduesNeon(SubtractModuloNeon(value0, value2, mod), twiddle0, modulus);
        Vector128<uint> lower1 = MultiplyResiduesNeon(SubtractModuloNeon(value1, value3, mod), twiddle1, modulus);
        Vector128<uint> out0 = AddModuloNeon(sum0, sum1, mod);
        Vector128<uint> out1 = MultiplyResiduesNeon(SubtractModuloNeon(sum0, sum1, mod), twiddle2, modulus);
        Vector128<uint> out2 = AddModuloNeon(lower0, lower1, mod);
        Vector128<uint> out3 = MultiplyResiduesNeon(SubtractModuloNeon(lower0, lower1, mod), twiddle2, modulus);
        ref uint outputRef = ref MemoryMarshal.GetReference(outputScratch);
        out0.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index0, count);
        out1.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index1, count);
        out2.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index2, count);
        out3.StoreUnsafe(ref outputRef); StorePaddedUInt32(outputScratch, values, index3, count);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteLengthFourAndTwoFusedTailAvx512(
        uint[] values, int index, int count, uint quarterTurnTwiddle,
        uint quarterTurnShoup, bool inverse, in Avx512NttModContext context)
    {
        Debug.Assert(count > 0 && count < 64 && (count & 3) == 0);
        Span<uint> scratch = stackalloc uint[64];
        scratch.Clear();
        values.AsSpan(index, count).CopyTo(scratch);
        ref uint data = ref MemoryMarshal.GetReference(scratch);
        Vector512<uint> twiddle = Vector512.Create(quarterTurnTwiddle);
        Vector512<uint> shoup = Vector512.Create(quarterTurnShoup);
        TransposeRadix4Avx512(
            Vector512.LoadUnsafe(ref data),
            Vector512.LoadUnsafe(ref data, (nuint)16),
            Vector512.LoadUnsafe(ref data, (nuint)32),
            Vector512.LoadUnsafe(ref data, (nuint)48),
            out Vector512<uint> value0, out Vector512<uint> value1,
            out Vector512<uint> value2, out Vector512<uint> value3);
        Vector512<uint> output0, output1, output2, output3;
        if (inverse)
        {
            Vector512<uint> leftSum = AddModuloAvx512(value0, value1, context);
            Vector512<uint> rightSum = AddModuloAvx512(value2, value3, context);
            Vector512<uint> leftDifference = SubtractModuloAvx512(value0, value1, context);
            Vector512<uint> rightDifference = MultiplyShoupAvx512(
                SubtractModuloAvx512(value2, value3, context), twiddle, shoup, context);
            TransposeRadix4Avx512(
                AddModuloAvx512(leftSum, rightSum, context),
                AddModuloAvx512(leftDifference, rightDifference, context),
                SubtractModuloAvx512(leftSum, rightSum, context),
                SubtractModuloAvx512(leftDifference, rightDifference, context),
                out output0, out output1, out output2, out output3);
        }
        else
        {
            Vector512<uint> topSum0 = AddModuloAvx512(value0, value2, context);
            Vector512<uint> topSum1 = AddModuloAvx512(value1, value3, context);
            Vector512<uint> lower0 = SubtractModuloAvx512(value0, value2, context);
            Vector512<uint> lower1 = MultiplyShoupAvx512(
                SubtractModuloAvx512(value1, value3, context), twiddle, shoup, context);
            TransposeRadix4Avx512(
                AddModuloAvx512(topSum0, topSum1, context),
                SubtractModuloAvx512(topSum0, topSum1, context),
                AddModuloAvx512(lower0, lower1, context),
                SubtractModuloAvx512(lower0, lower1, context),
                out output0, out output1, out output2, out output3);
        }
        output0.StoreUnsafe(ref data);
        output1.StoreUnsafe(ref data, (nuint)16);
        output2.StoreUnsafe(ref data, (nuint)32);
        output3.StoreUnsafe(ref data, (nuint)48);
        scratch[..count].CopyTo(values.AsSpan(index, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteLengthFourAndTwoFusedTailAvx2(
        uint[] values, int index, int count, uint quarterTurnTwiddle,
        uint quarterTurnShoup, bool inverse, in Avx2NttModContext context)
    {
        Debug.Assert(count > 0 && count < 32 && (count & 3) == 0);
        Span<uint> scratch = stackalloc uint[32];
        scratch.Clear();
        values.AsSpan(index, count).CopyTo(scratch);
        ref uint data = ref MemoryMarshal.GetReference(scratch);
        Vector256<uint> twiddle = Vector256.Create(quarterTurnTwiddle);
        Vector256<uint> shoup = Vector256.Create(quarterTurnShoup);
        TransposeRadix4Avx2(
            Vector256.LoadUnsafe(ref data),
            Vector256.LoadUnsafe(ref data, (nuint)8),
            Vector256.LoadUnsafe(ref data, (nuint)16),
            Vector256.LoadUnsafe(ref data, (nuint)24),
            out Vector256<uint> value0, out Vector256<uint> value1,
            out Vector256<uint> value2, out Vector256<uint> value3);
        Vector256<uint> output0, output1, output2, output3;
        if (inverse)
        {
            Vector256<uint> leftSum = AddModuloAvx2(value0, value1, context);
            Vector256<uint> rightSum = AddModuloAvx2(value2, value3, context);
            Vector256<uint> leftDifference = SubtractModuloAvx2(value0, value1, context);
            Vector256<uint> rightDifference = MultiplyShoupAvx2(
                SubtractModuloAvx2(value2, value3, context), twiddle, shoup, context);
            TransposeRadix4Avx2(
                AddModuloAvx2(leftSum, rightSum, context),
                AddModuloAvx2(leftDifference, rightDifference, context),
                SubtractModuloAvx2(leftSum, rightSum, context),
                SubtractModuloAvx2(leftDifference, rightDifference, context),
                out output0, out output1, out output2, out output3);
        }
        else
        {
            Vector256<uint> topSum0 = AddModuloAvx2(value0, value2, context);
            Vector256<uint> topSum1 = AddModuloAvx2(value1, value3, context);
            Vector256<uint> lower0 = SubtractModuloAvx2(value0, value2, context);
            Vector256<uint> lower1 = MultiplyShoupAvx2(
                SubtractModuloAvx2(value1, value3, context), twiddle, shoup, context);
            TransposeRadix4Avx2(
                AddModuloAvx2(topSum0, topSum1, context),
                SubtractModuloAvx2(topSum0, topSum1, context),
                AddModuloAvx2(lower0, lower1, context),
                SubtractModuloAvx2(lower0, lower1, context),
                out output0, out output1, out output2, out output3);
        }
        output0.StoreUnsafe(ref data);
        output1.StoreUnsafe(ref data, (nuint)8);
        output2.StoreUnsafe(ref data, (nuint)16);
        output3.StoreUnsafe(ref data, (nuint)24);
        scratch[..count].CopyTo(values.AsSpan(index, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteLengthFourAndTwoFusedTailSse(
        uint[] values, int index, int count, uint modulus, uint quarterTurnTwiddle,
        uint quarterTurnShoup, bool inverse)
    {
        Debug.Assert(count > 0 && count < 16 && (count & 3) == 0);
        Span<uint> scratch = stackalloc uint[16];
        scratch.Clear();
        values.AsSpan(index, count).CopyTo(scratch);
        ref uint data = ref MemoryMarshal.GetReference(scratch);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> twiddle = Vector128.Create(quarterTurnTwiddle);
        Vector128<uint> shoup = Vector128.Create(quarterTurnShoup);
        TransposeRadix4Sse(
            Vector128.LoadUnsafe(ref data),
            Vector128.LoadUnsafe(ref data, (nuint)4),
            Vector128.LoadUnsafe(ref data, (nuint)8),
            Vector128.LoadUnsafe(ref data, (nuint)12),
            out Vector128<uint> value0, out Vector128<uint> value1,
            out Vector128<uint> value2, out Vector128<uint> value3);
        Vector128<uint> output0, output1, output2, output3;
        if (inverse)
        {
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
                out output0, out output1, out output2, out output3);
        }
        else
        {
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
                out output0, out output1, out output2, out output3);
        }
        output0.StoreUnsafe(ref data);
        output1.StoreUnsafe(ref data, (nuint)4);
        output2.StoreUnsafe(ref data, (nuint)8);
        output3.StoreUnsafe(ref data, (nuint)12);
        scratch[..count].CopyTo(values.AsSpan(index, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteLengthFourAndTwoFusedTailNeon(
        uint[] values, int index, int count, uint modulus, uint quarterTurnTwiddle,
        uint quarterTurnShoup, bool inverse)
    {
        Debug.Assert(count > 0 && count < 16 && (count & 3) == 0);
        Span<uint> scratch = stackalloc uint[16];
        scratch.Clear();
        values.AsSpan(index, count).CopyTo(scratch);
        ref uint data = ref MemoryMarshal.GetReference(scratch);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> twiddle = Vector128.Create(quarterTurnTwiddle);
        Vector128<uint> shoup = Vector128.Create(quarterTurnShoup);
        TransposeRadix4Neon(
            Vector128.LoadUnsafe(ref data),
            Vector128.LoadUnsafe(ref data, (nuint)4),
            Vector128.LoadUnsafe(ref data, (nuint)8),
            Vector128.LoadUnsafe(ref data, (nuint)12),
            out Vector128<uint> value0, out Vector128<uint> value1,
            out Vector128<uint> value2, out Vector128<uint> value3);
        Vector128<uint> output0, output1, output2, output3;
        if (inverse)
        {
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
                out output0, out output1, out output2, out output3);
        }
        else
        {
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
                out output0, out output1, out output2, out output3);
        }
        output0.StoreUnsafe(ref data);
        output1.StoreUnsafe(ref data, (nuint)4);
        output2.StoreUnsafe(ref data, (nuint)8);
        output3.StoreUnsafe(ref data, (nuint)12);
        scratch[..count].CopyTo(values.AsSpan(index, count));
    }

}
