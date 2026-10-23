using System.Runtime.InteropServices;

namespace MathSolver.Services.Core;

// Snapshots share a reference-counted block tree and one bounded cache.
// No entry contains a managed value buffer; only the bounded arena cache does.
internal sealed class MatrixInputStorage : IDisposable
{
    internal const int BlockCells = 4096;
    internal const int BlockBytes = BlockCells * sizeof(long) + BlockCells / 8;
    internal const long EmptyResidentBytes = 512;
    private readonly object _gate = new();
    private InputBlockMap _blocks;
    private InputArena _arena;
    private long _defaultValue;
    private bool _defaultFilled;
    private bool _disposed;
    internal int Rows { get; }
    internal int Columns { get; }
    internal int CellCount => Rows * Columns;
    internal int MissingCount { get; private set; }
    internal bool IsFileBacked { get { lock (_gate) return _arena.IsFileBacked || _blocks.Values.Any(block => block.Arena is not null); } }
    internal long StoredBytes { get { lock (_gate) return _blocks.Values.Where(block => block.Arena is not null).Select(block => block.Arena!).Append(_arena).Distinct().Sum(arena => arena.StoredBytes); } }
    internal long CachedBytes { get { lock (_gate) return _arena.CachedBytes; } }
    internal long CacheLimitBytes { get { lock (_gate) return _arena.CacheLimitBytes; } }
    // Count persistent tree nodes conservatively, including paths shared by snapshots.
    // Shared cache bytes may be counted by each owner; this safely bounds release estimates.
    internal long ResidentBytes { get { lock (_gate) return _disposed ? 0 : EmptyResidentBytes + (long)_blocks.Count * 17 * 64 + _arena.CachedBytes; } }
    internal string? StoragePath { get { lock (_gate) return _arena.FilePath ?? _blocks.Values.Select(block => block.Arena?.FilePath).FirstOrDefault(path => path is not null); } }

    internal MatrixInputStorage(int rows, int columns, string? directory = null, long cacheLimitBytes = 2 * 1024 * 1024)
        : this(rows, columns, new InputBlockMap(), new InputArena(directory, cacheLimitBytes), 0, false, rows * columns) { }

    internal MatrixInputStorage(int rows, int columns, InputBlockMap blocks, InputArena arena,
        long defaultValue, bool defaultFilled, int missingCount)
    {
        Rows = rows; Columns = columns; _blocks = blocks; _arena = arena;
        _defaultValue = defaultValue; _defaultFilled = defaultFilled; MissingCount = missingCount;
    }

    internal MatrixInputStorage Snapshot()
    {
        lock (_gate)
        {
            CheckAlive();
            var arena = _arena.Fork();
            InputBlockMap? blocks = null;
            try
            {
                blocks = _blocks.Snapshot();
                return new(Rows, Columns, blocks, arena, _defaultValue, _defaultFilled, MissingCount);
            }
            catch { blocks?.Dispose(); arena.Dispose(); throw; }
        }
    }

    internal long GetValue(int index)
    {
        lock (_gate)
        {
            CheckAlive();
            if (!_blocks.TryGetValue(index / BlockCells, out var block)) return _defaultValue;
            return block.IsConstant ? block.Value : block.Arena!.Read(block).Values[index % BlockCells];
        }
    }

    internal bool IsFilled(int index)
    {
        lock (_gate)
        {
            CheckAlive();
            if (!_blocks.TryGetValue(index / BlockCells, out var block)) return _defaultFilled;
            if (block.FilledCount == block.Count) return true;
            if (block.FilledCount == 0) return false;
            var bits = block.Arena!.Read(block).Filled!;
            return (bits[(index % BlockCells) / 64] & (1UL << (index % 64))) != 0;
        }
    }

    internal void CopyTo(int index, Span<long> destination)
    {
        lock (_gate)
        {
            CheckAlive();
            while (!destination.IsEmpty)
            {
                int offset = index % BlockCells;
                int count = Math.Min(destination.Length, BlockCells - offset);
                if (!_blocks.TryGetValue(index / BlockCells, out var block)) destination[..count].Fill(_defaultValue);
                else if (block.IsConstant) destination[..count].Fill(block.Value);
                else block.Arena!.Read(block).Values.AsSpan(offset, count).CopyTo(destination);
                index += count; destination = destination[count..];
            }
        }
    }

    internal bool TryGetUniformValue(out long value)
    {
        lock (_gate)
        {
            CheckAlive(); value = _defaultValue;
            if (_blocks.Count == 0) return true;
            // A fully covered map has no implicit default cells.
            if (_blocks.Count == (CellCount + BlockCells - 1) / BlockCells) value = _blocks.Values.First().Value;
            foreach (var block in _blocks.Values)
                if (!block.IsConstant || block.Value != value) { value = 0; return false; }
            return true;
        }
    }

