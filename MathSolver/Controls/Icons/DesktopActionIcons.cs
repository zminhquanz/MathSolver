using System.Globalization;
using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>Utility-action identities; captions stay localized separately from the SVG outlines.</summary>
internal static class DesktopActionIcons
{
    internal static string? GlyphFor(string? key) => key switch
    {
        "Workspace.Copy" => "\ue90c",
        "Workspace.Paste" or "Paste" or "PasteCells" => "\ue90d",
        "Workspace.Clear" => "\ue90e",
        "Workspace.ExpandTable" => "\ue90f",
        "Workspace.RestoreTable" => "\ue910",
        "Workspace.MoreTools" or "Workspace.FewerTools" => "\ue911",
        "TechnicalDetails" or "Workspace.Details" => "\ue912",
        "Import" => "\ue913",
        "Export" => "\ue914",
        "Go" => "\ue915",
        "Workspace.EditSetup" or "Workspace.HideSetup" => "\ue918",
        "Previous" or "Workspace.CloseDetails" => "\ue916",
        "Next" => "\ue917",
        _ => null // Mathematical operation signs and deliberately retained emoji.
    };

    internal sealed class ImageConverter(string glyph) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            new NavigationFontImageSource { FontFamily = MathNavigationCatalog.FontAlias, Glyph = glyph,
                Size = 20, FontAutoScalingEnabled = false, Color = value is Color color ? color : Colors.Black };
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
