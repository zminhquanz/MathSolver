using MathSolver.Views;
using MathSolver.Services;
using System.ComponentModel;

namespace MathSolver;

public partial class AppShell : Shell
{
    private bool _openingSettings;
    private bool _returningFromSettings;

    public AppShell()
    {
        InitializeComponent();

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

        Routing.RegisterRoute(
            nameof(SettingsPage),
            typeof(SettingsPage));

        // SettingsPage là một route nằm ngoài cây giao diện Shell nên nó được
        // đẩy lên navigation stack. Khi đổi tab, cần pop route này trước.
        MainTabBar.PropertyChanged +=
            OnMainTabBarPropertyChanged;

        Navigating +=
            OnShellNavigating;

        Navigated +=
            OnShellNavigated;

        UpdateSettingsAccessibilityText();
    }

    private async void OnSettingsClicked(
        object sender,
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
            if (Navigation.ModalStack.LastOrDefault()
                is SettingsMenuPage)
            {
                await Navigation.PopModalAsync(
                    animated: false);
                return;
            }

            await Navigation.PushModalAsync(
                new SettingsMenuPage(),
                animated: false);
        }
        finally
        {
            SettingsButton.IsEnabled = true;
            _openingSettings = false;
        }
    }

    private void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        LocalizationService.RefreshAll();
        UpdateSettingsAccessibilityText();
    }

    private void UpdateSettingsAccessibilityText()
    {
        ToolTipProperties.SetText(
            SettingsButton,
            LocalizationService.Translate(
                "Cài đặt giao diện"));

        AutomationProperties.SetName(
            SettingsButton,
            LocalizationService.Translate(
                "Mở cài đặt giao diện"));
    }

    private void OnMainTabBarPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "CurrentItem" ||
            !IsSettingsOpen() ||
            _returningFromSettings)
        {
            return;
        }

        // Tab đã được đổi ở phía sau SettingsPage. Pop SettingsPage để
        // trang của tab vừa chọn xuất hiện ngay.
        Dispatcher.Dispatch(
            async () => await CloseSettingsAsync());
    }

    private void OnShellNavigating(
        object? sender,
        ShellNavigatingEventArgs e)
    {
        if (_returningFromSettings ||
            !IsSettingsOpen() ||
            !e.CanCancel)
        {
            return;
        }

        string targetLocation =
            e.Target.Location.OriginalString;

        string? targetMainRoute =
            GetMainRoute(targetLocation);

        if (targetMainRoute is null)
        {
            return;
        }

        // Route SettingsPage là global route. Nếu người dùng chọn một tab,
        // hủy điều hướng mặc định, pop SettingsPage rồi đi tới route tab.
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
            IsSettingsOpen();

        // Khi Settings đang mở, ẩn toàn bộ nhóm nút góc phải.
        // Khu vực này vẫn có thể chứa thêm nút trong tương lai, nhưng
        // chúng chỉ xuất hiện ở các màn hình chính.
        TitleActionButtons.IsVisible =
            !settingsOpen;


        UpdateSettingsAccessibilityText();
    }

    public async Task CloseSettingsAsync()
    {
        if (_returningFromSettings ||
            Shell.Current is null ||
            !IsSettingsOpen())
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

    private static bool IsSettingsOpen()
    {
        string location =
            Shell.Current?.CurrentState.Location.OriginalString ??
            string.Empty;

        return location.Contains(
            nameof(SettingsPage),
            StringComparison.OrdinalIgnoreCase);
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