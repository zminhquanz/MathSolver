

namespace MathSolver.Services.Core;

/// <summary>Streams rows into immutable compact blocks with one fixed-size staging buffer.</summary>
public sealed class MatrixInputBuilder : IDisposable
{
    private readonly int _columns, _maximumRows;
    private InputArena? _arena;
    private InputBlockMap _blocks = new();
    private readonly long[] _buffer = new long[MatrixInputStorage.BlockCells];
    private int _bufferCount, _rows, _blockCount;
    private long _defaultValue;
    public MatrixInputBuilder(int columns, int maximumRows = 16384, string? temporaryDirectory = null, long cacheLimitBytes = 2 * 1024 * 1024)
    {
        if (columns is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(columns));
        if (maximumRows is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(maximumRows));
        _columns = columns; _maximumRows = maximumRows; _arena = new InputArena(temporaryDirectory, cacheLimitBytes);
    }
    public void AddRow(ReadOnlySpan<long> values, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_arena is null, this);
        token.ThrowIfCancellationRequested();
        if (values.Length != _columns) throw new ArgumentException("The row width must match the builder columns.", nameof(values));
        if (_rows == _maximumRows) throw new ArgumentOutOfRangeException(nameof(values), "Too many matrix rows.");
        try
        {
            while (!values.IsEmpty)
            {
                token.ThrowIfCancellationRequested();
                int count = Math.Min(values.Length, _buffer.Length - _bufferCount);
                values[..count].CopyTo(_buffer.AsSpan(_bufferCount)); _bufferCount += count; values = values[count..];
                if (_bufferCount == _buffer.Length) Flush();
            }
            _rows++;
        }
        catch { Dispose(); throw; }
    }
    private void Flush()
    {
        if (_bufferCount == 0) return;
        var block = _arena!.Write(_buffer.AsSpan(0, _bufferCount));
        if (_blockCount == 0 && block.IsConstant) _defaultValue = block.Value;
        if (!block.IsConstant || block.Value != _defaultValue) _blocks.Set(_blockCount, block);
        _blockCount++;
        _bufferCount = 0;
    }
    public IntegerMatrix Complete()
    {
        ObjectDisposedException.ThrowIf(_arena is null, this);
        if (_rows == 0) throw new InvalidOperationException("A matrix needs at least one row.");
        var empty = new InputBlockMap();
        Flush();
        var matrix = new IntegerMatrix(new MatrixInputStorage(_rows, _columns, _blocks, _arena, _defaultValue, true, 0));
        _arena = null; _blocks = empty;
        return matrix;
    }
    public void Dispose()
    {
        var arena = _arena; _arena = null;
        try { _blocks.Dispose(); }
        finally { arena?.Dispose(); }
    }
}

