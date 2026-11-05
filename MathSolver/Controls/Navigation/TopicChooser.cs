using MathSolver.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MathSolver.Controls.Navigation;

/// <summary>Composed chooser; pages retain topic routing and their named probe wrappers.</summary>
internal sealed class TopicChooser
{
    public Button[] Choices { get; }
    public SearchBar Search { get; }
    public Label Heading { get; }
    public Label Empty { get; }
    public Button Close { get; }
    public Border Card { get; private set; } = null!;
    public ScrollView List { get; private set; } = null!;
    private readonly ContentView Overlay;
    private readonly Func<bool> canUse;
    private readonly Action dismiss;
    private bool needsFocus;
    private int generation;

    public TopicChooser(ContentView overlayHost, Button[] choiceButtons, SearchBar searchBar, Label heading,
        Label empty, Button close, string[] keys, string tabPrefix, string automationPrefix,
        Func<bool> canUse, Action dismissChooser, Action<int> selected, Action refresh)
    {
        Overlay = overlayHost; Choices = choiceButtons; Search = searchBar; Heading = heading;
        Empty = empty; Close = close; this.canUse = canUse; this.dismiss = dismissChooser;
        var choices = new VerticalStackLayout { Spacing = 8 };
        for (int i = 0; i < Choices.Length; i++)
        {
            int index = i;
            var button = new Button { Style = (Style)Application.Current!.Resources["SolverSecondaryActionStyle"],
                FontSize = 15, LineBreakMode = LineBreakMode.WordWrap, Padding = new Thickness(12, 10),
                AutomationId = automationPrefix + keys[i] };
            MathNavigationIcon.SetKey(button, tabPrefix + keys[i]);
            DisplayDensityManager.BindControl(button);
            button.Clicked += (_, _) => selected(index);
            Choices[i] = button;
            choices.Add(button);
        }
        Empty.FontSize = 14;
        Empty.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
        choices.Add(Empty);
        List = new ScrollView { Content = choices, VerticalScrollBarVisibility = ScrollBarVisibility.Default };
        Heading.FontSize = 20;
        Heading.FontAttributes = FontAttributes.Bold;
        Heading.LineBreakMode = LineBreakMode.WordWrap;
        Heading.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetHeadingLevel(Heading, SemanticHeadingLevel.Level2);
        Search.SetDynamicResource(SearchBar.TextColorProperty, "TextPrimaryColor");
        Search.SetDynamicResource(SearchBar.PlaceholderColorProperty, "TextSecondaryColor");
        Search.TextChanged += (_, _) => refresh();
        DisplayDensityManager.BindControl(Close, square: true);
        Close.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
        Close.SetDynamicResource(Button.BackgroundColorProperty, "SurfaceAltColor");
        Close.Clicked += (_, _) => dismissChooser();
        Close.Loaded += (_, _) => QueueFocus();
        var search = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        search.Add(Search); search.Add(Close, 1, 0);
        var body = new Grid { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) } };
        body.Add(Heading); body.Add(search, 0, 1); body.Add(List, 0, 2);
        Card = new Border { Content = body, Padding = 16, StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 }, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        Card.SetDynamicResource(VisualElement.BackgroundColorProperty, "SurfaceColor");
        Card.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
        var shade = new BoxView { Color = Colors.Black.WithAlpha(0.35f) };
        var dismiss = new TapGestureRecognizer(); dismiss.Tapped += (_, _) => dismissChooser();
        shade.GestureRecognizers.Add(dismiss);
        AutomationProperties.SetIsInAccessibleTree(shade, false);
        var overlay = new Grid(); overlay.Add(shade); overlay.Add(Card);
        Overlay.Content = overlay;
    }

    public void Refresh(string[] keys, string tabPrefix, int selected)
    {
        var options = keys.Select((key, index) => new QuizChoiceOption(index, key,
            MathNavigationCatalog.TranslateLabel(tabPrefix + key), "", "", index == selected)).ToArray();
        var matches = QuizChoiceCatalog.Filter(options, Search.Text ?? "").Select(option => option.Index).ToHashSet();
        foreach (var option in options)
        {
            var button = Choices[option.Index];
            button.Text = (option.IsSelected ? "\u2713 " : "") + option.Title;
            button.IsVisible = matches.Contains(option.Index);
            SelectionButtonStyler.Apply(button, option.IsSelected);
        }
        Empty.IsVisible = matches.Count == 0;
    }

    public void Resize(double width, double height)
    {
        Card.WidthRequest = TopicNavigationPolicy.CardExtent(width);
        Card.HeightRequest = TopicNavigationPolicy.CardExtent(height);
        Heading.IsVisible = !ResponsiveLayoutPolicy.UseStackedLayout(height, 320);
    }

    public void RequestFocus() { needsFocus = true; generation++; QueueFocus(); }
    public void Invalidate() { needsFocus = false; generation++; DetachKeyboard(); }
    public void QueueFocus()
    {
        if (!needsFocus) return;
        int token = generation;
        Overlay.Dispatcher.Dispatch(() =>
        {
            if (token != generation || !needsFocus || !canUse() || !Overlay.IsVisible
                || !Close.IsLoaded || Close.Handler?.MauiContext is null) return;
            bool focused = Close.Focus();
#if WINDOWS
            if (Close.Handler.PlatformView is Microsoft.UI.Xaml.Controls.Control control)
                focused = control.Focus(Microsoft.UI.Xaml.FocusState.Programmatic) || focused;
#endif
            if (focused) needsFocus = false;
        });
    }

    public bool HandleKey(int key, bool reverse)
    {
        if (!canUse() || !Overlay.IsVisible) return false;
        if (key == 0x1B) { dismiss(); return true; }
        if (key != 0x09) return false;
        var controls = new List<VisualElement> { Close, Search };
        controls.AddRange(Choices.Where(button => button.IsVisible));
        int current = controls.FindIndex(IsControlFocused);
        FocusControl(controls[TopicNavigationPolicy.NextFocus(current, controls.Count, reverse)]);
        return true;
    }
    public static void FocusControl(VisualElement view)
    {
#if WINDOWS
        // Focus() can short-circuit when MAUI's old IsFocused flag is still true.
        if (view.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control native
            && native.IsLoaded && native.IsEnabled && native.Focus(Microsoft.UI.Xaml.FocusState.Programmatic)) return;
#endif
        view.Focus();
    }

    public static bool IsControlFocused(VisualElement view)
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
    public void AttachKeyboard()
    {
#if WINDOWS
        DetachKeyboard();
        if (!Overlay.IsVisible || Overlay.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement surface) return;
        keyboard = surface;
        surface.AddHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnKeyDown), true);
#endif
    }

    public void DetachKeyboard()
    {
#if WINDOWS
        if (keyboard is not { } surface) return;
        keyboard = null;
        try { surface.RemoveHandler(Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnKeyDown)); }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine(error); }
#endif
    }

#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? keyboard;
    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        bool reverse = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Shift)
            & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        if (HandleKey((int)e.Key, reverse)) e.Handled = true;
    }
#endif
}
