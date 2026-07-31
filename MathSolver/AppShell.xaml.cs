using MathSolver.Views;
using MathSolver.Services;
using System.ComponentModel;
using System.Collections.Generic;

namespace MathSolver;

public partial class AppShell : Shell
{
    private bool _openingSettings;
    private bool _returningFromSettings;

    private string _activeMainRoute =
        "CalculationPage";


    public AppShell()
    {
        InitializeComponent();

        // Ghi nhận tab ban đầu nhưng không chạy animation khi ứng dụng mở.
        _activeMainRoute =
            GetSelectedMainRoute() ??
            "CalculationPage";

        LocalizationService.Attach(
            this);

        LocalizationService.Attach(
            CalculationShellContent);

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

        // Các trang cài đặt là global route nằm ngoài cây Shell nên được
        // đẩy lên navigation stack. Khi đổi tab, cần pop route trước.
        MainTabBar.PropertyChanged +=
            OnMainTabBarPropertyChanged;

        Navigating +=
            OnShellNavigating;

        Navigated +=
            OnShellNavigated;

        UpdateSettingsAccessibilityText();
        UpdateSettingsIconTint();
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

        _openingSettings = true;
        SettingsButton.IsEnabled = false;

        try
        {
            await AnimateSettingsButtonAsync();

            if (Navigation.ModalStack.LastOrDefault()
                is SettingsMenuPage settingsMenu)
            {
                await settingsMenu.CloseWithAnimationAsync();
                return;
            }

            await Navigation.PushModalAsync(
                new SettingsMenuPage(),
                animated:
                    false);
        }
        finally
        {
            SettingsButton.IsEnabled = true;
            _openingSettings = false;
        }
    }

    private async Task AnimateSettingsButtonAsync()
    {
        SettingsButton.CancelAnimations();

        await Task.WhenAll(
            SettingsButton.ScaleToAsync(
                0.88d,
                75,
                Easing.CubicOut),

            SettingsButton.RotateToAsync(
                22d,
                75,
                Easing.CubicOut));

        await Task.WhenAll(
            SettingsButton.ScaleToAsync(
                1d,
                110,
                Easing.CubicOut),

            SettingsButton.RotateToAsync(
                0d,
                110,
                Easing.CubicOut));
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
        RefreshSettingsIconTint();
    }

    private void OnRequestedThemeChanged(
        object? sender,
        AppThemeChangedEventArgs e)
    {
        RefreshSettingsIconTint();
    }

    private void RefreshSettingsIconTint()
    {
        // AppThemeManager thay ResourceDictionary trong cùng chu kỳ sự kiện.
        // Đẩy việc đọc màu sang dispatcher để lấy đúng resource mới.
        Dispatcher.Dispatch(
            UpdateSettingsIconTint);
    }

    private void UpdateSettingsIconTint()
    {
        if (!TryGetThemeColor(
                "TextPrimaryColor",
                out Color tintColor))
        {
            return;
        }

        SettingsIconTintBehavior.TintColor =
            tintColor;
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

    private void OnMainTabBarPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "CurrentItem" ||
            !IsSettingsRouteOpen() ||
            _returningFromSettings)
        {
            return;
        }

        // PropertyChanged chỉ còn phục vụ việc đóng trang cài đặt đang phủ
        // lên Shell. Không khởi chạy animation ở đây vì CurrentItem đổi sau
        // khi trang đích có thể đã xuất hiện, dễ tạo cảm giác nháy hai lần.
        Dispatcher.Dispatch(
            async () => await CloseSettingsAsync());
    }

    private void OnShellNavigating(
        object? sender,
        ShellNavigatingEventArgs e)
    {
        string targetLocation =
            e.Target.Location.OriginalString;

        string? targetMainRoute =
            GetMainRoute(
                targetLocation);

        if (_returningFromSettings ||
            !IsSettingsRouteOpen() ||
            !e.CanCancel ||
            targetMainRoute is null)
        {
            return;
        }

        // Khi một global route cài đặt đang mở mà người dùng chọn tab,
        // hủy điều hướng mặc định, pop route rồi đi tới tab đã chọn.
        e.Cancel();

        Dispatcher.Dispatch(
            async () =>
            {
                await CloseSettingsAsync();

                if (Shell.Current is not null)
                {
                    await Shell.Current.GoToAsync(
                        $"//{targetMainRoute}",
                        animate: false);
                }
            });
    }

    private void OnShellNavigated(
        object? sender,
        ShellNavigatedEventArgs e)
    {
        bool settingsOpen =
            IsSettingsRouteOpen();

        TitleActionButtons.IsVisible =
            !settingsOpen;

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

    public async Task CloseSettingsAsync()
    {
        if (_returningFromSettings ||
            Shell.Current is null ||
            !IsSettingsRouteOpen())
        {
            return;
        }

        _returningFromSettings = true;

        try
        {
            await Shell.Current.GoToAsync(
                "..",
                animate: false);
        }
        finally
        {
            _returningFromSettings = false;
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
            "FormulaPage" => 1,
            "MultiplicationTablePage" => 2,
            _ => -1
        };
    }

    private static string? GetMainRoute(
        string location)
    {
        string[] mainRoutes =
        [
            "CalculationPage",
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