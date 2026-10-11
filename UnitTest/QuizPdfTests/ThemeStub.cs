namespace MathSolver.Services;
public static class ThemeResource
{
    public static Microsoft.Maui.Graphics.Color GetColor(string key, string fallback) => Microsoft.Maui.Graphics.Color.FromArgb(fallback);
    public static IDisposable UsePrintPalette() => new PrintScope();
    private sealed class PrintScope : IDisposable { public void Dispose() { } }
}
