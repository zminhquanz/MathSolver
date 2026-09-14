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

    // Overlay này nằm trực tiếp trên visual tree của tab hiện tại, không phải
    // Shell route và cũng không dùng Navigation.PushModalAsync. Giữ cờ để các
    // trang cũ vẫn tương thích với logic bảo toàn GraphicsView/LLM.
    internal static bool IsTransparentOverlayActive { get; private set; }

    private readonly List<AppFontOption> _fontOptions =
        AppFontCatalog.Options.ToList();

    private bool _updatingPickerSelections;

    private readonly TaskCompletionSource<string?>
        _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _hasPlayedOpenAnimation;
    private bool _hasPlayedSettingsButtonOpenAnimation;
    private bool _isFullWindowOverlayMode;
    private bool _isNavigating;
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
                    420d,
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

            LocalizationService.CultureChanged +=
                OnLocalizationCultureChanged;

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

        LocalizationService.CultureChanged -=
            OnLocalizationCultureChanged;

    }

    protected override void OnSizeAllocated(
        double width,
        double height)
    {
        base.OnSizeAllocated(
            width,
            height);

#if ANDROID
        // Trên điện thoại, SettingsMenu là bottom sheet gần full-width.
        // Không dùng giới hạn 300-420 DIP của popup desktop vì sẽ để lại
        // khoảng trống hai bên không cần thiết trên Pixel-class devices.
        MenuPanel.WidthRequest =
            Math.Max(
                280d,
                width - 16d);
#else
        // Giữ nguyên kích thước popup Windows đã ổn định.
        MenuPanel.WidthRequest =
            Math.Max(
                300,
                Math.Min(
                    420,
                    width - 28));
#endif

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

#if ANDROID
        MenuPanel.MaximumHeightRequest =
            Math.Max(
                320d,
                Math.Min(
                    620d,
                    height - 16d));
#else
        MenuPanel.MaximumHeightRequest =
            Math.Max(
                360,
                height - 28d);
#endif
    }

    private void PrepareOpenAnimation()
    {
        OverlayScrim.Opacity =
            0d;

        MenuPanel.Opacity =
            0d;

#if ANDROID
        // Bottom-sheet motion cho mobile: đi theo trục dọc, tránh cảm giác
        // side-panel desktop khi panel đã được neo ở đáy màn hình.
        MenuPanel.TranslationX =
            0d;

        MenuPanel.TranslationY =
            32d;

        MenuPanel.Scale =
            0.995d;
#else
        MenuPanel.TranslationX =
            56d;

        MenuPanel.Scale =
            0.985d;
#endif
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

    internal void ActivateSettingsButtonOpenAnimation(
        double initialScale,
        double initialRotation)
    {
        if (!_isFullWindowOverlayMode ||
            _hasPlayedSettingsButtonOpenAnimation)
        {
            return;
        }

        _hasPlayedSettingsButtonOpenAnimation = true;

        // Gọi từ native WinUI Loaded của Popup. Nhờ vậy animation bắt đầu đúng
        // lúc gear overlay thật sự đã render, không bị chạy xong trong khoảng
        // ToPlatform()/Popup attach khi người dùng chưa nhìn thấy nó. Scale và
        // rotation hiện tại của gear Shell được truyền sang để frame handoff
        // không nhảy hình.
        Dispatcher.Dispatch(
            async () =>
                await PlayOverlaySettingsOpenAnimationAsync(
                    initialScale,
                    initialRotation));
    }

    private async Task PlayOverlaySettingsOpenAnimationAsync(
        double initialScale,
        double initialRotation)
    {
        // Cùng nhịp với gear thật của AppShell nhưng animate trực tiếp SVG,
        // không transform Grid 42x42 dùng để neo vị trí popup. Nhờ vậy icon
        // phản hồi ngay khi menu bắt đầu hiện mà không bị dịch vài pixel.
        const double restScale = 0.833333d;

        OverlaySettingsIcon.CancelAnimations();
        OverlaySettingsIcon.Scale =
            double.IsFinite(initialScale) &&
            initialScale > 0d
                ? initialScale
                : restScale;
        OverlaySettingsIcon.Rotation =
            double.IsFinite(initialRotation)
                ? initialRotation
                : 0d;

        try
        {
            await Task.WhenAll(
                OverlaySettingsIcon.ScaleToAsync(
                    0.72d,
                    65,
                    Easing.CubicOut),

                OverlaySettingsIcon.RotateToAsync(
                    18d,
                    65,
                    Easing.CubicOut));

            await Task.WhenAll(
                OverlaySettingsIcon.ScaleToAsync(
                    restScale,
                    105,
                    Easing.CubicOut),

                OverlaySettingsIcon.RotateToAsync(
                    0d,
                    105,
                    Easing.CubicOut));
        }
        catch (OperationCanceledException)
        {
            // Đóng menu/theme change có thể hủy animation đang chạy.
        }
        catch (InvalidOperationException exception)
        {
            // Popup vừa attach/detach native view thì animation chỉ là hiệu ứng
            // phụ; không được để nó ảnh hưởng flow mở Settings.
            System.Diagnostics.Debug.WriteLine(
                $"Settings overlay open animation skipped: {exception.Message}");
        }
        finally
        {
            OverlaySettingsIcon.Scale = restScale;
            OverlaySettingsIcon.Rotation = 0d;
        }
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

#if ANDROID
            MenuPanel.TranslateToAsync(
                0d,
                32d,
                165,
                Easing.CubicIn),

            MenuPanel.ScaleToAsync(
                0.995d,
                165,
                Easing.CubicIn));
#else
            MenuPanel.TranslateToAsync(
                48d,
                0d,
                165,
                Easing.CubicIn),

            MenuPanel.ScaleToAsync(
                0.985d,
                165,
                Easing.CubicIn));
