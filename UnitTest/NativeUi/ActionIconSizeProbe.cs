using MathSolver.Controls;
using MathSolver.Services;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace MathSolver;

// Opt-in real Win2D render check. Never compiled into the production application.
public partial class App
{
    protected override void OnStart()
    {
        base.OnStart();
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), RunActionIconSizeProbe);
    }

    private async void RunActionIconSizeProbe()
    {
        string log = Path.Combine(AppContext.BaseDirectory, "action-icon-size.log");
        try
        {
            File.WriteAllText(log, "START ACTION ICON SIZE CHECKS\n");
            var renderer = new MathSolver.Platforms.Windows.NavigationFontImageSourceService();
            foreach (double size in new[] { 20d, 24d })
            foreach (float density in new[] { 1f, 1.5f, 2f, 3f })
            {
                using var result = await renderer.GetImageSourceAsync(new NavigationFontImageSource
                {
                    FontFamily = MathNavigationCatalog.FontAlias, Glyph = "\ue90c",
                    Size = size, Color = Colors.Black
                }, density);
                if (result?.Value is not CanvasImageSource image
                    || image.Size.Width != size || image.Size.Height != size
                    || Math.Abs(image.Dpi - 96 * density) > .1 || !result.IsResolutionDependent)
                    throw new InvalidOperationException($"Expected logical {size}x{size} at {density * 100}% DPI; actual: "
                        + (result?.Value is CanvasImageSource actual ? $"{actual.Size}, {actual.Dpi} DPI" : "missing canvas"));
                File.AppendAllText(log, $"PASS {size}x{size}, {density * 100}% DPI\n");
            }
            File.AppendAllText(log, "PASS ALL ACTION ICON SIZE CHECKS\n");
        }
        catch (Exception error) { File.AppendAllText(log, "FAIL " + error + "\n"); }
        finally { Application.Current!.CloseWindow(Windows[0]); }
    }
}
