using MathSolver.Services.Core;
using System.Numerics;

namespace MathSolver.Tests;

internal static class MatrixNeonChecks
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void Run()
    {
        long[] edges = [long.MinValue, long.MaxValue, -1, 0, 1, uint.MaxValue, 1L << 32, -(1L << 32)];
        var random = new Random(1471);
        foreach (int length in new[] { 0, 1, 2, 3, 7, 16, 17, 63, 127, 257, 16383, 16384 })
        {
            const int offset = 3;
            var a = new long[length + offset]; var b = new long[length + offset];
            var output = new Int128[length + offset + 1];
            for (int pattern = 0; pattern < 4; pattern++)
            {
                for (int i = 0; i < length; i++)
                {
                    a[i + offset] = pattern switch { 0 => long.MinValue, 1 => edges[i % edges.Length],
                        2 => unchecked((long)(((ulong)random.NextInt64() << 1) | (uint)random.Next(2))), _ => i % 61 - 30 };
                    b[i + offset] = pattern switch { 0 => long.MinValue, 1 => edges[(i + 3) % edges.Length],
                        2 => unchecked((long)(((ulong)random.NextInt64() << 1) | (uint)random.Next(2))), _ => i % 37 - 18 };
                }
                foreach (bool subtract in new[] { false, true })
                {
                    Array.Fill(output, Int128.MinValue);
                    int end = MatrixEngine.ElementwiseNeon(a, b, output, offset, offset + length, subtract);
                    Check(end == offset + length / 2 * 2, "NEON elementwise prefix/tail");
                    Check(output[offset - 1] == Int128.MinValue && output[end] == Int128.MinValue, "NEON out-of-range output write");
                    for (int i = offset; i < end; i++)
                        Check(output[i] == (subtract ? (Int128)a[i] - b[i] : (Int128)a[i] + b[i]), "NEON Int128 carry/borrow");
                }
                var sum = default(MatrixEngine.Signed192); sum.Add(99);
                int processed = MatrixEngine.DotNeon(a, offset, b, offset, length, ref sum);
                BigInteger expected = 0;
                for (int i = 0; i < processed; i++) expected += (BigInteger)a[offset + i] * b[offset + i];
                Check(processed == length / 2 * 2 && sum.ToBigInteger() == expected, "NEON full-range Signed192 dot");
                for (int i = processed; i < length; i++)
                { sum.Add((Int128)a[offset + i] * b[offset + i]); expected += (BigInteger)a[offset + i] * b[offset + i]; }
                Check(sum.ToBigInteger() == expected, "NEON scalar tail");
                if (pattern == 3)
                {
                    processed = MatrixEngine.DotNeonBounded(a, offset, b, offset, length, ref sum);
                    expected = 0;
                    for (int i = 0; i < processed; i++) expected += (BigInteger)a[offset + i] * b[offset + i];
                    Check(sum.ToBigInteger() == expected, "NEON bounded dot");
                }
            }
        }
        Check(MatrixEngine.CanAccumulateNeonInInt64([long.MinValue], 1, [0], 1, 256), "Zero panel bound");
        Check(!MatrixEngine.CanAccumulateNeonInInt64([long.MinValue], 1, [-1], 1, 1), "MinValue magnitude bound");
        Check(MatrixEngine.CanAccumulateNeonInInt64([long.MaxValue / 2], 1, [1], 1, 2), "Exact safe bound");
        Check(!MatrixEngine.CanAccumulateNeonInInt64([long.MaxValue / 2 + 1], 1, [1], 1, 2), "Unsafe bound by one");
        Check(!MatrixEngine.CanAccumulateNeonInInt64([1L << 32], 1, [1L << 32], 1, 1), "UInt128 bound product");
        foreach (int depth in new[] { 2, 16, 256, 16384 })
        foreach (long sign in new[] { -1L, 1L })
        {
            var a = Enumerable.Repeat(sign * (long.MaxValue / depth), depth).ToArray();
            var b = Enumerable.Repeat(1L, depth).ToArray();
            Check(MatrixEngine.CanAccumulateNeonInInt64(a, depth, b, depth, depth), "Near-limit panel accepted");
            var sum = default(MatrixEngine.Signed192);
            int done = MatrixEngine.DotNeonBounded(a, 0, b, 0, depth, ref sum);
            Check(done == depth && sum.ToBigInteger() == (BigInteger)a[0] * depth, "Near-limit positive/negative bounded dot");
        }

        foreach (int n in new[] { 15, 16, 17, 31, 65, 129 })
        {
            using var a = new IntegerMatrix(n, n + 1);
            using var b = new IntegerMatrix(n, n + 1);
            for (int i = 0; i < a.CellCount; i++) { a.Data[i] = edges[i % 8]; b.Data[i] = edges[(i + 3) % 8]; }
            foreach (var op in new[] { MatrixOperation.Add, MatrixOperation.Subtract })
            foreach (long memory in new[] { 0L, 32L * 1024 * 1024 })
            {
                using var result = MatrixEngine.Calculate(a, b, op, executionOptions: new() { InMemoryResultLimitBytes = memory });
                for (int r = 0; r < n; r++) for (int c = 0; c <= n; c++)
                    Check(result.GetValue(r, c) == (op == MatrixOperation.Add ? (BigInteger)a.Data[r * (n + 1) + c] + b.Data[r * (n + 1) + c]
                        : (BigInteger)a.Data[r * (n + 1) + c] - b.Data[r * (n + 1) + c]), "Engine elementwise exact result");
                Check(n < 16 ? result.Backend == MatrixBackend.Scalar : !MatrixEngine.IsNeonSupported || result.Backend == MatrixBackend.Neon,
                    "Small matrix scalar threshold / ARM dispatch");
            }
        }
        foreach (int n in new[] { 16, 17, 33 })
        foreach (bool small in new[] { false, true })
        foreach (var kind in new[] { MatrixMultiplyKernel.SingleOutput, MatrixMultiplyKernel.PairedOutputs })
        {
            using var a = new IntegerMatrix(n, n + 1); using var b = new IntegerMatrix(n + 1, n + 2);
            for (int i = 0; i < a.CellCount; i++) a.Data[i] = small ? i % 31 - 15 : edges[i % 8];
            for (int i = 0; i < b.CellCount; i++) b.Data[i] = small ? i % 37 - 18 : edges[(i + 3) % 8];
            using var reference = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, MatrixBackend.Scalar);
            using var result = MatrixEngine.Calculate(a, b, MatrixOperation.Multiply, executionOptions: new() { BlockSize = 16, MultiplyKernel = kind });
            Check(!result.UsedUniformShortcut, "Nonuniform multiplication required");
            for (int r = 0; r < a.Rows; r++) for (int c = 0; c < b.Columns; c++)
                Check(result.GetValue(r, c) == reference.GetValue(r, c), "Engine NEON tiles/pairs/tails vs Scalar");
        }
        var uniform = Enumerable.Repeat(long.MinValue, 65539).ToArray();
        var scan = MatrixEngine.ScanUniform(uniform, MatrixBackend.Auto, 1);
        Check(scan.IsUniform && (!MatrixEngine.IsNeonSupported || scan.Statistics.Backend == MatrixBackend.Neon), "NEON uniform scan");
        uniform[^1] ^= 1L << 32;
        Check(!MatrixEngine.ScanUniform(uniform, MatrixBackend.Auto, 1).IsUniform, "NEON high-word scan tail");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        using var x = new IntegerMatrix(16, 16);
        Check(MatrixEngine.TryCalculate(x, x, MatrixOperation.Multiply, cancellationToken: cancel.Token).IsCanceled, "Canceled outcome");
        using var stop = new CancellationTokenSource();
        using var working = new IntegerMatrix(65, 65);
        for (int i = 0; i < working.CellCount; i++) working.Data[i] = i % 23 - 11;
        using var canceledResult = MatrixEngine.TryCalculate(working, working, MatrixOperation.Multiply,
            cancellationToken: stop.Token, progress: new CancelOnProgress(stop),
            executionOptions: new() { BlockSize = 16, InMemoryResultLimitBytes = 0 });
        Check(canceledResult.IsCanceled && canceledResult.Result is null, "In-flight cancellation must discard partial result");
        Check(MatrixEngine.ResolveBackend(MatrixBackend.Scalar) == MatrixBackend.Scalar, "Scalar preference");
        Check(MatrixEngine.ResolveBackend(MatrixBackend.Neon) == (MatrixEngine.IsNeonSupported ? MatrixBackend.Neon : MatrixBackend.Scalar), "NEON unsupported fallback");
    }

    private sealed class CancelOnProgress(CancellationTokenSource source) : IProgress<double>
    {
        public void Report(double value) { if (value > .2) source.Cancel(); }
    }
}
