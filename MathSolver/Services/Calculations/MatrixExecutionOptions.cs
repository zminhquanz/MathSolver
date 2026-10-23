namespace MathSolver.Services.Core;

/// <summary>Budgets for calculation buffers and result storage, excluding already-owned input matrices.</summary>
public sealed record MatrixExecutionOptions
{
    public int? BlockSize { get; init; }
    public MatrixMultiplyKernel MultiplyKernel { get; init; } = MatrixMultiplyKernel.Auto;
    public bool ReusePackedPanels { get; init; } = true;
    public long WorkspaceBudgetBytes { get; init; } = 64L * 1024 * 1024;
    public long ResultCacheBytes { get; init; } = 8L * 1024 * 1024;
    public long InMemoryResultLimitBytes { get; init; } = 32L * 1024 * 1024;
    public string? TemporaryDirectory { get; init; }

    internal void Validate()
    {
        if (BlockSize is < 16 or > 256) throw new ArgumentOutOfRangeException(nameof(BlockSize));
        if (!Enum.IsDefined(MultiplyKernel)) throw new ArgumentOutOfRangeException(nameof(MultiplyKernel));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(WorkspaceBudgetBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(ResultCacheBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(InMemoryResultLimitBytes);
    }
}

internal sealed record MatrixWorkPlan(int BlockSize, int WorkerLimit, bool FileBacked, bool DenseResult,
    long WorkspaceBytes, long CacheBytes, long ResidentResultBytes, MatrixMemoryCheck Memory)
{
    internal static MatrixWorkPlan Create(int rows, int inner, int columns, MatrixOperation operation,
        int requestedWorkers, MatrixExecutionOptions options, long availableBytes, MatrixBackend backend = MatrixBackend.Scalar,
        bool allowDenseResult = true)
    {
        options.Validate();
        if (rows is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(rows));
        if (inner is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(inner));
        if (columns is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(columns));
        ArgumentOutOfRangeException.ThrowIfNegative(availableBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedWorkers);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        long leftCells = (long)rows * inner;
        long rightCells = operation == MatrixOperation.Multiply ? (long)inner * columns : leftCells;
        int workers = Math.Max(leftCells, rightCells) >= MatrixLimits.ParallelThresholdCells ? requestedWorkers : 1;
        long outputCells = (long)rows * columns;
        long denseBytes = outputCells * 16;
        long workspaceCap = Math.Min(options.WorkspaceBudgetBytes, availableBytes);

        // Keep the existing small elementwise kernels: their only large buffer is the Int128 result.
        if (allowDenseResult && operation != MatrixOperation.Multiply && denseBytes <= options.InMemoryResultLimitBytes)
        {
            int denseWorkers = (int)Math.Min(Math.Min(workers, rows),
                Math.Min(workspaceCap, Math.Max(0, availableBytes - denseBytes)) / 65536);
            if (denseWorkers > 0)
            {
                long workspace = 65536L * denseWorkers;
                return new(0, denseWorkers, false, true, workspace, 0, denseBytes,
                    new(denseBytes + workspace, availableBytes));
            }
        }

        long smallestRefusal = long.MaxValue;
        // Measured policy: larger depth panels help wide single-worker kernels;
        // parallel work uses smaller groups to retain workers within the buffer budget.
        int initialSize = options.BlockSize ?? (workers > 1 ? 64
            : operation == MatrixOperation.Multiply && (backend is MatrixBackend.Avx2 or MatrixBackend.Avx512)
                && rows >= 256 && inner >= 256 && columns >= 256 ? 256 : 128);
        int group = operation == MatrixOperation.Multiply && options.ReusePackedPanels ? 2 : 1;
        for (int size = initialSize; ; size = Math.Max(16, size / 2))
        {
            long tileCount = (long)((rows + size - 1) / size) * ((columns + size - 1) / size);
            // Store descriptors/cache links are <64 bytes/tile; leave additional array/allocator headroom.
            long metadata = tileCount * 128 + 65536;
            // Two packed panels, Signed192 accumulators, Int128 elementwise scratch,
            // BigInteger output including limbs, and one encoded tile (no per-cell file index).
            // Includes the contiguous B source panel before transpose, plus a bounded scan buffer.
            long workerBytes = (long)size * size * (group == 2 ? 240 : 168) + 65536;
            long minimum = metadata + workerBytes;
            smallestRefusal = Math.Min(smallestRefusal, minimum);
            if (minimum <= workspaceCap)
            {
                long resident = outputCells * 64;
                bool file = resident > options.InMemoryResultLimitBytes || resident > availableBytes - minimum;
                if (file) resident = 0;
                long remaining = Math.Min(workspaceCap, availableBytes - resident);
                if (remaining < minimum) { file = true; resident = 0; remaining = workspaceCap; }
                long cache = file ? Math.Min(options.ResultCacheBytes, remaining - minimum) : 0;
                long groups = (long)((rows + size * group - 1) / (size * group)) * ((columns + size * group - 1) / (size * group));
                int count = (int)Math.Min(Math.Min(workers, groups), (remaining - metadata - cache) / workerBytes);
                long workspace = metadata + cache + workerBytes * count;
                return new(size, count, file, false, workspace, cache, resident,
                    new(workspace + resident, availableBytes));
            }
            if (size == 16) break;
        }
        return new(initialSize, 0, true, false, smallestRefusal, 0, 0,
            new(smallestRefusal, workspaceCap));
    }
}
