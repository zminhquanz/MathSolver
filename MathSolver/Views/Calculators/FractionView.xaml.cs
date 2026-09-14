using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Views.Base;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace MathSolver.Views;

public partial class FractionView : LocalizedSolverView
{
    private readonly FractionCalculationEngine _fractionEngine = new();
    private bool _isCompact;
    private bool _isUpdatingNumberText;

    // Ghi nhớ nội dung đang được khôi phục sau khi nhập sai.
    private readonly Dictionary<Entry, string>
        _pendingRestoredEntryTexts =
            [];

    private const int MaxIntegerInputDigits =
        39;

    private const string Int128InputRangeText =
        "−170,141,183,460,469,231,731,687,303,715,884,105,728 đến 170,141,183,460,469,231,731,687,303,715,884,105,727";

    private static readonly BigInteger MinInt128InputValue =
        (BigInteger)Int128.MinValue;

    private static readonly BigInteger MaxInt128InputValue =
        (BigInteger)Int128.MaxValue;

    private const string IntegerTypingErrorMessage =
        "Tử số và mẫu số chỉ được nhập số nguyên trong phạm vi từ " +
        Int128InputRangeText +
        "; dấu phân cách hàng nghìn được thêm tự động.";

    // Giá trị chính xác của Entry được lưu ở dạng khoa học dùng chữ e.
    // Ví dụ: 1.234567890123456789e18.
    private readonly Dictionary<Entry, string>
        _entryScientificCodeValues =
            [];

    private const int ScientificDisplayDigitThreshold =
        18;

    private const int ScientificDisplaySignificantDigits =
        12;

    private static readonly Regex LargeIntegerRegex =
        new(
            @"(?<!\d)(?<value>[-−]?\d{19,})(?!\d)",
            RegexOptions.Compiled);

    public ObservableCollection<FractionSolutionStep> SolutionSteps { get; } = [];

    private FractionOperation _selectedOperation = FractionOperation.Add;

    public FractionView()
    {
        InitializeComponent();

        InitializeLocalization();

        Entry[] fractionEntries =
        [
            FirstNumeratorEntry,
        FirstDenominatorEntry,
        SecondNumeratorEntry,
        SecondDenominatorEntry
        ];

        foreach (Entry entry in fractionEntries)
        {
            entry.Focused +=
                OnFractionEntryFocused;

            entry.Unfocused +=
                OnFractionEntryUnfocused;
        }

        SelectOperation(
            FractionOperation.Add);
    }

    protected override void RefreshLocalizedContent()
    {
        base.RefreshLocalizedContent();

        if (ResultBorder.IsVisible)
        {
            OnCalculateClicked(this, EventArgs.Empty);
        }
    }

    private void SelectOperation(
    FractionOperation operation)
    {
        _selectedOperation = operation;

        Button selectedButton;

        switch (operation)
        {
            case FractionOperation.Add:
                selectedButton = AddButton;

                OperatorLabel.Text = "+";
                OperatorLabel.IsVisible = true;
                break;

            case FractionOperation.Subtract:
                selectedButton = SubtractButton;

                OperatorLabel.Text = "−";
                OperatorLabel.IsVisible = true;
                break;

            case FractionOperation.Multiply:
                selectedButton = MultiplyButton;

                OperatorLabel.Text = "×";
                OperatorLabel.IsVisible = true;
                break;

            case FractionOperation.Divide:
                selectedButton = DivideButton;

                OperatorLabel.Text = "÷";
                OperatorLabel.IsVisible = true;
                break;

            case FractionOperation.CommonDenominator:
                selectedButton = CommonDenominatorButton;
                OperatorLabel.Text = "Và";

                OperatorLabel.IsVisible = true;

                break;

            default:
                selectedButton = AddButton;

                OperatorLabel.Text = "+";
                OperatorLabel.IsVisible = true;
                break;
        }

        SelectionButtonStyler.Select(
            selectedButton,
            AddButton,
            SubtractButton,
            MultiplyButton,
            DivideButton,
            CommonDenominatorButton);

        ResetOutput();
    }

