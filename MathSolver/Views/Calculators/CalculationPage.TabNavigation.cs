using MathSolver.Controls;
using MathSolver.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MathSolver.Views;

public partial class CalculationPage
{
    private static readonly string[] CalculationTypeKeys =
        ["Basic", "NumberTheory", "Average", "PowerRoot", "Fraction", "FindX", "Quadratic", "Geometry", "Matrix"];
    private readonly Button[] _calculationTypeChoices = new Button[9];
    private readonly SearchBar _calculationTypeSearch = new();
    private readonly Label _calculationTypeHeading = new(), _calculationTypeEmpty = new();
    private readonly Button _calculationTypeClose = new() { Text = "×", FontSize = 24, Padding = 0 };
    private Border _calculationTypeCard = null!;
    private ScrollView _calculationTypeList = null!;
    private bool _tabNavigationQueued, _tabNavigationStacked, _calculationChooserNeedsFocus;
    private int _tabNavigationGeneration, _calculationChooserFocusGeneration;
    private double _lastTabViewportWidth = -1, _lastTabTextScale = -1, _lastTabDensitySize = -1;

    private bool CanUseTabNavigation => _isPageActive && IsLoaded && Handler?.MauiContext is not null
        && Window is { Handler.MauiContext: not null } window && !WindowUiLifetime.IsClosing(window);

