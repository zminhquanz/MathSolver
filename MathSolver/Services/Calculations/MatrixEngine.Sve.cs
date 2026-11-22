#if ANDROID && NET11_0_OR_GREATER
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;

namespace MathSolver.Services.Core;

// SVE and SVE2 intrinsics are experimental in .NET 11.
#pragma warning disable SYSLIB5003
public static partial class MatrixEngine
{
    private static int ElementwiseSve(long[] a, long[] b, Int128[] output, int start, int end, bool subtract)
    {
        int width = Vector<ulong>.Count;
        var zero = Vector<ulong>.Zero;
        var one = Vector.Create(1UL);
        ref ulong destination = ref Unsafe.As<Int128, ulong>(ref MemoryMarshal.GetArrayDataReference(output));
        int i = start;
        for (; i <= end - width; i += width)
        {
            var left = Vector.AsVectorUInt64(Vector.LoadUnsafe(ref a[i]));
            var right = Vector.AsVectorUInt64(Vector.LoadUnsafe(ref b[i]));
            var signA = left >> 63;
            var signB = right >> 63;
            Vector<ulong> low;
            Vector<ulong> high;
            if (subtract)
            {
                low = Sve.Subtract(left, right);
                var borrow = Sve.ConditionalSelect(Sve.CompareLessThan(left, right), one, zero);
                high = signB - signA - borrow;
            }
            else
            {
                low = Sve.Add(left, right);
                var carry = Sve.ConditionalSelect(Sve.CompareLessThan(low, left), one, zero);
                high = carry - signA - signB;
            }

            // Android ARM64 stores each Int128 as its low word followed by its high word.
            nuint offset = (nuint)i * 2;
            Sve.ZipLow(low, high).StoreUnsafe(ref destination, offset);
            Sve.ZipHigh(low, high).StoreUnsafe(ref destination, offset + (nuint)width);
        }
        return i;
    }

    // Bounded requires sum(abs(a[k] * b[k])) <= Int64.MaxValue for the entire dot.
    // fitsInt32 certifies that every operand fits signed Int32. The caller handles the tail.
    private static int DotSve(long[] a, int aStart, long[] b, int bStart, int length,
        bool bounded, bool useSve2, bool fitsInt32, ref Signed192 sum)
    {
        if (bounded)
            return DotSveBounded(a, aStart, b, bStart, length, useSve2 && fitsInt32, ref sum);

        int width = Vector<ulong>.Count;
        var mask = Vector.Create((ulong)uint.MaxValue);
        var d0 = Vector<ulong>.Zero;
        var d1 = d0; var d2 = d0; var d3 = d0; var d4 = d0;
        int i = 0;
        for (; i <= length - width; i += width)
        {
            var left = Vector.AsVectorUInt64(Vector.LoadUnsafe(ref a[aStart + i]));
            var right = Vector.AsVectorUInt64(Vector.LoadUnsafe(ref b[bStart + i]));
            AccumulateSveLimbs(left & mask, left >> 32, Vector<ulong>.Zero - (left >> 63), right,
                mask, useSve2, ref d0, ref d1, ref d2, ref d3, ref d4);
        }
        sum = ReduceSveLimbs(d0, d1, d2, d3, d4);
        return i;
    }

    private static int DotSvePair(long[] a, int aStart, long[] b, int b0, int b1, int length,
        bool bounded, bool useSve2, bool fitsInt32, ref Signed192 sum0, ref Signed192 sum1)
    {
        if (bounded)
            return DotSvePairBounded(a, aStart, b, b0, b1, length, useSve2 && fitsInt32, ref sum0, ref sum1);

        int width = Vector<ulong>.Count;
        var mask = Vector.Create((ulong)uint.MaxValue);
        var d00 = Vector<ulong>.Zero;
        var d01 = d00; var d02 = d00; var d03 = d00; var d04 = d00;
        var d10 = d00; var d11 = d00; var d12 = d00; var d13 = d00; var d14 = d00;
        int i = 0;
        for (; i <= length - width; i += width)
        {
            var left = Vector.AsVectorUInt64(Vector.LoadUnsafe(ref a[aStart + i]));
            var leftLow = left & mask;
            var leftHigh = left >> 32;
            var signA = Vector<ulong>.Zero - (left >> 63);
            AccumulateSveLimbs(leftLow, leftHigh, signA,
                Vector.AsVectorUInt64(Vector.LoadUnsafe(ref b[b0 + i])),
                mask, useSve2, ref d00, ref d01, ref d02, ref d03, ref d04);
            AccumulateSveLimbs(leftLow, leftHigh, signA,
                Vector.AsVectorUInt64(Vector.LoadUnsafe(ref b[b1 + i])),
                mask, useSve2, ref d10, ref d11, ref d12, ref d13, ref d14);
        }
        sum0 = ReduceSveLimbs(d00, d01, d02, d03, d04);
        sum1 = ReduceSveLimbs(d10, d11, d12, d13, d14);
        return i;
    }

