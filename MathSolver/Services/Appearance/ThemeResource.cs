using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MathSolver.Services;

public static class ThemeResource
{
    private static readonly AsyncLocal<bool> Printing = new();
    /// <summary>Use deterministic light defaults in a background print operation, without reading UI resources.</summary>
    public static IDisposable UsePrintPalette()
    {
        bool previous = Printing.Value;
        Printing.Value = true;
        return new PrintPaletteScope(previous);
    }
    private sealed class PrintPaletteScope(bool previous) : IDisposable
    {
        public void Dispose() => Printing.Value = previous;
    }
    public static Color GetColor(
        string resourceKey,
        string fallbackHex)
    {
        if (Printing.Value) return Color.FromArgb(fallbackHex);
        if (Application.Current?.Resources.TryGetValue(
                resourceKey,
                out object? value) == true)
        {
            if (value is Color color)
            {
                return color;
            }

            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }
        }

        return Color.FromArgb(fallbackHex);
    }
}
