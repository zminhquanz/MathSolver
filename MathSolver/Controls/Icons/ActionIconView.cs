using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>Decorative heading/overlay view using the same vector source as buttons.</summary>
public class ActionIconView : Image
{
    public static readonly BindableProperty KeyProperty = BindableProperty.Create(nameof(Key), typeof(string),
        typeof(ActionIconView), null, propertyChanged: static (target, _, _) => ((ActionIconView)target).Refresh());
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color),
        typeof(ActionIconView), Colors.Black, propertyChanged: static (target, _, _) => ((ActionIconView)target).Refresh());
    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(nameof(IconSize), typeof(double),
        typeof(ActionIconView), ActionIconCatalog.NavigationSize,
        propertyChanged: static (target, _, _) => ((ActionIconView)target).Refresh());
    public string? Key { get => (string?)GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public double IconSize { get => (double)GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public ActionIconView()
    {
        InputTransparent = true;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        SetDynamicResource(ColorProperty, "TextPrimaryColor");
    }
    private void Refresh()
    {
        WidthRequest = HeightRequest = IconSize;
        Source = ActionIcon.Source(Key, Color, IconSize);
    }
}
