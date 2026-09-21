namespace MathSolver.Services
{
    public enum AppLanguage { English }
    public static class AppLanguageManager { public static AppLanguage CurrentLanguage => AppLanguage.English; }
}
namespace Microsoft.Maui.Storage
{
    public class Preferences
    {
        public static Preferences Default { get; } = new();
        private readonly Dictionary<string, object> values = new();
        public T Get<T>(string key, T fallback) => values.TryGetValue(key, out var value) ? (T)value : fallback;
        public void Set<T>(string key, T value) => values[key] = value!;
    }
}
