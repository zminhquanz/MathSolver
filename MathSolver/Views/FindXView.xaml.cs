using MathSolver.Models;
using MathSolver.Numerics;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Views.Base;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace MathSolver.Views;

public partial class FindXView : LocalizedSolverView
{
    private readonly FindXEngine _findXEngine = new();
    private const int MaxIntegerInputDigits = 39;
    private const int MaxDecimalPlaces = 10;

    private const string Int128InputRangeText =
        "−170,141,183,460,469,231,731,687,303,715,884,105,728 đến 170,141,183,460,469,231,731,687,303,715,884,105,727";

    private const string DecimalInputRangeText =
        "−79,228,162,514,264,337,593,543,950,335 đến 79,228,162,514,264,337,593,543,950,335";

    private static readonly BigInteger MinInt128InputValue =
        (BigInteger)Int128.MinValue;

    private static readonly BigInteger MaxInt128InputValue =
        (BigInteger)Int128.MaxValue;

    private static readonly decimal MinDecimalInputValue =
        decimal.MinValue;

    private static readonly decimal MaxDecimalInputValue =
        decimal.MaxValue;

    private enum FindXInputValidationError
    {
        None,
        InvalidFormat,
        OutOfRange
    }

    private FindXOperation _findXOperation = FindXOperation.Add;
    private FindXUnknownPosition _findXUnknownPosition = FindXUnknownPosition.Left;
    private FindXNumberInputType _findXNumberType = FindXNumberInputType.Integer;
    private bool _isUpdatingFindXNumberText;

    private readonly Dictionary<Entry, string>
        _pendingRestoredEntryTexts =
            [];

    // Responsive bằng code-behind; không dùng VisualStateManager trong XAML.
    private bool? _isCompactInputLayout;

    private const int ScientificDisplayDigitThreshold = 18;
    private const int ScientificDisplaySignificantDigits = 12;

    // Giá trị chính xác của Entry được giữ ở dạng khoa học dùng chữ e
    // khi giao diện đang hiển thị dạng rút gọn, ví dụ 1.234e18.
    private readonly Dictionary<Entry, string>
        _findXScientificCodeValues =
            [];

    public FindXView()
    {
        InitializeComponent();

        InitializeLocalization();

        ConfigureExpandedInputLayout();
        _isCompactInputLayout =
            false;

        InitializeFindXTab();
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

        bool useCompactLayout =
            width < 700;

        if (_isCompactInputLayout ==
            useCompactLayout)
        {
            return;
        }

        _isCompactInputLayout =
            useCompactLayout;

        if (useCompactLayout)
        {
            ConfigureCompactInputLayout();
        }
        else
        {
            ConfigureExpandedInputLayout();
        }
    }

    private void ConfigureCompactInputLayout()
    {
        FindXValueInputGrid.ColumnDefinitions.Clear();
        FindXValueInputGrid.RowDefinitions.Clear();

        FindXValueInputGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        FindXValueInputGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        FindXValueInputGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        Grid.SetRow(
            FindXKnownInputPanel,
            0);

        Grid.SetColumn(
            FindXKnownInputPanel,
            0);

        Grid.SetRow(
            FindXResultInputPanel,
            1);

        Grid.SetColumn(
            FindXResultInputPanel,
            0);

        FindXValueInputGrid.ColumnSpacing =
            0;

        FindXValueInputGrid.RowSpacing =
            10;
    }

    private void ConfigureExpandedInputLayout()
    {
        FindXValueInputGrid.ColumnDefinitions.Clear();
        FindXValueInputGrid.RowDefinitions.Clear();

        FindXValueInputGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        FindXValueInputGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        FindXValueInputGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        Grid.SetRow(
            FindXKnownInputPanel,
            0);

        Grid.SetColumn(
            FindXKnownInputPanel,
            0);

        Grid.SetRow(
            FindXResultInputPanel,
            0);

        Grid.SetColumn(
            FindXResultInputPanel,
            1);

        FindXValueInputGrid.ColumnSpacing =
            12;

        FindXValueInputGrid.RowSpacing =
            0;
    }

    #region Find X

    private void InitializeFindXTab()
    {
        Entry[] findXEntries =
        [
            FindXKnownValueEntry,
            FindXResultValueEntry
        ];

        foreach (Entry entry in findXEntries)
        {
            entry.Focused +=
                OnFindXEntryFocused;

            entry.Unfocused +=
                OnFindXEntryUnfocused;
        }

        SelectFindXOperation(
            FindXOperation.Add);

        SelectFindXUnknownPosition(
            FindXUnknownPosition.Left);

        SelectFindXNumberType(
            FindXNumberInputType.Integer,
            clearInputs: false);
    }

    private void OnFindXAddClicked(
        object? sender,
        EventArgs e)
    {
        SelectFindXOperation(
            FindXOperation.Add);
    }

    private void OnFindXSubtractClicked(
        object? sender,
        EventArgs e)
    {
        SelectFindXOperation(
            FindXOperation.Subtract);
    }

    private void OnFindXMultiplyClicked(
        object? sender,
        EventArgs e)
    {
        SelectFindXOperation(
            FindXOperation.Multiply);
    }

    private void OnFindXDivideClicked(
        object? sender,
        EventArgs e)
    {
        SelectFindXOperation(
            FindXOperation.Divide);
    }

    private void SelectFindXOperation(
        FindXOperation operation)
    {
        _findXOperation =
            operation;

        Button selectedButton =
            operation switch
            {
                FindXOperation.Add =>
                    FindXAddButton,

                FindXOperation.Subtract =>
                    FindXSubtractButton,

                FindXOperation.Multiply =>
                    FindXMultiplyButton,

                FindXOperation.Divide =>
                    FindXDivideButton,

                _ =>
                    FindXAddButton
            };

        SelectionButtonStyler.Select(
            selectedButton,
            FindXAddButton,
            FindXSubtractButton,
            FindXMultiplyButton,
            FindXDivideButton);

        UpdateFindXForm();
        HideFindXMessages();
    }

    private void OnFindXUnknownLeftClicked(
        object? sender,
        EventArgs e)
    {
        SelectFindXUnknownPosition(
            FindXUnknownPosition.Left);
    }

    private void OnFindXUnknownRightClicked(
        object? sender,
        EventArgs e)
    {
        SelectFindXUnknownPosition(
            FindXUnknownPosition.Right);
    }

    private void SelectFindXUnknownPosition(
        FindXUnknownPosition position)
    {
        _findXUnknownPosition =
            position;

        Button selectedButton =
            position ==
            FindXUnknownPosition.Left
                ? FindXUnknownLeftButton
                : FindXUnknownRightButton;

        SelectionButtonStyler.Select(
            selectedButton,
            FindXUnknownLeftButton,
            FindXUnknownRightButton);

        UpdateFindXForm();
        HideFindXMessages();
    }

    private void OnFindXIntegerTypeClicked(
        object? sender,
        EventArgs e)
    {
        SelectFindXNumberType(
            FindXNumberInputType.Integer,
            clearInputs: true);
    }

    private void OnFindXDecimalTypeClicked(
        object? sender,
        EventArgs e)
    {
        SelectFindXNumberType(
            FindXNumberInputType.Decimal,
            clearInputs: true);
    }

    private void SelectFindXNumberType(
        FindXNumberInputType numberType,
        bool clearInputs)
    {
        _findXNumberType =
            numberType;

        Button selectedButton =
            numberType ==
            FindXNumberInputType.Integer
                ? FindXIntegerTypeButton
                : FindXDecimalTypeButton;

        SelectionButtonStyler.Select(
            selectedButton,
            FindXIntegerTypeButton,
            FindXDecimalTypeButton);

        if (numberType ==
            FindXNumberInputType.Integer)
        {
            FindXKnownValueEntry.Placeholder =
                "Ví dụ: 8";

            FindXResultValueEntry.Placeholder =
                "Ví dụ: 20";
        }
        else
        {
            FindXKnownValueEntry.Placeholder =
                "Ví dụ: 2.5";

            FindXResultValueEntry.Placeholder =
                "Ví dụ: 7.5";
        }

        if (clearInputs)
        {
            _findXScientificCodeValues.Clear();
            _pendingRestoredEntryTexts.Clear();

            SetFindXEntryTextWithoutValidation(
                FindXKnownValueEntry,
                string.Empty);

            SetFindXEntryTextWithoutValidation(
                FindXResultValueEntry,
                string.Empty);

            FindXKnownValueEntry.Focus();
        }

        UpdateFindXEquationPreview();
        HideFindXMessages();
    }

