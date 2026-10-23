using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Services.Core;

public static partial class MatrixEngine
{
    // Two independent exact base-2^32 reductions share each A load, high half,
    // and sign mask. Int64 limb sums stay bounded for depth <= 16384. As with
    // the single-output kernels, the caller owns the incomplete vector tail.
    internal static int DotPair128(long[] a, int aStart, long[] b, int bStart0, int bStart1,
        int length, ref Signed192 sum0, ref Signed192 sum1)
    {
        var mask = Vector128.Create((ulong)uint.MaxValue);
        var d00 = Vector128<ulong>.Zero;
        var d01 = d00; var d02 = d00; var d03 = d00; var d04 = d00;
        var d10 = d00; var d11 = d00; var d12 = d00; var d13 = d00; var d14 = d00;
        int i = 0;
        for (; i <= length - 2; i += 2)
        {
            var left = Vector128.LoadUnsafe(ref a[aStart + i]).AsUInt64();
            var leftHigh = Sse2.ShiftRightLogical(left, 32);
            var signA = Vector128<ulong>.Zero - Sse2.ShiftRightLogical(left, 63);
            {
                var right = Vector128.LoadUnsafe(ref b[bStart0 + i]).AsUInt64();
                var rightHigh = Sse2.ShiftRightLogical(right, 32);
                var p00 = Sse2.Multiply(left.AsUInt32(), right.AsUInt32());
                var p01 = Sse2.Multiply(left.AsUInt32(), rightHigh.AsUInt32());
                var p10 = Sse2.Multiply(leftHigh.AsUInt32(), right.AsUInt32());
                var p11 = Sse2.Multiply(leftHigh.AsUInt32(), rightHigh.AsUInt32());
                var signB = Vector128<ulong>.Zero - Sse2.ShiftRightLogical(right, 63);
                d00 += p00 & mask;
                d01 += Sse2.ShiftRightLogical(p00, 32) + (p01 & mask) + (p10 & mask);
                d02 += Sse2.ShiftRightLogical(p01, 32) + Sse2.ShiftRightLogical(p10, 32) + (p11 & mask)
                    - (right & mask & signA) - (left & mask & signB);
                d03 += Sse2.ShiftRightLogical(p11, 32) - (rightHigh & signA) - (leftHigh & signB);
                d04 += Sse2.ShiftRightLogical(left & right, 63);
            }
            {
                var right = Vector128.LoadUnsafe(ref b[bStart1 + i]).AsUInt64();
                var rightHigh = Sse2.ShiftRightLogical(right, 32);
                var p00 = Sse2.Multiply(left.AsUInt32(), right.AsUInt32());
                var p01 = Sse2.Multiply(left.AsUInt32(), rightHigh.AsUInt32());
                var p10 = Sse2.Multiply(leftHigh.AsUInt32(), right.AsUInt32());
                var p11 = Sse2.Multiply(leftHigh.AsUInt32(), rightHigh.AsUInt32());
                var signB = Vector128<ulong>.Zero - Sse2.ShiftRightLogical(right, 63);
                d10 += p00 & mask;
                d11 += Sse2.ShiftRightLogical(p00, 32) + (p01 & mask) + (p10 & mask);
                d12 += Sse2.ShiftRightLogical(p01, 32) + Sse2.ShiftRightLogical(p10, 32) + (p11 & mask)
                    - (right & mask & signA) - (left & mask & signB);
                d13 += Sse2.ShiftRightLogical(p11, 32) - (rightHigh & signA) - (leftHigh & signB);
                d14 += Sse2.ShiftRightLogical(left & right, 63);
            }
        }
        sum0 = Signed192.FromLimbs(Vector128.Sum(d00.AsInt64()), Vector128.Sum(d01.AsInt64()),
            Vector128.Sum(d02.AsInt64()), Vector128.Sum(d03.AsInt64()), Vector128.Sum(d04.AsInt64()));
        sum1 = Signed192.FromLimbs(Vector128.Sum(d10.AsInt64()), Vector128.Sum(d11.AsInt64()),
            Vector128.Sum(d12.AsInt64()), Vector128.Sum(d13.AsInt64()), Vector128.Sum(d14.AsInt64()));
        return i;
    }

