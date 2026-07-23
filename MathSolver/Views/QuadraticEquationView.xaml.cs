using MathSolver.Services;
using System.Globalization;
using System.Text;

namespace MathSolver.Views;

public partial class QuadraticEquationView : ContentView
{
    private const int MaxInputDigits =
        15;

    private const int MaxOutputSignificantDigits =
        15;

    // Đồng bộ chiều rộng nội dung với tab Cơ bản, Phân số và Tìm x.
    private const double QuadraticMaximumContentWidth =
        1120d;

    private const double MachineEpsilon =
        2.2204460492503131E-16;

    private bool _isUpdatingText;
    private bool? _isCompactLayout;

    public QuadraticEquationView()
    {
        InitializeComponent();

        LocalizationService.Attach(
            this);

        QuadraticContent.WidthRequest =
            QuadraticMaximumContentWidth;

        ConfigureExpandedLayout();

        _isCompactLayout =
            false;

        UpdateEquationPreview();
    }

    protected override void OnSizeAllocated(
        double width,
        double height)
    {
        base.OnSizeAllocated(
            width,
            height);

        if (width <= 0)
        {
            return;
        }

        UpdateQuadraticContentWidth(
            width);

        bool useCompactLayout =
            width < 700;

        if (_isCompactLayout ==
            useCompactLayout)
        {
            return;
        }

        _isCompactLayout =
            useCompactLayout;

        if (useCompactLayout)
        {
            ConfigureCompactLayout();
        }
        else
        {
            ConfigureExpandedLayout();
        }
    }

    private void UpdateQuadraticContentWidth(
        double availableWidth)
    {
        double targetWidth =
            Math.Min(
                QuadraticMaximumContentWidth,
                availableWidth);

        if (targetWidth <= 0 ||
            Math.Abs(
                QuadraticContent.WidthRequest -
                targetWidth) <
            0.5)
        {
            return;
        }

        QuadraticContent.WidthRequest =
            targetWidth;
    }

    private void ConfigureCompactLayout()
    {
        CoefficientGrid.ColumnDefinitions.Clear();
        CoefficientGrid.RowDefinitions.Clear();

        CoefficientGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        for (int index = 0;
             index < 3;
             index++)
        {
            CoefficientGrid.RowDefinitions.Add(
                new RowDefinition(
                    GridLength.Auto));
        }

        SetCoefficientPanelPosition(
            CoefficientAPanel,
            row: 0,
            column: 0);

        SetCoefficientPanelPosition(
            CoefficientBPanel,
            row: 1,
            column: 0);

        SetCoefficientPanelPosition(
            CoefficientCPanel,
            row: 2,
            column: 0);

        CoefficientGrid.ColumnSpacing =
            0;

        CoefficientGrid.RowSpacing =
            10;
    }

    private void ConfigureExpandedLayout()
    {
        CoefficientGrid.ColumnDefinitions.Clear();
        CoefficientGrid.RowDefinitions.Clear();

        for (int index = 0;
             index < 3;
             index++)
        {
            CoefficientGrid.ColumnDefinitions.Add(
                new ColumnDefinition(
                    GridLength.Star));
        }

        CoefficientGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        SetCoefficientPanelPosition(
            CoefficientAPanel,
            row: 0,
            column: 0);

        SetCoefficientPanelPosition(
            CoefficientBPanel,
            row: 0,
            column: 1);

        SetCoefficientPanelPosition(
            CoefficientCPanel,
            row: 0,
            column: 2);

        CoefficientGrid.ColumnSpacing =
            12;

        CoefficientGrid.RowSpacing =
            0;
    }

    private static void SetCoefficientPanelPosition(
        BindableObject panel,
        int row,
        int column)
    {
        Grid.SetRow(
            panel,
            row);

        Grid.SetColumn(
            panel,
            column);
    }

    private void OnCoefficientEntryTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (_isUpdatingText ||
            sender is not Entry entry)
        {
            return;
        }

        string newText =
            e.NewTextValue ??
            string.Empty;

        if (!IsValidIntegerWhileTyping(
                newText))
        {
            SetEntryText(
                entry,
                e.OldTextValue ??
                string.Empty);

            ShowError(
                $"Chỉ được nhập số nguyên, tối đa " +
                $"{MaxInputDigits} chữ số; " +
                "không được nhập dấu chấm hoặc ký tự khác.");

            return;
        }

