using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Controls.Navigation;

namespace MathSolver.Views;

public partial class CalculationPage
{
    private static readonly string[] CalculationTypeKeys =
        ["Basic", "NumberTheory", "Average", "PowerRoot", "Fraction", "FindX", "Quadratic", "Geometry", "Matrix"];
    private readonly Button[] _calculationTypeChoices = new Button[9];
    private readonly SearchBar _calculationTypeSearch = new();
    private readonly Label _calculationTypeHeading = new(), _calculationTypeEmpty = new();
    private readonly Button _calculationTypeClose = new() { Text = "\u00d7", FontSize = 24, Padding = 0 };
    private Border _calculationTypeCard = null!;
    private ScrollView _calculationTypeList = null!;
    private bool _tabNavigationQueued, _tabNavigationStacked;
    private TopicChooser _topicChooser = null!;
    private int _tabNavigationGeneration;
    private double _lastTabViewportWidth = -1, _lastTabTextScale = -1, _lastTabDensitySize = -1;

    private bool CanUseTabNavigation => _isPageActive && IsLoaded && Handler?.MauiContext is not null
        && Window is { Handler.MauiContext: not null } window && !WindowUiLifetime.IsClosing(window);

    private void InitializeCalculationNavigation()
    {
#if ANDROID
        CalculationTabNavigation.MaximumWidthRequest = double.PositiveInfinity;
#endif
        AndroidCalculationSubTabBar.TabKeys = CalculationTypeKeys.Select(key => "Calculation.Tabs." + key).ToArray();
        AndroidCalculationSubTabBar.SelectionRequested += async (_, index) =>
        {
            if (!CanUseTabNavigation) return;
            try { await SwitchSubTabAsync((CalculationSubTab)index); }
            finally { AndroidCalculationSubTabBar.SetSelection((int)_selectedSubTab); }
        };
        // Explicit key bindings own these labels; identical text may have
        // different translations elsewhere (e.g. Average vs Arithmetic mean).
        LocalizationService.ExcludeSubtreeFromLegacyTracking(CalculationTabStripHost);
        LocalizationService.ExcludeSubtreeFromLegacyTracking(AllCalculationTypesOverlay);
        LocalizationService.ExcludeSubtreeFromLegacyTracking(AllCalculationTypesButton);
        DisplayDensityManager.BindControl(AllCalculationTypesButton);
        InitializeCalculationOverflow();
        _topicChooser = new TopicChooser(AllCalculationTypesOverlay, _calculationTypeChoices,
            _calculationTypeSearch, _calculationTypeHeading, _calculationTypeEmpty, _calculationTypeClose,
            CalculationTypeKeys, "Calculation.Tabs.", "CalculationType", () => CanUseTabNavigation,
            () => CloseCalculationTypes(), async index =>
            {
                if (!CanUseTabNavigation || !AllCalculationTypesOverlay.IsVisible || _isPowerRootCalculationInteractionLocked || _isSubTabTransitioning) return;
                CloseCalculationTypes(restoreFocus: false);
                await SwitchSubTabAsync((CalculationSubTab)index);
                if (CanUseTabNavigation)
                    (CalculationTabStripHost.IsVisible ? GetSubTabButton(_selectedSubTab) : AllCalculationTypesButton).Focus();
            }, RefreshCalculationChoices);
        _calculationTypeCard = _topicChooser.Card;
        _calculationTypeList = _topicChooser.List;
        CalculationTabNavigation.SizeChanged += QueueCalculationNavigation;
        CalculationSubTabScrollView.SizeChanged += QueueCalculationNavigation;
        AndroidCalculationSubTabBar.SizeChanged += QueueCalculationNavigation;
        CalculationSubTabGrid.SizeChanged += QueueCalculationNavigation;
        CalculationTabStripHost.SizeChanged += (_, _) => UpdateCalculationViewportClip();
        Loaded += QueueCalculationNavigation;
        Unloaded += (_, _) => InvalidateCalculationNavigation();
        HandlerChanging += (_, _) => InvalidateCalculationNavigation();
        AllCalculationTypesOverlay.SizeChanged += QueueCalculationNavigation;
        AllCalculationTypesOverlay.Loaded += (_, _) =>
        { AttachCalculationChooserKeyboard(); QueueCalculationChooserFocus(); };
        AllCalculationTypesOverlay.Unloaded += (_, _) => _topicChooser.Invalidate();
        RefreshCalculationChoices();
    }