    private static int DotSveBounded(long[] a, int aStart, long[] b, int bStart, int length,
        bool widen, ref Signed192 sum)
    {
        int width = Vector<long>.Count;
        var acc0 = Vector<long>.Zero;
        var acc1 = Vector<long>.Zero;
        int i = 0;
        if (widen)
        {
            for (; i <= length - 2 * width; i += 2 * width)
            {
                acc0 = MultiplyAddInt32Sve2(acc0, Vector.LoadUnsafe(ref a[aStart + i]),
                    Vector.LoadUnsafe(ref b[bStart + i]));
                acc1 = MultiplyAddInt32Sve2(acc1, Vector.LoadUnsafe(ref a[aStart + i + width]),
                    Vector.LoadUnsafe(ref b[bStart + i + width]));
            }
            if (i <= length - width)
            {
                acc0 = MultiplyAddInt32Sve2(acc0, Vector.LoadUnsafe(ref a[aStart + i]),
                    Vector.LoadUnsafe(ref b[bStart + i]));
                i += width;
            }
        }
        else
        {
            for (; i <= length - 2 * width; i += 2 * width)
            {
                acc0 = Sve.MultiplyAdd(acc0, Vector.LoadUnsafe(ref a[aStart + i]),
                    Vector.LoadUnsafe(ref b[bStart + i]));
                acc1 = Sve.MultiplyAdd(acc1, Vector.LoadUnsafe(ref a[aStart + i + width]),
                    Vector.LoadUnsafe(ref b[bStart + i + width]));
            }
            if (i <= length - width)
            {
                acc0 = Sve.MultiplyAdd(acc0, Vector.LoadUnsafe(ref a[aStart + i]),
                    Vector.LoadUnsafe(ref b[bStart + i]));
                i += width;
            }
        }
        sum = default;
        sum.Add(Vector.Sum(acc0 + acc1));
        return i;
    }

    private static int DotSvePairBounded(long[] a, int aStart, long[] b, int b0, int b1, int length,
        bool widen, ref Signed192 sum0, ref Signed192 sum1)
    {
        int width = Vector<long>.Count;
        var acc0 = Vector<long>.Zero;
        var acc1 = Vector<long>.Zero;
        int i = 0;
        if (widen)
        {
            for (; i <= length - width; i += width)
            {
                var left = Vector.LoadUnsafe(ref a[aStart + i]);
                acc0 = MultiplyAddInt32Sve2(acc0, left, Vector.LoadUnsafe(ref b[b0 + i]));
                acc1 = MultiplyAddInt32Sve2(acc1, left, Vector.LoadUnsafe(ref b[b1 + i]));
            }
        }
        else
        {
            for (; i <= length - width; i += width)
            {
                var left = Vector.LoadUnsafe(ref a[aStart + i]);
                acc0 = Sve.MultiplyAdd(acc0, left, Vector.LoadUnsafe(ref b[b0 + i]));
                acc1 = Sve.MultiplyAdd(acc1, left, Vector.LoadUnsafe(ref b[b1 + i]));
            }
        }
        sum0 = default;
        sum1 = default;
        sum0.Add(Vector.Sum(acc0));
        sum1.Add(Vector.Sum(acc1));
        return i;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<long> MultiplyAddInt32Sve2(Vector<long> acc, Vector<long> left, Vector<long> right) =>
        // The signed low word of each Int64 occupies an even Int32 lane.
        Sve2.MultiplyWideningEvenAndAdd(acc, Vector.AsVectorInt32(left), Vector.AsVectorInt32(right));

    // The same signed base-2^32 decomposition as DotNeon. Each product contributes
    // at most three UInt32 words to a limb, so matrix-depth lane sums fit in Int64.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateSveLimbs(Vector<ulong> leftLow, Vector<ulong> leftHigh, Vector<ulong> signA,
        Vector<ulong> right, Vector<ulong> mask, bool useSve2, ref Vector<ulong> d0, ref Vector<ulong> d1,
        ref Vector<ulong> d2, ref Vector<ulong> d3, ref Vector<ulong> d4)
    {
        var rightLow = right & mask;
        var rightHigh = right >> 32;
        var signB = Vector<ulong>.Zero - (right >> 63);
        Vector<ulong> p00, p01, p10, p11;
        if (useSve2)
        {
            var a0 = Vector.AsVectorUInt32(leftLow);
            var a1 = Vector.AsVectorUInt32(leftHigh);
            var b0 = Vector.AsVectorUInt32(rightLow);
            var b1 = Vector.AsVectorUInt32(rightHigh);
            p00 = Sve2.MultiplyWideningEven(a0, b0);
            p01 = Sve2.MultiplyWideningEven(a0, b1);
            p10 = Sve2.MultiplyWideningEven(a1, b0);
            p11 = Sve2.MultiplyWideningEven(a1, b1);
        }
        else
        {
            p00 = Sve.Multiply(leftLow, rightLow);
            p01 = Sve.Multiply(leftLow, rightHigh);
            p10 = Sve.Multiply(leftHigh, rightLow);
            p11 = Sve.Multiply(leftHigh, rightHigh);
        }
        d0 += p00 & mask;
        d1 += (p00 >> 32) + (p01 & mask) + (p10 & mask);
        d2 += (p01 >> 32) + (p10 >> 32) + (p11 & mask) - (rightLow & signA) - (leftLow & signB);
        d3 += (p11 >> 32) - (rightHigh & signA) - (leftHigh & signB);
        d4 += (signA & signB) & Vector.Create(1UL);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Signed192 ReduceSveLimbs(Vector<ulong> d0, Vector<ulong> d1, Vector<ulong> d2,
        Vector<ulong> d3, Vector<ulong> d4) =>
        Signed192.FromLimbs(Vector.Sum(Vector.AsVectorInt64(d0)), Vector.Sum(Vector.AsVectorInt64(d1)),
            Vector.Sum(Vector.AsVectorInt64(d2)), Vector.Sum(Vector.AsVectorInt64(d3)),
            Vector.Sum(Vector.AsVectorInt64(d4)));
}
#pragma warning restore SYSLIB5003
#endif