    internal static int DotPair256(long[] a, int aStart, long[] b, int bStart0, int bStart1,
        int length, ref Signed192 sum0, ref Signed192 sum1)
    {
        var mask = Vector256.Create((ulong)uint.MaxValue);
        var d00 = Vector256<ulong>.Zero;
        var d01 = d00; var d02 = d00; var d03 = d00; var d04 = d00;
        var d10 = d00; var d11 = d00; var d12 = d00; var d13 = d00; var d14 = d00;
        int i = 0;
        for (; i <= length - 4; i += 4)
        {
            var left = Vector256.LoadUnsafe(ref a[aStart + i]).AsUInt64();
            var leftHigh = Avx2.ShiftRightLogical(left, 32);
            var signA = Vector256<ulong>.Zero - Avx2.ShiftRightLogical(left, 63);
            {
                var right = Vector256.LoadUnsafe(ref b[bStart0 + i]).AsUInt64();
                var rightHigh = Avx2.ShiftRightLogical(right, 32);
                var p00 = Avx2.Multiply(left.AsUInt32(), right.AsUInt32());
                var p01 = Avx2.Multiply(left.AsUInt32(), rightHigh.AsUInt32());
                var p10 = Avx2.Multiply(leftHigh.AsUInt32(), right.AsUInt32());
                var p11 = Avx2.Multiply(leftHigh.AsUInt32(), rightHigh.AsUInt32());
                var signB = Vector256<ulong>.Zero - Avx2.ShiftRightLogical(right, 63);
                d00 += p00 & mask;
                d01 += Avx2.ShiftRightLogical(p00, 32) + (p01 & mask) + (p10 & mask);
                d02 += Avx2.ShiftRightLogical(p01, 32) + Avx2.ShiftRightLogical(p10, 32) + (p11 & mask)
                    - (right & mask & signA) - (left & mask & signB);
                d03 += Avx2.ShiftRightLogical(p11, 32) - (rightHigh & signA) - (leftHigh & signB);
                d04 += Avx2.ShiftRightLogical(left & right, 63);
            }
            {
                var right = Vector256.LoadUnsafe(ref b[bStart1 + i]).AsUInt64();
                var rightHigh = Avx2.ShiftRightLogical(right, 32);
                var p00 = Avx2.Multiply(left.AsUInt32(), right.AsUInt32());
                var p01 = Avx2.Multiply(left.AsUInt32(), rightHigh.AsUInt32());
                var p10 = Avx2.Multiply(leftHigh.AsUInt32(), right.AsUInt32());
                var p11 = Avx2.Multiply(leftHigh.AsUInt32(), rightHigh.AsUInt32());
                var signB = Vector256<ulong>.Zero - Avx2.ShiftRightLogical(right, 63);
                d10 += p00 & mask;
                d11 += Avx2.ShiftRightLogical(p00, 32) + (p01 & mask) + (p10 & mask);
                d12 += Avx2.ShiftRightLogical(p01, 32) + Avx2.ShiftRightLogical(p10, 32) + (p11 & mask)
                    - (right & mask & signA) - (left & mask & signB);
                d13 += Avx2.ShiftRightLogical(p11, 32) - (rightHigh & signA) - (leftHigh & signB);
                d14 += Avx2.ShiftRightLogical(left & right, 63);
            }
        }
        sum0 = Signed192.FromLimbs(Vector256.Sum(d00.AsInt64()), Vector256.Sum(d01.AsInt64()),
            Vector256.Sum(d02.AsInt64()), Vector256.Sum(d03.AsInt64()), Vector256.Sum(d04.AsInt64()));
        sum1 = Signed192.FromLimbs(Vector256.Sum(d10.AsInt64()), Vector256.Sum(d11.AsInt64()),
            Vector256.Sum(d12.AsInt64()), Vector256.Sum(d13.AsInt64()), Vector256.Sum(d14.AsInt64()));
        return i;
    }