    private void InvalidateCalculationNavigation()
    {
        _tabNavigationGeneration++; _tabNavigationQueued = false;
        _lastTabViewportWidth = -1;
        _topicChooser.Invalidate();
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
            // Android keeps the swipeable tab strip beside the fixed chooser,
            // including phone widths. Desktop retains its compact fallback.
#if ANDROID
            bool stacked = false;
#else
            bool stacked = ResponsiveLayoutPolicy.UseStackedLayout(CalculationTabNavigation.Width, 760);
#endif
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
#if ANDROID
            controlSize = Math.Max(48, controlSize);
            AllCalculationTypesButton.WidthRequest = AllCalculationTypesButton.HeightRequest = controlSize;
            AllCalculationTypesButton.MinimumWidthRequest = controlSize;
            CalculationNavigationFrame.Padding = Thickness.Zero;
            CalculationNavigationFrame.StrokeThickness = 0;
            CalculationNavigationFrame.SetDynamicResource(Border.BackgroundColorProperty, "SurfaceColor");
            AllCalculationTypesButton.BackgroundColor = Colors.Transparent;
            AllCalculationTypesButton.BorderWidth = 0;
            AllCalculationTypesButton.CornerRadius = (int)(controlSize / 2);
            AllCalculationTypesButton.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
#else
            CalculationNavigationFrame.Padding = stacked ? Thickness.Zero : new Thickness(8);
            CalculationNavigationFrame.StrokeThickness = stacked ? 0 : 1;
#endif
            RefreshCalculationNavigationCaption();
            if (!stacked && CalculationSubTabBar.IsVisible) CalculationSubTabScrollView.UpdateTabLayout(CalculationSubTabGrid);
            AndroidCalculationSubTabBar.RefreshLayout();
            double viewportWidth = AndroidCalculationSubTabBar.IsVisible
                ? AndroidCalculationSubTabBar.Width : CalculationSubTabScrollView.Width;
            double textScale = ResponsiveLayoutPolicy.TextScale;
            bool revealSelected = modeChanged || Math.Abs(viewportWidth - _lastTabViewportWidth) > .5
                || Math.Abs(textScale - _lastTabTextScale) > .001 || controlSize != _lastTabDensitySize;
            _lastTabViewportWidth = viewportWidth;
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
                UpdateCalculationViewportClip();
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
        _topicChooser.Refresh(CalculationTypeKeys, "Calculation.Tabs.", (int)_selectedSubTab);
    }

    private void RefreshCalculationNavigationCaption()
    {
        if (AllCalculationTypesButton is null) return;
        string all = LocalizationService.TranslateKey("Calculation.Navigation.All").TrimStart('\u2637', ' ');
        string current = MathNavigationCatalog.TranslateLabel("Calculation.Tabs." + CalculationTypeKeys[(int)_selectedSubTab]);
        MathNavigationIcon.SetKey(AllCalculationTypesButton, _tabNavigationStacked
            ? "Calculation.Tabs." + CalculationTypeKeys[(int)_selectedSubTab] : null);
        AllCalculationTypesButton.Text = _tabNavigationStacked
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture,
                LocalizationService.TranslateKey("Calculation.Navigation.Current"), current)
            : "\u25be";
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
        _topicChooser.RequestFocus();
    }

    private void QueueCalculationChooserFocus() => _topicChooser.QueueFocus();

    private void ResizeCalculationChooser()
    {
        double width = AllCalculationTypesOverlay.Width > 0 ? AllCalculationTypesOverlay.Width : CalculationPageContentRoot.Width;
        double height = AllCalculationTypesOverlay.Height > 0 ? AllCalculationTypesOverlay.Height : CalculationPageContentRoot.Height;
        _topicChooser.Resize(width, height);
    }

    private void CloseCalculationTypes(bool restoreFocus = true)
    {
        _topicChooser.Invalidate();
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
        if (MatrixSolverHost.IsVisible && MatrixSolverHost.Content is MatrixView matrix && matrix.TryCloseMatrixDetails()) return true;
#if ANDROID
        if (MatrixSolverHost.IsVisible && MatrixSolverHost.Content is MatrixView focusedMatrix && focusedMatrix.TryRestorePhoneNavigation()) return true;
#endif
        return base.OnBackButtonPressed();
    }
}
