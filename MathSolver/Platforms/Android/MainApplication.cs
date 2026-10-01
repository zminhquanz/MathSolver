using Android.App;
using Android.Runtime;

namespace MathSolver
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override void OnTrimMemory(Android.Content.TrimMemory level)
        {
            base.OnTrimMemory(level);
            // UI_HIDDEN (20) and BACKGROUND (40+) are the supported signals on
            // modern Android. No obsolete running/critical callbacks are needed.
            if ((int)level >= 20)
                global::MathSolver.Services.AppMemoryPolicy.EnterBackground();
        }
    }
}
