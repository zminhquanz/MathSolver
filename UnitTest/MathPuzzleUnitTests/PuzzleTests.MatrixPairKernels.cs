using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Numerics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    // Catches unsigned/sign corrections, swapped output offsets, dropped prefixes,
    // over-reading tails, and limb carry overflow at the maximum matrix depth.
    public static void CheckMatrixPairedKernelsMatchBigInteger()
    {
        var random = new Random(0x50414952);
        foreach (int lanes in new[] { 2, 4, 8 })
        {
            if (lanes == 2 && !Sse2.IsSupported || lanes == 4 && !Avx2.IsSupported
                || lanes == 8 && !Avx512F.IsSupported) continue;
            foreach (int length in new[] { 0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 31, 63, 127, 257, 16383, 16384 })
            for (int pattern = 0; pattern < 4; pattern++)
            {
                const int aStart = 3, bStart0 = 5;
                int bStart1 = bStart0 + length + 11;
                var a = new long[aStart + length];
                var b = new long[bStart1 + length];
                for (int i = 0; i < length; i++)
                {
                    a[aStart + i] = PairOperand(pattern, i, random);
                    b[bStart0 + i] = PairOperand(pattern, i + 3, random);
                    b[bStart1 + i] = PairOperand(pattern, i + 7, random);
                }
                var sum0 = default(MatrixEngine.Signed192);
                var sum1 = default(MatrixEngine.Signed192);
                sum0.Add(123); sum1.Add(-456); // Both outputs must be assigned, not appended.
                int processed = lanes switch
                {
                    2 => MatrixEngine.DotPair128(a, aStart, b, bStart0, bStart1, length, ref sum0, ref sum1),
                    4 => MatrixEngine.DotPair256(a, aStart, b, bStart0, bStart1, length, ref sum0, ref sum1),
                    _ => MatrixEngine.DotPair512(a, aStart, b, bStart0, bStart1, length, ref sum0, ref sum1)
                };
                TestAssert.That(processed == length / lanes * lanes,
                    $"Paired {lanes}-lane kernel must return its complete-vector prefix for length {length}.");
                BigInteger expected0 = 0, expected1 = 0;
                for (int i = 0; i < processed; i++)
                {
                    expected0 += (BigInteger)a[aStart + i] * b[bStart0 + i];
                    expected1 += (BigInteger)a[aStart + i] * b[bStart1 + i];
                }
                TestAssert.That(sum0.ToBigInteger() == expected0 && sum1.ToBigInteger() == expected1,
                    $"Paired {lanes}-lane prefix differs from BigInteger: length {length}, pattern {pattern}.");
                for (int i = processed; i < length; i++)
                {
                    sum0.Add((Int128)a[aStart + i] * b[bStart0 + i]);
                    sum1.Add((Int128)a[aStart + i] * b[bStart1 + i]);
                    expected0 += (BigInteger)a[aStart + i] * b[bStart0 + i];
                    expected1 += (BigInteger)a[aStart + i] * b[bStart1 + i];
                }
                TestAssert.That(sum0.ToBigInteger() == expected0 && sum1.ToBigInteger() == expected1,
                    $"Paired {lanes}-lane result plus scalar tail differs from BigInteger.");
            }
        }
    }

    private static long PairOperand(int pattern, int index, Random random) => pattern switch
    {
        0 => long.MinValue,
        1 => (index % 10) switch
        {
            0 => long.MinValue, 1 => long.MaxValue, 2 => -1, 3 => 0, 4 => 1,
            5 => uint.MaxValue, 6 => 1L << 32, 7 => -(1L << 32),
            8 => 0x123456789abcdefL, _ => -0x123456789abcdefL
        },
        2 => unchecked((long)(((ulong)random.NextInt64() << 1) | (uint)random.Next(2))),
        _ => (index & 1) == 0 ? long.MinValue : long.MaxValue
    };
}
