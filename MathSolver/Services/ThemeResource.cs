using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MathSolver.Services;

public static class ThemeResource
{
    public static Color GetColor(
        string resourceKey,
        string fallbackHex)
    {
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
