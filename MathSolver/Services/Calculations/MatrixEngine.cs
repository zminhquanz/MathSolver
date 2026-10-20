using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Services.Core;

public static partial class MatrixEngine
{
    public static MatrixCalculation Calculate(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend maximumBackend = MatrixBackend.Auto, CancellationToken cancellationToken = default,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (!Enum.IsDefined(maximumBackend)) throw new ArgumentOutOfRangeException(nameof(maximumBackend));
        if (operation == MatrixOperation.Multiply ? a.Columns != b.Rows : a.Rows != b.Rows || a.Columns != b.Columns)
            throw new ArgumentException("Matrix dimensions are incompatible for this operation.", nameof(b));
        cancellationToken.ThrowIfCancellationRequested();
        MatrixBackend backend = SelectBackend(a, b, maximumBackend);
        progress?.Report(0);
        cancellationToken.ThrowIfCancellationRequested();
        return operation == MatrixOperation.Multiply
            ? Multiply(a, b, backend, cancellationToken, progress)
            : AddOrSubtract(a, b, operation, backend, cancellationToken, progress);
    }

    private static MatrixBackend SelectBackend(IntegerMatrix a, IntegerMatrix b, MatrixBackend cap)
    {
        if (cap == MatrixBackend.Scalar || a.Rows < 16 || a.Columns < 16 || b.Rows < 16 || b.Columns < 16)
            return MatrixBackend.Scalar;
        if ((cap == MatrixBackend.Auto || cap >= MatrixBackend.Avx512) && Avx512F.IsSupported) return MatrixBackend.Avx512;
        if ((cap == MatrixBackend.Auto || cap >= MatrixBackend.Avx2) && Avx2.IsSupported) return MatrixBackend.Avx2;
        return Sse2.IsSupported ? MatrixBackend.Sse2 : MatrixBackend.Scalar;
    }

    private static MatrixCalculation AddOrSubtract(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend backend, CancellationToken cancellationToken, IProgress<double>? progress)
    {
        var values = new Int128[a.Data.Length];
        bool subtract = operation == MatrixOperation.Subtract;
        long batches = 0;
        for (int row = 0; row < a.Rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int start = row * a.Columns;
            int end = start + a.Columns;
            int index = start;
            switch (backend)
            {
                case MatrixBackend.Sse2: index = Elementwise128(a.Data, b.Data, values, start, end, subtract); break;
                case MatrixBackend.Avx2: index = Elementwise256(a.Data, b.Data, values, start, end, subtract); break;
                case MatrixBackend.Avx512: index = Elementwise512(a.Data, b.Data, values, start, end, subtract); break;
            }
            if (backend != MatrixBackend.Scalar)
            {
                int lanes = backend == MatrixBackend.Sse2 ? 2 : backend == MatrixBackend.Avx2 ? 4 : 8;
                batches += (index - start) / lanes;
            }
            for (; index < end; index++)
                values[index] = subtract ? (Int128)a.Data[index] - b.Data[index] : (Int128)a.Data[index] + b.Data[index];
            progress?.Report((double)(row + 1) / a.Rows);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new MatrixCalculation(a.Rows, a.Columns, operation, backend, batches, values, null);
    }

    private static MatrixCalculation Multiply(IntegerMatrix a, IntegerMatrix b, MatrixBackend backend,
        CancellationToken cancellationToken, IProgress<double>? progress)
    {
        // Contiguous dot operands avoid strided loads in every output cell.
        var transposed = new long[b.Data.Length];
        for (int row = 0; row < b.Rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int column = 0; column < b.Columns; column++)
                transposed[column * b.Rows + row] = b.Data[row * b.Columns + column];
        }
        var values = new BigInteger[a.Rows * b.Columns];
        long batches = 0;
        for (int row = 0; row < a.Rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int column = 0; column < b.Columns; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int leftStart = row * a.Columns;
                int rightStart = column * b.Rows;
                var sum = new Signed192();
                int index = 0;
                switch (backend)
                {
                    case MatrixBackend.Sse2: index = Dot128(a.Data, leftStart, transposed, rightStart, a.Columns, ref sum); break;
                    case MatrixBackend.Avx2: index = Dot256(a.Data, leftStart, transposed, rightStart, a.Columns, ref sum); break;
                    case MatrixBackend.Avx512: index = Dot512(a.Data, leftStart, transposed, rightStart, a.Columns, ref sum); break;
                }
                if (backend != MatrixBackend.Scalar)
                {
                    int lanes = backend == MatrixBackend.Sse2 ? 2 : backend == MatrixBackend.Avx2 ? 4 : 8;
                    batches += index / lanes;
                }
                for (; index < a.Columns; index++)
                    sum.Add((Int128)a.Data[leftStart + index] * transposed[rightStart + index]);
                values[row * b.Columns + column] = sum.ToBigInteger();
            }
            progress?.Report((double)(row + 1) / a.Rows);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new MatrixCalculation(a.Rows, b.Columns, MatrixOperation.Multiply, backend, batches, null, values);
    }

    // The magnitude of an Int64 product is at most 2^126; summing at most 1024
    // products is bounded by 2^136 and fits in 138 signed bits. This 192-bit
    // pair is sufficient for full-range inputs, with no BigInteger per term.
    private struct Signed192
    {
        private UInt128 low;
        private long high;

        // Arithmetic shifts propagate negative carries between signed limbs.
        public static Signed192 FromLimbs(long d0, long d1, long d2, long d3, long d4)
        {
            d1 += d0 >> 32;
            d2 += d1 >> 32;
            d3 += d2 >> 32;
            d4 += d3 >> 32;
            return new Signed192
            {
                low = (uint)d0 | ((UInt128)(uint)d1 << 32) | ((UInt128)(uint)d2 << 64) | ((UInt128)(uint)d3 << 96),
                high = d4
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Int128 value)
        {
            UInt128 previous = low;
            low = unchecked(low + (UInt128)value);
            high += (value < 0 ? -1L : 0L) + (low < previous ? 1L : 0L);
        }

        public readonly BigInteger ToBigInteger() => ((BigInteger)high << 128) + (BigInteger)low;
    }
}
