using MathSolver.Views;
using MathSolver.Services;
using System.Collections.Generic;

#if WINDOWS
using Microsoft.Maui.Platform;
using WinUIFrameworkElement = Microsoft.UI.Xaml.FrameworkElement;
using WinUIPopup = Microsoft.UI.Xaml.Controls.Primitives.Popup;
using WinUIWindow = Microsoft.UI.Xaml.Window;
using WindowsPoint = Windows.Foundation.Point;
#endif

namespace MathSolver;

public partial class AppShell : Shell
{
    private bool _openingSettings;
    private bool _returningFromSettings;

    private SettingsMenuPage? _settingsOverlay;
    private Grid? _settingsOverlayHost;

#if WINDOWS
    private WinUIPopup? _windowsSettingsPopup;
    private WinUIFrameworkElement? _windowsOverlayHostElement;
    private WinUIFrameworkElement? _windowsSettingsPlatformView;

    // Geometry của nút Settings được chụp đúng lúc trước khi TitleActionButtons
    // bị ẩn. Nút này neo ở góc phải TitleView, nên top/right/size không phụ
    // thuộc chiều rộng cửa sổ. Khi maximize/restore chỉ cần đổi kích thước
    // full-window overlay và dùng lại geometry này; tuyệt đối không đo lại
    // một native button đang IsVisible=false vì WinUI giữ tọa độ layout cũ.
    private double _windowsSettingsButtonTop;
    private double _windowsSettingsButtonRight;
    private double _windowsSettingsButtonWidth = 42d;
    private double _windowsSettingsButtonHeight = 42d;
#endif

    private bool _isShellChromeDimmed;

    private string _activeMainRoute =
        "CalculationPage";


    public AppShell()
    {
        InitializeComponent();


        // Giữ TitleActionButtons luôn trong visual tree. Khi cần ẩn ở trang
        // Settings detail chỉ đổi Opacity/InputTransparent, không IsVisible=false.
        // Nhờ vậy WinUI không dispose/re-create ImageButton qua mỗi lần vào/ra.
        SetSettingsActionPresentation(
            visible: true);

        // Ghi nhận tab ban đầu nhưng không chạy animation khi ứng dụng mở.
        _activeMainRoute =
            GetSelectedMainRoute() ??
            "CalculationPage";

        LocalizationService.Attach(
            this);

        LocalizationService.Attach(
            CalculationShellContent);

        LocalizationService.Attach(
            MathPuzzleShellContent);

        LocalizationService.Attach(
            FormulaShellContent);

        LocalizationService.Attach(
            MultiplicationShellContent);

        AppLanguageManager.LanguageChanged +=
            OnLanguageChanged;

        AppThemeManager.ThemeChanged +=
            OnThemeChanged;

        if (Application.Current is Application application)
        {
            application.RequestedThemeChanged +=
                OnRequestedThemeChanged;
        }

        Routing.RegisterRoute(
            nameof(SettingsPage),
            typeof(SettingsPage));

        Routing.RegisterRoute(
            nameof(HardwarePerformancePage),
            typeof(HardwarePerformancePage));

        Routing.RegisterRoute(
            nameof(DeveloperModePage),
            typeof(DeveloperModePage));

        Routing.RegisterRoute(
            nameof(AboutPage),
            typeof(AboutPage));

        // Các trang cài đặt chi tiết là global route nằm ngoài cây Shell.
        // Menu cài đặt nhanh không còn là route; nó là visual-tree overlay của tab hiện tại.
        Navigated +=
            OnShellNavigated;

        UpdateSettingsAccessibilityText();
        ApplyShellChromeAppearance();
    }

