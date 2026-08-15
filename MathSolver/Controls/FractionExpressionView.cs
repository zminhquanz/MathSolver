using System.Numerics;
using System.Globalization;
using Microsoft.Maui.Layouts;

namespace MathSolver.Controls;

public sealed class FractionExpressionView : ContentView
{
    public static readonly BindableProperty ExpressionProperty =
        BindableProperty.Create(
            nameof(Expression),
            typeof(string),
            typeof(FractionExpressionView),
            string.Empty,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty MathFontSizeProperty =
        BindableProperty.Create(
            nameof(MathFontSize),
            typeof(double),
            typeof(FractionExpressionView),
            16d,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty MathColorProperty =
        BindableProperty.Create(
            nameof(MathColor),
            typeof(Color),
            typeof(FractionExpressionView),
            Color.FromArgb("#334155"),
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty HorizontalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(TextAlignment),
            typeof(FractionExpressionView),
            TextAlignment.Start,
            propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty WrapContentProperty =
        BindableProperty.Create(
            nameof(WrapContent),
            typeof(bool),
            typeof(FractionExpressionView),
            false,
            propertyChanged: OnVisualPropertyChanged);

    public string Expression
    {
        get => (string)GetValue(ExpressionProperty);
        set => SetValue(ExpressionProperty, value);
    }

    public double MathFontSize
    {
        get => (double)GetValue(MathFontSizeProperty);
        set => SetValue(MathFontSizeProperty, value);
    }

    public Color MathColor
    {
        get => (Color)GetValue(MathColorProperty);
        set => SetValue(MathColorProperty, value);
    }

    public TextAlignment HorizontalTextAlignment
    {
        get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty);
        set => SetValue(HorizontalTextAlignmentProperty, value);
    }

    public bool WrapContent
    {
        get => (bool)GetValue(WrapContentProperty);
        set => SetValue(WrapContentProperty, value);
    }

    public FractionExpressionView()
    {
        SetDynamicResource(
            MathColorProperty,
            "TextPrimaryColor");

        Rebuild();
    }

    private static void OnVisualPropertyChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        ((FractionExpressionView)bindable).Rebuild();
    }

    private void Rebuild()
    {
        var rootLayout = new VerticalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = WrapContent
                ? LayoutOptions.Fill
                : GetHorizontalLayoutOptions()
        };

        string expression =
            Expression ?? string.Empty;

        string[] lines =
            expression
                .Replace("\r", string.Empty)
                .Split('\n');

        foreach (string line in lines)
        {
            rootLayout.Children.Add(
                CreateExpressionLine(line));
        }

        Content = rootLayout;
    }

    private View CreateExpressionLine(
        string line)
    {
        string[] tokens =
            line.Split(
                ' ',
                StringSplitOptions
                    .RemoveEmptyEntries);

        if (!WrapContent)
        {
            var singleLineLayout =
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = GetHorizontalLayoutOptions()
                };

            foreach (string token in tokens)
            {
                singleLineLayout.Children.Add(
                    CreateTokenView(token));
            }

            return singleLineLayout;
        }

        var wrappingLayout =
            new FlexLayout
            {
                Direction = FlexDirection.Row,
                Wrap = FlexWrap.Wrap,
                AlignItems = FlexAlignItems.Center,
                JustifyContent = HorizontalTextAlignment switch
                {
                    TextAlignment.Center => FlexJustify.Center,
                    TextAlignment.End => FlexJustify.End,
                    _ => FlexJustify.Start
                },
                HorizontalOptions = LayoutOptions.Fill
            };

        foreach (string token in tokens)
        {
            View tokenView = CreateTokenView(token);
            tokenView.Margin = new Thickness(0, 0, 8, 4);
            wrappingLayout.Children.Add(tokenView);
        }

