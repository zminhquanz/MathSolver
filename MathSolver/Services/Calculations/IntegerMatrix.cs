using System.Numerics;

namespace MathSolver.Services.Core;

public enum MatrixOperation { Add, Subtract, Multiply }
public enum MatrixBackend { Auto, Scalar, Sse2, Avx2, Avx512 }

public sealed class IntegerMatrix : IDisposable
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
        DenseData = data ?? new long[rows * columns];
    }
    public int Rows { get; }
    public int Columns { get; }
    /// <summary>Row-major mutable input. Callers must not edit it during Calculate.</summary>
    public long[] Data => DenseData ?? throw new InvalidOperationException("Paged matrix inputs do not expose a dense array. Use GetValue or CopyRowTo.");
    public long[]? DenseData { get; }
    internal MatrixInputStorage? Storage { get; }
    private int _disposed;
    internal IntegerMatrix(MatrixInputStorage storage)
    {
        Storage = storage; Rows = storage.Rows; Columns = storage.Columns;
    }
    public bool IsFileBacked => Storage?.IsFileBacked ?? false;
    public long StoredBytes => Storage?.StoredBytes ?? (long)Rows * Columns * sizeof(long);
    public long CachedBytes => Storage?.CachedBytes ?? 0;
    public long CacheLimitBytes => Storage?.CacheLimitBytes ?? 0;
    public long GetValue(int row, int column)
    {
        CheckCoordinates(row, column);
        return DenseData is { } data ? data[row * Columns + column] : Storage!.GetValue(row * Columns + column);
    }
    public void CopyRowTo(int row, int column, Span<long> destination)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row));
        if ((uint)column > (uint)Columns || destination.Length > Columns - column) throw new ArgumentOutOfRangeException(nameof(column));
        if (DenseData is { } data) data.AsSpan(row * Columns + column, destination.Length).CopyTo(destination);
        else Storage!.CopyTo(row * Columns + column, destination);
    }
    public bool TryGetUniformValue(out long value)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        value = 0;
        return Storage is not null && Storage.TryGetUniformValue(out value);
    }
    private void CheckCoordinates(int row, int column)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row));
        if ((uint)column >= (uint)Columns) throw new ArgumentOutOfRangeException(nameof(column));
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) Storage?.Dispose();
    }
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
