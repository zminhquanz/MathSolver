using MathSolver.Services;

namespace MathSolver.Views;

public partial class SettingsMenuPage : ContentView
{
    // SettingsMenuPage được render trong WinUI Popup ở Windows. Popup này không
    // còn nằm trong MAUI visual tree của AppShell, vì vậy DynamicResource có thể
    // giữ lại giá trị theme tại thời điểm ToPlatform() được gọi. Đặt một bản sao
    // palette ngay trên ContentView để toàn bộ descendants luôn có resource owner
    // còn sống trong Popup; mỗi ThemeChanged sẽ thay các key này và buộc MAUI
    // cập nhật BackgroundColor/TextColor/Stroke ngay khi menu vẫn đang mở.
    private static readonly string[] PopupThemeResourceKeys =
    [
        "PrimaryColor",
        "PrimaryBrush",
        "PrimaryDarkColor",
        "PrimaryDarkBrush",
        "PrimarySoftColor",
        "PrimarySoftBrush",
        "PrimaryBorderColor",
        "PrimaryBorderBrush",
        "OnPrimaryColor",
        "OnPrimaryBrush",
        "PageBackgroundColor",
        "PageBackgroundBrush",
        "SurfaceColor",
        "SurfaceBrush",
        "SurfaceAltColor",
        "SurfaceAltBrush",
        "InputBackgroundColor",
        "InputBackgroundBrush",
        "TextPrimaryColor",
        "TextPrimaryBrush",
        "TextSecondaryColor",
        "TextSecondaryBrush",
        "BorderColor",
        "BorderBrush",
        "DividerColor",
        "DividerBrush",
        "SuccessColor",
        "SuccessBrush",
        "SuccessSoftColor",
        "SuccessSoftBrush",
        "SuccessBorderColor",
        "SuccessBorderBrush",
        "WarningColor",
        "WarningBrush",
        "WarningSoftColor",
        "WarningSoftBrush",
        "WarningBorderColor",
        "WarningBorderBrush",
        "DangerColor",
        "DangerBrush",
        "DangerSoftColor",
        "DangerSoftBrush",
        "DangerBorderColor",
        "DangerBorderBrush",
        "InfoColor",
        "InfoBrush",
        "InfoSoftColor",
        "InfoSoftBrush",
        "InfoBorderColor",
        "InfoBorderBrush",
        "ShellBackgroundColor",
        "ShellBackgroundBrush",
        "ShellForegroundColor",
        "ShellForegroundBrush",
        "ShellUnselectedColor",
        "ShellUnselectedBrush",
        "Primary",
        "PrimaryDark",
        "PrimaryDarkText",
        "Secondary",
        "SecondaryDarkText",
        "Tertiary",
        "Magenta",
        "MidnightBlue"
    ];

    // Glyph › có optical center hơi lệch khi xoay 90° do font metrics.
    // Bù 1 DIP sang phải chỉ ở trạng thái mở để dấu hướng xuống nằm đúng
    // tâm hình tròn, nhưng vẫn giữ vị trí chuẩn khi đóng.
    private const double ExpandedChevronOffsetX = 1d;

    // Overlay này nằm trực tiếp trên visual tree của tab hiện tại, không phải
    // Shell route và cũng không dùng Navigation.PushModalAsync. Giữ cờ để các
    // trang cũ vẫn tương thích với logic bảo toàn GraphicsView/LLM.
    internal static bool IsTransparentOverlayActive { get; private set; }

    private readonly Dictionary<string, Button>
        _fontButtons =
            new(StringComparer.Ordinal);


    private readonly TaskCompletionSource<string?>
        _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _hasPlayedOpenAnimation;
    private bool _isFullWindowOverlayMode;
    private bool _isNavigating;
    private bool _isUpdatingFullNumberDisplaySwitch;
    private bool _isLoaded;
    private Task? _closeTask;

    private readonly HashSet<VisualElement>
        _animatingSections =
            new();

    public string? RequestedRoute { get; private set; }

