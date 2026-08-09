using MauiFontAttributes = Microsoft.Maui.Controls.FontAttributes;

namespace MathSolver.Controls;

/// <summary>
/// Hiển thị biểu thức căn theo cách trình bày trong sách giáo khoa:
/// căn bậc hai không ghi chỉ số, còn căn bậc lớn hơn đặt chỉ số nhỏ
/// ở góc trên bên trái và có gạch ngang phủ hết số dưới dấu căn.
/// </summary>
public sealed class TextbookRadicalExpressionView : ContentView
{
    public static readonly BindableProperty DegreeProperty =
        BindableProperty.Create(
            nameof(Degree),
            typeof(int),
            typeof(TextbookRadicalExpressionView),
            2,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty RadicandTextProperty =
        BindableProperty.Create(
            nameof(RadicandText),
            typeof(string),
            typeof(TextbookRadicalExpressionView),
            string.Empty,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty LineColorProperty =
        BindableProperty.Create(
            nameof(LineColor),
            typeof(Color),
            typeof(TextbookRadicalExpressionView),
            Color.FromArgb("#16A34A"),
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(Color),
            typeof(TextbookRadicalExpressionView),
            Color.FromArgb("#111827"),
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(
            nameof(FontSize),
            typeof(double),
            typeof(TextbookRadicalExpressionView),
            17d,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty DegreeFontSizeProperty =
        BindableProperty.Create(
            nameof(DegreeFontSize),
            typeof(double),
            typeof(TextbookRadicalExpressionView),
            11d,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty FontAttributesProperty =
        BindableProperty.Create(
            nameof(FontAttributes),
            typeof(MauiFontAttributes),
            typeof(TextbookRadicalExpressionView),
            MauiFontAttributes.Bold,
            propertyChanged: OnVisualPropertyChanged);

    private readonly Grid _expressionGrid;
    private readonly TextbookRadicalStrokeView _strokeView;
    private readonly Label _degreeLabel;
    private readonly Label _radicandLabel;

    public int Degree
    {
        get => (int)GetValue(DegreeProperty);
        set => SetValue(DegreeProperty, value);
    }

    public string RadicandText
    {
        get => (string)GetValue(RadicandTextProperty);
        set => SetValue(RadicandTextProperty, value);
    }

    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public double DegreeFontSize
    {
        get => (double)GetValue(DegreeFontSizeProperty);
        set => SetValue(DegreeFontSizeProperty, value);
    }

    public MauiFontAttributes FontAttributes
    {
        get => (MauiFontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    public TextbookRadicalExpressionView()
    {
        _strokeView =
            new TextbookRadicalStrokeView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };

        _degreeLabel =
            new Label
            {
                Margin = new Thickness(0, 0, -3, 0),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                FontAttributes = MauiFontAttributes.Bold,
                LineBreakMode = LineBreakMode.NoWrap
            };

        _radicandLabel =
            new Label
            {
                Margin = new Thickness(0, 4, 4, 0),
                Padding = Thickness.Zero,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                FontAttributes = MauiFontAttributes.Bold,
                LineBreakMode = LineBreakMode.NoWrap
            };

        _expressionGrid =
            new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition
                    {
                        Width = GridLength.Auto
                    },
                    new ColumnDefinition
                    {
                        Width = new GridLength(18)
                    },
                    new ColumnDefinition
                    {
                        Width = GridLength.Auto
                    }
                },
                RowDefinitions =
                {
                    new RowDefinition
                    {
                        Height = new GridLength(30)
                    }
                },
                ColumnSpacing = 0,
                RowSpacing = 0,
                HeightRequest = 30,
                HorizontalOptions = LayoutOptions.Start
            };

        Grid.SetColumn(
            _strokeView,
            1);

        Grid.SetColumnSpan(
            _strokeView,
            2);

        Grid.SetColumn(
            _degreeLabel,
            0);

        Grid.SetColumn(
            _radicandLabel,
            2);

        _expressionGrid.Children.Add(
            _strokeView);

        _expressionGrid.Children.Add(
            _degreeLabel);

        _expressionGrid.Children.Add(
            _radicandLabel);

        Content =
            _expressionGrid;

        InputTransparent = true;

        UpdateVisuals();
    }

    private static void OnVisualPropertyChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        ((TextbookRadicalExpressionView)bindable)
            .UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        int normalizedDegree =
            Math.Max(
                2,
                Degree);

        _degreeLabel.Text =
            normalizedDegree == 2
                ? string.Empty
                : normalizedDegree.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);

        _degreeLabel.IsVisible =
            normalizedDegree != 2;

        _radicandLabel.Text =
            RadicandText ??
            string.Empty;

        _strokeView.LineColor =
            LineColor;

        _degreeLabel.TextColor =
            LineColor;

        _radicandLabel.TextColor =
            TextColor;

        _radicandLabel.FontSize =
            FontSize;

        _degreeLabel.FontSize =
            DegreeFontSize;

        _radicandLabel.FontAttributes =
            FontAttributes;

        double requestedHeight =
            Math.Max(
                30d,
                FontSize + 13d);

        // Giữ dấu căn cân đối với chữ ở mọi cỡ. Ở cỡ kết quả 32 DIP,
        // móc căn và gạch ngang được phóng theo cùng tỷ lệ thay vì giữ
        // kích thước cố định vốn chỉ phù hợp với chữ 17 DIP.
        double radicalScale =
            requestedHeight /
            30d;

        _expressionGrid.ColumnDefinitions[1].Width =
            new GridLength(
                18d * radicalScale);

        _expressionGrid.HeightRequest =
            requestedHeight;

        _expressionGrid.RowDefinitions[0].Height =
            new GridLength(
                requestedHeight);
    }
}
