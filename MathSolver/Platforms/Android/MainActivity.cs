using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using MathSolver.Services;

namespace MathSolver
{
    [Activity(
        Theme = "@style/MathSolver.SplashTheme",
        MainLauncher = true,
        ResizeableActivity = true,
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
            // Android starts this Activity with MathSolver.SplashTheme so the
            // native MAUI splash/starting window can be displayed. Switch to a
            // MAUI's Material 3 theme before MAUI inflates any Material control.
            // This avoids relying on postSplashScreenTheme, which isn't present
            // in the Android resource set used by this project and fails AAPT2.
            SetTheme(Resource.Style.MathSolver_MainTheme);

            // Apply the optional Material You overlay only after MainTheme is
            // active, but still before MAUI creates the visual tree.
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