    private void UpdateFindXForm()
    {
        string operationSymbol =
            GetFindXOperationSymbol();

        FindXUnknownLeftButton.Text =
            $"x {operationSymbol} a = b";

        FindXUnknownRightButton.Text =
            $"a {operationSymbol} x = b";

        FindXKnownValueLabel.Text =
            GetFindXKnownValueName();

        FindXResultValueLabel.Text =
            GetFindXResultValueName();

        FindXPositionDescriptionLabel.Text =
            GetFindXPositionDescription();

        UpdateFindXEquationPreview();
    }

    private string GetFindXOperationSymbol()
    {
        return _findXOperation switch
        {
            FindXOperation.Add => "+",
            FindXOperation.Subtract => "−",
            FindXOperation.Multiply => "×",
            FindXOperation.Divide => "÷",
            _ => "+"
        };
    }

    private ArithmeticOperation GetCoreFindXOperation() =>
        _findXOperation switch
        {
            FindXOperation.Add => ArithmeticOperation.Add,
            FindXOperation.Subtract => ArithmeticOperation.Subtract,
            FindXOperation.Multiply => ArithmeticOperation.Multiply,
            FindXOperation.Divide => ArithmeticOperation.Divide,
            _ => throw new ArgumentOutOfRangeException()
        };

    private string GetFindXKnownValueName()
    {
        return _findXOperation switch
        {
            FindXOperation.Add =>
                "Số hạng đã biết",

            FindXOperation.Subtract
                when _findXUnknownPosition ==
                     FindXUnknownPosition.Left =>
                "Số trừ",

            FindXOperation.Subtract =>
                "Số bị trừ",

            FindXOperation.Multiply =>
                "Thừa số đã biết",

            FindXOperation.Divide
                when _findXUnknownPosition ==
                     FindXUnknownPosition.Left =>
                "Số chia",

            FindXOperation.Divide =>
                "Số bị chia",

            _ =>
                "Giá trị đã biết"
        };
    }

    private string GetFindXResultValueName()
    {
        return _findXOperation switch
        {
            FindXOperation.Add => "Tổng",
            FindXOperation.Subtract => "Hiệu",
            FindXOperation.Multiply => "Tích",
            FindXOperation.Divide => "Thương",
            _ => "Kết quả"
        };
    }

    private string GetFindXPositionDescription()
    {
        return _findXOperation switch
        {
            FindXOperation.Add =>
                "Chọn x là số hạng thứ nhất hoặc số hạng thứ hai.",

            FindXOperation.Subtract =>
                "Vị trí của x quyết định x là số bị trừ hay số trừ.",

            FindXOperation.Multiply =>
                "Chọn x là thừa số thứ nhất hoặc thừa số thứ hai.",

            FindXOperation.Divide =>
                "Vị trí của x quyết định x là số bị chia hay số chia.",

            _ =>
                string.Empty
        };
    }

    private void UpdateFindXEquationPreview()
    {
        string knownText =
            string.IsNullOrWhiteSpace(
                FindXKnownValueEntry.Text)
                ? "a"
                : FindXKnownValueEntry.Text!;

        string resultText =
            string.IsNullOrWhiteSpace(
                FindXResultValueEntry.Text)
                ? "b"
                : FindXResultValueEntry.Text!;

        FindXEquationPreviewLabel.Text =
            BuildFindXEquation(
                knownText,
                resultText);
    }

    private string BuildFindXEquation(
        string knownText,
        string resultText)
    {
        string operationSymbol =
            GetFindXOperationSymbol();

        return _findXUnknownPosition ==
               FindXUnknownPosition.Left
            ? $"x {operationSymbol} {knownText} = {resultText}"
            : $"{knownText} {operationSymbol} x = {resultText}";
    }

    private void OnFindXNumberEntryTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string newText =
            e.NewTextValue ??
            string.Empty;

        if (_pendingRestoredEntryTexts.TryGetValue(
                entry,
                out string? restoredText))
        {
            _pendingRestoredEntryTexts.Remove(
                entry);

            if (string.Equals(
                    newText,
                    restoredText,
                    StringComparison.Ordinal))
            {
                return;
            }
        }

        if (_isUpdatingFindXNumberText)
        {
            return;
        }

        _findXScientificCodeValues.Remove(
            entry);

        FindXInputValidationError validationError =
            ValidateFindXInputWhileTyping(
                newText);

        if (validationError !=
            FindXInputValidationError.None)
        {
            string oldText =
                e.OldTextValue ??
                string.Empty;

            string message;

            if (validationError ==
                FindXInputValidationError.OutOfRange)
            {
                message =
                    _findXNumberType ==
                    FindXNumberInputType.Integer
                        ? $"Số nguyên phải nằm trong phạm vi từ {Int128InputRangeText}."
                        : $"Số thập phân phải nằm trong phạm vi từ {DecimalInputRangeText}.";
            }
            else
            {
                message =
                    _findXNumberType ==
                    FindXNumberInputType.Integer
                        ? "Chỉ được nhập số nguyên; không được nhập " +
                          "dấu chấm hoặc ký tự khác."
                        : $"Chỉ được nhập số, một dấu âm ở đầu và " +
                          $"một dấu chấm; tối đa {MaxDecimalPlaces} " +
                          "chữ số sau dấu chấm.";
            }

            ShowFindXError(
                message);

            _pendingRestoredEntryTexts[entry] =
                oldText;

            SetFindXEntryTextWithoutValidation(
                entry,
                oldText);

            UpdateFindXEquationPreview();
            return;
        }

        string formattedText =
            IntegerInputFormatter.FormatWhileTyping(
                newText,
                allowDecimal: true);

        if (!string.Equals(
                formattedText,
                newText,
                StringComparison.Ordinal))
        {
            int oldCursorPosition =
                Math.Clamp(
                    entry.CursorPosition,
                    0,
                    newText.Length);

            int logicalPosition =
                IntegerInputFormatter.CountLogicalCharacters(
                    newText,
                    oldCursorPosition);

            SetFindXEntryTextWithoutValidation(
                entry,
                formattedText,
            IntegerInputFormatter.FindCursorPosition(
                formattedText,
                logicalPosition));
        }

        FindXResultBorder.IsVisible =
            false;

