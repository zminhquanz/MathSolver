namespace MathSolver.Views;

public partial class FormulaPage
{
    private bool HandleFormulaChooserKey(int key, bool reverse)
    {
        if (!CanUseTabNavigation || !AllFormulaTypesOverlay.IsVisible) return false;
        if (key == 0x1B) { CloseFormulaTypes(); return true; }
        if (key != 0x09) return false;
        var controls = new List<VisualElement> { _formulaTypeClose, _formulaTypeSearch };
        controls.AddRange(_formulaTypeChoices.Where(button => button.IsVisible));
        int current = controls.FindIndex(IsFormulaChooserControlFocused);
        int next = current < 0 ? (reverse ? controls.Count - 1 : 0)
            : (current + (reverse ? -1 : 1) + controls.Count) % controls.Count;
        FocusFormulaChooserControl(controls[next]);
        return true;
    }

    private static void FocusFormulaChooserControl(VisualElement view)
    {
#if WINDOWS
        // Focus() can short-circuit when MAUI's old IsFocused flag is still true.
        if (view.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control native
            && native.IsLoaded && native.IsEnabled && native.Focus(Microsoft.UI.Xaml.FocusState.Programmatic)) return;
#endif
        view.Focus();
    }

    private bool IsFormulaChooserControlFocused(VisualElement view)
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
    private void AttachFormulaChooserKeyboard()
    {
#if WINDOWS
        DetachFormulaChooserKeyboard();
        if (!AllFormulaTypesOverlay.IsVisible || AllFormulaTypesOverlay.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement surface) return;
        _formulaChooserKeyboard = surface;
        surface.AddHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnFormulaChooserKeyDown), true);
#endif
    }

    private void DetachFormulaChooserKeyboard()
    {
#if WINDOWS
        if (_formulaChooserKeyboard is not { } surface) return;
        _formulaChooserKeyboard = null;
        try { surface.RemoveHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnFormulaChooserKeyDown)); }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine(error); }
#endif
    }

#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _formulaChooserKeyboard;
    private void OnFormulaChooserKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        bool reverse = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Shift)
            & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        if (HandleFormulaChooserKey((int)e.Key, reverse)) e.Handled = true;
    }
#endif
}
