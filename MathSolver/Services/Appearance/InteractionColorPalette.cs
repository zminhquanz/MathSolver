namespace MathSolver.Services;

/// <summary>Interaction colors retain the existing hue and readable text contrast.</summary>
internal static class InteractionColorPalette
{
    internal static Color Highlight(Color background, Color foreground, Color accent, bool dark, bool pressed)
    {
        var layer = StateLayer(background, foreground, accent, dark, pressed);
        if (background.Alpha < 0.05f)
            return layer;
        return Blend(background, layer, layer.Alpha);
    }

    internal static Color StateLayer(Color background, Color foreground, Color accent, bool dark, bool pressed)
    {
        if (background.Alpha < 0.05f)
            return new Color(accent.Red, accent.Green, accent.Blue, pressed ? 0.20f : 0.12f);
        double requiredContrast = Math.Min(4.5, Contrast(background, foreground));
        // Dark surfaces brighten slightly; saturated buttons retain sufficient
        // contrast with white text, even if that means a smaller state layer.
        foreach (Color target in dark ? new[] { Colors.White, Colors.Black } : new[] { Colors.Black, Colors.White })
        {
            if (background.Red == target.Red && background.Green == target.Green && background.Blue == target.Blue) continue;
            for (float amount = pressed ? 0.16f : 0.09f; amount >= 0.01f; amount -= 0.01f)
            {
                Color result = Blend(background, target, amount);
                float difference = Math.Max(Math.Abs(result.Red - background.Red),
                    Math.Max(Math.Abs(result.Green - background.Green), Math.Abs(result.Blue - background.Blue)));
                if (difference >= 1f / 255 && Contrast(result, foreground) >= requiredContrast)
                    return new Color(target.Red, target.Green, target.Blue, amount);
            }
        }
        return Colors.Transparent;
    }

    private static Color Blend(Color background, Color target, float amount) =>
        new(background.Red + (target.Red - background.Red) * amount,
            background.Green + (target.Green - background.Green) * amount,
            background.Blue + (target.Blue - background.Blue) * amount, background.Alpha);

    internal static double Contrast(Color first, Color second)
    {
        static double Linear(float channel) => channel <= 0.04045f ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        static double Luminance(Color color) => 0.2126 * Linear(color.Red) + 0.7152 * Linear(color.Green) + 0.0722 * Linear(color.Blue);
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
}