    private void OnAddClicked(
    object? sender,
    EventArgs e)
    {
        SelectOperation(
            FractionOperation.Add);
    }

    private void OnSubtractClicked(
        object? sender,
        EventArgs e)
    {
        SelectOperation(
            FractionOperation.Subtract);
    }

    private void OnMultiplyClicked(
        object? sender,
        EventArgs e)
    {
        SelectOperation(
            FractionOperation.Multiply);
    }

    private void OnDivideClicked(
        object? sender,
        EventArgs e)
    {
        SelectOperation(
            FractionOperation.Divide);
    }

    private void OnCommonDenominatorClicked(
        object? sender,
        EventArgs e)
    {
        SelectOperation(
            FractionOperation.CommonDenominator);
    }

    private void OnCalculateClicked(
        object? sender,
        EventArgs e)
    {
        ResetOutput();

        if (!TryReadInteger(
                GetEntryInputText(
                    FirstNumeratorEntry),
                "Tử số của phân số thứ nhất",
                out Int128 numerator1Input) ||
            !TryReadInteger(
                GetEntryInputText(
                    FirstDenominatorEntry),
                "Mẫu số của phân số thứ nhất",
                out Int128 denominator1Input) ||
            !TryReadInteger(
                GetEntryInputText(
                    SecondNumeratorEntry),
                "Tử số của phân số thứ hai",
                out Int128 numerator2Input) ||
            !TryReadInteger(
                GetEntryInputText(
                    SecondDenominatorEntry),
                "Mẫu số của phân số thứ hai",
                out Int128 denominator2Input))
        {
            return;
        }

        ApplyEntryDisplayValue(
            FirstNumeratorEntry,
            numerator1Input);

        ApplyEntryDisplayValue(
            FirstDenominatorEntry,
            denominator1Input);

        ApplyEntryDisplayValue(
            SecondNumeratorEntry,
            numerator2Input);

        ApplyEntryDisplayValue(
            SecondDenominatorEntry,
            denominator2Input);

        // Đầu vào được giới hạn bằng Int128; chuyển sang BigInteger
        // trước khi nhân chéo để kết quả không bị overflow.
        BigInteger numerator1 =
            (BigInteger)numerator1Input;

        BigInteger denominator1 =
            (BigInteger)denominator1Input;

        BigInteger numerator2 =
            (BigInteger)numerator2Input;

        BigInteger denominator2 =
            (BigInteger)denominator2Input;

        FractionCalculationResult result =
            _fractionEngine.Calculate(
                numerator1,
                denominator1,
                numerator2,
                denominator2,
                _selectedOperation);

        if (!result.IsSuccess)
        {
            ShowError(
                result.ErrorMessage);

            return;
        }

        FullExpressionMathView.Expression =
            LocalizationService.Translate(
                FormatLargeIntegersForDisplay(
                    result.FullExpression));

        AnswerMathView.Expression =
            LocalizationService.Translate(
                FormatLargeIntegersForDisplay(
                    result.ResultExpression));

        foreach (FractionSolutionStep step
                 in result.Steps)
        {
            SolutionSteps.Add(
                CreateDisplayStep(
                    step));
        }

        ResultBorder.IsVisible =
            true;
    }

    public void RefreshNumberDisplay()
    {
        if (ResultBorder.IsVisible)
        {
            OnCalculateClicked(
                this,
                EventArgs.Empty);
        }
    }

    private async void OnFractionCopyResultClicked(
        object? sender,
        EventArgs e)
    {
        await ResultClipboardService.CopyAsync(
            FractionCopyResultButton,
            AnswerMathView.Expression);
    }