#endif
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

    private void OnLocalizationCultureChanged(
        object? sender,
        EventArgs e)
    {
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

        AppFontOption currentFont =
            AppFontCatalog.GetByKey(
                AppFontManager.CurrentFontKey);

        FontSummaryLabel.Text =
            currentFont.LocalizedDisplayName;

        AppLanguageOption currentLanguage =
            AppLanguageCatalog.GetByLanguage(
                AppLanguageManager.CurrentLanguage);

        LanguageSummaryLabel.Text =
            currentLanguage.LocalizedDisplayName;

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

        AdvancedMenuTitleLabel.Text =
            useEnglish
                ? "Advanced settings"
                : "Cài đặt nâng cao";

        AdvancedMenuSummaryLabel.Text =
            useEnglish
                ? "Appearance, results, and developer tools"
                : "Giao diện, kết quả và nhà phát triển";

        LocalizationService.Attach(
            this);
    }

    private async void OnAppearanceSettingsTapped(
        object? sender,
        TappedEventArgs e)
    {
        await RequestNavigationAsync(
            nameof(SettingsPage));
    }

    private async void OnHardwarePerformanceTapped(
        object? sender,
        TappedEventArgs e)
    {
        await RequestNavigationAsync(
            nameof(HardwarePerformancePage));
    }

    private async void OnAboutTapped(
        object? sender,
        TappedEventArgs e)
    {
        await RequestNavigationAsync(
            nameof(AboutPage));
    }

    private void OnResetTapped(
        object? sender,
        TappedEventArgs e)
    {
        // Keep the quick-menu reset behavior identical to the full Settings
        // page. XAML references this handler directly, so it must exist with
        // the exact TapGestureRecognizer signature for Release XAML compilation.
        AppThemeManager.ResetToDefault();
        AppFontManager.ResetToDefault();
        AppLanguageManager.ResetToDefault();
        DeveloperModeManager.ResetToDefault();
        ResultNumberDisplayMode.ResetToDefault();

#if ANDROID
        // Dynamic Material You color is also part of the appearance reset.
        // This may recreate the Android activity when dynamic color was active.
        AndroidMaterialYouManager.SetDynamicColorEnabled(false);
#endif

        RefreshPopupThemeResources();
        RefreshVectorThemeIcons();
        LocalizationService.RefreshAll();
        UpdateState();
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
        if (_closeTask is not null)
        {
            return;
        }

        await AnimateOverlaySettingsButtonAsync();
        await CloseAsync();
    }

    private async Task AnimateOverlaySettingsButtonAsync()
    {
        OverlaySettingsButton.CancelAnimations();

        try
        {
            await Task.WhenAll(
                OverlaySettingsButton.ScaleToAsync(
                    0.88d,
                    75,
                    Easing.CubicOut),

                OverlaySettingsButton.RotateToAsync(
                    -22d,
                    75,
                    Easing.CubicOut));

            await Task.WhenAll(
                OverlaySettingsButton.ScaleToAsync(
                    1d,
                    110,
                    Easing.CubicOut),

                OverlaySettingsButton.RotateToAsync(
                    0d,
                    110,
                    Easing.CubicOut));
        }
        finally
        {
            OverlaySettingsButton.Scale =
                1d;

            OverlaySettingsButton.Rotation =
                0d;
        }
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
