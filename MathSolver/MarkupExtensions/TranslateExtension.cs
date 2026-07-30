using MathSolver.Services.Localization;
using Microsoft.Maui.Controls.Xaml;

namespace MathSolver.MarkupExtensions;

/// <summary>
/// Binds XAML text to a stable localization key.
///
/// Example:
/// <Label Text="{localization:Translate Tabs.Solve}" />
/// </summary>
[ContentProperty(
    nameof(Key))]
public sealed class TranslateExtension :
    IMarkupExtension
{
    public string Key { get; set; } =
        string.Empty;

    public object ProvideValue(
        IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(
                Key))
        {
            return string.Empty;
        }

        return new Binding(
            $"[{Key}]")
        {
            Source =
                LocalizationManager.Instance,

            Mode =
                BindingMode.OneWay
        };
    }
}
