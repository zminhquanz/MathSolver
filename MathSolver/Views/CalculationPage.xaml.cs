using MathSolver.Graphics;
using MathSolver.Models;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Views;

public partial class CalculationPage : ContentPage
{
    private readonly LongDivisionDrawable _longDivisionDrawable = new();

    private ArithmeticOperation _selectedOperation = ArithmeticOperation.Add;

    private NumberInputType _selectedNumberType = NumberInputType.Integer;

    private bool _isUpdatingNumberText;

    private CalculationSubTab _selectedSubTab = CalculationSubTab.Basic;

    private LongDivisionDisplayMode _longDivisionDisplayMode = LongDivisionDisplayMode.Elementary;

    private decimal _currentDivisionDividend;
    private decimal _currentDivisionDivisor;

    public CalculationPage()
    {
        InitializeComponent();

        LongDivisionGraphicsView.Drawable = _longDivisionDrawable;

        SelectOperation(ArithmeticOperation.Add);

        SelectSubTab(CalculationSubTab.Basic);
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        SelectOperation(ArithmeticOperation.Add);
    }

    private void OnSubtractClicked(object sender, EventArgs e)
    {
        SelectOperation(ArithmeticOperation.Subtract);
    }

    private void OnMultiplyClicked(object sender, EventArgs e)
    {
        SelectOperation(ArithmeticOperation.Multiply);
    }

    private void OnDivideClicked(object sender, EventArgs e)
    {
        SelectOperation(ArithmeticOperation.Divide);
    }

    private void SelectOperation(ArithmeticOperation operation)
    {
        _selectedOperation = operation;

        ResetOperationButtonStyles();

        Button selectedButton;

        switch (operation)
        {
            case ArithmeticOperation.Add:
                selectedButton = AddButton;
                OperatorLabel.Text = "+";
                break;

            case ArithmeticOperation.Subtract:
                selectedButton = SubtractButton;
                OperatorLabel.Text = "−";
                break;

            case ArithmeticOperation.Multiply:
                selectedButton = MultiplyButton;
                OperatorLabel.Text = "×";
                break;

            case ArithmeticOperation.Divide:
                selectedButton = DivideButton;
                OperatorLabel.Text = "÷";
                break;

            default:
                selectedButton = AddButton;
                OperatorLabel.Text = "+";
                break;
        }

        selectedButton.BackgroundColor = Color.FromArgb("#2563EB");
        selectedButton.TextColor = Colors.White;

        HideMessages();
    }

    private void ResetOperationButtonStyles()
    {
        Color normalBackground = Color.FromArgb("#E8EEF6");
        Color normalTextColor = Color.FromArgb("#334155");

        AddButton.BackgroundColor = normalBackground;
        AddButton.TextColor = normalTextColor;

        SubtractButton.BackgroundColor = normalBackground;
        SubtractButton.TextColor = normalTextColor;

        MultiplyButton.BackgroundColor = normalBackground;
        MultiplyButton.TextColor = normalTextColor;

        DivideButton.BackgroundColor = normalBackground;
        DivideButton.TextColor = normalTextColor;
    }

    private void OnCalculateClicked(object sender, EventArgs e)
    {
        HideMessages();

        if (!TryReadNumber(
                FirstNumberEntry.Text,
                "Vui lòng nhập số thứ nhất.",
                out decimal firstNumber))
        {
            FirstNumberEntry.Focus();
            return;
        }

        if (!TryReadNumber(
                SecondNumberEntry.Text,
                "Vui lòng nhập số thứ hai.",
                out decimal secondNumber))
        {
            SecondNumberEntry.Focus();
            return;
        }

        if (_selectedOperation == ArithmeticOperation.Divide)
        {
            if (secondNumber == 0)
            {
                ShowDivisionByZeroError(firstNumber);
                SecondNumberEntry.Focus();
                return;
            }

            _currentDivisionDividend = firstNumber;
            _currentDivisionDivisor = secondNumber;

            //Phép chia thì sẽ có thêm phép chia đặt tính nên tạo ra hai hàm riêng biệt dành cho số nguyên và số thập phân để hiển thị kết quả
            if (_selectedNumberType == NumberInputType.Integer)
            {
                ShowElementaryDivisionResult(firstNumber, secondNumber);
            }
            else
            {
                ShowDecimalDivisionResult(firstNumber, secondNumber);
            }

            RefreshLongDivision();
        }
        else
        {
            //Trả kết quả dành cho cộng, trừ, nhân
            decimal result = Calculate(firstNumber, secondNumber);
            ShowResult(firstNumber, secondNumber, result);
        }
    }

