using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    #region NTT residual stages
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
    #endregion

    #region Inverse fusion
    /// <summary>
    /// Inverse-DIT two-stage fusion for both worker schedules.  Stage S and its parent 2S are
    /// completed while four quarter streams are resident in vector registers.
    /// When <paramref name="normalizeFinal"/> is true the parent is the final
    /// NTT stage and the normalized result is written directly to output.
    /// Persistent teams retain static dispatch over vector-aligned slices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseUncachedStagePairSegmented(
        uint[] values,
        uint[] output,
        int validOutputLength,
        uint modulus,
        uint inversePrimitiveRoot,
        uint inverseLength,
        uint inverseLengthShoup,
        int stageLength,
        bool normalizeFinal,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int parentLength = checked(stageLength << 1);
        int halfLength = stageLength >> 1;
        int parentCount = values.Length / parentLength;
        Debug.Assert(parentCount > 0);
        Debug.Assert(!normalizeFinal || parentLength == values.Length);

        uint firstRoot = (uint)ModPow(
            inversePrimitiveRoot,
            (modulus - 1u) / (uint)stageLength,
            modulus);
        uint secondRoot = (uint)ModPow(
            inversePrimitiveRoot,
            (modulus - 1u) / (uint)parentLength,
            modulus);
        uint secondPhase = (uint)ModPow(secondRoot, (uint)halfLength, modulus);

        int width =
            ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512InverseGlobal) && Avx512F.IsSupported)
                ? Vector512<uint>.Count
                : (workers.UseAvx2Ntt && Avx2.IsSupported)
                    ? Vector256<uint>.Count
                    : (workers.UseSseNtt && Sse2.IsSupported)
                        ? Vector128<uint>.Count
#if ANDROID
                        : (workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated))
                            ? Vector128<uint>.Count
#endif
                            : 1;

        if (width <= 1 || halfLength < width || (halfLength % width) != 0)
            throw new InvalidOperationException("Inverse stage-pair SIMD fusion requires a vector-aligned half stage.");

        int segmentsPerParent = GetFusionAlignedSegmentsPerGroup(
            halfLength, parentCount, workers, width);

        ExecuteRanges(
            checked(parentCount * segmentsPerParent),
            workers,
            cancellationToken,
            (segmentStart, segmentEnd) =>
            {
                for (int segmentIndex = segmentStart; segmentIndex < segmentEnd; segmentIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    GetFusionAlignedSegmentBounds(
                        segmentIndex,
                        segmentsPerParent,
                        halfLength,
                        width,
                        workers,
                        out int parentIndex,
                        out int first,
                        out int last);

                    if ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512InverseGlobal) && Avx512F.IsSupported)
                    {
                        ProcessInverseUncachedStagePairAvx512(
                            values, output, validOutputLength, modulus,
                            firstRoot, secondRoot, secondPhase,
                            inverseLength, inverseLengthShoup,
                            stageLength, parentIndex, first, last,
                            normalizeFinal, cancellationToken);
                    }
                    else if (workers.UseAvx2Ntt && Avx2.IsSupported)
                    {
                        ProcessInverseUncachedStagePairAvx2(
                            values, output, validOutputLength, modulus,
                            firstRoot, secondRoot, secondPhase,
                            inverseLength, inverseLengthShoup,
                            stageLength, parentIndex, first, last,
                            normalizeFinal, cancellationToken);
                    }
                    else if (workers.UseSseNtt && Sse2.IsSupported)
                    {
                        ProcessInverseUncachedStagePairSse(
                            values, output, validOutputLength, modulus,
                            firstRoot, secondRoot, secondPhase,
                            inverseLength, inverseLengthShoup,
                            stageLength, parentIndex, first, last,
                            normalizeFinal, cancellationToken);
                    }
#if ANDROID
                    else if (workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated))
                    {
                        ProcessInverseUncachedStagePairNeon(
                            values, output, validOutputLength, modulus,
                            firstRoot, secondRoot, secondPhase,
                            inverseLength, inverseLengthShoup,
                            stageLength, parentIndex, first, last,
                            normalizeFinal, cancellationToken);
                    }
