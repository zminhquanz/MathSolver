using MathSolver.Services;

namespace MathSolver.Views;

public partial class SettingsPage : ContentPage
{
    private bool _updatingControls;
    private bool _updatingFontSelection;
    private bool _updatingLanguageSelection;
    private bool _hasPlayedEntryAnimation;
    private bool _isClosing;
    private bool _updatingFullNumberDisplaySwitch;
    private bool _updatingDeveloperModeSwitch;
    private bool _updatingDynamicColorSwitch;
    private bool _updatingLiveWallpaperSwitch;
    private bool _updatingLiveWallpaperModePicker;
    private bool _isImportingLiveWallpaper;
    private string? _pendingLiveWallpaperFileName;

    // Picker.ItemsSource yêu cầu IList, trong khi AppFontCatalog.Options
    // được khai báo là IReadOnlyList. Tạo một List dùng chung để vừa
    // tương thích với Picker, vừa giữ đúng cùng các AppFontOption.
    private readonly List<AppFontOption> _fontOptions =
        AppFontCatalog.Options.ToList();

    private readonly List<AppLanguageOption> _languageOptions =
        AppLanguageCatalog.Options.ToList();

    private static readonly FilePickerFileType Mp4WallpaperFileType =
        new(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = [".mp4"],
                [DevicePlatform.Android] = ["video/mp4"],
                [DevicePlatform.iOS] = ["public.mpeg-4"],
                [DevicePlatform.MacCatalyst] = ["public.mpeg-4"]
            });

    public SettingsPage()
    {
        InitializeComponent();

        LocalizationService.Attach(
            this);

        Shell.SetNavBarIsVisible(
            this,
            true);

        Shell.SetBackButtonBehavior(
            this,
            new BackButtonBehavior
            {
                IsVisible =
                    false,

                IsEnabled =
                    false
            });

        Shell.SetTabBarIsVisible(
            this,
            false);

        FontPicker.ItemsSource =
            _fontOptions;

        LanguagePicker.ItemsSource =
            _languageOptions;

#if ANDROID
        ApplyAndroidCompactControlSurfaces();
#endif

        LoadCurrentSettings();
        PreparePageEntryAnimation();
    }

#if ANDROID
    /// <summary>
    /// Keep the Android Settings Picker fields visually distinct from their
    /// cards while retaining the stock .NET MAUI Material 3 Picker behavior.
    /// </summary>
    private void ApplyAndroidCompactControlSurfaces()
    {
        AndroidPickerVisualHelper.Attach(
            FontPicker);

        AndroidPickerVisualHelper.Attach(
            LanguagePicker);

        AndroidPickerVisualHelper.Attach(
            LiveWallpaperModePicker);

        ResetSettingsButton.SetDynamicResource(
            VisualElement.BackgroundColorProperty,
            "PrimarySoftColor");

        ResetSettingsButton.SetDynamicResource(
            Button.TextColorProperty,
            "PrimaryColor");

        ResetSettingsButton.SetDynamicResource(
            Button.BorderColorProperty,
            "PrimaryBorderColor");

        ResetSettingsButton.BorderWidth =
            1d;
    }
