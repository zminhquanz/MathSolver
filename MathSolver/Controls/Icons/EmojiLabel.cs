namespace MathSolver.Controls;

/// <summary>Decorative Unicode emoji rendered with native colour fonts.</summary>
public sealed class EmojiLabel : Label
{
    public EmojiLabel()
    {
        // System emoji fonts are not bundled font files. Keep the MAUI family
        // unset so FontManager does not look for Segoe UI Emoji.ttf/.otf.
        FontFamily = null;
        FontAttributes = FontAttributes.None;
        InputTransparent = true;
        AutomationProperties.SetIsInAccessibleTree(this, false);
    }

    internal static void ConfigureNativeFont()
    {
#if WINDOWS
        Microsoft.Maui.Handlers.LabelHandler.Mapper.AppendToMapping(
            nameof(Microsoft.Maui.ILabel.Font),
            static (handler, view) =>
            {
                if (view is EmojiLabel && handler.PlatformView is { } textBlock)
                {
                    // Apply after MAUI's font mapping, including font-size changes.
                    // WinUI resolves this installed family without probing app assets.
                    textBlock.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe UI Emoji");
                }
            });
#endif
    }
}
