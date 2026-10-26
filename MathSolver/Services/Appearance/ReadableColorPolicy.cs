namespace MathSolver.Services;

internal readonly record struct ReadableRgb(double Red, double Green, double Blue);

/// <summary>Linearized sRGB contrast for opaque reading surfaces, independent of UI/theme APIs.</summary>
internal static class ReadableColorPolicy
{
    private static readonly ReadableRgb Black = new(0, 0, 0), White = new(1, 1, 1);

    internal static double Contrast(ReadableRgb first, ReadableRgb second)
    {
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    internal static ReadableRgb OnBackground(ReadableRgb background) =>
        Contrast(Black, background) >= Contrast(White, background) ? Black : White;

    internal static ReadableRgb EnsureText(ReadableRgb candidate, params ReadableRgb[] backgrounds)
    {
        if (backgrounds.Length == 0) return candidate;
        double Minimum(ReadableRgb color) => backgrounds.Min(background => Contrast(color, background));
        if (Minimum(candidate) >= 4.5) return candidate;
        var target = Minimum(Black) >= Minimum(White) ? Black : White;
        double low = 0, high = 1;
        // Same-polarity content surfaces always admit black or white. Keep the
        // closest readable tint rather than changing the user's accent/background.
        for (int i = 0; i < 24; i++)
        {
            double amount = (low + high) / 2;
            if (Minimum(Mix(candidate, target, amount)) >= 4.5001) high = amount;
            else low = amount;
        }
        return Mix(candidate, target, high);
    }

    private static ReadableRgb Mix(ReadableRgb a, ReadableRgb b, double amount) =>
        new(a.Red + (b.Red - a.Red) * amount, a.Green + (b.Green - a.Green) * amount,
            a.Blue + (b.Blue - a.Blue) * amount);

    private static double Luminance(ReadableRgb color)
    {
        static double Linear(double channel) => channel <= .04045 ? channel / 12.92
            : Math.Pow((channel + .055) / 1.055, 2.4);
        return .2126 * Linear(color.Red) + .7152 * Linear(color.Green) + .0722 * Linear(color.Blue);
    }
}
