using MathSolver.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

// Independent WCAG contrast measurement of the resulting visible colors.
static double Contrast(Color background, Color foreground)
{
    double Light(Color color)
    {
        double Channel(double value) => value > 0.04045 ? Math.Pow((value + 0.055) / 1.055, 2.4) : value / 12.92;
        return 0.2126 * Channel(color.Red) + 0.7152 * Channel(color.Green) + 0.0722 * Channel(color.Blue);
    }
    var values = new[] { Light(background), Light(foreground) };
    return (values.Max() + 0.05) / (values.Min() + 0.05);
}

var samples = new (string Background, string Text)[]
{
    ("#16A34A", "#FFFFFF"), ("#EA580C", "#FFFFFF"), ("#2563EB", "#FFFFFF"),
    ("#E6F5EB", "#15803D"), ("#FEF2F2", "#B91C1C"), ("#DBEAFE", "#1E293B"),
    ("#F8FAFC", "#1E293B"), ("#1E293B", "#F8FAFC"), ("#334155", "#F8FAFC"),
    ("#000000", "#FFFFFF"), ("#FFFFFF", "#000000")
};
int count = 0;
foreach (var (backgroundHex, textHex) in samples)
foreach (bool dark in new[] { false, true })
foreach (bool pressed in new[] { false, true })
{
    Color background = Color.FromArgb(backgroundHex), text = Color.FromArgb(textHex);
    Color result = InteractionColorPalette.Highlight(background, text, Colors.Green, dark, pressed);
    Check(Contrast(result, text) + 1e-6 >= Math.Min(4.5, Contrast(background, text)),
        $"Interaction made text less readable: {backgroundHex}/{textHex}, dark={dark}, pressed={pressed}");
    Check(result.Alpha == background.Alpha, "Interaction changed background opacity.");
    Check(!result.Equals(background), $"No visible feedback: {backgroundHex}/{textHex}, dark={dark}, pressed={pressed}");
    count++;
}
foreach (bool dark in new[] { false, true })
{
    var hover = InteractionColorPalette.Highlight(Colors.Transparent, Colors.White, Colors.Orange, dark, false);
    var pressed = InteractionColorPalette.Highlight(Colors.Transparent, Colors.White, Colors.Orange, dark, true);
    Check(hover.Alpha > 0 && pressed.Alpha > hover.Alpha && pressed.Alpha < 0.3,
        "Icon/menu feedback must be visible and restrained.");
    Check(hover.Red == Colors.Orange.Red && hover.Green == Colors.Orange.Green && hover.Blue == Colors.Orange.Blue,
        "Transparent controls must retain the current accent hue.");
}
Console.WriteLine($"PASS {count} light/dark action, selection and semantic-answer color states, plus icon/menu layers.");