        UpdateFindXEquationPreview();
    }

    private FindXInputValidationError ValidateFindXInputWhileTyping(
        string text)
    {
        if (string.IsNullOrEmpty(
                text))
        {
            return FindXInputValidationError.None;
        }

        string normalizedText =
            text
                .Replace(
                    ",",
                    string.Empty)
                .Replace(
                    '−',
                    '-');

        if (normalizedText.Length == 0)
        {
            return FindXInputValidationError.InvalidFormat;
        }

        int startIndex =
            0;

        if (normalizedText[0] == '-')
        {
            startIndex =
                1;

            if (normalizedText.Length == 1)
            {
                return FindXInputValidationError.None;
            }
        }

        if (_findXNumberType ==
            FindXNumberInputType.Integer)
        {
            int digitCount =
                0;

            for (int index = startIndex;
                 index < normalizedText.Length;
                 index++)
            {
                char character =
                    normalizedText[index];

                if (character < '0' ||
                    character > '9')
                {
                    return FindXInputValidationError.InvalidFormat;
                }

                digitCount++;

                if (digitCount >
                    MaxIntegerInputDigits)
                {
                    return FindXInputValidationError.OutOfRange;
                }
            }

            if (!BigInteger.TryParse(
                    normalizedText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out BigInteger integerValue))
            {
                return FindXInputValidationError.InvalidFormat;
            }

            return integerValue <
                       MinInt128InputValue ||
                   integerValue >
                       MaxInt128InputValue
                ? FindXInputValidationError.OutOfRange
                : FindXInputValidationError.None;
        }

        bool hasDecimalPoint =
            false;

        int decimalDigitCount =
            0;

        int totalDigitCount =
            0;

        for (int index = startIndex;
             index < normalizedText.Length;
             index++)
        {
            char character =
                normalizedText[index];

            if (character >= '0' &&
                character <= '9')
            {
                totalDigitCount++;

                if (hasDecimalPoint)
                {
                    decimalDigitCount++;

                    if (decimalDigitCount >
                        MaxDecimalPlaces)
                    {
                        return FindXInputValidationError.InvalidFormat;
                    }
                }

                continue;
            }

            if (character == '.' &&
                !hasDecimalPoint)
            {
                hasDecimalPoint =
                    true;
                continue;
            }

            return FindXInputValidationError.InvalidFormat;
        }

        if (totalDigitCount == 0)
        {
            return normalizedText is "." or "-."
                ? FindXInputValidationError.None
                : FindXInputValidationError.InvalidFormat;
        }

        string parseText =
            normalizedText[^1] == '.'
                ? normalizedText[..^1]
                : normalizedText;

        if (!decimal.TryParse(
                parseText,
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal decimalValue))
        {
            return FindXInputValidationError.OutOfRange;
        }

        return decimalValue <
                   MinDecimalInputValue ||
               decimalValue >
                   MaxDecimalInputValue
            ? FindXInputValidationError.OutOfRange
            : FindXInputValidationError.None;
    }

    private void SetFindXEntryTextWithoutValidation(
        Entry entry,
        string text,
        int? cursorPosition = null)
    {
        _isUpdatingFindXNumberText =
            true;

        entry.Text =
            text;

        entry.CursorPosition =
            Math.Clamp(
                cursorPosition ??
                text.Length,
                0,
                text.Length);

        entry.SelectionLength =
            0;

        _isUpdatingFindXNumberText =
            false;
    }

    private void OnFindXEntryFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry ||
            !_findXScientificCodeValues.TryGetValue(
                entry,
                out string? scientificCode))
        {
            return;
        }

        if (_findXNumberType ==
            FindXNumberInputType.Integer)
        {
            if (!TryParseFindXInt128(
                    scientificCode,
                    out Int128 integerValue))
            {
                return;
            }

            _findXScientificCodeValues.Remove(
                entry);

            SetFindXEntryTextWithoutValidation(
                entry,
                ((BigInteger)integerValue).ToString(
                    "#,##0",
                    CultureInfo.InvariantCulture));

            UpdateFindXEquationPreview();
            return;
        }

        if (!decimal.TryParse(
                scientificCode,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal decimalValue))
        {
            return;
        }

        _findXScientificCodeValues.Remove(
            entry);

        SetFindXEntryTextWithoutValidation(
            entry,
            FormatFindXDecimalForEditing(
                decimalValue));

        UpdateFindXEquationPreview();
    }

    private void OnFindXEntryUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string normalizedText =
            NormalizeFindXInputText(
                entry.Text);

        if (_findXNumberType ==
            FindXNumberInputType.Integer)
        {
            if (!TryParseFindXInt128(
                    normalizedText,
                    out Int128 integerValue))
            {
                return;
            }

            ApplyFindXIntegerEntryDisplayValue(
                entry,
                integerValue);
        }
        else
        {
            if (!decimal.TryParse(
                    normalizedText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal decimalValue))
            {
                return;
            }

            ApplyFindXDecimalEntryDisplayValue(
                entry,
                decimalValue);
        }

        UpdateFindXEquationPreview();
    }

    private void ApplyFindXIntegerEntryDisplayValue(
        Entry entry,
        Int128 value)
    {
        BigInteger bigValue =
            (BigInteger)value;

        if (CountBigIntegerDigits(
                bigValue) <=
            ScientificDisplayDigitThreshold)
        {
            _findXScientificCodeValues.Remove(
                entry);

            SetFindXEntryTextWithoutValidation(
                entry,
                bigValue.ToString(
                    "#,##0",
                    CultureInfo.InvariantCulture));

            return;
        }

        _findXScientificCodeValues[entry] =
            FormatFindXIntegerScientificForCode(
                bigValue);

        SetFindXEntryTextWithoutValidation(
            entry,
            FormatFindXIntegerForDisplay(
                bigValue));
    }

    private void ApplyFindXDecimalEntryDisplayValue(
        Entry entry,
        decimal value)
    {
        string plainText =
            FormatFindXDecimalForEditing(
                value);

        if (CountIntegerDigits(
                plainText) <=
            ScientificDisplayDigitThreshold)
        {
            _findXScientificCodeValues.Remove(
                entry);

            SetFindXEntryTextWithoutValidation(
                entry,
                plainText);

            return;
        }

        _findXScientificCodeValues[entry] =
            FormatFindXDecimalScientificForCode(
                value);

        SetFindXEntryTextWithoutValidation(
            entry,
            FormatPlainNumberAsScientificDisplay(
                value.ToString(
                    "0.############################",
                    CultureInfo.InvariantCulture)));
    }

    private static string FormatFindXIntegerScientificForCode(
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
            BigInteger.Abs(
                value)
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

    private static string FormatFindXDecimalForEditing(
        decimal value)
    {
        return value.ToString(
            "#,##0.##########",
            CultureInfo.InvariantCulture);
    }

    private static string FormatFindXDecimalForDisplay(
        decimal value)
    {
        string plainText =
            FormatFindXDecimalForEditing(
                value);

        return CountIntegerDigits(
                   plainText) >
               ScientificDisplayDigitThreshold
            ? FormatPlainNumberAsScientificDisplay(
                value.ToString(
                    "0.############################",
                    CultureInfo.InvariantCulture))
            : plainText;
    }

    private static string FormatFindXQuadDoubleForDisplay(
        QuadDouble value)
    {
        if (!value.IsFinite)
        {
            return value.ToGeneralString();
        }

        if (value.IsZero)
        {
            return "0";
        }

        double approximateValue =
            Math.Abs(
                value.ToDouble());

        int exponent =
            (int)Math.Floor(
                Math.Log10(
                    approximateValue));

        bool useScientificNotation =
            exponent >=
                ScientificDisplayDigitThreshold ||
            exponent <=
                -MaxDecimalPlaces;

        int significantDigits =
            useScientificNotation
                ? ScientificDisplaySignificantDigits
                : Math.Clamp(
                    exponent +
                    1 +
                    MaxDecimalPlaces,
                    1,
                    QuadDouble.SignificantDigits);

        string text =
            value.ToGeneralString(
                significantDigits,
                ScientificDisplayDigitThreshold,
                -MaxDecimalPlaces);

        int exponentSeparatorIndex =
            text.IndexOfAny(
                new[] { 'e', 'E' });

        if (exponentSeparatorIndex >= 0)
        {
            string mantissa =
                text[..exponentSeparatorIndex]
                    .Replace(
                        "-",
                        "−",
                        StringComparison.Ordinal);

            if (int.TryParse(
                    text[(exponentSeparatorIndex + 1)..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int scientificExponent))
            {
                return
                    $"{mantissa} × " +
                    $"10{ToSuperscript(scientificExponent)}";
            }
        }

        bool isNegative =
            text.StartsWith(
                "-",
                StringComparison.Ordinal);

        string unsignedText =
            isNegative
                ? text[1..]
                : text;

        int decimalPointIndex =
            unsignedText.IndexOf(
                '.',
                StringComparison.Ordinal);

        string integerPart =
            decimalPointIndex >= 0
                ? unsignedText[..decimalPointIndex]
                : unsignedText;

        string fractionPart =
            decimalPointIndex >= 0
                ? unsignedText[(decimalPointIndex + 1)..]
                : string.Empty;

        integerPart =
            IntegerInputFormatter.AddThousandsSeparators(
                integerPart);

        return
            (isNegative ? "−" : string.Empty) +
            integerPart +
            (fractionPart.Length > 0
                ? $".{fractionPart}"
                : string.Empty);
    }

    private static string FormatFindXDecimalScientificForCode(
        decimal value)
    {
        if (value == 0)
        {
            return "0e0";
        }

        string scientificText =
            value.ToString(
                "0.############################E+0",
                CultureInfo.InvariantCulture);

        int exponentIndex =
            scientificText.IndexOf(
                'E');

        string mantissa =
            scientificText[..exponentIndex];

        string exponent =
            scientificText[(exponentIndex + 1)..]
                .TrimStart(
                    '+');

        return
            $"{mantissa}e{exponent}";
    }

    private void OnFindXCalculateClicked(
        object? sender,
        EventArgs e)
    {
        FindXErrorBorder.IsVisible =
            false;

        if (_findXNumberType ==
            FindXNumberInputType.Integer)
        {
            CalculateFindXInteger();
            return;
        }

        CalculateFindXDecimal();
    }

    private void CalculateFindXInteger()
    {
        if (!TryReadFindXIntegerValue(
                FindXKnownValueEntry,
                GetFindXKnownValueName(),
                out Int128 knownInput))
        {
            FindXKnownValueEntry.Focus();
            return;
        }

        if (!TryReadFindXIntegerValue(
                FindXResultValueEntry,
                GetFindXResultValueName(),
                out Int128 resultInput))
        {
            FindXResultValueEntry.Focus();
            return;
        }

        ApplyFindXIntegerEntryDisplayValue(
            FindXKnownValueEntry,
            knownInput);

        ApplyFindXIntegerEntryDisplayValue(
            FindXResultValueEntry,
            resultInput);

        UpdateFindXEquationPreview();

        BigInteger knownValue =
            (BigInteger)knownInput;

        BigInteger resultValue =
            (BigInteger)resultInput;

        FindXSolution solution =
            SolveFindXInteger(
                knownValue,
                resultValue);

        ShowFindXIntegerSolution(
            knownValue,
            resultValue,
            solution);
    }

    private void CalculateFindXDecimal()
    {
        if (!TryReadFindXDecimalValue(
                FindXKnownValueEntry,
                GetFindXKnownValueName(),
                out decimal knownValue))
        {
            FindXKnownValueEntry.Focus();
            return;
        }

        if (!TryReadFindXDecimalValue(
                FindXResultValueEntry,
                GetFindXResultValueName(),
                out decimal resultValue))
        {
            FindXResultValueEntry.Focus();
            return;
        }

        ApplyFindXDecimalEntryDisplayValue(
            FindXKnownValueEntry,
            knownValue);

        ApplyFindXDecimalEntryDisplayValue(
            FindXResultValueEntry,
            resultValue);

        UpdateFindXEquationPreview();

        FindXDecimalSolution solution =
            SolveFindXDecimal(
                knownValue,
                resultValue);

        ShowFindXDecimalSolution(
            knownValue,
            resultValue,
            solution);
    }

    private bool TryReadFindXIntegerValue(
        Entry entry,
        string fieldName,
        out Int128 value)
    {
        value =
            Int128.Zero;

        bool usesStoredScientificCode =
            _findXScientificCodeValues.TryGetValue(
                entry,
                out string? scientificCode);

        string text =
            usesStoredScientificCode
                ? scientificCode!
                : entry.Text ??
                  string.Empty;

        if (string.IsNullOrWhiteSpace(
                text))
        {
            ShowFindXError(
                $"Vui lòng nhập {fieldName.ToLowerInvariant()}.");

            return false;
        }

        string normalizedText =
            NormalizeFindXInputText(
                text);

        if ((!usesStoredScientificCode &&
             !IsCompleteValidFindXNumber(
                 normalizedText)) ||
            !TryParseFindXInt128(
                normalizedText,
                out value))
        {
            ShowFindXError(
                $"{fieldName} phải là số nguyên hợp lệ trong phạm vi " +
                $"từ {Int128InputRangeText}.");

            return false;
        }

        return true;
    }

    private bool TryReadFindXDecimalValue(
        Entry entry,
        string fieldName,
        out decimal value)
    {
        value =
            0m;

        bool usesStoredScientificCode =
            _findXScientificCodeValues.TryGetValue(
                entry,
                out string? scientificCode);

        string text =
            usesStoredScientificCode
                ? scientificCode!
                : entry.Text ??
                  string.Empty;

        if (string.IsNullOrWhiteSpace(
                text))
        {
            ShowFindXError(
                $"Vui lòng nhập {fieldName.ToLowerInvariant()}.");

            return false;
        }

        string normalizedText =
            NormalizeFindXInputText(
                text);

        if ((!usesStoredScientificCode &&
             !IsCompleteValidFindXNumber(
                 normalizedText)) ||
            !decimal.TryParse(
                normalizedText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
        {
            ShowFindXError(
                $"{fieldName} phải là số thập phân hợp lệ trong phạm vi " +
                $"từ {DecimalInputRangeText}.");

            return false;
        }

        if (value <
                MinDecimalInputValue ||
            value >
                MaxDecimalInputValue)
        {
            ShowFindXError(
                $"{fieldName} phải nằm trong phạm vi từ " +
                $"{DecimalInputRangeText}.");

            value =
                0m;

            return false;
        }

        return true;
    }

    private static bool TryParseFindXInt128(
        string text,
        out Int128 value)
    {
        value =
            Int128.Zero;

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

            return CountFindXInt128Digits(
                       value) <=
                   MaxIntegerInputDigits;
        }

        if (!TryParseFindXScientificInteger(
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

        return CountFindXInt128Digits(
                   value) <=
               MaxIntegerInputDigits;
    }

    private static bool TryParseFindXScientificInteger(
        string text,
        out BigInteger value)
    {
        value =
            BigInteger.Zero;

        int exponentIndex =
            text.IndexOf(
                'e');

        if (exponentIndex <= 0 ||
            exponentIndex !=
            text.LastIndexOf(
                'e') ||
            exponentIndex >=
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
            mantissaText.IndexOf(
                '.');

        if (decimalPointIndex !=
            mantissaText.LastIndexOf(
                '.'))
        {
            return false;
        }

        int decimalPlaces =
            decimalPointIndex < 0
                ? 0
                : mantissaText.Length -
                  decimalPointIndex -
                  1;

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

    private static int CountFindXInt128Digits(
        Int128 value)
    {
        return BigInteger.Abs(
                (BigInteger)value)
            .ToString(
                CultureInfo.InvariantCulture)
            .Length;
    }

    private static string NormalizeFindXInputText(
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

    private bool IsCompleteValidFindXNumber(
        string text)
    {
        if (string.IsNullOrWhiteSpace(
                text) ||
            text is "-" or "." or "-.")
        {
            return false;
        }

        if (_findXNumberType ==
                FindXNumberInputType.Integer &&
            CountDigits(
                text) >
            MaxIntegerInputDigits)
        {
            return false;
        }

        int startIndex =
            text[0] == '-'
                ? 1
                : 0;

        bool hasDecimalPoint =
            false;

        int digitCount =
            0;

        int decimalDigitCount =
            0;

        for (int index = startIndex;
             index < text.Length;
             index++)
        {
            char character =
                text[index];

            if (char.IsDigit(
                    character))
            {
                digitCount++;

                if (hasDecimalPoint)
                {
                    decimalDigitCount++;

                    if (decimalDigitCount >
                        MaxDecimalPlaces)
                    {
                        return false;
                    }
                }

                continue;
            }

            if (_findXNumberType ==
                    FindXNumberInputType.Decimal &&
                character == '.' &&
                !hasDecimalPoint)
            {
                hasDecimalPoint =
                    true;

                continue;
            }

            return false;
        }

        if (digitCount == 0)
        {
            return false;
        }

        if (_findXNumberType ==
            FindXNumberInputType.Decimal)
        {
            return decimal.TryParse(
                text,
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal decimalValue) &&
                   decimalValue >=
                   MinDecimalInputValue &&
                   decimalValue <=
                   MaxDecimalInputValue;
        }

        return true;
    }

    private FindXDecimalSolution SolveFindXDecimal(
        decimal knownValue,
        decimal resultValue)
    {
        FindXDecimalResult coreResult =
            _findXEngine.SolveDecimal(
                knownValue,
                resultValue,
                GetCoreFindXOperation(),
                _findXUnknownPosition ==
                    FindXUnknownPosition.Left);

        string knownText =
            FormatFindXDecimalForDisplay(
                knownValue);

        string resultText =
            FormatFindXDecimalForDisplay(
                resultValue);

        switch (_findXOperation)
        {
            case FindXOperation.Add:
                {
                    QuadDouble x = coreResult.Value;

                    return CreateUniqueFindXDecimalSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm một số hạng chưa biết, ta lấy tổng " +
                        "trừ đi số hạng đã biết.",
                        $"x = {resultText} − {knownText}\n" +
                        $"x = {FormatFindXQuadDoubleForDisplay(x)}");
                }

            case FindXOperation.Subtract
                when _findXUnknownPosition ==
                     FindXUnknownPosition.Left:
                {
                    QuadDouble x = coreResult.Value;

                    return CreateUniqueFindXDecimalSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm số bị trừ, ta lấy hiệu cộng với số trừ.",
                        $"x = {resultText} + {knownText}\n" +
                        $"x = {FormatFindXQuadDoubleForDisplay(x)}");
                }

            case FindXOperation.Subtract:
                {
                    QuadDouble x = coreResult.Value;

                    return CreateUniqueFindXDecimalSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm số trừ, ta lấy số bị trừ trừ đi hiệu.",
                        $"x = {knownText} − {resultText}\n" +
                        $"x = {FormatFindXQuadDoubleForDisplay(x)}");
                }

            case FindXOperation.Multiply:
                {
                    if (knownValue == 0)
                    {
                        if (resultValue == 0)
                        {
                            return new FindXDecimalSolution(
                                FindXSolutionKind.InfiniteSolutions,
                                QuadDouble.Zero,
                                "Vô số nghiệm",
                                "Khi một thừa số bằng 0, tích luôn bằng 0.",
                                "Phương trình trở thành 0 × x = 0.\n" +
                                "Đẳng thức đúng với mọi giá trị của x.",
                                string.Empty);
                        }

                        return new FindXDecimalSolution(
                            FindXSolutionKind.NoSolution,
                            QuadDouble.Zero,
                            "Không có nghiệm",
                            "Không có số nào nhân với 0 mà cho kết quả khác 0.",
                            $"Phương trình trở thành 0 × x = {resultText}.\n" +
                            "Vế trái luôn bằng 0 nên không thể bằng vế phải.",
                            string.Empty);
                    }

                    QuadDouble x = coreResult.Value;

                    return CreateUniqueFindXDecimalSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm một thừa số chưa biết, ta lấy tích " +
                        "chia cho thừa số đã biết.",
                        $"x = {resultText} ÷ {knownText}\n" +
                        $"x = {FormatFindXQuadDoubleForDisplay(x)}");
                }

            case FindXOperation.Divide
                when _findXUnknownPosition ==
                     FindXUnknownPosition.Left:
                {
                    if (knownValue == 0)
                    {
                        return new FindXDecimalSolution(
                            FindXSolutionKind.NoSolution,
                            QuadDouble.Zero,
                            "Phép tính không xác định",
                            "Số chia phải khác 0.",
                            "Phương trình có dạng x ÷ 0 nên phép chia " +
                            "không được xác định.",
                            string.Empty);
                    }

                    QuadDouble x = coreResult.Value;

                    return CreateUniqueFindXDecimalSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm số bị chia, ta lấy thương nhân với số chia.",
                        $"x = {resultText} × {knownText}\n" +
                        $"x = {FormatFindXQuadDoubleForDisplay(x)}");
                }

            case FindXOperation.Divide:
                {
                    if (resultValue == 0)
                    {
                        if (knownValue == 0)
                        {
                            return new FindXDecimalSolution(
                                FindXSolutionKind.InfiniteSolutions,
                                QuadDouble.Zero,
                                "Vô số nghiệm với x ≠ 0",
                                "0 chia cho mọi số khác 0 đều bằng 0.",
                                "Phương trình 0 ÷ x = 0 đúng với mọi x khác 0.",
                                string.Empty);
                        }

                        return new FindXDecimalSolution(
                            FindXSolutionKind.NoSolution,
                            QuadDouble.Zero,
                            "Không có nghiệm",
                            "Một số khác 0 chia cho một số hữu hạn khác 0 " +
                            "không thể bằng 0.",
                            $"{knownText} ÷ x = 0 không có giá trị x hợp lệ.",
                            string.Empty);
                    }

                    if (knownValue == 0)
                    {
                        return new FindXDecimalSolution(
                            FindXSolutionKind.NoSolution,
                            QuadDouble.Zero,
                            "Không có nghiệm",
                            "Số chia x phải khác 0.",
                            $"Từ 0 ÷ x = {resultText}, phép biến đổi hình thức " +
                            "cho x = 0 nhưng x = 0 lại làm phép chia không xác định.",
                            string.Empty);
                    }

                    QuadDouble x = coreResult.Value;

                    return CreateUniqueFindXDecimalSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm số chia, ta lấy số bị chia chia cho thương; " +
                        "đồng thời số chia phải khác 0.",
                        $"x = {knownText} ÷ {resultText}\n" +
                        $"x = {FormatFindXQuadDoubleForDisplay(x)}");
                }

            default:
                return new FindXDecimalSolution(
                    FindXSolutionKind.NoSolution,
                    QuadDouble.Zero,
                    "Không thể giải",
                    string.Empty,
                    string.Empty,
                    string.Empty);
        }
    }

    private FindXDecimalSolution CreateUniqueFindXDecimalSolution(
        QuadDouble x,
        decimal knownValue,
        decimal resultValue,
        string rule,
        string transformation)
    {
        string xText =
            FormatFindXQuadDoubleForDisplay(
                x);

        string verification =
            BuildFindXDecimalVerification(
                x,
                knownValue,
                resultValue);

        return new FindXDecimalSolution(
            FindXSolutionKind.Unique,
            x,
            "Nghiệm duy nhất",
            rule,
            $"Bước 1. Xác định thành phần chưa biết và áp dụng quy tắc.\n\n" +
            $"Bước 2. Thay các giá trị đã biết:\n" +
            $"{transformation}\n\n" +
            $"Vậy x = {xText}.",
            verification);
    }

    private string BuildFindXDecimalVerification(
        QuadDouble x,
        decimal knownValue,
        decimal resultValue)
    {
        QuadDouble leftValue =
            EvaluateFindXDecimalLeftSide(
                x,
                knownValue);

        QuadDouble resultQuadValue =
            QuadDouble.FromDecimal(
                resultValue);

        string xText =
            FormatFindXQuadDoubleForDisplay(
                x);

        string knownText =
            FormatFindXDecimalForDisplay(
                knownValue);

        string resultText =
            FormatFindXDecimalForDisplay(
                resultValue);

        string leftText =
            FormatFindXQuadDoubleForDisplay(
                leftValue);

        bool isExact =
            leftValue ==
            resultQuadValue;

        return
            $"Thay x = {xText} vào phép tính ban đầu:\n" +
            $"{BuildFindXEquation(knownText, resultText).Replace("x", xText, StringComparison.Ordinal)}\n\n" +
            (isExact
                ? $"Vế trái = {leftText}; vế phải = {resultText}.\n" +
                  "Hai vế bằng nhau nên kết quả tìm được là đúng."
                : $"Vế trái ≈ {leftText}; vế phải = {resultText}.");
    }

    private QuadDouble EvaluateFindXDecimalLeftSide(
        QuadDouble x,
        decimal knownValue)
    {
        return _findXEngine.EvaluateDecimalLeftSide(
            x,
            knownValue,
            GetCoreFindXOperation(),
            _findXUnknownPosition ==
                FindXUnknownPosition.Left);
    }

    private void ShowFindXDecimalSolution(
        decimal knownValue,
        decimal resultValue,
        FindXDecimalSolution solution)
    {
        string knownText =
            FormatFindXDecimalForDisplay(
                knownValue);

        string resultText =
            FormatFindXDecimalForDisplay(
                resultValue);

        FindXResultEquationLabel.Text =
            BuildFindXEquation(
                knownText,
                resultText);

        FindXStatusLabel.Text =
            solution.StatusText;

        FindXRuleLabel.Text =
            solution.RuleText;

        FindXSolutionStepsLabel.Text =
            solution.StepsText;

        FindXApproximationLabel.IsVisible =
            false;

        switch (solution.Kind)
        {
            case FindXSolutionKind.Unique:
                FindXAnswerLabel.Text =
                    $"x = {FormatFindXQuadDoubleForDisplay(solution.Value)}";

                FindXStatusLabel.SetDynamicResource(
                    Label.TextColorProperty,
                    "SuccessColor");

                FindXVerificationLabel.Text =
                    solution.VerificationText;

                FindXVerificationBorder.IsVisible =
                    true;
                break;

            case FindXSolutionKind.InfiniteSolutions:
                FindXAnswerLabel.Text =
                    solution.StatusText.Contains(
                        "x ≠ 0",
                        StringComparison.Ordinal)
                        ? "Mọi x khác 0"
                        : "Mọi giá trị của x";

                FindXStatusLabel.SetDynamicResource(
                    Label.TextColorProperty,
                    "WarningColor");

                FindXVerificationBorder.IsVisible =
                    false;
                break;

            default:
                FindXAnswerLabel.Text =
                    "Không tồn tại giá trị x phù hợp";

                FindXStatusLabel.SetDynamicResource(
                    Label.TextColorProperty,
                    "DangerColor");

                FindXVerificationBorder.IsVisible =
                    false;
                break;
        }

        FindXErrorBorder.IsVisible =
            false;

        FindXResultBorder.IsVisible =
            true;
    }

    private FindXSolution SolveFindXInteger(
        BigInteger knownValue,
        BigInteger resultValue)
    {
        FindXIntegerResult coreResult =
            _findXEngine.SolveInteger(
                knownValue,
                resultValue,
                GetCoreFindXOperation(),
                _findXUnknownPosition ==
                    FindXUnknownPosition.Left);

        string knownText =
            FormatFindXIntegerForDisplay(
                knownValue);

        string resultText =
            FormatFindXIntegerForDisplay(
                resultValue);

        switch (_findXOperation)
        {
            case FindXOperation.Add:
                {
                    return CreateUniqueFindXIntegerSolution(
                        coreResult.Numerator,
                        coreResult.Denominator,
                        knownValue,
                        resultValue,
                        "Muốn tìm một số hạng chưa biết, ta lấy tổng " +
                        "trừ đi số hạng đã biết.",
                        $"x = {resultText} − {knownText}\n" +
                        $"x = {FormatFindXValue(coreResult.Numerator, coreResult.Denominator)}");
                }

            case FindXOperation.Subtract
                when _findXUnknownPosition ==
                     FindXUnknownPosition.Left:
                {
                    return CreateUniqueFindXIntegerSolution(
                        coreResult.Numerator,
                        coreResult.Denominator,
                        knownValue,
                        resultValue,
                        "Muốn tìm số bị trừ, ta lấy hiệu cộng với số trừ.",
                        $"x = {resultText} + {knownText}\n" +
                        $"x = {FormatFindXValue(coreResult.Numerator, coreResult.Denominator)}");
                }

            case FindXOperation.Subtract:
                {
                    return CreateUniqueFindXIntegerSolution(
                        coreResult.Numerator,
                        coreResult.Denominator,
                        knownValue,
                        resultValue,
                        "Muốn tìm số trừ, ta lấy số bị trừ trừ đi hiệu.",
                        $"x = {knownText} − {resultText}\n" +
                        $"x = {FormatFindXValue(coreResult.Numerator, coreResult.Denominator)}");
                }

            case FindXOperation.Multiply:
                {
                    if (knownValue.IsZero)
                    {
                        if (resultValue.IsZero)
                        {
                            return new FindXSolution(
                                FindXSolutionKind.InfiniteSolutions,
                                BigInteger.Zero,
                                BigInteger.One,
                                "Vô số nghiệm",
                                "Khi một thừa số bằng 0, tích luôn bằng 0.",
                                "Phương trình trở thành 0 × x = 0.\n" +
                                "Đẳng thức đúng với mọi giá trị của x.",
                                string.Empty);
                        }

                        return new FindXSolution(
                            FindXSolutionKind.NoSolution,
                            BigInteger.Zero,
                            BigInteger.One,
                            "Không có nghiệm",
                            "Không có số nào nhân với 0 mà cho kết quả khác 0.",
                            $"Phương trình trở thành 0 × x = {resultText}.\n" +
                            "Vế trái luôn bằng 0 nên không thể bằng vế phải.",
                            string.Empty);
                    }

                    return CreateUniqueFindXIntegerSolution(
                        coreResult.Numerator,
                        coreResult.Denominator,
                        knownValue,
                        resultValue,
                        "Muốn tìm một thừa số chưa biết, ta lấy tích " +
                        "chia cho thừa số đã biết.",
                        $"x = {resultText} ÷ {knownText}\n" +
                        $"x = {FormatFindXValue(resultValue, knownValue)}");
                }

            case FindXOperation.Divide
                when _findXUnknownPosition ==
                     FindXUnknownPosition.Left:
                {
                    if (knownValue.IsZero)
                    {
                        return new FindXSolution(
                            FindXSolutionKind.NoSolution,
                            BigInteger.Zero,
                            BigInteger.One,
                            "Phép tính không xác định",
                            "Số chia phải khác 0.",
                            "Phương trình có dạng x ÷ 0 nên phép chia " +
                            "không được xác định.",
                            string.Empty);
                    }

                    return CreateUniqueFindXIntegerSolution(
                        coreResult.Numerator,
                        coreResult.Denominator,
                        knownValue,
                        resultValue,
                        "Muốn tìm số bị chia, ta lấy thương nhân với số chia.",
                        $"x = {resultText} × {knownText}\n" +
                        $"x = {FormatFindXValue(coreResult.Numerator, coreResult.Denominator)}");
                }

            case FindXOperation.Divide:
                {
                    // Dạng a ÷ x = b, trong đó x luôn phải khác 0.
                    if (resultValue.IsZero)
                    {
                        if (knownValue.IsZero)
                        {
                            return new FindXSolution(
                                FindXSolutionKind.InfiniteSolutions,
                                BigInteger.Zero,
                                BigInteger.One,
                                "Vô số nghiệm với x ≠ 0",
                                "0 chia cho mọi số khác 0 đều bằng 0.",
                                "Phương trình 0 ÷ x = 0 đúng với mọi x khác 0.",
                                string.Empty);
                        }

                        return new FindXSolution(
                            FindXSolutionKind.NoSolution,
                            BigInteger.Zero,
                            BigInteger.One,
                            "Không có nghiệm",
                            "Một số khác 0 chia cho một số hữu hạn khác 0 " +
                            "không thể bằng 0.",
                            $"{knownText} ÷ x = 0 không có giá trị x hợp lệ.",
                            string.Empty);
                    }

                    if (knownValue.IsZero)
                    {
                        return new FindXSolution(
                            FindXSolutionKind.NoSolution,
                            BigInteger.Zero,
                            BigInteger.One,
                            "Không có nghiệm",
                            "Số chia x phải khác 0.",
                            $"Từ 0 ÷ x = {resultText}, phép biến đổi hình thức " +
                            "cho x = 0 nhưng x = 0 lại làm phép chia không xác định.",
                            string.Empty);
                    }

                    return CreateUniqueFindXIntegerSolution(
                        coreResult.Numerator,
                        coreResult.Denominator,
                        knownValue,
                        resultValue,
                        "Muốn tìm số chia, ta lấy số bị chia chia cho thương; " +
                        "đồng thời số chia phải khác 0.",
                        $"x = {knownText} ÷ {resultText}\n" +
                        $"x = {FormatFindXValue(knownValue, resultValue)}");
                }

            default:
                return new FindXSolution(
                    FindXSolutionKind.NoSolution,
                    BigInteger.Zero,
                    BigInteger.One,
                    "Không thể giải",
                    string.Empty,
                    string.Empty,
                    string.Empty);
        }
    }

    private FindXSolution CreateUniqueFindXIntegerSolution(
        BigInteger numerator,
        BigInteger denominator,
        BigInteger knownValue,
        BigInteger resultValue,
        string rule,
        string transformation)
    {
        (numerator, denominator) =
            NormalizeFindXFraction(
                numerator,
                denominator);

        string xText =
            FormatFindXValue(
                numerator,
                denominator);

        string verification =
            BuildFindXIntegerVerification(
                numerator,
                denominator,
                knownValue,
                resultValue);

        return new FindXSolution(
            FindXSolutionKind.Unique,
            numerator,
            denominator,
            "Nghiệm duy nhất",
            rule,
            $"Bước 1. Xác định thành phần chưa biết và áp dụng quy tắc.\n\n" +
            $"Bước 2. Thay các giá trị đã biết:\n" +
            $"{transformation}\n\n" +
            $"Vậy x = {xText}.",
            verification);
    }

    private string BuildFindXIntegerVerification(
        BigInteger numerator,
        BigInteger denominator,
        BigInteger knownValue,
        BigInteger resultValue)
    {
        (BigInteger leftNumerator, BigInteger leftDenominator) =
            EvaluateFindXIntegerLeftSide(
                numerator,
                denominator,
                knownValue);

        string xText =
            FormatFindXValue(
                numerator,
                denominator);

        string knownText =
            FormatFindXIntegerForDisplay(
                knownValue);

        string resultText =
            FormatFindXIntegerForDisplay(
                resultValue);

        string leftText =
            FormatFindXValue(
                leftNumerator,
                leftDenominator);

        return
            $"Thay x = {xText} vào phép tính ban đầu:\n" +
            $"{BuildFindXEquation(knownText, resultText).Replace("x", xText, StringComparison.Ordinal)}\n\n" +
            $"Vế trái = {leftText}; vế phải = {resultText}.\n" +
            "Hai vế bằng nhau nên kết quả tìm được là đúng.";
    }

    private (BigInteger Numerator, BigInteger Denominator)
        EvaluateFindXIntegerLeftSide(
            BigInteger numerator,
            BigInteger denominator,
            BigInteger knownValue)
    {
        return _findXEngine.EvaluateIntegerLeftSide(
            numerator,
            denominator,
            knownValue,
            GetCoreFindXOperation(),
            _findXUnknownPosition ==
                FindXUnknownPosition.Left);
    }

    private void ShowFindXIntegerSolution(
        BigInteger knownValue,
        BigInteger resultValue,
        FindXSolution solution)
    {
        string knownText =
            FormatFindXIntegerForDisplay(
                knownValue);

        string resultText =
            FormatFindXIntegerForDisplay(
                resultValue);

        FindXResultEquationLabel.Text =
            BuildFindXEquation(
                knownText,
                resultText);

        FindXStatusLabel.Text =
            solution.StatusText;

        FindXRuleLabel.Text =
            solution.RuleText;

        FindXSolutionStepsLabel.Text =
            solution.StepsText;

        FindXApproximationLabel.IsVisible =
            false;

        switch (solution.Kind)
        {
            case FindXSolutionKind.Unique:
                {
                    string xText =
                        FormatFindXValue(
                            solution.Numerator,
                            solution.Denominator);

                    FindXAnswerLabel.Text =
                        $"x = {xText}";

                    FindXStatusLabel.SetDynamicResource(
                        Label.TextColorProperty,
                        "SuccessColor");

                    FindXVerificationLabel.Text =
                        solution.VerificationText;

                    FindXVerificationBorder.IsVisible =
                        true;

                    string? approximation =
                        CreateFindXApproximation(
                            solution.Numerator,
                            solution.Denominator);

                    if (!string.IsNullOrEmpty(
                            approximation) &&
                        !string.Equals(
                            approximation,
                            xText,
                            StringComparison.Ordinal))
                    {
                        FindXApproximationLabel.Text =
                            $"Giá trị gần đúng: {approximation}";

                        FindXApproximationLabel.IsVisible =
                            true;
                    }

                    break;
                }

            case FindXSolutionKind.InfiniteSolutions:
                FindXAnswerLabel.Text =
                    solution.StatusText.Contains(
                        "x ≠ 0",
                        StringComparison.Ordinal)
                        ? "Mọi x khác 0"
                        : "Mọi giá trị của x";

                FindXStatusLabel.SetDynamicResource(
                    Label.TextColorProperty,
                    "WarningColor");

                FindXVerificationBorder.IsVisible =
                    false;
                break;

            default:
                FindXAnswerLabel.Text =
                    "Không tồn tại giá trị x phù hợp";

                FindXStatusLabel.SetDynamicResource(
                    Label.TextColorProperty,
                    "DangerColor");

                FindXVerificationBorder.IsVisible =
                    false;
                break;
        }

        FindXErrorBorder.IsVisible =
            false;

        FindXResultBorder.IsVisible =
            true;
    }

    private static string FormatFindXValue(
        BigInteger numerator,
        BigInteger denominator)
    {
        (numerator, denominator) =
            NormalizeFindXFraction(
                numerator,
                denominator);

        if (denominator.IsOne)
        {
            return FormatFindXIntegerForDisplay(
                numerator);
        }

        if (TryFormatTerminatingFindXDecimal(
                numerator,
                denominator,
                out string decimalText))
        {
            return ShouldUseFindXScientificDisplay(
                    numerator,
                    denominator)
                ? FormatFindXScientificForDisplay(
                    numerator,
                    denominator)
                : decimalText;
        }

        return
            $"{FormatFindXIntegerForDisplay(numerator)}/" +
            $"{FormatFindXIntegerForDisplay(denominator)}";
    }

    private static bool ShouldUseFindXScientificDisplay(
        BigInteger numerator,
        BigInteger denominator)
    {
        if (denominator.IsOne)
        {
            return CountBigIntegerDigits(
                       numerator) >
                   ScientificDisplayDigitThreshold;
        }

        if (!TryGetFindXPlainNumberText(
                numerator,
                denominator,
                out string plainText))
        {
            return false;
        }

        return CountSignificantDigits(
                   plainText) >
               ScientificDisplayDigitThreshold;
    }

    private static string FormatFindXIntegerForDisplay(
        BigInteger value)
    {
        if (CountBigIntegerDigits(
                value) <=
            ScientificDisplayDigitThreshold)
        {
            return value.ToString(
                "#,##0",
                CultureInfo.InvariantCulture);
        }

        return FormatPlainNumberAsScientificDisplay(
            value.ToString(
                CultureInfo.InvariantCulture));
    }

    private static string FormatFindXScientificForDisplay(
        BigInteger numerator,
        BigInteger denominator)
    {
        if (!TryGetFindXPlainNumberText(
                numerator,
                denominator,
                out string plainText))
        {
            return
                $"{FormatFindXIntegerForDisplay(numerator)}/" +
                $"{FormatFindXIntegerForDisplay(denominator)}";
        }

        return FormatPlainNumberAsScientificDisplay(
            plainText);
    }

    private static string FormatPlainNumberAsScientificDisplay(
        string plainText)
    {
        if (!TryBuildScientificParts(
                plainText,
                out bool isNegative,
                out string significantDigits,
                out int exponent))
        {
            return plainText;
        }

        if (significantDigits == "0")
        {
            return "0";
        }

        int keptDigitCount =
            Math.Min(
                ScientificDisplaySignificantDigits,
                significantDigits.Length);

        string keptDigits =
            significantDigits[..keptDigitCount];

        bool wasShortened =
            significantDigits.Length >
            keptDigitCount &&
            significantDigits[keptDigitCount..]
            .Any(
                character =>
                    character != '0');

        string mantissa =
            keptDigits.Length == 1
                ? keptDigits
                : $"{keptDigits[0]}.{keptDigits[1..]}"
                    .TrimEnd('0')
                    .TrimEnd('.');

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
            $"{approximation}{sign}{mantissa} × 10" +
            ToSuperscript(
                exponent);
    }

    private static bool TryGetFindXPlainNumberText(
        BigInteger numerator,
        BigInteger denominator,
        out string plainText)
    {
        plainText =
            string.Empty;

        if (denominator.IsOne)
        {
            plainText =
                numerator.ToString(
                    CultureInfo.InvariantCulture);

            return true;
        }

        if (!TryFormatTerminatingFindXDecimal(
                numerator,
                denominator,
                out string decimalText))
        {
            return false;
        }

        plainText =
            decimalText
            .Replace(
                ",",
                string.Empty)
            .Replace(
                '−',
                '-');

        return true;
    }

    private static bool TryBuildScientificParts(
        string plainText,
        out bool isNegative,
        out string significantDigits,
        out int exponent)
    {
        isNegative =
            false;

        significantDigits =
            "0";

        exponent =
            0;

        string normalizedText =
            plainText
            .Trim()
            .Replace(
                ",",
                string.Empty)
            .Replace(
                '−',
                '-');

        if (normalizedText.Length == 0)
        {
            return false;
        }

        isNegative =
            normalizedText[0] == '-';

        if (isNegative)
        {
            normalizedText =
                normalizedText[1..];
        }

        int decimalPointIndex =
            normalizedText.IndexOf('.');

        if (decimalPointIndex !=
            normalizedText.LastIndexOf('.'))
        {
            return false;
        }

        string integerPart =
            decimalPointIndex < 0
                ? normalizedText
                : normalizedText[..decimalPointIndex];

        string decimalPart =
            decimalPointIndex < 0
                ? string.Empty
                : normalizedText[(decimalPointIndex + 1)..];

        if ((integerPart.Length == 0 &&
             decimalPart.Length == 0) ||
            !integerPart.All(
                char.IsDigit) ||
            !decimalPart.All(
                char.IsDigit))
        {
            return false;
        }

        string allDigits =
            integerPart +
            decimalPart;

        int firstNonZeroIndex =
            -1;

        for (int index = 0;
             index < allDigits.Length;
             index++)
        {
            if (allDigits[index] != '0')
            {
                firstNonZeroIndex =
                    index;

                break;
            }
        }

        if (firstNonZeroIndex < 0)
        {
            significantDigits =
                "0";

            return true;
        }

        significantDigits =
            allDigits[firstNonZeroIndex..]
            .TrimEnd('0');

        if (significantDigits.Length == 0)
        {
            significantDigits =
                "0";

            return true;
        }

        exponent =
            integerPart.Length -
            firstNonZeroIndex -
            1;

        return true;
    }

    private static int CountSignificantDigits(
        string plainText)
    {
        if (!TryBuildScientificParts(
                plainText,
                out _,
                out string significantDigits,
                out _))
        {
            return 0;
        }

        return significantDigits == "0"
            ? 1
            : significantDigits.Length;
    }

    private static int CountIntegerDigits(
        string plainText)
    {
        string normalizedText =
            plainText
            .Trim()
            .Replace(
                ",",
                string.Empty)
            .Replace(
                '−',
                '-');

        if (normalizedText.StartsWith(
                "-",
                StringComparison.Ordinal))
        {
            normalizedText =
                normalizedText[1..];
        }

        int decimalPointIndex =
            normalizedText.IndexOf('.');

        string integerPart =
            decimalPointIndex >= 0
                ? normalizedText[..decimalPointIndex]
                : normalizedText;

        integerPart =
            integerPart.TrimStart('0');

        return integerPart.Length == 0
            ? 1
            : integerPart.Length;
    }

    private static int CountBigIntegerDigits(
        BigInteger value)
    {
        return BigInteger.Abs(
                value)
            .ToString(
                CultureInfo.InvariantCulture)
            .Length;
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

    private static (BigInteger Numerator, BigInteger Denominator)
        NormalizeFindXFraction(
            BigInteger numerator,
            BigInteger denominator)
    {
        return FindXEngine.NormalizeFraction(
            numerator,
            denominator);
    }

    private static bool TryFormatTerminatingFindXDecimal(
        BigInteger numerator,
        BigInteger denominator,
        out string text)
    {
        text =
            string.Empty;

        (numerator, denominator) =
            NormalizeFindXFraction(
                numerator,
                denominator);

        int factorTwoCount =
            0;

        int factorFiveCount =
            0;

        while (denominator % 2 == 0)
        {
            denominator /=
                2;

            factorTwoCount++;
        }

        while (denominator % 5 == 0)
        {
            denominator /=
                5;

            factorFiveCount++;
        }

        if (!denominator.IsOne)
        {
            return false;
        }

        int scale =
            Math.Max(
                factorTwoCount,
                factorFiveCount);

        if (scale >
            MaxDecimalPlaces)
        {
            return false;
        }

        BigInteger scaledNumerator =
            BigInteger.Abs(
                numerator);

        if (scale >
            factorTwoCount)
        {
            scaledNumerator *=
                BigInteger.Pow(
                    2,
                    scale -
                    factorTwoCount);
        }

        if (scale >
            factorFiveCount)
        {
            scaledNumerator *=
                BigInteger.Pow(
                    5,
                    scale -
                    factorFiveCount);
        }

        string digits =
            scaledNumerator.ToString(
                CultureInfo.InvariantCulture);

        if (scale == 0)
        {
            text =
                numerator.Sign < 0
                    ? $"−{IntegerInputFormatter.AddThousandsSeparators(digits)}"
                    : IntegerInputFormatter.AddThousandsSeparators(digits);

            return true;
        }

        digits =
            digits.PadLeft(
                scale + 1,
                '0');

        string integerPart =
            digits[..^scale];

        string decimalPart =
            digits[^scale..]
                .TrimEnd('0');

        string sign =
            numerator.Sign < 0
                ? "−"
                : string.Empty;

        text =
            decimalPart.Length == 0
                ? $"{sign}{IntegerInputFormatter.AddThousandsSeparators(integerPart)}"
                : $"{sign}{IntegerInputFormatter.AddThousandsSeparators(integerPart)}.{decimalPart}";

        return true;
    }

    private static string? CreateFindXApproximation(
        BigInteger numerator,
        BigInteger denominator)
    {
        (numerator, denominator) =
            NormalizeFindXFraction(
                numerator,
                denominator);

        if (denominator.IsOne ||
            TryFormatTerminatingFindXDecimal(
                numerator,
                denominator,
                out _))
        {
            return null;
        }

        QuadDouble approximation =
            ConvertFindXBigIntegerToQuadDouble(
                numerator) /
            ConvertFindXBigIntegerToQuadDouble(
                denominator);

        return approximation.IsFinite
            ? FormatFindXQuadDoubleForDisplay(
                approximation)
            : null;
    }

    private static QuadDouble ConvertFindXBigIntegerToQuadDouble(
        BigInteger value)
    {
        if (value.IsZero)
        {
            return QuadDouble.Zero;
        }

        bool isNegative =
            value.Sign < 0;

        BigInteger magnitude =
            BigInteger.Abs(
                value);

        const uint ChunkBase =
            1_000_000_000;

        Span<uint> chunks =
            stackalloc uint[16];

        int chunkCount =
            0;

        while (!magnitude.IsZero)
        {
            magnitude =
                BigInteger.DivRem(
                    magnitude,
                    ChunkBase,
                    out BigInteger remainder);

            chunks[chunkCount++] =
                (uint)remainder;
        }

        QuadDouble result =
            QuadDouble.Zero;

        QuadDouble quadChunkBase =
            new(
                ChunkBase);

        for (int index = chunkCount - 1;
             index >= 0;
             index--)
        {
            result =
                result *
                quadChunkBase +
                new QuadDouble(
                    chunks[index]);
        }

        return isNegative
            ? -result
            : result;
    }

    private async void OnFindXCopyResultClicked(
        object? sender,
        EventArgs e)
    {
        string resultText =
            FindXAnswerLabel.Text ??
            string.Empty;

        if (FindXApproximationLabel.IsVisible &&
            !string.IsNullOrWhiteSpace(
                FindXApproximationLabel.Text))
        {
            resultText +=
                Environment.NewLine +
                FindXApproximationLabel.Text;
        }

        await ResultClipboardService.CopyAsync(
            FindXCopyResultButton,
            resultText);
    }

    private void OnFindXClearClicked(
        object? sender,
        EventArgs e)
    {
        _pendingRestoredEntryTexts.Clear();
        _findXScientificCodeValues.Clear();

        SetFindXEntryTextWithoutValidation(
            FindXKnownValueEntry,
            string.Empty);

        SetFindXEntryTextWithoutValidation(
            FindXResultValueEntry,
            string.Empty);

        HideFindXMessages();
        UpdateFindXEquationPreview();

        FindXKnownValueEntry.Focus();
    }

    private void ShowFindXError(
        string message)
    {
        FindXErrorLabel.Text =
            message;

        FindXErrorBorder.IsVisible =
            true;

        FindXResultBorder.IsVisible =
            false;
    }

    private void HideFindXMessages()
    {
        FindXErrorBorder.IsVisible =
            false;

        FindXResultBorder.IsVisible =
            false;
    }

    private enum FindXOperation
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }

    private enum FindXUnknownPosition
    {
        Left,
        Right
    }

    private enum FindXSolutionKind
    {
        Unique,
        NoSolution,
        InfiniteSolutions
    }

    private sealed record FindXSolution(
        FindXSolutionKind Kind,
        BigInteger Numerator,
        BigInteger Denominator,
        string StatusText,
        string RuleText,
        string StepsText,
        string VerificationText);

    private sealed record FindXDecimalSolution(
        FindXSolutionKind Kind,
        QuadDouble Value,
        string StatusText,
        string RuleText,
        string StepsText,
        string VerificationText);

    private static int CountDigits(
        string text)
    {
        int digitCount = 0;

        foreach (char character in text)
        {
            if (char.IsDigit(character))
            {
                digitCount++;
            }
        }

        return digitCount;
    }

    private enum FindXNumberInputType
    {
        Integer,
        Decimal
    }

    #endregion
}