    internal void Fill(long? value)
    {
        lock (_gate)
        {
            CheckAlive();
            // Detach from old file so a full replacement releases all old blocks at once.
            var blocks = new InputBlockMap();
            var arena = new InputArena(_arena.DirectoryPath, _arena.CacheLimitBytes);
            var previous = _arena;
            _arena = arena;
            _blocks.Dispose(); _blocks = blocks;
            _defaultValue = value ?? 0; _defaultFilled = value.HasValue;
            MissingCount = value.HasValue ? 0 : CellCount;
            previous.Dispose();
        }
    }

    internal void SetCell(int index, long? value)
    {
        lock (_gate)
        {
            CheckAlive();
            if (IsFilled(index) == value.HasValue && (!value.HasValue || GetValue(index) == value.Value)) return;
            EditBlock(index / BlockCells, (values, bits, start, count) =>
            {
                int offset = index - start;
                values[offset] = value ?? 0;
                SetBit(bits, offset, value.HasValue);
            });
        }
    }

    internal void SetConstantBlock(int key, long? value)
    {
        lock (_gate)
        {
            CheckAlive();
            int count = Math.Min(BlockCells, CellCount - key * BlockCells);
            int previousFilled = _blocks.TryGetValue(key, out var previous) ? previous.FilledCount : _defaultFilled ? count : 0;
            int filled = value.HasValue ? count : 0;
            _blocks.Set(key, new(0, count, filled, value ?? 0, true));
            MissingCount += previousFilled - filled;
        }
    }

    internal delegate void BlockEdit(long[] values, ulong[] bits, int start, int count);
    internal void EditBlock(int key, BlockEdit edit)
    {
        lock (_gate)
        {
            CheckAlive();
            int start = key * BlockCells, count = Math.Min(BlockCells, CellCount - start);
            long[] values = new long[count];
            ulong[] bits = new ulong[(count + 63) / 64];
            int previousFilled;
            if (_blocks.TryGetValue(key, out var block))
            {
                previousFilled = block.FilledCount;
                if (block.IsConstant) Array.Fill(values, block.Value);
                else block.Arena!.Read(block).Values.CopyTo(values, 0);
                if (previousFilled == count) Array.Fill(bits, ulong.MaxValue);
                else if (previousFilled > 0) block.Arena!.Read(block).Filled!.CopyTo(bits, 0);
            }
            else
            {
                Array.Fill(values, _defaultValue);
                previousFilled = _defaultFilled ? count : 0;
                if (_defaultFilled) Array.Fill(bits, ulong.MaxValue);
            }
            edit(values, bits, start, count);
            var replacement = _arena.Write(values, bits);
            _blocks.Set(key, replacement);
            MissingCount += previousFilled - replacement.FilledCount;
        }
    }

    internal void FillMissing(CancellationToken token)
    {
        lock (_gate)
        {
            CheckAlive(); token.ThrowIfCancellationRequested();
            if (MissingCount == 0) return;
            // All missing values are stored as zero. Occupancy can be replaced without scanning implicit cells.
            foreach (int key in _blocks.Keys.ToArray())
            {
                token.ThrowIfCancellationRequested();
                if (_blocks[key].FilledCount == _blocks[key].Count) continue;
                EditBlock(key, (_, bits, _, _) => Array.Fill(bits, ulong.MaxValue));
            }
            token.ThrowIfCancellationRequested();
            _defaultFilled = true; MissingCount = 0;
        }
    }

    internal static void SetBit(ulong[] bits, int offset, bool filled)
    {
        ulong mask = 1UL << (offset % 64);
        if (filled) bits[offset / 64] |= mask; else bits[offset / 64] &= ~mask;
    }
    private void CheckAlive() => ObjectDisposedException.ThrowIf(_disposed, this);
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try { _blocks.Dispose(); }
            finally { _arena.Dispose(); }
        }
    }
}

internal readonly record struct InputBlock(long Offset, int Count, int FilledCount, long Value, bool IsConstant, InputArena? Arena = null);
internal sealed record InputPage(long[] Values, ulong[]? Filled)
{
    internal long Bytes => Values.LongLength * sizeof(long) + (Filled?.LongLength ?? 0) * sizeof(ulong);
}


