namespace MathSolver.Services.Core;

/// <summary>Budgets for calculation buffers and result storage, excluding already-owned input matrices.</summary>
public sealed record MatrixExecutionOptions
{
    public int BlockSize { get; init; } = 128;
    public long WorkspaceBudgetBytes { get; init; } = 64L * 1024 * 1024;
    public long ResultCacheBytes { get; init; } = 8L * 1024 * 1024;
    public long InMemoryResultLimitBytes { get; init; } = 32L * 1024 * 1024;
    public string? TemporaryDirectory { get; init; }

    internal void Validate()
    {
        if (BlockSize is < 16 or > 256) throw new ArgumentOutOfRangeException(nameof(BlockSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(WorkspaceBudgetBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(ResultCacheBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(InMemoryResultLimitBytes);
    }
}

internal sealed record MatrixWorkPlan(int BlockSize, int WorkerLimit, bool FileBacked, bool DenseResult,
    long WorkspaceBytes, long CacheBytes, long ResidentResultBytes, MatrixMemoryCheck Memory)
{
    internal static MatrixWorkPlan Create(int rows, int inner, int columns, MatrixOperation operation,
        int requestedWorkers, MatrixExecutionOptions options, long availableBytes)
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
        if (operation != MatrixOperation.Multiply && denseBytes <= options.InMemoryResultLimitBytes)
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
        for (int size = options.BlockSize; ; size = Math.Max(16, size / 2))
        {
            long tileCount = (long)((rows + size - 1) / size) * ((columns + size - 1) / size);
            // Store descriptors/cache links are <64 bytes/tile; leave additional array/allocator headroom.
            long metadata = tileCount * 128 + 65536;
            // Two packed panels, Signed192 accumulators, Int128 elementwise scratch,
            // BigInteger output including limbs, and one encoded tile (no per-cell file index).
            long workerBytes = (long)size * size * 160 + 65536;
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
                int count = (int)Math.Min(Math.Min(workers, tileCount), (remaining - metadata - cache) / workerBytes);
                long workspace = metadata + cache + workerBytes * count;
                return new(size, count, file, false, workspace, cache, resident,
                    new(workspace + resident, availableBytes));
            }
            if (size == 16) break;
        }
        return new(options.BlockSize, 0, true, false, smallestRefusal, 0, 0,
            new(smallestRefusal, workspaceCap));
    }
}