        HideResultAndError();
        UpdateEquationPreview();
    }

    private void OnCoefficientEntryFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string normalized =
            NormalizeIntegerText(
                entry.Text);

        if (normalized.Length == 0)
        {
            return;
        }

        SetEntryText(
            entry,
            normalized);
    }

    private void OnCoefficientEntryUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        if (!TryParseCoefficientText(
                entry.Text,
                out double value))
        {
            return;
        }

        SetEntryText(
            entry,
            FormatInputInteger(
                value));

        UpdateEquationPreview();
    }

    private void OnCalculateClicked(
        object? sender,
        EventArgs e)
    {
        HideResultAndError();

        if (!TryReadCoefficient(
                CoefficientAEntry,
                "hệ số a",
                out double a))
        {
            CoefficientAEntry.Focus();
            return;
        }

        if (!TryReadCoefficient(
                CoefficientBEntry,
                "hệ số b",
                out double b))
        {
            CoefficientBEntry.Focus();
            return;
        }

        if (!TryReadCoefficient(
                CoefficientCEntry,
                "hệ số c",
                out double c))
        {
            CoefficientCEntry.Focus();
            return;
        }

        if (a == 0)
        {
            ShowError(
                "Hệ số a phải khác 0. Khi a = 0, " +
                "biểu thức không còn là phương trình bậc hai.");

            CoefficientAEntry.Focus();
            return;
        }

        SetEntryText(
            CoefficientAEntry,
            FormatInputInteger(
                a));

        SetEntryText(
            CoefficientBEntry,
            FormatInputInteger(
                b));

        SetEntryText(
            CoefficientCEntry,
            FormatInputInteger(
                c));

        double delta =
            NormalizeDeltaNearZero(
                CalculateDelta(
                    a,
                    b,
                    c),
                a,
                b,
                c);

        if (!IsFinite(
                delta))
        {
            ShowError(
                "Kết quả Δ vượt quá phạm vi mà ứng dụng hỗ trợ.");

            return;
        }

        ShowSolution(
            a,
            b,
            c,
            delta);
    }

    private void OnClearClicked(
        object? sender,
        EventArgs e)
    {
        SetEntryText(
            CoefficientAEntry,
            string.Empty);

        SetEntryText(
            CoefficientBEntry,
            string.Empty);

        SetEntryText(
            CoefficientCEntry,
            string.Empty);

        HideResultAndError();
        UpdateEquationPreview();

        CoefficientAEntry.Focus();
    }

    private bool TryReadCoefficient(
        Entry entry,
        string fieldName,
        out double value)
    {
        value =
            0;

        string normalized =
            NormalizeIntegerText(
                entry.Text);

        if (normalized.Length == 0 ||
            normalized == "-")
        {
            ShowError(
                $"Vui lòng nhập {fieldName}.");

            return false;
        }

        if (!IsCompleteIntegerText(
                normalized))
        {
            ShowError(
                $"{fieldName} phải là số nguyên hợp lệ.");

            return false;
        }

        int digitCount =
            normalized[0] == '-'
                ? normalized.Length - 1
                : normalized.Length;

        if (digitCount >
            MaxInputDigits)
        {
            ShowError(
                $"{fieldName} chỉ được có tối đa " +
                $"{MaxInputDigits} chữ số.");

            return false;
        }

        if (!double.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value) ||
            !IsFinite(
                value) ||
            Math.Truncate(
                value) !=
            value)
        {
            ShowError(
                $"{fieldName} không nằm trong phạm vi số nguyên " +
                "mà ứng dụng hỗ trợ.");

            value =
                0;

            return false;
        }

        return true;
    }

    private static bool TryParseCoefficientText(
        string? text,
        out double value)
    {
        value =
            0;

        string normalized =
            NormalizeIntegerText(
                text);

        return IsCompleteIntegerText(
                   normalized) &&
               double.TryParse(
                   normalized,
                   NumberStyles.AllowLeadingSign,
                   CultureInfo.InvariantCulture,
                   out value) &&
               IsFinite(
                   value) &&
               Math.Truncate(
                   value) ==
               value;
    }

    private static bool IsValidIntegerWhileTyping(
        string text)
    {
        string normalized =
            NormalizeIntegerText(
                text);

        if (normalized.Length == 0 ||
            normalized == "-")
        {
            return true;
        }

        if (!IsCompleteIntegerText(
                normalized))
        {
            return false;
        }

        int digitCount =
            normalized[0] == '-'
                ? normalized.Length - 1
                : normalized.Length;

        return digitCount <=
               MaxInputDigits;
    }

    private static bool IsCompleteIntegerText(
        string text)
    {
        if (text.Length == 0 ||
            text == "-")
        {
            return false;
        }

        int startIndex =
            text[0] == '-'
                ? 1
                : 0;

        if (startIndex ==
            text.Length)
        {
            return false;
        }

        for (int index = startIndex;
             index < text.Length;
             index++)
        {
            if (!char.IsDigit(
                    text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeIntegerText(
        string? text)
    {
        return
            (text ??
             string.Empty)
            .Trim()
            .Replace(
                ",",
                string.Empty)
            .Replace(
                '−',
                '-');
    }

    private void UpdateEquationPreview()
    {
        string aText =
            GetPreviewCoefficientText(
                CoefficientAEntry,
                "a");

        string bText =
            GetPreviewCoefficientText(
                CoefficientBEntry,
                "b");

        string cText =
            GetPreviewCoefficientText(
                CoefficientCEntry,
                "c");

        EquationPreviewLabel.Text =
            $"({aText})x² + ({bText})x + ({cText}) = 0";
    }

    private static string GetPreviewCoefficientText(
        Entry entry,
        string fallback)
    {
        string normalized =
            NormalizeIntegerText(
                entry.Text);

        return normalized.Length == 0 ||
               normalized == "-"
            ? fallback
            : normalized.Replace(
                "-",
                "−",
                StringComparison.Ordinal);
    }

    private static double CalculateDelta(
        double a,
        double b,
        double c)
    {
        double bSquared =
            b * b;

        // API scalar đa nền tảng .NET tự chọn cách triển khai
        // phù hợp cho x86/x64, ARM64 hoặc fallback phần mềm.
        return Math.FusedMultiplyAdd(
            -4d * a,
            c,
            bSquared);
    }

    private static double NormalizeDeltaNearZero(
        double delta,
        double a,
        double b,
        double c)
    {
        double bSquared =
            Math.Abs(
                b * b);

        double fourAc =
            Math.Abs(
                4d * a * c);

        double scale =
            Math.Max(
                1d,
                Math.Max(
                    bSquared,
                    fourAc));

        double tolerance =
            scale *
            MachineEpsilon *
            8d;

        return Math.Abs(
                   delta) <=
               tolerance
            ? 0d
            : delta;
    }

    private void ShowSolution(
        double a,
        double b,
        double c,
        double delta)
    {
        string aText =
            FormatNumber(
                a);

        string bText =
            FormatNumber(
                b);

        string cText =
            FormatNumber(
                c);

        string deltaText =
            FormatNumber(
                delta);

        string equation =
            BuildEquationText(
                a,
                b,
                c);

        ResultEquationLabel.Text =
            equation;

        DeltaValueLabel.Text =
            $"Δ = {deltaText}";

        Step1TitleLabel.Text =
            "Bước 1. Xác định các hệ số";

        Step1BodyLabel.Text =
            $"Phương trình có dạng ax² + bx + c = 0.\n" +
            $"Ta có: a = {aText}, b = {bText}, c = {cText}.\n" +
            $"Phương trình: {equation}";

        Step2TitleLabel.Text =
            "Bước 2. Tính biệt thức Δ";

        Step2BodyLabel.Text =
            $"Δ = b² − 4ac\n" +
            $"Δ = ({bText})² − 4 × ({aText}) × ({cText})\n" +
            $"Δ = {deltaText}";

        Step3Border.IsVisible =
            true;

        Step4Border.IsVisible =
            true;

        if (delta < 0)
        {
            SetResultStateColors(
                hasRealRoots: false);

            ClassificationLabel.Text =
                "Δ < 0: phương trình vô nghiệm trong tập số thực.";

            RootsLabel.Text =
                "Vô nghiệm";

            Step3TitleLabel.Text =
                "Bước 3. Xét dấu của Δ";

            Step3BodyLabel.Text =
                $"Vì Δ = {deltaText} < 0 nên √Δ không phải " +
                "là một số thực.";

            Step4TitleLabel.Text =
                "Bước 4. Kết luận";

            Step4BodyLabel.Text =
                "Phương trình vô nghiệm trong tập số thực ℝ.";
        }
        else if (delta == 0)
        {
            SetResultStateColors(
                hasRealRoots: true);

            double doubleRoot =
                -b /
                (2d * a);

            if (!IsFinite(
                    doubleRoot))
            {
                ShowError(
                    "Nghiệm vượt quá phạm vi mà ứng dụng hỗ trợ.");

                return;
            }

            string rootText =
                FormatNumber(
                    doubleRoot);

            ClassificationLabel.Text =
                "Δ = 0: phương trình có nghiệm kép.";

            RootsLabel.Text =
                $"x₁ = x₂ = {rootText}";

            Step3TitleLabel.Text =
                "Bước 3. Tính nghiệm kép";

            Step3BodyLabel.Text =
                $"x = −b / (2a)\n" +
                $"x = −({bText}) / " +
                $"(2 × ({aText}))\n" +
                $"x = {rootText}";

            Step4TitleLabel.Text =
                "Bước 4. Kết luận";

            Step4BodyLabel.Text =
                $"Phương trình có nghiệm kép " +
                $"x₁ = x₂ = {rootText}.";
        }
        else
        {
            SetResultStateColors(
                hasRealRoots: true);

            double squareRootDelta =
                Math.Sqrt(
                    delta);

            double denominator =
                2d * a;

            double firstRoot =
                (-b +
                 squareRootDelta) /
                denominator;

            double secondRoot =
                (-b -
                 squareRootDelta) /
                denominator;

            if (!IsFinite(
                    firstRoot) ||
                !IsFinite(
                    secondRoot))
            {
                ShowError(
                    "Nghiệm vượt quá phạm vi mà ứng dụng hỗ trợ.");

                return;
            }

            string squareRootText =
                FormatNumber(
                    squareRootDelta);

            string firstRootText =
                FormatNumber(
                    firstRoot);

            string secondRootText =
                FormatNumber(
                    secondRoot);

            ClassificationLabel.Text =
                "Δ > 0: phương trình có hai nghiệm phân biệt.";

            RootsLabel.Text =
                $"x₁ = {firstRootText}\n" +
                $"x₂ = {secondRootText}";

            Step3TitleLabel.Text =
                "Bước 3. Tính căn bậc hai của Δ";

            Step3BodyLabel.Text =
                $"√Δ = √({deltaText}) = {squareRootText}";

            Step4TitleLabel.Text =
                "Bước 4. Tính hai nghiệm";

            Step4BodyLabel.Text =
                $"x₁ = (−b + √Δ) / (2a)\n" +
                $"x₁ = (−({bText}) + {squareRootText}) / " +
                $"(2 × ({aText}))\n" +
                $"x₁ = {firstRootText}\n\n" +
                $"x₂ = (−b − √Δ) / (2a)\n" +
                $"x₂ = (−({bText}) − {squareRootText}) / " +
                $"(2 × ({aText}))\n" +
                $"x₂ = {secondRootText}";
        }

        ErrorBorder.IsVisible =
            false;

        ResultBorder.IsVisible =
            true;

        SolutionBorder.IsVisible =
            true;
    }

    private void SetResultStateColors(
        bool hasRealRoots)
    {
        string stateColorResource =
            hasRealRoots
                ? "SuccessColor"
                : "ErrorColor";

        ClassificationLabel.SetDynamicResource(
            Label.TextColorProperty,
            stateColorResource);

        RootsLabel.SetDynamicResource(
            Label.TextColorProperty,
            hasRealRoots
                ? "TextPrimaryColor"
                : "ErrorColor");
    }

    private static string BuildEquationText(
        double a,
        double b,
        double c)
    {
        var builder =
            new StringBuilder();

        AppendLeadingTerm(
            builder,
            a,
            "x²");

        AppendFollowingTerm(
            builder,
            b,
            "x");

        AppendFollowingTerm(
            builder,
            c,
            string.Empty);

        builder.Append(
            " = 0");

        return builder.ToString();
    }

    private static void AppendLeadingTerm(
        StringBuilder builder,
        double coefficient,
        string variable)
    {
        if (coefficient == -1 &&
            variable.Length > 0)
        {
            builder.Append(
                '−');

            builder.Append(
                variable);

            return;
        }

        if (coefficient == 1 &&
            variable.Length > 0)
        {
            builder.Append(
                variable);

            return;
        }

        builder.Append(
            FormatNumber(
                coefficient));

        builder.Append(
            variable);
    }

    private static void AppendFollowingTerm(
        StringBuilder builder,
        double coefficient,
        string variable)
    {
        if (coefficient == 0)
        {
            return;
        }

        bool isNegative =
            coefficient < 0;

        builder.Append(
            isNegative
                ? " − "
                : " + ");

        double absoluteCoefficient =
            Math.Abs(
                coefficient);

        if (absoluteCoefficient != 1 ||
            variable.Length == 0)
        {
            builder.Append(
                FormatNumber(
                    absoluteCoefficient));
        }

        builder.Append(
            variable);
    }

    private static string FormatInputInteger(
        double value)
    {
        return value.ToString(
            "#,##0",
            CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(
        double value)
    {
        if (value == 0)
        {
            return "0";
        }

        string raw =
            value.ToString(
                $"G{MaxOutputSignificantDigits}",
                CultureInfo.InvariantCulture);

        int exponentIndex =
            raw.IndexOfAny(
                ['E', 'e']);

        if (exponentIndex >= 0)
        {
            string mantissa =
                raw[..exponentIndex]
                .Replace(
                    "-",
                    "−",
                    StringComparison.Ordinal);

            int exponent =
                int.Parse(
                    raw[(exponentIndex + 1)..],
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture);

            return
                $"{mantissa} × 10" +
                ToSuperscript(
                    exponent);
        }

        string sign =
            string.Empty;

        if (raw.StartsWith(
                "-",
                StringComparison.Ordinal))
        {
            sign =
                "−";

            raw =
                raw[1..];
        }

        string[] parts =
            raw.Split(
                '.',
                2);

        string groupedInteger =
            GroupIntegerDigits(
                parts[0]);

        return parts.Length == 1
            ? sign +
              groupedInteger
            : sign +
              groupedInteger +
              "." +
              parts[1];
    }

    private static string GroupIntegerDigits(
        string digits)
    {
        if (digits.Length <= 3)
        {
            return digits;
        }

        var builder =
            new StringBuilder(
                digits.Length +
                digits.Length / 3);

        int firstGroupLength =
            digits.Length %
            3;

        if (firstGroupLength == 0)
        {
            firstGroupLength =
                3;
        }

        builder.Append(
            digits.AsSpan(
                0,
                firstGroupLength));

        for (int index = firstGroupLength;
             index < digits.Length;
             index += 3)
        {
            builder.Append(
                ',');

            builder.Append(
                digits.AsSpan(
                    index,
                    3));
        }

        return builder.ToString();
    }

    private static string ToSuperscript(
        int exponent)
    {
        string exponentText =
            exponent.ToString(
                CultureInfo.InvariantCulture);

        var builder =
            new StringBuilder(
                exponentText.Length);

        foreach (char character
                 in exponentText)
        {
            builder.Append(
                character switch
                {
                    '-' => '⁻',
                    '0' => '⁰',
                    '1' => '¹',
                    '2' => '²',
                    '3' => '³',
                    '4' => '⁴',
                    '5' => '⁵',
                    '6' => '⁶',
                    '7' => '⁷',
                    '8' => '⁸',
                    '9' => '⁹',
                    _ => character
                });
        }

        return builder.ToString();
    }

    private void SetEntryText(
        Entry entry,
        string text)
    {
        _isUpdatingText =
            true;

        try
        {
            entry.Text =
                text;

            entry.CursorPosition =
                text.Length;

            entry.SelectionLength =
                0;
        }
        finally
        {
            _isUpdatingText =
                false;
        }
    }

    private void ShowError(
        string message)
    {
        ErrorLabel.Text =
            message;

        ErrorBorder.IsVisible =
            true;

        ResultBorder.IsVisible =
            false;

        SolutionBorder.IsVisible =
            false;
    }

    private void HideResultAndError()
    {
        ErrorBorder.IsVisible =
            false;

        ResultBorder.IsVisible =
            false;

        SolutionBorder.IsVisible =
            false;
    }

    private static bool IsFinite(
        double value)
    {
        return !double.IsNaN(
                   value) &&
               !double.IsInfinity(
                   value);
    }

}