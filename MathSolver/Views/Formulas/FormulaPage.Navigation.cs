using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Controls.Navigation;

namespace MathSolver.Views;

public partial class FormulaPage
{
    private bool _formulaNavigationActive;

    private VisualElement GetFormulaNavigationButton(FormulaSubTab tab) => AndroidFormulaSubTabBar.IsVisible
        ? AndroidFormulaSubTabBar
        : tab switch
    {
        FormulaSubTab.UnknownComponent => UnknownComponentTabButton,
        FormulaSubTab.Proportion => ProportionTabButton,
        FormulaSubTab.Motion => MotionTabButton,
        FormulaSubTab.Average => AverageTabButton,
        FormulaSubTab.Measurement => MeasurementTabButton,
        _ => GeometryTabButton
    };

    private static readonly string[] FormulaTypeKeys =
        ["UnknownComponent", "Proportion", "Motion", "Average", "Measurement", "Geometry"];
    private readonly Button[] _formulaTypeChoices = new Button[6];
    private readonly SearchBar _formulaTypeSearch = new();
    private readonly Label _formulaTypeHeading = new(), _formulaTypeEmpty = new();
    private readonly Button _formulaTypeClose = new() { Text = "\u00d7", FontSize = 24, Padding = 0 };
    private Border _formulaTypeCard = null!;
    private ScrollView _formulaTypeList = null!;
    private bool _tabNavigationQueued, _tabNavigationStacked;
    private TopicChooser _topicChooser = null!;
    private int _tabNavigationGeneration;
    private double _lastTabViewportWidth = -1, _lastTabTextScale = -1, _lastTabDensitySize = -1;

    private bool CanUseTabNavigation => _formulaNavigationActive && IsLoaded && Handler?.MauiContext is not null
        && Window is { Handler.MauiContext: not null } window && !WindowUiLifetime.IsClosing(window);

