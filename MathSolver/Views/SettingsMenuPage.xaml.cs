using MathSolver.Services;

namespace MathSolver.Views;

public partial class SettingsMenuPage : ContentPage
{
    // Được bật ngay từ constructor, trước khi PushModalAsync làm trang bên
    // dưới nhận OnDisappearing. FormulaPage dùng cờ này để biết rằng nó chỉ
    // đang bị một overlay trong suốt che lên và không được tự ẩn nội dung.
    internal static bool IsTransparentOverlayActive { get; private set; }

    private readonly Dictionary<string, Button>
        _fontButtons =
            new(StringComparer.Ordinal);

    private bool _hasPlayedOpenAnimation;
    private bool _isClosing;
    private bool _isNavigating;
    private bool _isAnimatingDeveloperModeButton;

    private readonly HashSet<VisualElement>
        _animatingSections =
            new();

    public SettingsMenuPage()
    {
        IsTransparentOverlayActive =
            true;

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

        PrepareOpenAnimation();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        IsTransparentOverlayActive =
            true;

        AppThemeManager.ThemeChanged +=
            OnSettingsChanged;

        AppFontManager.FontChanged +=
            OnSettingsChanged;

        AppLanguageManager.LanguageChanged +=
            OnSettingsChanged;

        DeveloperModeManager.DeveloperModeChanged +=
            OnSettingsChanged;

        LocalizationService.Attach(
            this);
        UpdateState();

        if (!_hasPlayedOpenAnimation)
        {
            _hasPlayedOpenAnimation =
                true;

            Dispatcher.Dispatch(
                async () =>
                    await PlayOpenAnimationAsync());
        }
    }

    protected override void OnDisappearing()
    {
        IsTransparentOverlayActive =
            false;

        AppThemeManager.ThemeChanged -=
            OnSettingsChanged;

        AppFontManager.FontChanged -=
            OnSettingsChanged;

        AppLanguageManager.LanguageChanged -=
            OnSettingsChanged;

        DeveloperModeManager.DeveloperModeChanged -=
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

    private void PrepareOpenAnimation()
    {
        OverlayScrim.Opacity =
            0d;

        MenuPanel.Opacity =
            0d;

        MenuPanel.TranslationX =
            56d;

        MenuPanel.Scale =
            0.985d;
    }

    private async Task PlayOpenAnimationAsync()
    {
        OverlayScrim.CancelAnimations();
        MenuPanel.CancelAnimations();

        await Task.WhenAll(
            OverlayScrim.FadeToAsync(
                1d,
                170,
                Easing.CubicOut),

            MenuPanel.FadeToAsync(
                1d,
                150,
                Easing.CubicOut),

            MenuPanel.TranslateToAsync(
                0d,
                0d,
                220,
                Easing.CubicOut),

            MenuPanel.ScaleToAsync(
                1d,
                220,
                Easing.CubicOut));
    }

    private async Task PlayCloseAnimationAsync()
    {
        OverlayRoot.InputTransparent =
            true;

        OverlayScrim.CancelAnimations();
        MenuPanel.CancelAnimations();

        await Task.WhenAll(
            OverlayScrim.FadeToAsync(
                0d,
                150,
                Easing.CubicIn),

            MenuPanel.FadeToAsync(
                0d,
                130,
                Easing.CubicIn),

            MenuPanel.TranslateToAsync(
                48d,
                0d,
                165,
                Easing.CubicIn),

            MenuPanel.ScaleToAsync(
                0.985d,
                165,
                Easing.CubicIn));
    }

    public Task CloseWithAnimationAsync()
    {
        return CloseAsync();
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

        ResetSettingsIconTintBehavior.TintColor = tintColor;
        MoreSettingsIconTintBehavior.TintColor = tintColor;
        FontIconTintBehavior.TintColor = tintColor;
        LanguageIconTintBehavior.TintColor = tintColor;
        ThemeArrowForwardIconTintBehavior.TintColor = tintColor;
        ColorThemeArrowForwardIconTintBehavior.TintColor = tintColor;
        FontArrowForwardIconTintBehavior.TintColor = tintColor;
        LanguageArrowForwardIconTintBehavior.TintColor = tintColor;
        BenchmarkIconTintBehavior.TintColor = tintColor;
        AboutIconTintBehavior.TintColor = tintColor;
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

        bool useEnglish =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English;

        AboutMenuTitleLabel.Text =
            useEnglish
                ? "About"
                : "Giới thiệu";

        AboutMenuSummaryLabel.Text =
            useEnglish
                ? "Application, author, and version information"
                : "Thông tin ứng dụng, tác giả và phiên bản";

        DeveloperModeTitleLabel.Text =
            useEnglish
                ? "Developer mode"
                : "Chế độ nhà phát triển";

        DeveloperModeSummaryLabel.Text =
            (useEnglish, DeveloperModeManager.IsEnabled) switch
            {
                (true, true) =>
                    "On · Show JSON, logs, and technical details",
                (true, false) =>
                    "Off · Hide JSON, logs, and technical details",
                (false, true) =>
                    "Đang bật · Hiện JSON, log và chi tiết kỹ thuật",
                _ =>
                    "Đang tắt · Ẩn JSON, log và chi tiết kỹ thuật"
            };

        SemanticProperties.SetDescription(
            DeveloperModeToggleButton,
            useEnglish
                ? "Turn developer mode on or off"
                : "Bật hoặc tắt chế độ nhà phát triển");

        UpdateDeveloperModeButton(
            useEnglish);

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

    private async void OnThemeRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        await ToggleSectionAsync(
            ThemeOptionsBorder,
            ThemeArrowImage);
    }

    private async void OnAccentRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        await ToggleSectionAsync(
            AccentOptionsBorder,
            AccentArrowImage);
    }

