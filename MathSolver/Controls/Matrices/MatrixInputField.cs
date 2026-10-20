using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace MathSolver.Controls;

/// <summary>Shared construction for matrix dimensions, coordinates and cell values.</summary>
internal static class MatrixInputField
{
    internal static Entry CreateEntry(string text)
    {
        var entry = new Entry { Text = text, Keyboard = Keyboard.Numeric, MinimumHeightRequest = 46,
            IsSpellCheckEnabled = false, IsTextPredictionEnabled = false, MaxLength = 4,
            BackgroundColor = Colors.Transparent };
        entry.SetDynamicResource(Entry.TextColorProperty, "WallpaperTextPrimaryColor");
        entry.SetDynamicResource(Entry.PlaceholderColorProperty, "WallpaperTextSecondaryColor");
        return entry;
    }

    internal static Border Wrap(Entry entry)
    {
        // Match the other solver inputs: WinUI's TextBox keeps transparent
        // native chrome, and the MAUI Border owns theme-dependent paint.
        // Binding Entry.BackgroundColor to the palette re-enters WinUI's
        // TextControlBackground resources during its theme/template transition.
        var host = new Border
        {
            Content = entry, Padding = new Thickness(12, 4),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }
        };
        host.SetDynamicResource(Border.BackgroundColorProperty, "WallpaperSurfaceAltColor");
        host.SetDynamicResource(Border.StrokeProperty, "WallpaperBorderBrush");
        return host;
    }
}