    private void InitializeCalculationNavigation()
    {
        LocalizationService.ExcludeSubtreeFromLegacyTracking(AllCalculationTypesOverlay);
        LocalizationService.ExcludeSubtreeFromLegacyTracking(AllCalculationTypesButton);
        DisplayDensityManager.BindControl(AllCalculationTypesButton);
        InitializeCalculationOverflow();
        var choices = new VerticalStackLayout { Spacing = 8 };
        for (int i = 0; i < _calculationTypeChoices.Length; i++)
        {
            int index = i;
            var button = new Button { Style = (Style)Application.Current!.Resources["SolverSecondaryActionStyle"],
                FontSize = 15, LineBreakMode = LineBreakMode.WordWrap, Padding = new Thickness(12, 10),
                AutomationId = "CalculationType" + CalculationTypeKeys[i] };
            DisplayDensityManager.BindControl(button);
            button.Clicked += async (_, _) =>
            {
                if (!CanUseTabNavigation || !AllCalculationTypesOverlay.IsVisible || _isPowerRootCalculationInteractionLocked || _isSubTabTransitioning) return;
                CloseCalculationTypes(restoreFocus: false);
                await SwitchSubTabAsync((CalculationSubTab)index);
                if (CanUseTabNavigation)
                    (CalculationTabStripHost.IsVisible ? GetSubTabButton(_selectedSubTab) : AllCalculationTypesButton).Focus();
            };
            _calculationTypeChoices[i] = button;
            choices.Add(button);
        }
        _calculationTypeEmpty.FontSize = 14;
        _calculationTypeEmpty.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
        choices.Add(_calculationTypeEmpty);
        _calculationTypeList = new ScrollView { Content = choices, VerticalScrollBarVisibility = ScrollBarVisibility.Default };
        _calculationTypeHeading.FontSize = 20;
        _calculationTypeHeading.FontAttributes = FontAttributes.Bold;
        _calculationTypeHeading.LineBreakMode = LineBreakMode.WordWrap;
        _calculationTypeHeading.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetHeadingLevel(_calculationTypeHeading, SemanticHeadingLevel.Level2);
        _calculationTypeSearch.SetDynamicResource(SearchBar.TextColorProperty, "TextPrimaryColor");
        _calculationTypeSearch.SetDynamicResource(SearchBar.PlaceholderColorProperty, "TextSecondaryColor");
        _calculationTypeSearch.TextChanged += (_, _) => RefreshCalculationChoices();
        DisplayDensityManager.BindControl(_calculationTypeClose, square: true);
        _calculationTypeClose.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
        _calculationTypeClose.SetDynamicResource(Button.BackgroundColorProperty, "SurfaceAltColor");
        _calculationTypeClose.Clicked += (_, _) => CloseCalculationTypes();
        _calculationTypeClose.Loaded += (_, _) => QueueCalculationChooserFocus();
        var search = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        search.Add(_calculationTypeSearch); search.Add(_calculationTypeClose, 1, 0);
        var body = new Grid { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) } };
        body.Add(_calculationTypeHeading); body.Add(search, 0, 1); body.Add(_calculationTypeList, 0, 2);
        _calculationTypeCard = new Border { Content = body, Padding = 16, StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 }, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _calculationTypeCard.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
        _calculationTypeCard.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
        var shade = new BoxView { Color = Colors.Black.WithAlpha(0.35f) };
        var dismiss = new TapGestureRecognizer(); dismiss.Tapped += (_, _) => CloseCalculationTypes();
        shade.GestureRecognizers.Add(dismiss);
        AutomationProperties.SetIsInAccessibleTree(shade, false);
        var overlay = new Grid(); overlay.Add(shade); overlay.Add(_calculationTypeCard);
        AllCalculationTypesOverlay.Content = overlay;
        CalculationTabNavigation.SizeChanged += QueueCalculationNavigation;
        CalculationSubTabScrollView.SizeChanged += QueueCalculationNavigation;
        AndroidCalculationSubTabScrollView.SizeChanged += QueueCalculationNavigation;
        CalculationSubTabGrid.SizeChanged += QueueCalculationNavigation;
        Loaded += QueueCalculationNavigation;
        Unloaded += (_, _) => InvalidateCalculationNavigation();
        HandlerChanging += (_, _) => InvalidateCalculationNavigation();
        AllCalculationTypesOverlay.SizeChanged += QueueCalculationNavigation;
        AllCalculationTypesOverlay.Loaded += (_, _) =>
        { AttachCalculationChooserKeyboard(); QueueCalculationChooserFocus(); };
        AllCalculationTypesOverlay.Unloaded += (_, _) => DetachCalculationChooserKeyboard();
        RefreshCalculationChoices();
    }

    private void InvalidateCalculationNavigation()
    {
        _tabNavigationGeneration++; _tabNavigationQueued = false;
        _lastTabViewportWidth = -1;
        DetachCalculationChooserKeyboard();
    }

    private void QueueCalculationNavigation(object? sender, EventArgs e)
    {
        if (_tabNavigationQueued) return;
        _tabNavigationQueued = true;
        int generation = _tabNavigationGeneration;
        if (!Dispatcher.Dispatch(() =>
        {
            if (generation != _tabNavigationGeneration) return;
            _tabNavigationQueued = false;
            if (!CanUseTabNavigation) return;
            bool stacked = ResponsiveLayoutPolicy.UseStackedLayout(CalculationTabNavigation.Width, 760);
            bool modeChanged = stacked != _tabNavigationStacked;
            if (stacked != _tabNavigationStacked)
            {
                _tabNavigationStacked = stacked;
                CalculationTabStripHost.IsVisible = !stacked;
                Grid.SetRow(AllCalculationTypesButton, 0);
                Grid.SetColumn(AllCalculationTypesButton, stacked ? 0 : 1);
                Grid.SetColumnSpan(AllCalculationTypesButton, stacked ? 2 : 1);
                AllCalculationTypesButton.HorizontalOptions = stacked ? LayoutOptions.Fill : LayoutOptions.End;
            }
            double controlSize = DisplayDensityManager.Metrics.ControlHeight;
            AllCalculationTypesButton.WidthRequest = AllCalculationTypesButton.HeightRequest = stacked ? -1 : controlSize;
            AllCalculationTypesButton.MinimumWidthRequest = stacked ? -1 : controlSize;
            AllCalculationTypesButton.RemoveDynamicResource(Button.PaddingProperty);
            AllCalculationTypesButton.Padding = stacked
                ? new Thickness(DisplayDensityManager.Metrics.HorizontalPadding, DisplayDensityManager.Metrics.VerticalPadding)
                : Thickness.Zero;
            AllCalculationTypesButton.FontSize = stacked ? 14 : 18;
            CalculationNavigationFrame.Padding = stacked ? Thickness.Zero : new Thickness(6);
            CalculationNavigationFrame.StrokeThickness = stacked ? 0 : 1;
            RefreshCalculationNavigationCaption();
            if (!stacked && CalculationSubTabBar.IsVisible) CalculationSubTabScrollView.UpdateTabLayout(CalculationSubTabGrid);
            if (!stacked && AndroidCalculationSubTabBar.IsVisible) UpdateAndroidCalculationTabSizing();
            var tabScroll = AndroidCalculationSubTabBar.IsVisible
                ? (ScrollView)AndroidCalculationSubTabScrollView : CalculationSubTabScrollView;
            double textScale = ResponsiveLayoutPolicy.TextScale;
            bool revealSelected = modeChanged || Math.Abs(tabScroll.Width - _lastTabViewportWidth) > .5
                || Math.Abs(textScale - _lastTabTextScale) > .001 || controlSize != _lastTabDensitySize;
            _lastTabViewportWidth = tabScroll.Width;
            _lastTabTextScale = textScale;
            _lastTabDensitySize = controlSize;
            if (AllCalculationTypesOverlay.IsVisible) ResizeCalculationChooser();
            // The dispatcher turn after geometry changes observes the arranged tab coordinates.
            Dispatcher.Dispatch(async () =>
            {
                if (generation != _tabNavigationGeneration || !CanUseTabNavigation) return;
                // Content may reflow while the user pans. Reveal only after real
                // viewport/mode/font/density changes; explicit tab selection has
                // its own reveal path. Otherwise retain the user's scroll offset.
                if (revealSelected && CalculationTabStripHost.IsVisible)
                    await ScrollSubTabIntoViewAsync(GetSubTabButton(_selectedSubTab));
                RefreshCalculationOverflow();
            });
        })) _tabNavigationQueued = false;
    }

    private void RefreshCalculationChoices()
    {
        if (_calculationTypeChoices[0] is null) return;
        RefreshCalculationNavigationCaption();
        _calculationTypeHeading.Text = LocalizationService.TranslateKey("Calculation.Navigation.All");
        _calculationTypeSearch.Placeholder = LocalizationService.TranslateKey("Calculation.Navigation.Search");
        _calculationTypeEmpty.Text = LocalizationService.TranslateKey("Calculation.Navigation.Empty");
        string close = LocalizationService.TranslateKey("Calculation.Navigation.Close");
        SemanticProperties.SetDescription(_calculationTypeClose, close);
        ToolTipProperties.SetText(_calculationTypeClose, close);
        var options = CalculationTypeKeys.Select((key, index) => new QuizChoiceOption(index, key,
            LocalizationService.TranslateKey("Calculation.Tabs." + key), "", "", index == (int)_selectedSubTab)).ToArray();
        var matches = QuizChoiceCatalog.Filter(options, _calculationTypeSearch.Text ?? "").Select(option => option.Index).ToHashSet();
        foreach (var option in options)
        {
            var button = _calculationTypeChoices[option.Index];
            button.Text = (option.IsSelected ? "✓ " : "") + option.Title;
            button.IsVisible = matches.Contains(option.Index);
            SelectionButtonStyler.Apply(button, option.IsSelected);
        }
        _calculationTypeEmpty.IsVisible = matches.Count == 0;
    }

    private void RefreshCalculationNavigationCaption()
    {
        if (AllCalculationTypesButton is null) return;
        string all = LocalizationService.TranslateKey("Calculation.Navigation.All").TrimStart('☷', ' ');
        string current = LocalizationService.TranslateKey("Calculation.Tabs." + CalculationTypeKeys[(int)_selectedSubTab]);
        AllCalculationTypesButton.Text = _tabNavigationStacked
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture,
                LocalizationService.TranslateKey("Calculation.Navigation.Current"), current)
            : "▾";
        SemanticProperties.SetDescription(AllCalculationTypesButton, _tabNavigationStacked
            ? AllCalculationTypesButton.Text + ". " + all : all);
        ToolTipProperties.SetText(AllCalculationTypesButton, all);
    }

    private void OnAllCalculationTypesClicked(object? sender, EventArgs e)
    {
        if (!CanUseTabNavigation || _isPowerRootCalculationInteractionLocked || _isSubTabTransitioning) return;
        _calculationTypeSearch.Text = "";
        RefreshCalculationChoices();
        AllCalculationTypesOverlay.IsVisible = true;
        CalculationSubTabContentHost.InputTransparent = CalculationTabNavigation.InputTransparent = true;
        AutomationProperties.SetExcludedWithChildren(CalculationSubTabContentHost, true);
        AutomationProperties.SetExcludedWithChildren(CalculationTabNavigation, true);
        ResizeCalculationChooser();
        AttachCalculationChooserKeyboard();
        _calculationChooserNeedsFocus = true;
        _calculationChooserFocusGeneration++;
        QueueCalculationChooserFocus();
    }

    private void QueueCalculationChooserFocus()
    {
        if (!_calculationChooserNeedsFocus) return;
        int generation = _calculationChooserFocusGeneration;
        Dispatcher.Dispatch(() =>
        {
            if (generation != _calculationChooserFocusGeneration || !_calculationChooserNeedsFocus || !CanUseTabNavigation
                || !AllCalculationTypesOverlay.IsVisible || !_calculationTypeClose.IsLoaded
                || _calculationTypeClose.Handler?.MauiContext is null) return;
            bool focused = _calculationTypeClose.Focus();
#if WINDOWS
            if (_calculationTypeClose.Handler.PlatformView is Microsoft.UI.Xaml.Controls.Control control)
                focused = control.Focus(Microsoft.UI.Xaml.FocusState.Programmatic) || focused;
#endif
            if (focused) _calculationChooserNeedsFocus = false;
        });
    }

    private void ResizeCalculationChooser()
    {
        double width = AllCalculationTypesOverlay.Width > 0 ? AllCalculationTypesOverlay.Width : CalculationPageContentRoot.Width;
        double height = AllCalculationTypesOverlay.Height > 0 ? AllCalculationTypesOverlay.Height : CalculationPageContentRoot.Height;
        _calculationTypeCard.WidthRequest = Math.Max(1, Math.Min(720, width - 24));
        _calculationTypeCard.HeightRequest = Math.Max(1, Math.Min(720, height - 24));
        _calculationTypeHeading.IsVisible = !ResponsiveLayoutPolicy.UseStackedLayout(height, 320);
    }

    private void CloseCalculationTypes(bool restoreFocus = true)
    {
        _calculationChooserNeedsFocus = false;
        _calculationChooserFocusGeneration++;
        AllCalculationTypesOverlay.IsVisible = false;
        CalculationSubTabContentHost.InputTransparent = CalculationTabNavigation.InputTransparent = false;
        AutomationProperties.SetExcludedWithChildren(CalculationSubTabContentHost, false);
        AutomationProperties.SetExcludedWithChildren(CalculationTabNavigation, false);
        DetachCalculationChooserKeyboard();
        if (restoreFocus && CanUseTabNavigation) AllCalculationTypesButton.Focus();
    }

    protected override bool OnBackButtonPressed()
    {
        if (AllCalculationTypesOverlay.IsVisible) { CloseCalculationTypes(); return true; }
        return base.OnBackButtonPressed();
    }
}