// One cache is shared by a snapshot family, so ancestry never multiplies its RAM budget.
internal sealed class InputCache(long limit)
{
    private readonly object _gate = new();
    private readonly Dictionary<(InputArena Arena, long Offset), LinkedListNode<(InputArena Arena, long Offset, InputPage Page)>> _entries = new();
    private readonly LinkedList<(InputArena Arena, long Offset, InputPage Page)> _lru = new();
    private long _bytes;
    internal long Limit => limit;
    internal long Bytes { get { lock (_gate) return _bytes; } }
    internal InputPage? Get(InputArena arena, long offset)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue((arena, offset), out var node)) return null;
            _lru.Remove(node); _lru.AddFirst(node); return node.Value.Page;
        }
    }
    internal void Add(InputArena arena, long offset, InputPage page)
    {
        lock (_gate)
        {
            if (page.Bytes > limit) return;
            while (_bytes + page.Bytes > limit) Remove(_lru.Last!);
            var node = new LinkedListNode<(InputArena Arena, long Offset, InputPage Page)>((arena, offset, page));
            _entries.Add((arena, offset), node);
            _lru.AddFirst(node); _bytes += page.Bytes;
        }
    }
    private void Remove(LinkedListNode<(InputArena Arena, long Offset, InputPage Page)> node)
    {
        _entries.Remove((node.Value.Arena, node.Value.Offset));
        _bytes -= node.Value.Page.Bytes; _lru.Remove(node);
    }
    internal void RemoveArena(InputArena arena)
    {
        lock (_gate)
        {
            var node = _lru.First;
            while (node is not null)
            {
                var next = node.Next;
                if (ReferenceEquals(node.Value.Arena, arena)) Remove(node);
                node = next;
            }
        }
    }
}

internal sealed class InputArena : IDisposable
{
    private readonly object _gate = new();
    private readonly InputCache _cache;
    private FileStream? _file;
    private long _committedBytes;
    private int _references = 1;
    internal string DirectoryPath { get; }
    internal long CacheLimitBytes => _cache.Limit;
    internal long CachedBytes => _cache.Bytes;
    internal long StoredBytes { get { lock (_gate) return _file?.Length ?? 0; } }
    internal bool IsFileBacked => StoredBytes > 0;
    internal string? FilePath { get { lock (_gate) return _file?.Name; } }
    internal InputArena(string? directory, long cacheLimitBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cacheLimitBytes);
        DirectoryPath = directory ?? Path.GetTempPath(); _cache = new InputCache(cacheLimitBytes);
    }
    private InputArena(InputArena parent)
    {
        DirectoryPath = parent.DirectoryPath; _cache = parent._cache;
    }
    internal InputArena Fork() => new(this);
    internal void AddReference()
    {
        lock (_gate) { ObjectDisposedException.ThrowIf(_references == 0, this); _references++; }
    }
    internal InputBlock Write(ReadOnlySpan<long> values, ReadOnlySpan<ulong> bits = default)
    {
        int filled = bits.IsEmpty ? values.Length : 0;
        bool uniform = true;
        for (int i = 0; i < values.Length; i++)
        {
            if (!bits.IsEmpty && (bits[i / 64] & (1UL << (i % 64))) != 0) filled++;
            if (values[i] != values[0]) uniform = false;
        }
        if (uniform && (filled == 0 || filled == values.Length)) return new(0, values.Length, filled, values[0], true);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_references == 0, this);
            if (_file is null)
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = Path.Combine(DirectoryPath, $"matrix-input-{Guid.NewGuid():N}.bin");
                _file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete,
                    1, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
            }
            // Retry a failed partial append at the same position. Existing immutable
            // blocks never overlap the uncommitted tail and remain readable.
            long offset = _committedBytes;
            _file.Position = offset;
            _file.Write(MemoryMarshal.AsBytes(values));
            if (filled > 0 && filled < values.Length) _file.Write(MemoryMarshal.AsBytes(bits));
            _committedBytes = _file.Position;
            return new(offset, values.Length, filled, values[0], false, this);
        }
    }
    internal InputPage Read(InputBlock block)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_references == 0, this);
            if (_cache.Get(this, block.Offset) is { } cached) return cached;
            var values = new long[block.Count];
            ulong[]? bits = block.FilledCount > 0 && block.FilledCount < block.Count ? new ulong[(block.Count + 63) / 64] : null;
            _file!.Position = block.Offset;
            _file.ReadExactly(MemoryMarshal.AsBytes(values.AsSpan()));
            if (bits is not null) _file.ReadExactly(MemoryMarshal.AsBytes(bits.AsSpan()));
            var page = new InputPage(values, bits); _cache.Add(this, block.Offset, page);
            return page;
        }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_references == 0 || --_references != 0) return;
            _cache.RemoveArena(this); _file?.Dispose(); _file = null;
        }
    }
}