#endif
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreFinalAvx512(
        Vector512<uint> value, uint[] output, int index, int validOutputLength)
    {
        if (index >= validOutputLength) return;
        ref uint outputRef = ref MemoryMarshal.GetArrayDataReference(output);
        if (index + Vector512<uint>.Count <= validOutputLength)
        {
            value.StoreUnsafe(ref outputRef, (nuint)index);
            return;
        }

        Span<uint> scratch = stackalloc uint[Vector512<uint>.Count];
        ref uint scratchRef = ref MemoryMarshal.GetReference(scratch);
        value.StoreUnsafe(ref scratchRef);
        int remaining = validOutputLength - index;
        for (int lane = 0; lane < remaining; lane++)
            output[index + lane] = scratch[lane];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreFinalAvx2(
        Vector256<uint> value, uint[] output, int index, int validOutputLength)
    {
        if (index >= validOutputLength) return;
        ref uint outputRef = ref MemoryMarshal.GetArrayDataReference(output);
        if (index + Vector256<uint>.Count <= validOutputLength)
        {
            value.StoreUnsafe(ref outputRef, (nuint)index);
            return;
        }

        Span<uint> scratch = stackalloc uint[Vector256<uint>.Count];
        ref uint scratchRef = ref MemoryMarshal.GetReference(scratch);
        value.StoreUnsafe(ref scratchRef);
        int remaining = validOutputLength - index;
        for (int lane = 0; lane < remaining; lane++)
            output[index + lane] = scratch[lane];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreFinal128(
        Vector128<uint> value, uint[] output, int index, int validOutputLength)
    {
        if (index >= validOutputLength) return;
        ref uint outputRef = ref MemoryMarshal.GetArrayDataReference(output);
        if (index + Vector128<uint>.Count <= validOutputLength)
        {
            value.StoreUnsafe(ref outputRef, (nuint)index);
            return;
        }

        Span<uint> scratch = stackalloc uint[Vector128<uint>.Count];
        ref uint scratchRef = ref MemoryMarshal.GetReference(scratch);
        value.StoreUnsafe(ref scratchRef);
        int remaining = validOutputLength - index;
        for (int lane = 0; lane < remaining; lane++)
            output[index + lane] = scratch[lane];
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessInverseUncachedStagePairAvx512(
        uint[] values, uint[] output, int validOutputLength, uint modulus,
        uint firstRoot, uint secondRoot, uint secondPhase,
        uint inverseLength, uint inverseLengthShoup,
        int stageLength, int parentIndex, int first, int last,
        bool normalizeFinal, CancellationToken cancellationToken)
    {
        const int Width = 16;
        int halfLength = stageLength >> 1;
        int parentOffset = parentIndex * (stageLength << 1);
        var context = new Avx512NttModContext(modulus);
        Vector512<uint> t1 = CreateTwiddleSequenceAvx512(firstRoot, first, modulus);
        Vector512<uint> t20 = CreateTwiddleSequenceAvx512(secondRoot, first, modulus);
        Vector512<uint> t21 = MultiplyResiduesAvx512(t20, Vector512.Create(secondPhase), modulus);
        uint step1 = (uint)ModPow(firstRoot, Width, modulus);
        uint step2 = (uint)ModPow(secondRoot, Width, modulus);
        Vector512<uint> advance1 = Vector512.Create(step1);
        Vector512<uint> advance2 = Vector512.Create(step2);
        Vector512<uint> inv = Vector512.Create(inverseLength);
        Vector512<uint> invShoup = Vector512.Create(inverseLengthShoup);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

        for (int i = first; i < last; i += Width)
        {
            int index0 = parentOffset + i;
            int index1 = index0 + halfLength;
            int index2 = index0 + stageLength;
            int index3 = index2 + halfLength;
            Vector512<uint> a = Vector512.LoadUnsafe(ref data, (nuint)index0);
            Vector512<uint> b = Vector512.LoadUnsafe(ref data, (nuint)index1);
            Vector512<uint> c = Vector512.LoadUnsafe(ref data, (nuint)index2);
            Vector512<uint> d = Vector512.LoadUnsafe(ref data, (nuint)index3);

            Vector512<uint> br = MultiplyResiduesAvx512(b, t1, modulus);
            Vector512<uint> dr = MultiplyResiduesAvx512(d, t1, modulus);
            Vector512<uint> s0 = AddModuloAvx512(a, br, context);
            Vector512<uint> q0 = SubtractModuloAvx512(a, br, context);
            Vector512<uint> s1 = AddModuloAvx512(c, dr, context);
            Vector512<uint> q1 = SubtractModuloAvx512(c, dr, context);
            Vector512<uint> m0 = MultiplyResiduesAvx512(s1, t20, modulus);
            Vector512<uint> m1 = MultiplyResiduesAvx512(q1, t21, modulus);
            Vector512<uint> o0 = AddModuloAvx512(s0, m0, context);
            Vector512<uint> o1 = AddModuloAvx512(q0, m1, context);
            Vector512<uint> o2 = SubtractModuloAvx512(s0, m0, context);
            Vector512<uint> o3 = SubtractModuloAvx512(q0, m1, context);

            if (normalizeFinal)
            {
                o0 = MultiplyShoupAvx512(o0, inv, invShoup, context);
                o1 = MultiplyShoupAvx512(o1, inv, invShoup, context);
                o2 = MultiplyShoupAvx512(o2, inv, invShoup, context);
                o3 = MultiplyShoupAvx512(o3, inv, invShoup, context);
                StoreFinalAvx512(o0, output, index0, validOutputLength);
                StoreFinalAvx512(o1, output, index1, validOutputLength);
                StoreFinalAvx512(o2, output, index2, validOutputLength);
                StoreFinalAvx512(o3, output, index3, validOutputLength);
            }
            else
            {
                o0.StoreUnsafe(ref data, (nuint)index0);
                o1.StoreUnsafe(ref data, (nuint)index1);
                o2.StoreUnsafe(ref data, (nuint)index2);
                o3.StoreUnsafe(ref data, (nuint)index3);
            }

            if (i + Width < last)
            {
                t1 = MultiplyResiduesAvx512(t1, advance1, modulus);
                t20 = MultiplyResiduesAvx512(t20, advance2, modulus);
                t21 = MultiplyResiduesAvx512(t21, advance2, modulus);
            }
            if (((i - first) & 0x3FFF) == 0x3FF0)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessInverseUncachedStagePairAvx2(
        uint[] values, uint[] output, int validOutputLength, uint modulus,
        uint firstRoot, uint secondRoot, uint secondPhase,
        uint inverseLength, uint inverseLengthShoup,
        int stageLength, int parentIndex, int first, int last,
        bool normalizeFinal, CancellationToken cancellationToken)
    {
        const int Width = 8;
        int halfLength = stageLength >> 1;
        int parentOffset = parentIndex * (stageLength << 1);
        var context = new Avx2NttModContext(modulus);
        Vector256<uint> t1 = CreateTwiddleSequenceAvx2(firstRoot, first, modulus);
        Vector256<uint> t20 = CreateTwiddleSequenceAvx2(secondRoot, first, modulus);
        Vector256<uint> t21 = MultiplyResiduesAvx2(t20, Vector256.Create(secondPhase), modulus);
        Vector256<uint> advance1 = Vector256.Create((uint)ModPow(firstRoot, Width, modulus));
        Vector256<uint> advance2 = Vector256.Create((uint)ModPow(secondRoot, Width, modulus));
        Vector256<uint> inv = Vector256.Create(inverseLength);
        Vector256<uint> invShoup = Vector256.Create(inverseLengthShoup);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

        for (int i = first; i < last; i += Width)
        {
            int index0 = parentOffset + i;
            int index1 = index0 + halfLength;
            int index2 = index0 + stageLength;
            int index3 = index2 + halfLength;
            Vector256<uint> a = Vector256.LoadUnsafe(ref data, (nuint)index0);
            Vector256<uint> b = Vector256.LoadUnsafe(ref data, (nuint)index1);
            Vector256<uint> c = Vector256.LoadUnsafe(ref data, (nuint)index2);
            Vector256<uint> d = Vector256.LoadUnsafe(ref data, (nuint)index3);
            Vector256<uint> br = MultiplyResiduesAvx2(b, t1, modulus);
            Vector256<uint> dr = MultiplyResiduesAvx2(d, t1, modulus);
            Vector256<uint> s0 = AddModuloAvx2(a, br, context);
            Vector256<uint> q0 = SubtractModuloAvx2(a, br, context);
            Vector256<uint> s1 = AddModuloAvx2(c, dr, context);
            Vector256<uint> q1 = SubtractModuloAvx2(c, dr, context);
            Vector256<uint> m0 = MultiplyResiduesAvx2(s1, t20, modulus);
            Vector256<uint> m1 = MultiplyResiduesAvx2(q1, t21, modulus);
            Vector256<uint> o0 = AddModuloAvx2(s0, m0, context);
            Vector256<uint> o1 = AddModuloAvx2(q0, m1, context);
            Vector256<uint> o2 = SubtractModuloAvx2(s0, m0, context);
            Vector256<uint> o3 = SubtractModuloAvx2(q0, m1, context);

            if (normalizeFinal)
            {
                o0 = MultiplyShoupAvx2(o0, inv, invShoup, context);
                o1 = MultiplyShoupAvx2(o1, inv, invShoup, context);
                o2 = MultiplyShoupAvx2(o2, inv, invShoup, context);
                o3 = MultiplyShoupAvx2(o3, inv, invShoup, context);
                StoreFinalAvx2(o0, output, index0, validOutputLength);
                StoreFinalAvx2(o1, output, index1, validOutputLength);
                StoreFinalAvx2(o2, output, index2, validOutputLength);
                StoreFinalAvx2(o3, output, index3, validOutputLength);
            }
            else
            {
                o0.StoreUnsafe(ref data, (nuint)index0);
                o1.StoreUnsafe(ref data, (nuint)index1);
                o2.StoreUnsafe(ref data, (nuint)index2);
                o3.StoreUnsafe(ref data, (nuint)index3);
            }

            if (i + Width < last)
            {
                t1 = MultiplyResiduesAvx2(t1, advance1, modulus);
                t20 = MultiplyResiduesAvx2(t20, advance2, modulus);
                t21 = MultiplyResiduesAvx2(t21, advance2, modulus);
            }
            if (((i - first) & 0x3FFF) == 0x3FF8)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessInverseUncachedStagePairSse(
        uint[] values, uint[] output, int validOutputLength, uint modulus,
        uint firstRoot, uint secondRoot, uint secondPhase,
        uint inverseLength, uint inverseLengthShoup,
        int stageLength, int parentIndex, int first, int last,
        bool normalizeFinal, CancellationToken cancellationToken)
    {
        const int Width = 4;
        int halfLength = stageLength >> 1;
        int parentOffset = parentIndex * (stageLength << 1);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> t1 = CreateTwiddleSequenceSse(firstRoot, first, modulus);
        Vector128<uint> t20 = CreateTwiddleSequenceSse(secondRoot, first, modulus);
        Vector128<uint> t21 = MultiplyResiduesSse(t20, Vector128.Create(secondPhase), modulus);
        // The stride factors are invariant for this segment. Build their
        // Shoup companions once instead of reducing three general products
        // on every four-butterfly iteration.
        uint step1 = (uint)ModPow(firstRoot, Width, modulus);
        uint step2 = (uint)ModPow(secondRoot, Width, modulus);
        Vector128<uint> advance1 = Vector128.Create(step1);
        Vector128<uint> advance2 = Vector128.Create(step2);
        Vector128<uint> advanceShoup1 = Vector128.Create((uint)(((ulong)step1 << 32) / modulus));
        Vector128<uint> advanceShoup2 = Vector128.Create((uint)(((ulong)step2 << 32) / modulus));
        Vector128<uint> inv = Vector128.Create(inverseLength);
        Vector128<uint> invShoup = Vector128.Create(inverseLengthShoup);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

        for (int i = first; i < last; i += Width)
        {
            int index0 = parentOffset + i;
            int index1 = index0 + halfLength;
            int index2 = index0 + stageLength;
            int index3 = index2 + halfLength;
            Vector128<uint> a = Vector128.LoadUnsafe(ref data, (nuint)index0);
            Vector128<uint> b = Vector128.LoadUnsafe(ref data, (nuint)index1);
            Vector128<uint> c = Vector128.LoadUnsafe(ref data, (nuint)index2);
            Vector128<uint> d = Vector128.LoadUnsafe(ref data, (nuint)index3);
            Vector128<uint> br = MultiplyResiduesSse(b, t1, modulus);
            Vector128<uint> dr = MultiplyResiduesSse(d, t1, modulus);
            Vector128<uint> s0 = AddModuloSse(a, br, mod);
            Vector128<uint> q0 = SubtractModuloSse(a, br, mod);
            Vector128<uint> s1 = AddModuloSse(c, dr, mod);
            Vector128<uint> q1 = SubtractModuloSse(c, dr, mod);
            Vector128<uint> m0 = MultiplyResiduesSse(s1, t20, modulus);
            Vector128<uint> m1 = MultiplyResiduesSse(q1, t21, modulus);
            Vector128<uint> o0 = AddModuloSse(s0, m0, mod);
            Vector128<uint> o1 = AddModuloSse(q0, m1, mod);
            Vector128<uint> o2 = SubtractModuloSse(s0, m0, mod);
            Vector128<uint> o3 = SubtractModuloSse(q0, m1, mod);

            if (normalizeFinal)
            {
                o0 = MultiplyShoupSse(o0, inv, invShoup, mod);
                o1 = MultiplyShoupSse(o1, inv, invShoup, mod);
                o2 = MultiplyShoupSse(o2, inv, invShoup, mod);
                o3 = MultiplyShoupSse(o3, inv, invShoup, mod);
                StoreFinal128(o0, output, index0, validOutputLength);
                StoreFinal128(o1, output, index1, validOutputLength);
                StoreFinal128(o2, output, index2, validOutputLength);
                StoreFinal128(o3, output, index3, validOutputLength);
            }
            else
            {
                o0.StoreUnsafe(ref data, (nuint)index0);
                o1.StoreUnsafe(ref data, (nuint)index1);
                o2.StoreUnsafe(ref data, (nuint)index2);
                o3.StoreUnsafe(ref data, (nuint)index3);
            }

            if (i + Width < last)
            {
                t1 = MultiplyShoupSse(t1, advance1, advanceShoup1, mod);
                t20 = MultiplyShoupSse(t20, advance2, advanceShoup2, mod);
                t21 = MultiplyShoupSse(t21, advance2, advanceShoup2, mod);
            }
            if (((i - first) & 0x3FFF) == 0x3FFC)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }

#if ANDROID
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ProcessInverseUncachedStagePairNeon(
        uint[] values, uint[] output, int validOutputLength, uint modulus,
        uint firstRoot, uint secondRoot, uint secondPhase,
        uint inverseLength, uint inverseLengthShoup,
        int stageLength, int parentIndex, int first, int last,
        bool normalizeFinal, CancellationToken cancellationToken)
    {
        const int Width = 4;
        int halfLength = stageLength >> 1;
        int parentOffset = parentIndex * (stageLength << 1);
        Vector128<uint> mod = Vector128.Create(modulus);
        Vector128<uint> t1 = CreateTwiddleSequenceNeon(firstRoot, first, modulus);
        Vector128<uint> t20 = CreateTwiddleSequenceNeon(secondRoot, first, modulus);
        Vector128<uint> t21 = MultiplyResiduesNeon(t20, Vector128.Create(secondPhase), modulus);
        // The stride factors are invariant for this segment. Build their
        // Shoup companions once instead of reducing three general products
        // on every four-butterfly iteration.
        uint step1 = (uint)ModPow(firstRoot, Width, modulus);
        uint step2 = (uint)ModPow(secondRoot, Width, modulus);
        Vector128<uint> advance1 = Vector128.Create(step1);
        Vector128<uint> advance2 = Vector128.Create(step2);
        Vector128<uint> advanceShoup1 = Vector128.Create((uint)(((ulong)step1 << 32) / modulus));
        Vector128<uint> advanceShoup2 = Vector128.Create((uint)(((ulong)step2 << 32) / modulus));
        Vector128<uint> inv = Vector128.Create(inverseLength);
        Vector128<uint> invShoup = Vector128.Create(inverseLengthShoup);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);

        for (int i = first; i < last; i += Width)
        {
            int index0 = parentOffset + i;
            int index1 = index0 + halfLength;
            int index2 = index0 + stageLength;
            int index3 = index2 + halfLength;
            Vector128<uint> a = Vector128.LoadUnsafe(ref data, (nuint)index0);
            Vector128<uint> b = Vector128.LoadUnsafe(ref data, (nuint)index1);
            Vector128<uint> c = Vector128.LoadUnsafe(ref data, (nuint)index2);
            Vector128<uint> d = Vector128.LoadUnsafe(ref data, (nuint)index3);
            Vector128<uint> br = MultiplyResiduesNeon(b, t1, modulus);
            Vector128<uint> dr = MultiplyResiduesNeon(d, t1, modulus);
            Vector128<uint> s0 = AddModuloNeon(a, br, mod);
            Vector128<uint> q0 = SubtractModuloNeon(a, br, mod);
            Vector128<uint> s1 = AddModuloNeon(c, dr, mod);
            Vector128<uint> q1 = SubtractModuloNeon(c, dr, mod);
            Vector128<uint> m0 = MultiplyResiduesNeon(s1, t20, modulus);
            Vector128<uint> m1 = MultiplyResiduesNeon(q1, t21, modulus);
            Vector128<uint> o0 = AddModuloNeon(s0, m0, mod);
            Vector128<uint> o1 = AddModuloNeon(q0, m1, mod);
            Vector128<uint> o2 = SubtractModuloNeon(s0, m0, mod);
            Vector128<uint> o3 = SubtractModuloNeon(q0, m1, mod);

            if (normalizeFinal)
            {
                o0 = MultiplyShoupNeon(o0, inv, invShoup, mod);
                o1 = MultiplyShoupNeon(o1, inv, invShoup, mod);
                o2 = MultiplyShoupNeon(o2, inv, invShoup, mod);
                o3 = MultiplyShoupNeon(o3, inv, invShoup, mod);
                StoreFinal128(o0, output, index0, validOutputLength);
                StoreFinal128(o1, output, index1, validOutputLength);
                StoreFinal128(o2, output, index2, validOutputLength);
                StoreFinal128(o3, output, index3, validOutputLength);
            }
            else
            {
                o0.StoreUnsafe(ref data, (nuint)index0);
                o1.StoreUnsafe(ref data, (nuint)index1);
                o2.StoreUnsafe(ref data, (nuint)index2);
                o3.StoreUnsafe(ref data, (nuint)index3);
            }

            if (i + Width < last)
            {
                t1 = MultiplyShoupNeon(t1, advance1, advanceShoup1, mod);
                t20 = MultiplyShoupNeon(t20, advance2, advanceShoup2, mod);
                t21 = MultiplyShoupNeon(t21, advance2, advanceShoup2, mod);
            }
            if (((i - first) & 0x3FFF) == 0x3FFC)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanUseInverseStagePairSimd(FixedWorkerTeam workers, int halfLength)
    {
        int width =
            ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512InverseGlobal) && Avx512F.IsSupported)
                ? Vector512<uint>.Count
                : (workers.UseAvx2Ntt && Avx2.IsSupported)
                    ? Vector256<uint>.Count
                    : (workers.UseSseNtt && Sse2.IsSupported)
                        ? Vector128<uint>.Count
#if ANDROID
                        : (workers.UseNeonNtt && (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated))
                            ? Vector128<uint>.Count
#endif
                            : 1;
        return width > 1 && halfLength >= width && (halfLength % width) == 0;
    }
    #endregion

    #region Pointwise and final inverse
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
    #endregion
}
