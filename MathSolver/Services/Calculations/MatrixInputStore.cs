using System.Runtime.InteropServices;
using System.Numerics;

namespace MathSolver.Services.Core;

/// <summary>
/// Owns temporary exact input values and their filled bitmap. Uniform blocks need no buffers.
/// A positive or zero cache request smaller than one block is rounded up to one complete
/// values-plus-bitmap block; estimates and CacheLimitBytes report that normalized reserve.
/// All operations are synchronized and returned values are copied, never borrowed.
/// </summary>
internal sealed class MatrixInputStore : IDisposable
{
    public const int BlockLength = 65536;
    public const long DefaultCacheBytes = 4 * 1024 * 1024;
    private const int MaskLength = BlockLength / 64;
    private const long BlockBytes = BlockLength * sizeof(long) + MaskLength * sizeof(ulong);
    // Conservative reserves cover descriptor arrays, object/array headers and LRU entries.
    private const long StoreOverheadBytes = 512;
    private const long DescriptorBytes = 192;
    private readonly object _gate = new();
    private Descriptor[] _blocks;
    private readonly string? _temporaryDirectory;
    private readonly FileStream _file;
    private Entry? _oldest, _newest;
    private long _cachedBytes, _storedBytes;
    private long _mutationVersion;
    private int _missingCount;
    private bool _disposed;

    private struct Descriptor
    {
        public long Value;
        public long Offset;
        public int Missing;
        public bool Stored;
        public Entry? Cached;
    }

    private sealed class Entry(int blockIndex)
    {
        public int BlockIndex = blockIndex;
        public readonly long[] Values = new long[BlockLength];
        public readonly ulong[] Filled = new ulong[MaskLength];
        public Entry? Previous, Next;
        public bool Dirty;
    }

    public MatrixInputStore(int length, string? temporaryDirectory = null,
        long cacheLimitBytes = DefaultCacheBytes, bool filled = false)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        CacheLimitBytes = NormalizeLimit(cacheLimitBytes);
        Length = length;
        _temporaryDirectory = temporaryDirectory;
        _blocks = new Descriptor[BlockCount(length)];
        _missingCount = filled ? 0 : length;
        for (int block = 0; block < _blocks.Length; block++)
        {
            _blocks[block].Offset = -1;
            _blocks[block].Missing = filled ? 0 : CellsInBlock(block);
        }
        FilePath = Path.Combine(temporaryDirectory ?? Path.GetTempPath(), $"MathSolver-input-{Guid.NewGuid():N}.tmp");
        // No fallible work follows opening the owned handle. DeleteOnClose also covers process exit.
        _file = new FileStream(FilePath, FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
    }

    public int Length { get; }
    public string FilePath { get; }
    public long CacheLimitBytes { get; }
    public int MissingCount { get { lock (_gate) { ThrowIfDisposed(); return _missingCount; } } }
    public long CachedBytes { get { lock (_gate) return _cachedBytes; } }
    // File length also includes a partial failed write; the allocation cursor advances only
    // after a complete block is committed, so failed appends are safely retried in place.
    public long StoredBytes { get { lock (_gate) return _disposed ? 0 : RandomAccess.GetLength(_file.SafeFileHandle); } }
    public long ResidentBytes { get { lock (_gate) return _disposed ? 0 : MetadataBytes(Length) + _cachedBytes; } }

