using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Buffers.Binary;

namespace MathSolver.Services.Core;

public readonly record struct MatrixCalculationAttempt(MatrixCalculation? Result, MatrixMemoryCheck Memory) : IDisposable
{
    public void Dispose() => Result?.Dispose();
}

public static partial class MatrixEngine
{
    public static MatrixCalculation Calculate(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend maximumBackend = MatrixBackend.Auto, CancellationToken cancellationToken = default,
        IProgress<double>? progress = null, int maximumDegreeOfParallelism = 0, MatrixExecutionOptions? executionOptions = null)
    {
        var attempt = TryCalculate(a, b, operation, maximumBackend, cancellationToken, progress, maximumDegreeOfParallelism, executionOptions);
        return attempt.Result ?? throw new MatrixMemoryException(attempt.Memory.RequiredBytes, attempt.Memory.AvailableBytes);
    }

    /// <summary>Returns a memory refusal before allocating workspace; expected low memory does not throw.</summary>
    public static MatrixCalculationAttempt TryCalculate(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend maximumBackend = MatrixBackend.Auto, CancellationToken cancellationToken = default,
        IProgress<double>? progress = null, int maximumDegreeOfParallelism = 0, MatrixExecutionOptions? executionOptions = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (!Enum.IsDefined(maximumBackend)) throw new ArgumentOutOfRangeException(nameof(maximumBackend));
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDegreeOfParallelism);
        if (operation == MatrixOperation.Multiply ? a.Columns != b.Rows : a.Rows != b.Rows || a.Columns != b.Columns)
            throw new ArgumentException("Matrix dimensions are incompatible for this operation.", nameof(b));
        cancellationToken.ThrowIfCancellationRequested();
        var options = executionOptions ?? new MatrixExecutionOptions();
        var available = MatrixMemoryGuard.CheckAvailable(0);
        int requested = Math.Max(1, Math.Min(Environment.ProcessorCount,
            maximumDegreeOfParallelism == 0 ? Environment.ProcessorCount : maximumDegreeOfParallelism));
        var plan = MatrixWorkPlan.Create(a.Rows, a.Columns, b.Columns, operation, requested, options, available.AvailableBytes);
        cancellationToken.ThrowIfCancellationRequested();
        if (!plan.Memory.CanAllocate) return new(null, plan.Memory);
        MatrixBackend backend = SelectBackend(a, b, maximumBackend);
        progress?.Report(0);
        cancellationToken.ThrowIfCancellationRequested();
        var result = plan.DenseResult
            ? AddOrSubtract(a, b, operation, backend, plan.WorkerLimit, cancellationToken, progress)
            : CalculateBlocked(a, b, operation, backend, plan, options, cancellationToken, progress);
        result.WorkspaceLimitBytes = plan.WorkspaceBytes;
        return new(result, plan.Memory);
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
        MatrixBackend backend, int workers, CancellationToken cancellationToken, IProgress<double>? progress)
    {
        var values = GC.AllocateUninitializedArray<Int128>(a.Data.Length);
        bool subtract = operation == MatrixOperation.Subtract;
        int lanes = backend == MatrixBackend.Sse2 ? 2 : backend == MatrixBackend.Avx2 ? 4 : 8;
        var execution = ExecuteRows(a.Rows, workers, row =>
        {
            int start = row * a.Columns;
            int end = start + a.Columns;
            int index = start;
            switch (backend)
            {
                case MatrixBackend.Sse2: index = Elementwise128(a.Data, b.Data, values, start, end, subtract); break;
                case MatrixBackend.Avx2: index = Elementwise256(a.Data, b.Data, values, start, end, subtract); break;
                case MatrixBackend.Avx512: index = Elementwise512(a.Data, b.Data, values, start, end, subtract); break;
            }
            long batches = backend == MatrixBackend.Scalar ? 0 : (index - start) / lanes;
            for (; index < end; index++)
                values[index] = subtract ? (Int128)a.Data[index] - b.Data[index] : (Int128)a.Data[index] + b.Data[index];
            return batches;
        }, cancellationToken, progress, 0, 1);
        cancellationToken.ThrowIfCancellationRequested();
        return new MatrixCalculation(a.Rows, a.Columns, operation, backend, execution.Batches, values, null,
            execution.Workers, Math.Min(a.Rows, workers));
    }

    // The magnitude of an Int64 product is at most 2^126; summing at most 16384
    // products is bounded by 2^140 and fits in 142 signed bits. This 192-bit
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Signed192 value)
        {
            UInt128 previous = low;
            low = unchecked(low + value.low);
            high += value.high + (low < previous ? 1L : 0L);
        }

        public readonly BigInteger ToBigInteger()
        {
            Span<byte> bytes = stackalloc byte[24];
            BinaryPrimitives.WriteUInt128LittleEndian(bytes, low);
            BinaryPrimitives.WriteInt64LittleEndian(bytes[16..], high);
            return new BigInteger(bytes, isUnsigned: false, isBigEndian: false);
        }
    }
}
