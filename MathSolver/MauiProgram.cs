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

#if ANDROID
            ConfigureAndroidMaterial3Phase3(builder);
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

#if ANDROID
        /// <summary>
        /// .NET MAUI 10 exposes Material 3 as an Android app-wide feature flag.
        /// Phase 3 keeps the native Material controls and Shell navigation from
        /// Phases 1/2, then layers Material You color, typography, surface,
        /// elevation, ripple/state and shape tokens over Android application UI.
        /// Math/SGK renderers remain shared and intentionally unchanged.
        ///
        /// Legacy handlers remain registered for controls outside the migration
        /// boundary. In particular Entry must stay on EntryHandler because its
        /// Android EmojiCompat workaround below prevents the 1000 -> 1,000 input
        /// crash. These registrations are Android-only; WinUI is unchanged.
        /// </summary>
        private static void ConfigureAndroidMaterial3Phase3(
            MauiAppBuilder builder)
        {
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<Microsoft.Maui.Controls.Label, LabelHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.Entry, EntryHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.Editor, EditorHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.SearchBar, SearchBarHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.RadioButton, RadioButtonHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.DatePicker, DatePickerHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.TimePicker, TimePickerHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.ActivityIndicator, ActivityIndicatorHandler>();
                handlers.AddHandler<Microsoft.Maui.Controls.Image, ImageHandler>();

                // Deliberately NOT overridden in Material Phase 1/2:
                // Picker       -> PickerHandler2 (Material 3)
                // Switch       -> SwitchHandler2 (Material 3)
                // ProgressBar  -> ProgressBarHandler2 (Material 3)
                // Slider       -> SliderHandler2 (Material 3)
                // Button       -> MaterialButton via ButtonHandler
                // ImageButton  -> Material ShapeableImageView
            });

            LabelHandler.Mapper.AppendToMapping(
                "MathSolverAndroidMaterialTypography",
                static (handler, _) =>
                {
                    // Material typography has tighter vertical metrics than the
                    // legacy Android TextView defaults. Keep the selected app
                    // font family, but remove Android's extra font padding.
                    handler.PlatformView.SetIncludeFontPadding(false);
                });
        }
#endif

        /// <summary>
        /// Giữ Entry/Picker là control chuẩn của .NET MAUI, không tạo subclass.
        /// Entry tiếp tục dùng native-chrome neutralization (và Android
        /// EmojiCompat workaround). Picker legacy trên Windows/iOS/MacCatalyst
        /// vẫn được neutralize để Border MAUI vẽ khung; trên Android Material 3,
        /// PickerHandler2 được giữ nguyên để native Material field/dialog tự vẽ.
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

                    // AndroidX EmojiCompat gắn một TextWatcher vào
                    // AppCompatEditText. Khi các ô số của Math Solver tự
                    // format ngay trong TextChanged (1000 -> 1,000), watcher
                    // có thể tiếp tục xử lý range của chuỗi cũ sau khi Text đã
                    // đổi độ dài và ném Java.Lang.IllegalArgumentException:
                    // "end should be < than charSequence length".
                    //
                    // Các Entry của ứng dụng không cần emoji processing, nên
                    // tắt EmojiCompat ở native Android Entry. Đây chỉ nằm trong
                    // #if ANDROID; Windows giữ nguyên hoàn toàn.
                    editText.EmojiCompatEnabled = false;
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