#endif

    protected override bool OnBackButtonPressed()
    {
        _ =
            CloseAsync();

        return true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Shell.SetTabBarIsVisible(
            this,
            false);

        AppThemeManager.ThemeChanged += OnThemeChanged;
        AppFontManager.FontChanged += OnFontChanged;
        AppLanguageManager.LanguageChanged += OnLanguageChanged;
        LocalizationService.CultureChanged += OnLocalizationCultureChanged;
        DeveloperModeManager.DeveloperModeChanged += OnDeveloperModeChanged;
        ResultNumberDisplayMode.DisplayModeChanged += OnResultNumberDisplayModeChanged;
        LiveWallpaperManager.SettingsChanged += OnLiveWallpaperSettingsChanged;

        LoadCurrentSettings();

        if (LiveWallpaperManager.HasWallpaper &&
            !LiveWallpaperManager.IsHardwareH264Validated)
        {
            _ = ValidateExistingLiveWallpaperAsync();
        }

        if (!_hasPlayedEntryAnimation)
        {
            _hasPlayedEntryAnimation =
                true;

            Dispatcher.Dispatch(
                async () =>
                    await PlayPageEntryAnimationAsync());
        }
    }

    protected override void OnDisappearing()
    {
        AppThemeManager.ThemeChanged -= OnThemeChanged;
        AppFontManager.FontChanged -= OnFontChanged;
        AppLanguageManager.LanguageChanged -= OnLanguageChanged;
        LocalizationService.CultureChanged -= OnLocalizationCultureChanged;
        DeveloperModeManager.DeveloperModeChanged -= OnDeveloperModeChanged;
        ResultNumberDisplayMode.DisplayModeChanged -= OnResultNumberDisplayModeChanged;
        LiveWallpaperManager.SettingsChanged -= OnLiveWallpaperSettingsChanged;

        Shell.SetTabBarIsVisible(
            this,
            true);

        base.OnDisappearing();
    }

    private void PreparePageEntryAnimation()
    {
#if ANDROID
        // Material shared-axis style: enter from the trailing edge without
        // scaling the page surface. This keeps the motion lighter on phones.
        SettingsPageContentRoot.Opacity =
            0d;

        SettingsPageContentRoot.TranslationX =
            24d;

        SettingsPageContentRoot.Scale =
            1d;
#else
        SettingsPageContentRoot.Opacity =
            0d;

        SettingsPageContentRoot.TranslationX =
            42d;

        SettingsPageContentRoot.Scale =
            0.995d;
#endif
    }

    private async Task PlayPageEntryAnimationAsync()
    {
        SettingsPageContentRoot.CancelAnimations();

#if ANDROID
        await Task.WhenAll(
            SettingsPageContentRoot.FadeToAsync(
                1d,
                170,
                Easing.CubicOut),

            SettingsPageContentRoot.TranslateToAsync(
                0d,
                0d,
                220,
                Easing.CubicOut));
#else
        await Task.WhenAll(
            SettingsPageContentRoot.FadeToAsync(
                1d,
                190,
                Easing.CubicOut),

            SettingsPageContentRoot.TranslateToAsync(
                0d,
                0d,
                240,
                Easing.CubicOut),

            SettingsPageContentRoot.ScaleToAsync(
                1d,
                240,
                Easing.CubicOut));
#endif
    }

    private async Task PlayPageExitAnimationAsync()
    {
        SettingsPageContentRoot.CancelAnimations();

#if ANDROID
        await Task.WhenAll(
            SettingsPageContentRoot.FadeToAsync(
                0d,
                110,
                Easing.CubicIn),

            SettingsPageContentRoot.TranslateToAsync(
                24d,
                0d,
                150,
                Easing.CubicIn));
#else
        await Task.WhenAll(
            SettingsPageContentRoot.FadeToAsync(
                0d,
                125,
                Easing.CubicIn),

            SettingsPageContentRoot.TranslateToAsync(
                34d,
                0d,
                155,
                Easing.CubicIn),

            SettingsPageContentRoot.ScaleToAsync(
                0.995d,
                155,
                Easing.CubicIn));
#endif
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        LoadCurrentSettings();
    }

    private void OnLiveWallpaperSettingsChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            UpdateLiveWallpaperSettings);
    }

    private void OnFontChanged(
        object? sender,
        EventArgs e)
    {
        LoadFontSettings();
    }

    private void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        // AppLanguageManager phát event trước khi JSON language pack đổi xong.
        // Chỉ đồng bộ selection ở đây; text item được refresh khi
        // LocalizationService.CultureChanged chạy sau đó.
        LoadLanguageSettings();
        UpdateAdvancedSettingsState();
    }

    private void OnLocalizationCultureChanged(
        object? sender,
        EventArgs e)
    {
        RefreshPickerDisplayItems();
        LoadCurrentSettings();
    }

    private void RefreshPickerDisplayItems()
    {
        _updatingFontSelection = true;
        _updatingLanguageSelection = true;

        try
        {
            FontPicker.ItemsSource =
                null;

            FontPicker.ItemsSource =
                _fontOptions;

            LanguagePicker.ItemsSource =
                null;

            LanguagePicker.ItemsSource =
                _languageOptions;
        }
        finally
        {
            _updatingFontSelection = false;
            _updatingLanguageSelection = false;
        }
    }

    private void OnDeveloperModeChanged(
        object? sender,
        EventArgs e)
    {
        UpdateAdvancedSettingsState();
    }

    private void OnResultNumberDisplayModeChanged(
        object? sender,
        EventArgs e)
    {
        UpdateAdvancedSettingsState();
    }

    private void OnSystemThemeClicked(object? sender, EventArgs e)
    {
        AppThemeManager.SetThemeMode(AppThemeMode.System);
        UpdateThemeModeButtons();
    }

    private void OnLightThemeClicked(object? sender, EventArgs e)
    {
        AppThemeManager.SetThemeMode(AppThemeMode.Light);
        UpdateThemeModeButtons();
    }

    private void OnDarkThemeClicked(object? sender, EventArgs e)
    {
        AppThemeManager.SetThemeMode(AppThemeMode.Dark);
        UpdateThemeModeButtons();
    }

    private void OnPresetColorClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.CommandParameter is not string hexColor)
        {
            return;
        }

        ApplyHexColor(hexColor);
    }

    private void OnApplyHexClicked(object? sender, EventArgs e)
    {
        ApplyHexColor(HexColorEntry.Text);
    }

    private void OnRgbValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_updatingControls)
        {
            return;
        }

        Color color = GetSliderColor();

        UpdateRgbLabels();
        UpdateColorPreview(color);
        HexColorEntry.Text = AppThemeManager.ToHex(color);
        HideValidationMessage();
    }

    private void OnRgbDragCompleted(object? sender, EventArgs e)
    {
        if (_updatingControls)
        {
            return;
        }

        AppThemeManager.SetAccentColor(GetSliderColor());
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        AppThemeManager.ResetToDefault();
        AppFontManager.ResetToDefault();
        AppLanguageManager.ResetToDefault();
        DeveloperModeManager.ResetToDefault();
        ResultNumberDisplayMode.ResetToDefault();
        LiveWallpaperManager.ResetToDefault();
#if ANDROID
        AndroidMaterialYouManager.SetDynamicColorEnabled(false);
#endif

        LoadCurrentSettings();
        UpdateAdvancedSettingsState();
    }

    private void ApplyHexColor(string? input)
    {
        if (!AppThemeManager.TryParseHexColor(
                input,
                out Color color,
                out string normalizedHex))
        {
            ShowValidationMessage(
                "Màu không hợp lệ. Hãy nhập dạng #RRGGBB, ví dụ #6D28D9.");

            return;
        }

        AppThemeManager.SetAccentColor(normalizedHex);
        SetColorControls(color, normalizedHex);
        HideValidationMessage();
    }

    private void OnFullNumberDisplayToggled(
        object? sender,
        ToggledEventArgs e)
    {
        if (_updatingFullNumberDisplaySwitch)
        {
            return;
        }

        ResultNumberDisplayMode.SetShowFullNumbers(
            e.Value);

        UpdateAdvancedSettingsState();
    }

    private void OnDynamicColorToggled(
        object? sender,
        ToggledEventArgs e)
    {
#if ANDROID
        if (_updatingDynamicColorSwitch)
        {
            return;
        }

        AndroidMaterialYouManager.SetDynamicColorEnabled(
            e.Value);
#endif
    }

    private async Task ValidateExistingLiveWallpaperAsync()
    {
        await LiveWallpaperManager.EnsureOptimizedWallpaperAsync();

        if (Dispatcher.IsDispatchRequired)
        {
            Dispatcher.Dispatch(UpdateLiveWallpaperSettings);
        }
        else
        {
            UpdateLiveWallpaperSettings();
        }
    }

    private void OnLiveWallpaperToggled(
        object? sender,
        ToggledEventArgs e)
    {
        if (_updatingLiveWallpaperSwitch)
        {
            return;
        }

        LiveWallpaperManager.SetEnabled(
            e.Value);

        UpdateLiveWallpaperSettings();
    }

    private void OnLiveWallpaperModeChanged(
        object? sender,
        EventArgs e)
    {
        if (_updatingLiveWallpaperModePicker ||
            LiveWallpaperModePicker.SelectedIndex < 0)
        {
            return;
        }

        LiveWallpaperMode selectedMode =
            LiveWallpaperModePicker.SelectedIndex == 1
                ? LiveWallpaperMode.Mp4
                : LiveWallpaperMode.MathAnimation;

        LiveWallpaperManager.SetMode(selectedMode);
        UpdateLiveWallpaperSettings();
    }

    private async void OnSelectLiveWallpaperClicked(
        object? sender,
        EventArgs e)
    {
        bool useEnglish =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English;

        try
        {
            FileResult? selected =
                await FilePicker.Default.PickAsync(
                    new PickOptions
                    {
                        PickerTitle = useEnglish
                            ? "Choose an MP4 wallpaper"
                            : "Chọn hình nền động MP4",
                        FileTypes = Mp4WallpaperFileType
                    });

            if (selected is null)
            {
                return;
            }

            _isImportingLiveWallpaper = true;
            _pendingLiveWallpaperFileName = selected.FileName;
            UpdateLiveWallpaperSettings();

            // Give WinUI/Android one frame to paint the immediate "receiving"
            // state before file I/O and native metadata validation begin.
            await Task.Yield();

            await LiveWallpaperManager.ImportMp4Async(
                selected);
        }
        catch (LiveWallpaperVideoValidationException exception)
        {
            string message =
                exception.Error switch
                {
                    LiveWallpaperVideoValidationError.NotH264 =>
                        useEnglish
                            ? "The MP4 video stream must use H.264 / AVC so Math Solver can use the native hardware-decoding path."
                            : "Luồng video trong MP4 phải dùng H.264 / AVC để Math Solver sử dụng đường giải mã phần cứng native.",
                    LiveWallpaperVideoValidationError.HardwareH264DecoderUnavailable =>
                        useEnglish
                            ? "No compatible H.264 decoder is available for the hardware-preferred wallpaper path on this device."
                            : "Thiết bị không có bộ giải mã H.264 tương thích cho đường hình nền ưu tiên phần cứng.",
                    LiveWallpaperVideoValidationError.DurationTooLong =>
                        useEnglish
                            ? "Live wallpaper videos are limited to 120 seconds. Choose a shorter MP4 clip."
                            : "Video hình nền động được giới hạn tối đa 120 giây. Hãy chọn một MP4 ngắn hơn.",
                    _ =>
                        useEnglish
                            ? "The selected MP4 is not compatible with the optimized wallpaper path."
                            : "MP4 đã chọn không tương thích với đường hình nền đã tối ưu."
                };

            await MaterialDialogService.ShowAlertAsync(
                this,
                useEnglish ? "Unsupported video" : "Video không hỗ trợ",
                message,
                "OK");
        }
        catch (InvalidDataException)
        {
            await MaterialDialogService.ShowAlertAsync(
                this,
                useEnglish ? "Unsupported file" : "File không hỗ trợ",
                useEnglish
                    ? "Math Solver currently supports MP4 video wallpapers only."
                    : "Hiện tại Math Solver chỉ hỗ trợ hình nền động bằng video MP4.",
                "OK");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Unable to import live wallpaper: {exception}");

            await MaterialDialogService.ShowAlertAsync(
                this,
                useEnglish ? "Unable to use wallpaper" : "Không thể dùng hình nền",
                useEnglish
                    ? "The selected MP4 could not be copied into Math Solver's app storage."
                    : "Không thể sao chép file MP4 đã chọn vào bộ nhớ của Math Solver.",
                "OK");
        }
        finally
        {
            _isImportingLiveWallpaper = false;
            _pendingLiveWallpaperFileName = null;
            UpdateLiveWallpaperSettings();
        }
    }

    private void OnRemoveLiveWallpaperClicked(
        object? sender,
        EventArgs e)
    {
        LiveWallpaperManager.RemoveWallpaper();
        UpdateLiveWallpaperSettings();
    }

    private void UpdateLiveWallpaperSettings()
    {
        bool hasWallpaper =
            LiveWallpaperManager.HasWallpaper;
        LiveWallpaperMode mode =
            LiveWallpaperManager.Mode;
        bool useEnglish =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English;

        bool isAnalyzing =
            LiveWallpaperManager.IsFrameAnalysisRunning;

        _updatingLiveWallpaperModePicker = true;
        try
        {
            if (LiveWallpaperModePicker.Items.Count >= 2)
            {
                LiveWallpaperModePicker.SelectedIndex =
                    mode == LiveWallpaperMode.Mp4
                        ? 1
                        : 0;
            }
        }
        finally
        {
            _updatingLiveWallpaperModePicker = false;
        }

        bool canEnable =
            mode == LiveWallpaperMode.MathAnimation ||
            (hasWallpaper &&
             LiveWallpaperManager.IsHardwareH264Validated);

        _updatingLiveWallpaperSwitch = true;
        try
        {
            LiveWallpaperSwitch.IsEnabled =
                canEnable && !_isImportingLiveWallpaper;
            LiveWallpaperSwitch.IsToggled =
                LiveWallpaperManager.IsEnabled;
        }
        finally
        {
            _updatingLiveWallpaperSwitch = false;
        }

        MathAnimationInfoBorder.IsVisible =
            mode == LiveWallpaperMode.MathAnimation;
        Mp4WallpaperOptionsLayout.IsVisible =
            mode == LiveWallpaperMode.Mp4;

        LiveWallpaperImportActivity.IsVisible =
            _isImportingLiveWallpaper;
        LiveWallpaperImportActivity.IsRunning =
            _isImportingLiveWallpaper;

        SelectLiveWallpaperButton.IsEnabled =
            !_isImportingLiveWallpaper;
        RemoveLiveWallpaperButton.IsEnabled =
            hasWallpaper && !_isImportingLiveWallpaper;
        LiveWallpaperModePicker.IsEnabled =
            !_isImportingLiveWallpaper;

        SelectLiveWallpaperButton.Text =
            _isImportingLiveWallpaper
                ? (useEnglish ? "Receiving MP4…" : "Đang nhận MP4…")
                : (useEnglish ? "Choose MP4" : "Chọn MP4");

        LiveWallpaperFileNameLabel.Text =
            _isImportingLiveWallpaper &&
            !string.IsNullOrWhiteSpace(_pendingLiveWallpaperFileName)
                ? (useEnglish
                    ? $"Receiving {_pendingLiveWallpaperFileName}…"
                    : $"Đang nhận {_pendingLiveWallpaperFileName}…")
                : hasWallpaper
                    ? LiveWallpaperManager.OriginalFileName ??
                        Path.GetFileName(LiveWallpaperManager.WallpaperPath)
                    : (useEnglish
                        ? "No MP4 selected"
                        : "Chưa chọn file MP4");

        if (mode == LiveWallpaperMode.Mp4)
        {
            LiveWallpaperDecodeValueLabel.Text =
                isAnalyzing
                    ? (useEnglish
                        ? "H.264 / AVC • accepted • optimizing contrast…"
                        : "H.264 / AVC • đã nhận • đang tối ưu tương phản…")
                    : (useEnglish
                        ? "H.264 / AVC • hardware preferred"
                        : "H.264 / AVC • phần cứng ưu tiên");
        }

        LiveWallpaperEnabledSummaryLabel.Text =
            mode == LiveWallpaperMode.MathAnimation
                ? (useEnglish
                    ? "The lightweight 24 FPS GraphicsView animation keeps running during local AI inference and stops only when the tab is inactive."
                    : "Animation GraphicsView nhẹ ở 24 FPS vẫn chạy khi AI local tạo sinh và chỉ dừng khi tab không hoạt động.")
                : (useEnglish
                    ? "H.264 video loops silently with native hardware-preferred decoding and keeps playing during local AI inference."
                    : "Video H.264 tự lặp, tắt tiếng, ưu tiên giải mã phần cứng native và vẫn phát khi AI local tạo sinh.");
    }

    private void OnDeveloperModeToggled(
        object? sender,
        ToggledEventArgs e)
    {
        if (_updatingDeveloperModeSwitch)
        {
            return;
        }

        DeveloperModeManager.SetEnabled(
            e.Value);

        UpdateAdvancedSettingsState();
    }

    private void UpdateAdvancedSettingsState()
    {
        bool useEnglish =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English;

#if ANDROID
        DynamicColorTitleLabel.Text =
            useEnglish
                ? "Dynamic color"
                : "Màu theo hình nền";

        DynamicColorSummaryLabel.Text =
            !AndroidMaterialYouManager.IsDynamicColorSupported
                ? (useEnglish
                    ? "Requires Android 12 or later"
                    : "Yêu cầu Android 12 trở lên")
                : (useEnglish
                    ? "Use the Material You palette from your wallpaper"
                    : "Dùng bảng màu Material You từ hình nền hệ thống");
#endif

        _updatingFullNumberDisplaySwitch = true;
        _updatingDeveloperModeSwitch = true;

        try
        {
            FullNumberDisplaySwitch.IsToggled =
                ResultNumberDisplayMode.ShowFullNumbers;

            DeveloperModeSwitch.IsToggled =
                DeveloperModeManager.IsEnabled;
        }
        finally
        {
            _updatingFullNumberDisplaySwitch = false;
            _updatingDeveloperModeSwitch = false;
        }

        SettingsPageTitleLabel.Text =
            useEnglish
                ? "Settings"
                : "Cài đặt";

        SettingsPageSubtitleLabel.Text =
            useEnglish
                ? "Appearance, result display, and developer tools"
                : "Giao diện, kết quả hiển thị và công cụ nhà phát triển";

        LiveWallpaperSectionTitleLabel.Text =
            useEnglish
                ? "Animated wallpaper"
                : "Hình nền động";

        LiveWallpaperSectionDescriptionLabel.Text =
            useEnglish
                ? "Choose Math Solver's lightweight math animation or your own H.264 MP4 for the main learning tabs."
                : "Chọn animation toán học nhẹ của Math Solver hoặc video MP4 H.264 của bạn cho các tab học toán.";

        LiveWallpaperEnabledTitleLabel.Text =
            useEnglish
                ? "Enable animated wallpaper"
                : "Bật hình nền động";

        LiveWallpaperEnabledSummaryLabel.Text =
            useEnglish
                ? "Backgrounds stop on inactive tabs; both the Math animation and validated hardware H.264 keep running during local AI inference."
                : "Hình nền dừng khi tab không hoạt động; cả Math Animation và H.264 đã xác nhận giải mã phần cứng vẫn tiếp tục chạy khi AI local tạo sinh.";

        LiveWallpaperModeTitleLabel.Text =
            useEnglish
                ? "Animated background type"
                : "Kiểu hình nền động";

        _updatingLiveWallpaperModePicker = true;
        try
        {
            int selectedMode =
                LiveWallpaperManager.Mode == LiveWallpaperMode.Mp4
                    ? 1
                    : 0;

            LiveWallpaperModePicker.Items.Clear();
            LiveWallpaperModePicker.Items.Add(
                useEnglish
                    ? "Math Solver animation (GraphicsView)"
                    : "Animation Math Solver (GraphicsView)");
            LiveWallpaperModePicker.Items.Add(
                useEnglish
                    ? "MP4 live wallpaper"
                    : "Live Wallpaper MP4");
            LiveWallpaperModePicker.SelectedIndex = selectedMode;
        }
        finally
        {
            _updatingLiveWallpaperModePicker = false;
        }

        MathAnimationTitleLabel.Text =
            useEnglish
                ? "Math Solver Animation"
                : "Animation Math Solver";

        MathAnimationSummaryLabel.Text =
            useEnglish
                ? "GraphicsView • 24 FPS • no external file • lighter than video"
                : "GraphicsView • 24 FPS • không cần file • nhẹ hơn video";

        LiveWallpaperDecodeTitleLabel.Text =
            useEnglish
                ? "Video decoding"
                : "Giải mã video";

        LiveWallpaperDecodeValueLabel.Text =
            LiveWallpaperManager.IsFrameAnalysisRunning
                ? (useEnglish
                    ? "H.264 / AVC • accepted • optimizing contrast…"
                    : "H.264 / AVC • đã nhận • đang tối ưu tương phản…")
                : (useEnglish
                    ? "H.264 / AVC • hardware preferred"
                    : "H.264 / AVC • phần cứng ưu tiên");

        LiveWallpaperFileTitleLabel.Text =
            useEnglish
                ? "MP4 file"
                : "File MP4";

        SelectLiveWallpaperButton.Text =
            _isImportingLiveWallpaper
                ? (useEnglish ? "Receiving MP4…" : "Đang nhận MP4…")
                : (useEnglish ? "Choose MP4" : "Chọn MP4");

        RemoveLiveWallpaperButton.Text =
            useEnglish
                ? "Remove wallpaper"
                : "Gỡ hình nền";

        LiveWallpaperNoteLabel.Text =
            useEnglish
                ? "MP4 wallpapers must use H.264 / AVC and be no longer than 120 seconds. Math Solver accepts a validated file first, then builds the low-resolution brightness timeline in the background and automatically switches the learning UI between light and dark text/glass while playback stays hardware-preferred."
                : "Hình nền MP4 phải dùng H.264 / AVC và dài tối đa 120 giây. Math Solver nhận file H.264 hợp lệ trước, sau đó phân tích timeline độ sáng độ phân giải thấp ở nền rồi tự chuyển chữ/kính sáng hoặc tối khi phát; video vẫn ưu tiên giải mã phần cứng.";

        ResultDisplaySectionTitleLabel.Text =
            useEnglish
                ? "Result display"
                : "Hiển thị kết quả";

        ResultDisplaySectionDescriptionLabel.Text =
            useEnglish
                ? "Choose how Math Solver presents results containing many digits."
                : "Tùy chỉnh cách Math Solver trình bày các kết quả có nhiều chữ số.";

        FullNumberDisplayTitleLabel.Text =
            LocalizationService.TranslateKey(
                "Settings.NumberDisplay.Title");

        FullNumberDisplaySummaryLabel.Text =
            LocalizationService.TranslateKey(
                ResultNumberDisplayMode.ShowFullNumbers
                    ? "Settings.NumberDisplay.SummaryFull"
                    : "Settings.NumberDisplay.SummaryCompact");

        DeveloperSectionTitleLabel.Text =
            useEnglish
                ? "Developer mode"
                : "Chế độ nhà phát triển";

        DeveloperSectionDescriptionLabel.Text =
            useEnglish
                ? "Enable diagnostic data used to inspect algorithms and AI/LLM behavior."
                : "Bật các dữ liệu chẩn đoán dùng để kiểm tra thuật toán và AI/LLM.";

        DeveloperModeTitleLabel.Text =
            useEnglish
                ? "Developer mode"
                : "Chế độ nhà phát triển";

        DeveloperModeDescriptionLabel.Text =
            useEnglish
                ? "Show JSON, validation logs, and technical details when diagnostics are needed."
                : "Hiện JSON, log validation và chi tiết kỹ thuật khi cần kiểm tra.";

        DeveloperModeStateLabel.Text =
            (useEnglish, DeveloperModeManager.IsEnabled) switch
            {
                (true, true) => "✓ ENABLED",
                (true, false) => "○ DISABLED",
                (false, true) => "✓ ĐANG BẬT",
                _ => "○ ĐANG TẮT"
            };

        DeveloperModeStateBadge.SetDynamicResource(
            Border.BackgroundColorProperty,
            DeveloperModeManager.IsEnabled
                ? "PrimarySoftColor"
                : "SurfaceAltColor");

        DeveloperModeStateBadge.SetDynamicResource(
            Border.StrokeProperty,
            DeveloperModeManager.IsEnabled
                ? "PrimaryBorderBrush"
                : "BorderBrush");

        DeveloperModeStateLabel.SetDynamicResource(
            Label.TextColorProperty,
            DeveloperModeManager.IsEnabled
                ? "PrimaryColor"
                : "TextSecondaryColor");

        DeveloperModeDefaultNoteLabel.Text =
            useEnglish
                ? "Debug builds default to on; Release/Publish builds default to off. Your choice is remembered."
                : "Bản Debug mặc định bật; bản Release/Publish mặc định tắt. Lựa chọn của bạn sẽ được ghi nhớ.";

        DeveloperVisibleToolsTitleLabel.Text =
            useEnglish
                ? "Content shown while enabled"
                : "Nội dung được hiển thị khi bật";

        DeveloperLlmToolsTitleLabel.Text =
            useEnglish
                ? "AI JSON and validation logs"
                : "JSON và log kiểm tra AI";

        DeveloperLlmToolsDescriptionLabel.Text =
            useEnglish
                ? "Show LLM-generated JSON and each C# validation step."
                : "Hiện JSON do LLM tạo và từng bước validation của C#.";

        DeveloperPowerToolsTitleLabel.Text =
            useEnglish
                ? "Power and root details"
                : "Chi tiết lũy thừa và căn bậc";

        DeveloperPowerToolsDescriptionLabel.Text =
            useEnglish
                ? "Show the toggle and technical analysis of the calculation process."
                : "Hiện nút và nội dung phân tích kỹ thuật của quá trình tính toán.";

        ResetSectionTitleLabel.Text =
            useEnglish
                ? "Restore defaults"
                : "Khôi phục mặc định";

        ResetSectionDescriptionLabel.Text =
            useEnglish
                ? "Reset appearance, result display, and developer mode to their defaults."
                : "Đặt lại giao diện, hiển thị kết quả và chế độ nhà phát triển về mặc định.";

        ResetSettingsButton.Text =
            useEnglish
                ? "Restore"
                : "Khôi phục";

        SemanticProperties.SetDescription(
            FullNumberDisplaySwitch,
            useEnglish
                ? "Turn full result number display on or off"
                : "Bật hoặc tắt hiển thị kết quả đầy đủ");

        SemanticProperties.SetDescription(
            DeveloperModeSwitch,
            useEnglish
                ? "Turn developer mode on or off"
                : "Bật hoặc tắt chế độ nhà phát triển");

        // Mode-specific summary/visibility is applied after localization so a
        // language refresh does not overwrite the active background details.
        UpdateLiveWallpaperSettings();
    }

    private void LoadCurrentSettings()
    {
        Color color = AppThemeManager.CurrentAccentColor;
        string colorHex = AppThemeManager.CurrentAccentHex;

#if ANDROID
        if (AndroidMaterialYouManager.IsDynamicColorEnabled &&
            Application.Current?.Resources.TryGetValue(
                "PrimaryColor",
                out object? primaryValue) == true &&
            primaryValue is Color dynamicPrimary)
        {
            color = dynamicPrimary;
            colorHex = AppThemeManager.ToHex(dynamicPrimary);
        }

        UpdateDynamicColorSettings();
#endif

        SetColorControls(
            color,
            colorHex);

        UpdateThemeModeButtons();
        LoadLanguageSettings();
        LoadFontSettings();
        UpdateLiveWallpaperSettings();
        UpdateAdvancedSettingsState();
    }

