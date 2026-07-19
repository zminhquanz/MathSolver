namespace MathSolver.Controls;

/// <summary>
/// Picker không vẽ viền và nền native bên trong Border của MAUI.
/// Border bên ngoài trong XAML chịu trách nhiệm vẽ nền, stroke và bo góc.
/// </summary>
public sealed class BorderlessPicker : Picker
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
            is not Microsoft.UI.Xaml.Controls.ComboBox comboBox)
        {
            return;
        }

        var transparentBrush =
            new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Transparent);

        comboBox.BorderThickness =
            new Microsoft.UI.Xaml.Thickness(0);

        comboBox.BorderBrush =
            transparentBrush;

        comboBox.Background =
            transparentBrush;

        comboBox.Padding =
            new Microsoft.UI.Xaml.Thickness(
                0,
                0,
                30,
                0);

        comboBox.HorizontalAlignment =
            Microsoft.UI.Xaml.HorizontalAlignment.Stretch;

        comboBox.HorizontalContentAlignment =
            Microsoft.UI.Xaml.HorizontalAlignment.Stretch;

        comboBox.MinWidth =
            0;

        comboBox.UseSystemFocusVisuals =
            false;

        // WinUI đổi brush theo trạng thái normal, hover, pressed và focus.
        // Ghi đè các resource này để hai vạch dọc native không xuất hiện lại.
        comboBox.Resources["ComboBoxBackground"] =
            transparentBrush;

        comboBox.Resources["ComboBoxBackgroundPointerOver"] =
            transparentBrush;

        comboBox.Resources["ComboBoxBackgroundPressed"] =
            transparentBrush;

        comboBox.Resources["ComboBoxBackgroundDisabled"] =
            transparentBrush;

        comboBox.Resources["ComboBoxBorderBrush"] =
            transparentBrush;

        comboBox.Resources["ComboBoxBorderBrushPointerOver"] =
            transparentBrush;

        comboBox.Resources["ComboBoxBorderBrushPressed"] =
            transparentBrush;

        comboBox.Resources["ComboBoxBorderBrushDisabled"] =
            transparentBrush;

        comboBox.Resources["ComboBoxDropDownGlyphForeground"] =
            transparentBrush;

        comboBox.Resources["ComboBoxDropDownGlyphForegroundFocused"] =
            transparentBrush;

        comboBox.Resources["ComboBoxDropDownGlyphForegroundFocusedPressed"] =
            transparentBrush;

        comboBox.Resources["ComboBoxDropDownGlyphForegroundDisabled"] =
            transparentBrush;

        comboBox.Resources["ComboBoxEditableDropDownGlyphForeground"] =
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