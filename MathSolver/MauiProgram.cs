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
                        // Create one stable native foreground brush before the
                        // control starts receiving theme/input state changes. The
                        // realized presenter is clamped after WinUI VisualStates,
                        // so hover never depends on a cached ThemeResource brush.
                        PrimeWindowsPickerStableForegroundBrush(
                            picker,
                            comboBox);

                        AttachWindowsPickerVisualSync(picker);
                        AttachWindowsPickerNativeSelectionSync(
                            picker,
                            comboBox);
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
            public EventHandler? ThemeChangedHandler;
            public EventHandler? VisualResourcesChangedHandler;
            public WeakReference<Microsoft.Maui.Controls.Picker>? PickerReference;
        }

        private static readonly
            System.Runtime.CompilerServices.ConditionalWeakTable<
                Microsoft.Maui.Controls.Picker,
                WindowsPickerVisualSyncState>
            WindowsPickerVisualSyncStates = new();

        private sealed class WindowsPickerNativeSelectionSyncState
        {
            public bool Attached;
            public bool BrushInitialized;
            public int ForegroundClampGeneration;
            public Microsoft.UI.Xaml.Media.SolidColorBrush? ForegroundBrush;
            public Microsoft.UI.Xaml.VisualStateGroup? CommonStatesGroup;
            public Microsoft.UI.Xaml.VisualStateChangedEventHandler?
                CommonStateChangedHandler;
        }

        // Do not try to override WinUI's ComboBoxForegroundPointerOver
        // ThemeResource keys here. Those keys live in the framework control
        // template's lexical resource scope and some Windows App SDK versions
        // can still resolve/cache the old theme brush even when local resources
        // are supplied. The reliable strategy is a stable per-ComboBox brush +
        // a post-VisualState presenter clamp, with zero foreground dictionary
        // writes during either initialization or theme changes.

        private static readonly
            System.Runtime.CompilerServices.ConditionalWeakTable<
                Microsoft.UI.Xaml.Controls.ComboBox,
                WindowsPickerNativeSelectionSyncState>
            WindowsPickerNativeSelectionSyncStates = new();

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

                // ThemeChanged is the authoritative committed-palette event.
                // Update the already-installed stable brush immediately (Color only)
                // before scheduling the normal deferred native sync. Color mutation
                // does not touch ResourceDictionary/IMap and is safe during the
                // compositor/theme transition window.
                EventHandler? themeChangedHandler = null;
                themeChangedHandler = (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerStableBrushColorRefresh(livePicker);
                        QueueWindowsPickerPostVisualStateForegroundClamp(
                            livePicker);
                        QueueWindowsPickerVisualSync(livePicker);
                        return;
                    }

                    if (themeChangedHandler is not null)
                    {
                        AppThemeManager.ThemeChanged -=
                            themeChangedHandler;
                    }
                };

                state.ThemeChangedHandler = themeChangedHandler;
                AppThemeManager.ThemeChanged +=
                    themeChangedHandler;

                EventHandler? visualResourcesChangedHandler = null;
                visualResourcesChangedHandler = (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        // AppThemeManager raises WallpaperVisualResourcesChanged
                        // only after the wallpaper-aware palette transaction has
                        // completed. Queue from here so WinUI ComboBox receives the final
                        // adaptive polarity even when MAUI did not raise a
                        // Picker.TextColor PropertyChanged notification. Update the
                        // stable brush immediately, then do the guarded full sync.
                        QueueWindowsPickerStableBrushColorRefresh(livePicker);
                        QueueWindowsPickerPostVisualStateForegroundClamp(
                            livePicker);
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

            if (picker.Handler?.PlatformView
                is Microsoft.UI.Xaml.Controls.ComboBox comboBox)
            {
                AttachWindowsPickerNativeSelectionSync(
                    picker,
                    comboBox);
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

            // Only queue dependency-property/managed work here. Never touch
            // ComboBox.Resources from inside DynamicResource/TextColor propagation.
            QueueWindowsPickerPostVisualStateForegroundClamp(picker);
            QueueWindowsPickerVisualSync(picker);
        }

        private static void OnWindowsPickerHandlerChanged(
            object? sender,
            EventArgs e)
        {
            if (sender is Microsoft.Maui.Controls.Picker picker)
            {
                if (picker.Handler?.PlatformView
                    is Microsoft.UI.Xaml.Controls.ComboBox comboBox)
                {
                    AttachWindowsPickerNativeSelectionSync(
                        picker,
                        comboBox);
                }

                QueueWindowsPickerPostVisualStateForegroundClamp(picker);
                QueueWindowsPickerVisualSync(picker);
            }
        }

        private static void AttachWindowsPickerNativeSelectionSync(
            Microsoft.Maui.Controls.Picker picker,
            Microsoft.UI.Xaml.Controls.ComboBox comboBox)
        {
            WindowsPickerNativeSelectionSyncState state =
                WindowsPickerNativeSelectionSyncStates.GetOrCreateValue(
                    comboBox);

            if (state.Attached)
            {
                return;
            }

            var weakPicker =
                new WeakReference<Microsoft.Maui.Controls.Picker>(picker);

            try
            {
                comboBox.SelectionChanged += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerPostSelectionRefresh(livePicker);
                    }
                };

                comboBox.Loaded += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        TryAttachWindowsPickerCommonStateObserver(
                            livePicker,
                            comboBox,
                            state);
                        QueueWindowsPickerPostSelectionRefresh(livePicker);
                    }
                };

                // The stock WinUI ComboBox template owns PointerOver/Pressed and
                // writes ContentPresenter.Foreground from a ThemeResource inside
                // the framework template. A local ResourceDictionary override is
                // not sufficient on every Windows App SDK build: the storyboard
                // can still re-apply the brush resolved by the previous theme.
                // Observe the native state-changing input events and clamp the
                // realized presenter *after* WinUI has processed the state. This
                // path never mutates ResourceDictionary, so it cannot reintroduce
                // the former IMap.Insert/COMException race.
                comboBox.PointerEntered += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerPostVisualStateForegroundClamp(
                            livePicker);
                    }
                };

                comboBox.PointerExited += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerPostVisualStateForegroundClamp(
                            livePicker);
                    }
                };

                comboBox.PointerPressed += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerPostVisualStateForegroundClamp(
                            livePicker);
                    }
                };

                comboBox.PointerReleased += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerPostVisualStateForegroundClamp(
                            livePicker);
                    }
                };

                comboBox.GotFocus += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerPostVisualStateForegroundClamp(
                            livePicker);
                    }
                };

                comboBox.LostFocus += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerPostVisualStateForegroundClamp(
                            livePicker);
                    }
                };

                comboBox.DropDownOpened += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerPostVisualStateForegroundClamp(
                            livePicker);
                    }
                };

                comboBox.DropDownClosed += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        QueueWindowsPickerPostSelectionRefresh(livePicker);
                    }
                };

                // RequestedTheme can cause WinUI to re-evaluate the native
                // template. Re-clamp the realized presenter after the native
                // theme state has settled.
                comboBox.ActualThemeChanged += (_, _) =>
                {
                    if (weakPicker.TryGetTarget(out var livePicker))
                    {
                        TryAttachWindowsPickerCommonStateObserver(
                            livePicker,
                            comboBox,
                            state);
                        QueueWindowsPickerPostSelectionRefresh(livePicker);
                    }
                };

                state.Attached = true;

                // Mapper execution normally precedes Loaded, but handler reuse can
                // attach after the native control is already in the visual tree.
                if (comboBox.IsLoaded)
                {
                    TryAttachWindowsPickerCommonStateObserver(
                        picker,
                        comboBox,
                        state);
                }
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker native selection sync attach deferred: {exception}");
            }
            catch (ObjectDisposedException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker native selection sync attach skipped: {exception}");
            }
            catch (InvalidOperationException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker native selection sync attach skipped: {exception}");
            }
        }

        private static void TryAttachWindowsPickerCommonStateObserver(
            Microsoft.Maui.Controls.Picker picker,
            Microsoft.UI.Xaml.Controls.ComboBox comboBox,
            WindowsPickerNativeSelectionSyncState state)
        {
            try
            {
                comboBox.ApplyTemplate();

                Microsoft.UI.Xaml.FrameworkElement? layoutRoot =
                    FindWindowsPickerTemplateElementByName(
                        comboBox,
                        "LayoutRoot");

                if (layoutRoot is null)
                {
                    return;
                }

                Microsoft.UI.Xaml.VisualStateGroup? commonStates = null;
                foreach (Microsoft.UI.Xaml.VisualStateGroup group in
                    Microsoft.UI.Xaml.VisualStateManager.GetVisualStateGroups(
                        layoutRoot))
                {
                    if (string.Equals(
                            group.Name,
                            "CommonStates",
                            StringComparison.Ordinal))
                    {
                        commonStates = group;
                        break;
                    }
                }

                if (commonStates is null ||
                    ReferenceEquals(state.CommonStatesGroup, commonStates))
                {
                    return;
                }

                if (state.CommonStatesGroup is not null &&
                    state.CommonStateChangedHandler is not null)
                {
                    state.CommonStatesGroup.CurrentStateChanged -=
                        state.CommonStateChangedHandler;
                }

                var weakPicker =
                    new WeakReference<Microsoft.Maui.Controls.Picker>(picker);

                Microsoft.UI.Xaml.VisualStateChangedEventHandler handler =
                    (_, _) =>
                    {
                        if (weakPicker.TryGetTarget(out var livePicker))
                        {
                            // CurrentStateChanged is raised after WinUI has changed
                            // the CommonStates group. This is the authoritative
                            // hook for PointerOver/Pressed: clamp the presenter after
                            // the framework storyboard has written Foreground.
                            QueueWindowsPickerPostVisualStateForegroundClamp(
                                livePicker);
                        }
                    };

                commonStates.CurrentStateChanged += handler;
                state.CommonStatesGroup = commonStates;
                state.CommonStateChangedHandler = handler;
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker visual-state observer deferred: {exception}");
            }
            catch (ObjectDisposedException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker visual-state observer skipped: {exception}");
            }
            catch (InvalidOperationException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker visual-state observer skipped: {exception}");
            }
        }

        private static Microsoft.UI.Xaml.FrameworkElement?
            FindWindowsPickerTemplateElementByName(
                Microsoft.UI.Xaml.DependencyObject parent,
                string name)
        {
            int childCount =
                Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);

            for (int index = 0; index < childCount; index++)
            {
                Microsoft.UI.Xaml.DependencyObject child =
                    Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(
                        parent,
                        index);

                if (child is Microsoft.UI.Xaml.FrameworkElement element &&
                    string.Equals(
                        element.Name,
                        name,
                        StringComparison.Ordinal))
                {
                    return element;
                }

                Microsoft.UI.Xaml.FrameworkElement? nested =
                    FindWindowsPickerTemplateElementByName(
                        child,
                        name);

                if (nested is not null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void PrimeWindowsPickerStableForegroundBrush(
            Microsoft.Maui.Controls.Picker picker,
            Microsoft.UI.Xaml.Controls.ComboBox comboBox)
        {
            try
            {
                Microsoft.Maui.Graphics.Color color =
                    ResolveWindowsPickerAdaptiveTextColor(picker);
                global::Windows.UI.Color nativeColor =
                    ToWindowsColor(color);

                WindowsPickerNativeSelectionSyncState state =
                    WindowsPickerNativeSelectionSyncStates.GetOrCreateValue(
                        comboBox);

                // Create the stable native foreground brush early. No foreground
                // ResourceDictionary entries are written; hover/focus states are
                // corrected after the framework VisualState has applied.
                if (!state.BrushInitialized)
                {
                    InstallWindowsPickerStableForegroundBrush(
                        comboBox,
                        state,
                        nativeColor);
                }

                if (state.ForegroundBrush is not null)
                {
                    state.ForegroundBrush.Color = nativeColor;
                    SetWindowsForegroundIfNeeded(
                        comboBox,
                        state.ForegroundBrush);
                }
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker early stable brush initialization deferred: {exception}");
            }
            catch (ObjectDisposedException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker early stable brush initialization skipped: {exception}");
            }
            catch (InvalidOperationException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker early stable brush initialization skipped: {exception}");
            }
        }

        private static void QueueWindowsPickerStableBrushColorRefresh(
            Microsoft.Maui.Controls.Picker picker)
        {
            // Do not wait for the 96 ms/full native sync path just to change the
            // color of a brush that already exists. This closes the short window
            // where Normal had switched to Dark/Light but PointerOver could still
            // display the previous theme's cached color.
            picker.Dispatcher.Dispatch(
                () => ApplyWindowsPickerStableBrushColorOnly(picker));
        }

        private static void ApplyWindowsPickerStableBrushColorOnly(
            Microsoft.Maui.Controls.Picker picker)
        {
            if (picker.Handler?.PlatformView
                is not Microsoft.UI.Xaml.Controls.ComboBox comboBox)
            {
                return;
            }

            WindowsPickerNativeSelectionSyncState state =
                WindowsPickerNativeSelectionSyncStates.GetOrCreateValue(
                    comboBox);

            // Never initialize resources from this fast path. If the brush does
            // not exist yet, the guarded/full path will initialize it later.
            // This method is deliberately ResourceDictionary-free.
            if (state.ForegroundBrush is null)
            {
                return;
            }

            try
            {
                global::Windows.UI.Color nativeColor =
                    ToWindowsColor(
                        ResolveWindowsPickerAdaptiveTextColor(picker));

                if (!state.ForegroundBrush.Color.Equals(nativeColor))
                {
                    state.ForegroundBrush.Color = nativeColor;
                }

                // Keep the collapsed presenter rooted in the same mutable brush.
                // This is a normal dependency-property assignment, not an IMap
                // mutation, and does not rebuild the ComboBox template.
                SetWindowsForegroundIfNeeded(
                    comboBox,
                    state.ForegroundBrush);
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker stable brush color refresh deferred: {exception}");
            }
            catch (ObjectDisposedException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker stable brush color refresh skipped: {exception}");
            }
            catch (InvalidOperationException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker stable brush color refresh skipped: {exception}");
            }
        }

        internal static void RefreshWindowsPickerThemeVisual(
            Microsoft.Maui.Controls.Picker picker)
        {
            // SettingsPage calls this after its managed foreground repair. Do
            // the ResourceDictionary-free brush update immediately, then keep
            // the existing coalesced full sync for RequestedTheme/template work.
            QueueWindowsPickerStableBrushColorRefresh(picker);
            QueueWindowsPickerPostVisualStateForegroundClamp(picker);
            QueueWindowsPickerVisualSync(picker);
        }

        private static void QueueWindowsPickerPostSelectionRefresh(
            Microsoft.Maui.Controls.Picker picker)
        {
            // Selection/theme changes may replace the selected-content presenter.
            // Re-apply the base native visual immediately, then clamp again after
            // WinUI's queued VisualState/layout work. No ResourceDictionary writes
            // happen in either path.
            picker.Dispatcher.Dispatch(
                () => ApplyWindowsPickerVisualNow(picker));

            QueueWindowsPickerPostVisualStateForegroundClamp(picker);
            QueueWindowsPickerVisualSync(picker);
        }

        private static void QueueWindowsPickerPostVisualStateForegroundClamp(
            Microsoft.Maui.Controls.Picker picker)
        {
            if (picker.Handler?.PlatformView
                is not Microsoft.UI.Xaml.Controls.ComboBox comboBox)
            {
                return;
            }

            WindowsPickerNativeSelectionSyncState state =
                WindowsPickerNativeSelectionSyncStates.GetOrCreateValue(
                    comboBox);

            int generation =
                Interlocked.Increment(ref state.ForegroundClampGeneration);

            // Do two dispatcher-queue turns instead of using a time delay. The
            // first runs after the current routed input/VisualState transaction;
            // the second catches a selected-content presenter that WinUI realizes
            // during the first layout pass. Both passes are O(size of one ComboBox
            // visual tree), generation-coalesced, and never poll LayoutUpdated.
            QueueWindowsPickerForegroundClampPass(
                picker,
                comboBox,
                state,
                generation,
                remainingPasses: 2);
        }

        private static void QueueWindowsPickerForegroundClampPass(
            Microsoft.Maui.Controls.Picker picker,
            Microsoft.UI.Xaml.Controls.ComboBox comboBox,
            WindowsPickerNativeSelectionSyncState state,
            int generation,
            int remainingPasses)
        {
            if (remainingPasses <= 0)
            {
                return;
            }

            bool queued = comboBox.DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () =>
                {
                    if (generation !=
                        Volatile.Read(ref state.ForegroundClampGeneration))
                    {
                        return;
                    }

                    if (picker.Handler?.PlatformView is not
                        Microsoft.UI.Xaml.Controls.ComboBox currentComboBox ||
                        !ReferenceEquals(currentComboBox, comboBox))
                    {
                        return;
                    }

                    ApplyWindowsPickerForegroundClampNow(
                        picker,
                        comboBox,
                        state);

                    QueueWindowsPickerForegroundClampPass(
                        picker,
                        comboBox,
                        state,
                        generation,
                        remainingPasses - 1);
                });

            if (!queued)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Picker foreground clamp dispatcher queue unavailable.");
            }
        }

        private static void ApplyWindowsPickerForegroundClampNow(
            Microsoft.Maui.Controls.Picker picker,
            Microsoft.UI.Xaml.Controls.ComboBox comboBox,
            WindowsPickerNativeSelectionSyncState state)
        {
            Microsoft.UI.Xaml.Media.SolidColorBrush? foregroundBrush =
                state.ForegroundBrush;

            // Resource initialization is intentionally forbidden here. The fast
            // state-clamp path only mutates existing dependency properties/brush
            // Color and therefore remains safe even while a wallpaper or native
            // theme transition is in flight.
            if (foregroundBrush is null)
            {
                QueueWindowsPickerVisualSync(picker);
                return;
            }

            try
            {
                global::Windows.UI.Color nativeColor =
                    ToWindowsColor(
                        ResolveWindowsPickerAdaptiveTextColor(picker));

                if (!foregroundBrush.Color.Equals(nativeColor))
                {
                    foregroundBrush.Color = nativeColor;
                }

                SetWindowsForegroundIfNeeded(
                    comboBox,
                    foregroundBrush);

                // This direct presenter assignment is the authoritative fix for
                // the WinUI Picker hover bug. The framework PointerOver storyboard
                // may resolve a stale ThemeResource; we overwrite the resulting
                // concrete Foreground after that state transition has completed.
                ApplyWindowsPickerDescendantForeground(
                    comboBox,
                    foregroundBrush);
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker foreground clamp deferred: {exception}");
            }
            catch (ObjectDisposedException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker foreground clamp skipped: {exception}");
            }
            catch (InvalidOperationException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker foreground clamp skipped: {exception}");
            }
        }

        private static void ApplyWindowsPickerVisualNow(
            Microsoft.Maui.Controls.Picker picker)
        {
            if (picker.Handler?.PlatformView
                is not Microsoft.UI.Xaml.Controls.ComboBox comboBox)
            {
                return;
            }

            try
            {
                ApplyWindowsPickerRequestedTheme(
                    picker,
                    comboBox);

                ApplyWindowsPickerGlyphColor(
                    comboBox,
                    ResolveWindowsPickerAdaptiveTextColor(picker));
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker post-selection visual refresh deferred: {exception}");
            }
            catch (ObjectDisposedException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker post-selection visual refresh skipped: {exception}");
            }
            catch (InvalidOperationException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Picker post-selection visual refresh skipped: {exception}");
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
                        ApplyWindowsPickerRequestedTheme(
                            picker,
                            comboBox);

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

        private static void ApplyWindowsPickerRequestedTheme(
            Microsoft.Maui.Controls.Picker picker,
            Microsoft.UI.Xaml.Controls.ComboBox comboBox)
        {
            if (!IsSettingsPageDescendant(picker))
            {
                return;
            }

            Microsoft.UI.Xaml.ElementTheme requestedTheme =
                AppThemeManager.IsDarkThemeEffective
                    ? Microsoft.UI.Xaml.ElementTheme.Dark
                    : Microsoft.UI.Xaml.ElementTheme.Light;

            if (comboBox.RequestedTheme != requestedTheme)
            {
                // This is the important ownership fix for Settings Pickers.
                // MAUI updates the managed palette, but a WinUI ComboBox can
                // keep its previous native RequestedTheme. Existing text was
                // repairable by setting Foreground, while a *new* selection
                // presenter created later inherited the stale native theme and
                // therefore came back black in Dark (or white in Light).
                // Synchronizing RequestedTheme makes future TextBlocks correct
                // at creation time instead of chasing them after selection.
                comboBox.RequestedTheme = requestedTheme;
            }
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

        private static global::Windows.UI.Color ToWindowsColor(
            Microsoft.Maui.Graphics.Color color)
        {
            static byte ToByte(double value) =>
                (byte)Math.Clamp(
                    (int)Math.Round(value * 255d),
                    0,
                    255);

            return global::Windows.UI.Color.FromArgb(
                ToByte(color.Alpha),
                ToByte(color.Red),
                ToByte(color.Green),
                ToByte(color.Blue));
        }

        private static void ApplyWindowsPickerGlyphColor(
            Microsoft.UI.Xaml.Controls.ComboBox comboBox,
            Microsoft.Maui.Graphics.Color color)
        {
            ApplyWindowsPickerGlyphColorCore(
                comboBox,
                color,
                ensureLayout: true);
        }

        private static void ApplyWindowsPickerGlyphColorCore(
            Microsoft.UI.Xaml.Controls.ComboBox comboBox,
            Microsoft.Maui.Graphics.Color color,
            bool ensureLayout)
        {
            global::Windows.UI.Color nativeColor =
                ToWindowsColor(color);

            WindowsPickerNativeSelectionSyncState state =
                WindowsPickerNativeSelectionSyncStates.GetOrCreateValue(
                    comboBox);

            // Ensure the per-ComboBox stable brush exists. The framework can
            // still apply its own ThemeResource during PointerOver/Pressed; the
            // VisualState observer clamps the realized presenter afterwards.
            if (!EnsureWindowsPickerStableForegroundBrush(
                    comboBox,
                    state,
                    nativeColor))
            {
                return;
            }

            Microsoft.UI.Xaml.Media.SolidColorBrush foregroundBrush =
                state.ForegroundBrush!;

            if (!foregroundBrush.Color.Equals(nativeColor))
            {
                foregroundBrush.Color = nativeColor;
            }

            if (ensureLayout)
            {
                comboBox.ApplyTemplate();
                comboBox.UpdateLayout();
            }

            SetWindowsForegroundIfNeeded(
                comboBox,
                foregroundBrush);

            // Repair the currently-realized collapsed presenter. Future native
            // state changes are handled by the CommonStates observer/input-event
            // fallback, not by resource lookup overrides.
            ApplyWindowsPickerDescendantForeground(
                comboBox,
                foregroundBrush);
        }

        private static void InstallWindowsPickerStableForegroundBrush(
            Microsoft.UI.Xaml.Controls.ComboBox comboBox,
            WindowsPickerNativeSelectionSyncState state,
            global::Windows.UI.Color initialColor)
        {
            // Deliberately perform NO ResourceDictionary writes here.
            // Creating one stable brush is enough; every realized presenter is
            // rebound to this object after native VisualState transitions.
            state.ForegroundBrush ??=
                new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    initialColor);
            state.BrushInitialized = true;
        }

        private static bool EnsureWindowsPickerStableForegroundBrush(
            Microsoft.UI.Xaml.Controls.ComboBox comboBox,
            WindowsPickerNativeSelectionSyncState state,
            global::Windows.UI.Color initialColor)
        {
            if (!state.BrushInitialized || state.ForegroundBrush is null)
            {
                InstallWindowsPickerStableForegroundBrush(
                    comboBox,
                    state,
                    initialColor);
            }

            return state.ForegroundBrush is not null;
        }

        private static void SetWindowsForegroundIfNeeded(
            Microsoft.UI.Xaml.Controls.Control control,
            Microsoft.UI.Xaml.Media.SolidColorBrush brush)
        {
            // Color equality is not sufficient here. A control can be showing the
            // right color while still holding a different theme brush object. That
            // stale object is exactly what can reappear when PointerOver activates.
            // Canonicalize the brush identity so later Color mutation propagates.
            if (ReferenceEquals(control.Foreground, brush))
            {
                return;
            }

            control.Foreground = brush;
        }

        private static void ApplyWindowsPickerDescendantForeground(
            Microsoft.UI.Xaml.DependencyObject parent,
            Microsoft.UI.Xaml.Media.SolidColorBrush brush)
        {
            int childCount =
                Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);

            for (int index = 0; index < childCount; index++)
            {
                Microsoft.UI.Xaml.DependencyObject child =
                    Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(
                        parent,
                        index);

                if (child is Microsoft.UI.Xaml.Controls.ContentPresenter
                    contentPresenter)
                {
                    // WinUI ComboBox PointerOver targets ContentPresenter.Foreground
                    // directly. ContentPresenter is not a Control, so the previous
                    // recursive repair skipped the exact element that the stock
                    // VisualState mutates. Canonicalize its base foreground too.
                    if (!ReferenceEquals(contentPresenter.Foreground, brush))
                    {
                        contentPresenter.Foreground = brush;
                    }
                }
                else if (child is Microsoft.UI.Xaml.Controls.TextBlock textBlock)
                {
                    // Do not keep a different brush merely because it currently
                    // has the same RGB value. The identity must be the stable brush
                    // or a later VisualState/theme change can revive the old object.
                    if (!ReferenceEquals(textBlock.Foreground, brush))
                    {
                        textBlock.Foreground = brush;
                    }
                }
                else if (child is Microsoft.UI.Xaml.Controls.Control control)
                {
                    SetWindowsForegroundIfNeeded(
                        control,
                        brush);
                }

                ApplyWindowsPickerDescendantForeground(
                    child,
                    brush);
            }
        }
#endif
    }
}