    private void OnClearClicked(
        object? sender,
        EventArgs e)
    {
        _entryScientificCodeValues.Clear();
        _pendingRestoredEntryTexts.Clear();

        FirstNumeratorEntry.Text = string.Empty;
        FirstDenominatorEntry.Text = string.Empty;
        SecondNumeratorEntry.Text = string.Empty;
        SecondDenominatorEntry.Text = string.Empty;

        ResetOutput();
        FirstNumeratorEntry.Focus();
    }

    private bool TryReadInteger(
        string? text,
        string fieldName,
        out Int128 value)
    {
        string normalized =
            NormalizeIntegerInput(
                text);

        if (normalized.Length == 0)
        {
            value =
                Int128.Zero;

            ShowError(
                $"Vui lòng nhập {fieldName}.");

            return false;
        }

        if (!TryParseInt128Input(
                normalized,
                out value))
        {
            ShowError(
                $"{fieldName} phải là số nguyên hợp lệ trong phạm vi " +
                $"từ {Int128InputRangeText}.");

            return false;
        }

        return true;
    }

    private void ResetOutput()
    {
        ErrorBorder.IsVisible = false;
        ErrorLabel.Text = string.Empty;

        ResultBorder.IsVisible = false;

        FullExpressionMathView.Expression =
            string.Empty;

        AnswerMathView.Expression =
            string.Empty;

        SolutionSteps.Clear();
    }

    private void ShowError(
    string message)
    {
        ErrorLabel.Text = message;
        ErrorBorder.IsVisible = true;

        ResultBorder.IsVisible = false;

        FullExpressionMathView.Expression =
            string.Empty;

        AnswerMathView.Expression =
            string.Empty;

        SolutionSteps.Clear();
    }

    private string? GetEntryInputText(
    Entry entry)
    {
        return _entryScientificCodeValues.TryGetValue(
                entry,
                out string? scientificCode)
            ? scientificCode
            : entry.Text;
    }

    private void OnNumberEntryTextChanged(
    object? sender,
    TextChangedEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string newText =
            e.NewTextValue ?? string.Empty;

        // SetEntryText khôi phục OldTextValue sẽ phát sinh thêm TextChanged.
        // Bỏ qua sự kiện đó để nó không xóa thông báo lỗi vừa hiển thị.
        if (_pendingRestoredEntryTexts.TryGetValue(
                entry,
                out string? restoredText))
        {
            _pendingRestoredEntryTexts.Remove(entry);

            if (string.Equals(
                    newText,
                    restoredText,
                    StringComparison.Ordinal))
            {
                return;
            }
        }

        if (_isUpdatingNumberText)
        {
            return;
        }

        if (IsValidInputWhileTyping(newText))
        {
            _entryScientificCodeValues.Remove(
                entry);

            string formattedText =
                IntegerInputFormatter.FormatWhileTyping(
                    newText);

            if (!string.Equals(
                    formattedText,
                    newText,
                    StringComparison.Ordinal))
            {
                int logicalPosition =
                    IntegerInputFormatter.CountLogicalCharacters(
                        newText,
                        entry.CursorPosition);

                SetEntryText(
                    entry,
                    formattedText,
                    IntegerInputFormatter.FindCursorPosition(
                        formattedText,
                        logicalPosition));
            }

            // Chỉ xóa lỗi khi người dùng thực sự nhập lại nội dung hợp lệ,
            // không phải khi chương trình đang khôi phục nội dung cũ.
            if (ErrorBorder.IsVisible &&
                string.Equals(
                    ErrorLabel.Text,
                    IntegerTypingErrorMessage,
                    StringComparison.Ordinal))
            {
                ErrorBorder.IsVisible = false;
                ErrorLabel.Text = string.Empty;
            }

            return;
        }

        string oldText =
            e.OldTextValue ?? string.Empty;

        // Hiển thị thông báo trước.
        ShowError(IntegerTypingErrorMessage);

        // Đánh dấu nội dung sắp được khôi phục để bỏ qua
        // TextChanged phát sinh từ việc gán Entry.Text.
        _pendingRestoredEntryTexts[entry] =
            oldText;

        SetEntryText(
            entry,
            oldText);
    }

