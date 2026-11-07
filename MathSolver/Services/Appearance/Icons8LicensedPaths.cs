namespace MathSolver.Services;

/// <summary>The generated implementation is a local licensed build input; public builds use originals.</summary>
internal static partial class Icons8LicensedPaths
{
    private static readonly Dictionary<string, string> Paths = CreatePaths();
    private static Dictionary<string, string> CreatePaths()
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        AddPaths(paths);
        return paths;
    }

    static partial void AddPaths(Dictionary<string, string> paths);

    public static string? For(string key) => Icons8ActionCatalog.IsAvailable
        && Paths.TryGetValue(key, out string? data) ? data : null;
}