        return wrappingLayout;
    }

    private LayoutOptions GetHorizontalLayoutOptions() =>
        HorizontalTextAlignment switch
        {
            TextAlignment.Center => LayoutOptions.Center,
            TextAlignment.End => LayoutOptions.End,
            _ => LayoutOptions.Start
        };

    private View CreateTokenView(
        string token)
    {
        if (TryParseFraction(
                token,
                out string numerator,
                out string denominator))
        {
            return CreateFractionView(
                numerator,
                denominator);
        }

        if (TrySplitDecoratedFraction(
                token,
                out string prefix,
                out string fractionToken,
                out string suffix) &&
            TryParseFraction(
                fractionToken,
                out numerator,
                out denominator))
        {
            var decorated = new HorizontalStackLayout
            {
                Spacing = 1,
                VerticalOptions = LayoutOptions.Center
            };

            if (prefix.Length > 0)
            {
                decorated.Children.Add(CreateTextToken(prefix));
            }

            decorated.Children.Add(
                CreateFractionView(numerator, denominator));

            if (suffix.Length > 0)
            {
                decorated.Children.Add(CreateTextToken(suffix));
            }

            return decorated;
        }

        return CreateTextToken(token);
    }

    private Label CreateTextToken(string text) =>
        new()
        {
            Text = text,
            FontSize = MathFontSize,
            FontAttributes =
                FontAttributes.Bold,

            TextColor = MathColor,

            VerticalTextAlignment =
                TextAlignment.Center
        };

    private static bool TrySplitDecoratedFraction(
        string token,
        out string prefix,
        out string fractionToken,
        out string suffix)
    {
        const string leadingPunctuation = "([{\"'“‘";
        const string trailingPunctuation = ").,;:!?]}\"'”’";

        int start = 0;
        while (start < token.Length &&
               leadingPunctuation.Contains(token[start]))
        {
            start++;
        }

        int end = token.Length;
        while (end > start &&
               trailingPunctuation.Contains(token[end - 1]))
        {
            end--;
        }

        prefix = token[..start];
        fractionToken = token[start..end];
        suffix = token[end..];

        return start > 0 || end < token.Length;
    }

    private View CreateFractionView(
        string numerator,
        string denominator)
    {
        double minimumWidth =
            Math.Max(
                34,
                Math.Max(
                    numerator.Length,
                    denominator.Length) *
                MathFontSize * 0.65);

        var fractionGrid =
            new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(
                        GridLength.Auto),

                    new RowDefinition(
                        new GridLength(2)),

                    new RowDefinition(
                        GridLength.Auto)
                },

                RowSpacing = 2,

                MinimumWidthRequest =
                    minimumWidth,

                VerticalOptions =
                    LayoutOptions.Center
            };

        var numeratorLabel =
            new Label
            {
                Text = numerator,
                FontSize = MathFontSize,

                FontAttributes =
                    FontAttributes.Bold,

                TextColor = MathColor,

                HorizontalTextAlignment =
                    TextAlignment.Center
            };

        var fractionBar =
            new BoxView
            {
                HeightRequest = 2,
                BackgroundColor = MathColor,

                HorizontalOptions =
                    LayoutOptions.Fill
            };

        var denominatorLabel =
            new Label
            {
                Text = denominator,
                FontSize = MathFontSize,

                FontAttributes =
                    FontAttributes.Bold,

                TextColor = MathColor,

                HorizontalTextAlignment =
                    TextAlignment.Center
            };

        fractionGrid.Add(
            numeratorLabel,
            0,
            0);

        fractionGrid.Add(
            fractionBar,
            0,
            1);

        fractionGrid.Add(
            denominatorLabel,
            0,
            2);

        return fractionGrid;
    }

    private static bool TryParseFraction(
        string token,
        out string numerator,
        out string denominator)
    {
        numerator = string.Empty;
        denominator = string.Empty;

        int slashIndex =
            token.IndexOf('/');

        // Một phân số chỉ được có đúng một dấu "/".
        if (slashIndex <= 0 ||
            slashIndex != token.LastIndexOf('/') ||
            slashIndex >= token.Length - 1)
        {
            return false;
        }

        string numeratorText =
            token[..slashIndex];

        string denominatorText =
            token[(slashIndex + 1)..];

        // Không chỉ nhận một số nguyên như 4/5,
        // mà còn nhận tích ở tử và mẫu như:
        // 4×6/5×7 hoặc (4×6)/(5×7).
        if (!TryNormalizeFractionPart(
                numeratorText,
                out numerator) ||
            !TryNormalizeFractionPart(
                denominatorText,
                out denominator))
        {
            numerator = string.Empty;
            denominator = string.Empty;
            return false;
        }

        return true;
    }

    private static bool TryNormalizeFractionPart(
        string text,
        out string displayText)
    {
        displayText = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalizedText =
            RemoveOuterParentheses(
                text.Trim());

        // Hỗ trợ cả dấu nhân toán học và dấu *.
        string[] factors =
            normalizedText.Split(
                ['×', '*'],
                StringSplitOptions.None);

        if (factors.Length == 0)
        {
            return false;
        }

        var displayFactors =
            new List<string>(
                factors.Length);

        foreach (string factor in factors)
        {
            string factorText =
                RemoveOuterParentheses(
                    factor.Trim());

            // Không chấp nhận toán tử nhân bị thiếu toán hạng,
            // ví dụ 4× hoặc ×6.
            if (string.IsNullOrWhiteSpace(
                    factorText))
            {
                return false;
            }

            if (!TryFormatMathFactor(
            factorText,
            out string formattedFactor))
                {
                    return false;
                }

                displayFactors.Add(
                    formattedFactor);
        }

        displayText =
            string.Join(
                " × ",
                displayFactors);

        return true;
    }

    private static bool TryFormatMathFactor(
    string text,
    out string displayText)
    {
        displayText =
            string.Empty;

        bool isApproximate =
            text.StartsWith(
                "≈",
                StringComparison.Ordinal);

        string valueText =
            isApproximate
                ? text[1..]
                : text;

        bool isNegative =
            valueText.StartsWith(
                "−",
                StringComparison.Ordinal) ||
            valueText.StartsWith(
                "-",
                StringComparison.Ordinal);

        string unsignedText =
            isNegative
                ? valueText[1..]
                : valueText;

        string parsableText =
            valueText.Replace(
                '−',
                '-');

        if (BigInteger.TryParse(
                parsableText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out BigInteger integerValue))
        {
            displayText =
                isApproximate
                    ? $"≈{FormatIntegerForDisplay(integerValue)}"
                    : FormatIntegerForDisplay(integerValue);

            return true;
        }

        if (decimal.TryParse(
                parsableText,
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out _))
        {
            displayText =
                text.Replace(
                    '-',
                    '−');

            return true;
        }

        if (!IsPowerOfTenToken(
                unsignedText))
        {
            return false;
        }

        string sign =
            isNegative
                ? "−"
                : string.Empty;

        string approximation =
            isApproximate
                ? "≈"
                : string.Empty;

        displayText =
            $"{approximation}{sign}{unsignedText}";

        return true;
    }

    private static bool IsPowerOfTenToken(
        string text)
    {
        if (!text.StartsWith(
                "10",
                StringComparison.Ordinal) ||
            text.Length <= 2)
        {
            return false;
        }

        string exponentText =
            text[2..];

        bool hasExponentDigit =
            false;

        for (int index = 0;
             index < exponentText.Length;
             index++)
        {
            char character =
                exponentText[index];

            if (character == '⁻' &&
                index == 0)
            {
                continue;
            }

            if (character is
                '⁰' or '¹' or '²' or '³' or '⁴' or
                '⁵' or '⁶' or '⁷' or '⁸' or '⁹')
            {
                hasExponentDigit =
                    true;

                continue;
            }

            return false;
        }

        return hasExponentDigit;
    }

    private static string RemoveOuterParentheses(
        string text)
    {
        string result =
            text.Trim();

        while (result.Length >= 2 &&
               result[0] == '(' &&
               result[^1] == ')' &&
               HasSingleOuterParenthesesPair(
                   result))
        {
            result =
                result[1..^1]
                    .Trim();
        }

        return result;
    }

    private static bool HasSingleOuterParenthesesPair(
        string text)
    {
        int depth = 0;

        for (int index = 0;
             index < text.Length;
             index++)
        {
            char character =
                text[index];

            if (character == '(')
            {
                depth++;
            }
            else if (character == ')')
            {
                depth--;

                if (depth < 0)
                {
                    return false;
                }

                // Nếu cặp ngoặc ngoài đóng trước ký tự cuối,
                // thì ngoặc không bao toàn bộ biểu thức.
                if (depth == 0 &&
                    index < text.Length - 1)
                {
                    return false;
                }
            }
        }

        return depth == 0;
    }

    private static string FormatIntegerForDisplay(
        BigInteger value)
    {
        return value.Sign < 0
            ? $"−{BigInteger.Abs(value)}"
            : value.ToString();
    }
}
