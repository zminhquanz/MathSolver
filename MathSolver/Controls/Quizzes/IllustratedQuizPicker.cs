using System.ComponentModel;
using System.Runtime.CompilerServices;
using MathSolver.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MathSolver.Controls;

/// <summary>
/// Replaces native picker chrome while preserving the original Picker's selection/events.
/// The backing Picker is not in the visual tree, so it cannot open a second native popup.
/// </summary>
public sealed class IllustratedQuizPicker : ContentView
{
    private static readonly ConditionalWeakTable<Picker, IllustratedQuizPicker> Instances = new();
    private readonly Picker _picker;
    private readonly Button _open;
    private readonly Label _title;
    private readonly QuizChoiceIllustration _illustration;
    private readonly string _captionKey;
    private string[] _keys = [];
    private bool _opening;

    private IllustratedQuizPicker(Picker picker, string captionKey, Border? frame)
    {
        _picker = picker;
        _captionKey = captionKey;
        MinimumHeightRequest = DeviceInfo.Platform == DevicePlatform.Android ? 56 : 48;
        _open = ChoiceButton(drawFocusBorder: false);
        // Focus belongs to the field's single outer outline, not a second inner
        // button outline. Keep stroke thickness constant so content never shifts.
        if (frame is not null)
        {
            void UpdateFrameFocus()
            {
                if (_open.Handler?.PlatformView is null || frame.Handler?.PlatformView is null) return;
                frame.SetDynamicResource(Border.StrokeProperty, _open.IsFocused ? "PrimaryColor" : "BorderBrush");
            }
            _open.Focused += (_, _) => UpdateFrameFocus();
            _open.Unfocused += (_, _) => UpdateFrameFocus();
            _open.Loaded += (_, _) => UpdateFrameFocus();
        }
        // Use the hardware-page input surface. A transparent button receives an
        // accent-colored hover overlay, which makes the entire field look selected.
        _open.CornerRadius = 10;
        _open.SetDynamicResource(Button.BackgroundColorProperty, "InputBackgroundColor");
        _open.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
        _title = new Label { FontSize = 15, FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap };
        _title.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        _illustration = new QuizChoiceIllustration { WidthRequest = 32, HeightRequest = 32,
            VerticalOptions = LayoutOptions.Center };
        var arrow = new Polyline { Points = new PointCollection { new(1, 1), new(5, 5), new(9, 1) },
            StrokeThickness = 1.5, WidthRequest = 10, HeightRequest = 6,
            VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(8, 0, 4, 0) };
        arrow.SetDynamicResource(Shape.StrokeProperty, "TextSecondaryBrush");
        var display = new Grid { ColumnDefinitions = new() { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8,
            Padding = new Thickness(12, 8), InputTransparent = true };
        display.Add(_illustration);
        display.Add(_title, 1);
        display.Add(arrow, 2);
        AutomationProperties.SetExcludedWithChildren(display, true);
        var layout = new Grid();
        layout.Add(_open);
        layout.Add(ChoiceDisplay(display));
        Content = layout;
        _open.Clicked += OnOpenClicked;
        picker.PropertyChanged += OnPickerChanged;
        UpdateSelection();
    }

    public static void Attach(Picker picker, string captionKey)
    {
        if (Instances.TryGetValue(picker, out _)) return;
        var parent = picker.Parent;
        var frame = parent as Border ?? parent?.Parent as Border;
        if (frame is not null)
        {
            frame.Padding = 0;
            frame.StrokeThickness = 1;
            frame.StrokeShape = new RoundRectangle { CornerRadius = 10 };
            frame.SetDynamicResource(BackgroundColorProperty, "InputBackgroundColor");
            frame.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
        }
        var replacement = new IllustratedQuizPicker(picker, captionKey, frame);
        if (parent is Grid grid)
        {
            // These picker hosts contain only the picker and a decorative native chevron.
            grid.Children.Clear();
            grid.Add(replacement);
        }
        else if (parent is Border border) border.Content = replacement;
        else throw new InvalidOperationException("Illustrated picker needs a Grid or Border host.");
        Instances.Add(picker, replacement);
    }

    public static void SetKeys(Picker picker, IEnumerable<string> keys)
    {
        if (!Instances.TryGetValue(picker, out var instance)) return;
        instance._keys = keys.ToArray();
        instance.UpdateSelection();
    }

    private void OnPickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Picker.SelectedIndex) or nameof(Picker.SelectedItem)
            or nameof(Picker.ItemsSource) or nameof(Picker.IsEnabled)) UpdateSelection();
    }

    private void UpdateSelection()
    {
        // Picker.Items already applies ItemDisplayBinding for object sources.
        // This keeps translated model names (such as GeometryFormulaItem.Name)
        // instead of displaying the object's CLR type name.
        int index = _picker.SelectedIndex;
        _title.Text = index >= 0 && index < _picker.Items.Count ? _picker.Items[index] : Text(_captionKey);
        _open.IsEnabled = _picker.IsEnabled;
        _title.Opacity = _illustration.Opacity = _picker.IsEnabled ? 1 : 0.5;
        string description = "";
        if (index >= 0 && index < _keys.Length)
        {
            var option = QuizChoiceCatalog.Create(index, _keys[index], _title.Text,
                AppLanguageManager.CurrentLanguage, true);
            _illustration.IllustrationId = option.IllustrationId;
            description = option.Description;
        }
        SemanticProperties.SetDescription(_open, Text(_captionKey) + ": " + _title.Text);
        SemanticProperties.SetHint(_open, description);
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        if (_opening || !IsEnabled || !_picker.IsEnabled || _keys.Length == 0) return;
        _opening = true;
        _open.IsEnabled = false;
        try
        {
            // Freeze options for this dialog. Original indexes survive filtering and resizing.
            var choices = _keys.Select((key, index) => QuizChoiceCatalog.Create(index, key,
                _picker.Items[index],
                AppLanguageManager.CurrentLanguage, index == _picker.SelectedIndex)).ToArray();
            var page = new QuizChoicePage(Text(_captionKey), choices);
            int? selected = await page.ChooseAsync(Navigation);
            if (selected.HasValue && IsEnabled && _picker.IsEnabled) _picker.SelectedIndex = selected.Value;
        }
        finally
        {
            _opening = false;
            UpdateSelection();
            if (IsLoaded && _open.Handler?.PlatformView is not null) _open.Focus();
        }
    }

    internal static string Text(string key) => QuizContentCatalog.Text(AppLanguageManager.CurrentLanguage, key);

    internal static ContentView ChoiceDisplay(Grid display)
    {
        // WinUI layouts make only their background input-transparent; nested
        // layouts can still intercept the pointer above the Button. A ContentView
        // uses ViewHandler's subtree hit-testing switch, including blank spaces.
        display.InputTransparent = true;
        display.CascadeInputTransparent = true;
        var overlay = new ContentView { Content = display, InputTransparent = true, Padding = 0 };
        AutomationProperties.SetExcludedWithChildren(overlay, true);
        return overlay;
    }

    internal static Button ChoiceButton(bool drawFocusBorder = true)
    {
        var button = new Button { Text = "", Padding = 0, CornerRadius = 12,
            MinimumHeightRequest = 48, BackgroundColor = Colors.Transparent, BorderWidth = 0 };
        InteractiveButtonAnimation.SetIsPressAnimationEnabled(button, false);
        button.SetDynamicResource(Button.BorderColorProperty, "PrimaryColor");
        if (drawFocusBorder)
        {
            void UpdateFocusBorder()
            {
                // DisconnectHandler clears PlatformView before raising Unfocused.
                // Updating BorderWidth then invokes a mapper on a disconnected handler.
                if (button.Handler?.PlatformView is null) return;
                button.BorderWidth = button.IsFocused ? 2 : 0;
            }
            button.Focused += (_, _) => UpdateFocusBorder();
            button.Unfocused += (_, _) => UpdateFocusBorder();
            // A recycled/reconnected button may retain its last focused width.
            button.Loaded += (_, _) => UpdateFocusBorder();
        }
        return button;
    }
}