    private static bool IsValidInputWhileTyping(
        string text)
    {
        if (text.Length == 0 ||
            text is "-" or "−")
        {
            return true;
        }

        string normalizedText =
            text.Replace(
                ",",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                '−',
                '-');

        int firstDigitIndex =
            normalizedText[0] == '-'
                ? 1
                : 0;

        if (firstDigitIndex ==
            normalizedText.Length)
        {
            return true;
        }

        int digitCount =
            0;

        for (int index = firstDigitIndex;
             index < normalizedText.Length;
             index++)
        {
            char character =
                normalizedText[index];

            if (character < '0' ||
                character > '9')
            {
                return false;
            }

            digitCount++;

            if (digitCount >
                MaxIntegerInputDigits)
            {
                return false;
            }
        }

        if (!BigInteger.TryParse(
                normalizedText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out BigInteger integerValue))
        {
            return false;
        }

        return integerValue >=
                   MinInt128InputValue &&
               integerValue <=
                   MaxInt128InputValue;
    }

    private void OnFractionEntryFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string sourceText =
            _entryScientificCodeValues.TryGetValue(
                entry,
                out string? scientificCode)
                ? scientificCode
                : entry.Text ??
                  string.Empty;

        string normalized =
            NormalizeIntegerInput(
                sourceText);

        if (!TryParseInt128Input(
                normalized,
                out Int128 value))
        {
            return;
        }

        _entryScientificCodeValues.Remove(
            entry);

