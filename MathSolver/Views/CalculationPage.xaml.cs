using MathSolver.Graphics;
using MathSolver.Models;
using MathSolver.Services;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace MathSolver.Views;

public partial class CalculationPage : ContentPage
{
    private readonly LongDivisionDrawable _longDivisionDrawable = new();

    private ArithmeticOperation _selectedOperation = ArithmeticOperation.Add;

    private NumberInputType _selectedNumberType = NumberInputType.Integer;

    private bool _isUpdatingNumberText;

    // Khi Entry mất focus và số quá dài, giá trị chính xác được giữ
    // bằng dạng khoa học dùng chữ e, ví dụ: 1e18.
    private readonly Dictionary<Entry, string> _entryScientificCodeValues = new();

    // Trên giao diện, số có hơn 18 chữ số sẽ được rút gọn thành
    // dạng a × 10ⁿ để không làm vỡ bố cục.
    private const int ScientificDisplayDigitThreshold = 18;
    private const int ScientificDisplaySignificantDigits = 12;

    // decimal hỗ trợ khoảng 28–29 chữ số có nghĩa.
    // Dùng 28 để mọi giá trị nhập đều nằm trong vùng an toàn.
    private const int MaxInputSignificantDigits = 28;

    // Chỉ cho phép tối đa 10 chữ số sau dấu chấm.
    // Dấu phẩy chỉ dùng để phân nhóm hàng nghìn.
    private const int MaxDecimalPlaces = 10;

    private CalculationSubTab _selectedSubTab = CalculationSubTab.Basic;

    private LongDivisionDisplayMode _longDivisionDisplayMode = LongDivisionDisplayMode.Elementary;

    private decimal _currentDivisionDividend;
    private decimal _currentDivisionDivisor;

    public CalculationPage()
    {
        InitializeComponent();

        LongDivisionGraphicsView.Drawable = _longDivisionDrawable;

        FirstNumberEntry.Focused +=
            OnNumberEntryFocused;

        FirstNumberEntry.Unfocused +=
            OnNumberEntryUnfocused;

        SecondNumberEntry.Focused +=
            OnNumberEntryFocused;

        SecondNumberEntry.Unfocused +=
            OnNumberEntryUnfocused;

        SelectOperation(ArithmeticOperation.Add);

        SelectSubTab(CalculationSubTab.Basic);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Drawable đọc màu từ theme ở mỗi lần vẽ.
        LongDivisionGraphicsView.Invalidate();
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

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");

        HideMessages();
    }

