using MathSolver.Graphics;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Views;

public partial class ProportionFormulaView : ContentView
{
    private enum ProportionInteractiveMode
    {
        Single,
        Compound,
    }

    private const double CompactLayoutThreshold = 760d;
    private const double WideGraphAspectRatio = 1.90d;
    private const double CompactGraphAspectRatio = 1.60d;
    private const double WideGraphMinHeight = 390d;
    private const double WideGraphMaxHeight = 600d;
    private const double CompactGraphMinHeight = 320d;
    private const double CompactGraphMaxHeight = 500d;

    private const double BaseDays = 5d;
    private const double BaseProducts = 120d;
    private const double BaseWorkers = 4d;
    private const double BaseHoursPerDay = 6d;

    private readonly ProportionComparisonDrawable _comparisonDrawable = new();
    private readonly CompoundProportionDrawable _compoundDrawable = new();
    private bool _eventsSubscribed;
    private bool? _isCompactLayout;
    private ProportionInteractiveMode _interactiveMode = ProportionInteractiveMode.Single;

    public ProportionFormulaView()
    {
        InitializeComponent();

        LocalizationService.ExcludeSubtreeFromLegacyTracking(this);
        ProportionGraphicsView.Drawable = _comparisonDrawable;
        CompoundGraphicsView.Drawable = _compoundDrawable;

        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;

        SetInteractiveMode(ProportionInteractiveMode.Single);
        RefreshControlTheme();
        UpdateInteractiveValues();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0d)
        {
            return;
        }

        bool useCompactLayout = width < CompactLayoutThreshold;

        if (_isCompactLayout != useCompactLayout)
        {
            _isCompactLayout = useCompactLayout;

            ConfigureResponsivePair(
                RelationshipGrid,
                DirectRelationshipCard,
                InverseRelationshipCard,
                useCompactLayout,
                1d,
                1d);

            ConfigureResponsivePair(
                SingleInteractiveGrid,
                InteractiveExplanationPanel,
                InteractiveGraphPanel,
                useCompactLayout,
                3d,
                7d);

            ConfigureResponsivePair(
                CompoundInteractiveGrid,
                CompoundExplanationPanel,
                CompoundGraphPanel,
                useCompactLayout,
                3d,
                7d);

            ConfigureResponsivePair(
                ExamplesGrid,
                DirectExampleCard,
                InverseExampleCard,
                useCompactLayout,
                1d,
                1d);
        }

        UpdateInteractiveSectionSize(width, useCompactLayout);