/// <summary>A bounded modal with one scroll owner; layout follows content width, not platform.</summary>
internal sealed class QuizChoicePage : ContentPage
{
#if WINDOWS
    // WinUI's scrollbar overlays the ItemsPresenter. Reserve space inside that
    // presenter, not outside the CollectionView (which would move the bar too).
    private const double ScrollbarGutter = 24;
#else
    private const double ScrollbarGutter = 0;
#endif
    private readonly IReadOnlyList<QuizChoiceOption> _choices;
    private readonly TaskCompletionSource<int?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CollectionView _list;
    private readonly SearchBar _search;
    private readonly Border _card;
    private readonly GridItemsLayout _itemsLayout = new(1, ItemsLayoutOrientation.Vertical)
        { HorizontalItemSpacing = 10, VerticalItemSpacing = 10 };
    private bool _closing;
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _keyboardSurface;
    private Microsoft.UI.Xaml.Input.KeyEventHandler? _keyHandler;
#endif

    public QuizChoicePage(string title, IReadOnlyList<QuizChoiceOption> choices)
    {
        _choices = choices;
        Shell.SetNavBarIsVisible(this, false);
        SetDynamicResource(BackgroundColorProperty, "PageBackgroundColor");
        var heading = new Label { Text = title, FontSize = 20, FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center };
        heading.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        var close = new Button { Text = IllustratedQuizPicker.Text("Choice.Close"), MinimumHeightRequest = 48,
            Padding = new Thickness(12, 6), CornerRadius = 10 };
        close.SetDynamicResource(Button.BackgroundColorProperty, "SurfaceAltColor");
        close.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
        close.Clicked += async (_, _) => await CloseAsync(null);
        var header = new Grid { ColumnDefinitions = new() { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
        header.Add(heading);
        header.Add(close, 1);
        _search = new SearchBar { Placeholder = IllustratedQuizPicker.Text("Choice.Search"), MinimumHeightRequest = 48 };
        _search.SetDynamicResource(SearchBar.TextColorProperty, "TextPrimaryColor");
        _search.SetDynamicResource(SearchBar.PlaceholderColorProperty, "TextSecondaryColor");
        SemanticProperties.SetDescription(_search, IllustratedQuizPicker.Text("Choice.Search"));
        _search.TextChanged += (_, _) => ApplyFilter();
        _list = new CollectionView { SelectionMode = SelectionMode.None, ItemsLayout = _itemsLayout,
            ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
            ItemTemplate = new DataTemplate(CreateChoiceCard),
            EmptyView = new Label { Text = IllustratedQuizPicker.Text("Choice.Empty"), FontSize = 16,
                HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(12) } };
#if WINDOWS
        _list.HandlerChanged += (_, _) => ApplyScrollbarGutter();
        _list.Loaded += (_, _) => ApplyScrollbarGutter();
#endif
        var body = new Grid { RowDefinitions = new() { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) }, RowSpacing = 12 };
        body.Add(header);
        body.Add(_search, 0, 1);
        body.Add(_list, 0, 2);
        _card = new Border { Content = body, Padding = 16, StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _card.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
        _card.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
        var root = new Grid();
        root.Add(_card);
        Content = root;
        SizeChanged += (_, _) => Resize();
        ApplyFilter();
    }

    public async Task<int?> ChooseAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this);
        return await _completion.Task;
    }

