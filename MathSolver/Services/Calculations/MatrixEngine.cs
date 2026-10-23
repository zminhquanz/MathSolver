using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Buffers.Binary;
using System.Diagnostics;

namespace MathSolver.Services.Core;

public readonly record struct MatrixCalculationAttempt(MatrixCalculation? Result, MatrixMemoryCheck Memory) : IDisposable
{
    public bool IsCanceled { get; init; }
    public bool IsCancelled { get => IsCanceled; init => IsCanceled = value; }
    public MatrixCalculationAttempt(MatrixCalculation? Result, MatrixMemoryCheck Memory, bool IsCancelled)
        : this(Result, Memory) { IsCanceled = IsCancelled; }
    public void Deconstruct(out MatrixCalculation? result, out MatrixMemoryCheck memory, out bool isCancelled)
    { result = Result; memory = Memory; isCancelled = IsCanceled; }
    internal static MatrixCalculationAttempt Canceled => new(null, default) { IsCanceled = true };
    public void Dispose() => Result?.Dispose();
}

public static partial class MatrixEngine
{
    public static MatrixCalculation Calculate(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend maximumBackend = MatrixBackend.Auto, CancellationToken cancellationToken = default,
        IProgress<double>? progress = null, int maximumDegreeOfParallelism = 0, MatrixExecutionOptions? executionOptions = null,
        IProgress<MatrixCalculationProgress>? calculationProgress = null)
    {
        var attempt = TryCalculate(a, b, operation, maximumBackend, cancellationToken, progress, maximumDegreeOfParallelism, executionOptions, calculationProgress);
        // Preserve the throwing API for callers that explicitly use Calculate.
        // The interactive UI uses TryCalculate and receives cancellation as data.
        if (attempt.IsCanceled) throw new OperationCanceledException(cancellationToken);
        return attempt.Result ?? throw new MatrixMemoryException(attempt.Memory.RequiredBytes, attempt.Memory.AvailableBytes);
    }

    /// <summary>Returns cancellation or a memory refusal as a normal outcome; neither throws.</summary>
    public static MatrixCalculationAttempt TryCalculate(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend maximumBackend = MatrixBackend.Auto, CancellationToken cancellationToken = default,
        IProgress<double>? progress = null, int maximumDegreeOfParallelism = 0, MatrixExecutionOptions? executionOptions = null,
        IProgress<MatrixCalculationProgress>? calculationProgress = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (!Enum.IsDefined(maximumBackend)) throw new ArgumentOutOfRangeException(nameof(maximumBackend));
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDegreeOfParallelism);
        if (operation == MatrixOperation.Multiply ? a.Columns != b.Rows : a.Rows != b.Rows || a.Columns != b.Columns)
            throw new ArgumentException("Matrix dimensions are incompatible for this operation.", nameof(b));
        if (cancellationToken.IsCancellationRequested) return MatrixCalculationAttempt.Canceled;
        var reporting = new MatrixProgressReporter(progress, calculationProgress);
        reporting.Phase(MatrixCalculationStage.Preparing, 0);
        if (cancellationToken.IsCancellationRequested) return MatrixCalculationAttempt.Canceled;
        var options = executionOptions ?? new MatrixExecutionOptions();
        var available = MatrixMemoryGuard.CheckAvailable(0);
        int requested = Math.Max(1, Math.Min(Environment.ProcessorCount,
            maximumDegreeOfParallelism == 0 ? Environment.ProcessorCount : maximumDegreeOfParallelism));
        MatrixBackend backend = SelectBackend(a, b, maximumBackend);
        var plan = MatrixWorkPlan.Create(a.Rows, a.Columns, b.Columns, operation, requested, options, available.AvailableBytes, backend,
            allowDenseResult: a.DenseData is not null && b.DenseData is not null);
        if (cancellationToken.IsCancellationRequested) return MatrixCalculationAttempt.Canceled;
        if (!plan.Memory.CanAllocate) return new(null, plan.Memory);
        reporting.Report(0);
        if (cancellationToken.IsCancellationRequested) return MatrixCalculationAttempt.Canceled;
        var result = plan.DenseResult
            ? AddOrSubtract(a, b, operation, backend, plan.WorkerLimit, cancellationToken, reporting)
            : CalculateBlocked(a, b, operation, backend, plan, options, cancellationToken, reporting);
        if (plan.DenseResult) reporting.Phase(MatrixCalculationStage.Finalizing, .99);
        if (result is null || cancellationToken.IsCancellationRequested)
        {
            result?.Dispose();
            return MatrixCalculationAttempt.Canceled;
        }
        result.WorkspaceLimitBytes = plan.WorkspaceBytes;
        try { reporting.Complete(); }
        catch { result.Dispose(); throw; }
        if (cancellationToken.IsCancellationRequested)
        {
            result.Dispose();
            return MatrixCalculationAttempt.Canceled;
        }
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

    private static MatrixCalculation? AddOrSubtract(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend backend, int workers, CancellationToken cancellationToken, IProgress<double>? progress)
    {
        if (progress is MatrixProgressReporter reporting) reporting.Phase(MatrixCalculationStage.Multiplying);
        var left = a.DenseData!;
        var right = b.DenseData!;
        var values = GC.AllocateUninitializedArray<Int128>(left.Length);
        bool subtract = operation == MatrixOperation.Subtract;
        int lanes = backend == MatrixBackend.Sse2 ? 2 : backend == MatrixBackend.Avx2 ? 4 : 8;
        long kernelTicks = 0;
        var execution = ExecuteRows(a.Rows, workers, row =>
        {
            long started = Stopwatch.GetTimestamp();
            int start = row * a.Columns;
            int end = start + a.Columns;
            int index = start;
            switch (backend)
            {
                case MatrixBackend.Sse2: index = Elementwise128(left, right, values, start, end, subtract); break;
                case MatrixBackend.Avx2: index = Elementwise256(left, right, values, start, end, subtract); break;
                case MatrixBackend.Avx512: index = Elementwise512(left, right, values, start, end, subtract); break;
            }
            long batches = backend == MatrixBackend.Scalar ? 0 : (index - start) / lanes;
            for (; index < end; index++)
                values[index] = subtract ? (Int128)left[index] - right[index] : (Int128)left[index] + right[index];
            Interlocked.Add(ref kernelTicks, Stopwatch.GetTimestamp() - started);
            return batches;
        }, cancellationToken, progress, 0, 1);
        if (cancellationToken.IsCancellationRequested) return null;
        return new MatrixCalculation(a.Rows, a.Columns, operation, backend, execution.Batches, values, null,
            execution.Workers, Math.Min(a.Rows, workers))
            { Execution = new(default, Stopwatch.GetElapsedTime(0, kernelTicks), default, 0, 0, 0, default) };
    }

    // The magnitude of an Int64 product is at most 2^126; summing at most 16384
    // products is bounded by 2^140 and fits in 142 signed bits. This 192-bit
    // pair is sufficient for full-range inputs, with no BigInteger per term.
    internal struct Signed192
    {
        private UInt128 low;
        private long high;

        // Arithmetic shifts propagate negative carries between signed limbs.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
