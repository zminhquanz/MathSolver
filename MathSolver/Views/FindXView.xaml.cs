using MathSolver.Services;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace MathSolver.Views;

public partial class FindXView : ContentView
{
    private const int MaxInputSignificantDigits = 28;
    private const int MaxDecimalPlaces = 10;

    private FindXOperation _findXOperation = FindXOperation.Add;
    private FindXUnknownPosition _findXUnknownPosition = FindXUnknownPosition.Left;
    private FindXNumberInputType _findXNumberType = FindXNumberInputType.Integer;
    private bool _isUpdatingFindXNumberText;
    // Responsive bằng code-behind; không dùng VisualStateManager trong XAML.
    private bool? _isCompactInputLayout;

    // Đồng bộ chiều rộng nội dung với tab Cơ bản và tab Phân số.
    // MaximumWidthRequest trong XAML chỉ giới hạn chiều rộng tối đa,
    // không bắt layout phải mở rộng đến đúng kích thước này.
    private const double FindXMaximumContentWidth = 1120d;

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

        LocalizationService.Attach(
            this);

        FindXContent.WidthRequest =
            FindXMaximumContentWidth;

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

        UpdateFindXContentWidth(
            width);

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

    private void UpdateFindXContentWidth(
        double availableWidth)
    {
        double targetWidth =
            Math.Min(
                FindXMaximumContentWidth,
                availableWidth);

        if (targetWidth <= 0 ||
            Math.Abs(
                FindXContent.WidthRequest -
                targetWidth) <
            0.5)
        {
            return;
        }

        FindXContent.WidthRequest =
            targetWidth;
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

        ResetFindXOperationButtonStyles();

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

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");

        UpdateFindXForm();
        HideFindXMessages();
    }

    private void ResetFindXOperationButtonStyles()
    {
        Button[] buttons =
        [
            FindXAddButton,
            FindXSubtractButton,
            FindXMultiplyButton,
            FindXDivideButton
        ];

        foreach (Button button in buttons)
        {
            button.SetDynamicResource(
                Button.BackgroundColorProperty,
                "SurfaceAltColor");

            button.SetDynamicResource(
                Button.TextColorProperty,
                "TextPrimaryColor");
        }
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

        ResetFindXPositionButtonStyles();

        Button selectedButton =
            position ==
            FindXUnknownPosition.Left
                ? FindXUnknownLeftButton
                : FindXUnknownRightButton;

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");

        UpdateFindXForm();
        HideFindXMessages();
    }