    private async void OnFontRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        await ToggleSectionAsync(
            FontOptionsBorder,
            FontArrowImage);
    }

    private async void OnLanguageRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        await ToggleSectionAsync(
            LanguageOptionsBorder,
            LanguageArrowImage);
    }

    private async Task ToggleSectionAsync(
        VisualElement section,
        VisualElement arrow)
    {
        if (!_animatingSections.Add(
                section))
        {
            return;
        }

        bool isExpanding =
            !section.IsVisible;

        section.InputTransparent =
            true;

        section.CancelAnimations();
        arrow.CancelAnimations();

        try
        {
            if (isExpanding)
            {
                section.IsVisible =
                    true;

                section.Opacity =
                    0d;

                section.TranslationY =
                    -10d;

                section.ScaleY =
                    0.82d;

                await Task.Yield();

                await Task.WhenAll(
                    section.FadeToAsync(
                        1d,
                        150,
                        Easing.CubicOut),

                    section.TranslateToAsync(
                        0d,
                        0d,
                        210,
                        Easing.CubicOut),

                    section.ScaleYToAsync(
                        1d,
                        210,
                        Easing.CubicOut),

                    arrow.RotateToAsync(
                        90d,
                        180,
                        Easing.CubicOut));
            }
            else
            {
                await Task.WhenAll(
                    section.FadeToAsync(
                        0d,
                        115,
                        Easing.CubicIn),

                    section.TranslateToAsync(
                        0d,
                        -8d,
                        145,
                        Easing.CubicIn),

                    section.ScaleYToAsync(
                        0.82d,
                        145,
                        Easing.CubicIn),

                    arrow.RotateToAsync(
                        0d,
                        145,
                        Easing.CubicIn));

                section.IsVisible =
                    false;

                // Đặt lại để lần mở tiếp theo luôn bắt đầu ổn định.
                section.Opacity =
                    1d;

                section.TranslationY =
                    0d;

                section.ScaleY =
                    1d;
            }
        }
        finally
        {
            section.InputTransparent =
                false;

            _animatingSections.Remove(
                section);
        }
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

    private void UpdateDeveloperModeButton(
        bool useEnglish)
    {
        bool isEnabled =
            DeveloperModeManager.IsEnabled;

        DeveloperModeToggleButton.Text =
            (useEnglish, isEnabled) switch
            {
                (true, true) => "✓ ON",
                (true, false) => "○ OFF",
                (false, true) => "✓ BẬT",
                _ => "○ TẮT"
            };

        DeveloperModeToggleButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            isEnabled
                ? "PrimaryColor"
                : "SurfaceAltColor");

        DeveloperModeToggleButton.SetDynamicResource(
            Button.TextColorProperty,
            isEnabled
                ? "OnPrimaryColor"
                : "TextSecondaryColor");

        DeveloperModeToggleButton.SetDynamicResource(
            Button.BorderColorProperty,
            isEnabled
                ? "PrimaryColor"
                : "BorderColor");

        SemanticProperties.SetHint(
            DeveloperModeToggleButton,
            (useEnglish, isEnabled) switch
            {
                (true, true) => "Developer mode is on. Activate to turn it off.",
                (true, false) => "Developer mode is off. Activate to turn it on.",
                (false, true) => "Chế độ nhà phát triển đang bật. Nhấn để tắt.",
                _ => "Chế độ nhà phát triển đang tắt. Nhấn để bật."
            });
    }

    private async void OnDeveloperModeClicked(
        object? sender,
        EventArgs e)
    {
        if (_isAnimatingDeveloperModeButton)
        {
            return;
        }

        _isAnimatingDeveloperModeButton = true;

        try
        {
            DeveloperModeManager.SetEnabled(
                !DeveloperModeManager.IsEnabled);

            UpdateState();

            DeveloperModeToggleButton.CancelAnimations();

            await DeveloperModeToggleButton.ScaleToAsync(
                0.94d,
                70,
                Easing.CubicOut);

            await DeveloperModeToggleButton.ScaleToAsync(
                1d,
                110,
                Easing.CubicOut);
        }
        finally
        {
            _isAnimatingDeveloperModeButton = false;
        }
    }

    private void OnResetTapped(
        object? sender,
        TappedEventArgs e)
    {
        AppThemeManager.ResetToDefault();
        AppFontManager.ResetToDefault();
        AppLanguageManager.ResetToDefault();
        DeveloperModeManager.ResetToDefault();
        UpdateState();
    }

    private async void OnHardwarePerformanceTapped(
        object? sender,
        TappedEventArgs e)
    {
        await OpenHardwarePerformancePageAsync();
    }

    private async Task OpenHardwarePerformancePageAsync()
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating =
            true;

        try
        {
            await CloseAsync();

            if (Shell.Current is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(
                nameof(HardwarePerformancePage),
                animate:
                    false);
        }
        finally
        {
            _isNavigating =
                false;
        }
    }

    private async void OnAboutTapped(
        object? sender,
        TappedEventArgs e)
    {
        await OpenAboutPageAsync();
    }

    private async Task OpenAboutPageAsync()
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating =
            true;

        try
        {
            await CloseAsync();

            if (Shell.Current is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(
                nameof(AboutPage),
                animate:
                    false);
        }
        finally
        {
            _isNavigating =
                false;
        }
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
        if (_isNavigating)
        {
            return;
        }

        _isNavigating =
            true;

        try
        {
            await CloseAsync();

            if (Shell.Current is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(
                nameof(SettingsPage),
                animate:
                    false);
        }
        finally
        {
            _isNavigating =
                false;
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
        if (_isClosing)
        {
            return;
        }

        _isClosing =
            true;

        try
        {
            await PlayCloseAnimationAsync();

            if (Navigation.ModalStack.Contains(
                    this))
            {
                await Navigation.PopModalAsync(
                    animated:
                        false);
            }
        }
        finally
        {
            _isClosing =
                false;
        }
    }
}