    internal static int DotPair512(long[] a, int aStart, long[] b, int bStart0, int bStart1,
        int length, ref Signed192 sum0, ref Signed192 sum1)
    {
        var mask = Vector512.Create((ulong)uint.MaxValue);
        var d00 = Vector512<ulong>.Zero;
        var d01 = d00; var d02 = d00; var d03 = d00; var d04 = d00;
        var d10 = d00; var d11 = d00; var d12 = d00; var d13 = d00; var d14 = d00;
        int i = 0;
        for (; i <= length - 8; i += 8)
        {
            var left = Vector512.LoadUnsafe(ref a[aStart + i]).AsUInt64();
            var leftHigh = Avx512F.ShiftRightLogical(left, 32);
            var signA = Vector512<ulong>.Zero - Avx512F.ShiftRightLogical(left, 63);
            {
                var right = Vector512.LoadUnsafe(ref b[bStart0 + i]).AsUInt64();
                var rightHigh = Avx512F.ShiftRightLogical(right, 32);
                var p00 = Avx512F.Multiply(left.AsUInt32(), right.AsUInt32());
                var p01 = Avx512F.Multiply(left.AsUInt32(), rightHigh.AsUInt32());
                var p10 = Avx512F.Multiply(leftHigh.AsUInt32(), right.AsUInt32());
                var p11 = Avx512F.Multiply(leftHigh.AsUInt32(), rightHigh.AsUInt32());
                var signB = Vector512<ulong>.Zero - Avx512F.ShiftRightLogical(right, 63);
                d00 += p00 & mask;
                d01 += Avx512F.ShiftRightLogical(p00, 32) + (p01 & mask) + (p10 & mask);
                d02 += Avx512F.ShiftRightLogical(p01, 32) + Avx512F.ShiftRightLogical(p10, 32) + (p11 & mask)
                    - (right & mask & signA) - (left & mask & signB);
                d03 += Avx512F.ShiftRightLogical(p11, 32) - (rightHigh & signA) - (leftHigh & signB);
                d04 += Avx512F.ShiftRightLogical(left & right, 63);
            }
            {
                var right = Vector512.LoadUnsafe(ref b[bStart1 + i]).AsUInt64();
                var rightHigh = Avx512F.ShiftRightLogical(right, 32);
                var p00 = Avx512F.Multiply(left.AsUInt32(), right.AsUInt32());
                var p01 = Avx512F.Multiply(left.AsUInt32(), rightHigh.AsUInt32());
                var p10 = Avx512F.Multiply(leftHigh.AsUInt32(), right.AsUInt32());
                var p11 = Avx512F.Multiply(leftHigh.AsUInt32(), rightHigh.AsUInt32());
                var signB = Vector512<ulong>.Zero - Avx512F.ShiftRightLogical(right, 63);
                d10 += p00 & mask;
                d11 += Avx512F.ShiftRightLogical(p00, 32) + (p01 & mask) + (p10 & mask);
                d12 += Avx512F.ShiftRightLogical(p01, 32) + Avx512F.ShiftRightLogical(p10, 32) + (p11 & mask)
                    - (right & mask & signA) - (left & mask & signB);
                d13 += Avx512F.ShiftRightLogical(p11, 32) - (rightHigh & signA) - (leftHigh & signB);
                d14 += Avx512F.ShiftRightLogical(left & right, 63);
            }
        }
        sum0 = Signed192.FromLimbs(Vector512.Sum(d00.AsInt64()), Vector512.Sum(d01.AsInt64()),
            Vector512.Sum(d02.AsInt64()), Vector512.Sum(d03.AsInt64()), Vector512.Sum(d04.AsInt64()));
        sum1 = Signed192.FromLimbs(Vector512.Sum(d10.AsInt64()), Vector512.Sum(d11.AsInt64()),
            Vector512.Sum(d12.AsInt64()), Vector512.Sum(d13.AsInt64()), Vector512.Sum(d14.AsInt64()));
        return i;
    }

}