    private async void OnSettingsClicked(
        object? sender,
        EventArgs e)
    {
        if (_openingSettings ||
            _returningFromSettings ||
            Shell.Current is null)
        {
            return;
        }

        _openingSettings =
            true;


        try
        {
            await AnimateSettingsButtonAsync();

            if (IsSettingsRouteOpen())
            {
                await CloseSettingsAsync();
                return;
            }

            ContentPage? hostPage =
                Shell.Current.CurrentPage as ContentPage;

            if (hostPage?.Content is not Grid hostGrid)
            {
                // Bốn tab chính hiện tại đều dùng Grid làm root. Không thay
                // Content ở runtime để tránh detach/re-attach các native view
                // như GraphicsView trên WinUI.
                return;
            }

            var settingsMenu =
                new SettingsMenuPage();

            _settingsOverlay =
                settingsMenu;

            bool isFullWindowOverlay =
                TryAttachSettingsOverlayToWindow(
                    settingsMenu);

            if (!isFullWindowOverlay)
            {
                Grid.SetRow(
                    settingsMenu,
                    0);

                Grid.SetRowSpan(
                    settingsMenu,
                    Math.Max(
                        1,
                        hostGrid.RowDefinitions.Count));

                Grid.SetColumn(
                    settingsMenu,
                    0);

                Grid.SetColumnSpan(
                    settingsMenu,
                    Math.Max(
                        1,
                        hostGrid.ColumnDefinitions.Count));

                settingsMenu.ZIndex =
                    10000;

                _settingsOverlayHost =
                    hostGrid;

                // Fallback cho các nền tảng không gắn được overlay ở cấp Window.
                // SettingsMenuPage vẫn nằm trên content của tab và Shell chrome
                // được làm tối riêng.
                SetShellChromeDimmed(
                    true);

                hostGrid.Children.Add(
                    settingsMenu);
            }

            // Trên Windows, SettingsMenuPage nằm trong WinUI Popup layer phía
            // trên toàn bộ Shell. Fallback ở nền tảng khác vẫn dùng ContentPage overlay.
            if (!isFullWindowOverlay)
            {
                SetSettingsActionPresentation(
                    visible: true);
            }

            string? requestedRoute =
                await settingsMenu.WaitForCloseAsync();

            RemoveSettingsOverlay();

            if (!string.IsNullOrWhiteSpace(
                    requestedRoute) &&
                Shell.Current is not null)
            {
                // Ẩn bằng Opacity/InputTransparent nhưng KHÔNG tháo
                // TitleActionButtons khỏi visual tree. Điều này tránh WinUI
                // tái tạo ImageButton/behavior sau mỗi lần navigation.
                SetSettingsActionPresentation(
                    visible: false);

                // Chỉ có đúng một lệnh Shell navigation: từ tab hiện tại tới
                // trang chi tiết. Không còn chuỗi ".." rồi push route mới.
                await Shell.Current.GoToAsync(
                    requestedRoute,
                    animate: false);
            }
        }
        finally
        {
            // Nếu có exception giữa chừng vẫn phải trả visual tree về trạng thái
            // hợp lệ trước khi cho phép bấm Settings lần tiếp theo.
            RemoveSettingsOverlay();


            _openingSettings =
                false;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_settingsOverlay is not null)
        {
            _ =
                _settingsOverlay.CloseWithAnimationAsync();

            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void RemoveSettingsOverlay()
    {
        SettingsMenuPage? overlay =
            _settingsOverlay;

        Grid? host =
            _settingsOverlayHost;

        _settingsOverlay =
            null;

        _settingsOverlayHost =
            null;

        bool removedFromWindow =
            RemoveSettingsOverlayFromWindow();

        if (!removedFromWindow &&
            overlay is not null &&
            host is not null &&
            host.Children.Contains(
                overlay))
        {
            host.Children.Remove(
                overlay);
        }

        overlay?.ReleaseOverlayState();

        // TabBar chính luôn được giữ nguyên trạng thái hiển thị khi mở/đóng
        // Settings overlay. Không cần restore Shell.TabBarIsVisible ở đây nữa.
        SetShellChromeDimmed(
            false);

        SetSettingsActionPresentation(
            visible: !IsSettingsRouteOpen());
    }

    private bool TryAttachSettingsOverlayToWindow(
        SettingsMenuPage settingsMenu)
    {
#if WINDOWS
        if (Window?.Handler?.PlatformView is not
                WinUIWindow nativeWindow ||
            nativeWindow.Content is not
                WinUIFrameworkElement shellRoot ||
            shellRoot.XamlRoot is null)
        {
            return false;
        }

        var mauiContext =
            Window.Handler.MauiContext;

        if (mauiContext is null)
        {
            return false;
        }

        // Không thay nativeWindow.Content và không re-parent Shell root nữa.
        // Việc bọc lại Window.Content ở runtime có thể phá layout/title-bar
        // của MAUI Shell. WinUI Popup có popup layer riêng và nằm trên XamlRoot.
        WinUIFrameworkElement platformView =
            settingsMenu.ToPlatform(
                mauiContext);

        double overlayWidth =
            Math.Max(
                1d,
                shellRoot.XamlRoot.Size.Width);

        double overlayHeight =
            Math.Max(
                1d,
                shellRoot.XamlRoot.Size.Height);

        platformView.Width =
            overlayWidth;

        platformView.Height =
            overlayHeight;

        platformView.HorizontalAlignment =
            Microsoft.UI.Xaml.HorizontalAlignment.Left;

        platformView.VerticalAlignment =
            Microsoft.UI.Xaml.VerticalAlignment.Top;

        GetWindowsSettingsButtonGeometry(
            shellRoot,
            overlayWidth,
            out double buttonTop,
            out double buttonRight,
            out double buttonWidth,
            out double buttonHeight);

        // Lưu geometry theo right edge để resize/maximize chỉ cần cập nhật
        // kích thước overlay. SettingsButton thật vẫn được giữ trong visual tree
        // xuyên suốt vòng đời popup, tránh mất handler sau nhiều lần navigation.
        _windowsSettingsButtonTop =
            buttonTop;

        _windowsSettingsButtonRight =
            buttonRight;

        _windowsSettingsButtonWidth =
            buttonWidth;

        _windowsSettingsButtonHeight =
            buttonHeight;

        settingsMenu.UseFullWindowOverlayMode(
            overlayWidth,
            overlayHeight,
            _windowsSettingsButtonTop,
            _windowsSettingsButtonRight,
            _windowsSettingsButtonWidth,
            _windowsSettingsButtonHeight);

        var popup =
            new WinUIPopup
            {
                XamlRoot =
                    shellRoot.XamlRoot,

                Child =
                    platformView,

                HorizontalOffset =
                    0d,

                VerticalOffset =
                    0d,

                IsLightDismissEnabled =
                    false
            };

        _windowsSettingsPopup =
            popup;

        _windowsOverlayHostElement =
            shellRoot;

        _windowsSettingsPlatformView =
            platformView;

        shellRoot.SizeChanged +=
            OnWindowsOverlayHostSizeChanged;

        // Giữ SettingsButton thật trong visual tree/layout để không mất handler,
        // nhưng ẩn PHẦN HÌNH của button thật. Popup đã có OverlaySettingsButton
        // ở đúng tọa độ. Như vậy chỉ có một gear được render, không còn hai icon
        // chồng qua lớp scrim tạo cảm giác bị đè/dày.
        SetSettingsActionPresentation(
            visible: true);

        SetBaseSettingsButtonVisual(
            visible: false);

        popup.IsOpen =
            true;

        // Native embedding không được phép phụ thuộc hoàn toàn vào MAUI Loaded
        // để chạy animation/lifecycle. Kích hoạt rõ ràng sau khi Popup đã mở.
        settingsMenu.ActivateFullWindowOverlay();

        SetShellChromeDimmed(
            false);

        return true;
#else
        return false;
#endif
    }

#if WINDOWS
    private void GetWindowsSettingsButtonGeometry(
        WinUIFrameworkElement shellRoot,
        double overlayWidth,
        out double buttonTop,
        out double buttonRight,
        out double buttonWidth,
        out double buttonHeight)
    {
        // Giá trị fallback chỉ được dùng nếu handler của SettingsButtonHost chưa
        // có platform view. Bình thường host thật luôn đã loaded khi click.
        buttonTop =
            4d;

        buttonRight =
            4d;

        buttonWidth =
            Math.Max(
                42d,
                SettingsButtonHost.Width);

        buttonHeight =
            Math.Max(
                42d,
                SettingsButtonHost.Height);

        // Đo chính Grid 42x42 đang chứa gear, không đo ImageButton trong suốt.
        // Icon thật được center theo SettingsButtonHost; Popup cũng dùng cùng
        // hộp này nên khi chuyển từ gear Shell sang gear Popup không nhảy vài px.
        if (SettingsButtonHost.Handler?.PlatformView is not
                WinUIFrameworkElement nativeSettingsHost ||
            nativeSettingsHost.XamlRoot is null ||
            !ReferenceEquals(
                nativeSettingsHost.XamlRoot,
                shellRoot.XamlRoot))
        {
            return;
        }

        try
        {
            var transform =
                nativeSettingsHost.TransformToVisual(
                    null);

            WindowsPoint origin =
                transform.TransformPoint(
                    new WindowsPoint(
                        0d,
                        0d));

            double measuredWidth =
                nativeSettingsHost.ActualWidth > 0d
                    ? nativeSettingsHost.ActualWidth
                    : 42d;

            double measuredHeight =
                nativeSettingsHost.ActualHeight > 0d
                    ? nativeSettingsHost.ActualHeight
                    : 42d;

            buttonTop =
                Math.Max(
                    0d,
                    origin.Y);

            buttonRight =
                Math.Max(
                    0d,
                    overlayWidth -
                    origin.X -
                    measuredWidth);

            buttonWidth =
                measuredWidth;

            buttonHeight =
                measuredHeight;
        }
        catch (InvalidOperationException)
        {
            // Nếu visual tree đang chuyển trạng thái đúng lúc click, fallback
            // ở trên vẫn giữ menu usable thay vì làm crash ứng dụng.
        }
        catch (ArgumentException)
        {
            // TransformToVisual có thể thất bại nếu visual vừa bị detach.
            // Không để lỗi layout của overlay làm crash Settings.
        }
    }

    private void OnWindowsOverlayHostSizeChanged(
        object sender,
        Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (_windowsSettingsPopup?.IsOpen != true ||
            _windowsSettingsPlatformView is not
                WinUIFrameworkElement platformView ||
            _settingsOverlay is not
                SettingsMenuPage settingsMenu ||
            sender is not
                WinUIFrameworkElement shellRoot ||
            shellRoot.XamlRoot is null)
        {
            return;
        }

        double overlayWidth =
            Math.Max(
                1d,
                shellRoot.XamlRoot.Size.Width);

        double overlayHeight =
            Math.Max(
                1d,
                shellRoot.XamlRoot.Size.Height);

        platformView.Width =
            overlayWidth;

        platformView.Height =
            overlayHeight;

        // Dùng geometry neo theo cạnh phải đã chụp lúc mở popup. Right/top/size
        // ổn định khi maximize/restore, còn overlayWidth/Height luôn lấy mới.
        settingsMenu.UpdateFullWindowOverlayLayout(
            overlayWidth,
            overlayHeight,
            _windowsSettingsButtonTop,
            _windowsSettingsButtonRight,
            _windowsSettingsButtonWidth,
            _windowsSettingsButtonHeight);
    }
#endif

    private bool RemoveSettingsOverlayFromWindow()
    {
#if WINDOWS
        WinUIPopup? popup =
            _windowsSettingsPopup;

        WinUIFrameworkElement? hostElement =
            _windowsOverlayHostElement;

        _windowsSettingsPopup =
            null;

        _windowsOverlayHostElement =
            null;

        _windowsSettingsPlatformView =
            null;

        _windowsSettingsButtonTop =
            0d;

        _windowsSettingsButtonRight =
            0d;

        _windowsSettingsButtonWidth =
            42d;

        _windowsSettingsButtonHeight =
            42d;

        if (hostElement is not null)
        {
            hostElement.SizeChanged -=
                OnWindowsOverlayHostSizeChanged;
        }

        if (popup is null)
        {
            SetBaseSettingsButtonVisual(
                visible: true);

            return false;
        }

        popup.IsOpen =
            false;

        popup.Child =
            null;

        // Popup gear đã biến mất; trả lại duy nhất gear thật của Shell.
        SetBaseSettingsButtonVisual(
            visible: true);

        return true;
#else
        return false;
#endif
    }

    private async Task AnimateSettingsButtonAsync()
    {
        // Animate cả host chứa vector gear. SettingsButton chỉ là hit target
        // trong suốt, nên animate riêng ImageButton sẽ không làm gear chuyển động.
        SettingsButtonHost.CancelAnimations();

        try
        {
            await Task.WhenAll(
                SettingsButtonHost.ScaleToAsync(
                    0.88d,
                    75,
                    Easing.CubicOut),

                SettingsButtonHost.RotateToAsync(
                    22d,
                    75,
                    Easing.CubicOut));

            await Task.WhenAll(
                SettingsButtonHost.ScaleToAsync(
                    1d,
                    110,
                    Easing.CubicOut),

                SettingsButtonHost.RotateToAsync(
                    0d,
                    110,
                    Easing.CubicOut));
        }
        finally
        {
            // Không để transform dở dang ảnh hưởng phép đo tọa độ dùng để neo Popup.
            SettingsButtonHost.Scale =
                1d;

            SettingsButtonHost.Rotation =
                0d;
        }
    }

    private void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        LocalizationService.RefreshAll();
        UpdateSettingsAccessibilityText();
    }

    private void OnThemeChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            SettingsButtonIcon.RefreshThemeColor();
            ApplyShellChromeAppearance();
        });
    }

    private void OnRequestedThemeChanged(
        object? sender,
        AppThemeChangedEventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            SettingsButtonIcon.RefreshThemeColor();
            ApplyShellChromeAppearance();
        });
    }

    private void SetBaseSettingsButtonVisual(
        bool visible)
    {
        // Popup Windows có một Settings button riêng nằm trên scrim để người
        // dùng có thể bấm lại và đóng menu. Nếu vẫn vẽ button/icon thật của
        // Shell ở bên dưới, hai glyph gần như trùng nhau qua lớp scrim và tạo
        // cảm giác icon bị đè/dày. Chỉ đổi Opacity: control thật vẫn ở nguyên
        // visual tree và vẫn giữ layout/handler ổn định.
        double opacity =
            visible
                ? 1d
                : 0d;

        if (visible)
        {
            // Gear thật của Shell bị ẩn trong lúc Popup mở. Nếu theme đã đổi
            // trong thời gian đó, refresh màu TRƯỚC khi đưa Opacity về 1 để
            // không lóe/mất icon do giữ Fill của theme cũ.
            SettingsButtonIcon.RefreshThemeColor();
        }

        SettingsButton.Opacity =
            opacity;

        SettingsButtonIcon.Opacity =
            opacity;
    }

    private void SetSettingsActionPresentation(
        bool visible)
    {
        // Không dùng IsVisible để ẩn TitleActionButtons. IsVisible=false khiến
        // WinUI Shell có thể detach/recreate native ImageButton sau nhiều vòng
        // detail -> Back. Giữ nguyên visual tree và chỉ đổi Opacity/InputTransparent.
        TitleActionButtons.IsVisible =
            true;

        TitleActionButtons.Opacity =
            visible
                ? 1d
                : 0d;

        TitleActionButtons.InputTransparent =
            !visible;

        SettingsButton.IsEnabled =
            visible;

    }

    private void SetShellChromeDimmed(
        bool isDimmed)
    {
        if (_isShellChromeDimmed == isDimmed)
        {
            return;
        }

        _isShellChromeDimmed =
            isDimmed;

        ApplyShellChromeAppearance();
    }

    private void ApplyShellChromeAppearance()
    {
        ShellTitleScrim.IsVisible =
            _isShellChromeDimmed;

        if (TryGetThemeColor(
                "ShellBackgroundColor",
                out Color shellBackgroundColor))
        {
            Shell.SetTabBarBackgroundColor(
                MainTabBar,
                _isShellChromeDimmed
                    ? BlendColorWithBlack(
                        shellBackgroundColor,
                        0.4f)
                    : shellBackgroundColor);
        }

        if (TryGetThemeColor(
                "ShellForegroundColor",
                out Color shellForegroundColor))
        {
            Color dimmedForegroundColor =
                _isShellChromeDimmed
                    ? BlendColorWithBlack(
                        shellForegroundColor,
                        0.4f)
                    : shellForegroundColor;

            Shell.SetTabBarForegroundColor(
                MainTabBar,
                dimmedForegroundColor);

            Shell.SetTabBarTitleColor(
                MainTabBar,
                dimmedForegroundColor);
        }

        if (TryGetThemeColor(
                "ShellUnselectedColor",
                out Color shellUnselectedColor))
        {
            Shell.SetTabBarUnselectedColor(
                MainTabBar,
                _isShellChromeDimmed
                    ? BlendColorWithBlack(
                        shellUnselectedColor,
                        0.4f)
                    : shellUnselectedColor);
        }
    }

    private static Color BlendColorWithBlack(
        Color source,
        float blackAmount)
    {
        float clampedBlackAmount =
            Math.Clamp(
                blackAmount,
                0f,
                1f);

        float retainedAmount =
            1f - clampedBlackAmount;

        return new Color(
            source.Red * retainedAmount,
            source.Green * retainedAmount,
            source.Blue * retainedAmount,
            source.Alpha);
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

        return TryGetThemeColor(
            resources,
            resourceKey,
            out color);
    }

    private static bool TryGetThemeColor(
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
            if (TryGetThemeColor(
                    mergedDictionaries[index],
                    resourceKey,
                    out color))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateSettingsAccessibilityText()
    {
        ToolTipProperties.SetText(
            SettingsButton,
            LocalizationService.Translate(
                "Cài đặt giao diện"));

        SemanticProperties.SetDescription(
            SettingsButton,
            LocalizationService.Translate(
                "Mở cài đặt giao diện"));
    }

    private void OnShellNavigated(
        object? sender,
        ShellNavigatedEventArgs e)
    {
        // TabBar chính vẫn có thể bấm khi Settings overlay đang mở. Nếu người
        // dùng chuyển sang tab khác, đóng overlay của tab cũ để không giữ một
        // WaitForCloseAsync treo và để lần quay lại tab cũ không còn lớp phủ.
        if (_settingsOverlay is not null)
        {
            SetSettingsActionPresentation(
                visible: true);

            _ =
                _settingsOverlay.CloseWithAnimationAsync();

            UpdateSettingsAccessibilityText();
            return;
        }

        bool settingsOpen =
            IsSettingsRouteOpen();

        SetSettingsActionPresentation(
            visible: !settingsOpen);

        UpdateSettingsAccessibilityText();

        // Không chạy animation trong Navigated. Mỗi trang tự quyết định
        // transition đúng một lần trong OnAppearing bằng route cuối đã hiển thị.
    }

    public bool TryConsumeMainTabTransition(
        string pageRoute,
        out int direction)
    {
        direction =
            0;

        int targetIndex =
            GetMainRouteIndex(
                pageRoute);

        if (targetIndex < 0 ||
            string.Equals(
                _activeMainRoute,
                pageRoute,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int currentIndex =
            GetMainRouteIndex(
                _activeMainRoute);

        // OnAppearing của chính trang đích là nguồn sự thật duy nhất.
        // Không phụ thuộc Shell.Navigating/CurrentItem vì hai sự kiện đó
        // có thể không phát theo cùng thứ tự trên Windows và Android.
        direction =
            targetIndex >= currentIndex
                ? 1
                : -1;

        // Commit trước khi trả về. Dù OnAppearing bị gọi lặp, cùng route
        // không thể nhận animation lần thứ hai.
        _activeMainRoute =
            pageRoute;

        return true;
    }

    public async Task CloseSettingsAsync(
        Page? sourcePage = null)
    {
        if (_returningFromSettings ||
            Shell.Current is null ||
            !IsSettingsRouteOpen())
        {
            return;
        }

        // Nếu một lần back khác đã thắng race và CurrentPage không còn là
        // trang phát lệnh nữa thì không pop thêm lần thứ hai. Đây là guard
        // quan trọng cho WinUI, nơi handler có thể bị disconnect trong lúc
        // Shell đang hoàn tất một navigation trước đó.
        if (sourcePage is not null &&
            !ReferenceEquals(
                CurrentPage,
                sourcePage))
        {
            return;
        }

        _returningFromSettings = true;

        try
        {
            // Exit animation vừa kết thúc có thể vẫn còn một layout pass của
            // WinUI trong queue. Nhường đúng một UI turn trước khi pop để
            // tránh đụng handler đang ở pha disconnect/reconnect.
            await Task.Yield();

            if (sourcePage is not null &&
                !ReferenceEquals(
                    CurrentPage,
                    sourcePage))
            {
                return;
            }

#if WINDOWS
            // Với Settings detail là global route, Shell tạo navigation stack.
            // Trên WinUI dùng INavigation.PopAsync để pop đúng page hiện tại
            // thay vì GoToAsync(".."). Cách này tránh đường Shell URI back
            // thỉnh thoảng chạm vào PlatformView đã bị disconnect.
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync(
                    animated: false);
            }
            else
            {
                // Trạng thái này không nên xảy ra với global Settings route.
                // Không gọi GoToAsync("..") làm fallback trên Windows vì đó
                // chính là đường navigation gây PlatformView-null race.
                System.Diagnostics.Debug.WriteLine(
                    "[Settings] Back ignored: navigation stack has no detail page to pop.");
            }
#else
            await GoToAsync(
                "..",
                animate: false);
#endif
        }
        finally
        {
            _returningFromSettings = false;

            Dispatcher.Dispatch(() =>
                SetSettingsActionPresentation(
                    visible: !IsSettingsRouteOpen()));
        }
    }

    private static bool IsSettingsRouteOpen()
    {
        string location =
            Shell.Current?.CurrentState.Location.OriginalString ??
            string.Empty;

        return location.Contains(
                   nameof(SettingsPage),
                   StringComparison.OrdinalIgnoreCase) ||
               location.Contains(
                   nameof(HardwarePerformancePage),
                   StringComparison.OrdinalIgnoreCase) ||
               location.Contains(
                   nameof(DeveloperModePage),
                   StringComparison.OrdinalIgnoreCase) ||
               location.Contains(
                   nameof(AboutPage),
                   StringComparison.OrdinalIgnoreCase);
    }

    private string? GetSelectedMainRoute()
    {
        ShellSection? selectedSection =
            MainTabBar.CurrentItem;

        ShellContent? selectedContent =
            selectedSection?.CurrentItem;

        if (ReferenceEquals(
                selectedContent,
                CalculationShellContent))
        {
            return "CalculationPage";
        }

        if (ReferenceEquals(
                selectedContent,
                MathPuzzleShellContent))
        {
            return "MathPuzzlePage";
        }

        if (ReferenceEquals(
                selectedContent,
                FormulaShellContent))
        {
            return "FormulaPage";
        }

        if (ReferenceEquals(
                selectedContent,
                MultiplicationShellContent))
        {
            return "MultiplicationTablePage";
        }

        string candidateLocation =
            selectedContent?.Route ??
            selectedSection?.Route ??
            CurrentState.Location.OriginalString;

        return GetMainRoute(
            candidateLocation);
    }

    private static int GetMainRouteIndex(
        string route)
    {
        return route switch
        {
            "CalculationPage" => 0,
            "MathPuzzlePage" => 1,
            "FormulaPage" => 2,
            "MultiplicationTablePage" => 3,
            _ => -1
        };
    }

    private static string? GetMainRoute(
        string location)
    {
        string[] mainRoutes =
        [
            "CalculationPage",
            "MathPuzzlePage",
            "FormulaPage",
            "MultiplicationTablePage"
        ];

        foreach (string route in mainRoutes)
        {
            if (location.Contains(
                    route,
                    StringComparison.OrdinalIgnoreCase))
            {
                return route;
            }
        }

        return null;
    }
}
