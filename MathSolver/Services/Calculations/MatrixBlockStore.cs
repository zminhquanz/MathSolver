using System.Numerics;

namespace MathSolver.Services.Core;

public enum MatrixNumberStorage { Int64, Int128, Signed192, BigInteger }

/// <summary>An append-only exact tile store. Publication and all file/cache access share one lock.</summary>
internal sealed class MatrixBlockStore : IDisposable
{
    private static readonly BigInteger Int128Minimum = -(BigInteger.One << 127);
    private static readonly BigInteger Int128Maximum = (BigInteger.One << 127) - 1;
    private static readonly BigInteger Signed192Minimum = -(BigInteger.One << 191);
    private static readonly BigInteger Signed192Maximum = (BigInteger.One << 191) - 1;

    private readonly object _gate = new();
    private readonly int _blockSize, _blockRows, _blockColumns;
    private readonly Block[] _blocks;
    private readonly FileStream? _file;
    private int _cacheHead = -1, _cacheTail = -1;
    private int _written;
    private long _storedBytes, _cachedBytes;
    private bool _complete, _disposed;

    // O(number of tiles), with no per-cell index or decoded BigInteger cache.
    private struct Block
    {
        internal long Offset;
        internal byte[]? Payload;
        internal int Length, Stride;
        internal int PreviousCached, NextCached;
        internal MatrixNumberStorage Kind;
        internal bool Uniform;
    }

