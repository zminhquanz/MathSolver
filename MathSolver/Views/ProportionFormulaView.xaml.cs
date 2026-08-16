using MathSolver.Graphics;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Views;

public partial class ProportionFormulaView : ContentView
{
    private const double CompactLayoutThreshold = 760d;
    private const double WideGraphAspectRatio = 1.90d;
    private const double CompactGraphAspectRatio = 1.60d;
    private const double WideGraphMinHeight = 390d;
    private const double WideGraphMaxHeight = 600d;
    private const double CompactGraphMinHeight = 320d;
    private const double CompactGraphMaxHeight = 500d;

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

        // Chỉ rebuild Grid khi thật sự đổi giữa layout ngang và layout compact.
        // Chiều cao tương tác vẫn phải được tính lại ở MỌI lần resize/maximize để
        // biểu đồ không bị kéo dài theo chiều ngang khi cửa sổ rộng hơn.
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
                InteractiveGraphGrid,
                InteractiveExplanationPanel,
                InteractiveGraphPanel,
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

        UpdateInteractiveSectionSize(
            width,
            useCompactLayout);

        ProportionGraphicsView.Invalidate();
    }

    private void UpdateInteractiveSectionSize(
        double availableWidth,
        bool compact)
    {
        // Content của view có padding ngoài + Border 18 DIP và khoảng cách giữa hai panel.
        // Không cần đo tuyệt đối từng pixel; lấy chiều rộng thực của ContentView làm cơ sở
        // giúp tỉ lệ vẫn ổn khi Windows maximize/restore hoặc DPI scale thay đổi.
        double innerWidth =
            Math.Max(
                0d,
                availableWidth - 72d);

        double targetHeight;

        if (compact)
        {
            // Hai panel xếp dọc: biểu đồ gần full width, giữ tỉ lệ rộng/cao tự nhiên.
            targetHeight =
                Math.Clamp(
                    innerWidth / CompactGraphAspectRatio,
                    CompactGraphMinHeight,
                    CompactGraphMaxHeight);

            InteractiveExplanationPanel.MinimumHeightRequest = -1d;
        }
        else
        {
            // Layout 30/70: ước lượng đúng chiều rộng thực của panel biểu đồ rồi suy ra
            // chiều cao theo aspect ratio. Nhờ vậy Border "Minh họa tương tác" tự mở
            // rộng theo scale màn hình thay vì biểu đồ càng rộng càng bẹt.
            double graphWidth =
                Math.Max(
                    0d,
                    (innerWidth - InteractiveGraphGrid.ColumnSpacing) * 0.70d);

            targetHeight =
                Math.Clamp(
                    graphWidth / WideGraphAspectRatio,
                    WideGraphMinHeight,
                    WideGraphMaxHeight);

            // Giữ hai cột 30/70 cân chiều cao. Border ngoài dùng Auto nên sẽ tự nở theo.
            InteractiveExplanationPanel.MinimumHeightRequest = targetHeight;
        }

        ProportionGraphicsView.HeightRequest = targetHeight;
        InteractiveGraphPanel.MinimumHeightRequest = targetHeight;
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
        Dispatcher.Dispatch(() =>
        {
            RefreshSliderTheme();
            ProportionGraphicsView.Invalidate();
        });
    }

    private void RefreshSliderTheme()
    {
        Color primary =
            ThemeResource.GetColor(
                "PrimaryColor",
                "#6D28D9");

        Color primaryBorder =
            ThemeResource.GetColor(
                "PrimaryBorderColor",
                "#C4B5FD");

        Color sliderBackground =
            ThemeResource.GetColor(
                "SurfaceAltColor",
                AppThemeManager.IsDarkThemeEffective
                    ? "#172033"
                    : "#F4F8FF");

        // Concrete colors are intentional here. WinUI's native Slider can
        // retain old brush instances after a runtime palette swap.
        // Dùng cùng một track duy nhất để không còn cảm giác bị tách thành 2 thanh.
        ProportionXSlider.MinimumTrackColor = primary;
        ProportionXSlider.MaximumTrackColor = primaryBorder;
        ProportionXSlider.ThumbColor = primary;
        ProportionXSlider.BackgroundColor = sliderBackground;
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
