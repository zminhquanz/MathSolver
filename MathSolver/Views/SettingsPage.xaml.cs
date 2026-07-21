using MathSolver.Services;

namespace MathSolver.Views;

public partial class SettingsPage : ContentPage
{
    private bool _updatingControls;
    private bool _updatingFontSelection;

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

        // Settings là một màn hình riêng, không hiển thị ba tab chính.
        Shell.SetTabBarIsVisible(this, false);
        Shell.SetNavBarIsVisible(this, true);

        FontPicker.ItemsSource =
            _fontOptions;

        LoadCurrentSettings();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        AppThemeManager.ThemeChanged += OnThemeChanged;
        AppFontManager.FontChanged += OnFontChanged;
        AppLanguageManager.LanguageChanged += OnLanguageChanged;

        LoadCurrentSettings();
    }

    protected override void OnDisappearing()
    {
        AppThemeManager.ThemeChanged -= OnThemeChanged;
        AppFontManager.FontChanged -= OnFontChanged;
        AppLanguageManager.LanguageChanged -= OnLanguageChanged;

        base.OnDisappearing();
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
    }

    private void OnVietnameseLanguageClicked(
        object sender,
        EventArgs e)
    {
        AppLanguageManager.SetLanguage(
            AppLanguage.Vietnamese);
        UpdateLanguageButtons();
    }

    private void OnEnglishLanguageClicked(
        object sender,
        EventArgs e)
    {
        AppLanguageManager.SetLanguage(
            AppLanguage.English);
        UpdateLanguageButtons();
    }

    private void OnSystemThemeClicked(object sender, EventArgs e)
    {
        AppThemeManager.SetThemeMode(AppThemeMode.System);
        UpdateThemeModeButtons();
    }

    private void OnLightThemeClicked(object sender, EventArgs e)
    {
        AppThemeManager.SetThemeMode(AppThemeMode.Light);
        UpdateThemeModeButtons();
    }

    private void OnDarkThemeClicked(object sender, EventArgs e)
    {
        AppThemeManager.SetThemeMode(AppThemeMode.Dark);
        UpdateThemeModeButtons();
    }

    private void OnPresetColorClicked(object sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.CommandParameter is not string hexColor)
        {
            return;
        }

        ApplyHexColor(hexColor);
    }

    private void OnApplyHexClicked(object sender, EventArgs e)
    {
        ApplyHexColor(HexColorEntry.Text);
    }

    private void OnRgbValueChanged(object sender, ValueChangedEventArgs e)
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

    private void OnRgbDragCompleted(object sender, EventArgs e)
    {
        if (_updatingControls)
        {
            return;
        }

        AppThemeManager.SetAccentColor(GetSliderColor());
    }

    private void OnResetClicked(object sender, EventArgs e)
    {
        AppThemeManager.ResetToDefault();
        AppFontManager.ResetToDefault();
        AppLanguageManager.ResetToDefault();

        LoadCurrentSettings();
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

    private void LoadCurrentSettings()
    {
        Color color = AppThemeManager.CurrentAccentColor;

        SetColorControls(
            color,
            AppThemeManager.CurrentAccentHex);

        UpdateThemeModeButtons();
        UpdateLanguageButtons();
        LoadFontSettings();
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
        object sender,
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