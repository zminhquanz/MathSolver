namespace MathSolver.Controls;

/// <summary>
/// Entry không sử dụng viền và nền mặc định của control native.
/// Border bên ngoài trong XAML sẽ chịu trách nhiệm vẽ nền, stroke
/// và bo góc. Cách này tránh xuất hiện các vạch dọc ở hai đầu Entry
/// trên Windows khi dùng giao diện tối.
/// </summary>
public sealed class BorderlessEntry : Entry
{
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        ApplyPlatformStyle();
    }

    private void ApplyPlatformStyle()
    {
#if WINDOWS
        if (Handler?.PlatformView
            is not Microsoft.UI.Xaml.Controls.TextBox textBox)
        {
            return;
        }

        var transparentBrush =
            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Transparent);

        // Loại bỏ toàn bộ chrome mặc định của WinUI TextBox.
        textBox.BorderThickness =
            new Microsoft.UI.Xaml.Thickness(0);

        textBox.BorderBrush =
            transparentBrush;

        textBox.Background =
            transparentBrush;

        textBox.Padding =
            new Microsoft.UI.Xaml.Thickness(0);

        textBox.UseSystemFocusVisuals =
            false;

        // Các VisualState của TextBox có thể gán lại màu khi rê chuột
        // hoặc focus. Ghi đè resource để viền native không xuất hiện lại.
        textBox.Resources["TextControlBorderBrush"] =
            transparentBrush;

        textBox.Resources["TextControlBorderBrushPointerOver"] =
            transparentBrush;

        textBox.Resources["TextControlBorderBrushFocused"] =
            transparentBrush;

        textBox.Resources["TextControlBorderBrushDisabled"] =
            transparentBrush;

        textBox.Resources["TextControlBackground"] =
            transparentBrush;

        textBox.Resources["TextControlBackgroundPointerOver"] =
            transparentBrush;

        textBox.Resources["TextControlBackgroundFocused"] =
            transparentBrush;

        textBox.Resources["TextControlBackgroundDisabled"] =
            transparentBrush;

#elif ANDROID
        if (Handler?.PlatformView
            is Android.Widget.EditText editText)
        {
            editText.Background =
                null;

            editText.SetPadding(
                0,
                0,
                0,
                0);
        }

#elif IOS || MACCATALYST
        if (Handler?.PlatformView
            is UIKit.UITextField textField)
        {
            textField.BorderStyle =
                UIKit.UITextBorderStyle.None;

            textField.BackgroundColor =
                UIKit.UIColor.Clear;
        }
#endif
    }
}
