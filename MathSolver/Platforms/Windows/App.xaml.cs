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
            this.InitializeComponent();

            // Last-resort WinUI guard for transient native COM failures that can
            // surface *after* a MAUI DynamicResource assignment has returned.
            // AppThemeManager catches synchronous failures itself; this event is
            // specifically for the asynchronous WinUI callback path that used to
            // terminate the process while MediaElement/ComboBox native objects
            // were being rebuilt during a live-wallpaper transition.
            this.UnhandledException += OnWinUiUnhandledException;
        }

        private static void OnWinUiUnhandledException(
            object sender,
            Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
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
