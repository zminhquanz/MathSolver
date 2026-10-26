using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckReadableColors()
    {
        var black = new ReadableRgb(0, 0, 0);
        var white = new ReadableRgb(1, 1, 1);
        Require(Math.Abs(ReadableColorPolicy.Contrast(black, white) - 21) < 0.00001,
            "Black/white luminance contrast must be 21:1.");
        Require(Math.Abs(ReadableColorPolicy.Contrast(new(1, 0, 0), white) - 3.99848) < 0.0001,
            "Use linearized sRGB rather than channel brightness for contrast.");
        var light = new[] { white, new ReadableRgb(.965, .973, .988), new ReadableRgb(.88, .94, .98) };
        var dark = new[] { new ReadableRgb(.063, .094, .153), new ReadableRgb(.094, .133, .208), new ReadableRgb(.08, .16, .23) };
        foreach (double red in new[] { 0d, .1, .4, .7, 1 })
        foreach (double green in new[] { 0d, .1, .4, .7, 1 })
        foreach (double blue in new[] { 0d, .1, .4, .7, 1 })
        {
            var accent = new ReadableRgb(red, green, blue);
            Require(ReadableColorPolicy.Contrast(ReadableColorPolicy.OnBackground(accent), accent) >= 4.5,
                "Primary action text needs readable contrast for every user accent.");
            foreach (var surfaces in new[] { light, dark })
            {
                var text = ReadableColorPolicy.EnsureText(accent, surfaces);
                Require(surfaces.All(surface => ReadableColorPolicy.Contrast(text, surface) >= 4.5 - .00001),
                    "Accent text must pass on each ordinary, alternate and tinted reading surface.");
                if (surfaces.All(surface => ReadableColorPolicy.Contrast(accent, surface) >= 4.5))
                    Require(text == accent, "Already readable accents must not change.");
            }
        }
        Console.WriteLine("Checked sRGB reference ratios, 125 user accents, both polarities and unchanged readable colors.");
    }
}
