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
    private bool _themeSubscribed;
    private int _themeRefreshGeneration;

    private IllustratedQuizPicker(Picker picker, string captionKey, Border? frame)
    {
        _picker = picker;
        _captionKey = captionKey;
        DisplayDensityManager.BindControl(this);
        _open = ChoiceButton(drawFocusBorder: false);
        Action? refreshFrameFocus = null;
        // Focus belongs to the field's single outer outline, not a second inner
        // button outline. Keep stroke thickness constant so content never shifts.
        if (frame is not null)
        {
            void UpdateFrameFocus()
            {
                if (_open.Handler?.PlatformView is null || frame.Handler?.PlatformView is null) return;
                frame.SetDynamicResource(Border.StrokeProperty, _open.IsFocused ? "PrimaryColor" : "BorderBrush");
                if (_open.IsFocused && _illustration?.IsThemeGroup == true)
                    frame.Stroke = new SolidColorBrush(QuizChoiceIllustration.ReadableInk(
                        ThemeResource.GetColor("PrimaryColor", "#2563EB"), ThemeResource.GetColor("InputBackgroundColor", "#FFFFFF")));
            }
            refreshFrameFocus = UpdateFrameFocus;
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
        _illustration.SetDynamicResource(QuizChoiceIllustration.InkSurfaceColorProperty, "InputBackgroundColor");
        _illustration.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(QuizChoiceIllustration.InkColor) or nameof(QuizChoiceIllustration.InkSurfaceColor)) refreshFrameFocus?.Invoke();
        };
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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        UpdateSelection();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (!_themeSubscribed)
        {
            AppThemeManager.ThemeChanged += OnThemeChanged;
            _themeSubscribed = true;
        }
        QueueThemeTextRefresh();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        AppThemeManager.ThemeChanged -= OnThemeChanged;
        _themeSubscribed = false;
        ++_themeRefreshGeneration;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => QueueThemeTextRefresh();

    private void QueueThemeTextRefresh()
    {
        int generation = ++_themeRefreshGeneration;
        void Refresh()
        {
            if (!_themeSubscribed || generation != _themeRefreshGeneration) return;
            // The label is an overlay, not the backing Picker's native text.
            // A disconnected WinUI resource listener can interrupt DynamicResource
            // notifications. Read the committed palette after the theme transaction,
            // including when returning to a page created under an earlier theme.
            if (Application.Current?.Resources.TryGetValue("TextPrimaryColor", out var value) == true
                && value is Color primary)
            {
                try { _title.TextColor = _illustration.IsThemeGroup
                    ? QuizChoiceIllustration.ReadableInk(primary, ThemeResource.GetColor("InputBackgroundColor", "#FFFFFF")) : primary; }
                catch (Exception error) when (error is System.Runtime.InteropServices.COMException or ObjectDisposedException)
                { System.Diagnostics.Debug.WriteLine($"Illustrated picker theme text deferred: {error}"); }
            }
        }
        Dispatcher.Dispatch(Refresh);
#if WINDOWS
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(180), Refresh);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(420), Refresh);
#endif
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

    /// <summary>Uses the shared chooser for text choices without a catalogue illustration.</summary>
    public static void SetTextOptions(Picker picker)
        => SetKeys(picker, Enumerable.Repeat("", picker.Items.Count));

    public static bool IsChoosing(Picker picker)
        => Instances.TryGetValue(picker, out var instance) && instance._opening;

    /// <summary>The same difficulty labels are used by AI setup and Excel authoring.</summary>
    public static string[] DifficultyLabels() => Enumerable.Range(1, 5).Select(stars =>
        new string('★', stars) + "  " + (stars == 1
            ? LocalizationService.TranslateKey("AiBank.TemplateOneStar")
            : string.Format(System.Globalization.CultureInfo.CurrentCulture,
                LocalizationService.TranslateKey("AiBank.TemplateStarCount"), stars))).ToArray();

    /// <summary>Uses the same searchable, responsive selector for flows without a backing Picker.</summary>
    public static Task<int?> ChooseAsync(INavigation navigation, string title,
        IReadOnlyList<QuizChoiceOption> choices, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        return choices.Count == 0 ? Task.FromResult<int?>(null)
            : new QuizChoicePage(title, choices).ChooseAsync(navigation, cancellation);
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
        _illustration.IsVisible = index >= 0 && index < _keys.Length && !string.IsNullOrEmpty(_keys[index]);
        _illustration.IsThemeGroup = _illustration.IsVisible && QuizThemeIconPolicy.IsGroupKey(_keys[index]);
        if (_illustration.IsVisible)
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
        QuizChoicePage? page = null;
        try
        {
            // Freeze options for this dialog. Original indexes survive filtering and resizing.
            var choices = _keys.Select((key, index) => string.IsNullOrEmpty(key)
                ? new QuizChoiceOption(index, "", _picker.Items[index], "", "", index == _picker.SelectedIndex)
                : QuizChoiceCatalog.Create(index, key, _picker.Items[index],
                    AppLanguageManager.CurrentLanguage, index == _picker.SelectedIndex)).ToArray();
            page = new QuizChoicePage(Text(_captionKey), choices);
            int? selected = await page.ChooseAsync(Navigation);
            if (selected.HasValue && IsEnabled && _picker.IsEnabled) _picker.SelectedIndex = selected.Value;
        }
        finally
        {
            _opening = false;
            if (page?.WasHostDestroyed != true)
            {
                UpdateSelection();
                if (IsLoaded && _open.Handler?.PlatformView is not null) _open.Focus();
            }
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
        DisplayDensityManager.BindControl(button, square: true);
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
    // CV2 caches the first template's pre-binding height in its WrapperView.
    // Choice cards update HeightRequest after the dialog has measured, so each
    // realized Windows card must be remeasured rather than reuse that cache.
    private const ItemSizingStrategy GridSizingStrategy = ItemSizingStrategy.MeasureAllItems;
#else
    private const double ScrollbarGutter = 0;
    private const ItemSizingStrategy GridSizingStrategy = ItemSizingStrategy.MeasureFirstItem;
#endif
    private readonly IReadOnlyList<QuizChoiceOption> _choices;
    private readonly TaskCompletionSource<int?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CollectionView _list;
    private readonly SearchBar _search;
    private readonly Label _heading;
    private readonly Button _close;
    private readonly Button _compactClose;
    private readonly Grid _header;
    private readonly Grid _searchRow;
    private readonly Grid _root;
    private readonly Border _card;
    public static readonly BindableProperty ChoiceCardHeightProperty = BindableProperty.Create(
        nameof(ChoiceCardHeight), typeof(double), typeof(QuizChoicePage), 136d);
    public double ChoiceCardHeight
    {
        get => (double)GetValue(ChoiceCardHeightProperty);
        private set => SetValue(ChoiceCardHeightProperty, value);
    }
    public static readonly BindableProperty ChoiceIconSizeProperty = BindableProperty.Create(
        nameof(ChoiceIconSize), typeof(double), typeof(QuizChoicePage), 54d);
    public double ChoiceIconSize
    {
        get => (double)GetValue(ChoiceIconSizeProperty);
        private set => SetValue(ChoiceIconSizeProperty, value);
    }
    public static readonly BindableProperty ChoiceTextMaxLinesProperty = BindableProperty.Create(
        nameof(ChoiceTextMaxLines), typeof(int), typeof(QuizChoicePage), 3);
    public int ChoiceTextMaxLines
    {
        get => (int)GetValue(ChoiceTextMaxLinesProperty);
        private set => SetValue(ChoiceTextMaxLinesProperty, value);
    }
    private readonly LinearItemsLayout _listLayout = new(ItemsLayoutOrientation.Vertical) { ItemSpacing = 10 };
    private readonly GridItemsLayout _itemsLayout = new(1, ItemsLayoutOrientation.Vertical)
        { HorizontalItemSpacing = 10, VerticalItemSpacing = 10 };
    private bool _closing;
    private bool _dismissed;
    private bool _visible;
    private bool _resizing;
    private bool _hostDestroyed;
    private Window? _hostWindow;
#if ANDROID
    private Android.Views.View? _androidViewport;
    private Android.Views.ViewTreeObserver? _androidObserver;
#endif
    internal bool WasHostDestroyed => _hostDestroyed;
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _keyboardSurface;
    private Microsoft.UI.Xaml.Input.KeyEventHandler? _keyHandler;
#endif

    public QuizChoicePage(string title, IReadOnlyList<QuizChoiceOption> choices)
    {
        _choices = choices;
        Title = title;
        SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.All;
        HideSoftInputOnTapped = true;
        Shell.SetNavBarIsVisible(this, false);
        SetDynamicResource(BackgroundColorProperty, "PageBackgroundColor");
        _heading = new Label { Text = title, FontSize = 20, FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
        _heading.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetHeadingLevel(_heading, SemanticHeadingLevel.Level1);
        SemanticProperties.SetDescription(_heading, title);
        _close = new Button { Text = IllustratedQuizPicker.Text("Choice.Close"), MinimumHeightRequest = 48, MinimumWidthRequest = 48,
            Padding = new Thickness(12, 6), CornerRadius = 10 };
        _close.SetDynamicResource(Button.BackgroundColorProperty, "SurfaceAltColor");
        _close.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetDescription(_close, IllustratedQuizPicker.Text("Choice.Close"));
        ToolTipProperties.SetText(_close, IllustratedQuizPicker.Text("Choice.Close"));
        _close.Clicked += async (_, _) => await CloseAsync(null);
        _header = new Grid { ColumnDefinitions = new() { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
        _header.Add(_heading);
        _header.Add(_close, 1);
        _header.SizeChanged += (_, _) => Resize();
        bool themeChoices = choices.Count > 0 && choices.All(option =>
            option.Key.StartsWith("Learning.Group.", StringComparison.Ordinal));
        string searchText = IllustratedQuizPicker.Text(themeChoices ? "Choice.SearchThemes" : "Choice.Search");
        _search = new SearchBar { Placeholder = searchText, MinimumHeightRequest = 48 };
        _search.SetDynamicResource(SearchBar.TextColorProperty, "TextPrimaryColor");
        _search.SetDynamicResource(SearchBar.PlaceholderColorProperty, "TextSecondaryColor");
        SemanticProperties.SetDescription(_search, searchText);
        _search.TextChanged += (_, _) => ApplyFilter();
        _search.SearchButtonPressed += OnSearchSubmitted;
        _search.SizeChanged += (_, _) => Resize();
        _compactClose = new Button { Text = "×", FontSize = 24, FontAutoScalingEnabled = false,
            MinimumHeightRequest = 48, MinimumWidthRequest = 48, Padding = 0, CornerRadius = 10, IsVisible = false };
        _compactClose.SetDynamicResource(Button.BackgroundColorProperty, "SurfaceAltColor");
        _compactClose.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetDescription(_compactClose, IllustratedQuizPicker.Text("Choice.Close"));
        ToolTipProperties.SetText(_compactClose, IllustratedQuizPicker.Text("Choice.Close"));
        _compactClose.Clicked += async (_, _) => await CloseAsync(null);
        _searchRow = new Grid { ColumnDefinitions = new() { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
        _searchRow.Add(_search);
        _searchRow.Add(_compactClose, 1);
        _searchRow.SizeChanged += (_, _) => Resize();
        _list = new CollectionView { SelectionMode = SelectionMode.None, ItemsLayout = _itemsLayout,
            ItemSizingStrategy = GridSizingStrategy,
            VerticalScrollBarVisibility = ScrollBarVisibility.Default,
            ItemTemplate = new DataTemplate(CreateChoiceCard),
            EmptyView = new Label { Text = IllustratedQuizPicker.Text(themeChoices ? "Choice.EmptyThemes" : "Choice.Empty"), FontSize = 16,
                HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(12) } };
#if WINDOWS
        _list.HandlerChanged += (_, _) => ApplyWindowsLayout();
        _list.Loaded += (_, _) => ApplyWindowsLayout();
#endif
        var body = new Grid { RowDefinitions = new() { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) }, RowSpacing = 12 };
        body.Add(_header);
        body.Add(_searchRow, 0, 1);
        body.Add(_list, 0, 2);
        _card = new Border { Content = body, Padding = 16, StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _card.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
        _card.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
        // Measure the content area after the page has avoided system bars,
        // cutouts and the soft keyboard, rather than the full page bounds.
        _root = new Grid { SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.None };
        _root.Add(_card);
        _root.SizeChanged += (_, _) => Resize();
        Content = _root;
#if ANDROID
        Loaded += (_, _) =>
        {
            if (_dismissed || _androidViewport is not null || Handler?.PlatformView is not Android.Views.View native) return;
            _androidViewport = native;
            _androidObserver = native.ViewTreeObserver;
            if (_androidObserver is { IsAlive: true }) _androidObserver.GlobalLayout += OnAndroidViewportLayout;
            OnAndroidViewportLayout(this, EventArgs.Empty);
        };
#endif
#if WINDOWS
        Loaded += (_, _) =>
        {
            AttachWindowsKeyboard();
            Dispatcher.Dispatch(() => { if (CanUseDialog && DeviceInfo.Idiom == DeviceIdiom.Desktop) _search.Focus(); });
        };
#endif
        SizeChanged += (_, _) => Resize();
        Unloaded += (_, _) => EndLifetime();
        ApplyFilter();
    }

    public async Task<int?> ChooseAsync(INavigation navigation, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        await navigation.PushModalAsync(this);
        // Register after the push so cancellation cannot pop the underlying page.
        using var registration = cancellation.Register(() => Dispatcher.Dispatch(async () =>
        {
            if (!_dismissed) await CloseAsync(null);
        }));
        int? selected = await _completion.Task;
        cancellation.ThrowIfCancellationRequested();
        return selected;
    }

    private void Resize()
    {
        if (_resizing || _closing || _dismissed || !_visible || _root.Width <= 0 || _root.Height <= 0) return;
        _resizing = true;
        try
        {
            // With a keyboard in landscape, keep search and Close on one row so
            // the list still has usable height. The page's accessible Title remains.
            double viewportHeight = Math.Max(1, _root.Height - _root.Padding.VerticalThickness);
            bool shortInputArea = viewportHeight < 260;
            if (_list.Parent is Grid body) body.RowSpacing = viewportHeight < 120 ? 0 : viewportHeight < 180 ? 4 : 12;
            _card.StrokeThickness = viewportHeight < 120 ? 0 : 1;
            _header.IsVisible = !shortInputArea;
            _compactClose.IsVisible = shortInputArea;
            // Searching changes the items, not the position/size of the field being
            // typed into. Fit the original catalogue, including an empty result set.
            var layout = QuizChoiceLayout.Calculate(_root.Width, viewportHeight, _choices.Count,
                ResponsiveLayoutPolicy.TextScale, ScrollbarGutter,
                _searchRow.Height > 0 ? (_header.IsVisible ? Math.Max(48, _header.Height) : 0) + _searchRow.Height : 0,
                forceSingleColumn: DeviceInfo.Idiom == DeviceIdiom.Phone);
            _card.WidthRequest = layout.Width;
            _card.HeightRequest = layout.Height;
            _card.Padding = layout.Padding;
            ChoiceCardHeight = layout.UseList ? -1 : layout.ItemHeight - 4;
            ChoiceIconSize = layout.UseList ? 44 : 54;
            ChoiceTextMaxLines = layout.UseList ? -1 : 3;
            _heading.MaxLines = viewportHeight < 400 ? 1 : 2;
            _close.Text = layout.Width < 600 * ResponsiveLayoutPolicy.TextScale ? "×" : IllustratedQuizPicker.Text("Choice.Close");
            _close.FontSize = _close.Text == "×" ? 24 : 14;
            _close.FontAutoScalingEnabled = _close.Text != "×";
            if (_itemsLayout.Span != layout.Columns) _itemsLayout.Span = layout.Columns;
            IItemsLayout itemsLayout = layout.UseList ? _listLayout : _itemsLayout;
            if (!ReferenceEquals(_list.ItemsLayout, itemsLayout)) _list.ItemsLayout = itemsLayout;
            _list.ItemSizingStrategy = layout.UseList ? ItemSizingStrategy.MeasureAllItems : GridSizingStrategy;
#if WINDOWS
            ApplyWindowsLayout();
#endif
        }
        finally { _resizing = false; }
    }

#if WINDOWS
    private void ApplyWindowsLayout()
    {
        if (_dismissed) return;
        if (_list.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control nativeList)
        {
            // Both the MAUI 11 ItemsView and the legacy ListViewBase expose
            // Padding on Control. Keep the last card clear of the scroll bar.
            nativeList.Padding = new Microsoft.UI.Xaml.Thickness(0, 0, ScrollbarGutter, 0);
            if (nativeList is Microsoft.UI.Xaml.Controls.ItemsView itemsView &&
                itemsView.Layout is Microsoft.UI.Xaml.Controls.UniformGridLayout grid &&
                _list.ItemsLayout is GridItemsLayout columns && _card.WidthRequest > 0)
            {
                // MAUI 11 uses UniformGridLayout: Span is only a maximum, and
                // auto width caches the first template's full-row measurement.
                // Supply the cross-axis size explicitly, using the same spacing
                // adjustment as MAUI's ApplyMinItemSizeForSpan. Subtract our
                // inner padding and scrollbar gutter before dividing into cells.
                double width = _card.WidthRequest - _card.Padding.HorizontalThickness
                    - 2 * _card.StrokeThickness - ScrollbarGutter;
                grid.MinItemWidth = Math.Max(1, Math.Floor(width / columns.Span) - columns.HorizontalItemSpacing);
                // The first native measurement can precede the responsive
                // HeightRequest binding. Do not retain that smaller cached row
                // height: reserve the whole Border plus its 2px outer margins.
                grid.MinItemHeight = ChoiceCardHeight + 4;
            }
        }
    }
#endif

    private void ApplyFilter()
    {
        var visible = QuizChoiceCatalog.Filter(_choices, _search.Text ?? "");
        _list.ItemsSource = visible;
    }

    private async void OnSearchSubmitted(object? sender, EventArgs e)
    {
        if (!CanUseDialog) return;
        try
        {
            await _search.HideSoftInputAsync(CancellationToken.None);
            if (CanUseDialog && DeviceInfo.Platform == DevicePlatform.Android) _search.Unfocus();
        }
        catch (Exception error) when (_dismissed && error is (ObjectDisposedException
            or System.Runtime.InteropServices.COMException or OperationCanceledException))
        {
            // The host can close while the keyboard is being dismissed.
        }
    }

    private View CreateChoiceCard()
    {
        static void RestoreResource(Element view, BindableProperty property, string key)
        {
            // Group contrast assigns a local value. Reusing the same resource key
            // alone is a no-op in MAUI; detach first when a template is rebound.
            view.RemoveDynamicResource(property);
            view.ClearValue(property);
            view.SetDynamicResource(property, key);
        }
        var button = IllustratedQuizPicker.ChoiceButton();
        var icon = new QuizChoiceIllustration { WidthRequest = 54, HeightRequest = 54,
            VerticalOptions = LayoutOptions.Center };
        icon.SetBinding(WidthRequestProperty, new Binding(nameof(ChoiceIconSize), source: this));
        icon.SetBinding(HeightRequestProperty, new Binding(nameof(ChoiceIconSize), source: this));
        var title = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap };
        title.SetBinding(Label.MaxLinesProperty, new Binding(nameof(ChoiceTextMaxLines), source: this));
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        var description = new Label { FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
        description.SetBinding(Label.MaxLinesProperty, new Binding(nameof(ChoiceTextMaxLines), source: this));
        description.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
        var check = new Label { Text = "✓", FontSize = 22, FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Start };
        check.SetDynamicResource(Label.TextColorProperty, "PrimaryColor");
        var text = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        // The check mark shares only the title row. Descriptions retain the full
        // text width on phones rather than losing another column to the check.
        var titleRow = new Grid { ColumnDefinitions = new() { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 6 };
        titleRow.Add(title);
        titleRow.Add(check, 1);
        text.Add(titleRow);
        text.Add(description);
        var display = new Grid { ColumnDefinitions = new() { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 10,
            Padding = 12, InputTransparent = true };
        display.Add(icon);
        display.Add(text, 1);
        AutomationProperties.SetExcludedWithChildren(display, true);
        var layout = new Grid();
        layout.Add(button);
        layout.Add(IllustratedQuizPicker.ChoiceDisplay(display));
        var card = new Border { Content = layout, StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 }, Margin = new Thickness(2), MinimumHeightRequest = 96 };
        icon.SetBinding(QuizChoiceIllustration.InkSurfaceColorProperty, new Binding(nameof(BackgroundColor), source: card));
        void RefreshGroupColors()
        {
            if (card.BindingContext is not QuizChoiceOption option || !QuizThemeIconPolicy.IsGroupKey(option.Key)) return;
            var surface = card.BackgroundColor ?? Colors.White;
            var primary = QuizChoiceIllustration.ReadableInk(ThemeResource.GetColor("TextPrimaryColor", "#1E293B"), surface);
            var secondary = QuizChoiceIllustration.ReadableInk(ThemeResource.GetColor("TextSecondaryColor", "#64748B"), surface);
            var accent = QuizChoiceIllustration.ReadableInk(ThemeResource.GetColor("PrimaryColor", "#2563EB"), surface);
            title.TextColor = primary; description.TextColor = secondary; check.TextColor = accent;
            button.BorderColor = accent;
            if (option.IsSelected) card.Stroke = new SolidColorBrush(accent);
        }
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BackgroundColor)) RefreshGroupColors();
        };
        icon.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(QuizChoiceIllustration.InkColor)) RefreshGroupColors();
        };
        card.SetBinding(HeightRequestProperty, new Binding(nameof(ChoiceCardHeight), source: this));
        card.BindingContextChanged += (_, _) =>
        {
            if (card.BindingContext is not QuizChoiceOption option) { icon.IsThemeGroup = false; return; }
            bool group = QuizThemeIconPolicy.IsGroupKey(option.Key);
            title.Text = option.Title;
            description.Text = group ? option.Summary ?? option.Description : option.Description;
            description.IsVisible = !string.IsNullOrWhiteSpace(option.Description);
            card.SetDynamicResource(BackgroundColorProperty, option.IsSelected ? "PrimarySoftColor" : "SurfaceAltColor");
            icon.IsThemeGroup = group;
            icon.IllustrationId = option.IllustrationId;
            icon.IsVisible = !string.IsNullOrWhiteSpace(option.IllustrationId);
            check.IsVisible = option.IsSelected;
            card.StrokeThickness = option.IsSelected ? 2 : 1;
            card.SetDynamicResource(Border.StrokeProperty, option.IsSelected ? "PrimaryBorderBrush" : "BorderBrush");
            RestoreResource(title, Label.TextColorProperty, "TextPrimaryColor");
            RestoreResource(description, Label.TextColorProperty, "TextSecondaryColor");
            RestoreResource(check, Label.TextColorProperty, "PrimaryColor");
            RestoreResource(button, Button.BorderColorProperty, "PrimaryColor");
            RefreshGroupColors();
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
        if (_closing || _dismissed) return;
        _closing = true;
        try
        {
            await Navigation.PopModalAsync();
            _completion.TrySetResult(selection);
        }
        catch (Exception error) when (_hostDestroyed && error is (ObjectDisposedException
            or System.Runtime.InteropServices.COMException or OperationCanceledException))
        {
            _completion.TrySetResult(null);
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
        _visible = true;
        _hostWindow = Window;
        if (_hostWindow is not null) _hostWindow.Destroying += OnHostDestroying;
        Resize();
        LocalizationService.CultureChanged += OnCultureChanged;
#if WINDOWS
        AttachWindowsKeyboard();
        // Touch tablets should not open their on-screen keyboard on arrival.
        Dispatcher.Dispatch(() => { if (CanUseDialog && DeviceInfo.Idiom == DeviceIdiom.Desktop) _search.Focus(); });
#endif
        var selected = _choices.FirstOrDefault(option => option.IsSelected);
        if (selected is not null) Dispatcher.Dispatch(() =>
        { if (CanUseDialog) _list.ScrollTo(selected, position: ScrollToPosition.MakeVisible); });
    }

    protected override void OnDisappearing()
    {
        EndLifetime();
        base.OnDisappearing();
    }

    private bool CanUseDialog => _visible && !_dismissed && !_closing && IsLoaded
        && Handler?.PlatformView is not null && _hostWindow?.Handler?.PlatformView is not null;

    private void OnHostDestroying(object? sender, EventArgs e)
    {
        _hostDestroyed = true;
        EndLifetime();
        _completion.TrySetResult(null);
    }

    private void EndLifetime()
    {
        if (_dismissed) return;
        _visible = false;
        _dismissed = true;
        LocalizationService.CultureChanged -= OnCultureChanged;
        if (_hostWindow is not null) _hostWindow.Destroying -= OnHostDestroying;
        _hostWindow = null;
#if ANDROID
        if (_androidObserver is { IsAlive: true } observer) observer.GlobalLayout -= OnAndroidViewportLayout;
        _androidObserver = null;
        _androidViewport = null;
#endif
#if WINDOWS
        try
        {
            if (!_hostDestroyed && _keyHandler is not null)
                _keyboardSurface?.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, _keyHandler);
        }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        {
            System.Diagnostics.Debug.WriteLine($"Picker keyboard surface already disconnected: {error.Message}");
        }
        _keyboardSurface = null;
        _keyHandler = null;
#endif
        // Navigation can remove the modal without calling CloseAsync.
        if (!_closing) _completion.TrySetResult(null);
    }

    private void OnCultureChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(async () =>
    { if (CanUseDialog) await CloseAsync(null); });
#if ANDROID
    private void OnAndroidViewportLayout(object? sender, EventArgs e)
    {
        if (_dismissed || _androidViewport is null || _root.Handler?.PlatformView is not Android.Views.View nativeRoot) return;
        var insets = AndroidX.Core.View.ViewCompat.GetRootWindowInsets(_androidViewport);
        if (insets is null) return;
        bool keyboard = insets.IsVisible(AndroidX.Core.View.WindowInsetsCompat.Type.Ime());
        double overlap = 0;
        if (keyboard)
        {
            // Account only for overlap: the modal may already consume system bars
            // or resize partway. Root padding itself does not move its bottom edge.
            var decor = _androidViewport.RootView!;
            int[] windowPosition = new int[2], rootPosition = new int[2];
            decor.GetLocationOnScreen(windowPosition); nativeRoot.GetLocationOnScreen(rootPosition);
            int keyboardTop = windowPosition[1] + decor.Height - (insets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.Ime())?.Bottom ?? 0);
            double density = nativeRoot.Resources?.DisplayMetrics?.Density ?? 1;
            overlap = Math.Max(0, rootPosition[1] + nativeRoot.Height - keyboardTop) / Math.Max(1, density);
        }
        if (Math.Abs(_root.Padding.Bottom - overlap) > .5)
        {
            _root.Padding = new Thickness(0, 0, 0, overlap);
            Resize();
        }
    }

#endif
#if WINDOWS
    private void AttachWindowsKeyboard()
    {
        // A newly presented modal may appear before its content handler exists.
        // Loaded retries the same attachment; lifetime cleanup removes it once.
        if (!_visible || _dismissed || _keyHandler is not null) return;
        _keyboardSurface = Content.Handler?.PlatformView as Microsoft.UI.Xaml.UIElement;
        if (_keyboardSurface is null) return;
        _keyHandler = OnKeyDown;
        _keyboardSurface.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, _keyHandler, true);
    }

    private async void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != global::Windows.System.VirtualKey.Escape) return;
        e.Handled = true;
        await CloseAsync(null);
    }
#endif
}