    private void ResetFindXPositionButtonStyles()
    {
        Button[] buttons =
        [
            FindXUnknownLeftButton,
            FindXUnknownRightButton
        ];

        foreach (Button button in buttons)
        {
            button.SetDynamicResource(
                Button.BackgroundColorProperty,
                "SurfaceAltColor");

            button.SetDynamicResource(
                Button.TextColorProperty,
                "TextPrimaryColor");
        }
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

        Button[] buttons =
        [
            FindXIntegerTypeButton,
            FindXDecimalTypeButton
        ];

        foreach (Button button in buttons)
        {
            button.SetDynamicResource(
                Button.BackgroundColorProperty,
                "SurfaceAltColor");

            button.SetDynamicResource(
                Button.TextColorProperty,
                "TextPrimaryColor");
        }

        Button selectedButton =
            numberType ==
            FindXNumberInputType.Integer
                ? FindXIntegerTypeButton
                : FindXDecimalTypeButton;

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");

        if (numberType ==
            FindXNumberInputType.Integer)
        {
            FindXNumberTypeDescriptionLabel.Text =
                "Nhập các giá trị đã biết bằng số nguyên. " +
                "Nếu x không phải số nguyên, ứng dụng vẫn giữ kết quả " +
                "chính xác dưới dạng phân số.";

            FindXKnownValueEntry.Placeholder =
                "Ví dụ: 8";

            FindXResultValueEntry.Placeholder =
                "Ví dụ: 20";
        }
        else
        {
            FindXNumberTypeDescriptionLabel.Text =
                $"Dùng dấu chấm cho phần thập phân, tối đa " +
                $"{MaxDecimalPlaces} chữ số sau dấu chấm. " +
                "Kết quả được xử lý chính xác bằng phân số nội bộ.";

            FindXKnownValueEntry.Placeholder =
                "Ví dụ: 2.5";

            FindXResultValueEntry.Placeholder =
                "Ví dụ: 7.5";
        }

        if (clearInputs)
        {
            _findXScientificCodeValues.Clear();

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
        if (_isUpdatingFindXNumberText ||
            sender is not Entry entry)
        {
            return;
        }

        _findXScientificCodeValues.Remove(
            entry);

        string newText =
            e.NewTextValue ??
            string.Empty;

        if (!IsValidFindXInputWhileTyping(
                newText))
        {
            SetFindXEntryTextWithoutValidation(
                entry,
                e.OldTextValue ??
                string.Empty);

            ShowFindXError(
                _findXNumberType ==
                FindXNumberInputType.Integer
                    ? $"Chỉ được nhập số nguyên, tối đa " +
                      $"{MaxInputSignificantDigits} chữ số; " +
                      "không được nhập dấu chấm hoặc ký tự khác."
                    : $"Chỉ được nhập số, một dấu âm ở đầu và " +
                      $"một dấu chấm; tối đa {MaxDecimalPlaces} " +
                      $"chữ số sau dấu chấm.");

            UpdateFindXEquationPreview();
            return;
        }

        string formattedText =
            FormatNumberWhileTyping(
                newText);

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
                CountLogicalCharacters(
                    newText,
                    oldCursorPosition);

            SetFindXEntryTextWithoutValidation(
                entry,
                formattedText,
                FindCursorPosition(
                    formattedText,
                    logicalPosition));
        }

        // Dữ liệu đã thay đổi nên kết quả cũ không còn hiệu lực.
        FindXResultBorder.IsVisible =
            false;

        UpdateFindXEquationPreview();
    }

    private bool IsValidFindXInputWhileTyping(
        string text)
    {
        if (string.IsNullOrEmpty(
                text))
        {
            return true;
        }

        string normalizedText =
            text
                .Replace(
                    ",",
                    string.Empty)
                .Replace(
                    '−',
                    '-');

        if (normalizedText.Length == 0 ||
            CountDigits(
                normalizedText) >
            MaxInputSignificantDigits)
        {
            return false;
        }

        int startIndex =
            0;

        if (normalizedText[0] == '-')
        {
            startIndex =
                1;

            if (normalizedText.Length == 1)
            {
                return true;
            }
        }

        bool hasDecimalPoint =
            false;

        int decimalDigitCount =
            0;

        for (int index = startIndex;
             index < normalizedText.Length;
             index++)
        {
            char character =
                normalizedText[index];

            if (char.IsDigit(
                    character))
            {
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

        return true;
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
                out string? scientificCode) ||
            !FindXRational.TryParse(
                scientificCode,
                out FindXRational value))
        {
            return;
        }

        _findXScientificCodeValues.Remove(
            entry);

        SetFindXEntryTextWithoutValidation(
            entry,
            FormatFindXValueForEditing(
                value));

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

        if (!IsCompleteValidFindXNumber(
                normalizedText) ||
            !FindXRational.TryParse(
                normalizedText,
                out FindXRational value))
        {
            return;
        }

        ApplyFindXEntryDisplayValue(
            entry,
            value);

        UpdateFindXEquationPreview();
    }

    private void ApplyFindXEntryDisplayValue(
        Entry entry,
        FindXRational value)
    {
        if (!ShouldUseFindXScientificDisplay(
                value))
        {
            _findXScientificCodeValues.Remove(
                entry);

            SetFindXEntryTextWithoutValidation(
                entry,
                FormatFindXValueForEditing(
                    value));

            return;
        }

        _findXScientificCodeValues[entry] =
            FormatFindXScientificForCode(
                value);

        SetFindXEntryTextWithoutValidation(
            entry,
            FormatFindXScientificForDisplay(
                value));
    }