    private void Resize()
    {
        if (Width <= 0 || Height <= 0) return;
        _card.WidthRequest = Math.Max(1, Math.Min(1100, Width - 24));
        _card.HeightRequest = Math.Max(1, Math.Min(780, Height - 24));
        double scale = 1;
#if ANDROID
        scale = Math.Max(1, Android.App.Application.Context.Resources?.Configuration?.FontScale ?? 1);
#elif WINDOWS
        scale = Math.Max(1, new global::Windows.UI.ViewManagement.UISettings().TextScaleFactor);
#endif
        int span = ResponsiveLayoutPolicy.Columns(_card.WidthRequest - 34 - ScrollbarGutter, 320 * scale, 3, 10);
        if (_itemsLayout.Span != span) _itemsLayout.Span = span;
    }

#if WINDOWS
    private void ApplyScrollbarGutter()
    {
        if (_list.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ListViewBase nativeList)
        {
            // FormsGridView binds this padding to its ItemsPresenter. The wrap
            // grid then divides the remaining width evenly between all columns.
            nativeList.Padding = new Microsoft.UI.Xaml.Thickness(0, 0, ScrollbarGutter, 0);
        }
    }
#endif

    private void ApplyFilter()
    {
        _list.ItemsSource = QuizChoiceCatalog.Filter(_choices, _search.Text ?? "");
    }

