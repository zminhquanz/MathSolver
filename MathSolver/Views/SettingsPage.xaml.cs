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

    // Picker.ItemsSource yêu cầu IList, trong khi AppFontCatalog.Options
    // được khai báo là IReadOnlyList. Tạo một List dùng chung để vừa
    // tương thích với Picker, vừa giữ đúng cùng các AppFontOption.
    private readonly List<AppFontOption> _fontOptions =
        AppFontCatalog.Options.ToList();

    private readonly List<AppLanguageOption> _languageOptions =
        AppLanguageCatalog.Options.ToList();

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

        LoadCurrentSettings();

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