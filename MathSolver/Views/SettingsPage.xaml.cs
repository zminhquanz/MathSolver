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

            await LiveWallpaperManager.ImportMp4Async(
                selected);

            UpdateLiveWallpaperSettings();
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

        _updatingLiveWallpaperSwitch = true;

        try
        {
            LiveWallpaperSwitch.IsEnabled =
                hasWallpaper;
            LiveWallpaperSwitch.IsToggled =
                LiveWallpaperManager.IsEnabled;
        }
        finally
        {
            _updatingLiveWallpaperSwitch = false;
        }

        RemoveLiveWallpaperButton.IsEnabled =
            hasWallpaper;

        bool useEnglish =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English;

        LiveWallpaperFileNameLabel.Text =
            hasWallpaper
                ? LiveWallpaperManager.OriginalFileName ??
                    Path.GetFileName(LiveWallpaperManager.WallpaperPath)
                : (useEnglish
                    ? "No MP4 selected"
                    : "Chưa chọn file MP4");
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
                ? "Use your own MP4 video as an animated background for the main learning tabs."
                : "Dùng video MP4 của bạn làm hình nền chuyển động cho các tab học toán.";

        LiveWallpaperEnabledTitleLabel.Text =
            useEnglish
                ? "Enable animated wallpaper"
                : "Bật hình nền động";

        LiveWallpaperEnabledSummaryLabel.Text =
            useEnglish
                ? "H.264 video loops silently with native hardware-preferred decoding and pauses for local AI inference."
                : "Video H.264 tự lặp, tắt tiếng, ưu tiên giải mã phần cứng native và tạm dừng khi AI local tạo sinh.";

        LiveWallpaperDecodeTitleLabel.Text =
            useEnglish
                ? "Video decoding"
                : "Giải mã video";

        LiveWallpaperDecodeValueLabel.Text =
            useEnglish
                ? "H.264 / AVC • hardware preferred"
                : "H.264 / AVC • phần cứng ưu tiên";

        LiveWallpaperFileTitleLabel.Text =
            useEnglish
                ? "MP4 file"
                : "File MP4";

        SelectLiveWallpaperButton.Text =
            useEnglish
                ? "Choose MP4"
                : "Chọn MP4";

        RemoveLiveWallpaperButton.Text =
            useEnglish
                ? "Remove wallpaper"
                : "Gỡ hình nền";

        LiveWallpaperNoteLabel.Text =
            useEnglish
                ? "MP4 wallpapers must use H.264 / AVC. Windows uses MediaPlayer/Media Foundation and Android uses ExoPlayer/MediaCodec; playback is paused while local AI is generating to reserve compute and memory bandwidth."
                : "Hình nền MP4 phải dùng H.264 / AVC. Windows dùng MediaPlayer/Media Foundation, Android dùng ExoPlayer/MediaCodec; video sẽ tạm dừng khi AI local tạo sinh để nhường compute và băng thông bộ nhớ.";

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