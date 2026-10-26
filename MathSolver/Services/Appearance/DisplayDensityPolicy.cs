namespace MathSolver.Services;

public enum DisplayDensityMode { Automatic, Compact, Touch }

internal readonly record struct DisplayDensityMetrics(
    double ControlHeight, double HorizontalPadding, double VerticalPadding,
    double CardPadding, double PagePadding, double Spacing,
    double MatrixColumnWidth, double MatrixRowHeight);

internal static class DisplayDensityPolicy
{
    internal static DisplayDensityMode Parse(string? value) =>
        Enum.TryParse<DisplayDensityMode>(value, true, out var mode) && Enum.IsDefined(mode)
            ? mode : DisplayDensityMode.Automatic;

    internal static DisplayDensityMode Resolve(DisplayDensityMode requested, bool? touchPresent) =>
        requested is DisplayDensityMode.Compact or DisplayDensityMode.Touch ? requested
            : touchPresent == false ? DisplayDensityMode.Compact : DisplayDensityMode.Touch;

    internal static DisplayDensityMetrics Metrics(DisplayDensityMode effective) => effective == DisplayDensityMode.Compact
        ? new(40, 12, 6, 12, 16, 8, 96, 36)
        : new(48, 16, 10, 16, 20, 12, 112, 48);
}
