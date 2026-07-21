using MathSolver.Services;

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

    protected override Window CreateWindow(
        IActivationState? activationState)
    {
        return new Window(
            new AppShell());
    }
}
