using System.IO.Compression;

namespace MathSolver.Services.Artwork;

/// <summary>
/// Reads release artwork without extracting individual images. Cached byte arrays
/// are immutable to callers and bounded independently of the total pack size.
/// </summary>
public sealed class ArtworkProvider
{
    public const string PackFileName = "mathsolver-artwork.pack";
    public const long DefaultCacheLimit = 24L * 1024 * 1024;
    private const long MaximumEntrySize = 8L * 1024 * 1024;

    private readonly string _packPath;
    private readonly long _cacheLimit;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _reads = new(4, 4);
    private readonly Dictionary<string, LinkedListNode<CacheItem>> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<CacheItem> _recent = new();
    private long _cachedBytes;

    public ArtworkProvider(string packPath, long cacheLimit = DefaultCacheLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cacheLimit);
        _packPath = packPath;
        _cacheLimit = cacheLimit;
    }

    public long CachedBytes
    {
        get { lock (_gate) return _cachedBytes; }
    }

    public int CachedEntryCount
    {
        get { lock (_gate) return _cache.Count; }
    }

    public async Task<ArtworkEntry?> GetAsync(string filename, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValidName(filename))
            return null;

        lock (_gate)
        {
            if (_cache.TryGetValue(filename, out var cached))
            {
                _recent.Remove(cached);
                _recent.AddFirst(cached);
                return cached.Value.Entry;
            }
        }

        await _reads.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Recheck after waiting so concurrent image requests share the cache.
            lock (_gate)
            {
                if (_cache.TryGetValue(filename, out var cached))
                {
                    _recent.Remove(cached);
                    _recent.AddFirst(cached);
                    return cached.Value.Entry;
                }
            }

            using var archive = ZipFile.OpenRead(_packPath);
            var zipEntry = archive.GetEntry(filename);
            if (zipEntry is null && filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                zipEntry = archive.GetEntry(Path.ChangeExtension(filename, ".svg"));

            if (zipEntry is null)
                return null;
            if (zipEntry.Length > MaximumEntrySize)
                throw new InvalidDataException($"Artwork entry exceeds {MaximumEntrySize} bytes: {zipEntry.Name}");

            using var input = zipEntry.Open();
            using var output = new MemoryStream((int)zipEntry.Length);
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
            {
                if (output.Length + read > MaximumEntrySize)
                    throw new InvalidDataException($"Artwork entry exceeds {MaximumEntrySize} bytes: {zipEntry.Name}");

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            if (output.Length != zipEntry.Length)
                throw new InvalidDataException($"Incomplete artwork entry: {zipEntry.Name}");

            var result = new ArtworkEntry(zipEntry.Name, output.ToArray());
            if (result.Bytes.Length <= _cacheLimit)
            {
                lock (_gate)
                {
                    if (!_cache.ContainsKey(filename))
                    {
                        while (_cachedBytes + result.Bytes.Length > _cacheLimit)
                        {
                            var last = _recent.Last!;
                            _cache.Remove(last.Value.Key);
                            _cachedBytes -= last.Value.Entry.Bytes.Length;
                            _recent.RemoveLast();
                        }

                        var node = _recent.AddFirst(new CacheItem(filename, result));
                        _cache.Add(filename, node);
                        _cachedBytes += result.Bytes.Length;
                    }
                }
            }

            return result;
        }
        finally
        {
            _reads.Release();
        }
    }

    private static bool IsValidName(string? filename) =>
        !string.IsNullOrWhiteSpace(filename) &&
        filename == Path.GetFileName(filename) &&
        filename.IndexOfAny(['/', '\\', ':']) < 0 &&
        (filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
         filename.EndsWith(".svg", StringComparison.OrdinalIgnoreCase));

    private sealed record CacheItem(string Key, ArtworkEntry Entry);
}

public sealed record ArtworkEntry(string Name, byte[] Bytes)
{
    public bool IsSvg => Name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
}
