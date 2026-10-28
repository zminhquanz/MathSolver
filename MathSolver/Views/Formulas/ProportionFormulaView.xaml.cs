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


    private const double BaseDays = 5d;
    private const double BaseProducts = 120d;
    private const double BaseWorkers = 4d;
    private const double BaseHoursPerDay = 6d;

    private readonly ProportionComparisonDrawable _comparisonDrawable = new();
    private readonly CompoundProportionDrawable _compoundDrawable = new();
    private bool _eventsSubscribed;
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
        UpdateResponsiveLayout(FormulaContent.Width > 0d ? FormulaContent.Width : Math.Min(width, 1320d));
    }

    private void OnFormulaContentSizeChanged(object? sender, EventArgs e) =>
        UpdateResponsiveLayout(FormulaContent.Width);

    private void UpdateResponsiveLayout(double width)
    {
        if (width <= 0) return;
        ProportionGraphicsView.HeightRequest = Math.Clamp((InteractiveGraphPanel.Width > 0 ? InteractiveGraphPanel.Width - 24 : width / 2) / 1.6, 220, 360);
        CompoundGraphicsView.HeightRequest = Math.Clamp((CompoundGraphPanel.Width > 0 ? CompoundGraphPanel.Width - 24 : width / 2) / 1.6, 220, 360);
        ProportionGraphicsView.Invalidate();
        CompoundGraphicsView.Invalidate();
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
        SingleResultPanel.IsVisible = mode == ProportionInteractiveMode.Single;
        CompoundResultPanel.IsVisible = mode == ProportionInteractiveMode.Compound;
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

        SemanticProperties.SetDescription(ProportionXSlider, GraphXValueLabel.Text);
        SemanticProperties.SetDescription(ProportionGraphicsView,
            string.Join(". ", GraphXValueLabel.Text, GraphDirectValueLabel.Text, GraphInverseValueLabel.Text));
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

        SemanticProperties.SetDescription(CompoundProductSlider, CompoundProductValueLabel.Text);
        SemanticProperties.SetDescription(CompoundWorkersSlider, CompoundWorkersValueLabel.Text);
        SemanticProperties.SetDescription(CompoundHoursSlider, CompoundHoursValueLabel.Text);
        SemanticProperties.SetDescription(CompoundGraphicsView, string.Join(". ",
            CompoundProductValueLabel.Text, CompoundWorkersValueLabel.Text,
            CompoundHoursValueLabel.Text, CompoundDaysValueLabel.Text));
        CompoundGraphicsView.Invalidate();
    }

    private static string FormatLocalizedValue(string key, double value)
    {
        string template = LocalizationService.TranslateKey(key);
        string formattedValue = value.ToString("0.##", CultureInfo.CurrentCulture);

        return string.Format(CultureInfo.CurrentCulture, template, formattedValue);
    }

}
