using System.Numerics;
using System.Globalization;
using Microsoft.Maui.Layouts;
using MathSolver.Services;

namespace MathSolver.Controls;

public sealed class FractionExpressionView : ContentView
{
    public static readonly BindableProperty ParseArithmeticExpressionsProperty = BindableProperty.Create(
        nameof(ParseArithmeticExpressions), typeof(bool), typeof(FractionExpressionView), false,
        propertyChanged: OnVisualPropertyChanged);
    public bool ParseArithmeticExpressions
    {
        get => (bool)GetValue(ParseArithmeticExpressionsProperty);
        set => SetValue(ParseArithmeticExpressionsProperty, value);
    }
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

    public static readonly BindableProperty TokenSpacingProperty =
        BindableProperty.Create(
            nameof(TokenSpacing),
            typeof(double),
            typeof(FractionExpressionView),
            8d,
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

    public double TokenSpacing
    {
        get => (double)GetValue(TokenSpacingProperty);
        set => SetValue(TokenSpacingProperty, Math.Max(0d, value));
    }

    public FractionExpressionView()
    {
        Loaded += OnLoaded;
        Unloaded += (_, _) => AppLanguageManager.LanguageChanged -= OnLanguageChanged;
        SetDynamicResource(
            MathColorProperty,
            "WallpaperTextPrimaryColor");

        Rebuild();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        AppLanguageManager.LanguageChanged -= OnLanguageChanged;
        AppLanguageManager.LanguageChanged += OnLanguageChanged;
        Rebuild();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.Dispatch(Rebuild);

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

        SemanticProperties.SetDescription(this,
            AccessibleMathText.Format(expression, AppLanguageManager.CurrentLanguage));
        AutomationProperties.SetIsInAccessibleTree(this, true);
        AutomationProperties.SetExcludedWithChildren(rootLayout, true);

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
        if (ParseArithmeticExpressions)
        {
            var fragments = TextbookFractionParser.ParseLine(line);
            if (fragments.Any(fragment => fragment.Math is not null))
                return CreateTextbookLine(fragments);
        }

        string[] tokens =
            line.Split(
                ' ',
                StringSplitOptions
                    .RemoveEmptyEntries);

        // Plain result/unit lines need normal word spacing, even when the
        // question itself contains a stacked fraction.
        bool hasFraction = tokens.Any(token =>
        {
            TrySplitDecoratedFraction(token, out _, out string fraction, out _);
            return TryParseFraction(fraction, out _, out _);
        });
        if (!hasFraction)
        {
            Label textLine = CreateTextToken(line);
            textLine.HorizontalTextAlignment = HorizontalTextAlignment;
            textLine.HorizontalOptions = WrapContent ? LayoutOptions.Fill : GetHorizontalLayoutOptions();
            textLine.LineBreakMode = WrapContent ? LineBreakMode.WordWrap : LineBreakMode.NoWrap;
            return textLine;
        }

        if (!WrapContent)
        {
            var singleLineLayout =
                new HorizontalStackLayout
                {
                    Spacing = TokenSpacing,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = GetHorizontalLayoutOptions()
                };

            foreach (View tokenView in CreateTokenViews(tokens))
            {
                singleLineLayout.Children.Add(
                    tokenView);
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

        foreach (View tokenView in CreateTokenViews(tokens))
        {
            tokenView.Margin = new Thickness(
                0,
                0,
                TokenSpacing,
                Math.Min(4d, TokenSpacing));
            wrappingLayout.Children.Add(tokenView);
        }

        return wrappingLayout;
    }

    private View CreateTextbookLine(IReadOnlyList<TextbookFractionParser.Fragment> fragments)
    {
        var items = new List<View>();
        foreach (var fragment in fragments)
        {
            if (fragment.Math is { } math)
                items.AddRange(TopLevelMathViews(math));
            else
                items.AddRange(fragment.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(CreateTextToken));
        }
        if (!WrapContent)
        {
            var row = new HorizontalStackLayout { Spacing = TokenSpacing, VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = GetHorizontalLayoutOptions() };
            foreach (var item in items) row.Children.Add(item);
            return row;
        }
        var wrap = new FlexLayout { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center,
            JustifyContent = HorizontalTextAlignment switch { TextAlignment.Center => FlexJustify.Center,
                TextAlignment.End => FlexJustify.End, _ => FlexJustify.Start }, HorizontalOptions = LayoutOptions.Fill };
        foreach (var item in items)
        {
            item.Margin = new Thickness(0, 0, TokenSpacing, Math.Min(4, TokenSpacing));
            wrap.Children.Add(item);
        }
        return wrap;
    }

    private IEnumerable<View> TopLevelMathViews(TextbookFractionParser.Node node)
    {
        // Fractions and mixed numbers stay intact; equations can wrap between operators.
        if (node is TextbookFractionParser.Binary { Operator: not "/" } binary)
        {
            foreach (var child in TopLevelMathViews(binary.Left)) yield return child;
            yield return CreateTextToken(MathSymbol(binary.Operator));
            foreach (var child in TopLevelMathViews(binary.Right)) yield return child;
        }
        else yield return CreateMathNode(node);
    }

    private static string MathSymbol(string symbol) => symbol switch { "*" => "×", "-" => "−", _ => symbol };

    private View CreateMathNode(TextbookFractionParser.Node node)
    {
        View Row(params View[] children)
        {
            var row = new HorizontalStackLayout { Spacing = Math.Min(TokenSpacing, MathFontSize * .2),
                VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
            foreach (var child in children) row.Children.Add(child);
            return row;
        }
        TextbookFractionParser.Node Ungroup(TextbookFractionParser.Node part) => part is TextbookFractionParser.Group group
            ? Ungroup(group.Content) : part;
        return node switch
        {
            TextbookFractionParser.Number number => CreateTextToken(BigInteger.TryParse(number.Text,
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ? FormatIntegerForDisplay(integer) : number.Text),
            TextbookFractionParser.Group group => Row(CreateTextToken("("), CreateMathNode(group.Content), CreateTextToken(")")),
            TextbookFractionParser.Unary unary => Row(CreateTextToken(MathSymbol(unary.Operator)), CreateMathNode(unary.Content)),
            TextbookFractionParser.Mixed mixed => Row(CreateTextToken(mixed.Whole.Text),
                CreateFractionView(CreateMathNode(mixed.Numerator), CreateMathNode(mixed.Denominator))),
            TextbookFractionParser.Binary { Operator: "/" } fraction => CreateFractionView(
                CreateMathNode(Ungroup(fraction.Left)), CreateMathNode(Ungroup(fraction.Right))),
            TextbookFractionParser.Binary binary => Row(CreateMathNode(binary.Left), CreateTextToken(MathSymbol(binary.Operator)), CreateMathNode(binary.Right)),
            _ => throw new ArgumentOutOfRangeException(nameof(node))
        };
    }

    private View CreateFractionView(View numerator, View denominator)
    {
        var grid = new Grid { RowDefinitions = { new(GridLength.Auto), new(new GridLength(2)), new(GridLength.Auto) },
            RowSpacing = 2, MinimumWidthRequest = MathFontSize * 1.25, VerticalOptions = LayoutOptions.Center };
        numerator.HorizontalOptions = LayoutOptions.Center;
        denominator.HorizontalOptions = LayoutOptions.Center;
        grid.Add(numerator, 0, 0);
        grid.Add(new BoxView { HeightRequest = 2, BackgroundColor = MathColor, HorizontalOptions = LayoutOptions.Fill }, 0, 1);
        grid.Add(denominator, 0, 2);
        return grid;
    }

    private IEnumerable<View> CreateTokenViews(string[] tokens)
    {
        for (int index = 0; index < tokens.Length; index++)
        {
            // A mixed number is one mathematical item; keep the whole and
            // proper fraction together when a paragraph or choice wraps.
            TrySplitDecoratedFraction(tokens[index], out string prefix, out string whole, out string wholeSuffix);
            if (index + 1 < tokens.Length && wholeSuffix.Length == 0 &&
                BigInteger.TryParse(whole, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                TrySplitDecoratedFraction(tokens[index + 1], out string fractionPrefix, out string fraction, out string suffix);
                string[] parts = fraction.Split('/');
                if (fractionPrefix.Length == 0 && parts.Length == 2 &&
                    BigInteger.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger n) &&
                    BigInteger.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger d) &&
                    n > 0 && n < d && TryParseFraction(fraction, out string numerator, out string denominator))
                {
                    var mixed = new HorizontalStackLayout
                    {
                        Spacing = Math.Min(TokenSpacing, MathFontSize * .12),
                        VerticalOptions = LayoutOptions.Center
                    };
                    if (prefix.Length > 0) mixed.Children.Add(CreateTextToken(prefix));
                    mixed.Children.Add(CreateTextToken(whole));
                    mixed.Children.Add(CreateFractionView(numerator, denominator));
                    if (suffix.Length > 0) mixed.Children.Add(CreateTextToken(suffix));
                    yield return mixed;
                    index++;
                    continue;
                }
            }
            yield return CreateTokenView(tokens[index]);
        }
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
        // Tách dấu câu trước khi thử parse phân số. Nếu parse token đầy đủ
        // trước, chuỗi như "38425517497/24508967912." có thể bị decimal
        // parser hiểu dấu chấm cuối câu là dấu thập phân của mẫu số, khiến
        // mẫu không được chèn dấu phân cách hàng nghìn.
        if (TrySplitDecoratedFraction(
                token,
                out string prefix,
                out string fractionToken,
                out string suffix) &&
            TryParseFraction(
                fractionToken,
                out string numerator,
                out string denominator))
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

        if (TryParseFraction(
                token,
                out numerator,
                out denominator))
        {
            return CreateFractionView(
                numerator,
                denominator);
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

        // Kết quả phân số ở chế độ "Hiển thị đầy đủ" có thể đã được
        // formatter chung chèn dấu phẩy phân cách hàng nghìn, ví dụ
        // 100,000,000/25,000,000. Khi nhận diện token để dựng phân số
        // kiểu SGK, bỏ dấu phân cách trước khi parse nhưng vẫn format lại
        // có grouping khi render.
        string parsableText =
            valueText
                .Replace(
                    ",",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
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
        string grouped =
            BigInteger.Abs(value)
                .ToString(
                    "N0",
                    CultureInfo.InvariantCulture);

        return value.Sign < 0
            ? $"−{grouped}"
            : grouped;
    }
}
