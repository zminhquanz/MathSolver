namespace MathSolver.Testing;

/// <summary>Only a newly created, uniquely named directory is owned and eligible for cleanup.</summary>
public sealed class TestWorkspace : IDisposable
{
    public string DirectoryPath { get; }
    private readonly bool _retain;
    private bool _disposed;

    private TestWorkspace(string root, string name, bool retain)
    {
        if (name.Length == 0 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Use a simple workspace label.", nameof(name));
        string resolvedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        DirectoryPath = Path.GetFullPath(Path.Combine(resolvedRoot, $"{name}-{Guid.NewGuid():N}"));
        if (!string.Equals(Path.GetDirectoryName(DirectoryPath), resolvedRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Workspace must stay directly under its designated root.");
        if (Directory.Exists(DirectoryPath)) throw new IOException("Workspace already exists.");
        Directory.CreateDirectory(DirectoryPath);
        _retain = retain;
    }

    public static TestWorkspace Temporary(string name) => new(Path.GetTempPath(), "MathSolver-" + name, false);
    public static TestWorkspace Artifacts(string name) => new("artifacts/verification", name, true);
    public static TestWorkspace Artifacts(string name, string root) => new(root, name, true);

    public string FilePath(string name)
    {
        if (name != Path.GetFileName(name) || name is "" or "." or "..")
            throw new ArgumentException("Expected a file name, not a path.", nameof(name));
        return Path.Combine(DirectoryPath, name);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_retain || !Directory.Exists(DirectoryPath)) return;
        try { Directory.Delete(DirectoryPath, recursive: true); }
        catch (IOException error) { Console.Error.WriteLine($"Temporary workspace retained: {DirectoryPath}: {error.Message}"); }
        catch (UnauthorizedAccessException error) { Console.Error.WriteLine($"Temporary workspace retained: {DirectoryPath}: {error.Message}"); }
    }
}
