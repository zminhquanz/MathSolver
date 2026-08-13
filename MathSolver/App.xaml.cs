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
    MathSolver.Platforms.Windows.WindowStateManager.Attach(window);
#endif

        splashPage.Loaded += async (_, _) =>
        {
            await Task.Delay(500);

            window.Page = new AppShell();
        };

        return window;
    }
}
