using MathSolver.Numerics;
using MathSolver.Services;
using MathSolver.Views.Base;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace MathSolver.Views;

public partial class AverageView : LocalizedSolverView
{
    private const int ResultSignificantDigits =
        OctoDouble.SignificantDigits;

    private static readonly BigInteger MinInt128Value =
        (BigInteger)Int128.MinValue;

    private static readonly BigInteger MaxInt128Value =
        (BigInteger)Int128.MaxValue;

    private AverageNumberType _numberType =
        AverageNumberType.Integer;

    private AverageSolutionState? _solutionState;
    private bool _isUpdatingValuesText;
    private bool _isShowingCompactValues;
    private string _editableValuesText = string.Empty;

    public AverageView()
    {
        InitializeComponent();

        // Toàn bộ chuỗi tĩnh dùng stable localization key. Không để bộ dịch
        // legacy ghi đè các binding hoặc phần lời giải được dựng bằng code.
        InitializeLocalization();

        ValuesEditor.Focused +=
            OnValuesEditorFocused;

        ValuesEditor.Unfocused +=
            OnValuesEditorUnfocused;

        SelectNumberType(
            AverageNumberType.Integer,
            clearInput: false);
    }

    protected override void RefreshLocalizedContent()
    {
        base.RefreshLocalizedContent();
        RefreshLocalizedSolution();
    }

    private void OnIntegerTypeClicked(
        object? sender,
        EventArgs e)
    {
        SelectNumberType(
            AverageNumberType.Integer,
            clearInput: true);
    }

    private void OnDecimalTypeClicked(
        object? sender,
        EventArgs e)
    {
        SelectNumberType(
            AverageNumberType.Decimal,
            clearInput: true);
    }

    private void SelectNumberType(
        AverageNumberType numberType,
        bool clearInput)
    {
        _numberType =
            numberType;

        Button selectedButton =
            numberType == AverageNumberType.Integer
                ? IntegerTypeButton
                : DecimalTypeButton;

        SelectionButtonStyler.Select(
            selectedButton,
            IntegerTypeButton,
            DecimalTypeButton);

        if (clearInput)
        {
            ClearAll();
        }
    }

    private void OnValuesTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (_isUpdatingValuesText)
        {
            return;
        }

        HideError();
        HideResult();

        string newText =
            e.NewTextValue ??
            string.Empty;

        string formattedText =
            FormatValuesWhileTyping(
                newText);

        _editableValuesText =
            formattedText;

        _isShowingCompactValues =
            false;

        if (formattedText == newText)
        {
            return;
        }

        int oldCursorPosition =
            Math.Clamp(
                ValuesEditor.CursorPosition,
                0,
                newText.Length);

        int logicalPosition =
            CountLogicalCharacters(
                newText,
                oldCursorPosition);

