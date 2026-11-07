using System.Globalization;
using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>Shared action artwork on native buttons; preserve handlers, focus and text bindings.</summary>
public static class ActionIcon
{
    public static readonly BindableProperty KeyProperty = BindableProperty.CreateAttached(
        "Key", typeof(string), typeof(ActionIcon), null,
        propertyChanged: static (target, _, key) =>
        {
            if (target is not Button button) return;
            button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 8);
            button.SetBinding(Button.ImageSourceProperty, new Binding(nameof(Button.TextColor), source: button,
                converter: new ImageConverter((string?)key)));
        });
    public static string? GetKey(BindableObject target) => (string?)target.GetValue(KeyProperty);
    public static void SetKey(BindableObject target, string? key) => target.SetValue(KeyProperty, key);

    internal static NavigationFontImageSource? Source(string? key, Color color, double size) =>
        ActionIconCatalog.GlyphFor(key) is { } glyph ? new NavigationFontImageSource
        {
            FontFamily = ActionIconCatalog.FontFor(key), Glyph = glyph, Size = size,
            FontAutoScalingEnabled = false, Color = color
        } : null;

    private sealed class ImageConverter(string? key) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Source(key, value as Color ?? Colors.Black, ActionIconCatalog.ActionSize);
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