    private void ResetOperationButtonStyles()
    {
        Button[] buttons =
        [
            AddButton,
            SubtractButton,
            MultiplyButton,
            DivideButton
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

    private void OnCalculateClicked(object sender, EventArgs e)
    {
        HideMessages();

        if (!TryReadNumber(
                GetEntryInputText(
                    FirstNumberEntry),
                "Vui lòng nhập số thứ nhất.",
                out decimal firstNumber))
        {
            FirstNumberEntry.Focus();
            return;
        }

        if (!TryReadNumber(
                GetEntryInputText(
                    SecondNumberEntry),
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
                ShowDecimalDivisionResult(
                    firstNumber,
                    secondNumber);
            }

            // Không gọi RefreshLongDivision() thêm lần nữa ở đây.
            // Hai hàm Show...DivisionResult đã tự kiểm tra phạm vi Int64
            // trước khi gửi dữ liệu vào LongDivisionCalculator.
        }
        else
        {
            // Cộng, trừ và nhân phải kiểm tra tràn trước khi hiển thị.
            if (!TryCalculateSafely(
                    firstNumber,
                    secondNumber,
                    out decimal result))
            {
                return;
            }

            ShowResult(
                firstNumber,
                secondNumber,
                result);
        }
    }

    private void ShowDivisionByZeroError(decimal firstNumber)
    {
        string firstText = FormatNumberForDisplay(firstNumber);

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

    private void ShowElementaryDivisionResult(
        decimal dividend,
        decimal divisor)
    {
        if (!IsWholeNumber(dividend) ||
            !IsWholeNumber(divisor))
        {
            ShowDecimalDivisionResult(
                dividend,
                divisor);

            return;
        }

        // Không chuyển sang long vì long chỉ chứa tối đa khoảng 19 chữ số.
        // decimal của ứng dụng có thể nhận tới 28 chữ số, nên dùng BigInteger
        // để thực hiện phép chia số nguyên chính xác và không bị overflow.
        BigInteger dividendInteger =
            new(dividend);

        BigInteger divisorInteger =
            new(divisor);

        BigInteger quotient =
            BigInteger.DivRem(
                dividendInteger,
                divisorInteger,
                out BigInteger remainder);

        BigInteger absoluteRemainder =
            BigInteger.Abs(
                remainder);

        string dividendText =
            FormatIntegerForDisplay(
                dividendInteger);

        string divisorText =
            FormatIntegerForDisplay(
                divisorInteger);

        string quotientText =
            FormatIntegerForDisplay(
                quotient);

        string remainderText =
            FormatIntegerForDisplay(
                absoluteRemainder);

        ExpressionLabel.Text =
            $"{dividendText} ÷ {divisorText}";

        QuotientLabel.Text =
            quotientText;

        RemainderLabel.Text =
            remainderText;

        DivisionDetailBorder.IsVisible =
            true;

        ResultBorder.IsVisible =
            true;

        if (remainder.IsZero)
        {
            DivisionTypeLabel.Text =
                "Đây là phép chia hết";

            ResultLabel.Text =
                quotientText;

            ExplanationLabel.Text =
                $"Ta thực hiện phép chia {dividendText} cho {divisorText}.\n\n" +
                $"{dividendText} ÷ {divisorText} = {quotientText}\n\n" +
                "Vì số dư bằng 0 nên đây là phép chia hết.\n" +
                $"Thương là {quotientText}.";
        }
        else
        {
            DivisionTypeLabel.Text =
                "Đây là phép chia có dư";

            ResultLabel.Text =
                $"{quotientText} dư {remainderText}";

            ExplanationLabel.Text =
                $"Ta thực hiện phép chia {dividendText} cho {divisorText}.\n\n" +
                $"{dividendText} ÷ {divisorText} = " +
                $"{quotientText} dư {remainderText}\n\n" +
                "Ta kiểm tra:\n" +
                $"{divisorText} × {quotientText} + " +
                $"{remainderText} = {dividendText}\n\n" +
                $"Vậy thương là {quotientText} và số dư là " +
                $"{remainderText}.";
        }

        // Drawable đặt tính hiện tại chỉ nên nhận dữ liệu trong vùng Int64.
        // Với số lớn hơn, vẫn hiện kết quả và lời giải nhưng ẩn hình đặt tính
        // để tránh overflow ở tầng LongDivisionCalculator/Drawable.
        if (CanRenderLongDivision(
                dividend,
                divisor))
        {
            ShowLongDivision(
                dividend,
                divisor);
        }
        else
        {
            HideLongDivision();
        }

        decimal quotientDecimal =
            (decimal)quotient;

        AdditionalLabel.Text =
            CreateAdditional(
                dividend,
                divisor,
                quotientDecimal);
    }
    private static bool IsWholeNumber(decimal number)
    {
        return decimal.Truncate(number) == number;
    }

    private void ShowDecimalDivisionResult(
        decimal dividend,
        decimal divisor)
    {
        decimal result;

        try
        {
            result =
                checked(
                    dividend /
                    divisor);
        }
        catch (OverflowException)
        {
            ShowOverflowError(
                "Thương",
                dividend,
                divisor);

            return;
        }

        if (!IsSupportedDecimalDivisionResult(
                dividend,
                divisor,
                result))
        {
            ShowResultPrecisionError(
                dividend,
                divisor);

            return;
        }

        string dividendText =
            FormatNumberForDisplay(
                dividend);

        string divisorText =
            FormatNumberForDisplay(
                divisor);

        string resultText =
            FormatNumberForDisplay(
                result);

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

    private bool TryReadNumber(
        string? input,
        string emptyMessage,
        out decimal number)
    {
        number = 0;

        if (string.IsNullOrWhiteSpace(
                input))
        {
            ShowError(
                emptyMessage);

            return false;
        }

        string normalizedInput =
            NormalizeNumberForParsing(
                input.Trim());

        bool isScientificCode =
            IsScientificCodeNotation(
                normalizedInput);

        if ((!isScientificCode &&
             !IsCompleteValidNumber(
                 normalizedInput)) ||
            (isScientificCode &&
             !IsCompleteScientificCodeNumber(
                 normalizedInput)))
        {
            if (_selectedNumberType ==
                NumberInputType.Integer)
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

        bool isValid =
            decimal.TryParse(
                normalizedInput,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out number);

        if (!isValid)
        {
            ShowError(
                $"Giá trị \"{input}\" không phải là số hợp lệ.");

            return false;
        }

        if (_selectedNumberType ==
                NumberInputType.Integer &&
            decimal.Truncate(
                number) !=
            number)
        {
            ShowError(
                "Bạn đang chọn chế độ số nguyên.");

            return false;
        }

        return true;
    }

    private bool TryCalculateSafely(
        decimal firstNumber,
        decimal secondNumber,
        out decimal result)
    {
        result = 0;

        if (_selectedOperation == ArithmeticOperation.Add &&
            WillAdditionOverflow(firstNumber, secondNumber))
        {
            ShowOverflowError(
                "Tổng",
                firstNumber,
                secondNumber);

            return false;
        }

        if (_selectedOperation == ArithmeticOperation.Multiply &&
            WillMultiplicationOverflow(firstNumber, secondNumber))
        {
            ShowOverflowError(
                "Tích",
                firstNumber,
                secondNumber);

            return false;
        }

        try
        {
            result =
                _selectedOperation switch
                {
                    ArithmeticOperation.Add =>
                        checked(firstNumber + secondNumber),

                    ArithmeticOperation.Subtract =>
                        checked(firstNumber - secondNumber),

                    ArithmeticOperation.Multiply =>
                        checked(firstNumber * secondNumber),

                    ArithmeticOperation.Divide =>
                        checked(firstNumber / secondNumber),

                    _ => 0
                };

            if (_selectedNumberType ==
                    NumberInputType.Decimal &&
                GetEffectiveDecimalPlaces(
                    result) >
                MaxDecimalPlaces)
            {
                ShowResultPrecisionError(
                    firstNumber,
                    secondNumber);

                return false;
            }

            return true;
        }
        catch (OverflowException)
        {
            ShowOverflowError(
                "Kết quả",
                firstNumber,
                secondNumber);

            return false;
        }
    }

    private static bool WillAdditionOverflow(
        decimal firstNumber,
        decimal secondNumber)
    {
        if (secondNumber > 0)
        {
            return firstNumber >
                   decimal.MaxValue -
                   secondNumber;
        }

        if (secondNumber < 0)
        {
            return firstNumber <
                   decimal.MinValue -
                   secondNumber;
        }

        return false;
    }

    private static bool WillMultiplicationOverflow(
        decimal firstNumber,
        decimal secondNumber)
    {
        if (firstNumber == 0 ||
            secondNumber == 0)
        {
            return false;
        }

        decimal absoluteFirst =
            Math.Abs(firstNumber);

        decimal absoluteSecond =
            Math.Abs(secondNumber);

        return absoluteFirst >
               decimal.MaxValue /
               absoluteSecond;
    }

    private void ShowOverflowError(
        string resultName,
        decimal firstNumber,
        decimal secondNumber)
    {
        string operationSymbol =
            GetOperationSymbol();

        ErrorLabel.Text =
            $"{resultName} của phép tính " +
            $"{FormatNumberForDisplay(firstNumber)} " +
            $"{operationSymbol} " +
            $"{FormatNumberForDisplay(secondNumber)} " +
            "vượt quá phạm vi số mà ứng dụng đang hỗ trợ.";

        ErrorBorder.IsVisible = true;
        ResultBorder.IsVisible = false;
        DivisionDetailBorder.IsVisible = false;

        HideLongDivision();
    }

    private void ShowResult(
        decimal firstNumber,
        decimal secondNumber,
        decimal result)
    {
        string firstText =
            FormatNumberForDisplay(
                firstNumber);

        string secondText =
            FormatNumberForDisplay(
                secondNumber);

        string resultText =
            FormatOperationResult(
                firstNumber,
                secondNumber,
                result);

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

    private string CreateAdditional(
        decimal firstNumber,
        decimal secondNumber,
        decimal result)
    {
        string firstText =
            FormatNumberForDisplay(
                firstNumber);

        string secondText =
            FormatNumberForDisplay(
                secondNumber);

        string resultText =
            _selectedOperation ==
            ArithmeticOperation.Divide
                ? FormatNumberForDisplay(
                    result)
                : FormatOperationResult(
                    firstNumber,
                    secondNumber,
                    result);

        return _selectedOperation switch
        {
            ArithmeticOperation.Add =>
                CreateAdditionAdditional(
                    firstNumber,
                    secondNumber,
                    firstText,
                    secondText,
                    resultText),

            ArithmeticOperation.Subtract =>
                CreateSubtractionAdditional(
                    firstNumber,
                    secondNumber,
                    firstText,
                    secondText,
                    resultText),

            ArithmeticOperation.Multiply =>
                CreateMultiplicationAdditional(
                    firstNumber,
                    secondNumber,
                    firstText,
                    secondText,
                    resultText),

            ArithmeticOperation.Divide =>
                CreateDivisionAdditional(
                    firstNumber,
                    secondNumber,
                    firstText,
                    secondText,
                    resultText),

            _ =>
                string.Empty
        };
    }

    private static string CreateAdditionAdditional(
        decimal firstNumber,
        decimal secondNumber,
        string firstText,
        string secondText,
        string resultText)
    {
        const string properties =
            "Phép cộng có tính chất giao hoán và kết hợp.\n" +
            "Số 0 là phần tử trung hòa của phép cộng.";

        if (firstNumber == 0 &&
            secondNumber == 0)
        {
            return BuildAdditionalText(
                properties,
                "Cả hai số hạng đều bằng 0.",
                "0 + 0 = 0.",
                $"{firstText} + {secondText} = {resultText}");
        }

        if (firstNumber == 0 ||
            secondNumber == 0)
        {
            string nonZeroText =
                firstNumber == 0
                    ? secondText
                    : firstText;

            return BuildAdditionalText(
                properties,
                "Đây là trường hợp cộng với số 0.",
                "a + 0 = 0 + a = a",
                $"{firstText} + {secondText} = " +
                $"{nonZeroText} = {resultText}");
        }

        if (firstNumber == secondNumber)
        {
            return BuildAdditionalText(
                properties,
                "Hai số hạng bằng nhau.",
                "a + a = 2 × a",
                $"{firstText} + {secondText} = {resultText}");
        }

        return BuildAdditionalText(
            properties,
            "Áp dụng tính chất giao hoán.",
            "a + b = b + a",
            $"{firstText} + {secondText}\n" +
            $"= {secondText} + {firstText}\n" +
            $"= {resultText}");
    }

    private static string CreateSubtractionAdditional(
        decimal firstNumber,
        decimal secondNumber,
        string firstText,
        string secondText,
        string resultText)
    {
        const string properties =
            "Phép trừ không có tính chất giao hoán.\n" +
            "Phép trừ cũng không có tính chất kết hợp.";

        if (secondNumber == 0)
        {
            return BuildAdditionalText(
                properties,
                "Đây là trường hợp trừ đi số 0.",
                "a − 0 = a",
                $"{firstText} − {secondText} = {resultText}");
        }

        if (firstNumber == secondNumber)
        {
            return BuildAdditionalText(
                properties,
                "Một số trừ chính nó luôn bằng 0.",
                "a − a = 0",
                $"{firstText} − {secondText} = {resultText}");
        }

        if (firstNumber == 0)
        {
            return BuildAdditionalText(
                properties,
                "Lấy 0 trừ một số sẽ được số đối của số đó.",
                "0 − a = −a",
                $"{firstText} − {secondText} = {resultText}");
        }

        return BuildAdditionalText(
            properties,
            "Có thể kiểm tra phép trừ bằng phép cộng.",
            "Hiệu + số trừ = số bị trừ",
            $"{resultText} + {secondText} = {firstText}");
    }

    private static string CreateMultiplicationAdditional(
        decimal firstNumber,
        decimal secondNumber,
        string firstText,
        string secondText,
        string resultText)
    {
        const string properties =
            "Phép nhân có tính chất giao hoán, kết hợp và phân phối.\n" +
            "Số 1 là phần tử đơn vị; số 0 là phần tử hấp thụ.";

        if (firstNumber == 0 ||
            secondNumber == 0)
        {
            return BuildAdditionalText(
                properties,
                "Đây là trường hợp nhân với số 0.",
                "a × 0 = 0 × a = 0",
                $"{firstText} × {secondText} = {resultText}");
        }

        if (firstNumber == 1 ||
            secondNumber == 1)
        {
            string unchangedText =
                firstNumber == 1
                    ? secondText
                    : firstText;

            return BuildAdditionalText(
                properties,
                "Đây là trường hợp nhân với số 1.",
                "a × 1 = 1 × a = a",
                $"{firstText} × {secondText} = " +
                $"{unchangedText} = {resultText}");
        }

        if (firstNumber == -1 ||
            secondNumber == -1)
        {
            return BuildAdditionalText(
                properties,
                "Nhân với −1 sẽ đổi một số thành số đối của nó.",
                "a × (−1) = −a",
                $"{firstText} × {secondText} = {resultText}");
        }

        if (firstNumber == secondNumber)
        {
            return BuildAdditionalText(
                properties,
                "Hai thừa số bằng nhau nên đây là một bình phương.",
                "a × a = a²",
                $"{firstText} × {secondText} = {resultText}");
        }

        return BuildAdditionalText(
            properties,
            "Áp dụng tính chất giao hoán.",
            "a × b = b × a",
            $"{firstText} × {secondText}\n" +
            $"= {secondText} × {firstText}\n" +
            $"= {resultText}");
    }

    private static string CreateDivisionAdditional(
        decimal firstNumber,
        decimal secondNumber,
        string firstText,
        string secondText,
        string resultText)
    {
        const string properties =
            "Phép chia không có tính chất giao hoán.\n" +
            "Phép chia cũng không có tính chất kết hợp.\n" +
            "Mọi quy tắc chỉ áp dụng khi số chia khác 0.";

        if (firstNumber == 0)
        {
            return BuildAdditionalText(
                properties,
                "Số 0 chia cho một số khác 0 luôn bằng 0.",
                "0 ÷ a = 0, với a ≠ 0",
                $"{firstText} ÷ {secondText} = {resultText}");
        }

        if (secondNumber == 1)
        {
            return BuildAdditionalText(
                properties,
                "Một số chia cho 1 vẫn giữ nguyên.",
                "a ÷ 1 = a",
                $"{firstText} ÷ {secondText} = {resultText}");
        }

        if (firstNumber == secondNumber)
        {
            return BuildAdditionalText(
                properties,
                "Một số khác 0 chia cho chính nó luôn bằng 1.",
                "a ÷ a = 1, với a ≠ 0",
                $"{firstText} ÷ {secondText} = {resultText}");
        }

        if (secondNumber == -1)
        {
            return BuildAdditionalText(
                properties,
                "Chia cho −1 sẽ đổi một số thành số đối của nó.",
                "a ÷ (−1) = −a",
                $"{firstText} ÷ {secondText} = {resultText}");
        }

        return BuildAdditionalText(
            properties,
            "Có thể kiểm tra phép chia bằng phép nhân.",
            "Thương × số chia = số bị chia",
            $"{resultText} × {secondText} = {firstText}");
    }

    private static string BuildAdditionalText(
        string properties,
        string currentCase,
        string rule,
        string example)
    {
        return
            $"Tính chất chung\n" +
            $"• {properties.Replace("\n", "\n• ")}\n\n" +
            $"Trường hợp đang áp dụng\n" +
            $"• {currentCase}\n" +
            $"• Quy tắc: {rule}\n\n" +
            $"Minh họa\n" +
            $"{example}";
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

    private string FormatOperationResult(
        decimal firstNumber,
        decimal secondNumber,
        decimal result)
    {
        int firstScale =
            GetDecimalScale(
                firstNumber);

        int secondScale =
            GetDecimalScale(
                secondNumber);

        int displayDecimalPlaces =
            _selectedOperation switch
            {
                ArithmeticOperation.Add or
                ArithmeticOperation.Subtract =>
                    Math.Max(
                        firstScale,
                        secondScale),

                ArithmeticOperation.Multiply =>
                    Math.Min(
                        MaxDecimalPlaces,
                        firstScale +
                        secondScale),

                _ =>
                    GetEffectiveDecimalPlaces(
                        result)
            };

        displayDecimalPlaces =
            Math.Clamp(
                displayDecimalPlaces,
                0,
                MaxDecimalPlaces);

        string standardText;

        if (displayDecimalPlaces == 0)
        {
            standardText =
                result.ToString(
                    "#,##0",
                    CultureInfo.InvariantCulture);
        }
        else
        {
            string format =
                "#,##0." +
                new string(
                    '0',
                    displayDecimalPlaces);

            standardText =
                result.ToString(
                    format,
                    CultureInfo.InvariantCulture);
        }

        return CountNumericDigits(
                   standardText) >
               ScientificDisplayDigitThreshold
            ? FormatScientificForDisplay(
                result)
            : standardText;
    }

    private static int GetDecimalScale(
        decimal number)
    {
        int[] bits =
            decimal.GetBits(
                number);

        return
            (bits[3] >> 16) &
            0x7F;
    }

    private static int GetEffectiveDecimalPlaces(
        decimal number)
    {
        int[] bits =
            decimal.GetBits(
                number);

        int scale =
            (bits[3] >> 16) &
            0x7F;

        if (scale == 0)
        {
            return 0;
        }

        BigInteger unscaledValue =
            (uint)bits[0];

        unscaledValue |=
            (BigInteger)(uint)bits[1] <<
            32;

        unscaledValue |=
            (BigInteger)(uint)bits[2] <<
            64;

        while (scale > 0 &&
               !unscaledValue.IsZero &&
               unscaledValue % 10 == 0)
        {
            unscaledValue /=
                10;

            scale--;
        }

        return scale;
    }

    private static bool IsSupportedDecimalDivisionResult(
        decimal dividend,
        decimal divisor,
        decimal result)
    {
        if (GetEffectiveDecimalPlaces(
                result) >
            MaxDecimalPlaces)
        {
            return false;
        }

        try
        {
            // Nếu nhân ngược không trở lại đúng số bị chia,
            // thương decimal đã được làm tròn nội bộ.
            return checked(
                       result *
                       divisor) ==
                   dividend;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private void ShowResultPrecisionError(
        decimal firstNumber,
        decimal secondNumber)
    {
        string operationSymbol =
            GetOperationSymbol();

        ErrorLabel.Text =
            $"Phép tính {FormatNumberForDisplay(firstNumber)} " +
            $"{operationSymbol} {FormatNumberForDisplay(secondNumber)} " +
            $"cho kết quả cần nhiều hơn {MaxDecimalPlaces} chữ số " +
            "sau dấu chấm hoặc không thể biểu diễn chính xác trong " +
            "giới hạn hiện tại. Ứng dụng không làm tròn kết quả để " +
            "tránh sai lệch.";

        ErrorBorder.IsVisible =
            true;

        ResultBorder.IsVisible =
            false;

        DivisionDetailBorder.IsVisible =
            false;

        HideLongDivision();
    }

    private static string FormatNumber(
        decimal number)
    {
        // Định dạng đầy đủ dùng cho chỉnh sửa và các phép xử lý nội bộ.
        return number.ToString(
            "#,##0.##########",
            CultureInfo.InvariantCulture);
    }

    private static string FormatNumberForDisplay(
        decimal number)
    {
        string standardText =
            FormatNumber(
                number);

        return CountNumericDigits(
                   standardText) >
               ScientificDisplayDigitThreshold
            ? FormatScientificForDisplay(
                number)
            : standardText;
    }

    private static string FormatInteger(
        BigInteger number)
    {
        return number.ToString(
            "#,##0",
            CultureInfo.InvariantCulture);
    }

    private static string FormatIntegerForDisplay(
        BigInteger number)
    {
        string standardText =
            FormatInteger(
                number);

        if (CountNumericDigits(
                standardText) <=
            ScientificDisplayDigitThreshold)
        {
            return standardText;
        }

        bool isNegative =
            number.Sign < 0;

        string digits =
            BigInteger.Abs(
                number)
            .ToString(
                CultureInfo.InvariantCulture);

        int exponent =
            digits.Length - 1;

        string mantissaDigits =
            digits[..Math.Min(
                ScientificDisplaySignificantDigits,
                digits.Length)];

        bool wasRounded =
            digits.Length >
            ScientificDisplaySignificantDigits &&
            digits[ScientificDisplaySignificantDigits..]
                .Any(
                    character =>
                        character != '0');

        string mantissa =
            BuildMantissaText(
                mantissaDigits);

        string sign =
            isNegative
                ? "−"
                : string.Empty;

        string approximation =
            wasRounded
                ? "≈ "
                : string.Empty;

        if (mantissa == "1")
        {
            return
                $"{approximation}{sign}10{ToSuperscript(exponent)}";
        }

        return
            $"{approximation}{sign}{mantissa} × " +
            $"10{ToSuperscript(exponent)}";
    }

    private static string FormatScientificForDisplay(
        decimal number)
    {
        string code =
            FormatScientificForCode(
                number);

        int exponentSeparatorIndex =
            code.IndexOf(
                'e');

        string exactMantissaText =
            code[..exponentSeparatorIndex];

        int exponent =
            int.Parse(
                code[(exponentSeparatorIndex + 1)..],
                CultureInfo.InvariantCulture);

        decimal exactMantissa =
            decimal.Parse(
                exactMantissaText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture);

        int mantissaDecimalPlaces =
            Math.Max(
                0,
                ScientificDisplaySignificantDigits - 1);

        decimal roundedMantissa =
            Math.Round(
                exactMantissa,
                mantissaDecimalPlaces,
                MidpointRounding.AwayFromZero);

        if (Math.Abs(
                roundedMantissa) >=
            10)
        {
            roundedMantissa /=
                10;

            exponent++;
        }

        bool wasRounded =
            roundedMantissa !=
            exactMantissa;

        string mantissaText =
            roundedMantissa.ToString(
                "0.###########",
                CultureInfo.InvariantCulture);

        string approximation =
            wasRounded
                ? "≈ "
                : string.Empty;

        if (mantissaText == "1")
        {
            return
                $"{approximation}10{ToSuperscript(exponent)}";
        }

        if (mantissaText == "-1")
        {
            return
                $"{approximation}−10{ToSuperscript(exponent)}";
        }

        return
            $"{approximation}{mantissaText} × " +
            $"10{ToSuperscript(exponent)}";
    }

    private static string FormatScientificForCode(
        decimal number)
    {
        if (number == 0)
        {
            return "0e0";
        }

        string scientificText =
            number.ToString(
                "0.############################E+0",
                CultureInfo.InvariantCulture);

        int exponentIndex =
            scientificText.IndexOf(
                'E');

        string mantissa =
            scientificText[..exponentIndex];

        string exponent =
            scientificText[(exponentIndex + 1)..]
                .TrimStart('+');

        return
            $"{mantissa}e{exponent}";
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

    private static int CountNumericDigits(
        string text)
    {
        int count =
            0;

        foreach (char character
                 in text)
        {
            if (char.IsDigit(
                    character))
            {
                count++;
            }
        }

        return count;
    }

    private static string NormalizeNumberForParsing(
        string text)
    {
        return text
            .Replace(
                ",",
                string.Empty)
            .Replace(
                "E",
                "e",
                StringComparison.Ordinal);
    }

    private static bool IsScientificCodeNotation(
        string text)
    {
        return text.Contains(
            'e');
    }

    private static bool IsCompleteScientificCodeNumber(
        string text)
    {
        int exponentIndex =
            text.IndexOf(
                'e');

        if (exponentIndex <= 0 ||
            exponentIndex !=
            text.LastIndexOf(
                'e') ||
            exponentIndex ==
            text.Length - 1)
        {
            return false;
        }

        string mantissa =
            text[..exponentIndex];

        string exponent =
            text[(exponentIndex + 1)..];

        if (!decimal.TryParse(
                mantissa,
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out _))
        {
            return false;
        }

        return int.TryParse(
            exponent,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out _);
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        _entryScientificCodeValues.Clear();

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
                "Nhập số nguyên; dấu phẩy phân nhóm hàng nghìn được thêm tự động, " +
                "ví dụ: 1,000 hoặc -25,000.";

            FirstNumberEntry.Placeholder = "Ví dụ: 100,000";
            SecondNumberEntry.Placeholder = "Ví dụ: 25,000";
        }
        else
        {
            _selectedNumberType = NumberInputType.Decimal;

            NumberTypeDescriptionLabel.Text =
                $"Dùng dấu chấm cho phần thập phân, tối đa " +
                $"{MaxDecimalPlaces} chữ số sau dấu chấm; dấu phẩy phân nhóm " +
                "hàng nghìn được thêm tự động, ví dụ: 2,500.75.";

            FirstNumberEntry.Placeholder = "Ví dụ: 2,500.75";
            SecondNumberEntry.Placeholder = "Ví dụ: 1,250.5";
        }

        // Xóa dữ liệu cũ để tránh số đang nhập không phù hợp
        // với loại số vừa được chọn.
        _entryScientificCodeValues.Clear();

        FirstNumberEntry.Text = string.Empty;
        SecondNumberEntry.Text = string.Empty;

        HideMessages();
        FirstNumberEntry.Focus();
    }

    private string? GetEntryInputText(
        Entry entry)
    {
        if (_entryScientificCodeValues.TryGetValue(
                entry,
                out string scientificCode))
        {
            return scientificCode;
        }

        return entry.Text;
    }

    private void OnNumberEntryFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry ||
            !_entryScientificCodeValues.TryGetValue(
                entry,
                out string scientificCode) ||
            !decimal.TryParse(
                scientificCode,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal number))
        {
            return;
        }

        _entryScientificCodeValues.Remove(
            entry);

        SetEntryTextWithoutValidation(
            entry,
            FormatNumber(
                number));
    }

    private void OnNumberEntryUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string currentText =
            entry.Text ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                currentText))
        {
            return;
        }

        string normalizedText =
            NormalizeNumberForParsing(
                currentText);

        if (!decimal.TryParse(
                normalizedText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal number))
        {
            return;
        }

        string standardText =
            FormatNumber(
                number);

        if (CountNumericDigits(
                standardText) <=
            ScientificDisplayDigitThreshold)
        {
            _entryScientificCodeValues.Remove(
                entry);

            return;
        }

        _entryScientificCodeValues[entry] =
            FormatScientificForCode(
                number);

        SetEntryTextWithoutValidation(
            entry,
            FormatScientificForDisplay(
                number));
    }

    private void SetEntryTextWithoutValidation(
        Entry entry,
        string text)
    {
        _isUpdatingNumberText =
            true;

        entry.Text =
            text;

        entry.CursorPosition =
            text.Length;

        entry.SelectionLength =
            0;

        _isUpdatingNumberText =
            false;
    }

    private void OnNumberEntryTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_isUpdatingNumberText)
        {
            return;
        }

        if (sender is not Entry entry)
        {
            return;
        }

        _entryScientificCodeValues.Remove(
            entry);

        string newText =
            e.NewTextValue ??
            string.Empty;

        if (!IsValidInputWhileTyping(
                newText))
        {
            // Khôi phục nội dung hợp lệ trước đó.
            _isUpdatingNumberText =
                true;

            entry.Text =
                e.OldTextValue ??
                string.Empty;

            entry.CursorPosition =
                entry.Text.Length;

            _isUpdatingNumberText =
                false;

            ShowInputTypeError();
            return;
        }

        string formattedText =
            FormatNumberWhileTyping(
                newText);

        if (formattedText == newText)
        {
            return;
        }

        int oldCursorPosition =
            Math.Clamp(
                entry.CursorPosition,
                0,
                newText.Length);

        int logicalPosition =
            CountLogicalCharacters(
                newText,
                oldCursorPosition);

        _isUpdatingNumberText =
            true;

        entry.Text =
            formattedText;

        entry.CursorPosition =
            FindCursorPosition(
                formattedText,
                logicalPosition);

        entry.SelectionLength =
            0;

        _isUpdatingNumberText =
            false;
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

    private void ShowInputTypeError()
    {
        if (_selectedNumberType == NumberInputType.Integer)
        {
            ErrorLabel.Text =
                $"Số nguyên chỉ được chứa chữ số, một dấu âm ở đầu " +
                $"và tối đa {MaxInputSignificantDigits} chữ số. " +
                "Dấu phẩy phân nhóm được ứng dụng thêm tự động.";
        }
        else
        {
            ErrorLabel.Text =
                $"Số thập phân chỉ được chứa chữ số, một dấu âm ở đầu, " +
                $"tối đa một dấu chấm, tối đa {MaxDecimalPlaces} chữ số " +
                $"sau dấu chấm và tối đa {MaxInputSignificantDigits} chữ số " +
                "tổng cộng; dấu phẩy được thêm tự động.";
        }

        ErrorBorder.IsVisible = true;
        ResultBorder.IsVisible = false;
        DivisionDetailBorder.IsVisible = false;
    }

    private bool IsValidInputWhileTyping(
        string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        string normalizedText =
            text.Replace(
                ",",
                string.Empty);

        if (normalizedText.Length == 0)
        {
            return false;
        }

        if (CountDigits(normalizedText) >
            MaxInputSignificantDigits)
        {
            return false;
        }

        int startIndex =
            0;

        // Cho phép một dấu âm duy nhất ở đầu.
        if (normalizedText[0] == '-')
        {
            startIndex =
                1;

            if (normalizedText.Length == 1)
            {
                return true;
            }
        }

        if (_selectedNumberType ==
            NumberInputType.Integer)
        {
            for (int index = startIndex;
                 index < normalizedText.Length;
                 index++)
            {
                if (!char.IsDigit(
                        normalizedText[index]))
                {
                    return false;
                }
            }

            return true;
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

            if (character == '.')
            {
                if (hasDecimalPoint)
                {
                    return false;
                }

                hasDecimalPoint =
                    true;

                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsCompleteValidNumber(
        string text)
    {
        if (string.IsNullOrWhiteSpace(
                text))
        {
            return false;
        }

        string normalizedText =
            text.Replace(
                ",",
                string.Empty);

        if (normalizedText == "-" ||
            normalizedText == "." ||
            normalizedText == "-.")
        {
            return false;
        }

        if (CountDigits(normalizedText) >
            MaxInputSignificantDigits)
        {
            return false;
        }

        int startIndex =
            normalizedText[0] == '-'
                ? 1
                : 0;

        int decimalPointCount =
            0;

        int digitCount =
            0;

        int decimalDigitCount =
            0;

        bool isAfterDecimalPoint =
            false;

        for (int index = startIndex;
             index < normalizedText.Length;
             index++)
        {
            char character =
                normalizedText[index];

            if (char.IsDigit(
                    character))
            {
                digitCount++;

                if (isAfterDecimalPoint)
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

            if (_selectedNumberType ==
                    NumberInputType.Decimal &&
                character == '.')
            {
                decimalPointCount++;

                if (decimalPointCount > 1)
                {
                    return false;
                }

                isAfterDecimalPoint =
                    true;

                continue;
            }

            return false;
        }

        return digitCount > 0;
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

    private static bool CanRenderLongDivision(
        decimal dividend,
        decimal divisor)
    {
        return dividend >= long.MinValue &&
               dividend <= long.MaxValue &&
               divisor >= long.MinValue &&
               divisor <= long.MaxValue;
    }

    private void ShowLongDivision(
        decimal dividend,
        decimal divisor)
    {
        if (!CanRenderLongDivision(
                dividend,
                divisor))
        {
            HideLongDivision();
            return;
        }

        try
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

            UpdateLongDivisionHeight();

            LongDivisionBorder.IsVisible =
                true;

            LongDivisionGraphicsView.Invalidate();
        }
        catch (OverflowException)
        {
            // Phần kết quả số học đã được tính chính xác ở phía trên.
            // Chỉ ẩn phần minh họa đặt tính nếu engine không hỗ trợ độ lớn.
            HideLongDivision();
        }
    }

    private void UpdateLongDivisionHeight()
    {
        double availableWidth =
            LongDivisionGraphicsView.Width;

        if (availableWidth <= 0)
        {
            // Lần hiển thị đầu tiên GraphicsView có thể chưa được measure.
            // Dùng chiều rộng trang làm giá trị dự phòng.
            availableWidth =
                Math.Max(
                    320,
                    Width - 96);
        }

        LongDivisionGraphicsView.HeightRequest =
            _longDivisionDrawable.GetPreferredHeight(
                availableWidth);
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
        ResetSubTabButton(BasicTabButton);
        ResetSubTabButton(FractionTabButton);
        ResetSubTabButton(FindXTabButton);
        ResetSubTabButton(GeometryTabButton);

        Button selectedButton =
            _selectedSubTab switch
            {
                CalculationSubTab.Basic => BasicTabButton,
                CalculationSubTab.Fraction => FractionTabButton,
                CalculationSubTab.FindX => FindXTabButton,
                CalculationSubTab.Geometry => GeometryTabButton,
                _ => BasicTabButton
            };

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");
    }

    private static void ResetSubTabButton(Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "SurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            "TextPrimaryColor");
    }

    private static string CreateDecimalDivisionExplanation(
    decimal dividend,
    decimal divisor,
    decimal result)
    {
        string dividendText =
            FormatNumberForDisplay(dividend);

        string divisorText =
            FormatNumberForDisplay(divisor);

        string resultText =
            FormatNumberForDisplay(result);

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
            $"= {FormatNumberForDisplay(normalizedDividend)} ÷ " +
            $"{FormatNumberForDisplay(normalizedDivisor)}.\n\n" +
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

        if (_currentDivisionDivisor == 0)
        {
            HideLongDivision();
            return;
        }

        if (!CanRenderLongDivision(
                _currentDivisionDividend,
                _currentDivisionDivisor))
        {
            // Số học vẫn được tính bằng decimal/BigInteger,
            // nhưng phần đặt tính hiện tại chỉ hỗ trợ Int64.
            HideLongDivision();
            return;
        }

        RefreshLongDivision();
    }

    private void UpdateLongDivisionModeStyles()
    {
        bool elementarySelected =
            _longDivisionDisplayMode ==
            LongDivisionDisplayMode.Elementary;

        ApplyLongDivisionModeStyle(
            ElementaryDivisionModeBorder,
            elementarySelected);

        ApplyLongDivisionModeStyle(
            DecimalDivisionModeBorder,
            !elementarySelected);
    }

    private static void ApplyLongDivisionModeStyle(
        Border border,
        bool selected)
    {
        border.SetDynamicResource(
            Border.BackgroundColorProperty,
            selected
                ? "PrimarySoftColor"
                : "SurfaceColor");

        border.SetDynamicResource(
            Border.StrokeProperty,
            selected
                ? "PrimaryBrush"
                : "BorderBrush");

        border.StrokeThickness =
            selected ? 1.5 : 1;
    }

    private void RefreshLongDivision()
    {
        if (_currentDivisionDivisor == 0 ||
            !CanRenderLongDivision(
                _currentDivisionDividend,
                _currentDivisionDivisor))
        {
            HideLongDivision();
            return;
        }

        int maximumDecimalPlaces =
            _longDivisionDisplayMode ==
            LongDivisionDisplayMode.Elementary
                ? 0
                : 8;

        try
        {
            LongDivisionResult divisionResult =
                LongDivisionCalculator.Calculate(
                    _currentDivisionDividend,
                    _currentDivisionDivisor,
                    maximumDecimalPlaces);

            if (!divisionResult.IsLongDivisionSupported)
            {
                HideLongDivision();
                return;
            }

            _longDivisionDrawable.Result =
                divisionResult;

            UpdateLongDivisionHeight();

            LongDivisionBorder.IsVisible =
                true;

            LongDivisionGraphicsView.Invalidate();
        }
        catch (OverflowException)
        {
            // LongDivisionCalculator hiện dùng long.Parse().
            // Nếu dữ liệu vượt Int64 thì chỉ ẩn hình đặt tính,
            // không làm ứng dụng bị dừng.
            HideLongDivision();
        }
        catch (FormatException)
        {
            HideLongDivision();
        }
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