        SetEntryText(
            entry,
            FormatIntegerForEditing(
                (BigInteger)value));
    }

    private void OnFractionEntryUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string normalized =
            NormalizeIntegerInput(
                entry.Text);

        if (!TryParseInt128Input(
                normalized,
                out Int128 value))
        {
            return;
        }

        ApplyEntryDisplayValue(
            entry,
            value);
    }

    private void ApplyEntryDisplayValue(
        Entry entry,
        Int128 value)
    {
        BigInteger bigValue =
            (BigInteger)value;

        if (CountIntegerDigits(
                bigValue) <=
            ScientificDisplayDigitThreshold)
        {
            _entryScientificCodeValues.Remove(
                entry);

            SetEntryText(
                entry,
                FormatIntegerForEditing(
                    bigValue));

            return;
        }

        _entryScientificCodeValues[entry] =
            FormatScientificForCode(
                bigValue);

        SetEntryText(
            entry,
            FormatBigIntegerForDisplay(
                bigValue));
    }

    private void SetEntryText(
        Entry entry,
        string text,
        int? cursorPosition = null)
    {
        _isUpdatingNumberText = true;

        try
        {
            entry.Text = text;
            entry.CursorPosition =
                Math.Clamp(
                    cursorPosition ??
                    text.Length,
                    0,
                    text.Length);
            entry.SelectionLength = 0;
        }
        finally
        {
            _isUpdatingNumberText = false;
        }
    }

    private static string NormalizeIntegerInput(
        string? text)
    {
        return
            (text ?? string.Empty)
            .Trim()
            .Replace(
                ",",
                string.Empty)
            .Replace(
                '−',
                '-')
            .Replace(
                "E",
                "e",
                StringComparison.Ordinal);
    }

    private static bool TryParseInt128Input(
        string text,
        out Int128 value)
    {
        value =
            Int128.Zero;

        // Nội dung người dùng nhập trực tiếp được parse bằng Int128.
        if (!text.Contains(
                'e'))
        {
            if (!Int128.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return false;
            }

            return CountInt128Digits(
                       value) <=
                   MaxIntegerInputDigits;
        }

        // BigInteger chỉ được dùng tạm để khôi phục chuỗi khoa học
        // do ứng dụng tự lưu khi Entry hiển thị rút gọn trên 18 chữ số.
        if (!TryParseBigIntegerInput(
                text,
                out BigInteger bigValue) ||
            bigValue <
                (BigInteger)Int128.MinValue ||
            bigValue >
                (BigInteger)Int128.MaxValue)
        {
            return false;
        }

        value =
            (Int128)bigValue;

        return CountInt128Digits(
                   value) <=
               MaxIntegerInputDigits;
    }

    private static int CountInt128Digits(
        Int128 value)
    {
        return BigInteger.Abs(
                (BigInteger)value)
            .ToString(
                CultureInfo.InvariantCulture)
            .Length;
    }

    private static bool TryParseBigIntegerInput(
        string text,
        out BigInteger value)
    {
        value =
            BigInteger.Zero;

        int exponentIndex =
            text.IndexOf('e');

        if (exponentIndex < 0)
        {
            return BigInteger.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        if (exponentIndex == 0 ||
            exponentIndex !=
            text.LastIndexOf('e') ||
            exponentIndex ==
            text.Length - 1)
        {
            return false;
        }

        string mantissaText =
            text[..exponentIndex];

        string exponentText =
            text[(exponentIndex + 1)..];

        if (!int.TryParse(
                exponentText,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int exponent))
        {
            return false;
        }

        bool isNegative =
            mantissaText.StartsWith(
                "-",
                StringComparison.Ordinal);

        if (isNegative)
        {
            mantissaText =
                mantissaText[1..];
        }

        int decimalPointIndex =
            mantissaText.IndexOf('.');

        if (decimalPointIndex !=
            mantissaText.LastIndexOf('.'))
        {
            return false;
        }

        int decimalPlaces =
            decimalPointIndex >= 0
                ? mantissaText.Length -
                  decimalPointIndex -
                  1
                : 0;

        string coefficientText =
            mantissaText.Replace(
                ".",
                string.Empty);

        if (coefficientText.Length == 0 ||
            !coefficientText.All(
                char.IsDigit) ||
            !BigInteger.TryParse(
                coefficientText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out BigInteger coefficient))
        {
            return false;
        }

        int power =
            exponent -
            decimalPlaces;

        if (power >= 0)
        {
            value =
                coefficient *
                BigInteger.Pow(
                    10,
                    power);
        }
        else
        {
            BigInteger divisor =
                BigInteger.Pow(
                    10,
                    -power);

            value =
                BigInteger.DivRem(
                    coefficient,
                    divisor,
                    out BigInteger remainder);

            if (!remainder.IsZero)
            {
                value =
                    BigInteger.Zero;

                return false;
            }
        }

        if (isNegative)
        {
            value =
                BigInteger.Negate(
                    value);
        }

        return true;
    }

    private static FractionSolutionStep CreateDisplayStep(
        FractionSolutionStep source)
    {
        var displayStep =
            new FractionSolutionStep
            {
                Title =
                    LocalizationService.Translate(
                        source.Title),

                Description =
                    LocalizationService.Translate(
                        FormatLargeIntegersForDisplay(
                            source.Description)),

                IsImportant =
                    source.IsImportant
            };

        foreach (string mathLine
                 in source.MathLines)
        {
            displayStep.MathLines.Add(
                LocalizationService.Translate(
                    FormatLargeIntegersForDisplay(
                        mathLine)));
        }

        return displayStep;
    }

    private static string FormatLargeIntegersForDisplay(
        string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return LargeIntegerRegex.Replace(
            text,
            match =>
            {
                string normalized =
                    match.Groups["value"]
                    .Value
                    .Replace(
                        '−',
                        '-');

                return BigInteger.TryParse(
                        normalized,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out BigInteger value)
                    ? FormatBigIntegerForDisplay(
                        value)
                    : match.Value;
            });
    }

    private static string FormatIntegerForEditing(
        BigInteger value)
    {
        return value.ToString(
            "#,##0",
            CultureInfo.InvariantCulture);
    }

    private static string FormatBigIntegerForDisplay(
        BigInteger value)
    {
        if (ResultNumberDisplayMode.ShowFullNumbers ||
            CountIntegerDigits(value) <=
            ScientificDisplayDigitThreshold)
        {
            return FormatIntegerForEditing(
                value);
        }

        bool isNegative =
            value.Sign < 0;

        string digits =
            BigInteger.Abs(value)
            .ToString(
                CultureInfo.InvariantCulture);

        int exponent =
            digits.Length -
            1;

        int keptDigitCount =
            Math.Min(
                ScientificDisplaySignificantDigits,
                digits.Length);

        string keptDigits =
            digits[..keptDigitCount];

        bool wasShortened =
            digits.Length >
            keptDigitCount &&
            digits[keptDigitCount..]
            .Any(
                character =>
                    character != '0');

        string mantissa =
            BuildMantissaText(
                keptDigits);

        string sign =
            isNegative
                ? "−"
                : string.Empty;

        string approximation =
            wasShortened
                ? "≈"
                : string.Empty;

        if (mantissa == "1")
        {
            return
                $"{approximation}{sign}10" +
                ToSuperscript(
                    exponent);
        }

        return
            $"{approximation}{sign}{mantissa}×10" +
            ToSuperscript(
                exponent);
    }

    private static string FormatScientificForCode(
        BigInteger value)
    {
        if (value.IsZero)
        {
            return "0e0";
        }

        string sign =
            value.Sign < 0
                ? "-"
                : string.Empty;

        string digits =
            BigInteger.Abs(value)
            .ToString(
                CultureInfo.InvariantCulture);

        int exponent =
            digits.Length -
            1;

        string mantissa =
            digits.Length == 1
                ? digits
                : $"{digits[0]}.{digits[1..]}"
                    .TrimEnd('0')
                    .TrimEnd('.');

        return
            $"{sign}{mantissa}e{exponent}";
    }

    private static int CountIntegerDigits(
        BigInteger value)
    {
        return BigInteger.Abs(value)
            .ToString(
                CultureInfo.InvariantCulture)
            .Length;
    }

    private static string BuildMantissaText(
        string digits)
    {
        if (digits.Length == 1)
        {
            return digits;
        }

        return
            $"{digits[0]}.{digits[1..]}"
            .TrimEnd('0')
            .TrimEnd('.');
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

    protected override void OnSizeAllocated(
        double width,
        double height)
    {
        base.OnSizeAllocated(
            width,
            height);

        bool shouldBeCompact =
            width > 0 &&
            width < 700;

        if (_isCompact ==
            shouldBeCompact)
        {
            return;
        }

        _isCompact =
            shouldBeCompact;

        if (shouldBeCompact)
        {
            ConfigureCompactLayout();
        }
        else
        {
            ConfigureExpandedLayout();
        }
    }

    private void ConfigureCompactLayout()
    {
        // Nhóm nút phép tính: bốn phép tính ở hàng đầu,
        // nút Quy đồng chiếm toàn bộ hàng thứ hai.
        OperationButtonsGrid.ColumnDefinitions.Clear();
        OperationButtonsGrid.RowDefinitions.Clear();

        for (int index = 0;
             index < 4;
             index++)
        {
            OperationButtonsGrid.ColumnDefinitions.Add(
                new ColumnDefinition(
                    GridLength.Star));
        }

        OperationButtonsGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        OperationButtonsGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        Grid.SetRow(
            AddButton,
            0);

        Grid.SetColumn(
            AddButton,
            0);

        Grid.SetRow(
            SubtractButton,
            0);

        Grid.SetColumn(
            SubtractButton,
            1);

        Grid.SetRow(
            MultiplyButton,
            0);

        Grid.SetColumn(
            MultiplyButton,
            2);

        Grid.SetRow(
            DivideButton,
            0);

        Grid.SetColumn(
            DivideButton,
            3);

        Grid.SetRow(
            CommonDenominatorButton,
            1);

        Grid.SetColumn(
            CommonDenominatorButton,
            0);

        Grid.SetColumnSpan(
            CommonDenominatorButton,
            4);

        OperationButtonsGrid.ColumnSpacing =
            6;

        OperationButtonsGrid.RowSpacing =
            8;

        // Hai phân số xếp dọc, dấu phép tính nằm giữa.
        FractionInputGrid.ColumnDefinitions.Clear();
        FractionInputGrid.RowDefinitions.Clear();

        FractionInputGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        FractionInputGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        FractionInputGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        FractionInputGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        Grid.SetRow(
            FirstFractionCard,
            0);

        Grid.SetColumn(
            FirstFractionCard,
            0);

        Grid.SetRow(
            OperatorLabel,
            1);

        Grid.SetColumn(
            OperatorLabel,
            0);

        Grid.SetRow(
            SecondFractionCard,
            2);

        Grid.SetColumn(
            SecondFractionCard,
            0);

        FractionInputGrid.RowSpacing =
            10;

        FractionInputGrid.ColumnSpacing =
            0;

        // Nút thao tác xếp dọc để không bị chật trên điện thoại.
        ActionButtonsGrid.ColumnDefinitions.Clear();
        ActionButtonsGrid.RowDefinitions.Clear();

        ActionButtonsGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        ActionButtonsGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        ActionButtonsGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        Grid.SetRow(
            CalculateButton,
            0);

        Grid.SetColumn(
            CalculateButton,
            0);

        Grid.SetRow(
            ClearButton,
            1);

        Grid.SetColumn(
            ClearButton,
            0);

        ActionButtonsGrid.ColumnSpacing =
            0;

        ActionButtonsGrid.RowSpacing =
            8;
    }

    private void ConfigureExpandedLayout()
    {
        // Nhóm nút phép tính trên một hàng.
        OperationButtonsGrid.RowDefinitions.Clear();
        OperationButtonsGrid.ColumnDefinitions.Clear();

        OperationButtonsGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        OperationButtonsGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        OperationButtonsGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        OperationButtonsGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        OperationButtonsGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        OperationButtonsGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(
                    1.35,
                    GridUnitType.Star)));

        Button[] operationButtons =
        [
            AddButton,
            SubtractButton,
            MultiplyButton,
            DivideButton,
            CommonDenominatorButton
        ];

        for (int index = 0;
             index < operationButtons.Length;
             index++)
        {
            Grid.SetRow(
                operationButtons[index],
                0);

            Grid.SetColumn(
                operationButtons[index],
                index);

            Grid.SetColumnSpan(
                operationButtons[index],
                1);
        }

        OperationButtonsGrid.ColumnSpacing =
            8;

        OperationButtonsGrid.RowSpacing =
            0;

        // Hai phân số nằm cạnh nhau.
        FractionInputGrid.RowDefinitions.Clear();
        FractionInputGrid.ColumnDefinitions.Clear();

        FractionInputGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        FractionInputGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        FractionInputGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(
                    64)));

        FractionInputGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        Grid.SetRow(
            FirstFractionCard,
            0);

        Grid.SetColumn(
            FirstFractionCard,
            0);

        Grid.SetRow(
            OperatorLabel,
            0);

        Grid.SetColumn(
            OperatorLabel,
            1);

        Grid.SetRow(
            SecondFractionCard,
            0);

        Grid.SetColumn(
            SecondFractionCard,
            2);

        FractionInputGrid.RowSpacing =
            0;

        FractionInputGrid.ColumnSpacing =
            12;

        // Hai nút thao tác nằm trên một hàng.
        ActionButtonsGrid.RowDefinitions.Clear();
        ActionButtonsGrid.ColumnDefinitions.Clear();

        ActionButtonsGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        ActionButtonsGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        ActionButtonsGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        Grid.SetRow(
            CalculateButton,
            0);

        Grid.SetColumn(
            CalculateButton,
            0);

        Grid.SetRow(
            ClearButton,
            0);

        Grid.SetColumn(
            ClearButton,
            1);

        ActionButtonsGrid.ColumnSpacing =
            10;

        ActionButtonsGrid.RowSpacing =
            0;
    }

}
