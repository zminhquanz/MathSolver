using MathSolver.Graphics;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Views;

public partial class ProportionFormulaView : ContentView
{
    private const double CompactLayoutThreshold = 760d;

    private readonly ProportionComparisonDrawable _comparisonDrawable = new();
    private bool _eventsSubscribed;
    private bool? _isCompactLayout;

    public ProportionFormulaView()
    {
        InitializeComponent();

        LocalizationService.ExcludeSubtreeFromLegacyTracking(this);
        ProportionGraphicsView.Drawable = _comparisonDrawable;

        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;

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

        if (_isCompactLayout == useCompactLayout)
        {
            return;
        }

        _isCompactLayout = useCompactLayout;

        ConfigureResponsivePair(
            RelationshipGrid,
            DirectRelationshipCard,
            InverseRelationshipCard,
            useCompactLayout,
            1d,
            1d);

        ConfigureResponsivePair(
            InteractiveGraphGrid,
            InteractiveExplanationPanel,
            InteractiveGraphPanel,
            useCompactLayout,
            4d,
            6d);

        ConfigureResponsivePair(
            ExamplesGrid,
            DirectExampleCard,
            InverseExampleCard,
            useCompactLayout,
            1d,
            1d);

        ProportionGraphicsView.HeightRequest = useCompactLayout ? 285d : 330d;
        ProportionGraphicsView.Invalidate();
    }

    private void OnViewLoaded(object? sender, EventArgs e)
    {
        SubscribeDynamicEvents();
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
        Dispatcher.Dispatch(UpdateInteractiveValues);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(ProportionGraphicsView.Invalidate);
    }

    private void OnProportionXSliderValueChanged(
        object? sender,
        ValueChangedEventArgs e)
    {
        UpdateInteractiveValues();
    }

    private void UpdateInteractiveValues()
    {
        double x = Math.Clamp(
            ProportionXSlider.Value,
            ProportionXSlider.Minimum,
            ProportionXSlider.Maximum);

        double directY = 2d * x;
        double inverseY = 2d / x;

        _comparisonDrawable.SelectedX = x;
        _comparisonDrawable.DirectLegend = LocalizationService.TranslateKey(
            "Formula.Proportion.Graph.DirectLegend");
        _comparisonDrawable.InverseLegend = LocalizationService.TranslateKey(
            "Formula.Proportion.Graph.InverseLegend");

        GraphXValueLabel.Text = FormatLocalizedValue(
            "Formula.Proportion.Graph.XValue",
            x);
        GraphDirectValueLabel.Text = FormatLocalizedValue(
            "Formula.Proportion.Graph.DirectValue",
            directY);
        GraphInverseValueLabel.Text = FormatLocalizedValue(
            "Formula.Proportion.Graph.InverseValue",
            inverseY);

        ProportionGraphicsView.Invalidate();
    }

    private static string FormatLocalizedValue(string key, double value)
    {
        string template = LocalizationService.TranslateKey(key);
        string formattedValue = value.ToString(
            "0.##",
            CultureInfo.CurrentCulture);

        return string.Format(
            CultureInfo.CurrentCulture,
            template,
            formattedValue);
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
