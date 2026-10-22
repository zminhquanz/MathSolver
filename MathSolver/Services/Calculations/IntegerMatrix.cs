using System.Numerics;

namespace MathSolver.Services.Core;

public enum MatrixOperation { Add, Subtract, Multiply }
public enum MatrixBackend { Auto, Scalar, Sse2, Avx2, Avx512 }

public sealed class IntegerMatrix
{
    public IntegerMatrix(int rows, int columns, long[]? data = null)
    {
        if (rows is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(columns));
        if (data is not null && data.Length != rows * columns)
            throw new ArgumentException("The data length must equal rows times columns.", nameof(data));
        Rows = rows;
        Columns = columns;
        if (data is null) MatrixMemoryGuard.EnsureAvailable((long)rows * columns * sizeof(long));
        Data = data ?? new long[rows * columns];
    }
    public int Rows { get; }
    public int Columns { get; }
    /// <summary>Row-major mutable input. Callers must not edit it during Calculate.</summary>
    public long[] Data { get; }
}

public sealed class MatrixCalculation : IDisposable
{
    private readonly MatrixBlockStore? _blocks;
    private int _disposed;
    internal MatrixCalculation(int rows, int columns, MatrixOperation operation, MatrixBackend backend,
        long vectorBatches, Int128[]? wideValues, BigInteger[]? bigValues, int workerCount = 1, int workerLimit = 1)
    {
        Rows = rows;
        Columns = columns;
        Operation = operation;
        Backend = backend;
        VectorBatches = vectorBatches;
        WideValues = wideValues;
        BigValues = bigValues;
        WorkerCount = workerCount;
        WorkerLimit = workerLimit;
    }
    internal MatrixCalculation(MatrixBlockStore blocks, MatrixOperation operation, MatrixBackend backend,
        long vectorBatches, int workerCount, MatrixWorkPlan plan, bool uniform = false)
    {
        _blocks = blocks;
        Rows = blocks.Rows; Columns = blocks.Columns;
        Operation = operation; Backend = backend; VectorBatches = vectorBatches;
        WorkerCount = workerCount; WorkerLimit = uniform ? 1 : plan.WorkerLimit;
        WorkspaceLimitBytes = plan.WorkspaceBytes; BlockSize = plan.BlockSize;
        UsedUniformShortcut = uniform;
    }
    public int Rows { get; }
    public int Columns { get; }
    public MatrixOperation Operation { get; }
    public MatrixBackend Backend { get; }
    /// <summary>Number of vector batches actually executed, excluding scalar tails.</summary>
    public long VectorBatches { get; }
    /// <summary>Peak simultaneous row kernels observed across the calculation stages.</summary>
    public int WorkerCount { get; }
    public int WorkerLimit { get; }
    public Int128[]? WideValues { get; }
    public BigInteger[]? BigValues { get; }
    public bool IsFileBacked => _blocks?.IsFileBacked ?? false;
    public IReadOnlyCollection<MatrixNumberStorage> StorageKinds => _blocks?.StorageKinds
        ?? (BigValues is not null ? [MatrixNumberStorage.BigInteger] : [MatrixNumberStorage.Int128]);
    public long StoredBytes => _blocks?.StoredBytes ?? (long)Rows * Columns * 16;
    public long WorkspaceLimitBytes { get; internal set; }
    public long CacheLimitBytes => _blocks?.CacheLimitBytes ?? 0;
    public long CachedBytes => _blocks?.CachedBytes ?? 0;
    public int BlockSize { get; }
    public bool UsedUniformShortcut { get; }
    public MatrixUniformScanStatistics UniformScan { get; internal set; }
    public BigInteger GetValue(int row, int column)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row));
        if ((uint)column >= (uint)Columns) throw new ArgumentOutOfRangeException(nameof(column));
        if (_blocks is not null) return _blocks.GetValue(row, column);
        int index = row * Columns + column;
        return BigValues is not null ? BigValues[index] : (BigInteger)WideValues![index];
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _blocks?.Dispose();
    }
}
