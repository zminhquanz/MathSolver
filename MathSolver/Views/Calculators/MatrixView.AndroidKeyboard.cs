using MathSolver.Services;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private Grid _keyboardRoot = null!;

#if ANDROID
    private global::Android.Views.View? _keyboardDecor;
    private global::Android.Views.ViewTreeObserver? _keyboardObserver;
    private global::Android.Views.Window? _matrixInputWindow;
    private global::Android.Views.SoftInput _savedMatrixSoftInput;
    private global::AndroidX.Core.View.WindowInsetsControllerCompat? _matrixStatusController;
    private bool _savedMatrixStatusVisible;
    private int _savedMatrixBarsBehavior;
    private bool _matrixKeyboardVisible;
    private Page? _matrixChromePage;
    private VisualElement? _matrixSubNavigation;
    private bool _savedMatrixNavBar, _savedMatrixTabBar, _savedMatrixSubNavigation;
    private bool _phoneNavigationRequested;
    private bool IsPhoneLandscape => DeviceInfo.Current.Idiom == DeviceIdiom.Phone
        && DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape;
    // Other Shell writers respect ownership only after the original visibility
    // has been captured. OnAppearing may precede this cached view's Loaded event.
    internal bool IsPhoneNavigationCompact => _matrixChromePage is not null
        && _active && IsPhoneLandscape && !_phoneNavigationRequested;

    private void AttachMatrixKeyboard()
    {
        DetachMatrixKeyboard();
        _matrixInputWindow = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window;
        if (_matrixInputWindow is { } inputWindow)
        {
            _savedMatrixSoftInput = inputWindow.Attributes?.SoftInputMode ?? global::Android.Views.SoftInput.AdjustUnspecified;
            // MAUI's window default can replace the manifest's AdjustResize with
            // AdjustPan. Panning fights the grid's own IME inset handling.
            inputWindow.SetSoftInputMode((_savedMatrixSoftInput & ~global::Android.Views.SoftInput.MaskAdjust)
                | global::Android.Views.SoftInput.AdjustResize);
        }
        _keyboardDecor = _matrixInputWindow?.DecorView;
        _keyboardObserver = _keyboardDecor?.ViewTreeObserver;
        if (_keyboardObserver is { IsAlive: true }) _keyboardObserver.GlobalLayout += OnMatrixKeyboardLayout;
        _keyboardRoot.SizeChanged += OnMatrixKeyboardLayout;
        UpdateMatrixKeyboardInsets();
    }

    private void DetachMatrixKeyboard()
    {
        if (_keyboardObserver is { IsAlive: true }) _keyboardObserver.GlobalLayout -= OnMatrixKeyboardLayout;
        _keyboardRoot.SizeChanged -= OnMatrixKeyboardLayout;
        _keyboardObserver = null; _keyboardDecor = null;
        RestoreMatrixStatusBar();
        var inputWindow = _matrixInputWindow;
        _matrixInputWindow = null;
        if (inputWindow is not null && inputWindow.Handle != IntPtr.Zero)
            inputWindow.SetSoftInputMode(_savedMatrixSoftInput);
        _matrixKeyboardVisible = false;
        RestoreMatrixPhoneChrome();
        _phoneNavigationRequested = false;
        if (CanTouchUi) _keyboardRoot.Padding = 0;
    }

    private void OnMatrixKeyboardLayout(object? sender, EventArgs e) => UpdateMatrixKeyboardInsets();

    private void UpdateMatrixKeyboardInsets()
    {
        if (!CanTouchUi || !_active || _keyboardDecor is null
            || _keyboardRoot.Handler?.PlatformView is not global::Android.Views.View nativeRoot) return;
        var insets = global::AndroidX.Core.View.ViewCompat.GetRootWindowInsets(_keyboardDecor);
        int imeType = global::AndroidX.Core.View.WindowInsetsCompat.Type.Ime();
        bool visible = insets?.IsVisible(imeType) == true;
        if (_matrixKeyboardVisible != visible)
        {
            _matrixKeyboardVisible = visible;
            // Run after the native inset/layout traversal, and only for a live
            // tab. A keyboard transition changes presentation, never the draft.
            Dispatcher.Dispatch(() => { if (CanTouchUi && _active) RefreshWorkspacePresentation(); });
        }
        double overlap = 0;
        if (visible)
        {
            int bottom = insets?.GetInsets(imeType)?.Bottom ?? 0;
            int[] windowLocation = new int[2], rootLocation = new int[2];
            _keyboardDecor.GetLocationOnScreen(windowLocation); nativeRoot.GetLocationOnScreen(rootLocation);
            int keyboardTop = windowLocation[1] + _keyboardDecor.Height - bottom;
            double density = nativeRoot.Resources?.DisplayMetrics?.Density ?? 1;
            // Use only the part overlapping this workspace. Shell bars may
            // already occupy part of the IME region, or Android may resize it.
            overlap = Math.Max(0, rootLocation[1] + nativeRoot.Height - keyboardTop) / Math.Max(1, density);
        }
        if (Math.Abs(_keyboardRoot.Padding.Bottom - overlap) > .5)
            _keyboardRoot.Padding = new Thickness(0, 0, 0, overlap);
    }

    private void RefreshMatrixPhoneChrome()
    {
        if (!IsPhoneLandscape) _phoneNavigationRequested = false;
        bool compact = _active && IsPhoneLandscape && !_phoneNavigationRequested;
        if (!compact) { RestoreMatrixPhoneChrome(); return; }
        if (_matrixChromePage is null)
        {
            for (Element? parent = Parent; parent is not null; parent = parent.Parent)
                if (parent is Page page) { _matrixChromePage = page; break; }
            if (_matrixChromePage is null) return;
            _savedMatrixNavBar = Shell.GetNavBarIsVisible(_matrixChromePage);
            _savedMatrixTabBar = Shell.GetTabBarIsVisible(_matrixChromePage);
            _matrixSubNavigation = _matrixChromePage.FindByName<Grid>("CalculationTabNavigation");
            _savedMatrixSubNavigation = _matrixSubNavigation?.IsVisible ?? true;
        }
        Shell.SetNavBarIsVisible(_matrixChromePage, false);
        Shell.SetTabBarIsVisible(_matrixChromePage, false);
        // Keep topic switching until typing starts. While typing, Done carries
        // the coordinate and restores the full navigation after completion.
        if (_matrixSubNavigation is not null)
            _matrixSubNavigation.IsVisible = !_phoneKeyboardEntry && _savedMatrixSubNavigation;
        RefreshMatrixStatusBar(_phoneKeyboardEntry);
    }

    private void RestoreMatrixPhoneChrome()
    {
        RestoreMatrixStatusBar();
        var page = _matrixChromePage;
        if (page is null) return;
        _matrixChromePage = null;
        if (page.Handler?.MauiContext is not null && !WindowUiLifetime.IsClosing(page.Window))
        {
            Shell.SetNavBarIsVisible(page, _savedMatrixNavBar);
            Shell.SetTabBarIsVisible(page, _savedMatrixTabBar);
            if (_matrixSubNavigation is not null) _matrixSubNavigation.IsVisible = _savedMatrixSubNavigation;
        }
        _matrixSubNavigation = null;
    }

    private void RefreshMatrixStatusBar(bool entering)
    {
        if (!entering) { RestoreMatrixStatusBar(); return; }
        if (_matrixStatusController is not null || _matrixInputWindow is null || _keyboardDecor is null) return;
        int status = global::AndroidX.Core.View.WindowInsetsCompat.Type.StatusBars();
        _savedMatrixStatusVisible = global::AndroidX.Core.View.ViewCompat.GetRootWindowInsets(_keyboardDecor)?.IsVisible(status) == true;
        var controller = global::AndroidX.Core.View.WindowCompat.GetInsetsController(_matrixInputWindow, _keyboardDecor);
        if (controller is null) return;
        _matrixStatusController = controller;
        _savedMatrixBarsBehavior = controller.SystemBarsBehavior;
        controller.SystemBarsBehavior = global::AndroidX.Core.View.WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        controller.Hide(status);
    }

    private void RestoreMatrixStatusBar()
    {
        var controller = _matrixStatusController;
        _matrixStatusController = null;
        if (controller is null || _matrixInputWindow is null || _matrixInputWindow.Handle == IntPtr.Zero) return;
        if (_savedMatrixStatusVisible) controller.Show(global::AndroidX.Core.View.WindowInsetsCompat.Type.StatusBars());
        controller.SystemBarsBehavior = _savedMatrixBarsBehavior;
    }

    internal bool TryRestorePhoneNavigation()
    {
        if (_matrixChromePage is null) return false;
        _phoneNavigationRequested = true;
        RestoreMatrixPhoneChrome();
        RefreshWorkspacePresentation();
        return true;
    }
#else
    private void AttachMatrixKeyboard() { }
    private void DetachMatrixKeyboard() { }
#endif
}