    public SettingsMenuPage()
    {
        IsTransparentOverlayActive =
            true;

        // Tạo local ResourceDictionary TRƯỚC InitializeComponent để mọi
        // {DynamicResource ...} trong XAML bind ngay từ đầu vào owner nằm trên
        // chính SettingsMenuPage. Đây là điểm khác biệt quan trọng khi view sau
        // đó được ToPlatform() và đưa ra ngoài MAUI Shell vào WinUI Popup.
        Resources =
            new ResourceDictionary();

        RefreshPopupThemeResources();

        InitializeComponent();

        Loaded +=
            OnLoaded;

        Unloaded +=
            OnUnloaded;

        BuildFontOptions();
        LocalizationService.Attach(
            this);
        UpdateState();

        PrepareOpenAnimation();
    }

    public Task<string?> WaitForCloseAsync()
    {
        return _completion.Task;
    }

    internal void UseFullWindowOverlayMode(
        double overlayWidth,
        double overlayHeight,
        double buttonTop,
        double buttonRight,
        double buttonWidth,
        double buttonHeight)
    {
        _isFullWindowOverlayMode =
            true;

        OverlaySettingsButton.IsVisible =
            true;

        UpdateFullWindowOverlayLayout(
            overlayWidth,
            overlayHeight,
            buttonTop,
            buttonRight,
            buttonWidth,
            buttonHeight);
    }

    internal void UpdateFullWindowOverlayLayout(
        double overlayWidth,
        double overlayHeight,
        double buttonTop,
        double buttonRight,
        double buttonWidth,
        double buttonHeight)
    {
        if (!_isFullWindowOverlayMode)
        {
            return;
        }

        double safeWidth =
            Math.Max(
                1d,
                overlayWidth);

        double safeHeight =
            Math.Max(
                1d,
                overlayHeight);

        WidthRequest =
            safeWidth;

        HeightRequest =
            safeHeight;

        OverlaySettingsButton.WidthRequest =
            Math.Max(
                36d,
                buttonWidth);

        OverlaySettingsButton.HeightRequest =
            Math.Max(
                36d,
                buttonHeight);

        OverlaySettingsButton.Margin =
            new Thickness(
                0d,
                Math.Max(
                    0d,
                    buttonTop),
                Math.Max(
                    0d,
                    buttonRight),
                0d);

        double menuTop =
            Math.Max(
                0d,
                buttonTop +
                buttonHeight +
                4d);

        double menuRight =
            Math.Max(
                8d,
                buttonRight);

        MenuPanel.Margin =
            new Thickness(
                0d,
                menuTop,
                menuRight,
                12d);

        MenuPanel.WidthRequest =
            Math.Max(
                300d,
                Math.Min(
                    390d,
                    safeWidth - 28d));

        MenuPanel.MaximumHeightRequest =
            Math.Max(
                240d,
                safeHeight -
                menuTop -
                12d);
    }

    internal void ActivateFullWindowOverlay()
    {
        ActivateOverlayLifecycle();

    }

    private void OnLoaded(
        object? sender,
        EventArgs e)
    {
        ActivateOverlayLifecycle();
    }

    private void ActivateOverlayLifecycle()
    {
        if (!_isLoaded)
        {
            _isLoaded =
                true;

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

            RefreshPopupThemeResources();
            RefreshVectorThemeIcons();
            UpdateState();
        }

        if (_hasPlayedOpenAnimation)
        {
            return;
        }

        _hasPlayedOpenAnimation =
            true;

        Dispatcher.Dispatch(
            async () =>
                await PlayOpenAnimationAsync());
    }

    private void OnUnloaded(
        object? sender,
        EventArgs e)
    {
        ReleaseOverlayState();

        // Nếu host page bị tháo bất ngờ (đóng cửa sổ / đổi root), không để
        // AppShell chờ vô hạn ở WaitForCloseAsync.
        _completion.TrySetResult(
            null);
    }

