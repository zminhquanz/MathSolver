using MathSolver.Controls;
using MathSolver.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MathSolver.Views;

public partial class FormulaPage
{
    private bool _formulaNavigationActive;

    private Button GetFormulaNavigationButton(FormulaSubTab tab) => AndroidFormulaSubTabBar.IsVisible
        ? GetAndroidFormulaSubTabButton(tab)
        : tab switch
    {
        FormulaSubTab.UnknownComponent => UnknownComponentTabButton,
        FormulaSubTab.Proportion => ProportionTabButton,
        FormulaSubTab.Motion => MotionTabButton,
        FormulaSubTab.Average => AverageTabButton,
        FormulaSubTab.Measurement => MeasurementTabButton,
        _ => GeometryTabButton
    };

    private Button GetAndroidFormulaSubTabButton(
        FormulaSubTab tab)
    {
        return tab switch
        {
            FormulaSubTab.UnknownComponent => AndroidUnknownComponentTabButton,
            FormulaSubTab.Proportion => AndroidProportionTabButton,
            FormulaSubTab.Motion => AndroidMotionTabButton,
            FormulaSubTab.Average => AndroidAverageTabButton,
            FormulaSubTab.Measurement => AndroidMeasurementTabButton,
            FormulaSubTab.Geometry => AndroidFormulaGeometryTabButton,
            _ => AndroidUnknownComponentTabButton
        };
    }

    private static readonly string[] FormulaTypeKeys =
        ["UnknownComponent", "Proportion", "Motion", "Average", "Measurement", "Geometry"];
    private readonly Button[] _formulaTypeChoices = new Button[6];
    private readonly SearchBar _formulaTypeSearch = new();
    private readonly Label _formulaTypeHeading = new(), _formulaTypeEmpty = new();
    private readonly Button _formulaTypeClose = new() { Text = "×", FontSize = 24, Padding = 0 };
    private Border _formulaTypeCard = null!;
    private ScrollView _formulaTypeList = null!;
    private bool _tabNavigationQueued, _tabNavigationStacked, _formulaChooserNeedsFocus;
    private int _tabNavigationGeneration, _formulaChooserFocusGeneration;
    private double _lastTabViewportWidth = -1, _lastTabTextScale = -1, _lastTabDensitySize = -1;

    private bool CanUseTabNavigation => _formulaNavigationActive && IsLoaded && Handler?.MauiContext is not null
        && Window is { Handler.MauiContext: not null } window && !WindowUiLifetime.IsClosing(window);