        ProportionGraphicsView.Invalidate();
        CompoundGraphicsView.Invalidate();
    }

    private void UpdateInteractiveSectionSize(double availableWidth, bool compact)
    {
        double innerWidth = Math.Max(0d, availableWidth - 72d);
        double targetHeight;

        if (compact)
        {
            targetHeight = Math.Clamp(
                innerWidth / CompactGraphAspectRatio,
                CompactGraphMinHeight,
                CompactGraphMaxHeight);

            InteractiveExplanationPanel.MinimumHeightRequest = -1d;
            CompoundExplanationPanel.MinimumHeightRequest = -1d;
        }
        else
        {
            double graphWidth = Math.Max(
                0d,
                (innerWidth - SingleInteractiveGrid.ColumnSpacing) * 0.70d);

            targetHeight = Math.Clamp(
                graphWidth / WideGraphAspectRatio,
                WideGraphMinHeight,
                WideGraphMaxHeight);

            InteractiveExplanationPanel.MinimumHeightRequest = targetHeight;
            CompoundExplanationPanel.MinimumHeightRequest = targetHeight;
        }

        ProportionGraphicsView.HeightRequest = targetHeight;
        InteractiveGraphPanel.MinimumHeightRequest = targetHeight;

        CompoundGraphicsView.HeightRequest = targetHeight;
        CompoundGraphPanel.MinimumHeightRequest = targetHeight;
    }

    private void OnViewLoaded(object? sender, EventArgs e)
    {
        SubscribeDynamicEvents();
        RefreshControlTheme();
        UpdateInteractiveValues();
    }

    private void OnViewUnloaded(object? sender, EventArgs e)
    {
        UnsubscribeDynamicEvents();
    }

    private void SubscribeDynamicEvents()
    {
        if (_eventsSubscribed)
        {
            return;
        }

        LocalizationService.CultureChanged += OnCultureChanged;
        AppThemeManager.ThemeChanged += OnThemeChanged;
        _eventsSubscribed = true;
    }

    private void UnsubscribeDynamicEvents()
    {
        if (!_eventsSubscribed)
        {
            return;
        }

        LocalizationService.CultureChanged -= OnCultureChanged;
        AppThemeManager.ThemeChanged -= OnThemeChanged;
        _eventsSubscribed = false;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            RefreshControlTheme();
            UpdateInteractiveValues();
        });
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            RefreshControlTheme();
            ProportionGraphicsView.Invalidate();
            CompoundGraphicsView.Invalidate();
        });
    }

    private void OnSingleModeClicked(object? sender, EventArgs e)
    {
        SetInteractiveMode(ProportionInteractiveMode.Single);
    }

    private void OnCompoundModeClicked(object? sender, EventArgs e)
    {
        SetInteractiveMode(ProportionInteractiveMode.Compound);
    }

    private void SetInteractiveMode(ProportionInteractiveMode mode)
    {
        _interactiveMode = mode;
        SingleInteractiveGrid.IsVisible = mode == ProportionInteractiveMode.Single;
        CompoundInteractiveGrid.IsVisible = mode == ProportionInteractiveMode.Compound;
        RefreshModeButtons();

        if (mode == ProportionInteractiveMode.Single)
        {
            ProportionGraphicsView.Invalidate();
        }
        else
        {
            CompoundGraphicsView.Invalidate();
        }
    }

    private void RefreshControlTheme()
    {
        Color primary = ThemeResource.GetColor("PrimaryColor", "#6D28D9");
        Color primaryBorder = ThemeResource.GetColor("WallpaperPrimaryBorderColor", "#C4B5FD");
        Color sliderBackground = ThemeResource.GetColor(
            "WallpaperSurfaceAltColor",
            AppThemeManager.IsDarkThemeEffective ? "#172033" : "#F4F8FF");

        ProportionXSlider.MinimumTrackColor = primary;
        ProportionXSlider.MaximumTrackColor = primaryBorder;
        ProportionXSlider.ThumbColor = primary;
        ProportionXSlider.BackgroundColor = sliderBackground;

        ApplySliderTheme(CompoundProductSlider, primary, primaryBorder, sliderBackground);
        ApplySliderTheme(CompoundWorkersSlider, primary, primaryBorder, sliderBackground);
        ApplySliderTheme(CompoundHoursSlider, primary, primaryBorder, sliderBackground);

        RefreshModeButtons();
    }

    private void ApplySliderTheme(Slider slider, Color minimumTrack, Color maximumTrack, Color background)
    {
        slider.MinimumTrackColor = minimumTrack;
        slider.MaximumTrackColor = maximumTrack;
        slider.ThumbColor = minimumTrack;
        slider.BackgroundColor = background;
    }

    private void RefreshModeButtons()
    {
        Color primary = ThemeResource.GetColor("PrimaryColor", "#6D28D9");
        Color surfaceAlt = ThemeResource.GetColor(
            "WallpaperSurfaceAltColor",
            AppThemeManager.IsDarkThemeEffective ? "#172033" : "#F8FAFC");
        Color textPrimary = ThemeResource.GetColor(
            "WallpaperTextPrimaryColor",
            AppThemeManager.IsDarkThemeEffective ? "#F8FAFC" : "#0F172A");
        Color white = Colors.White;

        StyleModeButton(
            SingleModeButton,
            _interactiveMode == ProportionInteractiveMode.Single,
            primary,
            surfaceAlt,
            white,
            textPrimary);

        StyleModeButton(
            CompoundModeButton,
            _interactiveMode == ProportionInteractiveMode.Compound,
            primary,
            surfaceAlt,
            white,
            textPrimary);
    }

    private static void StyleModeButton(
        Button button,
        bool isActive,
        Color activeBackground,
        Color inactiveBackground,
        Color activeText,
        Color inactiveText)
    {
        button.BackgroundColor = isActive ? activeBackground : inactiveBackground;
        button.TextColor = isActive ? activeText : inactiveText;
        button.BorderColor = activeBackground;
        button.BorderWidth = 1d;
    }

    private void OnProportionXSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        UpdateSingleModeValues();
    }

    private void OnCompoundSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        UpdateCompoundModeValues();
    }

    private void UpdateInteractiveValues()
    {
        UpdateSingleModeValues();
        UpdateCompoundModeValues();
    }

    private void UpdateSingleModeValues()
    {
        double x = Math.Clamp(
            ProportionXSlider.Value,
            ProportionXSlider.Minimum,
            ProportionXSlider.Maximum);

        double directY = 2d * x;
        double inverseY = 2d / x;

        _comparisonDrawable.SelectedX = x;
        _comparisonDrawable.DirectLegend = LocalizationService.TranslateKey("Formula.Proportion.Graph.DirectLegend");
        _comparisonDrawable.InverseLegend = LocalizationService.TranslateKey("Formula.Proportion.Graph.InverseLegend");

        GraphXValueLabel.Text = FormatLocalizedValue("Formula.Proportion.Graph.XValue", x);
        GraphDirectValueLabel.Text = FormatLocalizedValue("Formula.Proportion.Graph.DirectValue", directY);
        GraphInverseValueLabel.Text = FormatLocalizedValue("Formula.Proportion.Graph.InverseValue", inverseY);

        ProportionGraphicsView.Invalidate();
    }

    private void UpdateCompoundModeValues()
    {
        int productCount = (int)Math.Round(CompoundProductSlider.Value);
        int workerCount = Math.Max(1, (int)Math.Round(CompoundWorkersSlider.Value));
        int hoursPerDay = Math.Max(1, (int)Math.Round(CompoundHoursSlider.Value));

        double daysNeeded =
            BaseDays *
            (productCount / BaseProducts) *
            (BaseWorkers / workerCount) *
            (BaseHoursPerDay / hoursPerDay);

        CompoundProductValueLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.InteractiveProduct"),
            productCount.ToString("0", CultureInfo.CurrentCulture));

        CompoundWorkersValueLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.InteractiveWorkers"),
            workerCount.ToString("0", CultureInfo.CurrentCulture));

        CompoundHoursValueLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.InteractiveHours"),
            hoursPerDay.ToString("0", CultureInfo.CurrentCulture));

        CompoundDaysValueLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.InteractiveDays"),
            daysNeeded.ToString("0.##", CultureInfo.CurrentCulture));

        _compoundDrawable.ProductCount = productCount;
        _compoundDrawable.WorkerCount = workerCount;
        _compoundDrawable.HoursPerDay = hoursPerDay;
        _compoundDrawable.DaysNeeded = daysNeeded;

        CompoundGraphicsView.Invalidate();
    }

    private static string FormatLocalizedValue(string key, double value)
    {
        string template = LocalizationService.TranslateKey(key);
        string formattedValue = value.ToString("0.##", CultureInfo.CurrentCulture);

        return string.Format(CultureInfo.CurrentCulture, template, formattedValue);
    }

    private static void ConfigureResponsivePair(
        Grid grid,
        View first,
        View second,
        bool compact,
        double firstStar,
        double secondStar)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        if (compact)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Star,
            });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto,
            });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto,
            });

            grid.ColumnSpacing = 0d;
            grid.RowSpacing = 12d;
            Grid.SetColumn(first, 0);
            Grid.SetRow(first, 0);
            Grid.SetColumn(second, 0);
            Grid.SetRow(second, 1);
            return;
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(firstStar, GridUnitType.Star),
        });
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(secondStar, GridUnitType.Star),
        });
        grid.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto,
        });

        grid.ColumnSpacing = 12d;
        grid.RowSpacing = 0d;
        Grid.SetColumn(first, 0);
        Grid.SetRow(first, 0);
        Grid.SetColumn(second, 1);
        Grid.SetRow(second, 0);
    }
}
