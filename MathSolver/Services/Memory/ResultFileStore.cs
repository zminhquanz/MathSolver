namespace MathSolver.Services;

/// <summary>Owns temporary exact-result files, published only after a complete write.</summary>
public sealed class ResultFileStore
{
    private readonly string _directory;

    public ResultFileStore(string directory)
    {
        _directory = Path.GetFullPath(directory);
    }

    public async Task<string> CreateAsync(Action<string, CancellationToken> write,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        string finalPath = Path.Combine(_directory, $"result-{Guid.NewGuid():N}.txt");
        string pendingPath = finalPath + ".pending";
        try
        {
            await Task.Run(() => write(pendingPath, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(pendingPath, finalPath);
            return finalPath;
        }
        catch
        {
            Delete(pendingPath);
            throw;
        }
    }

    public void Delete(string? path)
    {
        if (path is null) return;
        string fullPath = Path.GetFullPath(path);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(fullPath), _directory, comparison) ||
            !Path.GetFileName(fullPath).StartsWith("result-", StringComparison.Ordinal) ||
            !(fullPath.EndsWith(".txt", comparison) || fullPath.EndsWith(".txt.pending", comparison)))
            throw new ArgumentException("Only this store's result files can be deleted.", nameof(path));
        try { File.Delete(fullPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void DeleteAbandonedFiles(DateTime olderThanUtc)
    {
        if (!Directory.Exists(_directory)) return;
        try
        {
            foreach (string path in Directory.EnumerateFiles(_directory, "result-*.txt*"))
            {
                string name = Path.GetFileName(path);
                int suffixLength = name.EndsWith(".txt.pending", StringComparison.Ordinal) ? 12 :
                    name.EndsWith(".txt", StringComparison.Ordinal) ? 4 : 0;
                if (suffixLength == 0 || name.Length != 7 + 32 + suffixLength ||
                    !Guid.TryParseExact(name.AsSpan(7, 32), "N", out _)) continue;
                if (File.GetLastWriteTimeUtc(path) < olderThanUtc) Delete(path);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
