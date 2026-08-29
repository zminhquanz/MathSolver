using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
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
                .UseMauiCommunityToolkitMediaElement(
                    isAndroidForegroundServiceEnabled: false,
                    static options =>
                    {
                        // Live wallpaper never plays as background audio/video.
                        // Set this both on the builder and MediaElement options
                        // because MediaElement 10.x otherwise may still merge
                        // the Android foreground-service path.
                        options.SetIsAndroidForegroundServiceEnabled(false);
#if ANDROID
                        // A background video must obey MAUI sibling Z-order.
                        // TextureView is required here so controls/cards stay
                        // above the video instead of SurfaceView punching through.
                        options.SetDefaultAndroidViewType(
                            AndroidViewType.TextureView);
#endif
                    })
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

            EditorHandler.Mapper.AppendToMapping(
                "MathSolverBorderHostedEditor",
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

                    // AverageView uses Editor for its multiline number list.
                    // Keep the MAUI Border as the only visible input chrome,
                    // matching Entry fields across the other solver tabs.
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
                        AttachWindowsPickerVisualSync(picker);
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

            // Do not mutate WinUI ComboBox resources synchronously from the
            // MAUI TextColor mapper. DynamicResource propagation can run while a
            // live-wallpaper native surface or ComboBox popup is being rebuilt;
            // synchronous native resource writes here were the remaining source
            // of intermittent COMException crashes. Windows Picker visual sync is
            // attached above and performs a coalesced deferred update instead.
        }

