using CommunityToolkit.Maui;
using MathSolver.Controls;
using MathSolver.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace MathSolver
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            ConfigureNativeInputChrome();

            ButtonHandler.Mapper.AppendToMapping(
                "InteractivePressAnimation",
                static (_, view) =>
                {
                    if (view is Button button)
                    {
                        InteractiveButtonAnimation.Attach(button);
                    }
                });

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    // Toàn bộ font được quản lý tại AppFontCatalog.
                    // Khi thêm font mới, không cần sửa MauiProgram.
                    AppFontCatalog.RegisterFonts(fonts);
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        /// <summary>
        /// Giữ Entry/Picker là control chuẩn của .NET MAUI. Các input được
        /// Border trong XAML bao quanh sẽ loại bỏ native border/background để
        /// Border là lớp duy nhất vẽ nền, stroke và bo góc; riêng native picker
        /// indicator/chevron vẫn do platform control tự vẽ.
        ///
        /// Handler mapper là global nên không cần custom Entry/Picker subclass.
        /// </summary>
        private static void ConfigureNativeInputChrome()
        {
            EntryHandler.Mapper.AppendToMapping(
                "MathSolverBorderHostedEntry",
                static (handler, _) =>
                {
#if WINDOWS
                    var textBox = handler.PlatformView;
                    var transparentBrush =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Transparent);

                    textBox.BorderThickness =
                        new Microsoft.UI.Xaml.Thickness(0);
                    textBox.BorderBrush = transparentBrush;
                    textBox.Background = transparentBrush;
                    textBox.Padding = new Microsoft.UI.Xaml.Thickness(0);
                    textBox.UseSystemFocusVisuals = false;

                    // WinUI có thể gán lại brush ở PointerOver/Focused/Disabled.
                    // Neutralize toàn bộ VisualState để native TextBox không vẽ
                    // thêm một lớp viền bên trong Border của MAUI.
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
                    var editText = handler.PlatformView;
                    editText.Background = null;
                    editText.SetPadding(0, 0, 0, 0);
#elif IOS || MACCATALYST
                    var textField = handler.PlatformView;
                    textField.BorderStyle = UIKit.UITextBorderStyle.None;
                    textField.BackgroundColor = UIKit.UIColor.Clear;
#endif
                });

            PickerHandler.Mapper.AppendToMapping(
                "MathSolverBorderHostedPicker",
                static (handler, view) =>
                {
#if WINDOWS
                    var comboBox = handler.PlatformView;
                    var transparentBrush =
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Transparent);

                    comboBox.BorderThickness =
                        new Microsoft.UI.Xaml.Thickness(0);
                    comboBox.BorderBrush = transparentBrush;
                    comboBox.Background = transparentBrush;
                    // Không chỉnh Padding, HorizontalContentAlignment, template
                    // hay vùng drop-down button của ComboBox. Các giá trị native này
                    // chịu trách nhiệm bố trí chevron chuẩn WinUI ở mép phải.
                    // Chỉ neutralize background/border để Border MAUI vẽ hình dạng.
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

                    // Border/background native được vô hiệu hóa, nhưng glyph xổ
                    // xuống phải luôn còn nhìn thấy. Đặt rõ màu cho các WinUI glyph
                    // resource để chevron không bị mất khi native chrome trong suốt; dùng màu
                    // theo Picker.TextColor để WinUI ComboBox giữ chevron native.
                    if (view is Microsoft.Maui.Controls.Picker picker)
                    {
                        ApplyWindowsPickerGlyphColor(
                            comboBox,
                            picker.TextColor);
                    }
#elif ANDROID
                    var editText = handler.PlatformView;
                    editText.Background = null;
                    editText.SetPadding(0, 0, 0, 0);
#elif IOS || MACCATALYST
                    var textField = handler.PlatformView;
                    textField.BorderStyle = UIKit.UITextBorderStyle.None;
                    textField.BackgroundColor = UIKit.UIColor.Clear;
#endif
                });

            // Khi DynamicResource đổi TextColor (ví dụ đổi Light/Dark ngay khi
            // menu đang mở), cập nhật lại chevron native cùng lúc.
            PickerHandler.Mapper.AppendToMapping(
                nameof(Microsoft.Maui.Controls.Picker.TextColor),
                static (handler, view) =>
                {
#if WINDOWS
                    if (view is Microsoft.Maui.Controls.Picker picker)
                    {
                        ApplyWindowsPickerGlyphColor(
                            handler.PlatformView,
                            picker.TextColor);
                    }
#endif
                });
        }

#if WINDOWS
        private static void ApplyWindowsPickerGlyphColor(
            Microsoft.UI.Xaml.Controls.ComboBox comboBox,
            Microsoft.Maui.Graphics.Color color)
        {
            static byte ToByte(double value) =>
                (byte)Math.Clamp(
                    (int)Math.Round(value * 255d),
                    0,
                    255);

            var nativeColor =
                global::Windows.UI.Color.FromArgb(
                    ToByte(color.Alpha),
                    ToByte(color.Red),
                    ToByte(color.Green),
                    ToByte(color.Blue));

            var glyphBrush =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    nativeColor);

            comboBox.Resources["ComboBoxDropDownGlyphForeground"] =
                glyphBrush;
            comboBox.Resources["ComboBoxDropDownGlyphForegroundFocused"] =
                glyphBrush;
            comboBox.Resources["ComboBoxDropDownGlyphForegroundFocusedPressed"] =
                glyphBrush;
            comboBox.Resources["ComboBoxDropDownGlyphForegroundDisabled"] =
                glyphBrush;
            comboBox.Resources["ComboBoxEditableDropDownGlyphForeground"] =
                glyphBrush;
        }
#endif
    }
}
