using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using MathSolver.Services;

namespace MathSolver
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        WindowSoftInputMode = SoftInput.AdjustResize,
        ConfigurationChanges = ConfigChanges.ScreenSize |
                               ConfigChanges.Orientation |
                               ConfigChanges.UiMode |
                               ConfigChanges.ScreenLayout |
                               ConfigChanges.SmallestScreenSize |
                               ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Material dynamic colors must be installed before MAUI inflates
            // the Android activity. This branch does not exist on Windows.
            AndroidMaterialYouManager.ApplyDynamicColorsIfEnabled(this);
            base.OnCreate(savedInstanceState);
        }

        protected override void OnPostCreate(Bundle? savedInstanceState)
        {
            base.OnPostCreate(savedInstanceState);

            // Once the dynamic overlay is active, mirror its semantic colors
            // into MAUI DynamicResources so custom MAUI surfaces match native
            // Material controls and dialogs.
            AppThemeManager.RefreshFromPlatformTheme();
        }
    }
}
