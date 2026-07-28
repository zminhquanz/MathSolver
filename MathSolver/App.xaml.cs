using MathSolver.Services;
using MathSolver.Views;

namespace MathSolver;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        AppLanguageManager.Initialize();
        LocalizationService.Initialize();
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

        splashPage.Loaded += async (_, _) =>
        {
            await Task.Delay(500);

            window.Page = new AppShell();
        };

        return window;
    }
}
