namespace MathSolver.Controls;

/// <summary>Small decorative stationery artwork with accessible, localized native content.</summary>
public sealed class IllustratedMessage : VerticalStackLayout
{
    public IllustratedMessage(string asset, View message, double imageWidth = 120, double imageHeight = 80)
    {
        Spacing = 12;
        var image = new Image
        {
            Source = asset, Aspect = Aspect.AspectFit,
            WidthRequest = imageWidth, HeightRequest = imageHeight,
            HorizontalOptions = LayoutOptions.Center, InputTransparent = true,
            AutomationId = Path.GetFileNameWithoutExtension(asset)
        };
        AutomationProperties.SetIsInAccessibleTree(image, false);
        Children.Add(image);
        Children.Add(message);
    }
}
