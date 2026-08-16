using MathSolver.Services;
using MathSolver.Views;

#if WINDOWS
using MathSolver.Platforms.Windows;
#endif

namespace MathSolver;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        AppLanguageManager.Initialize();
        LocalizationService.Initialize();
        DeveloperModeManager.Initialize();
        AppThemeManager.Initialize(this);
        AppFontManager.Initialize(this);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var splashPage = new SplashPage();

        var window = new Window(splashPage)
        {
            Title = "Math Solver"
        };

#if WINDOWS
        // .NET MAUI 10 has a first-class TitleBar control. On Windows use it
        // as the visible title text instead of relying on DWM to recolor the
        // native caption string after an in-app theme switch.
        var appTitleBar = new TitleBar
        {
            Title = "Math Solver",
            HeightRequest = 32
        };

        ApplyWindowTitleBarTheme(
            appTitleBar);

        window.TitleBar =
            appTitleBar;

        EventHandler titleBarThemeChanged =
            (_, _) =>
                ApplyWindowTitleBarTheme(
                    appTitleBar);

        AppThemeManager.ThemeChanged +=
            titleBarThemeChanged;

        window.Destroying +=
            (_, _) =>
                AppThemeManager.ThemeChanged -=
                    titleBarThemeChanged;

        MathSolver.Platforms.Windows.WindowStateManager.Attach(window);
#endif

        splashPage.Loaded += async (_, _) =>
        {
            await Task.Delay(500);

            window.Page = new AppShell();
        };

        return window;
    }

#if WINDOWS
    private void ApplyWindowTitleBarTheme(
        TitleBar titleBar)
    {
        // Exact black/white is intentional here so the title remains readable
        // regardless of the current accent color.
        titleBar.ForegroundColor =
            AppThemeManager.IsDarkThemeEffective
                ? Colors.White
                : Colors.Black;

        if (Resources.TryGetValue(
                "ShellBackgroundColor",
                out object? shellBackgroundValue) &&
            shellBackgroundValue is Color shellBackgroundColor)
        {
            titleBar.BackgroundColor =
                shellBackgroundColor;
        }
    }
#endif
}
