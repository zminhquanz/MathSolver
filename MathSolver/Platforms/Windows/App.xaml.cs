using Microsoft.UI.Xaml;
using MathSolver.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MathSolver.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Register before InitializeComponent: generated XAML registers its
            // Debugger.Break handler there. Capture the original error first.
            this.UnhandledException += OnWinUiUnhandledException;
            this.InitializeComponent();

        }

        private static readonly string ExceptionLogPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MathSolver", "Diagnostics", "windows-unhandled.log");

        private static void OnWinUiUnhandledException(
            object sender,
            Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            UnhandledExceptionLog.Record(ExceptionLogPath, "WinUI UnhandledException", e.Exception, e.Message);
            // AppThemeManager handles synchronous resource failures. This guard
            // covers delayed native COM callbacks during live-wallpaper changes.
            if (e.Exception is not System.Runtime.InteropServices.COMException exception ||
                !AppThemeManager.IsLiveWallpaperNativeExceptionGuardActive)
            {
                return;
            }

            // Scope this safety net narrowly to the live-wallpaper transition
            // window. COM failures elsewhere remain visible and are not hidden.
            e.Handled = true;

            System.Diagnostics.Debug.WriteLine(
                $"Recovered WinUI live-wallpaper COMException: {exception}");

            AppThemeManager.RecoverFromLiveWallpaperNativeException(exception);
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
