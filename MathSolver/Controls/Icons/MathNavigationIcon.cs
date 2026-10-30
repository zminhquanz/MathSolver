using System.Globalization;
using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>
/// Reuses the existing native Button, keeping focus, scrolling and measurement intact.
/// FontImageSource renders the SVG outlines as vectors and follows exact text color.
/// </summary>
public static class MathNavigationIcon
{
    public static readonly BindableProperty KeyProperty = BindableProperty.CreateAttached(
        "Key", typeof(string), typeof(MathNavigationIcon), null,
        propertyChanged: OnKeyChanged);

    public static string? GetKey(BindableObject target) => (string?)target.GetValue(KeyProperty);
    public static void SetKey(BindableObject target, string? key) => target.SetValue(KeyProperty, key);

    private static void OnKeyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Button button) return;
        button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 8);
        button.SetBinding(Button.ImageSourceProperty, new Binding(nameof(Button.TextColor),
            source: button, converter: new NavigationImageConverter((string?)newValue)));
    }

    private sealed class NavigationImageConverter(string? key) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string? glyph = MathNavigationCatalog.GlyphFor(key);
            return glyph is null ? null : new NavigationFontImageSource
            {
                FontFamily = MathNavigationCatalog.FontAlias,
                Glyph = glyph,
                Size = MathNavigationCatalog.IconSize,
                FontAutoScalingEnabled = false,
                Color = value is Color color ? color : Colors.Black
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