    private static string FormatFindXValueForEditing(
        FindXRational value)
    {
        if (value.Denominator.IsOne)
        {
            return value.Numerator.ToString(
                "#,##0",
                CultureInfo.InvariantCulture);
        }

        if (TryFormatTerminatingFindXDecimal(
                value,
                out string decimalText))
        {
            return decimalText;
        }

        return
            $"{value.Numerator.ToString("#,##0", CultureInfo.InvariantCulture)}/" +
            $"{value.Denominator.ToString("#,##0", CultureInfo.InvariantCulture)}";
    }

    private void OnFindXCalculateClicked(
        object? sender,
        EventArgs e)
    {
        FindXErrorBorder.IsVisible =
            false;

        if (!TryReadFindXValue(
                FindXKnownValueEntry,
                GetFindXKnownValueName(),
                out FindXRational knownValue))
        {
            FindXKnownValueEntry.Focus();
            return;
        }

        if (!TryReadFindXValue(
                FindXResultValueEntry,
                GetFindXResultValueName(),
                out FindXRational resultValue))
        {
            FindXResultValueEntry.Focus();
            return;
        }

        ApplyFindXEntryDisplayValue(
            FindXKnownValueEntry,
            knownValue);

        ApplyFindXEntryDisplayValue(
            FindXResultValueEntry,
            resultValue);

        UpdateFindXEquationPreview();

        FindXSolution solution =
            SolveFindX(
                knownValue,
                resultValue);

        ShowFindXSolution(
            knownValue,
            resultValue,
            solution);
    }

    private bool TryReadFindXValue(
        Entry entry,
        string fieldName,
        out FindXRational value)
    {
        value =
            FindXRational.Zero;

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
            !FindXRational.TryParse(
                normalizedText,
                out value))
        {
            ShowFindXError(
                $"{fieldName} không phải là một số hợp lệ.");

            return false;
        }

        return true;
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

        if (CountDigits(
                text) >
            MaxInputSignificantDigits)
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

