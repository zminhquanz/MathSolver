namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private Grid _keyboardRoot = null!;

#if ANDROID
    private global::Android.Views.View? _keyboardDecor;
    private global::Android.Views.ViewTreeObserver? _keyboardObserver;

    private void AttachMatrixKeyboard()
    {
        DetachMatrixKeyboard();
        _keyboardDecor = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window?.DecorView;
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
        if (CanTouchUi) _keyboardRoot.Padding = 0;
    }

    private void OnMatrixKeyboardLayout(object? sender, EventArgs e) => UpdateMatrixKeyboardInsets();

    private void UpdateMatrixKeyboardInsets()
    {
        if (!CanTouchUi || !_active || _keyboardDecor is null
            || _keyboardRoot.Handler?.PlatformView is not global::Android.Views.View nativeRoot) return;
        var insets = global::AndroidX.Core.View.ViewCompat.GetRootWindowInsets(_keyboardDecor);
        int imeType = global::AndroidX.Core.View.WindowInsetsCompat.Type.Ime();
        double overlap = 0;
        if (insets?.IsVisible(imeType) == true)
        {
            int bottom = insets.GetInsets(imeType)?.Bottom ?? 0;
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
#else
    private void AttachMatrixKeyboard() { }
    private void DetachMatrixKeyboard() { }
#endif
}
