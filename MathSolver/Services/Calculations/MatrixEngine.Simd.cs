using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Services.Core;

public static partial class MatrixEngine
{
    // Store complete low/high pairs directly into little-endian Int128 storage.
    // No per-lane extraction, input reload or scalar carry reconstruction.
    private static int Elementwise128(long[] a, long[] b, Int128[] output, int start, int end, bool subtract)
    {
        ref ulong destination = ref Unsafe.As<Int128, ulong>(ref MemoryMarshal.GetArrayDataReference(output));
        int i = start;
        for (; i <= end - 2; i += 2)
        {
            var left = Vector128.LoadUnsafe(ref a[i]).AsUInt64();
            var right = Vector128.LoadUnsafe(ref b[i]).AsUInt64();
            var low = subtract ? left - right : left + right;
            var signA = Sse2.ShiftRightLogical(left, 63);
            var signB = Sse2.ShiftRightLogical(right, 63);
            // Carry/borrow identities also work with SSE2 (no unsigned Int64 compare).
            var carry = Sse2.ShiftRightLogical(subtract
                ? (~left & right) | (~(left ^ right) & low)
                : (left & right) | ((left | right) & ~low), 63);
            var high = subtract ? signB - signA - carry : carry - signA - signB;
            Sse2.UnpackLow(low, high).StoreUnsafe(ref destination, (nuint)(2 * i));
            Sse2.UnpackHigh(low, high).StoreUnsafe(ref destination, (nuint)(2 * i + 2));
        }
        return i;
    }

    private static int Elementwise256(long[] a, long[] b, Int128[] output, int start, int end, bool subtract)
    {
        ref ulong destination = ref Unsafe.As<Int128, ulong>(ref MemoryMarshal.GetArrayDataReference(output));
        int i = start;
        for (; i <= end - 4; i += 4)
        {
            var left = Vector256.LoadUnsafe(ref a[i]).AsUInt64();
            var right = Vector256.LoadUnsafe(ref b[i]).AsUInt64();
            var low = subtract ? left - right : left + right;
            var signA = Avx2.ShiftRightLogical(left, 63);
            var signB = Avx2.ShiftRightLogical(right, 63);
            // Carry/borrow identities also work with SSE2 (no unsigned Int64 compare).
            var carry = Avx2.ShiftRightLogical(subtract
                ? (~left & right) | (~(left ^ right) & low)
                : (left & right) | ((left | right) & ~low), 63);
            var high = subtract ? signB - signA - carry : carry - signA - signB;
            var even = Avx2.UnpackLow(low, high);
            var odd = Avx2.UnpackHigh(low, high);
            Avx2.Permute2x128(even, odd, 0x20).StoreUnsafe(ref destination, (nuint)(2 * i));
            Avx2.Permute2x128(even, odd, 0x31).StoreUnsafe(ref destination, (nuint)(2 * i + 4));
        }
        return i;
    }

    private static int Elementwise512(long[] a, long[] b, Int128[] output, int start, int end, bool subtract)
    {
        ref ulong destination = ref Unsafe.As<Int128, ulong>(ref MemoryMarshal.GetArrayDataReference(output));
        var firstPairs = Vector512.Create(0UL, 8UL, 1UL, 9UL, 2UL, 10UL, 3UL, 11UL);
        var lastPairs = Vector512.Create(4UL, 12UL, 5UL, 13UL, 6UL, 14UL, 7UL, 15UL);
        int i = start;
        for (; i <= end - 8; i += 8)
        {
            var left = Vector512.LoadUnsafe(ref a[i]).AsUInt64();
            var right = Vector512.LoadUnsafe(ref b[i]).AsUInt64();
            var low = subtract ? left - right : left + right;
            var signA = Avx512F.ShiftRightArithmetic(left.AsInt64(), 63).AsUInt64();
            var signB = Avx512F.ShiftRightArithmetic(right.AsInt64(), 63).AsUInt64();
            var high = subtract
                ? signA - signB + Avx512F.CompareLessThan(left, right)
                : signA + signB - Avx512F.CompareLessThan(low, left);
            Avx512F.PermuteVar8x64x2(low, firstPairs, high).StoreUnsafe(ref destination, (nuint)(2 * i));
            Avx512F.PermuteVar8x64x2(low, lastPairs, high).StoreUnsafe(ref destination, (nuint)(2 * i + 8));
        }
        return i;
    }

    // Accumulate base-2^32 limbs in registers; normalize once per dot product.
    // Each limb is bounded by a few multiples of 16384 * 2^32, below signed Int64.
    // Signed correction: ab = unsigned(a)unsigned(b)
    //   - sign(a)unsigned(b)2^64 - sign(b)unsigned(a)2^64
    //   + sign(a)sign(b)2^128.
    private static int Dot128(long[] a, int aStart, long[] b, int bStart, int length, ref Signed192 sum)
    {
        var mask = Vector128.Create((ulong)uint.MaxValue);
        var d0 = Vector128<ulong>.Zero;
        var d1 = d0; var d2 = d0; var d3 = d0; var d4 = d0;
        int i = 0;
        for (; i <= length - 2; i += 2)
        {
            var left = Vector128.LoadUnsafe(ref a[aStart + i]).AsUInt64();
            var right = Vector128.LoadUnsafe(ref b[bStart + i]).AsUInt64();
            var leftHigh = Sse2.ShiftRightLogical(left, 32);
            var rightHigh = Sse2.ShiftRightLogical(right, 32);
            var p00 = Sse2.Multiply(left.AsUInt32(), right.AsUInt32());
            var p01 = Sse2.Multiply(left.AsUInt32(), rightHigh.AsUInt32());
            var p10 = Sse2.Multiply(leftHigh.AsUInt32(), right.AsUInt32());
            var p11 = Sse2.Multiply(leftHigh.AsUInt32(), rightHigh.AsUInt32());
            var signA = Vector128<ulong>.Zero - Sse2.ShiftRightLogical(left, 63);
            var signB = Vector128<ulong>.Zero - Sse2.ShiftRightLogical(right, 63);
            d0 += p00 & mask;
            d1 += Sse2.ShiftRightLogical(p00, 32) + (p01 & mask) + (p10 & mask);
            d2 += Sse2.ShiftRightLogical(p01, 32) + Sse2.ShiftRightLogical(p10, 32) + (p11 & mask)
                - (right & mask & signA) - (left & mask & signB);
            d3 += Sse2.ShiftRightLogical(p11, 32) - (rightHigh & signA) - (leftHigh & signB);
            d4 += Sse2.ShiftRightLogical(left & right, 63);
        }
        sum = Signed192.FromLimbs(Vector128.Sum(d0.AsInt64()), Vector128.Sum(d1.AsInt64()),
            Vector128.Sum(d2.AsInt64()), Vector128.Sum(d3.AsInt64()), Vector128.Sum(d4.AsInt64()));
        return i;
    }

