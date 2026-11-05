namespace MathSolver.Services;

/// <summary>Available arranged content in DIPs. Shell, safe-area and IME space already
/// excluded by the owner must never be supplied again as chrome.</summary>
internal readonly record struct WorkspaceLayoutMetrics
{
    private WorkspaceLayoutMetrics(double width, double height, double textScale)
    {
        AvailableWidth = width;
        AvailableHeight = height;
        TextScale = textScale;
    }

    internal double AvailableWidth { get; }
    internal double AvailableHeight { get; }
    internal double TextScale { get; }
    // Keep default(struct) safe as well as factory-created values.
    internal double EffectiveWidth => AvailableWidth / NormalizeScale(TextScale);
    internal double EffectiveHeight => AvailableHeight / NormalizeScale(TextScale);

    internal static WorkspaceLayoutMetrics FromAvailable(double width, double height, double textScale = 1d,
        double horizontalChrome = 0d, double verticalChrome = 0d) => new(
            Math.Max(0d, NormalizeDimension(width) - NormalizeDimension(horizontalChrome)),
            Math.Max(0d, NormalizeDimension(height) - NormalizeDimension(verticalChrome)),
            NormalizeScale(textScale));

    internal static double NormalizeDimension(double value) => double.IsFinite(value) ? Math.Max(0d, value) : 0d;
    internal static double NormalizeScale(double value) => double.IsFinite(value) ? Math.Max(1d, value) : 1d;
}