    internal void ReleaseOverlayState()
    {
        IsTransparentOverlayActive =
            false;

        if (!_isLoaded)
        {
            return;
        }

        _isLoaded =
            false;

        AppThemeManager.ThemeChanged -=
            OnSettingsChanged;

        AppFontManager.FontChanged -=
            OnSettingsChanged;

        AppLanguageManager.LanguageChanged -=
            OnSettingsChanged;

        DeveloperModeManager.DeveloperModeChanged -=
            OnSettingsChanged;

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

        if (_isFullWindowOverlayMode)
        {
            double menuTop =
                Math.Max(
                    0d,
                    MenuPanel.Margin.Top);

            MenuPanel.MaximumHeightRequest =
                Math.Max(
                    240d,
                    height -
                    menuTop -
                    12d);

            return;
        }

        MenuPanel.MaximumHeightRequest =
            Math.Max(
                360,
                height - 28d);
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
        // AppThemeManager.ApplyCurrentTheme() cập nhật Application.Resources
        // TRƯỚC khi phát ThemeChanged. Copy palette mới xuống dictionary cục bộ
        // của Popup trước rồi mới refresh text/state. Đây là phần quan trọng để
        // menu đang mở đổi Dark <-> Light ngay lập tức trên Windows.
        RefreshPopupThemeResources();
        RefreshVectorThemeIcons();

        LocalizationService.RefreshAll();
        UpdateState();
    }

    private void RefreshVectorThemeIcons()
    {
        // SettingsMenuPage được native-embed trong WinUI Popup. DynamicResource
        // đã được đồng bộ bằng RefreshPopupThemeResources(), nhưng lifecycle
        // Loaded/Unloaded của các MAUI vector control không đáng tin cậy trong
        // Popup khi đổi Light <-> Dark. Ép từng icon đọc lại theme hiện tại.
        OverlaySettingsIcon.RefreshThemeColor();
        FontMenuIcon.RefreshThemeColor();
        LanguageMenuIcon.RefreshThemeColor();
        HardwareMenuIcon.RefreshThemeColor();
        AboutMenuIcon.RefreshThemeColor();
        ResetMenuIcon.RefreshThemeColor();
        AdvancedMenuIcon.RefreshThemeColor();
    }

    private void RefreshPopupThemeResources()
    {
        if (Application.Current?.Resources is not
            ResourceDictionary applicationResources)
        {
            return;
        }

        ResourceDictionary localResources =
            Resources;

        foreach (string key in PopupThemeResourceKeys)
        {
            if (!TryFindResource(
                    applicationResources,
                    key,
                    out object? value) ||
                value is null)
            {
                continue;
            }

            // Gán lại key ở dictionary thuộc chính SettingsMenuPage. DynamicResource
            // của các child sẽ nhận notification từ dictionary này ngay cả khi
            // toàn bộ view đã được ToPlatform() và đặt trong WinUI Popup.
            localResources[key] =
                CloneThemeResourceValue(
                    value);
        }
    }

    private static object CloneThemeResourceValue(
        object value)
    {
        // Brush là mutable. Tạo instance mới để việc thay theme luôn tạo một
        // property change rõ ràng cho native view thay vì giữ reference cũ.
        if (value is SolidColorBrush brush)
        {
            return new SolidColorBrush(
                brush.Color);
        }

        return value;
    }

    private static bool TryFindResource(
        ResourceDictionary resources,
        string key,
        out object? value)
    {
        if (resources.TryGetValue(
                key,
                out value))
        {
            return true;
        }

        // Phòng trường hợp một theme key sau này được chuyển vào merged dictionary.
        // Duyệt từ cuối về đầu để giữ đúng precedence của MAUI resources.
        var mergedDictionaries =
            new List<ResourceDictionary>(
                resources.MergedDictionaries);

        for (int index =
                 mergedDictionaries.Count - 1;
             index >= 0;
             index--)
        {
            if (TryFindResource(
                    mergedDictionaries[index],
                    key,
                    out value))
            {
                return true;
            }
        }

        value =
            null;

        return false;
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
    private void UpdateState()
    {
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

        NumberDisplaySummaryLabel.Text =
            LocalizationService.TranslateKey(
                ResultNumberDisplayMode.ShowFullNumbers
                    ? "Settings.NumberDisplay.SummaryFull"
                    : "Settings.NumberDisplay.SummaryCompact");

        _isUpdatingFullNumberDisplaySwitch =
            true;

        try
        {
            FullNumberDisplaySwitch.IsToggled =
                ResultNumberDisplayMode.ShowFullNumbers;
        }
        finally
        {
            _isUpdatingFullNumberDisplaySwitch =
                false;
        }

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
            ThemeChevronLabel);
    }

