// Platform persistence is replaced only at the MAUI boundary. Tests execute the
// real shared threading manager and matrix engine without changing OS settings.
namespace Microsoft.Maui.Storage;

internal static class Preferences
{
    internal static MemoryPreferences Default { get; } = new();
}

internal sealed class MemoryPreferences
{
    private readonly Dictionary<string, bool> _values = new();
    public bool Get(string key, bool defaultValue) => _values.GetValueOrDefault(key, defaultValue);
    public void Set(string key, bool value) => _values[key] = value;
}
