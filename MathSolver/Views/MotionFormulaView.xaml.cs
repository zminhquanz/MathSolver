using MathSolver.Graphics;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Views;

public partial class MotionFormulaView : ContentView
{
    private const double CompactLayoutThreshold = 760d;
    private const double WideGraphAspectRatio = 1.90d;
    private const double WideGraphMinHeight = 390d;
    private const double WideGraphMaxHeight = 600d;
    private const double CompactGraphAspectRatio = 1.60d;
    private const double CompactGraphMinHeight = 320d;
    private const double CompactGraphMaxHeight = 500d;

    private readonly MotionAverageSpeedDrawable _motionDrawable = new();
    private bool _eventsSubscribed;
    private bool _isSliderSyncInProgress;
    private bool? _isCompactLayout;

    public MotionFormulaView()
    {
        InitializeComponent();

        LocalizationService.ExcludeSubtreeFromLegacyTracking(this);
        MotionGraphicsView.Drawable = _motionDrawable;

        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;

        RefreshSliderTheme();
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
                MotionInteractiveGrid,
                MotionExplanationPanel,
                MotionGraphPanel,
                useCompactLayout,
                3d,
                7d);
        }

        UpdateInteractiveSectionSize(width, useCompactLayout);
        MotionGraphicsView.Invalidate();
    }

    private void UpdateInteractiveSectionSize(
        double availableWidth,
        bool compact)
    {
        double innerWidth = Math.Max(0d, availableWidth - 72d);
        double targetHeight;

        if (compact)
        {
            targetHeight = Math.Clamp(
                innerWidth / CompactGraphAspectRatio,
                CompactGraphMinHeight,
                CompactGraphMaxHeight);

            MotionExplanationPanel.MinimumHeightRequest = -1d;
        }
        else
        {
            double graphWidth = Math.Max(
                0d,
                (innerWidth - MotionInteractiveGrid.ColumnSpacing) * 0.70d);

            targetHeight = Math.Clamp(
                graphWidth / WideGraphAspectRatio,
                WideGraphMinHeight,
                WideGraphMaxHeight);

            MotionExplanationPanel.MinimumHeightRequest = targetHeight;
        }

        MotionGraphicsView.HeightRequest = targetHeight;
        MotionGraphPanel.MinimumHeightRequest = targetHeight;
    }

    private void OnViewLoaded(object? sender, EventArgs e)
    {
        SubscribeDynamicEvents();
        RefreshSliderTheme();
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

        AppThemeManager.ThemeChanged += OnThemeChanged;
        LocalizationService.CultureChanged += OnCultureChanged;
        _eventsSubscribed = true;
    }

    private void UnsubscribeDynamicEvents()
    {
        if (!_eventsSubscribed)
        {
            return;
        }

        AppThemeManager.ThemeChanged -= OnThemeChanged;
        LocalizationService.CultureChanged -= OnCultureChanged;
        _eventsSubscribed = false;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            RefreshSliderTheme();
            MotionGraphicsView.Invalidate();
        });
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(UpdateInteractiveValues);
    }

    private void RefreshSliderTheme()
    {
        Color primary = ThemeResource.GetColor(
            "PrimaryColor",
            "#F97316");

        Color primaryBorder = ThemeResource.GetColor(
            "PrimaryBorderColor",
            "#FDBA74");

        Color sliderBackground = ThemeResource.GetColor(
            "SurfaceAltColor",
            AppThemeManager.IsDarkThemeEffective
                ? "#172033"
                : "#F4F8FF");

        ApplySliderTheme(MotionDistanceSlider, primary, primaryBorder, sliderBackground);
        ApplySliderTheme(MotionTimeSlider, primary, primaryBorder, sliderBackground);
    }

    private static void ApplySliderTheme(
        Slider slider,
        Color primary,
        Color primaryBorder,
        Color sliderBackground)
    {
        slider.MinimumTrackColor = primary;
        slider.MaximumTrackColor = primaryBorder;
        slider.ThumbColor = primary;
        slider.BackgroundColor = sliderBackground;
    }

    private void OnMotionSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isSliderSyncInProgress)
        {
            return;
        }

        UpdateInteractiveValues();
    }

    private void UpdateInteractiveValues()
    {
        double snappedDistance = Math.Clamp(
            Math.Round(MotionDistanceSlider.Value / 5d) * 5d,
            MotionDistanceSlider.Minimum,
            MotionDistanceSlider.Maximum);

        double snappedTime = Math.Clamp(
            Math.Round(MotionTimeSlider.Value),
            MotionTimeSlider.Minimum,
            MotionTimeSlider.Maximum);

        if (Math.Abs(MotionDistanceSlider.Value - snappedDistance) > 0.001d ||
            Math.Abs(MotionTimeSlider.Value - snappedTime) > 0.001d)
        {
            _isSliderSyncInProgress = true;

            MotionDistanceSlider.Value = snappedDistance;
            MotionTimeSlider.Value = snappedTime;

            _isSliderSyncInProgress = false;
        }

        double speed = snappedDistance / snappedTime;

        MotionAverageFormulaLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            "v = {0} ÷ {1} = {2} m/s",
            snappedDistance.ToString("0", CultureInfo.CurrentCulture),
            snappedTime.ToString("0", CultureInfo.CurrentCulture),
            speed.ToString("0.##", CultureInfo.CurrentCulture));

        MotionDistanceValueLabel.Text = snappedDistance.ToString(
            "0",
            CultureInfo.CurrentCulture);

        MotionTimeValueLabel.Text = snappedTime.ToString(
            "0",
            CultureInfo.CurrentCulture);

        MotionSpeedValueLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            LocalizationService.TranslateKey(
                "Formula.Motion.Graph.SpeedSentence"),
            speed.ToString("0.##", CultureInfo.CurrentCulture));

        _motionDrawable.DistanceMeters = snappedDistance;
        _motionDrawable.TimeSeconds = Math.Max(1, (int)snappedTime);
        _motionDrawable.SpeedMetersPerSecond = speed;

        MotionGraphicsView.Invalidate();
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