    private async void OnAccentRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        await ToggleSectionAsync(
            AccentOptionsBorder,
            AccentChevronLabel);
    }

    private async void OnFontRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        await ToggleSectionAsync(
            FontOptionsBorder,
            FontChevronLabel);
    }

    private async void OnLanguageRowTapped(
        object? sender,
        TappedEventArgs e)
    {
        await ToggleSectionAsync(
            LanguageOptionsBorder,
            LanguageChevronLabel);
    }

    private async Task ToggleSectionAsync(
        VisualElement section,
        Label chevron)
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
        chevron.CancelAnimations();

        // Giữ đúng một glyph › trong pill và xoay chính Label như icon SVG:
        // 0° khi đóng, 90° khi mở. HashSet phía trên khóa riêng từng section
        // nên bấm liên tục không thể làm chevron kẹt giữa hai trạng thái.
        chevron.Text = "›";

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

                    chevron.RotateToAsync(
                        90d,
                        210,
                        Easing.CubicOut),

                    chevron.TranslateToAsync(
                        ExpandedChevronOffsetX,
                        0d,
                        210,
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

                    chevron.RotateToAsync(
                        0d,
                        145,
                        Easing.CubicIn),

                    chevron.TranslateToAsync(
                        0d,
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
            // Bảo đảm trạng thái cuối luôn chính xác nếu animation bị hủy do
            // trang đóng hoặc vòng đời giao diện thay đổi giữa chừng.
            chevron.Rotation =
                isExpanding
                    ? 90d
                    : 0d;

            chevron.TranslationX =
                isExpanding
                    ? ExpandedChevronOffsetX
                    : 0d;

            chevron.TranslationY = 0d;

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

    private void OnFullNumberDisplayToggled(
        object? sender,
        ToggledEventArgs e)
    {
        if (_isUpdatingFullNumberDisplaySwitch)
        {
            return;
        }

        ResultNumberDisplayMode.SetShowFullNumbers(
            e.Value);

        UpdateState();
    }

    private void OnResetTapped(
        object? sender,
        TappedEventArgs e)
    {
        AppThemeManager.ResetToDefault();
        AppFontManager.ResetToDefault();
        AppLanguageManager.ResetToDefault();
        DeveloperModeManager.ResetToDefault();
        ResultNumberDisplayMode.ResetToDefault();
        UpdateState();
    }

    private async void OnHardwarePerformanceTapped(
        object? sender,
        TappedEventArgs e)
    {
        await RequestNavigationAsync(
            nameof(HardwarePerformancePage));
    }

    private async void OnDeveloperModeTapped(
        object? sender,
        TappedEventArgs e)
    {
        await RequestNavigationAsync(
            nameof(DeveloperModePage));
    }

    private async void OnAboutTapped(
        object? sender,
        TappedEventArgs e)
    {
        await RequestNavigationAsync(
            nameof(AboutPage));
    }

    private async void OnAdvancedColorClicked(
        object? sender,
        EventArgs e)
    {
        await RequestNavigationAsync(
            nameof(SettingsPage));
    }

    private async void OnAdvancedSettingsTapped(
        object? sender,
        TappedEventArgs e)
    {
        await RequestNavigationAsync(
            nameof(SettingsPage));
    }

    private async Task RequestNavigationAsync(
        string route)
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating =
            true;

        RequestedRoute =
            route;

        try
        {
            await CloseAsync();
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

    private async void OnOverlaySettingsButtonClicked(
        object? sender,
        EventArgs e)
    {
        await CloseAsync();
    }

    private async void OnCloseClicked(
        object? sender,
        EventArgs e)
    {
        await CloseAsync();
    }

    private Task CloseAsync()
    {
        if (_closeTask is not null)
        {
            return _closeTask;
        }

        _closeTask =
            CloseCoreAsync();

        return _closeTask;
    }

    private async Task CloseCoreAsync()
    {
        try
        {
            await PlayCloseAnimationAsync();

            IsTransparentOverlayActive =
                false;

            _completion.TrySetResult(
                RequestedRoute);
        }
        finally
        {
            _closeTask =
                null;
        }
    }
}