    private static int Dot256(long[] a, int aStart, long[] b, int bStart, int length, ref Signed192 sum)
    {
        var mask = Vector256.Create((ulong)uint.MaxValue);
        var d0 = Vector256<ulong>.Zero;
        var d1 = d0; var d2 = d0; var d3 = d0; var d4 = d0;
        int i = 0;
        for (; i <= length - 4; i += 4)
        {
            var left = Vector256.LoadUnsafe(ref a[aStart + i]).AsUInt64();
            var right = Vector256.LoadUnsafe(ref b[bStart + i]).AsUInt64();
            var leftHigh = Avx2.ShiftRightLogical(left, 32);
            var rightHigh = Avx2.ShiftRightLogical(right, 32);
            var p00 = Avx2.Multiply(left.AsUInt32(), right.AsUInt32());
            var p01 = Avx2.Multiply(left.AsUInt32(), rightHigh.AsUInt32());
            var p10 = Avx2.Multiply(leftHigh.AsUInt32(), right.AsUInt32());
            var p11 = Avx2.Multiply(leftHigh.AsUInt32(), rightHigh.AsUInt32());
            var signA = Vector256<ulong>.Zero - Avx2.ShiftRightLogical(left, 63);
            var signB = Vector256<ulong>.Zero - Avx2.ShiftRightLogical(right, 63);
            d0 += p00 & mask;
            d1 += Avx2.ShiftRightLogical(p00, 32) + (p01 & mask) + (p10 & mask);
            d2 += Avx2.ShiftRightLogical(p01, 32) + Avx2.ShiftRightLogical(p10, 32) + (p11 & mask)
                - (right & mask & signA) - (left & mask & signB);
            d3 += Avx2.ShiftRightLogical(p11, 32) - (rightHigh & signA) - (leftHigh & signB);
            d4 += Avx2.ShiftRightLogical(left & right, 63);
        }
        sum = Signed192.FromLimbs(Vector256.Sum(d0.AsInt64()), Vector256.Sum(d1.AsInt64()),
            Vector256.Sum(d2.AsInt64()), Vector256.Sum(d3.AsInt64()), Vector256.Sum(d4.AsInt64()));
        return i;
    }

    private static int Dot512(long[] a, int aStart, long[] b, int bStart, int length, ref Signed192 sum)
    {
        var mask = Vector512.Create((ulong)uint.MaxValue);
        var d0 = Vector512<ulong>.Zero;
        var d1 = d0; var d2 = d0; var d3 = d0; var d4 = d0;
        int i = 0;
        for (; i <= length - 8; i += 8)
        {
            var left = Vector512.LoadUnsafe(ref a[aStart + i]).AsUInt64();
            var right = Vector512.LoadUnsafe(ref b[bStart + i]).AsUInt64();
            var leftHigh = Avx512F.ShiftRightLogical(left, 32);
            var rightHigh = Avx512F.ShiftRightLogical(right, 32);
            var p00 = Avx512F.Multiply(left.AsUInt32(), right.AsUInt32());
            var p01 = Avx512F.Multiply(left.AsUInt32(), rightHigh.AsUInt32());
            var p10 = Avx512F.Multiply(leftHigh.AsUInt32(), right.AsUInt32());
            var p11 = Avx512F.Multiply(leftHigh.AsUInt32(), rightHigh.AsUInt32());
            var signA = Vector512<ulong>.Zero - Avx512F.ShiftRightLogical(left, 63);
            var signB = Vector512<ulong>.Zero - Avx512F.ShiftRightLogical(right, 63);
            d0 += p00 & mask;
            d1 += Avx512F.ShiftRightLogical(p00, 32) + (p01 & mask) + (p10 & mask);
            d2 += Avx512F.ShiftRightLogical(p01, 32) + Avx512F.ShiftRightLogical(p10, 32) + (p11 & mask)
                - (right & mask & signA) - (left & mask & signB);
            d3 += Avx512F.ShiftRightLogical(p11, 32) - (rightHigh & signA) - (leftHigh & signB);
            d4 += Avx512F.ShiftRightLogical(left & right, 63);
        }
        sum = Signed192.FromLimbs(Vector512.Sum(d0.AsInt64()), Vector512.Sum(d1.AsInt64()),
            Vector512.Sum(d2.AsInt64()), Vector512.Sum(d3.AsInt64()), Vector512.Sum(d4.AsInt64()));
        return i;
    }

}

