using MathSolver.Graphics;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Views;

public partial class MotionFormulaView : ContentView
{

    private readonly MotionAverageSpeedDrawable _motionDrawable = new();
    private bool _eventsSubscribed;
    private bool _isSliderSyncInProgress;

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
        UpdateResponsiveLayout(FormulaContent.Width > 0d ? FormulaContent.Width : Math.Min(width, 1320d));
    }

    private void OnFormulaContentSizeChanged(object? sender, EventArgs e) =>
        UpdateResponsiveLayout(FormulaContent.Width);

    private void UpdateResponsiveLayout(double width)
    {
        if (width <= 0) return;
        MotionGraphicsView.HeightRequest = Math.Clamp((MotionGraphPanel.Width > 0 ? MotionGraphPanel.Width - 24 : width / 2) / 1.6, 220, 360);
        MotionGraphicsView.Invalidate();
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
            "WallpaperPrimaryBorderColor",
            "#FDBA74");

        Color sliderBackground = ThemeResource.GetColor(
            "WallpaperSurfaceAltColor",
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

        SemanticProperties.SetDescription(MotionDistanceSlider,
            LocalizationService.TranslateKey("Formula.Motion.Symbol.Distance"));
        SemanticProperties.SetDescription(MotionTimeSlider,
            LocalizationService.TranslateKey("Formula.Motion.Symbol.Time"));
        SemanticProperties.SetDescription(MotionGraphicsView, MotionAverageFormulaLabel.Text + ". " + MotionSpeedValueLabel.Text);
        MotionGraphicsView.Invalidate();
    }

}