    private View CreateChoiceCard()
    {
        var button = IllustratedQuizPicker.ChoiceButton();
        var icon = new QuizChoiceIllustration { WidthRequest = 54, HeightRequest = 54,
            VerticalOptions = LayoutOptions.Center };
        var title = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap };
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        var description = new Label { FontSize = 13, MaxLines = 3, LineBreakMode = LineBreakMode.WordWrap };
        description.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
        var check = new Label { Text = "✓", FontSize = 22, FontAttributes = FontAttributes.Bold };
        check.SetDynamicResource(Label.TextColorProperty, "PrimaryColor");
        var text = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        text.Add(title);
        text.Add(description);
        var display = new Grid { ColumnDefinitions = new() { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 10,
            Padding = 12, InputTransparent = true, MinimumHeightRequest = 102 };
        display.Add(icon);
        display.Add(text, 1);
        display.Add(check, 2);
        AutomationProperties.SetExcludedWithChildren(display, true);
        var layout = new Grid();
        layout.Add(button);
        layout.Add(IllustratedQuizPicker.ChoiceDisplay(display));
        var card = new Border { Content = layout, StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 }, Margin = new Thickness(2) };
        card.BindingContextChanged += (_, _) =>
        {
            if (card.BindingContext is not QuizChoiceOption option) return;
            title.Text = option.Title;
            description.Text = option.Description;
            icon.IllustrationId = option.IllustrationId;
            check.IsVisible = option.IsSelected;
            card.StrokeThickness = option.IsSelected ? 2 : 1;
            card.SetDynamicResource(Border.StrokeProperty, option.IsSelected ? "PrimaryBorderBrush" : "BorderBrush");
            card.SetDynamicResource(BackgroundColorProperty, option.IsSelected ? "PrimarySoftColor" : "SurfaceAltColor");
            SemanticProperties.SetDescription(button, option.Title + ". " + option.Description
                + (option.IsSelected ? ". " + IllustratedQuizPicker.Text("Choice.Selected") : ""));
        };
        button.Clicked += async (_, _) =>
        {
            if (card.BindingContext is QuizChoiceOption option) await CloseAsync(option.Index);
        };
        return card;
    }

    private async Task CloseAsync(int? selection)
    {
        if (_closing) return;
        _closing = true;
        try
        {
            await Navigation.PopModalAsync();
            _completion.TrySetResult(selection);
        }
        catch
        {
            _closing = false;
            throw;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Resize();
        LocalizationService.CultureChanged += OnCultureChanged;
#if WINDOWS
        _keyboardSurface = Content.Handler?.PlatformView as Microsoft.UI.Xaml.UIElement;
        _keyHandler = OnKeyDown;
        _keyboardSurface?.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, _keyHandler, true);
        Dispatcher.Dispatch(() => _search.Focus());
#endif
        var selected = _choices.FirstOrDefault(option => option.IsSelected);
        if (selected is not null) Dispatcher.Dispatch(() => _list.ScrollTo(selected, position: ScrollToPosition.MakeVisible));
    }

    protected override void OnDisappearing()
    {
        LocalizationService.CultureChanged -= OnCultureChanged;
#if WINDOWS
        if (_keyHandler is not null)
            _keyboardSurface?.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, _keyHandler);
        _keyboardSurface = null;
        _keyHandler = null;
#endif
        // An external navigation may dismiss the modal without the Close button.
        if (!_closing) _completion.TrySetResult(null);
        base.OnDisappearing();
    }

    private void OnCultureChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(async () => await CloseAsync(null));
#if WINDOWS
    private async void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != global::Windows.System.VirtualKey.Escape) return;
        e.Handled = true;
        await CloseAsync(null);
    }
#endif
}