#if ANDROID
    private void UpdateDynamicColorSettings()
    {
        _updatingDynamicColorSwitch = true;

        try
        {
            DynamicColorSwitch.IsEnabled =
                AndroidMaterialYouManager.IsDynamicColorSupported;
            DynamicColorSwitch.IsToggled =
                AndroidMaterialYouManager.IsDynamicColorEnabled;
        }
        finally
        {
            _updatingDynamicColorSwitch = false;
        }

        bool customAccentEnabled =
            !AndroidMaterialYouManager.IsDynamicColorEnabled;

        AccentColorCard.InputTransparent =
            !customAccentEnabled;
        AccentColorCard.Opacity =
            customAccentEnabled
                ? 1d
                : 0.56d;
    }
#endif

    private void LoadLanguageSettings()
    {
        _updatingLanguageSelection =
            true;

        LanguagePicker.SelectedItem =
            _languageOptions.FirstOrDefault(
                option =>
                    option.Language ==
                    AppLanguageManager.CurrentLanguage);

        _updatingLanguageSelection =
            false;
    }

    private void OnLanguageSelectionChanged(
        object? sender,
        EventArgs e)
    {
        if (_updatingLanguageSelection ||
            LanguagePicker.SelectedItem
            is not AppLanguageOption selectedLanguage)
        {
            return;
        }

        AppLanguageManager.SetLanguage(
            selectedLanguage.Language);
    }

    private void LoadFontSettings()
    {
        AppFontOption selectedFont =
            AppFontManager.CurrentFont;

        _updatingFontSelection =
            true;

        FontPicker.SelectedItem =
            _fontOptions.FirstOrDefault(
                option =>
                    option.Key ==
                    selectedFont.Key);

        _updatingFontSelection =
            false;

        UpdateFontPreview(
            selectedFont);
    }

    private void OnFontSelectionChanged(
        object? sender,
        EventArgs e)
    {
        if (_updatingFontSelection ||
            FontPicker.SelectedItem
            is not AppFontOption selectedFont)
        {
            return;
        }

        AppFontManager.SetFont(
            selectedFont.Key);

        UpdateFontPreview(
            selectedFont);
    }

    private void UpdateFontPreview(
        AppFontOption font)
    {
        // Gán trực tiếp để phần xem trước đổi ngay,
        // kể cả khi chọn font hệ thống (chuỗi rỗng).
        FontPreviewLabel.FontFamily =
            font.FontFamily;

        SelectedFontNameLabel.Text =
            font.DisplayName;
    }

    private void SetColorControls(Color color, string hexColor)
    {
        _updatingControls = true;

        RedSlider.Value = Math.Round(color.Red * 255);
        GreenSlider.Value = Math.Round(color.Green * 255);
        BlueSlider.Value = Math.Round(color.Blue * 255);
        HexColorEntry.Text = hexColor;

        UpdateRgbLabels();
        UpdateColorPreview(color);

        _updatingControls = false;
    }

    private void UpdateRgbLabels()
    {
        RedValueLabel.Text = Math.Round(RedSlider.Value).ToString();
        GreenValueLabel.Text = Math.Round(GreenSlider.Value).ToString();
        BlueValueLabel.Text = Math.Round(BlueSlider.Value).ToString();
    }

    private void UpdateColorPreview(Color color)
    {
        ColorPreviewBorder.BackgroundColor = color;
        PreviewHexLabel.Text = AppThemeManager.ToHex(color);

        Color readableText = GetReadableTextColor(color);
        PreviewTitleLabel.TextColor = readableText;
        PreviewHexLabel.TextColor = readableText;
        PreviewSampleLabel.TextColor = readableText;
    }

    private Color GetSliderColor()
    {
        return Color.FromRgb(
            (byte)Math.Round(RedSlider.Value),
            (byte)Math.Round(GreenSlider.Value),
            (byte)Math.Round(BlueSlider.Value));
    }

    private static Color GetReadableTextColor(Color background)
    {
        double luminance =
            0.2126 * background.Red +
            0.7152 * background.Green +
            0.0722 * background.Blue;

        return luminance > 0.58
            ? Color.FromArgb("#111827")
            : Colors.White;
    }

    private void UpdateThemeModeButtons()
    {
        UpdateThemeModeButton(
            SystemThemeButton,
            AppThemeManager.CurrentMode == AppThemeMode.System);

        UpdateThemeModeButton(
            LightThemeButton,
            AppThemeManager.CurrentMode == AppThemeMode.Light);

        UpdateThemeModeButton(
            DarkThemeButton,
            AppThemeManager.CurrentMode == AppThemeMode.Dark);
    }

    private static void UpdateThemeModeButton(Button button, bool selected)
    {
        if (selected)
        {
            button.SetDynamicResource(
                Button.BackgroundColorProperty,
                "PrimaryColor");

            button.SetDynamicResource(
                Button.TextColorProperty,
                "OnPrimaryColor");

            button.SetDynamicResource(
                Button.BorderColorProperty,
                "PrimaryColor");
        }
        else
        {
            button.SetDynamicResource(
                Button.BackgroundColorProperty,
                "SurfaceAltColor");

            button.SetDynamicResource(
                Button.TextColorProperty,
                "TextPrimaryColor");

            button.SetDynamicResource(
                Button.BorderColorProperty,
                "BorderColor");
        }

        button.BorderWidth = 1;
        button.CornerRadius = 10;
        button.FontAttributes = FontAttributes.Bold;
    }

    private async void OnCloseClicked(
        object? sender,
        EventArgs e)
    {
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing =
            true;

        SettingsBackButton.IsEnabled =
            false;

        try
        {
            await PlayPageExitAnimationAsync();

            if (Shell.Current is AppShell appShell)
            {
                await appShell.CloseSettingsAsync(
                    this);

                return;
            }

            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync(
                    animated: false);
            }
        }
        finally
        {
            _isClosing =
                false;

            SettingsBackButton.IsEnabled =
                true;
        }
    }

    private void ShowValidationMessage(string message)
    {
        ValidationLabel.Text = message;
        ValidationLabel.IsVisible = true;
    }

    private void HideValidationMessage()
    {
        ValidationLabel.Text = string.Empty;
        ValidationLabel.IsVisible = false;
    }
}