    private void ShowDivisionByZeroError(decimal firstNumber)
    {
        string firstText = FormatNumber(firstNumber);

        ErrorLabel.Text = "Bạn không thể chia cho 0.";
        ErrorBorder.IsVisible = true;

        ExpressionLabel.Text = $"{firstText} ÷ 0";
        ResultLabel.Text = "Không xác định";

        ExplanationLabel.Text =
            $"Không thể thực hiện phép tính {firstText} ÷ 0.\n" +
            "Trong toán học, phép chia cho 0 không được xác định.\n" +
            "Bạn không thể chia một số cho 0.";

        ResultBorder.IsVisible = true;

        HideLongDivision();
    }

    private void ShowElementaryDivisionResult(decimal dividend, decimal divisor)
    {
        if (!IsWholeNumber(dividend) || !IsWholeNumber(divisor))
        {
            ShowDecimalDivisionResult(dividend, divisor);
            return;
        }

        long dividendInteger = decimal.ToInt64(dividend);
        long divisorInteger = decimal.ToInt64(divisor);

        long quotient = dividendInteger / divisorInteger;
        long remainder = dividendInteger % divisorInteger;

        string dividendText = dividendInteger.ToString();
        string divisorText = divisorInteger.ToString();

        ExpressionLabel.Text =
            $"{dividendText} ÷ {divisorText}";

        QuotientLabel.Text = quotient.ToString();
        RemainderLabel.Text = Math.Abs(remainder).ToString();

        DivisionDetailBorder.IsVisible = true;
        ResultBorder.IsVisible = true;

        if (remainder == 0)
        {
            DivisionTypeLabel.Text = "Đây là phép chia hết";

            ResultLabel.Text = quotient.ToString();

            ExplanationLabel.Text =
                $"Ta thực hiện phép chia {dividendText} cho {divisorText}.\n\n" +
                $"{dividendText} ÷ {divisorText} = {quotient}\n\n" +
                $"Vì số dư bằng 0 nên đây là phép chia hết.\n" +
                $"Thương là {quotient}.";
        }
        else
        {
            DivisionTypeLabel.Text = "Đây là phép chia có dư";

            ResultLabel.Text =
                $"{quotient} dư {Math.Abs(remainder)}";

            ExplanationLabel.Text =
                $"Ta thực hiện phép chia {dividendText} cho {divisorText}.\n\n" +
                $"{dividendText} ÷ {divisorText} = " +
                $"{quotient} dư {Math.Abs(remainder)}\n\n" +
                $"Ta kiểm tra:\n" +
                $"{divisorText} × {quotient} + " +
                $"{Math.Abs(remainder)} = {dividendText}\n\n" +
                $"Vậy thương là {quotient} và số dư là " +
                $"{Math.Abs(remainder)}.";
        }

        ShowLongDivision(dividend, divisor);
        AdditionalLabel.Text = CreateAdditional(dividendInteger, divisorInteger, quotient);
    }
    private static bool IsWholeNumber(decimal number)
    {
        return decimal.Truncate(number) == number;
    }

