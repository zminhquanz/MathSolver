using MathSolver.Services;

namespace MathSolver.Views;

public partial class SettingsMenuPage : ContentPage
{
    private readonly Dictionary<string, Button>
        _fontButtons =
            new(StringComparer.Ordinal);

    public SettingsMenuPage()
    {
        InitializeComponent();

        Shell.SetNavBarIsVisible(
            this,
            false);

        Shell.SetTabBarIsVisible(
            this,
            false);

        BuildFontOptions();
        LocalizationService.Attach(
            this);
        UpdateState();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        AppThemeManager.ThemeChanged +=
            OnSettingsChanged;

        AppFontManager.FontChanged +=
            OnSettingsChanged;

        AppLanguageManager.LanguageChanged +=
            OnSettingsChanged;

        LocalizationService.Attach(
            this);
        UpdateState();
    }

    protected override void OnDisappearing()
    {
        AppThemeManager.ThemeChanged -=
            OnSettingsChanged;

        AppFontManager.FontChanged -=
            OnSettingsChanged;

        AppLanguageManager.LanguageChanged -=
            OnSettingsChanged;

        base.OnDisappearing();
    }

    protected override void OnSizeAllocated(
        double width,
        double height)
    {
        base.OnSizeAllocated(
            width,
            height);

        MenuPanel.WidthRequest =
            Math.Max(
                300,
                Math.Min(
                    390,
                    width - 28));

        MenuPanel.MaximumHeightRequest =
            Math.Max(
                360,
                height - 28);
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private void OnSettingsChanged(
        object? sender,
        EventArgs e)
    {
        LocalizationService.RefreshAll();
        UpdateState();
    }

    private void BuildFontOptions()
    {
        FontOptionsLayout.Children.Clear();
        _fontButtons.Clear();

        foreach (AppFontOption font
                 in AppFontCatalog.Options)
        {
            var button =
                new Button
                {
                    Text =
                        font.DisplayName,

                    FontFamily =
                        font.FontFamily,

                    HorizontalOptions =
                        LayoutOptions.Fill,

                    CommandParameter =
                        font.Key,

                    Padding =
                        new Thickness(
                            12,
                            8),

                    MinimumHeightRequest =
                        42
                };

            button.Clicked +=
                OnFontClicked;

            _fontButtons[font.Key] =
                button;

            FontOptionsLayout.Children.Add(
                button);
        }
    }
    private void UpdateSettingsIconTint()
    {
        if (!TryGetThemeColor(
                "TextPrimaryColor",
                out Color tintColor))
        {
            return;
        }

        MoreSettingsIconTintBehavior.TintColor = tintColor;
        FontIconTintBehavior.TintColor = tintColor;
        LanguageIconTintBehavior.TintColor = tintColor;
    }

    private static bool TryGetThemeColor(
        string resourceKey,
        out Color color)
    {
        color =
            Colors.Transparent;

        if (Application.Current?.Resources is not
            ResourceDictionary resources)
        {
            return false;
        }

        return TryGetThemeColorNextStep(
            resources,
            resourceKey,
            out color);
    }

    private static bool TryGetThemeColorNextStep(
        ResourceDictionary resources,
        string resourceKey,
        out Color color)
    {
        // Mỗi phương thức có tham số out phải gán giá trị trên mọi
        // nhánh thoát, kể cả khi không tìm thấy resource.
        color =
            Colors.Transparent;

        if (resources.TryGetValue(
                resourceKey,
                out object? resourceValue))
        {
            if (resourceValue is Color resourceColor)
            {
                color =
                    resourceColor;

                return true;
            }

            if (resourceValue is SolidColorBrush resourceBrush)
            {
                color =
                    resourceBrush.Color;

                return true;
            }
        }

        // MergedDictionaries có kiểu ICollection<ResourceDictionary>,
        // nên không thể truy cập trực tiếp bằng toán tử [index].
        // Chép sang List để duyệt ngược theo đúng thứ tự ưu tiên.
        var mergedDictionaries =
            new List<ResourceDictionary>(
                resources.MergedDictionaries);

        for (int index =
                 mergedDictionaries.Count - 1;
             index >= 0;
             index--)
        {
            if (TryGetThemeColorNextStep(
                    mergedDictionaries[index],
                    resourceKey,
                    out color))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateState()
    {
        UpdateSettingsIconTint();
        ThemeSummaryLabel.Text =
            AppThemeManager.CurrentMode switch
            {
                AppThemeMode.Light =>
                    "Sáng",

                AppThemeMode.Dark =>
                    "Tối",

                _ =>
                    "Hệ thống"
            };

        AccentSummaryLabel.Text =
            AppThemeManager.CurrentAccentHex;

        AccentSummaryLabel.TextColor =
            AppThemeManager.CurrentAccentColor;

        FontSummaryLabel.Text =
            AppFontManager.CurrentFont.DisplayName;

        LanguageSummaryLabel.Text =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English
                ? "Tiếng Anh"
                : "Tiếng Việt";

        UpdateChoiceButton(
            SystemThemeButton,
            AppThemeManager.CurrentMode ==
            AppThemeMode.System);

        UpdateChoiceButton(
            LightThemeButton,
            AppThemeManager.CurrentMode ==
            AppThemeMode.Light);

        UpdateChoiceButton(
            DarkThemeButton,
            AppThemeManager.CurrentMode ==
            AppThemeMode.Dark);

        UpdateChoiceButton(
            VietnameseLanguageButton,
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.Vietnamese);

        UpdateChoiceButton(
            EnglishLanguageButton,
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English);

        foreach ((string key, Button button)
                 in _fontButtons)
        {
            UpdateChoiceButton(
                button,
                key ==
                AppFontManager.CurrentFontKey);
        }

        LocalizationService.Attach(
            this);
    }

    private static void UpdateChoiceButton(
        Button button,
        bool isSelected)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            isSelected
                ? "PrimaryColor"
                : "SurfaceColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            isSelected
                ? "OnPrimaryColor"
                : "TextPrimaryColor");

        button.SetDynamicResource(
            Button.BorderColorProperty,
            isSelected
                ? "PrimaryColor"
                : "BorderColor");

        button.BorderWidth =
            1;

        button.CornerRadius =
            9;
    }

    private void OnThemeRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        ToggleSection(
            ThemeOptionsBorder,
            ThemeChevronLabel);
    }