    private void InitializeFormulaNavigation()
    {
#if ANDROID
        FormulaTabNavigation.MaximumWidthRequest = double.PositiveInfinity;
#endif
        AndroidFormulaSubTabBar.TabKeys = FormulaTypeKeys.Select(key => "Formula.Tabs." + key).ToArray();
        AndroidFormulaSubTabBar.SelectionRequested += async (_, index) =>
        {
            if (!CanUseTabNavigation) return;
            try { await SwitchFormulaSubTabAsync((FormulaSubTab)index); }
            finally { AndroidFormulaSubTabBar.SetSelection((int)_selectedSubTab); }
        };
        // Keep legacy source-text matching from replacing stable-key bindings.
        LocalizationService.ExcludeSubtreeFromLegacyTracking(FormulaTabStripHost);
        LocalizationService.ExcludeSubtreeFromLegacyTracking(AllFormulaTypesOverlay);
        LocalizationService.ExcludeSubtreeFromLegacyTracking(AllFormulaTypesButton);
        DisplayDensityManager.BindControl(AllFormulaTypesButton);
        InitializeFormulaOverflow();
        _topicChooser = new TopicChooser(AllFormulaTypesOverlay, _formulaTypeChoices,
            _formulaTypeSearch, _formulaTypeHeading, _formulaTypeEmpty, _formulaTypeClose,
            FormulaTypeKeys, "Formula.Tabs.", "FormulaType", () => CanUseTabNavigation,
            () => CloseFormulaTypes(), async index =>
            {
                if (!CanUseTabNavigation || !AllFormulaTypesOverlay.IsVisible || _isSubTabTransitioning || _isMainTabTransitioning) return;
                CloseFormulaTypes(restoreFocus: false);
                await SwitchFormulaSubTabAsync((FormulaSubTab)index);
                if (CanUseTabNavigation)
                    (FormulaTabStripHost.IsVisible ? GetFormulaNavigationButton(_selectedSubTab) : AllFormulaTypesButton).Focus();
            }, RefreshFormulaChoices);
        _formulaTypeCard = _topicChooser.Card;
        _formulaTypeList = _topicChooser.List;
        FormulaTabNavigation.SizeChanged += QueueFormulaNavigation;
        FormulaSubTabScrollView.SizeChanged += QueueFormulaNavigation;
        AndroidFormulaSubTabBar.SizeChanged += QueueFormulaNavigation;
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
        AllFormulaTypesOverlay.Unloaded += (_, _) => _topicChooser.Invalidate();
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
        _topicChooser.Invalidate();
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
            // Android keeps the swipeable tab strip beside the fixed chooser,
            // including phone widths. Desktop retains its compact fallback.
#if ANDROID
            bool stacked = false;
#else
            bool stacked = ResponsiveLayoutPolicy.UseStackedLayout(FormulaTabNavigation.Width, 760);
#endif
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
#if ANDROID
            controlSize = Math.Max(48, controlSize);
            AllFormulaTypesButton.WidthRequest = AllFormulaTypesButton.HeightRequest = controlSize;
            AllFormulaTypesButton.MinimumWidthRequest = controlSize;
            FormulaNavigationFrame.Padding = Thickness.Zero;
            FormulaNavigationFrame.StrokeThickness = 0;
            FormulaNavigationFrame.SetDynamicResource(Border.BackgroundColorProperty, "SurfaceColor");
            AllFormulaTypesButton.BackgroundColor = Colors.Transparent;
            AllFormulaTypesButton.BorderWidth = 0;
            AllFormulaTypesButton.CornerRadius = (int)(controlSize / 2);
            AllFormulaTypesButton.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
#else
            FormulaNavigationFrame.Padding = stacked ? Thickness.Zero : new Thickness(8);
            FormulaNavigationFrame.StrokeThickness = stacked ? 0 : 1;
#endif
            RefreshFormulaNavigationCaption();
            if (!stacked && FormulaSubTabBar.IsVisible) FormulaSubTabScrollView.UpdateTabLayout(FormulaSubTabGrid);
            AndroidFormulaSubTabBar.RefreshLayout();
            double viewportWidth = AndroidFormulaSubTabBar.IsVisible
                ? AndroidFormulaSubTabBar.Width : FormulaSubTabScrollView.Width;
            double textScale = ResponsiveLayoutPolicy.TextScale;
            bool revealSelected = modeChanged || Math.Abs(viewportWidth - _lastTabViewportWidth) > .5
                || Math.Abs(textScale - _lastTabTextScale) > .001 || controlSize != _lastTabDensitySize;
            _lastTabViewportWidth = viewportWidth;
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
        _topicChooser.Refresh(FormulaTypeKeys, "Formula.Tabs.", (int)_selectedSubTab);
    }

    private void RefreshFormulaNavigationCaption()
    {
        if (AllFormulaTypesButton is null) return;
        string all = LocalizationService.TranslateKey("Formula.Navigation.All").TrimStart('\u2637', ' ');
        string current = MathNavigationCatalog.TranslateLabel("Formula.Tabs." + FormulaTypeKeys[(int)_selectedSubTab]);
        MathNavigationIcon.SetKey(AllFormulaTypesButton, _tabNavigationStacked
            ? "Formula.Tabs." + FormulaTypeKeys[(int)_selectedSubTab] : null);
        AllFormulaTypesButton.Text = _tabNavigationStacked
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture,
                LocalizationService.TranslateKey("Formula.Navigation.Current"), current)
            : "\u25be";
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
        _topicChooser.RequestFocus();
    }

    private void QueueFormulaChooserFocus() => _topicChooser.QueueFocus();

    private void ResizeFormulaChooser()
    {
        double width = AllFormulaTypesOverlay.Width > 0 ? AllFormulaTypesOverlay.Width : FormulaPageContentRoot.Width;
        double height = AllFormulaTypesOverlay.Height > 0 ? AllFormulaTypesOverlay.Height : FormulaPageContentRoot.Height;
        _topicChooser.Resize(width, height);
    }

    private void CloseFormulaTypes(bool restoreFocus = true)
    {
        _topicChooser.Invalidate();
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
