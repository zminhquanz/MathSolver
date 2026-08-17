using MathSolver.Services;

namespace MathSolver.Views;

public partial class SettingsPage : ContentPage
{
    private bool _updatingControls;
    private bool _updatingFontSelection;
    private bool _hasPlayedEntryAnimation;
    private bool _isClosing;
    private bool _updatingFullNumberDisplaySwitch;
    private bool _updatingDeveloperModeSwitch;

    // Picker.ItemsSource yêu cầu IList, trong khi AppFontCatalog.Options
    // được khai báo là IReadOnlyList. Tạo một List dùng chung để vừa
    // tương thích với Picker, vừa giữ đúng cùng các AppFontOption.
    private readonly List<AppFontOption> _fontOptions =
        AppFontCatalog.Options.ToList();

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

        LoadCurrentSettings();
        PreparePageEntryAnimation();
    }

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
        DeveloperModeManager.DeveloperModeChanged -= OnDeveloperModeChanged;
        ResultNumberDisplayMode.DisplayModeChanged -= OnResultNumberDisplayModeChanged;

        Shell.SetTabBarIsVisible(
            this,
            true);

        base.OnDisappearing();
    }

    private void PreparePageEntryAnimation()
    {
        SettingsPageContentRoot.Opacity =
            0d;

        SettingsPageContentRoot.TranslationX =
            42d;

        SettingsPageContentRoot.Scale =
            0.995d;
    }

    private async Task PlayPageEntryAnimationAsync()
    {
        SettingsPageContentRoot.CancelAnimations();

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
    }

    private async Task PlayPageExitAnimationAsync()
    {
        SettingsPageContentRoot.CancelAnimations();

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
        FontPicker.ItemsSource =
            null;

        FontPicker.ItemsSource =
            _fontOptions;

        LoadCurrentSettings();
        LocalizationService.RefreshAll();
        UpdateAdvancedSettingsState();
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

    private void OnVietnameseLanguageClicked(
        object? sender,
        EventArgs e)
    {
        AppLanguageManager.SetLanguage(
            AppLanguage.Vietnamese);
        UpdateLanguageButtons();
    }

    private void OnEnglishLanguageClicked(
        object? sender,
        EventArgs e)
    {
        AppLanguageManager.SetLanguage(
            AppLanguage.English);
        UpdateLanguageButtons();
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
                ? "SETTINGS"
                : "CÀI ĐẶT";

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

        SetColorControls(
            color,
            AppThemeManager.CurrentAccentHex);

        UpdateThemeModeButtons();
        UpdateLanguageButtons();
        LoadFontSettings();
        UpdateAdvancedSettingsState();
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

    private void UpdateLanguageButtons()
    {
        UpdateThemeModeButton(
            VietnameseLanguageButton,
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.Vietnamese);

        UpdateThemeModeButton(
            EnglishLanguageButton,
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English);
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