    private void ShowDecimalDivisionResult(decimal dividend, decimal divisor)
    {
        decimal result = dividend / divisor;

        string dividendText = FormatNumber(dividend);
        string divisorText = FormatNumber(divisor);
        string resultText = FormatNumber(result);

        ExpressionLabel.Text =
            $"{dividendText} ÷ {divisorText} = {resultText}";

        ResultLabel.Text = resultText;

        ExplanationLabel.Text =
            $"Ta lấy {dividendText} chia cho {divisorText}.\n\n" +
            $"{dividendText} ÷ {divisorText} = {resultText}.\n\n" +
            "Vì phép tính có số thập phân nên kết quả được " +
            "trình bày theo dạng số thập phân.";

        DivisionDetailBorder.IsVisible = false;
        ResultBorder.IsVisible = true;
        ShowLongDivision(dividend, divisor);
        AdditionalLabel.Text = CreateAdditional(dividend, divisor, result);
    }

    private bool TryReadNumber(string? input, string emptyMessage, out decimal number)
    {
        number = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            ShowError(emptyMessage);
            return false;
        }

        string normalizedInput = input.Trim();

        if (!IsCompleteValidNumber(normalizedInput))
        {
            if (_selectedNumberType == NumberInputType.Integer)
            {
                ShowError(
                    $"Giá trị \"{input}\" không phải là số nguyên hợp lệ.");
            }
            else
            {
                ShowError(
                    $"Giá trị \"{input}\" không phải là số thập phân hợp lệ.");
            }

            return false;
        }

        normalizedInput = normalizedInput.Replace(',', '.');

        bool isValid = decimal.TryParse(
            normalizedInput,
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out number);

        if (!isValid)
        {
            ShowError($"Giá trị \"{input}\" không phải là số hợp lệ.");
            return false;
        }

        if (_selectedNumberType == NumberInputType.Integer &&
            decimal.Truncate(number) != number)
        {
            ShowError("Bạn đang chọn chế độ số nguyên.");
            return false;
        }