    private void InitializeFormulaNavigation()
    {
        LocalizationService.ExcludeSubtreeFromLegacyTracking(AllFormulaTypesOverlay);
        LocalizationService.ExcludeSubtreeFromLegacyTracking(AllFormulaTypesButton);
        DisplayDensityManager.BindControl(AllFormulaTypesButton);
        InitializeFormulaOverflow();
        var choices = new VerticalStackLayout { Spacing = 8 };
        for (int i = 0; i < _formulaTypeChoices.Length; i++)
        {
            int index = i;
            var button = new Button { Style = (Style)Application.Current!.Resources["SolverSecondaryActionStyle"],
                FontSize = 15, LineBreakMode = LineBreakMode.WordWrap, Padding = new Thickness(12, 10),
                AutomationId = "FormulaType" + FormulaTypeKeys[i] };
            DisplayDensityManager.BindControl(button);
            button.Clicked += async (_, _) =>
            {
                if (!CanUseTabNavigation || !AllFormulaTypesOverlay.IsVisible || _isSubTabTransitioning || _isMainTabTransitioning) return;
                CloseFormulaTypes(restoreFocus: false);
                await SwitchFormulaSubTabAsync((FormulaSubTab)index);
                if (CanUseTabNavigation)
                    (FormulaTabStripHost.IsVisible ? GetFormulaNavigationButton(_selectedSubTab) : AllFormulaTypesButton).Focus();
            };
            _formulaTypeChoices[i] = button;
            choices.Add(button);
        }
        _formulaTypeEmpty.FontSize = 14;
        _formulaTypeEmpty.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
        choices.Add(_formulaTypeEmpty);
        _formulaTypeList = new ScrollView { Content = choices, VerticalScrollBarVisibility = ScrollBarVisibility.Default };
        _formulaTypeHeading.FontSize = 20;
        _formulaTypeHeading.FontAttributes = FontAttributes.Bold;
        _formulaTypeHeading.LineBreakMode = LineBreakMode.WordWrap;
        _formulaTypeHeading.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetHeadingLevel(_formulaTypeHeading, SemanticHeadingLevel.Level2);
        _formulaTypeSearch.SetDynamicResource(SearchBar.TextColorProperty, "TextPrimaryColor");
        _formulaTypeSearch.SetDynamicResource(SearchBar.PlaceholderColorProperty, "TextSecondaryColor");
        _formulaTypeSearch.TextChanged += (_, _) => RefreshFormulaChoices();
        DisplayDensityManager.BindControl(_formulaTypeClose, square: true);
        _formulaTypeClose.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
        _formulaTypeClose.SetDynamicResource(Button.BackgroundColorProperty, "SurfaceAltColor");
        _formulaTypeClose.Clicked += (_, _) => CloseFormulaTypes();
        _formulaTypeClose.Loaded += (_, _) => QueueFormulaChooserFocus();
        var search = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        search.Add(_formulaTypeSearch); search.Add(_formulaTypeClose, 1, 0);
        var body = new Grid { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) } };
        body.Add(_formulaTypeHeading); body.Add(search, 0, 1); body.Add(_formulaTypeList, 0, 2);
        _formulaTypeCard = new Border { Content = body, Padding = 16, StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 }, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _formulaTypeCard.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
        _formulaTypeCard.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
        var shade = new BoxView { Color = Colors.Black.WithAlpha(0.35f) };
        var dismiss = new TapGestureRecognizer(); dismiss.Tapped += (_, _) => CloseFormulaTypes();
        shade.GestureRecognizers.Add(dismiss);
        AutomationProperties.SetIsInAccessibleTree(shade, false);
        var overlay = new Grid(); overlay.Add(shade); overlay.Add(_formulaTypeCard);
        AllFormulaTypesOverlay.Content = overlay;
        FormulaTabNavigation.SizeChanged += QueueFormulaNavigation;
        FormulaSubTabScrollView.SizeChanged += QueueFormulaNavigation;
        AndroidFormulaSubTabScrollView.SizeChanged += QueueFormulaNavigation;
        FormulaSubTabGrid.SizeChanged += QueueFormulaNavigation;
        FormulaTabStripHost.SizeChanged += (_, _) => UpdateFormulaViewportClip();
        Loaded += QueueFormulaNavigation;
        Loaded += (_, _) =>
        {
            LocalizationService.CultureChanged -= OnFormulaNavigationCultureChanged;
            LocalizationService.CultureChanged += OnFormulaNavigationCultureChanged;
            RefreshFormulaChoices();
        };
        Unloaded += (_, _) => LocalizationService.CultureChanged -= OnFormulaNavigationCultureChanged;
        Unloaded += (_, _) => InvalidateFormulaNavigation();
        HandlerChanging += (_, _) => InvalidateFormulaNavigation();
        AllFormulaTypesOverlay.SizeChanged += QueueFormulaNavigation;
        AllFormulaTypesOverlay.Loaded += (_, _) =>
        { AttachFormulaChooserKeyboard(); QueueFormulaChooserFocus(); };
        AllFormulaTypesOverlay.Unloaded += (_, _) => DetachFormulaChooserKeyboard();
        RefreshFormulaChoices();
    }

    private void OnFormulaNavigationCultureChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(() =>
    {
        if (!CanUseTabNavigation) return;
        RefreshFormulaChoices();
        // A new locale can change the common tab width even when the viewport
        // and OS text scale stay the same. Reveal the current section once.
        _lastTabViewportWidth = -1;
        QueueFormulaNavigation(this, EventArgs.Empty);
    });

    private void InvalidateFormulaNavigation()
    {
        _tabNavigationGeneration++; _tabNavigationQueued = false;
        _lastTabViewportWidth = -1;
        DetachFormulaChooserKeyboard();
    }

    private void QueueFormulaNavigation(object? sender, EventArgs e)
    {
        if (_tabNavigationQueued) return;
        _tabNavigationQueued = true;
        int generation = _tabNavigationGeneration;
        if (!Dispatcher.Dispatch(() =>
        {
            if (generation != _tabNavigationGeneration) return;
            _tabNavigationQueued = false;
            if (!CanUseTabNavigation) return;
            bool stacked = ResponsiveLayoutPolicy.UseStackedLayout(FormulaTabNavigation.Width, 760);
            bool modeChanged = stacked != _tabNavigationStacked;
            if (stacked != _tabNavigationStacked)
            {
                _tabNavigationStacked = stacked;
                FormulaTabStripHost.IsVisible = !stacked;
                Grid.SetRow(AllFormulaTypesButton, 0);
                Grid.SetColumn(AllFormulaTypesButton, stacked ? 0 : 1);
                Grid.SetColumnSpan(AllFormulaTypesButton, stacked ? 2 : 1);
                AllFormulaTypesButton.HorizontalOptions = stacked ? LayoutOptions.Fill : LayoutOptions.End;
            }
            double controlSize = DisplayDensityManager.Metrics.ControlHeight;
            AllFormulaTypesButton.WidthRequest = AllFormulaTypesButton.HeightRequest = stacked ? -1 : controlSize;
            AllFormulaTypesButton.MinimumWidthRequest = stacked ? -1 : controlSize;
            AllFormulaTypesButton.RemoveDynamicResource(Button.PaddingProperty);
            AllFormulaTypesButton.Padding = stacked
                ? new Thickness(DisplayDensityManager.Metrics.HorizontalPadding, DisplayDensityManager.Metrics.VerticalPadding)
                : Thickness.Zero;
            AllFormulaTypesButton.FontSize = stacked ? 14 : 18;
            FormulaNavigationFrame.Padding = stacked ? Thickness.Zero : new Thickness(8);
            FormulaNavigationFrame.StrokeThickness = stacked ? 0 : 1;
            RefreshFormulaNavigationCaption();
            if (!stacked && FormulaSubTabBar.IsVisible) FormulaSubTabScrollView.UpdateTabLayout(FormulaSubTabGrid);
            if (!stacked && AndroidFormulaSubTabBar.IsVisible) UpdateAndroidFormulaTabSizing();
            var tabScroll = AndroidFormulaSubTabBar.IsVisible
                ? (ScrollView)AndroidFormulaSubTabScrollView : FormulaSubTabScrollView;
            double textScale = ResponsiveLayoutPolicy.TextScale;
            bool revealSelected = modeChanged || Math.Abs(tabScroll.Width - _lastTabViewportWidth) > .5
                || Math.Abs(textScale - _lastTabTextScale) > .001 || controlSize != _lastTabDensitySize;
            _lastTabViewportWidth = tabScroll.Width;
            _lastTabTextScale = textScale;
            _lastTabDensitySize = controlSize;
            if (AllFormulaTypesOverlay.IsVisible) ResizeFormulaChooser();
            // The dispatcher turn after geometry changes observes the arranged tab coordinates.
            Dispatcher.Dispatch(async () =>
            {
                if (generation != _tabNavigationGeneration || !CanUseTabNavigation) return;
                // Content may reflow while the user pans. Reveal only after real
                // viewport/mode/font/density changes; explicit tab selection has
                // its own reveal path. Otherwise retain the user's scroll offset.
                if (revealSelected && FormulaTabStripHost.IsVisible)
                    await ScrollFormulaSubTabIntoViewAsync(_selectedSubTab);
                UpdateFormulaViewportClip();
                RefreshFormulaOverflow();
            });
        })) _tabNavigationQueued = false;
    }

    private void RefreshFormulaChoices()
    {
        if (_formulaTypeChoices[0] is null) return;
        RefreshFormulaNavigationCaption();
        _formulaTypeHeading.Text = LocalizationService.TranslateKey("Formula.Navigation.All");
        _formulaTypeSearch.Placeholder = LocalizationService.TranslateKey("Formula.Navigation.Search");
        _formulaTypeEmpty.Text = LocalizationService.TranslateKey("Formula.Navigation.Empty");
        string close = LocalizationService.TranslateKey("Formula.Navigation.Close");
        SemanticProperties.SetDescription(_formulaTypeClose, close);
        ToolTipProperties.SetText(_formulaTypeClose, close);
        var options = FormulaTypeKeys.Select((key, index) => new QuizChoiceOption(index, key,
            LocalizationService.TranslateKey("Formula.Tabs." + key), "", "", index == (int)_selectedSubTab)).ToArray();
        var matches = QuizChoiceCatalog.Filter(options, _formulaTypeSearch.Text ?? "").Select(option => option.Index).ToHashSet();
        foreach (var option in options)
        {
            var button = _formulaTypeChoices[option.Index];
            button.Text = (option.IsSelected ? "✓ " : "") + option.Title;
            button.IsVisible = matches.Contains(option.Index);
            SelectionButtonStyler.Apply(button, option.IsSelected);
        }
        _formulaTypeEmpty.IsVisible = matches.Count == 0;
    }

    private void RefreshFormulaNavigationCaption()
    {
        if (AllFormulaTypesButton is null) return;
        string all = LocalizationService.TranslateKey("Formula.Navigation.All").TrimStart('☷', ' ');
        string current = LocalizationService.TranslateKey("Formula.Tabs." + FormulaTypeKeys[(int)_selectedSubTab]);
        AllFormulaTypesButton.Text = _tabNavigationStacked
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture,
                LocalizationService.TranslateKey("Formula.Navigation.Current"), current)
            : "▾";
        SemanticProperties.SetDescription(AllFormulaTypesButton, _tabNavigationStacked
            ? AllFormulaTypesButton.Text + ". " + all : all);
        ToolTipProperties.SetText(AllFormulaTypesButton, all);
    }

    private void OnAllFormulaTypesClicked(object? sender, EventArgs e)
    {
        if (!CanUseTabNavigation || _isSubTabTransitioning || _isMainTabTransitioning) return;
        _formulaTypeSearch.Text = "";
        RefreshFormulaChoices();
        AllFormulaTypesOverlay.IsVisible = true;
        FormulaSubTabContentHost.InputTransparent = FormulaTabNavigation.InputTransparent = true;
        AutomationProperties.SetExcludedWithChildren(FormulaSubTabContentHost, true);
        AutomationProperties.SetExcludedWithChildren(FormulaTabNavigation, true);
        ResizeFormulaChooser();
        AttachFormulaChooserKeyboard();
        _formulaChooserNeedsFocus = true;
        _formulaChooserFocusGeneration++;
        QueueFormulaChooserFocus();
    }

    private void QueueFormulaChooserFocus()
    {
        if (!_formulaChooserNeedsFocus) return;
        int generation = _formulaChooserFocusGeneration;
        Dispatcher.Dispatch(() =>
        {
            if (generation != _formulaChooserFocusGeneration || !_formulaChooserNeedsFocus || !CanUseTabNavigation
                || !AllFormulaTypesOverlay.IsVisible || !_formulaTypeClose.IsLoaded
                || _formulaTypeClose.Handler?.MauiContext is null) return;
            bool focused = _formulaTypeClose.Focus();
#if WINDOWS
            if (_formulaTypeClose.Handler.PlatformView is Microsoft.UI.Xaml.Controls.Control control)
                focused = control.Focus(Microsoft.UI.Xaml.FocusState.Programmatic) || focused;
#endif
            if (focused) _formulaChooserNeedsFocus = false;
        });
    }

    private void ResizeFormulaChooser()
    {
        double width = AllFormulaTypesOverlay.Width > 0 ? AllFormulaTypesOverlay.Width : FormulaPageContentRoot.Width;
        double height = AllFormulaTypesOverlay.Height > 0 ? AllFormulaTypesOverlay.Height : FormulaPageContentRoot.Height;
        _formulaTypeCard.WidthRequest = Math.Max(1, Math.Min(720, width - 24));
        _formulaTypeCard.HeightRequest = Math.Max(1, Math.Min(720, height - 24));
        _formulaTypeHeading.IsVisible = !ResponsiveLayoutPolicy.UseStackedLayout(height, 320);
    }

    private void CloseFormulaTypes(bool restoreFocus = true)
    {
        _formulaChooserNeedsFocus = false;
        _formulaChooserFocusGeneration++;
        AllFormulaTypesOverlay.IsVisible = false;
        FormulaSubTabContentHost.InputTransparent = FormulaTabNavigation.InputTransparent = false;
        AutomationProperties.SetExcludedWithChildren(FormulaSubTabContentHost, false);
        AutomationProperties.SetExcludedWithChildren(FormulaTabNavigation, false);
        DetachFormulaChooserKeyboard();
        if (restoreFocus && CanUseTabNavigation) AllFormulaTypesButton.Focus();
    }

    protected override bool OnBackButtonPressed()
    {
        if (AllFormulaTypesOverlay.IsVisible) { CloseFormulaTypes(); return true; }
        return base.OnBackButtonPressed();
    }
}
