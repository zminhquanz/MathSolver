using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace MathSolver.Services.Core;

public static partial class MatrixEngine
{
    // Mono ARM64 exposes accelerated generic Vector128 without the AdvSimd API.
    internal static int ElementwiseNeon(long[] a, long[] b, Int128[] output, int start, int end, bool subtract)
    {
        ref ulong destination = ref Unsafe.As<Int128, ulong>(ref MemoryMarshal.GetArrayDataReference(output));
        int i = start;
        for (; i <= end - 2; i += 2)
        {
            var left = Vector128.LoadUnsafe(ref a[i]).AsUInt64();
            var right = Vector128.LoadUnsafe(ref b[i]).AsUInt64();
            var low = subtract ? left - right : left + right;
            var signA = left >> 63;
            var signB = right >> 63;
            var carry = (subtract ? (~left & right) | (~(left ^ right) & low)
                : (left & right) | ((left | right) & ~low)) >> 63;
            var high = subtract ? signB - signA - carry : carry - signA - signB;
            var first = AdvSimd.Arm64.IsSupported ? AdvSimd.Arm64.ZipLow(low, high)
                : Vector128.Create(low.GetLower(), high.GetLower());
            var second = AdvSimd.Arm64.IsSupported ? AdvSimd.Arm64.ZipHigh(low, high)
                : Vector128.Create(low.GetUpper(), high.GetUpper());
            first.StoreUnsafe(ref destination, (nuint)(2 * i));
            second.StoreUnsafe(ref destination, (nuint)(2 * i + 2));
        }
        return i;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> MultiplyLowWordsNeon(Vector128<ulong> left, Vector128<ulong> right)
    {
        if (AdvSimd.Arm64.IsSupported)
            return AdvSimd.MultiplyWideningLower(AdvSimd.ExtractNarrowingLower(left), AdvSimd.ExtractNarrowingLower(right));
        // Two exact scalar 32x32->64 products; limb accumulation stays vectorized.
        // Faster than emulating widening multiplication with 16-bit products on Mono.
        return Vector128.Create((ulong)(uint)left.GetElement(0) * (uint)right.GetElement(0),
            (ulong)(uint)left.GetElement(1) * (uint)right.GetElement(1));
    }

    internal static int DotNeon(long[] a, int aStart, long[] b, int bStart, int length, ref Signed192 sum)
    {
        var mask = Vector128.Create((ulong)uint.MaxValue);
        var d0 = Vector128<ulong>.Zero;
        var d1 = d0; var d2 = d0; var d3 = d0; var d4 = d0;
        int i = 0;
        for (; i <= length - 2; i += 2)
        {
            var left = Vector128.LoadUnsafe(ref a[aStart + i]).AsUInt64();
            var right = Vector128.LoadUnsafe(ref b[bStart + i]).AsUInt64();
            var leftHigh = left >> 32;
            var rightHigh = right >> 32;
            var p00 = MultiplyLowWordsNeon(left, right);
            var p01 = MultiplyLowWordsNeon(left, rightHigh);
            var p10 = MultiplyLowWordsNeon(leftHigh, right);
            var p11 = MultiplyLowWordsNeon(leftHigh, rightHigh);
            var signA = Vector128<ulong>.Zero - (left >> 63);
            var signB = Vector128<ulong>.Zero - (right >> 63);
            d0 += p00 & mask;
            d1 += (p00 >> 32) + (p01 & mask) + (p10 & mask);
            d2 += (p01 >> 32) + (p10 >> 32) + (p11 & mask)
                - (right & mask & signA) - (left & mask & signB);
            d3 += (p11 >> 32) - (rightHigh & signA) - (leftHigh & signB);
            d4 += (left & right) >> 63;
        }
        sum = Signed192.FromLimbs(Vector128.Sum(d0.AsInt64()), Vector128.Sum(d1.AsInt64()),
            Vector128.Sum(d2.AsInt64()), Vector128.Sum(d3.AsInt64()), Vector128.Sum(d4.AsInt64()));
        return i;
    }

    // The caller must prove sum(abs(a[k]*b[k])) <= Int64.MaxValue for the
    // entire packed panel. Vector lanes and the horizontal sum then fit.
    internal static int DotNeonBounded(long[] a, int aStart, long[] b, int bStart, int length, ref Signed192 sum)
    {
        var accumulated = Vector128<long>.Zero;
        int i = 0;
        for (; i <= length - 2; i += 2)
            accumulated += Vector128.LoadUnsafe(ref a[aStart + i]) * Vector128.LoadUnsafe(ref b[bStart + i]);
        sum = default;
        sum.Add(Vector128.Sum(accumulated));
        return i;
    }

    internal static bool CanAccumulateNeonInInt64(long[] a, int aCount, long[] b, int bCount, int depth)
    {
        ulong maxA = 0, maxB = 0;
        for (int i = 0; i < aCount; i++) maxA = Math.Max(maxA, Magnitude(a[i]));
        for (int i = 0; i < bCount; i++) maxB = Math.Max(maxB, Magnitude(b[i]));
        return (UInt128)maxA * maxB <= (ulong)long.MaxValue / (uint)depth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Magnitude(long value) => value < 0 ? 0UL - (ulong)value : (ulong)value;

    private static int DotNeonPair(long[] a, int aStart, long[] b, int b0, int b1, int length,
        bool bounded, ref Signed192 sum0, ref Signed192 sum1)
    {
        if (bounded)
        {
            DotNeonBounded(a, aStart, b, b0, length, ref sum0);
            return DotNeonBounded(a, aStart, b, b1, length, ref sum1);
        }
        DotNeon(a, aStart, b, b0, length, ref sum0);
        return DotNeon(a, aStart, b, b1, length, ref sum1);
    }
}