        return digitCount > 0;
    }

    private FindXSolution SolveFindX(
        FindXRational knownValue,
        FindXRational resultValue)
    {
        string knownText =
            FormatFindXValue(
                knownValue);

        string resultText =
            FormatFindXValue(
                resultValue);

        switch (_findXOperation)
        {
            case FindXOperation.Add:
                {
                    FindXRational x =
                        resultValue -
                        knownValue;

                    return CreateUniqueFindXSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm một số hạng chưa biết, ta lấy tổng " +
                        "trừ đi số hạng đã biết.",
                        $"x = {resultText} − {knownText}\n" +
                        $"x = {FormatFindXValue(x)}");
                }

            case FindXOperation.Subtract
                when _findXUnknownPosition ==
                     FindXUnknownPosition.Left:
                {
                    FindXRational x =
                        resultValue +
                        knownValue;

                    return CreateUniqueFindXSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm số bị trừ, ta lấy hiệu cộng với số trừ.",
                        $"x = {resultText} + {knownText}\n" +
                        $"x = {FormatFindXValue(x)}");
                }

            case FindXOperation.Subtract:
                {
                    FindXRational x =
                        knownValue -
                        resultValue;

                    return CreateUniqueFindXSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm số trừ, ta lấy số bị trừ trừ đi hiệu.",
                        $"x = {knownText} − {resultText}\n" +
                        $"x = {FormatFindXValue(x)}");
                }

            case FindXOperation.Multiply:
                {
                    if (knownValue.IsZero)
                    {
                        if (resultValue.IsZero)
                        {
                            return new FindXSolution(
                                FindXSolutionKind.InfiniteSolutions,
                                FindXRational.Zero,
                                "Vô số nghiệm",
                                "Khi một thừa số bằng 0, tích luôn bằng 0.",
                                "Phương trình trở thành 0 × x = 0.\n" +
                                "Đẳng thức đúng với mọi giá trị của x.",
                                string.Empty);
                        }

                        return new FindXSolution(
                            FindXSolutionKind.NoSolution,
                            FindXRational.Zero,
                            "Không có nghiệm",
                            "Không có số nào nhân với 0 mà cho kết quả khác 0.",
                            $"Phương trình trở thành 0 × x = {resultText}.\n" +
                            "Vế trái luôn bằng 0 nên không thể bằng vế phải.",
                            string.Empty);
                    }

                    FindXRational x =
                        resultValue /
                        knownValue;

                    return CreateUniqueFindXSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm một thừa số chưa biết, ta lấy tích " +
                        "chia cho thừa số đã biết.",
                        $"x = {resultText} ÷ {knownText}\n" +
                        $"x = {FormatFindXValue(x)}");
                }

            case FindXOperation.Divide
                when _findXUnknownPosition ==
                     FindXUnknownPosition.Left:
                {
                    if (knownValue.IsZero)
                    {
                        return new FindXSolution(
                            FindXSolutionKind.NoSolution,
                            FindXRational.Zero,
                            "Phép tính không xác định",
                            "Số chia phải khác 0.",
                            "Phương trình có dạng x ÷ 0 nên phép chia " +
                            "không được xác định.",
                            string.Empty);
                    }

                    FindXRational x =
                        resultValue *
                        knownValue;

                    return CreateUniqueFindXSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm số bị chia, ta lấy thương nhân với số chia.",
                        $"x = {resultText} × {knownText}\n" +
                        $"x = {FormatFindXValue(x)}");
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
                                FindXRational.Zero,
                                "Vô số nghiệm với x ≠ 0",
                                "0 chia cho mọi số khác 0 đều bằng 0.",
                                "Phương trình 0 ÷ x = 0 đúng với mọi x khác 0.",
                                string.Empty);
                        }

                        return new FindXSolution(
                            FindXSolutionKind.NoSolution,
                            FindXRational.Zero,
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
                            FindXRational.Zero,
                            "Không có nghiệm",
                            "Số chia x phải khác 0.",
                            $"Từ 0 ÷ x = {resultText}, phép biến đổi hình thức " +
                            "cho x = 0 nhưng x = 0 lại làm phép chia không xác định.",
                            string.Empty);
                    }

                    FindXRational x =
                        knownValue /
                        resultValue;

                    return CreateUniqueFindXSolution(
                        x,
                        knownValue,
                        resultValue,
                        "Muốn tìm số chia, ta lấy số bị chia chia cho thương; " +
                        "đồng thời số chia phải khác 0.",
                        $"x = {knownText} ÷ {resultText}\n" +
                        $"x = {FormatFindXValue(x)}");
                }

            default:
                return new FindXSolution(
                    FindXSolutionKind.NoSolution,
                    FindXRational.Zero,
                    "Không thể giải",
                    string.Empty,
                    string.Empty,
                    string.Empty);
        }
    }

    private FindXSolution CreateUniqueFindXSolution(
        FindXRational x,
        FindXRational knownValue,
        FindXRational resultValue,
        string rule,
        string transformation)
    {
        string xText =
            FormatFindXValue(
                x);

        string verification =
            BuildFindXVerification(
                x,
                knownValue,
                resultValue);

        return new FindXSolution(
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

    private string BuildFindXVerification(
        FindXRational x,
        FindXRational knownValue,
        FindXRational resultValue)
    {
        FindXRational leftValue =
            EvaluateFindXLeftSide(
                x,
                knownValue);

        string xText =
            FormatFindXValue(
                x);

        string knownText =
            FormatFindXValue(
                knownValue);

        string resultText =
            FormatFindXValue(
                resultValue);

        string leftText =
            FormatFindXValue(
                leftValue);

        return
            $"Thay x = {xText} vào phép tính ban đầu:\n" +
            $"{BuildFindXEquation(knownText, resultText).Replace("x", xText, StringComparison.Ordinal)}\n\n" +
            $"Vế trái = {leftText}; vế phải = {resultText}.\n" +
            "Hai vế bằng nhau nên kết quả tìm được là đúng.";
    }

    private FindXRational EvaluateFindXLeftSide(
        FindXRational x,
        FindXRational knownValue)
    {
        return (_findXOperation,
                _findXUnknownPosition) switch
        {
            (FindXOperation.Add, FindXUnknownPosition.Left) =>
                x + knownValue,

            (FindXOperation.Add, FindXUnknownPosition.Right) =>
                knownValue + x,

            (FindXOperation.Subtract, FindXUnknownPosition.Left) =>
                x - knownValue,

            (FindXOperation.Subtract, FindXUnknownPosition.Right) =>
                knownValue - x,

            (FindXOperation.Multiply, FindXUnknownPosition.Left) =>
                x * knownValue,

            (FindXOperation.Multiply, FindXUnknownPosition.Right) =>
                knownValue * x,

            (FindXOperation.Divide, FindXUnknownPosition.Left) =>
                x / knownValue,

            (FindXOperation.Divide, FindXUnknownPosition.Right) =>
                knownValue / x,

            _ =>
                FindXRational.Zero
        };
    }

    private void ShowFindXSolution(
        FindXRational knownValue,
        FindXRational resultValue,
        FindXSolution solution)
    {
        string knownText =
            FormatFindXValue(
                knownValue);

        string resultText =
            FormatFindXValue(
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
                            solution.Value);

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
                            solution.Value);

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
        FindXRational value)
    {
        if (value.Denominator.IsOne)
        {
            return FormatFindXIntegerForDisplay(
                value.Numerator);
        }

        if (TryFormatTerminatingFindXDecimal(
                value,
                out string decimalText))
        {
            return ShouldUseFindXScientificDisplay(
                    value)
                ? FormatFindXScientificForDisplay(
                    value)
                : decimalText;
        }

        return
            $"{FormatFindXIntegerForDisplay(value.Numerator)}/" +
            $"{FormatFindXIntegerForDisplay(value.Denominator)}";
    }

    private static bool ShouldUseFindXScientificDisplay(
        FindXRational value)
    {
        if (value.Denominator.IsOne)
        {
            return CountBigIntegerDigits(
                       value.Numerator) >
                   ScientificDisplayDigitThreshold;
        }

        if (!TryGetFindXPlainNumberText(
                value,
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
        FindXRational value)
    {
        if (!TryGetFindXPlainNumberText(
                value,
                out string plainText))
        {
            return FormatFindXValueForEditing(
                value);
        }

        return FormatPlainNumberAsScientificDisplay(
            plainText);
    }

    private static string FormatFindXScientificForCode(
        FindXRational value)
    {
        if (!TryGetFindXPlainNumberText(
                value,
                out string plainText) ||
            !TryBuildScientificParts(
                plainText,
                out bool isNegative,
                out string significantDigits,
                out int exponent))
        {
            return "0e0";
        }

        if (significantDigits == "0")
        {
            return "0e0";
        }

        string mantissa =
            significantDigits.Length == 1
                ? significantDigits
                : $"{significantDigits[0]}.{significantDigits[1..]}";

        return
            $"{(isNegative ? "-" : string.Empty)}" +
            $"{mantissa}e{exponent}";
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
        FindXRational value,
        out string plainText)
    {
        plainText =
            string.Empty;

        if (value.Denominator.IsOne)
        {
            plainText =
                value.Numerator.ToString(
                    CultureInfo.InvariantCulture);

            return true;
        }

        if (!TryFormatTerminatingFindXDecimal(
                value,
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

    private static bool TryFormatTerminatingFindXDecimal(
        FindXRational value,
        out string text)
    {
        text =
            string.Empty;

        BigInteger denominator =
            value.Denominator;

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
                value.Numerator);

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
                value.Numerator.Sign < 0
                    ? $"−{AddThousandsSeparators(digits)}"
                    : AddThousandsSeparators(digits);

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
            value.Numerator.Sign < 0
                ? "−"
                : string.Empty;

        text =
            decimalPart.Length == 0
                ? $"{sign}{AddThousandsSeparators(integerPart)}"
                : $"{sign}{AddThousandsSeparators(integerPart)}.{decimalPart}";

        return true;
    }

    private static string? CreateFindXApproximation(
        FindXRational value)
    {
        if (value.Denominator.IsOne ||
            TryFormatTerminatingFindXDecimal(
                value,
                out _))
        {
            return null;
        }

        double numerator =
            (double)value.Numerator;

        double denominator =
            (double)value.Denominator;

        double approximation =
            numerator /
            denominator;

        if (!double.IsFinite(
                approximation))
        {
            return null;
        }

        return approximation.ToString(
            "#,##0.##########",
            CultureInfo.InvariantCulture);
    }

    private void OnFindXClearClicked(
        object? sender,
        EventArgs e)
    {
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
        FindXRational Value,
        string StatusText,
        string RuleText,
        string StepsText,
        string VerificationText);

    private readonly struct FindXRational
    {
        public BigInteger Numerator { get; }

        public BigInteger Denominator { get; }

        public bool IsZero =>
            Numerator.IsZero;

        public static FindXRational Zero =>
            new(
                BigInteger.Zero,
                BigInteger.One);

        public FindXRational(
            BigInteger numerator,
            BigInteger denominator)
        {
            if (denominator.IsZero)
            {
                throw new DivideByZeroException(
                    "Mẫu số không được bằng 0.");
            }

            if (denominator.Sign < 0)
            {
                numerator =
                    BigInteger.Negate(
                        numerator);

                denominator =
                    BigInteger.Negate(
                        denominator);
            }

            if (numerator.IsZero)
            {
                Numerator =
                    BigInteger.Zero;

                Denominator =
                    BigInteger.One;

                return;
            }

            BigInteger greatestCommonDivisor =
                BigInteger.GreatestCommonDivisor(
                    BigInteger.Abs(
                        numerator),
                    denominator);

            Numerator =
                numerator /
                greatestCommonDivisor;

            Denominator =
                denominator /
                greatestCommonDivisor;
        }

        public static bool TryParse(
            string text,
            out FindXRational value)
        {
            value =
                Zero;

            string normalizedText =
                text
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

            if (normalizedText.Length == 0)
            {
                return false;
            }

            if (normalizedText.Contains(
                    'e'))
            {
                return TryParseScientific(
                    normalizedText,
                    out value);
            }

            bool isNegative =
                normalizedText[0] == '-';

            if (isNegative)
            {
                normalizedText =
                    normalizedText[1..];
            }

            if (normalizedText.Length == 0 ||
                normalizedText.Count(
                    character =>
                        character == '.') >
                1)
            {
                return false;
            }

            int decimalPointIndex =
                normalizedText.IndexOf('.');

            int decimalPlaces =
                decimalPointIndex < 0
                    ? 0
                    : normalizedText.Length -
                      decimalPointIndex -
                      1;

            string coefficientText =
                normalizedText.Replace(
                    ".",
                    string.Empty);

            if (coefficientText.Length == 0 ||
                !coefficientText.All(
                    char.IsDigit) ||
                !BigInteger.TryParse(
                    coefficientText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out BigInteger numerator))
            {
                return false;
            }

            if (isNegative)
            {
                numerator =
                    BigInteger.Negate(
                        numerator);
            }

            BigInteger denominator =
                decimalPlaces == 0
                    ? BigInteger.One
                    : BigInteger.Pow(
                        10,
                        decimalPlaces);

            value =
                new FindXRational(
                    numerator,
                    denominator);

            return true;
        }

        private static bool TryParseScientific(
            string text,
            out FindXRational value)
        {
            value =
                Zero;

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

            if (mantissaText.Length == 0 ||
                mantissaText.Count(
                    character =>
                        character == '.') >
                1)
            {
                return false;
            }

            int decimalPointIndex =
                mantissaText.IndexOf('.');

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

            if (isNegative)
            {
                coefficient =
                    BigInteger.Negate(
                        coefficient);
            }

            int power =
                exponent -
                decimalPlaces;

            BigInteger numerator =
                coefficient;

            BigInteger denominator =
                BigInteger.One;

            if (power >= 0)
            {
                numerator *=
                    BigInteger.Pow(
                        10,
                        power);
            }
            else
            {
                denominator =
                    BigInteger.Pow(
                        10,
                        -power);
            }

            value =
                new FindXRational(
                    numerator,
                    denominator);

            return true;
        }

        public static FindXRational operator +(
            FindXRational left,
            FindXRational right)
        {
            BigInteger greatestCommonDivisor =
                BigInteger.GreatestCommonDivisor(
                    left.Denominator,
                    right.Denominator);

            BigInteger leftScale =
                right.Denominator /
                greatestCommonDivisor;

            BigInteger rightScale =
                left.Denominator /
                greatestCommonDivisor;

            return new FindXRational(
                left.Numerator *
                leftScale +
                right.Numerator *
                rightScale,
                left.Denominator *
                leftScale);
        }

        public static FindXRational operator -(
            FindXRational left,
            FindXRational right)
        {
            return left +
                   new FindXRational(
                       BigInteger.Negate(
                           right.Numerator),
                       right.Denominator);
        }

        public static FindXRational operator *(
            FindXRational left,
            FindXRational right)
        {
            BigInteger firstCancellation =
                BigInteger.GreatestCommonDivisor(
                    BigInteger.Abs(
                        left.Numerator),
                    right.Denominator);

            BigInteger secondCancellation =
                BigInteger.GreatestCommonDivisor(
                    BigInteger.Abs(
                        right.Numerator),
                    left.Denominator);

            BigInteger leftNumerator =
                left.Numerator /
                firstCancellation;

            BigInteger rightDenominator =
                right.Denominator /
                firstCancellation;

            BigInteger rightNumerator =
                right.Numerator /
                secondCancellation;

            BigInteger leftDenominator =
                left.Denominator /
                secondCancellation;

            return new FindXRational(
                leftNumerator *
                rightNumerator,
                leftDenominator *
                rightDenominator);
        }

        public static FindXRational operator /(
            FindXRational left,
            FindXRational right)
        {
            if (right.Numerator.IsZero)
            {
                throw new DivideByZeroException(
                    "Không thể chia cho 0.");
            }

            return left *
                   new FindXRational(
                       right.Denominator,
                       right.Numerator);
        }
    }

    private static string FormatNumberWhileTyping(
        string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Bỏ các dấu phẩy cũ rồi tạo lại đúng theo từng nhóm 3 chữ số.
        string normalizedText =
            text.Replace(
                ",",
                string.Empty);

        if (normalizedText == "-")
        {
            return normalizedText;
        }

        bool isNegative =
            normalizedText.StartsWith(
                '-');

        string unsignedText =
            isNegative
                ? normalizedText[1..]
                : normalizedText;

        int decimalPointIndex =
            unsignedText.IndexOf('.');

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

        // Khi người dùng bắt đầu bằng dấu chấm, tự hiển thị thành 0.
        if (integerPart.Length == 0)
        {
            integerPart =
                "0";
        }
        else
        {
            // Tránh hiển thị kiểu 0,001 khi người dùng nhập 0001.
            integerPart =
                integerPart.TrimStart('0');

            if (integerPart.Length == 0)
            {
                integerPart =
                    "0";
            }
        }

        string groupedIntegerPart =
            AddThousandsSeparators(
                integerPart);

        string sign =
            isNegative
                ? "-"
                : string.Empty;

        return hasDecimalPoint
            ? $"{sign}{groupedIntegerPart}.{decimalPart}"
            : $"{sign}{groupedIntegerPart}";
    }

    private static string AddThousandsSeparators(
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
            digits.Length % 3;

        if (firstGroupLength == 0)
        {
            firstGroupLength =
                3;
        }

        builder.Append(
            digits,
            0,
            firstGroupLength);

        for (int index = firstGroupLength;
             index < digits.Length;
             index += 3)
        {
            builder.Append(',');
            builder.Append(
                digits,
                index,
                3);
        }

        return builder.ToString();
    }

    private static int CountLogicalCharacters(
        string text,
        int cursorPosition)
    {
        int logicalCount =
            0;

        int characterCount =
            Math.Min(
                cursorPosition,
                text.Length);

        for (int index = 0;
             index < characterCount;
             index++)
        {
            if (text[index] != ',')
            {
                logicalCount++;
            }
        }

        return logicalCount;
    }

    private static int FindCursorPosition(
        string formattedText,
        int logicalPosition)
    {
        if (logicalPosition <= 0)
        {
            return 0;
        }

        int logicalCount =
            0;

        for (int index = 0;
             index < formattedText.Length;
             index++)
        {
            if (formattedText[index] == ',')
            {
                continue;
            }

            logicalCount++;

            if (logicalCount >=
                logicalPosition)
            {
                return index + 1;
            }
        }

        return formattedText.Length;
    }

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