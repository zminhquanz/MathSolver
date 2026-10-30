using MathSolver.Controls;
using MathSolver.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Maui.Platform;

namespace MathSolver.Platforms.Windows;

/// <summary>
/// MAUI's default Windows font-image service forces 96 DPI. Preserve the logical
/// 24-unit canvas but render enough pixels for the requesting window's density.
/// </summary>
public sealed class NavigationFontImageSourceService : ImageSourceService,
    IImageSourceService<NavigationFontImageSource>
{
    public override Task<IImageSourceServiceResult<Microsoft.UI.Xaml.Media.ImageSource>?> GetImageSourceAsync(
        IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (imageSource is not NavigationFontImageSource source || imageSource.IsEmpty)
            return Task.FromResult<IImageSourceServiceResult<Microsoft.UI.Xaml.Media.ImageSource>?>(null);

        float density = float.IsFinite(scale) && scale > 0 ? scale : 1;
        float size = (float)MathNavigationCatalog.IconSize;
        var device = CanvasDevice.GetSharedDevice();
        // MAUI copies registered fonts to the application directory on Windows.
        // A file URI works for both packaged and unpackaged Win2D font loading.
        string family = new Uri(Path.Combine(AppContext.BaseDirectory, "MathNavigationIcons.ttf")).AbsoluteUri
            + "#Math Solver Navigation";
        using var format = new CanvasTextFormat
        {
            FontFamily = family,
            FontSize = size,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center
        };
        using var layout = new CanvasTextLayout(device, source.Glyph, format, size, size);
        var canvas = new CanvasImageSource(device, size, size, 96 * density);
        using (var drawing = canvas.CreateDrawingSession(Microsoft.UI.Colors.Transparent))
        {
            drawing.DrawTextLayout(layout, 0, 0, source.Color.ToWindowsColor());
        }
        return Task.FromResult<IImageSourceServiceResult<Microsoft.UI.Xaml.Media.ImageSource>?>(
            new ImageSourceServiceResult(canvas, true));
    }
}