    internal MatrixBlockStore(int rows, int columns, int blockSize, bool fileBacked,
        string? temporaryDirectory, long cacheLimitBytes)
    {
        if (rows is < 1 or > 16384) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns is < 1 or > 16384) throw new ArgumentOutOfRangeException(nameof(columns));
        if (blockSize is < 16 or > 256) throw new ArgumentOutOfRangeException(nameof(blockSize));
        if (cacheLimitBytes < 0) throw new ArgumentOutOfRangeException(nameof(cacheLimitBytes));
        Rows = rows;
        Columns = columns;
        IsFileBacked = fileBacked;
        CacheLimitBytes = cacheLimitBytes;
        _blockSize = blockSize;
        _blockRows = (rows + blockSize - 1) / blockSize;
        _blockColumns = (columns + blockSize - 1) / blockSize;
        _blocks = new Block[checked(_blockRows * _blockColumns)];
        if (fileBacked)
        {
            string directory = temporaryDirectory ?? Path.GetTempPath();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "mathsolver-matrix-" + Guid.NewGuid().ToString("N") + ".tmp");
            // CreateNew never takes ownership of an existing file. DeleteOnClose is also the
            // process-exit fallback; the live handle remains owned by this store until Dispose.
            _file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.Read | FileShare.Delete, 1, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
            BackingFilePath = path;
        }
    }

    internal int Rows { get; }
    internal int Columns { get; }
    internal bool IsFileBacked { get; }
    internal string? BackingFilePath { get; }
    internal long CacheLimitBytes { get; }
    internal long StoredBytes { get { lock (_gate) return _storedBytes; } }
    internal long CachedBytes { get { lock (_gate) return _cachedBytes; } }
    internal IReadOnlyCollection<MatrixNumberStorage> StorageKinds
    {
        get
        {
            lock (_gate)
            {
                var kinds = new bool[4];
                foreach (ref readonly Block block in _blocks.AsSpan())
                    if (block.Length != 0) kinds[(int)block.Kind] = true;
                return Enumerable.Range(0, kinds.Length).Where(i => kinds[i]).Select(i => (MatrixNumberStorage)i).ToArray();
            }
        }
    }

    internal void WriteBlock(int blockRow, int blockColumn, int height, int width, ReadOnlySpan<BigInteger> values)
    {
        lock (_gate)
        {
            int index = ValidateWrite(blockRow, blockColumn, height, width);
            if (values.Length != checked(height * width)) throw new ArgumentException("Value count must match the exact tile shape.", nameof(values));
            BigInteger first = values[0];
            bool uniform = true;
            MatrixNumberStorage kind = MatrixNumberStorage.Int64;
            int stride = 8;
            foreach (BigInteger value in values)
            {
                uniform &= value == first;
                MatrixNumberStorage valueKind = Classify(value);
                if (valueKind > kind) kind = valueKind;
                stride = Math.Max(stride, value.GetByteCount());
            }
            stride = Width(kind, stride);
            var payload = new byte[checked(stride * (uniform ? 1 : values.Length))];
            if (uniform) Encode(first, payload);
            else
                for (int i = 0; i < values.Length; i++) Encode(values[i], payload.AsSpan(i * stride, stride));
            Store(index, payload, kind, stride, uniform);
        }
    }

    internal void WriteConstantBlock(int blockRow, int blockColumn, int height, int width, BigInteger value)
    {
        lock (_gate)
        {
            int index = ValidateWrite(blockRow, blockColumn, height, width);
            MatrixNumberStorage kind = Classify(value);
            int stride = Width(kind, value.GetByteCount());
            var payload = new byte[stride];
            Encode(value, payload);
            Store(index, payload, kind, stride, true);
        }
    }

    private int ValidateWrite(int blockRow, int blockColumn, int height, int width)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_complete) throw new InvalidOperationException("Completed matrix storage is immutable.");
        if ((uint)blockRow >= (uint)_blockRows) throw new ArgumentOutOfRangeException(nameof(blockRow));
        if ((uint)blockColumn >= (uint)_blockColumns) throw new ArgumentOutOfRangeException(nameof(blockColumn));
        if (height != Math.Min(_blockSize, Rows - blockRow * _blockSize) ||
            width != Math.Min(_blockSize, Columns - blockColumn * _blockSize))
            throw new ArgumentException("Tile shape must match its matrix position, including partial edges.");
        int index = blockRow * _blockColumns + blockColumn;
        if (_blocks[index].Length != 0) throw new InvalidOperationException("This tile has already been written.");
        return index;
    }

    private void Store(int index, byte[] payload, MatrixNumberStorage kind, int stride, bool uniform)
    {
        long offset = _storedBytes;
        if (_file is not null)
        {
            _file.Position = offset;
            _file.Write(payload);
        }
        _blocks[index] = new Block { Offset = offset, Payload = _file is null ? payload : null,
            Length = payload.Length, Stride = stride, Kind = kind, Uniform = uniform,
            PreviousCached = -1, NextCached = -1 };
        _storedBytes = checked(offset + payload.Length);
        _written++;
    }

    internal void Complete()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_written != _blocks.Length) throw new InvalidOperationException("Every tile must be written before publication.");
            if (_complete) return;
            _file?.Flush();
            _complete = true;
        }
    }

    internal BigInteger GetValue(int row, int column)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_complete) throw new InvalidOperationException("Matrix storage must be completed before reading.");
            if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row));
            if ((uint)column >= (uint)Columns) throw new ArgumentOutOfRangeException(nameof(column));
            int blockColumn = column / _blockSize;
            int index = row / _blockSize * _blockColumns + blockColumn;
            ref Block block = ref _blocks[index];
            int width = Math.Min(_blockSize, Columns - blockColumn * _blockSize);
            int position = block.Uniform ? 0 : checked(((row % _blockSize) * width + column % _blockSize) * block.Stride);
            if (block.Payload is not null)
            {
                if (IsFileBacked && _cacheHead != index)
                {
                    UnlinkCached(index);
                    LinkCacheHead(index);
                }
                return new BigInteger(block.Payload.AsSpan(position, block.Stride), isUnsigned: false, isBigEndian: false);
            }
            if (block.Length > CacheLimitBytes)
            {
                // Oversized tiles never enter the cache. Read one encoded cell directly,
                // so even a zero-byte cache does not allocate a tile-sized scratch buffer.
                Span<byte> cell = block.Stride <= 64 ? stackalloc byte[64] : new byte[block.Stride];
                cell = cell[..block.Stride];
                _file!.Position = block.Offset + position;
                _file.ReadExactly(cell);
                return new BigInteger(cell, isUnsigned: false, isBigEndian: false);
            }
            while (_cachedBytes > CacheLimitBytes - block.Length)
            {
                int evicted = _cacheTail;
                _cachedBytes -= _blocks[evicted].Length;
                _blocks[evicted].Payload = null;
                UnlinkCached(evicted);
            }
            var payload = new byte[block.Length];
            _file!.Position = block.Offset;
            _file.ReadExactly(payload);
            block.Payload = payload;
            LinkCacheHead(index);
            _cachedBytes += payload.Length;
            return new BigInteger(payload.AsSpan(position, block.Stride), isUnsigned: false, isBigEndian: false);
        }
    }

    // Cache links live in the tile descriptors: no dictionary or per-cache-entry node
    // allocations can turn a cache of tiny uniform tiles into unbudgeted metadata.
    private void UnlinkCached(int index)
    {
        ref Block block = ref _blocks[index];
        if (block.PreviousCached >= 0) _blocks[block.PreviousCached].NextCached = block.NextCached;
        else _cacheHead = block.NextCached;
        if (block.NextCached >= 0) _blocks[block.NextCached].PreviousCached = block.PreviousCached;
        else _cacheTail = block.PreviousCached;
        block.PreviousCached = block.NextCached = -1;
    }

    private void LinkCacheHead(int index)
    {
        ref Block block = ref _blocks[index];
        block.PreviousCached = -1;
        block.NextCached = _cacheHead;
        if (_cacheHead >= 0) _blocks[_cacheHead].PreviousCached = index;
        else _cacheTail = index;
        _cacheHead = index;
    }

    private static MatrixNumberStorage Classify(BigInteger value) =>
        value >= long.MinValue && value <= long.MaxValue ? MatrixNumberStorage.Int64 :
        value >= Int128Minimum && value <= Int128Maximum ? MatrixNumberStorage.Int128 :
        value >= Signed192Minimum && value <= Signed192Maximum ? MatrixNumberStorage.Signed192 : MatrixNumberStorage.BigInteger;

    private static int Width(MatrixNumberStorage kind, int bigWidth) => kind switch
    {
        MatrixNumberStorage.Int64 => 8,
        MatrixNumberStorage.Int128 => 16,
        MatrixNumberStorage.Signed192 => 24,
        _ => bigWidth
    };

    private static void Encode(BigInteger value, Span<byte> destination)
    {
        destination.Fill(value.Sign < 0 ? (byte)255 : (byte)0);
        if (!value.TryWriteBytes(destination, out _, isUnsigned: false, isBigEndian: false))
            throw new InvalidOperationException("The tile's signed numeric width is insufficient.");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _cacheHead = _cacheTail = -1;
            _cachedBytes = 0;
            Array.Clear(_blocks);
            _file?.Dispose();
        }
    }
}