#if WINDOWS
        private sealed class WindowsPickerVisualSyncState
        {
            public WindowsPickerVisualSyncState()
            {
            }

            public int Generation;
            public bool Attached;
            public EventHandler? VisualResourcesChangedHandler;
            public WeakReference<Microsoft.Maui.Controls.Picker>? PickerReference;
        }

        private static readonly
            System.Runtime.CompilerServices.ConditionalWeakTable<
                Microsoft.Maui.Controls.Picker,
                WindowsPickerVisualSyncState>
            WindowsPickerVisualSyncStates = new();

        private static void AttachWindowsPickerVisualSync(
            Microsoft.Maui.Controls.Picker picker)
        {
            WindowsPickerVisualSyncState state =
                WindowsPickerVisualSyncStates.GetOrCreateValue(picker);

            if (!state.Attached)
            {
                picker.PropertyChanged +=
                    OnWindowsPickerPropertyChanged;
                picker.HandlerChanged +=
                    OnWindowsPickerHandlerChanged;

                var weakPicker =
                    new WeakReference<Microsoft.Maui.Controls.Picker>(picker);
                state.PickerReference = weakPicker;

                EventHandler? visualResourcesChangedHandler = null;
                visualResourcesChangedHandler = (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        // AppThemeManager raises WallpaperVisualResourcesChanged
                        // only after the wallpaper-aware palette transaction has
                        // completed. Queue from here so WinUI ComboBox receives the final
                        // adaptive polarity even when MAUI did not raise a
                        // Picker.TextColor PropertyChanged notification.
                        QueueWindowsPickerVisualSync(livePicker);
                        return;
                    }

                    if (visualResourcesChangedHandler is not null)
                    {
                        AppThemeManager.WallpaperVisualResourcesChanged -=
                            visualResourcesChangedHandler;
                    }
                };

                state.VisualResourcesChangedHandler = visualResourcesChangedHandler;
                AppThemeManager.WallpaperVisualResourcesChanged +=
                    visualResourcesChangedHandler;
                state.Attached = true;
            }

            QueueWindowsPickerVisualSync(picker);
        }

        private static void OnWindowsPickerPropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not Microsoft.Maui.Controls.Picker picker ||
                e.PropertyName !=
                Microsoft.Maui.Controls.Picker.TextColorProperty.PropertyName)
            {
                return;
            }

            // Only queue managed work here. Never touch ComboBox.Resources from
            // inside DynamicResource/TextColor propagation itself.
            QueueWindowsPickerVisualSync(picker);
        }

        private static void OnWindowsPickerHandlerChanged(
            object? sender,
            EventArgs e)
        {
            if (sender is Microsoft.Maui.Controls.Picker picker)
            {
                QueueWindowsPickerVisualSync(picker);
            }
        }

        private static void QueueWindowsPickerVisualSync(
            Microsoft.Maui.Controls.Picker picker)
        {
            WindowsPickerVisualSyncState state =
                WindowsPickerVisualSyncStates.GetOrCreateValue(picker);

            int generation =
                Interlocked.Increment(ref state.Generation);

            TimeSpan delay =
                AppThemeManager.GetWindowsPickerVisualUpdateDelay(
                    TimeSpan.FromMilliseconds(96));

            picker.Dispatcher.DispatchDelayed(
                delay,
                () =>
                {
                    if (generation != Volatile.Read(ref state.Generation))
                    {
                        return;
                    }

                    TimeSpan remaining =
                        AppThemeManager.GetWindowsPickerVisualUpdateDelay(
                            TimeSpan.Zero);

                    if (remaining > TimeSpan.Zero)
                    {
                        QueueWindowsPickerVisualSync(picker);
                        return;
                    }

                    if (picker.Handler?.PlatformView
                        is not Microsoft.UI.Xaml.Controls.ComboBox comboBox)
                    {
                        return;
                    }

                    try
                    {
                        ApplyWindowsPickerGlyphColor(
                            comboBox,
                            ResolveWindowsPickerAdaptiveTextColor(
                                picker));
                    }
                    catch (System.Runtime.InteropServices.COMException exception)
                    {
                        // A stale WinUI ComboBox can survive for a few compositor
                        // turns after a page/live-wallpaper transition. Ignore the
                        // stale native target and let the current handler receive
                        // the next queued color. Never terminate the app for a
                        // cosmetic chevron refresh.
                        System.Diagnostics.Debug.WriteLine(
                            $"Picker visual sync deferred: {exception}");

                        if (AppThemeManager.IsLiveWallpaperNativeExceptionGuardActive)
                        {
                            AppThemeManager.RecoverFromLiveWallpaperNativeException(
                                exception);
                        }
                    }
                    catch (ObjectDisposedException exception)
                    {
                        // ObjectDisposedException derives from InvalidOperationException,
                        // so it must be caught first to keep this handler reachable.
                        System.Diagnostics.Debug.WriteLine(
                            $"Picker visual sync skipped: {exception}");
                    }
                    catch (InvalidOperationException exception)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Picker visual sync skipped: {exception}");
                    }
                });
        }

        private static Microsoft.Maui.Graphics.Color
            ResolveWindowsPickerAdaptiveTextColor(
                Microsoft.Maui.Controls.Picker picker)
        {
            // SettingsPage intentionally follows the selected app theme even
            // while it is configuring/previewing a live wallpaper. Its Pickers
            // are rebound with concrete TextPrimaryColor values after a theme
            // switch, so never override them with WallpaperTextPrimaryColor.
            string resourceKey =
                IsSettingsPageDescendant(picker)
                    ? "TextPrimaryColor"
                    : "WallpaperTextPrimaryColor";

            if (Application.Current?.Resources is ResourceDictionary resources &&
                resources.TryGetValue(resourceKey, out object? value))
            {
                if (value is Microsoft.Maui.Graphics.Color color)
                {
                    return color;
                }

                if (value is Microsoft.Maui.Controls.SolidColorBrush brush)
                {
                    return brush.Color;
                }
            }

            return picker.TextColor;
        }

        private static bool IsSettingsPageDescendant(
            Microsoft.Maui.Controls.Element element)
        {
            Microsoft.Maui.Controls.Element? current = element;

            while (current is not null)
            {
                if (current is MathSolver.Views.SettingsPage)
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

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

            // MAUI Picker.TextColor can change dynamically while MP4 adaptive
            // contrast is running. WinUI ComboBox does not always propagate a
            // resource-only Foreground change to its selected-content TextBlock,
            // so push the same brush into the native control and its visual-state
            // resources. This keeps both the selected text and chevron readable
            // when wallpaper polarity flips independently of the app theme.
            comboBox.Foreground = glyphBrush;
            comboBox.Resources["ComboBoxForeground"] = glyphBrush;
            comboBox.Resources["ComboBoxForegroundPointerOver"] = glyphBrush;
            comboBox.Resources["ComboBoxForegroundPressed"] = glyphBrush;
            comboBox.Resources["ComboBoxForegroundDisabled"] = glyphBrush;
            comboBox.Resources["ComboBoxPlaceholderForeground"] = glyphBrush;

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