        SetValuesEditorText(
            formattedText,
            FindCursorPosition(
                formattedText,
                logicalPosition));
    }

    private void OnValuesEditorFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (!_isShowingCompactValues)
        {
            return;
        }

        _isShowingCompactValues =
            false;

        SetValuesEditorText(
            _editableValuesText,
            _editableValuesText.Length);
    }

    private void OnValuesEditorUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(
                _editableValuesText))
        {
            return;
        }

        string displayText =
            FormatValuesForDisplay(
                _editableValuesText);

        if (displayText ==
            _editableValuesText)
        {
            _isShowingCompactValues =
                false;

            return;
        }

        _isShowingCompactValues =
            true;

        SetValuesEditorText(
            displayText);
    }

    private void OnCalculateClicked(
        object? sender,
        EventArgs e)
    {
        CalculateAverage();
    }

    private void CalculateAverage()
    {
        HideError();
        HideResult();

        string input =
            _isShowingCompactValues
                ? _editableValuesText
                : ValuesEditor.Text ??
                  string.Empty;

        if (string.IsNullOrWhiteSpace(
                input))
        {
            ShowError(
                Translate(
                    "Average.Required"));

            ValuesEditor.Focus();
            return;
        }

        string[] tokens =
            SeparatorRegex()
                .Split(
                    input.Trim())
                .Where(
                    token =>
                        !string.IsNullOrWhiteSpace(
                            token))
                .ToArray();

        if (tokens.Length == 0)
        {
            ShowError(
                Translate(
                    "Average.Required"));

            ValuesEditor.Focus();
            return;
        }

        if (_numberType ==
            AverageNumberType.Integer)
        {
            CalculateIntegerAverage(
                tokens);
        }
        else
        {
            CalculateDecimalAverage(
                tokens);
        }
    }

    private void CalculateIntegerAverage(
        IReadOnlyList<string> tokens)
    {
        var values =
            new List<Int128>(
                tokens.Count);

        for (int index = 0;
             index < tokens.Count;
             index++)
        {
            IntegerParseError error =
                TryParseInteger(
                    tokens[index],
                    out Int128 value);

            if (error !=
                IntegerParseError.None)
            {
                ShowValueError(
                    index,
                    tokens[index],
                    error == IntegerParseError.OutOfRange
                        ? "Average.Int128Range"
                        : "Average.InvalidInteger",
                    Int128.MinValue.ToString(
                        CultureInfo.InvariantCulture),
                    Int128.MaxValue.ToString(
                        CultureInfo.InvariantCulture));

                return;
            }

            values.Add(
                value);
        }

        BigInteger numerator =
            BigInteger.Zero;

        var valueTexts =
            new List<string>(
                values.Count);

        foreach (Int128 value
                 in values)
        {
            BigInteger bigValue =
                (BigInteger)value;

            numerator +=
                bigValue;

            valueTexts.Add(
                FormatRational(
                    bigValue,
                    BigInteger.One));
        }

        ShowSolution(
            valueTexts,
            numerator,
            BigInteger.One,
            values.Count);
    }

    private void CalculateDecimalAverage(
        IReadOnlyList<string> tokens)
    {
        var values =
            new List<decimal>(
                tokens.Count);

        for (int index = 0;
             index < tokens.Count;
             index++)
        {
            DecimalParseError error =
                TryParseDecimal(
                    tokens[index],
                    out decimal value);

            if (error !=
                DecimalParseError.None)
            {
                ShowValueError(
                    index,
                    tokens[index],
                    error == DecimalParseError.OutOfRange
                        ? "Average.DecimalRange"
                        : "Average.InvalidDecimal",
                    decimal.MinValue.ToString(
                        CultureInfo.InvariantCulture),
                    decimal.MaxValue.ToString(
                        CultureInfo.InvariantCulture));

                return;
            }

            values.Add(
                value);
        }

        int commonScale =
            values.Count == 0
                ? 0
                : values.Max(
                    GetDecimalScale);

        BigInteger denominator =
            BigInteger.Pow(
                10,
                commonScale);

        BigInteger numerator =
            BigInteger.Zero;

        var valueTexts =
            new List<string>(
                values.Count);

        foreach (decimal value
                 in values)
        {
            GetDecimalParts(
                value,
                out BigInteger unscaledValue,
                out int scale);

            numerator +=
                unscaledValue *
                BigInteger.Pow(
                    10,
                    commonScale - scale);

            valueTexts.Add(
                FormatRational(
                    unscaledValue,
                    BigInteger.Pow(
                        10,
                        scale)));
        }

        ShowSolution(
            valueTexts,
            numerator,
            denominator,
            values.Count);
    }

    private void ShowSolution(
        IReadOnlyList<string> valueTexts,
        BigInteger sumNumerator,
        BigInteger sumDenominator,
        int count)
    {
        BigInteger averageDenominator =
            sumDenominator *
            count;

        // Kết quả cuối cùng luôn là OctoDouble. Riêng tử/mẫu BigInteger
        // chỉ giữ tổng chính xác của dữ liệu Int128/decimal trước khi đổi kiểu.
        OctoDouble average =
            OctoDouble.FromRational(
                sumNumerator,
                averageDenominator);

        if (!average.IsFinite)
        {
            ShowError(
                Translate(
                    "Average.NotFinite"));

            return;
        }

        string valuesText =
            string.Join(
                "; ",
                valueTexts);

        string expressionText =
            string.Join(
                " + ",
                valueTexts.Select(
                    ParenthesizeNegative));

        string sumText =
            FormatRational(
                sumNumerator,
                sumDenominator);

        string resultText =
            FormatRational(
                sumNumerator,
                averageDenominator);

        _solutionState =
            new AverageSolutionState(
                valuesText,
                expressionText,
                count,
                sumText,
                resultText,
                average);

        RefreshLocalizedSolution();

        ResultBorder.IsVisible =
            true;
    }

    private void RefreshLocalizedSolution()
    {
        if (_solutionState is not
            AverageSolutionState state)
        {
            return;
        }

        AverageResultLabel.Text =
            state.ResultText;

        CountValueLabel.Text =
            state.Count.ToString(
                "#,##0",
                CultureInfo.InvariantCulture);

        SumValueLabel.Text =
            state.SumText;

        NumbersStepLabel.Text =
            FormatTranslation(
                "Average.StepNumbers",
                state.ValuesText);

        CountStepLabel.Text =
            FormatTranslation(
                "Average.StepCount",
                state.Count);

        SumStepLabel.Text =
            FormatTranslation(
                "Average.StepSum",
                state.ExpressionText,
                state.SumText);

        AverageStepLabel.Text =
            FormatTranslation(
                "Average.StepAverage",
                state.SumText,
                state.Count,
                state.ResultText);
    }

    private void ShowValueError(
        int zeroBasedIndex,
        string token,
        string messageKey,
        string minimum,
        string maximum)
    {
        string detail =
            FormatTranslation(
                messageKey,
                minimum,
                maximum);

        ShowError(
            FormatTranslation(
                "Average.ValueError",
                zeroBasedIndex + 1,
                token,
                detail));

        ValuesEditor.Focus();
    }

    private static IntegerParseError TryParseInteger(
        string token,
        out Int128 value)
    {
        value =
            default;

        if (!IntegerTokenRegex().IsMatch(
                token))
        {
            return IntegerParseError.InvalidFormat;
        }

        string normalized =
            token.Replace(
                ",",
                string.Empty,
                StringComparison.Ordinal);

        if (!BigInteger.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out BigInteger bigValue))
        {
            return IntegerParseError.InvalidFormat;
        }

        if (bigValue < MinInt128Value ||
            bigValue > MaxInt128Value)
        {
            return IntegerParseError.OutOfRange;
        }

        value =
            (Int128)bigValue;

        return IntegerParseError.None;
    }

    private static DecimalParseError TryParseDecimal(
        string token,
        out decimal value)
    {
        value =
            default;

        if (!DecimalTokenRegex().IsMatch(
                token))
        {
            return DecimalParseError.InvalidFormat;
        }

        if (!decimal.TryParse(
                token,
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out value))
        {
            return DecimalParseError.OutOfRange;
        }

        return DecimalParseError.None;
    }

    private static int GetDecimalScale(
        decimal value)
    {
        return (decimal.GetBits(
                    value)[3] >> 16) &
               0xFF;
    }

    private static void GetDecimalParts(
        decimal value,
        out BigInteger unscaledValue,
        out int scale)
    {
        int[] bits =
            decimal.GetBits(
                value);

        uint low =
            unchecked((uint)bits[0]);

        uint middle =
            unchecked((uint)bits[1]);

        uint high =
            unchecked((uint)bits[2]);

        scale =
            (bits[3] >> 16) &
            0xFF;

        unscaledValue =
            ((BigInteger)high << 64) |
            ((BigInteger)middle << 32) |
            low;

        if ((bits[3] & int.MinValue) != 0)
        {
            unscaledValue =
                BigInteger.Negate(
                    unscaledValue);
        }
    }

    /// <summary>
    /// Định dạng trực tiếp từ phân số chính xác của dữ liệu nhập. Giá trị
    /// OctoDouble vẫn là kết quả dùng trong tính toán; formatter này tránh
    /// hiển thị nhiễu nhị phân ở cuối các số thập phân hữu hạn như 1.5.
    /// </summary>
    private static string FormatRational(
        BigInteger numerator,
        BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            return numerator.Sign < 0
                ? "-Infinity"
                : numerator.IsZero
                    ? "NaN"
                    : "Infinity";
        }

        if (numerator.IsZero)
        {
            return "0";
        }

        bool negative =
            numerator.Sign !=
            denominator.Sign;

        BigInteger absoluteNumerator =
            BigInteger.Abs(
                numerator);

        BigInteger absoluteDenominator =
            BigInteger.Abs(
                denominator);

        int exponent =
            EstimateDecimalExponent(
                absoluteNumerator,
                absoluteDenominator);

        BigInteger normalizedNumerator;
        BigInteger normalizedDenominator;

        if (exponent >= 0)
        {
            normalizedNumerator =
                absoluteNumerator;

            normalizedDenominator =
                absoluteDenominator *
                BigInteger.Pow(
                    10,
                    exponent);
        }
        else
        {
            normalizedNumerator =
                absoluteNumerator *
                BigInteger.Pow(
                    10,
                    -exponent);

            normalizedDenominator =
                absoluteDenominator;
        }

        char[] digits =
            new char[
                ResultSignificantDigits +
                1];

        BigInteger remainder =
            normalizedNumerator;

        for (int index = 0;
             index < digits.Length;
             index++)
        {
            BigInteger digit =
                BigInteger.DivRem(
                    remainder,
                    normalizedDenominator,
                    out BigInteger nextRemainder);

            digits[index] =
                (char)('0' + (int)digit);

            remainder =
                nextRemainder *
                10;
        }

        int keptLength =
            ResultSignificantDigits;

        if (digits[
                ResultSignificantDigits] >=
            '5')
        {
            int carryIndex =
                keptLength -
                1;

            while (carryIndex >= 0 &&
                   digits[carryIndex] == '9')
            {
                digits[carryIndex] =
                    '0';

                carryIndex--;
            }

            if (carryIndex >= 0)
            {
                digits[carryIndex]++;
            }
            else
            {
                digits[0] =
                    '1';

                for (int index = 1;
                     index < keptLength;
                     index++)
                {
                    digits[index] =
                        '0';
                }

                exponent++;
            }
        }

        while (keptLength > 1 &&
               digits[keptLength - 1] == '0')
        {
            keptLength--;
        }

        string digitText =
            new(
                digits,
                0,
                keptLength);

        string sign =
            negative
                ? "−"
                : string.Empty;

        if (exponent >= 18 ||
            exponent <= -10)
        {
            string mantissa =
                digitText.Length == 1
                    ? digitText
                    : digitText.Insert(
                        1,
                        ".");

            return $"{sign}{mantissa} × 10{ToSuperscript(exponent)}";
        }

        int decimalPointIndex =
            exponent +
            1;

        var builder =
            new StringBuilder();

        builder.Append(
            sign);

        if (decimalPointIndex <= 0)
        {
            builder.Append(
                "0.");

            builder.Append(
                '0',
                -decimalPointIndex);

            builder.Append(
                digitText);
        }
        else if (decimalPointIndex >=
                 digitText.Length)
        {
            builder.Append(
                digitText);

            builder.Append(
                '0',
                decimalPointIndex -
                digitText.Length);
        }
        else
        {
            builder.Append(
                digitText.AsSpan(
                    0,
                    decimalPointIndex));

            builder.Append(
                '.');

            builder.Append(
                digitText.AsSpan(
                    decimalPointIndex));
        }

        return AddThousandsSeparatorsToPlainNumber(
            builder.ToString());
    }

    private string FormatValuesWhileTyping(
        string text)
    {
        if (string.IsNullOrEmpty(
                text))
        {
            return string.Empty;
        }

        return ValueTokenRegex()
            .Replace(
                text,
                match =>
                    FormatValueTokenWhileTyping(
                        match.Value));
    }

    private string FormatValueTokenWhileTyping(
        string token)
    {
        string normalized =
            token.Replace(
                ",",
                string.Empty,
                StringComparison.Ordinal);

        if (normalized is "" or "-" or "." or "-.")
        {
            return token;
        }

        bool isNegative =
            normalized.StartsWith(
                '-');

        string unsignedText =
            isNegative
                ? normalized[1..]
                : normalized;

        if (_numberType ==
            AverageNumberType.Integer)
        {
            if (unsignedText.Length == 0 ||
                !unsignedText.All(
                    char.IsDigit))
            {
                return token;
            }

            return BuildGroupedTypingText(
                unsignedText,
                isNegative,
                hasDecimalPoint: false,
                string.Empty);
        }

        int decimalPointIndex =
            unsignedText.IndexOf(
                '.');

        if (decimalPointIndex !=
                unsignedText.LastIndexOf(
                    '.') ||
            unsignedText.Any(
                character =>
                    character != '.' &&
                    !char.IsDigit(
                        character)))
        {
            return token;
        }

        bool hasDecimalPoint =
            decimalPointIndex >= 0;

        string integerPart =
            hasDecimalPoint
                ? unsignedText[..decimalPointIndex]
                : unsignedText;

        string decimalPart =
            hasDecimalPoint
                ? unsignedText[(decimalPointIndex + 1)..]
                : string.Empty;

        return BuildGroupedTypingText(
            integerPart,
            isNegative,
            hasDecimalPoint,
            decimalPart);
    }

    private static string BuildGroupedTypingText(
        string integerPart,
        bool isNegative,
        bool hasDecimalPoint,
        string decimalPart)
    {
        integerPart =
            integerPart.TrimStart(
                '0');

        if (integerPart.Length == 0)
        {
            integerPart =
                "0";
        }

        string sign =
            isNegative
                ? "-"
                : string.Empty;

        string groupedIntegerPart =
            AddThousandsSeparators(
                integerPart);

        return hasDecimalPoint
            ? $"{sign}{groupedIntegerPart}.{decimalPart}"
            : $"{sign}{groupedIntegerPart}";
    }

    private static string FormatValuesForDisplay(
        string text)
    {
        return ValueTokenRegex()
            .Replace(
                text,
                match =>
                    FormatValueTokenForDisplay(
                        match.Value));
    }

    private static string FormatValueTokenForDisplay(
        string token)
    {
        string normalized =
            token.Replace(
                ",",
                string.Empty,
                StringComparison.Ordinal);

        string unsignedText =
            normalized.TrimStart(
                '-');

        int decimalPointIndex =
            unsignedText.IndexOf(
                '.');

        string integerPart =
            decimalPointIndex >= 0
                ? unsignedText[..decimalPointIndex]
                : unsignedText;

        int integerDigitCount =
            integerPart.TrimStart(
                    '0')
                .Length;

        if (integerDigitCount <= 18)
        {
            return token;
        }

        if (IntegerTokenRegex().IsMatch(
                token) &&
            BigInteger.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out BigInteger integerValue))
        {
            return FormatRational(
                integerValue,
                BigInteger.One);
        }

        if (DecimalTokenRegex().IsMatch(
                token) &&
            decimal.TryParse(
                token,
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out decimal decimalValue))
        {
            GetDecimalParts(
                decimalValue,
                out BigInteger unscaledValue,
                out int scale);

            return FormatRational(
                unscaledValue,
                BigInteger.Pow(
                    10,
                    scale));
        }

        return token;
    }

    private static string AddThousandsSeparatorsToPlainNumber(
        string text)
    {
        bool isNegative =
            text.StartsWith(
                '−');

        string unsignedText =
            isNegative
                ? text[1..]
                : text;

        int decimalPointIndex =
            unsignedText.IndexOf(
                '.');

        string integerPart =
            decimalPointIndex >= 0
                ? unsignedText[..decimalPointIndex]
                : unsignedText;

        string fractionPart =
            decimalPointIndex >= 0
                ? unsignedText[decimalPointIndex..]
                : string.Empty;

        return
            (isNegative ? "−" : string.Empty) +
            AddThousandsSeparators(
                integerPart) +
            fractionPart;
    }

    private static string AddThousandsSeparators(
        string digits)
    {
        if (digits.Length <= 3)
        {
            return digits;
        }

        int firstGroupLength =
            digits.Length % 3;

        if (firstGroupLength == 0)
        {
            firstGroupLength =
                3;
        }

        var builder =
            new StringBuilder(
                digits.Length +
                digits.Length / 3);

        builder.Append(
            digits.AsSpan(
                0,
                firstGroupLength));

        for (int index = firstGroupLength;
             index < digits.Length;
             index += 3)
        {
            builder.Append(',');
            builder.Append(
                digits.AsSpan(
                    index,
                    3));
        }

        return builder.ToString();
    }

    private void SetValuesEditorText(
        string text,
        int? cursorPosition = null)
    {
        _isUpdatingValuesText =
            true;

        ValuesEditor.Text =
            text;

        if (cursorPosition.HasValue)
        {
            ValuesEditor.CursorPosition =
                Math.Clamp(
                    cursorPosition.Value,
                    0,
                    text.Length);
        }

        _isUpdatingValuesText =
            false;
    }

    private static int CountLogicalCharacters(
        string text,
        int endIndex)
    {
        int count =
            0;

        for (int index = 0;
             index < endIndex;
             index++)
        {
            if (text[index] != ',')
            {
                count++;
            }
        }

        return count;
    }

    private static int FindCursorPosition(
        string text,
        int logicalPosition)
    {
        if (logicalPosition <= 0)
        {
            return 0;
        }

        int logicalCount =
            0;

        for (int index = 0;
             index < text.Length;
             index++)
        {
            if (text[index] != ',')
            {
                logicalCount++;
            }

            if (logicalCount >=
                logicalPosition)
            {
                return index + 1;
            }
        }

        return text.Length;
    }

    private static int EstimateDecimalExponent(
        BigInteger numerator,
        BigInteger denominator)
    {
        int exponent =
            numerator.ToString(
                    CultureInfo.InvariantCulture)
                .Length -
            denominator.ToString(
                    CultureInfo.InvariantCulture)
                .Length;

        if (exponent >= 0)
        {
            if (numerator <
                denominator *
                BigInteger.Pow(
                    10,
                    exponent))
            {
                exponent--;
            }
        }
        else if (numerator *
                 BigInteger.Pow(
                     10,
                     -exponent) <
                 denominator)
        {
            exponent--;
        }

        return exponent;
    }

    private static string ParenthesizeNegative(
        string value)
    {
        return value.StartsWith(
                '−')
            ? $"({value})"
            : value;
    }

    private static string ToSuperscript(
        int value)
    {
        const string superscriptDigits =
            "⁰¹²³⁴⁵⁶⁷⁸⁹";

        string text =
            Math.Abs(
                    value)
                .ToString(
                    CultureInfo.InvariantCulture);

        var builder =
            new StringBuilder();

        if (value < 0)
        {
            builder.Append(
                '⁻');
        }

        foreach (char character
                 in text)
        {
            builder.Append(
                superscriptDigits[
                    character -
                    '0']);
        }

        return builder.ToString();
    }

    private static string Translate(
        string key)
    {
        return LocalizationService.TranslateKey(
            key);
    }

    private static string FormatTranslation(
        string key,
        params object[] values)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            Translate(
                key),
            values);
    }

    private async void OnAverageCopyResultClicked(
        object? sender,
        EventArgs e)
    {
        await ResultClipboardService.CopyAsync(
            AverageCopyResultButton,
            _solutionState?.ResultText);
    }

    private void OnClearClicked(
        object? sender,
        EventArgs e)
    {
        ClearAll();
        ValuesEditor.Focus();
    }

    private void ClearAll()
    {
        _editableValuesText =
            string.Empty;

        _isShowingCompactValues =
            false;

        SetValuesEditorText(
            string.Empty);

        HideError();
        HideResult();
    }

    private void ShowError(
        string message)
    {
        ErrorLabel.Text =
            message;

        ErrorBorder.IsVisible =
            true;
    }

    private void HideError()
    {
        ErrorBorder.IsVisible =
            false;

        ErrorLabel.Text =
            string.Empty;
    }

    private void HideResult()
    {
        ResultBorder.IsVisible =
            false;

        _solutionState =
            null;
    }

    [GeneratedRegex(
        @"[+\s;|]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(
        @"[^+\s;|]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex ValueTokenRegex();

    [GeneratedRegex(
        @"^-?(?:\d{1,3}(?:,\d{3})+|\d+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex IntegerTokenRegex();

    [GeneratedRegex(
        @"^-?(?:(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?|\.\d+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DecimalTokenRegex();

    private sealed record AverageSolutionState(
        string ValuesText,
        string ExpressionText,
        int Count,
        string SumText,
        string ResultText,
        OctoDouble Average);

    private enum AverageNumberType
    {
        Integer,
        Decimal
    }

    private enum IntegerParseError
    {
        None,
        InvalidFormat,
        OutOfRange
    }

    private enum DecimalParseError
    {
        None,
        InvalidFormat,
        OutOfRange
    }
}