    private void OnAccentRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        ToggleSection(
            AccentOptionsBorder,
            AccentChevronLabel);
    }

    private void OnFontRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        ToggleSection(
            FontOptionsBorder,
            FontChevronLabel);
    }

    private void OnLanguageRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        ToggleSection(
            LanguageOptionsBorder,
            LanguageChevronLabel);
    }

    private static void ToggleSection(
        VisualElement section,
        Label chevron)
    {
        bool isExpanded =
            !section.IsVisible;

        section.IsVisible =
            isExpanded;

        // Giữ nguyên một glyph chevron rồi xoay nó, thay vì đổi sang
        // ký tự ⌄ vốn có baseline thấp và hình dáng không cân đối.
        chevron.Text =
            "›";

        chevron.Rotation =
            isExpanded
                ? 90d
                : 0d;

        chevron.SetDynamicResource(
            Label.TextColorProperty,
            isExpanded
                ? "PrimaryColor"
                : "TextSecondaryColor");
    }

    private void OnSystemThemeClicked(
        object? sender,
        EventArgs e)
    {
        AppThemeManager.SetThemeMode(
            AppThemeMode.System);
        UpdateState();
    }

    private void OnLightThemeClicked(
        object? sender,
        EventArgs e)
    {
        AppThemeManager.SetThemeMode(
            AppThemeMode.Light);
        UpdateState();
    }

    private void OnDarkThemeClicked(
        object? sender,
        EventArgs e)
    {
        AppThemeManager.SetThemeMode(
            AppThemeMode.Dark);
        UpdateState();
    }

    private void OnPresetColorClicked(
        object? sender,
        EventArgs e)
    {
        if (sender is not Button button ||
            button.CommandParameter is not string color)
        {
            return;
        }

        AppThemeManager.SetAccentColor(
            color);
        UpdateState();
    }

    private void OnFontClicked(
        object? sender,
        EventArgs e)
    {
        if (sender is not Button button ||
            button.CommandParameter is not string key)
        {
            return;
        }

        AppFontManager.SetFont(
            key);
        UpdateState();
    }

    private void OnVietnameseClicked(
        object? sender,
        EventArgs e)
    {
        AppLanguageManager.SetLanguage(
            AppLanguage.Vietnamese);
        UpdateState();
    }

    private void OnEnglishClicked(
        object? sender,
        EventArgs e)
    {
        AppLanguageManager.SetLanguage(
            AppLanguage.English);
        UpdateState();
    }

    private void OnResetTapped(
        object? sender,
        TappedEventArgs e)
    {
        AppThemeManager.ResetToDefault();
        AppFontManager.ResetToDefault();
        AppLanguageManager.ResetToDefault();
        UpdateState();
    }

    private async void OnAdvancedColorClicked(
        object? sender,
        EventArgs e)
    {
        await OpenAdvancedSettingsAsync();
    }

    private async void OnAdvancedSettingsTapped(
        object? sender,
        TappedEventArgs e)
    {
        await OpenAdvancedSettingsAsync();
    }

    private async Task OpenAdvancedSettingsAsync()
    {
        await CloseAsync();

        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync(
                nameof(SettingsPage));
        }
    }

    private async void OnOutsideTapped(
        object? sender,
        TappedEventArgs e)
    {
        await CloseAsync();
    }

    private async void OnCloseClicked(
        object? sender,
        EventArgs e)
    {
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (Navigation.ModalStack.Contains(
                this))
        {
            await Navigation.PopModalAsync(
                animated: false);
        }
    }
}