    public static long EstimateResidentBytes(int length, long cacheLimitBytes = DefaultCacheBytes)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        long limit = NormalizeLimit(cacheLimitBytes);
        long cacheBlocks = Math.Min(BlockCount(length), limit / BlockBytes);
        return checked(MetadataBytes(length) + cacheBlocks * BlockBytes);
    }

    public long Read(int index)
    {
        lock (_gate)
        {
            ValidateIndex(index);
            int block = index / BlockLength;
            ref Descriptor descriptor = ref _blocks[block];
            if (descriptor.Cached is null && !descriptor.Stored) return descriptor.Value;
            return GetBlock(block).Values[index % BlockLength];
        }
    }

    public bool IsFilled(int index)
    {
        lock (_gate)
        {
            ValidateIndex(index);
            int block = index / BlockLength;
            ref Descriptor descriptor = ref _blocks[block];
            if (descriptor.Cached is null && !descriptor.Stored) return descriptor.Missing == 0;
            return HasValue(GetBlock(block), index % BlockLength);
        }
    }

    public void CopyValues(int start, Span<long> destination)
    {
        lock (_gate)
        {
            ValidateRange(start, destination.Length);
            while (!destination.IsEmpty)
            {
                int block = start / BlockLength, offset = start % BlockLength;
                int count = Math.Min(destination.Length, CellsInBlock(block) - offset);
                ref Descriptor descriptor = ref _blocks[block];
                if (descriptor.Cached is null && !descriptor.Stored) destination[..count].Fill(descriptor.Value);
                else GetBlock(block).Values.AsSpan(offset, count).CopyTo(destination);
                start += count;
                destination = destination[count..];
            }
        }
    }

    public void Set(int index, long? value)
    {
        lock (_gate)
        {
            ValidateIndex(index);
            SetPartial(index / BlockLength, index % BlockLength, 1, value);
        }
    }

    /// <summary>Copies a bounded input panel and marks every supplied cell filled, including zeros.</summary>
    public void WriteValues(int start, ReadOnlySpan<long> values)
    {
        lock (_gate)
        {
            ValidateRange(start, values.Length);
            while (!values.IsEmpty)
            {
                int block = start / BlockLength, offset = start % BlockLength;
                int count = Math.Min(values.Length, CellsInBlock(block) - offset);
                ReadOnlySpan<long> part = values[..count];
                if (offset == 0 && count == CellsInBlock(block) && part.IndexOfAnyExcept(part[0]) < 0)
                    SetUniform(block, part[0]);
                else
                {
                    Entry entry = GetBlock(block);
                    part.CopyTo(entry.Values.AsSpan(offset, count));
                    int newlyFilled = 0, end = offset + count;
                    for (int index = offset; index < end;)
                    {
                        int bit = index & 63, amount = Math.Min(64 - bit, end - index);
                        ulong mask = (ulong.MaxValue >> (64 - amount)) << bit;
                        newlyFilled += BitOperations.PopCount(mask & ~entry.Filled[index >> 6]);
                        entry.Filled[index >> 6] |= mask;
                        index += amount;
                    }
                    _blocks[block].Missing -= newlyFilled;
                    _missingCount -= newlyFilled;
                    entry.Dirty = true;
                    _mutationVersion++;
                }
                start += count;
                values = values[count..];
            }
        }
    }

    public void Fill(int start, int count, long? value)
    {
        lock (_gate)
        {
            ValidateRange(start, count);
            while (count > 0)
            {
                int block = start / BlockLength, offset = start % BlockLength;
                int amount = Math.Min(count, CellsInBlock(block) - offset);
                if (offset == 0 && amount == CellsInBlock(block)) SetUniform(block, value);
                else SetPartial(block, offset, amount, value);
                start += amount;
                count -= amount;
            }
        }
    }

    public void FillMissingWithZero()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            for (int block = 0; block < _blocks.Length; block++)
            {
                ref Descriptor descriptor = ref _blocks[block];
                if (descriptor.Missing == 0) continue;
                if (descriptor.Missing == CellsInBlock(block)) { SetUniform(block, 0); continue; }
                Entry entry = GetBlock(block);
                // Blank values are always zero; only the packed filled state changes.
                Array.Fill(entry.Filled, ulong.MaxValue);
                entry.Dirty = true;
                _missingCount -= descriptor.Missing;
                descriptor.Missing = 0;
                _mutationVersion++;
            }
        }
    }

    public MatrixInputStore Clone(CancellationToken cancellationToken, IProgress<double>? progress = null)
    {
        long version;
        int blockCount;
        lock (_gate)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            version = _mutationVersion;
            blockCount = _blocks.Length;
        }
        var target = new MatrixInputStore(Length, _temporaryDirectory, CacheLimitBytes);
        try
        {
            for (int block = 0; block < blockCount; block++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    ValidateCloneVersion(version);
                    ref Descriptor descriptor = ref _blocks[block];
                    if (descriptor.Cached is null && !descriptor.Stored)
                        target.SetUniform(block, descriptor.Missing == 0 ? descriptor.Value : null);
                    else
                    {
                        Entry source = GetBlock(block), copy = target.GetBlock(block);
                        source.Values.CopyTo(copy.Values, 0);
                        source.Filled.CopyTo(copy.Filled, 0);
                        copy.Dirty = true;
                        target._missingCount += descriptor.Missing - target._blocks[block].Missing;
                        target._blocks[block].Missing = descriptor.Missing;
                    }
                }
                // UI reads, cancellation and closure can run between bounded block copies.
                // A callback is application code and must never execute under our source lock.
                progress?.Report((double)(block + 1) / blockCount);
            }
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate) ValidateCloneVersion(version);
            return target;
        }
        catch { target.Dispose(); throw; }
    }

    public void Flush()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            for (Entry? entry = _oldest; entry is not null; entry = entry.Next) WriteDirty(entry);
            _file.Flush();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            // Temporary data is discarded, so disposal need not flush dirty entries.
            _blocks = [];
            _oldest = _newest = null;
            _cachedBytes = 0;
            _file.Dispose();
        }
    }

    private Entry GetBlock(int block)
    {
        ref Descriptor descriptor = ref _blocks[block];
        if (descriptor.Cached is { } cached) { Touch(cached); return cached; }
        Entry? reusable = null;
        while (_cachedBytes > CacheLimitBytes - BlockBytes)
        {
            Entry victim = _oldest!;
            // Keep the dirty entry and its arrays owned until the entire write succeeds.
            WriteDirty(victim);
            Remove(victim);
            _blocks[victim.BlockIndex].Cached = null;
            reusable = victim;
        }
        // Recycle the evicted arrays after committing their old contents. Steady-state
        // scans therefore allocate no large arrays, even with a one-block cache.
        var entry = reusable ?? new Entry(block);
        entry.BlockIndex = block;
        if (descriptor.Stored)
        {
            ReadExactly(MemoryMarshal.AsBytes(entry.Values.AsSpan()), descriptor.Offset);
            ReadExactly(MemoryMarshal.AsBytes(entry.Filled.AsSpan()), descriptor.Offset + BlockLength * sizeof(long));
        }
        else
        {
            Array.Fill(entry.Values, descriptor.Value);
            Array.Fill(entry.Filled, descriptor.Missing == 0 ? ulong.MaxValue : 0UL);
        }
        // A failed or partial read never becomes visible in the cache.
        descriptor.Cached = entry;
        Append(entry);
        return entry;
    }

    private void SetPartial(int block, int start, int count, long? value)
    {
        Entry entry = GetBlock(block);
        ref Descriptor descriptor = ref _blocks[block];
        for (int index = start; index < start + count; index++)
        {
            bool wasFilled = HasValue(entry, index);
            if (wasFilled != value.HasValue)
            {
                int change = value.HasValue ? -1 : 1;
                descriptor.Missing += change;
                _missingCount += change;
            }
            entry.Values[index] = value.GetValueOrDefault();
            ulong bit = 1UL << (index & 63);
            if (value.HasValue) entry.Filled[index >> 6] |= bit;
            else entry.Filled[index >> 6] &= ~bit;
        }
        entry.Dirty = true;
        _mutationVersion++;
    }

    private void SetUniform(int block, long? value)
    {
        ref Descriptor descriptor = ref _blocks[block];
        if (descriptor.Cached is { } cached) { Remove(cached); descriptor.Cached = null; }
        int missing = value.HasValue ? 0 : CellsInBlock(block);
        _missingCount += missing - descriptor.Missing;
        descriptor.Missing = missing;
        descriptor.Value = value.GetValueOrDefault();
        descriptor.Stored = false;
        _mutationVersion++;
    }

    private void WriteDirty(Entry entry)
    {
        if (!entry.Dirty) return;
        ref Descriptor descriptor = ref _blocks[entry.BlockIndex];
        long offset = descriptor.Offset >= 0 ? descriptor.Offset : _storedBytes;
        RandomAccess.Write(_file.SafeFileHandle, MemoryMarshal.AsBytes(entry.Values.AsSpan()), offset);
        RandomAccess.Write(_file.SafeFileHandle, MemoryMarshal.AsBytes(entry.Filled.AsSpan()), offset + BlockLength * sizeof(long));
        descriptor.Offset = offset;
        descriptor.Stored = true;
        _storedBytes = Math.Max(_storedBytes, checked(offset + BlockBytes));
        entry.Dirty = false;
    }

    private void ReadExactly(Span<byte> destination, long offset)
    {
        while (!destination.IsEmpty)
        {
            int count = RandomAccess.Read(_file.SafeFileHandle, destination, offset);
            if (count == 0) throw new EndOfStreamException("The temporary matrix input block is incomplete.");
            offset += count;
            destination = destination[count..];
        }
    }

    private void Touch(Entry entry)
    {
        if (ReferenceEquals(entry, _newest)) return;
        Remove(entry);
        Append(entry);
    }

    private void Remove(Entry entry)
    {
        if (entry.Previous is { } previous) previous.Next = entry.Next;
        else _oldest = entry.Next;
        if (entry.Next is { } next) next.Previous = entry.Previous;
        else _newest = entry.Previous;
        entry.Next = entry.Previous = null;
        _cachedBytes -= BlockBytes;
    }

    private void Append(Entry entry)
    {
        entry.Previous = _newest;
        if (_newest is { } previous) previous.Next = entry;
        else _oldest = entry;
        _newest = entry;
        _cachedBytes += BlockBytes;
    }

    private static bool HasValue(Entry entry, int index) => (entry.Filled[index >> 6] & (1UL << (index & 63))) != 0;
    private static int BlockCount(int length) => (int)(((long)length + BlockLength - 1) / BlockLength);
    private int CellsInBlock(int block) => Math.Min(BlockLength, Length - block * BlockLength);
    private static long MetadataBytes(int length) => StoreOverheadBytes + BlockCount(length) * DescriptorBytes;
    private static long NormalizeLimit(long limit) => limit < 0 ? throw new ArgumentOutOfRangeException(nameof(limit)) : Math.Max(BlockBytes, limit);
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private void ValidateCloneVersion(long version)
    {
        ThrowIfDisposed();
        if (_mutationVersion != version)
            throw new InvalidOperationException("The matrix input changed while its snapshot was being copied.");
    }
    private void ValidateIndex(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)Length) throw new ArgumentOutOfRangeException(nameof(index));
    }
    private void ValidateRange(int start, int count)
    {
        ThrowIfDisposed();
        if (start < 0 || count < 0 || start > Length - count) throw new ArgumentOutOfRangeException(nameof(start));
    }
}