        return true;
    }

    private decimal Calculate(decimal firstNumber, decimal secondNumber)
    {
        return _selectedOperation switch
        {
            ArithmeticOperation.Add =>
                firstNumber + secondNumber,

            ArithmeticOperation.Subtract =>
                firstNumber - secondNumber,

            ArithmeticOperation.Multiply =>
                firstNumber * secondNumber,

            ArithmeticOperation.Divide =>
                firstNumber / secondNumber,

            _ => 0
        };
    }

    private void ShowResult(decimal firstNumber, decimal secondNumber, decimal result)
    {
        string firstText = FormatNumber(firstNumber);
        string secondText = FormatNumber(secondNumber);
        string resultText = FormatNumber(result);

        string operationSymbol = GetOperationSymbol();
        if (operationSymbol != "÷")
        {
            HideLongDivision();
        }

        ExpressionLabel.Text =
            $"{firstText} {operationSymbol} {secondText} = {resultText}";

        ResultLabel.Text = resultText;

        ExplanationLabel.Text = CreateExplanation(
            firstText,
            secondText,
            resultText);

        AdditionalLabel.Text = CreateAdditional(firstNumber, secondNumber, result);

        ResultBorder.IsVisible = true;
    }

    private string CreateExplanation(
        string firstNumber,
        string secondNumber,
        string result)
    {
        return _selectedOperation switch
        {
            ArithmeticOperation.Add =>
                $"Ta lấy {firstNumber} cộng với {secondNumber}.\n" +
                $"{firstNumber} + {secondNumber} = {result}.\n" +
                $"Vậy kết quả là {result}.",

            ArithmeticOperation.Subtract =>
                $"Ta lấy {firstNumber} trừ đi {secondNumber}.\n" +
                $"{firstNumber} − {secondNumber} = {result}.\n" +
                $"Vậy kết quả là {result}.",

            ArithmeticOperation.Multiply =>
                $"Ta lấy {firstNumber} nhân với {secondNumber}.\n" +
                $"{firstNumber} × {secondNumber} = {result}.\n" +
                $"Vậy kết quả là {result}.",

            _ => string.Empty
        };
    }

    private string CreateAdditional(decimal firstNumber, decimal secondNumber, decimal result)
    {
        string firstText = FormatNumber(firstNumber);
        string secondText = FormatNumber(secondNumber);
        string resultText = FormatNumber(result);
        string sumContentAdditional = "";
        string subContentAdditional = "";
        string mulContentAdditional = "";
        string divContentAdditional = "";

        //Trường hợp cộng số 0, một trong hai số nhập vào có số 0
        if (_selectedOperation == ArithmeticOperation.Add)
        {
            sumContentAdditional = $"Phép cộng có tính chất giao hoán, kết hợp và cộng với số 0";
            if ((firstNumber == 0 && secondNumber != 0) || (firstNumber != 0 && secondNumber == 0))
            {
                sumContentAdditional +=
                    $"\nĐây là trường hợp có tính chất giao hoán và cộng số 0" +
                    $"\nNên là {firstText} + {secondText} = {secondText} + {firstText} = {resultText}";
            }
            //Trường hợp giao hoán, cả hai số nhập vào lớn hơn 0
            else if (firstNumber > 0 && secondNumber > 0 && firstNumber != secondNumber)
            {
                sumContentAdditional +=
                    $"\nĐây là trường hợp có tính chất giao hoán" +
                    $"\nNên là {firstText} + {secondText} = {secondText} + {firstText} = {resultText}";
            }
            //Trường hợp giao hoán, cả hai số đều bằng nhau
            else if (firstNumber > 0 && secondNumber > 0 && firstNumber == secondNumber)
            {
                sumContentAdditional +=
                    $"\nĐây là trường hợp có tính chất giao hoán vì hai số nhập vào đều bằng nhau" +
                    $"\nNên là {firstText} + {secondText} = {resultText}";
            }
            //Trường hợp giao hoán, cả hai số đều bằng 0
            else if (firstNumber == 0 && secondNumber == 0)
            {
                sumContentAdditional +=
                    $"\nĐây là trường hợp có tính chất giao hoán vì hai số nhập vào đều là số 0" +
                    $"\nNên là {firstText} + {secondText} = {resultText}";
            }
        }
        else if (_selectedOperation == ArithmeticOperation.Subtract)
        {
            subContentAdditional = $"Phép trừ có tính chất trừ đi số 0 và trừ đi chính nó nên lúc nào cũng bằng 0";
            //Trừ đi số 0
            if (secondNumber == 0)
            {
                subContentAdditional +=
                    $"\nĐây là trường hợp trừ đi số 0 nên lúc nào cũng bằng với số bị trừ" +
                    $"\nNên là {firstText} - {secondText} = {resultText}";
            }

            //Trừ đi chính nó
            else if (firstNumber == secondNumber)
            {
                subContentAdditional +=
                    $"\nĐây là trường hợp trừ đi chính nó nên lúc nào cũng bằng 0" +
                    $"\nNên là {firstText} - {secondText} = {resultText}";
            }
        }
        else if (_selectedOperation == ArithmeticOperation.Multiply)
        {
            mulContentAdditional = $"Phép nhân có tính chất giao hoán, kết hợp, nhân với 1, nhân với 0 và tính chất phân phối";

            if (firstNumber == 0 || secondNumber == 0)
            {
                mulContentAdditional +=
                    $"\nĐây là trường hợp có tính chất giao hoán và nhân với số 0 nên lúc nào cũng bằng 0" +
                    $"\nNên là {firstText} × {secondText} = {resultText}";
            }
            else if (firstNumber == 1 || secondNumber == 1)
            {
                mulContentAdditional +=
                    $"\nĐây là trường hợp có tính chất giao hoán và nhân với số 1 nên lúc nào cũng bằng với thừa số thứ nhất hoặc thừa số thứ hai" +
                    $"\nNên là {firstText} × {secondText} = {resultText}";
            }
            else if (firstNumber > 0 && secondNumber > 0 && firstNumber != secondNumber)
            {
                mulContentAdditional +=
                    $"\nĐây là trường hợp có tính chất giao hoán" +
                    $"\nNên là {firstText} × {secondText} = {secondText} × {firstText} = {resultText}";
            }
        }
        else if (_selectedOperation == ArithmeticOperation.Divide)
        {
            divContentAdditional = $"Phép chia có tính chất chia cho 1, chia cho chính nó, số 0 chia cho một số";
            //Trường hợp chia cho 1 và số 0 chia cho 1 số
            if (firstNumber == 0 && secondNumber == 1)
            {
                divContentAdditional +=
                    $"\nĐây là trường hợp chia cho 1 và số 0 chia cho 1 số nên lúc nào cũng bằng với số bị chia là 0" +
                    $"\nNên là {firstText} ÷ {secondText} = {resultText}";
            }
            //Trường hợp chia cho 1
            else if (firstNumber > 0 && secondNumber == 1)
            {
                divContentAdditional +=
                    $"\nĐây là trường hợp chia cho 1 nên lúc cũng bằng với số bị chia" +
                    $"\nNên là {firstText} ÷ {secondText} = {resultText}";
            }

            //Trường hợp chia cho chính nó
            else if (firstNumber == secondNumber)
            {
                divContentAdditional +=
                    $"\nĐây là trường hợp chia cho chính nó nên lúc nào cũng bằng 1" +
                    $"\nNên là {firstText} ÷ {secondText} = {resultText}";
            }

            //Trường hợp số 0 chia cho một số
            else if (firstNumber == 0 && secondNumber > 1)
            {
                divContentAdditional +=
                    $"\nĐây là trường hợp số 0 chia cho một số nên lúc nào cũng bằng 0" +
                    $"\nNên là {firstText} ÷ {secondText} = {resultText}";
            }
        }

        return _selectedOperation switch
        {
            ArithmeticOperation.Add =>
                sumContentAdditional,
            ArithmeticOperation.Subtract =>
                subContentAdditional,
            ArithmeticOperation.Multiply =>
                mulContentAdditional,
            ArithmeticOperation.Divide =>
                divContentAdditional,
            _ => string.Empty
        };
    }

    private string GetOperationSymbol()
    {
        return _selectedOperation switch
        {
            ArithmeticOperation.Add => "+",
            ArithmeticOperation.Subtract => "−",
            ArithmeticOperation.Multiply => "×",
            ArithmeticOperation.Divide => "÷",
            _ => "+"
        };
    }

    private static string FormatNumber(decimal number)
    {
        // Hiển thị tối đa 10 chữ số thập phân,
        // đồng thời loại bỏ các số 0 không cần thiết ở cuối.
        return number.ToString(
            "0.##########",
            CultureInfo.InvariantCulture);
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        FirstNumberEntry.Text = string.Empty;
        SecondNumberEntry.Text = string.Empty;

        HideMessages();

        FirstNumberEntry.Focus();
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorBorder.IsVisible = true;
        ResultBorder.IsVisible = false;
    }

    private void HideMessages()
    {
        ErrorBorder.IsVisible = false;
        ResultBorder.IsVisible = false;

        DivisionDetailBorder.IsVisible = false;
        HideLongDivision();
    }

    private void OnNumberTypeChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
        {
            return;
        }

        if (IntegerRadioButton.IsChecked)
        {
            _selectedNumberType = NumberInputType.Integer;

            NumberTypeDescriptionLabel.Text =
                "Chỉ được nhập số nguyên, ví dụ: 12, 50 hoặc -8.";

            FirstNumberEntry.Placeholder = "Ví dụ: 25";
            SecondNumberEntry.Placeholder = "Ví dụ: 15";
        }
        else
        {
            _selectedNumberType = NumberInputType.Decimal;

            NumberTypeDescriptionLabel.Text =
                "Được nhập số thập phân bằng dấu phẩy hoặc dấu chấm, " +
                "ví dụ: 2,5 hoặc 3.75.";

            FirstNumberEntry.Placeholder = "Ví dụ: 2,5";
            SecondNumberEntry.Placeholder = "Ví dụ: 1,25";
        }

        // Xóa dữ liệu cũ để tránh số đang nhập không phù hợp
        // với loại số vừa được chọn.
        FirstNumberEntry.Text = string.Empty;
        SecondNumberEntry.Text = string.Empty;

        HideMessages();
        FirstNumberEntry.Focus();
    }

    private void OnNumberEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingNumberText)
        {
            return;
        }

        if (sender is not Entry entry)
        {
            return;
        }

        string newText = e.NewTextValue ?? string.Empty;

        if (IsValidInputWhileTyping(newText))
        {
            return;
        }

        // Khôi phục lại nội dung hợp lệ trước đó.
        _isUpdatingNumberText = true;
        entry.Text = e.OldTextValue ?? string.Empty;
        _isUpdatingNumberText = false;

        ShowInputTypeError();
    }

    private void ShowInputTypeError()
    {
        if (_selectedNumberType == NumberInputType.Integer)
        {
            ErrorLabel.Text =
                "Bạn đang chọn số nguyên. " +
                "Không được nhập dấu phẩy, dấu chấm hoặc ký tự khác.";
        }
        else
        {
            ErrorLabel.Text =
                "Số thập phân chỉ được chứa chữ số, một dấu âm ở đầu " +
                "và tối đa một dấu phẩy hoặc dấu chấm.";
        }

        ErrorBorder.IsVisible = true;
        ResultBorder.IsVisible = false;
        DivisionDetailBorder.IsVisible = false;
    }

    private bool IsValidInputWhileTyping(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        int startIndex = 0;

        // Cho phép dấu âm duy nhất ở đầu chuỗi.
        if (text[0] == '-')
        {
            startIndex = 1;

            // Cho phép trạng thái tạm thời chỉ có dấu âm
            // trong lúc người dùng chuẩn bị nhập số.
            if (text.Length == 1)
            {
                return true;
            }
        }

        if (_selectedNumberType == NumberInputType.Integer)
        {
            for (int index = startIndex; index < text.Length; index++)
            {
                if (!char.IsDigit(text[index]))
                {
                    return false;
                }
            }

            return true;
        }

        bool hasDecimalSeparator = false;

        for (int index = startIndex; index < text.Length; index++)
        {
            char character = text[index];

            if (char.IsDigit(character))
            {
                continue;
            }

            if (character == ',' || character == '.')
            {
                if (hasDecimalSeparator)
                {
                    return false;
                }

                hasDecimalSeparator = true;
                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsCompleteValidNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text == "-" ||
            text == "," ||
            text == "." ||
            text == "-," ||
            text == "-.")
        {
            return false;
        }

        int startIndex = text[0] == '-' ? 1 : 0;
        int separatorCount = 0;
        int digitCount = 0;

        for (int index = startIndex; index < text.Length; index++)
        {
            char character = text[index];

            if (char.IsDigit(character))
            {
                digitCount++;
                continue;
            }

            if (_selectedNumberType == NumberInputType.Decimal &&
                (character == ',' || character == '.'))
            {
                separatorCount++;

                if (separatorCount > 1)
                {
                    return false;
                }

                continue;
            }

            return false;
        }

        return digitCount > 0;
    }

    private void ShowLongDivision(decimal dividend, decimal divisor)
    {
        LongDivisionResult divisionResult =
            LongDivisionCalculator.Calculate(
                dividend,
                divisor,
                maximumDecimalPlaces: 8);

        if (!divisionResult.IsLongDivisionSupported)
        {
            HideLongDivision();
            return;
        }

        _longDivisionDrawable.Result =
            divisionResult;

        LongDivisionGraphicsView.HeightRequest =
            CalculateLongDivisionHeight(
                divisionResult);

        LongDivisionBorder.IsVisible = true;

        LongDivisionGraphicsView.Invalidate();
    }

    private static double CalculateLongDivisionHeight(LongDivisionResult result)
    {
        const double minimumHeight = 180;
        const double topAreaHeight = 90;
        const double rowHeight = 38;

        if (result.Steps.Count == 0)
        {
            return minimumHeight;
        }

        int visibleRows = 1;

        for (int index = 0;
             index < result.Steps.Count;
             index++)
        {
            // Dòng tích cần trừ.
            visibleRows++;

            // Từ bước thứ hai trở đi có dòng số sau khi hạ xuống.
            if (index > 0)
            {
                visibleRows++;
            }
        }

        // Dòng số dư cuối cùng.
        visibleRows++;

        double calculatedHeight =
            topAreaHeight + visibleRows * rowHeight;

        return Math.Max(
            minimumHeight,
            calculatedHeight);
    }
    private void HideLongDivision()
    {
        LongDivisionBorder.IsVisible = false;

        _longDivisionDrawable.Result = null;

        LongDivisionGraphicsView.Invalidate();
    }

    private void OnBasicTabClicked(object sender, EventArgs e)
    {
        SelectSubTab(CalculationSubTab.Basic);
    }

    private void OnFractionTabClicked(object sender, EventArgs e)
    {
        SelectSubTab(CalculationSubTab.Fraction);
    }

    private void OnFindXTabClicked(object sender, EventArgs e)
    {
        SelectSubTab(CalculationSubTab.FindX);
    }

    private void OnGeometryTabClicked(object sender, EventArgs e)
    {
        SelectSubTab(CalculationSubTab.Geometry);
    }

    private void SelectSubTab(CalculationSubTab selectedTab)
    {
        _selectedSubTab = selectedTab;

        BasicTabContent.IsVisible = selectedTab == CalculationSubTab.Basic;

        FractionTabContent.IsVisible = selectedTab == CalculationSubTab.Fraction;

        FindXTabContent.IsVisible = selectedTab == CalculationSubTab.FindX;

        GeometryTabContent.IsVisible = selectedTab == CalculationSubTab.Geometry;


        UpdateSubTabButtonStyles();
    }

    private void UpdateSubTabButtonStyles()
    {
        Color selectedBackground =
            Color.FromArgb("#6D28D9");

        Color normalBackground =
            Colors.Transparent;

        Color selectedText =
            Colors.White;

        Color normalText =
            Color.FromArgb("#475569");

        ResetSubTabButton(
            BasicTabButton,
            normalBackground,
            normalText);

        ResetSubTabButton(
            FractionTabButton,
            normalBackground,
            normalText);

        ResetSubTabButton(
            FindXTabButton,
            normalBackground,
            normalText);

        ResetSubTabButton(
            GeometryTabButton,
            normalBackground,
            normalText);


        Button selectedButton =
            _selectedSubTab switch
            {
                CalculationSubTab.Basic =>
                    BasicTabButton,

                CalculationSubTab.Fraction =>
                    FractionTabButton,

                CalculationSubTab.FindX =>
                    FindXTabButton,

                CalculationSubTab.Geometry =>
                    GeometryTabButton,

                _ => BasicTabButton
            };

        selectedButton.BackgroundColor =
            selectedBackground;

        selectedButton.TextColor =
            selectedText;
    }

    private static void ResetSubTabButton(
    Button button,
    Color backgroundColor,
    Color textColor)
    {
        button.BackgroundColor =
            backgroundColor;

        button.TextColor =
            textColor;
    }

    private static string CreateDecimalDivisionExplanation(
    decimal dividend,
    decimal divisor,
    decimal result)
    {
        string dividendText =
            FormatNumber(dividend);

        string divisorText =
            FormatNumber(divisor);

        string resultText =
            FormatNumber(result);

        int divisorDecimalPlaces =
            GetDecimalPlaces(divisor);

        if (divisorDecimalPlaces == 0)
        {
            return
                $"Ta thực hiện phép chia {dividendText} cho " +
                $"{divisorText}.\n\n" +
                "Khi chia đến phần thập phân của số bị chia, " +
                "ta viết dấu phẩy vào thương rồi tiếp tục chia.\n\n" +
                $"{dividendText} ÷ {divisorText} = {resultText}.\n\n" +
                $"Vậy kết quả là {resultText}.";
        }

        decimal multiplier =
            GetPowerOfTen(
                divisorDecimalPlaces);

        decimal normalizedDividend =
            dividend * multiplier;

        decimal normalizedDivisor =
            divisor * multiplier;

        return
            $"Số chia {divisorText} có " +
            $"{divisorDecimalPlaces} chữ số ở phần thập phân.\n\n" +
            $"Ta chuyển dấu phẩy của cả số bị chia và số chia " +
            $"sang phải {divisorDecimalPlaces} chữ số:\n\n" +
            $"{dividendText} ÷ {divisorText}\n" +
            $"= {FormatNumber(normalizedDividend)} ÷ " +
            $"{FormatNumber(normalizedDivisor)}.\n\n" +
            "Sau đó thực hiện phép chia đặt tính như chia số tự nhiên.\n\n" +
            $"{dividendText} ÷ {divisorText} = {resultText}.\n\n" +
            $"Vậy kết quả là {resultText}.";
    }

    private static int GetDecimalPlaces(
    decimal number)
    {
        number =
            Math.Abs(number);

        int decimalPlaces = 0;

        while (number != decimal.Truncate(number) &&
               decimalPlaces < 28)
        {
            number *= 10;
            decimalPlaces++;
        }

        return decimalPlaces;
    }

    private static decimal GetPowerOfTen(
        int exponent)
    {
        decimal result = 1;

        for (int index = 0;
             index < exponent;
             index++)
        {
            result *= 10;
        }

        return result;
    }

    private void OnLongDivisionDisplayModeChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
        {
            return;
        }

        _longDivisionDisplayMode =
            sender == ElementaryDivisionModeRadioButton
                ? LongDivisionDisplayMode.Elementary
                : LongDivisionDisplayMode.Decimal;

        UpdateLongDivisionModeStyles();

        if (_currentDivisionDivisor != 0)
        {
            RefreshLongDivision();
        }
    }

    private void UpdateLongDivisionModeStyles()
    {
        Color selectedBackground =
            Color.FromArgb("#F5F3FF");

        Color selectedStroke =
            Color.FromArgb("#7C3AED");

        Color normalBackground =
            Colors.White;

        Color normalStroke =
            Color.FromArgb("#CBD5E1");

        bool elementarySelected =
            _longDivisionDisplayMode ==
            LongDivisionDisplayMode.Elementary;

        ElementaryDivisionModeBorder.BackgroundColor =
            elementarySelected
                ? selectedBackground
                : normalBackground;

        ElementaryDivisionModeBorder.Stroke =
            elementarySelected
                ? selectedStroke
                : normalStroke;

        ElementaryDivisionModeBorder.StrokeThickness =
            elementarySelected ? 1.5 : 1;

        DecimalDivisionModeBorder.BackgroundColor =
            elementarySelected
                ? normalBackground
                : selectedBackground;

        DecimalDivisionModeBorder.Stroke =
            elementarySelected
                ? normalStroke
                : selectedStroke;

        DecimalDivisionModeBorder.StrokeThickness =
            elementarySelected ? 1 : 1.5;
    }

    private void RefreshLongDivision()
    {
        LongDivisionResult divisionResult;

        if (_longDivisionDisplayMode ==
            LongDivisionDisplayMode.Elementary)
        {
            divisionResult =
                LongDivisionCalculator.Calculate(
                    _currentDivisionDividend,
                    _currentDivisionDivisor,
                    maximumDecimalPlaces: 0);
        }
        else
        {
            divisionResult =
                LongDivisionCalculator.Calculate(
                    _currentDivisionDividend,
                    _currentDivisionDivisor,
                    maximumDecimalPlaces: 8);
        }

        if (!divisionResult.IsLongDivisionSupported)
        {
            HideLongDivision();
            return;
        }

        _longDivisionDrawable.Result =
            divisionResult;

        LongDivisionGraphicsView.HeightRequest =
            CalculateLongDivisionHeight(
                divisionResult);

        LongDivisionBorder.IsVisible = true;
        LongDivisionGraphicsView.Invalidate();
    }
}
public enum ArithmeticOperation
{
    Add,
    Subtract,
    Multiply,
    Divide
}
public enum NumberInputType
{
    Integer,
    Decimal
}

public enum CalculationSubTab
{
    Basic,
    Fraction,
    FindX,
    Geometry
}
public enum LongDivisionDisplayMode
{
    Elementary,
    Decimal
}