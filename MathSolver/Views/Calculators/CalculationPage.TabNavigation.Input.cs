namespace MathSolver.Views;

public partial class CalculationPage
{
    private bool HandleCalculationChooserKey(int key, bool reverse)
    {
        if (!CanUseTabNavigation || !AllCalculationTypesOverlay.IsVisible) return false;
        if (key == 0x1B) { CloseCalculationTypes(); return true; }
        if (key != 0x09) return false;
        var controls = new List<VisualElement> { _calculationTypeClose, _calculationTypeSearch };
        controls.AddRange(_calculationTypeChoices.Where(button => button.IsVisible));
        int current = controls.FindIndex(IsCalculationChooserControlFocused);
        int next = current < 0 ? (reverse ? controls.Count - 1 : 0)
            : (current + (reverse ? -1 : 1) + controls.Count) % controls.Count;
        FocusCalculationChooserControl(controls[next]);
        return true;
    }

    private static void FocusCalculationChooserControl(VisualElement view)
    {
#if WINDOWS
        // Focus() can short-circuit when MAUI's old IsFocused flag is still true.
        if (view.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control native
            && native.IsLoaded && native.IsEnabled && native.Focus(Microsoft.UI.Xaml.FocusState.Programmatic)) return;
#endif
        view.Focus();
    }

    private bool IsCalculationChooserControlFocused(VisualElement view)
    {
#if WINDOWS
        // MAUI focus notifications can lag both entering and leaving a control.
        // Prefer the actual WinUI focus owner in either direction.
        // SearchBar focus belongs to its nested native text input.
        if (view.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement native && native.XamlRoot is not null)
        {
            var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(native.XamlRoot) as Microsoft.UI.Xaml.DependencyObject;
            for (var element = focused; element is not null; element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
                if (ReferenceEquals(element, native)) return true;
            return false;
        }
#endif
        return view.IsFocused;
    }
    private void AttachCalculationChooserKeyboard()
    {
#if WINDOWS
        DetachCalculationChooserKeyboard();
        if (!AllCalculationTypesOverlay.IsVisible || AllCalculationTypesOverlay.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement surface) return;
        _calculationChooserKeyboard = surface;
        surface.AddHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnCalculationChooserKeyDown), true);
#endif
    }

    private void DetachCalculationChooserKeyboard()
    {
#if WINDOWS
        if (_calculationChooserKeyboard is not { } surface) return;
        _calculationChooserKeyboard = null;
        try { surface.RemoveHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnCalculationChooserKeyDown)); }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine(error); }
#endif
    }

#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _calculationChooserKeyboard;
    private void OnCalculationChooserKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        bool reverse = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Shift)
            & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        if (HandleCalculationChooserKey((int)e.Key, reverse)) e.Handled = true;
    }
#endif
}
