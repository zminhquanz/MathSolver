using MathSolver.Controls;
using MathSolver.Graphics;
using MathSolver.Models;
using MathSolver.Numerics;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace MathSolver.Views;

public partial class CalculationPage : ContentPage
{
    private int _mainTabAnimationVersion;

    private readonly LongDivisionDrawable _longDivisionDrawable = new();

    private readonly BasicArithmeticEngine _arithmeticEngine = new();

    private ArithmeticOperation _selectedOperation = ArithmeticOperation.Add;

    private bool _isExpressionMode;

    private NumberInputType _selectedNumberType = NumberInputType.Integer;

    private bool _isUpdatingNumberText;

    private bool _isUpdatingArithmeticExpressionText;

    private bool _isArithmeticExpressionDisplayCompacted;

    private string _arithmeticExpressionRawText =
        string.Empty;

    private bool _isUpdatingLongDivisionMode;

    private bool? _isCompactOperationLayout;

    private bool _lastAppliedShowFullNumbers;

    // Khi Entry mất focus và số quá dài, giá trị chính xác được giữ
    // bằng dạng khoa học dùng chữ e, ví dụ: 1e18.
    private readonly Dictionary<Entry, string> _entryScientificCodeValues = new();

    // Ghi nhớ TextChanged do chương trình khôi phục OldTextValue tạo ra,
    // để thông báo lỗi phạm vi không bị ẩn ngay sau khi vừa hiển thị.
    private readonly Dictionary<Entry, string> _pendingRestoredEntryTexts = new();

    // Trên giao diện, số có hơn 18 chữ số sẽ được rút gọn thành
    // dạng a × 10ⁿ để không làm vỡ bố cục.
    private const int ScientificDisplayDigitThreshold = 18;
    private const int ScientificDisplaySignificantDigits = 12;

    // Int128 có tối đa 39 chữ số và phải được kiểm tra theo đúng
    // cận dưới/cận trên, không chỉ theo số lượng chữ số.
    private const int MaxIntegerInputDigits = 39;

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

    // Chỉ cho phép tối đa 10 chữ số sau dấu chấm.
    // Dấu phẩy chỉ dùng để phân nhóm hàng nghìn.
    private const int MaxDecimalPlaces = 10;

    private enum InputValidationError
    {
        None,
        InvalidFormat,
        OutOfRange
    }

    private CalculationSubTab _selectedSubTab = CalculationSubTab.Basic;

    private bool _isSubTabTransitioning;
    private bool _isPowerRootCalculationInteractionLocked;

    private const double CalculationSubTabSpacing =
        6d;

    private LongDivisionDisplayMode _longDivisionDisplayMode = LongDivisionDisplayMode.Elementary;

    private decimal _currentDivisionDividend;
    private decimal _currentDivisionDivisor;

    public CalculationPage()
    {
        InitializeComponent();

        _lastAppliedShowFullNumbers =
            ResultNumberDisplayMode.ShowFullNumbers;

        InteractiveButtonAnimation.SetIsScopeEnabled(
            this,
            true);

        LocalizationService.Attach(
            this);

        LongDivisionGraphicsView.Drawable =
            _longDivisionDrawable;

        FirstNumberEntry.Focused +=
            OnNumberEntryFocused;

        FirstNumberEntry.Unfocused +=
            OnNumberEntryUnfocused;

        SecondNumberEntry.Focused +=
            OnNumberEntryFocused;

        SecondNumberEntry.Unfocused +=
            OnNumberEntryUnfocused;

        CalculationSubTabScrollView.SizeChanged +=
            OnCalculationSubTabScrollViewSizeChanged;

        PowerRootSolverView.CalculationInteractionLockChanged +=
            OnPowerRootCalculationInteractionLockChanged;

        SelectNumberType(
            NumberInputType.Integer,
            clearInputs: false);

        SelectOperation(
            ArithmeticOperation.Add);

        SelectSubTab(
            CalculationSubTab.Basic);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        LiveWallpaper.Resume();

        // Main page luôn là nguồn sự thật cuối cùng cho Shell TabBar. Nếu
        // WinUI vừa hoàn tất một Settings Pop theo thứ tự native bất thường,
        // re-assert này sửa chrome ngay trong lifecycle của trang chính.
        Shell.SetTabBarIsVisible(
            this,
            true);

        OnCalculationSubTabScrollViewSizeChanged(
            CalculationSubTabScrollView,
            EventArgs.Empty);

        BeginMainTabTransitionIfPending();

        UpdateNumberTypeButtonStyles();

        RefreshNumberDisplaysIfSettingChanged();

        LongDivisionGraphicsView.Invalidate();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0d)
        {
            return;
        }

        bool compact = width < 720d;

        if (_isCompactOperationLayout == compact)
        {
            return;
        }

        _isCompactOperationLayout = compact;
        ConfigureOperationButtonsLayout(compact);
    }

    private void ConfigureOperationButtonsLayout(bool compact)
    {
        OperationButtonsGrid.ColumnDefinitions.Clear();
        OperationButtonsGrid.RowDefinitions.Clear();

#if ANDROID
        // Material/mobile layout mirrors FractionView: keep the four arithmetic
        // operators on one row and let Expression own the complete second row.
        // Do not reuse the old 3-column compact desktop fallback on Android.
        if (compact)
        {
            for (int index = 0; index < 4; index++)
            {
                OperationButtonsGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = GridLength.Star,
                    });
            }

            OperationButtonsGrid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto,
                });

            OperationButtonsGrid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto,
                });

            Grid.SetColumn(AddButton, 0);
            Grid.SetColumnSpan(AddButton, 1);
            Grid.SetRow(AddButton, 0);

            Grid.SetColumn(SubtractButton, 1);
            Grid.SetColumnSpan(SubtractButton, 1);
            Grid.SetRow(SubtractButton, 0);

            Grid.SetColumn(MultiplyButton, 2);
            Grid.SetColumnSpan(MultiplyButton, 1);
            Grid.SetRow(MultiplyButton, 0);

            Grid.SetColumn(DivideButton, 3);
            Grid.SetColumnSpan(DivideButton, 1);
            Grid.SetRow(DivideButton, 0);

            Grid.SetColumn(ExpressionButton, 0);
            Grid.SetColumnSpan(ExpressionButton, 4);
            Grid.SetRow(ExpressionButton, 1);

            OperationButtonsGrid.ColumnSpacing = 6d;
            OperationButtonsGrid.RowSpacing = 8d;
            return;
        }
#endif

        int columnCount = compact ? 3 : 5;

        for (int index = 0; index < columnCount; index++)
        {
            OperationButtonsGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = GridLength.Star,
                });
        }

        OperationButtonsGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto,
            });

        if (compact)
        {
            OperationButtonsGrid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto,
                });

            OperationButtonsGrid.RowSpacing = 8d;
            Grid.SetColumn(AddButton, 0);
            Grid.SetRow(AddButton, 0);
            Grid.SetColumn(SubtractButton, 1);
            Grid.SetRow(SubtractButton, 0);
            Grid.SetColumn(MultiplyButton, 2);
            Grid.SetRow(MultiplyButton, 0);
            Grid.SetColumn(DivideButton, 0);
            Grid.SetRow(DivideButton, 1);
            Grid.SetColumn(ExpressionButton, 1);
            Grid.SetColumnSpan(ExpressionButton, 2);
            Grid.SetRow(ExpressionButton, 1);
            return;
        }

        OperationButtonsGrid.RowSpacing = 0d;
        Button[] buttons =
        [
            AddButton,
            SubtractButton,
            MultiplyButton,
            DivideButton,
            ExpressionButton
        ];

        for (int index = 0; index < buttons.Length; index++)
        {
            Grid.SetColumn(buttons[index], index);
            Grid.SetColumnSpan(buttons[index], 1);
            Grid.SetRow(buttons[index], 0);
        }
    }

    protected override void OnDisappearing()
    {
        LiveWallpaper.Pause();

        // Hủy transition đang chạy ở trang sắp bị ẩn. Khi quay lại trang,
        // một phiếu mới sẽ tạo một animation mới thay vì nối tiếp animation cũ.
        _mainTabAnimationVersion++;

        CalculationPageContentRoot.CancelAnimations();
        ResetMainTabRoot();

        base.OnDisappearing();
    }

    private void BeginMainTabTransitionIfPending()
    {
        if (Shell.Current is not AppShell appShell ||
            !appShell.TryConsumeMainTabTransition(
                "CalculationPage",
                out int direction))
        {
            return;
        }

        int animationVersion =
            ++_mainTabAnimationVersion;

        direction =
            direction >= 0
                ? 1
                : -1;

        // Chuẩn bị ngay trong OnAppearing, trước frame đầu tiên của trang.
        // Không để trang hiện hoàn chỉnh rồi mới reset Opacity về 0.
        CalculationPageContentRoot.CancelAnimations();

        CalculationPageContentRoot.Opacity =
            0d;

        CalculationPageContentRoot.TranslationX =
            direction *
            44d;

        CalculationPageContentRoot.Scale =
            0.985d;

        Dispatcher.Dispatch(
            async () =>
                await PlayPreparedMainTabTransitionAsync(
                    animationVersion));
    }

    private async Task PlayPreparedMainTabTransitionAsync(
        int animationVersion)
    {
        // Nhường đúng một lượt cho layout nhưng root vẫn đang ẩn. Vì vậy
        // không có frame UI hoàn chỉnh xuất hiện trước transition.
        await Task.Yield();

        if (animationVersion !=
            _mainTabAnimationVersion)
        {
            return;
        }

        try
        {
            await Task.WhenAll(
                CalculationPageContentRoot.FadeToAsync(
                    1d,
                    175,
                    Easing.CubicOut),

                CalculationPageContentRoot.TranslateToAsync(
                    0d,
                    0d,
                    250,
                    Easing.CubicOut),

                CalculationPageContentRoot.ScaleToAsync(
                    1d,
                    250,
                    Easing.CubicOut));
        }
        finally
        {
            if (animationVersion ==
                _mainTabAnimationVersion)
            {
                ResetMainTabRoot();
            }
        }
    }

    private void ResetMainTabRoot()
    {
        CalculationPageContentRoot.Opacity =
            1d;

        CalculationPageContentRoot.TranslationX =
            0d;

        CalculationPageContentRoot.Scale =
            1d;
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        SelectOperation(ArithmeticOperation.Add);
    }

    private void OnSubtractClicked(object? sender, EventArgs e)
    {
        SelectOperation(ArithmeticOperation.Subtract);
    }

    private void OnMultiplyClicked(object? sender, EventArgs e)
    {
        SelectOperation(ArithmeticOperation.Multiply);
    }

    private void OnDivideClicked(object? sender, EventArgs e)
    {
        SelectOperation(ArithmeticOperation.Divide);
    }

    private void OnExpressionClicked(object? sender, EventArgs e)
    {
        _isExpressionMode = true;

        SelectionButtonStyler.Select(
            ExpressionButton,
            AddButton,
            SubtractButton,
            MultiplyButton,
            DivideButton,
            ExpressionButton);

        UpdateArithmeticInputModeUi();
        HideMessages();
        ArithmeticExpressionEditor.Focus();
    }

    private void SelectOperation(ArithmeticOperation operation)
    {
        _isExpressionMode = false;
        _selectedOperation = operation;

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

        SelectionButtonStyler.Select(
            selectedButton,
            AddButton,
            SubtractButton,
            MultiplyButton,
            DivideButton,
            ExpressionButton);

        UpdateArithmeticInputModeUi();
        HideMessages();
    }

    private void UpdateArithmeticInputModeUi()
    {
        NumberInputGrid.IsVisible = !_isExpressionMode;
        ExpressionInputPanel.IsVisible = _isExpressionMode;
        NumberInputTitleLabel.IsVisible = !_isExpressionMode;
        ExpressionInputTitleLabel.IsVisible = _isExpressionMode;
    }

    private void OnCalculateClicked(object? sender, EventArgs e)
    {
        HideMessages();

        if (_isExpressionMode)
        {
            CalculateArithmeticExpression();
            return;
        }

        if (_selectedNumberType ==
            NumberInputType.Integer)
        {
            CalculateIntegerValues();
            return;
        }
        else
        {
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

            if (_selectedOperation ==
                ArithmeticOperation.Divide)
            {
                if (secondNumber == 0)
                {
                    ShowDivisionByZeroError(
                        firstNumber);

                    SecondNumberEntry.Focus();
                    return;
                }

                _currentDivisionDividend =
                    firstNumber;

                _currentDivisionDivisor =
                    secondNumber;

                ShowDecimalDivisionResult(
                    firstNumber,
                    secondNumber);

                return;
            }

            QuadDouble result =
                CalculateDecimalResult(
                    firstNumber,
                    secondNumber);

            ShowResult(
                firstNumber,
                secondNumber,
                result);
        }
    }

    private void CalculateArithmeticExpression()
    {
        string expression =
            GetArithmeticExpressionInputText();

        try
        {
            if (_selectedNumberType == NumberInputType.Integer)
            {
                IntegerExpressionResult evaluation =
                    _arithmeticEngine.EvaluateIntegerExpression(expression);

                string resultText =
                    FormatIntegerExpressionResultForDisplay(
                        evaluation.ResultNumerator,
                        evaluation.ResultDenominator);

                ShowArithmeticExpressionResult(
                    evaluation.NormalizedExpression,
                    resultText,
                    evaluation.Steps);

                return;
            }

            DecimalExpressionResult decimalEvaluation =
                _arithmeticEngine.EvaluateDecimalExpression(expression);

            ShowArithmeticExpressionResult(
                decimalEvaluation.NormalizedExpression,
                FormatNumberForDisplay(decimalEvaluation.Result),
                decimalEvaluation.Steps);
        }
        catch (ArithmeticExpressionException exception)
        {
            ShowError(
                GetArithmeticExpressionErrorMessage(
                    exception.Error));

            ArithmeticExpressionEditor.Focus();
        }
    }

    private void ShowArithmeticExpressionResult(
        string normalizedExpression,
        string resultText,
        IReadOnlyList<string> steps)
    {
        string displayExpression =
            FormatArithmeticExpressionForDisplay(
                normalizedExpression);

        ExpressionLabel.Text =
            $"{displayExpression} = {resultText}";

        ResultLabel.Text =
            resultText;

        string stepText =
            steps.Count == 0
                ? LocalizationService.TranslateKey(
                    "Calculation.Expression.SingleValue")
                : string.Join(
                    Environment.NewLine,
                    steps.Select((step, index) =>
                        $"{index + 1}. " +
                        FormatArithmeticExpressionForDisplay(
                            step)));

        ExplanationLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.TranslateKey(
                    "Calculation.Expression.SolutionSteps"),
                stepText,
                resultText);

        AdditionalLabel.Text =
            LocalizationService.TranslateKey(
                "Calculation.Expression.ResultRules");

        DivisionDetailBorder.IsVisible = false;
        ResultBorder.IsVisible = true;
        _currentDivisionDividend = 0;
        _currentDivisionDivisor = 0;
        HideLongDivision();
    }

    private static string GetArithmeticExpressionErrorMessage(
        ArithmeticExpressionError error)
    {
        string key = error switch
        {
            ArithmeticExpressionError.Empty =>
                "Calculation.Expression.Error.Empty",
            ArithmeticExpressionError.TooLong =>
                "Calculation.Expression.Error.TooLong",
            ArithmeticExpressionError.InvalidCharacter =>
                "Calculation.Expression.Error.InvalidCharacter",
            ArithmeticExpressionError.InvalidNumber =>
                "Calculation.Expression.Error.InvalidNumber",
            ArithmeticExpressionError.NumberOutOfRange =>
                "Calculation.Expression.Error.NumberOutOfRange",
            ArithmeticExpressionError.MissingOperand =>
                "Calculation.Expression.Error.MissingOperand",
            ArithmeticExpressionError.MissingOperator =>
                "Calculation.Expression.Error.MissingOperator",
            ArithmeticExpressionError.MismatchedBracket =>
                "Calculation.Expression.Error.MismatchedBracket",
            ArithmeticExpressionError.InvalidBracketOrder =>
                "Calculation.Expression.Error.InvalidBracketOrder",
            ArithmeticExpressionError.DivisionByZero =>
                "Calculation.Expression.Error.DivisionByZero",
            ArithmeticExpressionError.NonIntegralDivision =>
                "Calculation.Expression.Error.NonIntegralDivision",
            _ =>
                "Calculation.Expression.Error.InvalidCharacter"
        };

        return LocalizationService.TranslateKey(key);
    }

    private void CalculateIntegerValues()
    {
        if (!TryReadIntegerInput(
                GetEntryInputText(
                    FirstNumberEntry),
                "Vui lòng nhập số thứ nhất.",
                out Int128 firstNumber))
        {
            FirstNumberEntry.Focus();
            return;
        }

        if (!TryReadIntegerInput(
                GetEntryInputText(
                    SecondNumberEntry),
                "Vui lòng nhập số thứ hai.",
                out Int128 secondNumber))
        {
            SecondNumberEntry.Focus();
            return;
        }

        ApplyIntegerEntryDisplayValue(
            FirstNumberEntry,
            firstNumber);

        ApplyIntegerEntryDisplayValue(
            SecondNumberEntry,
            secondNumber);

        if (_selectedOperation ==
            ArithmeticOperation.Divide)
        {
            if (secondNumber == Int128.Zero)
            {
                ShowDivisionByZeroError(
                    firstNumber);

                SecondNumberEntry.Focus();
                return;
            }

            ShowIntegerDivisionResult(
                firstNumber,
                secondNumber);

            return;
        }

        BigInteger first =
            (BigInteger)firstNumber;

        BigInteger second =
            (BigInteger)secondNumber;

        IntegerArithmeticResult calculation =
            _arithmeticEngine.CalculateInteger(
                new IntegerArithmeticExpression(
                    first,
                    _selectedOperation,
                    second));

        ShowIntegerResult(
            first,
            second,
            calculation.Result);
    }

    private void ShowDivisionByZeroError(
        Int128 firstNumber)
    {
        string firstText =
            FormatIntegerForDisplay(
                (BigInteger)firstNumber);

        ErrorLabel.Text =
            "Bạn không thể chia cho 0.";

        ErrorBorder.IsVisible =
            true;

        ExpressionLabel.Text =
            $"{firstText} ÷ 0";

        ResultLabel.Text =
            "Không xác định";

        ExplanationLabel.Text =
            $"Không thể thực hiện phép tính {firstText} ÷ 0.\n" +
            "Trong toán học, phép chia cho 0 không được xác định.\n" +
            "Bạn không thể chia một số cho 0.";

        ResultBorder.IsVisible =
            true;

        DivisionDetailBorder.IsVisible =
            false;

        _currentDivisionDividend =
            0;

        _currentDivisionDivisor =
            0;

        HideLongDivision();
    }

    private void ShowIntegerResult(
        BigInteger firstNumber,
        BigInteger secondNumber,
        BigInteger result)
    {
        string firstText =
            FormatIntegerForDisplay(
                firstNumber);

        string secondText =
            FormatIntegerForDisplay(
                secondNumber);

        string resultText =
            FormatIntegerForDisplay(
                result);

        string operationSymbol =
            GetOperationSymbol();

        ExpressionLabel.Text =
            $"{firstText} {operationSymbol} " +
            $"{secondText} = {resultText}";

        ResultLabel.Text =
            resultText;

        ExplanationLabel.Text =
            CreateIntegerExplanation(
                firstText,
                secondText,
                resultText);

        AdditionalLabel.Text =
            CreateIntegerAdditional(
                firstNumber,
                secondNumber,
                result);

        DivisionDetailBorder.IsVisible =
            false;

        ResultBorder.IsVisible =
            true;

        _currentDivisionDividend =
            0;

        _currentDivisionDivisor =
            0;

        HideLongDivision();
    }

    private void ShowIntegerDivisionResult(
        Int128 dividend,
        Int128 divisor)
    {
        BigInteger dividendInteger =
            (BigInteger)dividend;

        BigInteger divisorInteger =
            (BigInteger)divisor;

        IntegerArithmeticResult calculation =
            _arithmeticEngine.CalculateInteger(
                new IntegerArithmeticExpression(
                    dividendInteger,
                    ArithmeticOperation.Divide,
                    divisorInteger));

        BigInteger quotient =
            calculation.Result;

        BigInteger remainder =
            calculation.Remainder;

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

        AdditionalLabel.Text =
            CreateIntegerAdditional(
                dividendInteger,
                divisorInteger,
                quotient);

        PreferElementaryLongDivisionMode();

        if (CanRenderLongDivision(
                dividend,
                divisor))
        {
            _currentDivisionDividend =
                (long)dividend;

            _currentDivisionDivisor =
                (long)divisor;

            ShowLongDivision(
                _currentDivisionDividend,
                _currentDivisionDivisor);
        }
        else
        {
            _currentDivisionDividend =
                0;

            _currentDivisionDivisor =
                0;

            HideLongDivision();
        }
    }

    private string CreateIntegerExplanation(
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

            _ =>
                string.Empty
        };
    }

    private string CreateIntegerAdditional(
        BigInteger firstNumber,
        BigInteger secondNumber,
        BigInteger result)
    {
        string firstText =
            FormatIntegerForDisplay(
                firstNumber);

        string secondText =
            FormatIntegerForDisplay(
                secondNumber);

        string resultText =
            FormatIntegerForDisplay(
                result);

        return _selectedOperation switch
        {
            ArithmeticOperation.Add =>
                BuildAdditionalText(
                    "Phép cộng có tính giao hoán và kết hợp.",
                    secondNumber.IsZero
                        ? "Đang cộng với 0 nên giá trị không thay đổi."
                        : "Hai số nguyên được cộng theo đúng giá trị hàng.",
                    "Cộng các chữ số cùng hàng từ phải sang trái và nhớ sang hàng kế tiếp khi cần.",
                    $"{firstText} + {secondText} = {resultText}"),

            ArithmeticOperation.Subtract =>
                BuildAdditionalText(
                    "Phép trừ là phép toán ngược của phép cộng.",
                    secondNumber.IsZero
                        ? "Đang trừ đi 0 nên giá trị không thay đổi."
                        : "Lấy số bị trừ bớt đi số trừ.",
                    "Trừ các chữ số cùng hàng từ phải sang trái và mượn ở hàng kế tiếp khi cần.",
                    $"{firstText} − {secondText} = {resultText}"),

            ArithmeticOperation.Multiply =>
                BuildAdditionalText(
                    "Phép nhân có tính giao hoán, kết hợp và phân phối đối với phép cộng.",
                    firstNumber.IsZero ||
                    secondNumber.IsZero
                        ? "Có một thừa số bằng 0 nên tích bằng 0."
                        : firstNumber.IsOne ||
                          secondNumber.IsOne
                            ? "Có một thừa số bằng 1 nên tích bằng thừa số còn lại."
                            : "Tích được tạo bởi phép cộng lặp lại theo các hàng.",
                    "Nhân lần lượt từng chữ số rồi cộng các tích riêng đã dịch đúng vị trí.",
                    $"{firstText} × {secondText} = {resultText}"),

            ArithmeticOperation.Divide =>
                BuildAdditionalText(
                    "Phép chia là phép toán ngược của phép nhân.",
                    "Số bị chia được tách thành thương và số dư.",
                    "Số dư luôn có giá trị tuyệt đối nhỏ hơn số chia.",
                    $"{firstText} ÷ {secondText} = {resultText}"),

            _ =>
                string.Empty
        };
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
        DivisionDetailBorder.IsVisible = false;

        _currentDivisionDividend = 0;
        _currentDivisionDivisor = 0;

        HideLongDivision();
    }

    private void ShowDecimalDivisionResult(
        decimal dividend,
        decimal divisor)
    {
        QuadDouble result =
            _arithmeticEngine.CalculateDecimal(
                dividend,
                ArithmeticOperation.Divide,
                divisor);

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

        PreferElementaryLongDivisionMode();

        ShowLongDivision(
            dividend,
            divisor);

        AdditionalLabel.Text =
            CreateAdditional(
                dividend,
                divisor,
                result);
    }

    private bool TryReadIntegerInput(
        string? input,
        string emptyMessage,
        out Int128 number)
    {
        number =
            Int128.Zero;

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

        if (!TryParseInt128Input(
                normalizedInput,
                out number))
        {
            ShowError(
                $"Giá trị \"{input}\" phải là số nguyên hợp lệ " +
                $"trong phạm vi từ {Int128InputRangeText}.");

            return false;
        }

        return true;
    }

    private static bool TryParseInt128Input(
        string text,
        out Int128 value)
    {
        value =
            Int128.Zero;

        if (!text.Contains(
                'e'))
        {
            return Int128.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        if (!TryParseScientificInteger(
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

        return true;
    }

    private static bool TryParseScientificInteger(
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

    private static int CountIntegerDigits(
        Int128 value)
    {
        return BigInteger.Abs(
                (BigInteger)value)
            .ToString(
                CultureInfo.InvariantCulture)
            .Length;
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

        if (!isValid ||
            number < MinDecimalInputValue ||
            number > MaxDecimalInputValue)
        {
            ShowError(
                $"Giá trị \"{input}\" phải nằm trong phạm vi " +
                $"decimal từ {DecimalInputRangeText}.");

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

    private QuadDouble CalculateDecimalResult(
        decimal firstNumber,
        decimal secondNumber)
    {
        return _arithmeticEngine.CalculateDecimal(
            firstNumber,
            _selectedOperation,
            secondNumber);
    }

    private void ShowResult(
        decimal firstNumber,
        decimal secondNumber,
        QuadDouble result)
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

        _currentDivisionDividend = 0;
        _currentDivisionDivisor = 0;

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
        QuadDouble result)
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

    private static string FormatIntegerExpressionResultForDisplay(
        BigInteger numerator,
        BigInteger denominator)
    {
        string plainText =
            RationalDecimalFormatter.Format(
                numerator,
                denominator,
                MaxDecimalPlaces);

        return
            IntegerInputFormatter
                .AddThousandsSeparatorsToPlainNumber(
                    plainText);
    }

    private string FormatOperationResult(
        decimal firstNumber,
        decimal secondNumber,
        QuadDouble result)
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
                    MaxDecimalPlaces
            };

        displayDecimalPlaces =
            Math.Clamp(
                displayDecimalPlaces,
                0,
                MaxDecimalPlaces);

        return FormatQuadDoubleForDisplay(
            result,
            displayDecimalPlaces);
    }

    private static string FormatNumberForDisplay(
        QuadDouble number)
    {
        return FormatQuadDoubleForDisplay(
            number);
    }

    private static string FormatNumberForDisplay(
        OctoDouble number)
    {
        if (!number.IsFinite)
        {
            return number.ToGeneralString();
        }

        if (number.IsZero)
        {
            return "0";
        }

        double approximateValue =
            Math.Abs(
                number.ToDouble());

        int exponent =
            (int)Math.Floor(
                Math.Log10(
                    approximateValue));

        bool useScientificNotation =
            !ResultNumberDisplayMode.ShowFullNumbers &&
            (exponent >=
                 ScientificDisplayDigitThreshold ||
             exponent <=
                 -MaxDecimalPlaces);

        int significantDigits =
            useScientificNotation
                ? ScientificDisplaySignificantDigits
                : Math.Clamp(
                    exponent +
                    1 +
                    MaxDecimalPlaces,
                    1,
                    OctoDouble.SignificantDigits);

        string text =
            number.ToGeneralString(
                significantDigits,
                ResultNumberDisplayMode.ShowFullNumbers
                    ? int.MaxValue
                    : ScientificDisplayDigitThreshold,
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

        return
            IntegerInputFormatter
                .AddThousandsSeparatorsToPlainNumber(
                    text);
    }

    private static string FormatQuadDoubleForDisplay(
        QuadDouble number,
        int? fixedDecimalPlaces = null)
    {
        if (!number.IsFinite)
        {
            return number.ToGeneralString();
        }

        if (number.IsZero)
        {
            return fixedDecimalPlaces > 0
                ? "0." +
                  new string(
                      '0',
                      fixedDecimalPlaces.Value)
                : "0";
        }

        double approximateValue =
            Math.Abs(
                number.ToDouble());

        int exponent =
            (int)Math.Floor(
                Math.Log10(
                    approximateValue));

        bool useScientificNotation =
            !ResultNumberDisplayMode.ShowFullNumbers &&
            (exponent >=
                 ScientificDisplayDigitThreshold ||
             exponent <=
                 -MaxDecimalPlaces);

        int decimalPlaces =
            fixedDecimalPlaces ??
            MaxDecimalPlaces;

        int significantDigits =
            useScientificNotation
                ? ScientificDisplaySignificantDigits
                : Math.Clamp(
                    exponent +
                    1 +
                    decimalPlaces,
                    1,
                    QuadDouble.SignificantDigits);

        string text =
            number.ToGeneralString(
                significantDigits,
                ResultNumberDisplayMode.ShowFullNumbers
                    ? int.MaxValue
                    : ScientificDisplayDigitThreshold,
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

        if (fixedDecimalPlaces.HasValue)
        {
            fractionPart =
                fractionPart.PadRight(
                    fixedDecimalPlaces.Value,
                    '0');

            if (fractionPart.Length >
                fixedDecimalPlaces.Value)
            {
                fractionPart =
                    fractionPart[..fixedDecimalPlaces.Value];
            }
        }

        string plainText =
            (isNegative ? "-" : string.Empty) +
            integerPart +
            (fractionPart.Length > 0
                ? $".{fractionPart}"
                : string.Empty);

        return
            IntegerInputFormatter
                .AddThousandsSeparatorsToPlainNumber(
                    plainText);
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

        return !ResultNumberDisplayMode.ShowFullNumbers &&
               CountNumericDigits(
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

        if (ResultNumberDisplayMode.ShowFullNumbers ||
            CountNumericDigits(
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

    private async void OnBasicCopyResultClicked(
        object? sender,
        EventArgs e)
    {
        await ResultClipboardService.CopyAsync(
            BasicCopyResultButton,
            ResultLabel.Text);
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        _pendingRestoredEntryTexts.Clear();
        _entryScientificCodeValues.Clear();

        FirstNumberEntry.Text = string.Empty;
        SecondNumberEntry.Text = string.Empty;
        ClearArithmeticExpressionInput();

        HideMessages();

        if (_isExpressionMode)
        {
            ArithmeticExpressionEditor.Focus();
        }
        else
        {
            FirstNumberEntry.Focus();
        }
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

    private void OnIntegerTypeClicked(
    object? sender,
    EventArgs e)
    {
        SelectNumberType(
            NumberInputType.Integer);
    }

    private void OnDecimalTypeClicked(
        object? sender,
        EventArgs e)
    {
        SelectNumberType(
            NumberInputType.Decimal);
    }

    private void SelectNumberType(
        NumberInputType numberType,
        bool clearInputs = true)
    {
        bool typeChanged =
            _selectedNumberType !=
            numberType;

        _selectedNumberType =
            numberType;

        UpdateNumberTypeButtonStyles();

        if (numberType ==
            NumberInputType.Integer)
        {

            FirstNumberEntry.Placeholder =
                "Ví dụ: 100,000";

            SecondNumberEntry.Placeholder =
                "Ví dụ: 25,000";
        }
        else
        {

            FirstNumberEntry.Placeholder =
                "Ví dụ: 2,500.75";

            SecondNumberEntry.Placeholder =
                "Ví dụ: 1,250.5";
        }

        // Nhấn lại đúng loại số đang chọn thì không xóa dữ liệu.
        if (!clearInputs ||
            !typeChanged)
        {
            return;
        }

        _entryScientificCodeValues.Clear();
        _pendingRestoredEntryTexts.Clear();

        FirstNumberEntry.Text =
            string.Empty;

        SecondNumberEntry.Text =
            string.Empty;

        ClearArithmeticExpressionInput();

        HideMessages();

        if (_isExpressionMode)
        {
            ArithmeticExpressionEditor.Focus();
        }
        else
        {
            FirstNumberEntry.Focus();
        }
    }

    private void OnArithmeticExpressionTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (_isUpdatingArithmeticExpressionText)
        {
            return;
        }

        HideMessages();

        string newText =
            e.NewTextValue ??
            string.Empty;

        int cursorPosition =
            Math.Clamp(
                ArithmeticExpressionEditor.CursorPosition,
                0,
                newText.Length);

        int logicalPosition =
            IntegerInputFormatter.CountLogicalCharacters(
                newText,
                cursorPosition);

        string formattedText =
            FormatArithmeticExpressionWhileTyping(
                newText);

        _arithmeticExpressionRawText =
            RemoveArithmeticExpressionGrouping(
                formattedText);

        _isArithmeticExpressionDisplayCompacted =
            false;

        if (string.Equals(
                formattedText,
                newText,
                StringComparison.Ordinal))
        {
            return;
        }

        SetArithmeticExpressionEditorText(
            formattedText,
            IntegerInputFormatter.FindCursorPosition(
                formattedText,
                logicalPosition));
    }

    private void OnArithmeticExpressionFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (!_isArithmeticExpressionDisplayCompacted)
        {
            return;
        }

        _isArithmeticExpressionDisplayCompacted =
            false;

        SetArithmeticExpressionEditorText(
            FormatArithmeticExpressionWhileTyping(
                _arithmeticExpressionRawText));
    }

    private void OnArithmeticExpressionUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        CompactArithmeticExpressionEditorDisplay();
    }

    private string GetArithmeticExpressionInputText()
    {
        if (!_isArithmeticExpressionDisplayCompacted)
        {
            _arithmeticExpressionRawText =
                RemoveArithmeticExpressionGrouping(
                    ArithmeticExpressionEditor.Text ??
                    string.Empty);
        }

        return _arithmeticExpressionRawText;
    }

    private void CompactArithmeticExpressionEditorDisplay()
    {
        string rawExpression =
            GetArithmeticExpressionInputText();

        if (string.IsNullOrWhiteSpace(
                rawExpression))
        {
            return;
        }

        _isArithmeticExpressionDisplayCompacted =
            true;

        SetArithmeticExpressionEditorText(
            FormatArithmeticExpressionForDisplay(
                rawExpression));
    }

    private void ClearArithmeticExpressionInput()
    {
        _arithmeticExpressionRawText =
            string.Empty;

        _isArithmeticExpressionDisplayCompacted =
            false;

        SetArithmeticExpressionEditorText(
            string.Empty);
    }

    private void SetArithmeticExpressionEditorText(
        string text,
        int? cursorPosition = null)
    {
        _isUpdatingArithmeticExpressionText =
            true;

        try
        {
            ArithmeticExpressionEditor.Text =
                text;

            ArithmeticExpressionEditor.CursorPosition =
                Math.Clamp(
                    cursorPosition ??
                    text.Length,
                    0,
                    text.Length);

            ArithmeticExpressionEditor.SelectionLength =
                0;
        }
        finally
        {
            _isUpdatingArithmeticExpressionText =
                false;
        }
    }

    private static string FormatArithmeticExpressionWhileTyping(
        string text)
    {
        string source =
            RemoveArithmeticExpressionGrouping(
                text);

        var builder =
            new StringBuilder(
                source.Length +
                source.Length / 4);

        for (int index = 0;
             index < source.Length;)
        {
            if (!char.IsDigit(
                    source[index]))
            {
                builder.Append(
                    source[index]);

                index++;
                continue;
            }

            int startIndex =
                index;

            int decimalPointCount =
                0;

            while (index < source.Length &&
                   (char.IsDigit(
                        source[index]) ||
                    source[index] == '.'))
            {
                if (source[index] == '.')
                {
                    decimalPointCount++;
                }

                index++;
            }

            string numberText =
                source[startIndex..index];

            builder.Append(
                decimalPointCount <= 1
                    ? IntegerInputFormatter.FormatWhileTyping(
                        numberText,
                        allowDecimal: true)
                    : numberText);
        }

        return builder.ToString();
    }

    private string FormatArithmeticExpressionForDisplay(
        string expression)
    {
        string source =
            RemoveArithmeticExpressionGrouping(
                expression);

        var builder =
            new StringBuilder(
                source.Length +
                source.Length / 4);

        for (int index = 0;
             index < source.Length;)
        {
            if (!char.IsDigit(
                    source[index]))
            {
                builder.Append(
                    source[index]);

                index++;
                continue;
            }

            int startIndex =
                index;

            int decimalPointCount =
                0;

            while (index < source.Length &&
                   (char.IsDigit(
                        source[index]) ||
                    source[index] == '.'))
            {
                if (source[index] == '.')
                {
                    decimalPointCount++;
                }

                index++;
            }

            if (index < source.Length &&
                source[index] is 'e' or 'E')
            {
                int exponentEndIndex =
                    index + 1;

                if (exponentEndIndex < source.Length &&
                    source[exponentEndIndex] is '+' or '-')
                {
                    exponentEndIndex++;
                }

                int exponentDigitStart =
                    exponentEndIndex;

                while (exponentEndIndex < source.Length &&
                       char.IsDigit(
                           source[exponentEndIndex]))
                {
                    exponentEndIndex++;
                }

                if (exponentEndIndex >
                    exponentDigitStart)
                {
                    index =
                        exponentEndIndex;
                }
            }

            string numberText =
                source[startIndex..index];

            builder.Append(
                FormatArithmeticNumberForDisplay(
                    numberText,
                    decimalPointCount));
        }

        return builder.ToString();
    }

    private string FormatArithmeticNumberForDisplay(
        string numberText,
        int decimalPointCount)
    {
        if (_selectedNumberType ==
                NumberInputType.Integer &&
            decimalPointCount == 0 &&
            !numberText.Contains(
                "e",
                StringComparison.OrdinalIgnoreCase) &&
            BigInteger.TryParse(
                numberText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out BigInteger integerValue))
        {
            return FormatIntegerForDisplay(
                integerValue);
        }

        if (_selectedNumberType ==
                NumberInputType.Integer &&
            decimalPointCount == 1 &&
            !numberText.Contains(
                "e",
                StringComparison.OrdinalIgnoreCase))
        {
            // Integer-expression divisions are evaluated as exact rationals.
            // Keep terminating decimals exactly as produced by the engine;
            // repeating decimals have already been rounded to 10 places.
            return
                IntegerInputFormatter
                    .AddThousandsSeparatorsToPlainNumber(
                        numberText);
        }

        if (decimalPointCount <= 1 &&
            OctoDouble.TryParse(
                numberText,
                out OctoDouble decimalValue) &&
            decimalValue.IsFinite)
        {
            return FormatNumberForDisplay(
                decimalValue);
        }

        return IntegerInputFormatter.FormatWhileTyping(
            numberText,
            allowDecimal: true);
    }

    private static string RemoveArithmeticExpressionGrouping(
        string text)
    {
        return text.Replace(
            ",",
            string.Empty,
            StringComparison.Ordinal);
    }

    private void UpdateNumberTypeButtonStyles()
    {
        Button selectedButton =
            _selectedNumberType ==
            NumberInputType.Integer
                ? IntegerTypeButton
                : DecimalTypeButton;

        SelectionButtonStyler.Select(
            selectedButton,
            IntegerTypeButton,
            DecimalTypeButton);
    }

    private string? GetEntryInputText(
        Entry entry)
    {
        if (_entryScientificCodeValues.TryGetValue(
                entry,
                out string? scientificCode) &&
            !string.IsNullOrEmpty(
                scientificCode))
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
                out string? scientificCode) ||
            string.IsNullOrEmpty(
                scientificCode))
        {
            return;
        }

        if (_selectedNumberType ==
            NumberInputType.Integer)
        {
            if (!TryParseInt128Input(
                    scientificCode,
                    out Int128 integerValue))
            {
                return;
            }

            _entryScientificCodeValues.Remove(
                entry);

            SetEntryTextWithoutValidation(
                entry,
                FormatInteger(
                    (BigInteger)integerValue));

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

        _entryScientificCodeValues.Remove(
            entry);

        SetEntryTextWithoutValidation(
            entry,
            FormatNumber(
                decimalValue));
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

        if (_selectedNumberType ==
            NumberInputType.Integer)
        {
            if (!TryParseInt128Input(
                    normalizedText,
                    out Int128 integerValue))
            {
                return;
            }

            ApplyIntegerEntryDisplayValue(
                entry,
                integerValue);

            return;
        }

        if (!decimal.TryParse(
                normalizedText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal decimalValue))
        {
            return;
        }

        string standardText =
            FormatNumber(
                decimalValue);

        if (CountNumericDigits(
                standardText) <=
            ScientificDisplayDigitThreshold)
        {
            _entryScientificCodeValues.Remove(
                entry);

            SetEntryTextWithoutValidation(
                entry,
                standardText);

            return;
        }

        _entryScientificCodeValues[entry] =
            FormatScientificForCode(
                decimalValue);

        SetEntryTextWithoutValidation(
            entry,
            FormatScientificForDisplay(
                decimalValue));
    }

    private void ApplyIntegerEntryDisplayValue(
        Entry entry,
        Int128 value)
    {
        BigInteger bigValue =
            (BigInteger)value;

        if (CountIntegerDigits(
                value) <=
            ScientificDisplayDigitThreshold)
        {
            _entryScientificCodeValues.Remove(
                entry);

            SetEntryTextWithoutValidation(
                entry,
                FormatInteger(
                    bigValue));

            return;
        }

        _entryScientificCodeValues[entry] =
            FormatIntegerScientificForCode(
                bigValue);

        SetEntryTextWithoutValidation(
            entry,
            FormatIntegerForDisplay(
                bigValue));
    }

    private static string FormatIntegerScientificForCode(
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

    private void SetEntryTextWithoutValidation(
        Entry entry,
        string text,
        int? cursorPosition = null)
    {
        _isUpdatingNumberText =
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

        _isUpdatingNumberText =
            false;
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
            e.NewTextValue ??
            string.Empty;

        // SetEntryTextWithoutValidation có thể phát sinh thêm TextChanged.
        // Bỏ qua đúng sự kiện khôi phục để ErrorBorder không biến mất.
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

        if (_isUpdatingNumberText)
        {
            return;
        }

        _entryScientificCodeValues.Remove(
            entry);

        InputValidationError validationError =
            ValidateInputWhileTyping(
                newText);

        if (validationError !=
            InputValidationError.None)
        {
            string oldText =
                e.OldTextValue ??
                string.Empty;

            if (validationError ==
                InputValidationError.OutOfRange)
            {
                ShowInputRangeError();
            }
            else
            {
                ShowInputTypeError();
            }

            _pendingRestoredEntryTexts[entry] =
                oldText;

            SetEntryTextWithoutValidation(
                entry,
                oldText);

            return;
        }

        string formattedText =
            IntegerInputFormatter.FormatWhileTyping(
                newText,
                allowDecimal: true);

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
            IntegerInputFormatter.CountLogicalCharacters(
                newText,
                oldCursorPosition);

        SetEntryTextWithoutValidation(
            entry,
            formattedText,
            IntegerInputFormatter.FindCursorPosition(
                formattedText,
                logicalPosition));
    }

    private void ShowInputTypeError()
    {
        if (_selectedNumberType ==
            NumberInputType.Integer)
        {
            ErrorLabel.Text =
                "Số nguyên chỉ được chứa chữ số và một dấu âm ở đầu. " +
                "Dấu phẩy phân nhóm được ứng dụng thêm tự động.";
        }
        else
        {
            ErrorLabel.Text =
                $"Số thập phân chỉ được chứa chữ số, một dấu âm ở đầu, " +
                $"tối đa một dấu chấm và tối đa {MaxDecimalPlaces} chữ số " +
                "sau dấu chấm; dấu phẩy được thêm tự động.";
        }

        ErrorBorder.IsVisible = true;
        ResultBorder.IsVisible = false;
        DivisionDetailBorder.IsVisible = false;
    }

    private void ShowInputRangeError()
    {
        ErrorLabel.Text =
            _selectedNumberType ==
            NumberInputType.Integer
                ? $"Số nguyên phải nằm trong phạm vi từ {Int128InputRangeText}."
                : $"Số thập phân phải nằm trong phạm vi từ {DecimalInputRangeText}.";

        ErrorBorder.IsVisible = true;
        ResultBorder.IsVisible = false;
        DivisionDetailBorder.IsVisible = false;
    }

    private InputValidationError ValidateInputWhileTyping(
        string text)
    {
        if (string.IsNullOrEmpty(
                text))
        {
            return InputValidationError.None;
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
            return InputValidationError.InvalidFormat;
        }

        int startIndex =
            0;

        if (normalizedText[0] == '-')
        {
            startIndex =
                1;

            if (normalizedText.Length == 1)
            {
                return InputValidationError.None;
            }
        }

        if (_selectedNumberType ==
            NumberInputType.Integer)
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
                    return InputValidationError.InvalidFormat;
                }

                digitCount++;

                if (digitCount >
                    MaxIntegerInputDigits)
                {
                    return InputValidationError.OutOfRange;
                }
            }

            if (!BigInteger.TryParse(
                    normalizedText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out BigInteger integerValue))
            {
                return InputValidationError.InvalidFormat;
            }

            return integerValue <
                       MinInt128InputValue ||
                   integerValue >
                       MaxInt128InputValue
                ? InputValidationError.OutOfRange
                : InputValidationError.None;
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
                        return InputValidationError.InvalidFormat;
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

            return InputValidationError.InvalidFormat;
        }

        if (totalDigitCount == 0)
        {
            // Cho phép nhập tạm "." hoặc "-."; hàm format sẽ đổi thành 0.
            return normalizedText is "." or "-."
                ? InputValidationError.None
                : InputValidationError.InvalidFormat;
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
            return InputValidationError.OutOfRange;
        }

        return decimalValue <
                   MinDecimalInputValue ||
               decimalValue >
                   MaxDecimalInputValue
            ? InputValidationError.OutOfRange
            : InputValidationError.None;
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

        if (_selectedNumberType ==
                NumberInputType.Integer &&
            CountDigits(
                normalizedText) >
            MaxIntegerInputDigits)
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

        if (digitCount == 0)
        {
            return false;
        }

        if (_selectedNumberType ==
            NumberInputType.Decimal)
        {
            return decimal.TryParse(
                normalizedText,
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
        Int128 dividend,
        Int128 divisor)
    {
        return dividend >=
               (Int128)long.MinValue &&
               dividend <=
               (Int128)long.MaxValue &&
               divisor >=
               (Int128)long.MinValue &&
               divisor <=
               (Int128)long.MaxValue;
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
            int maximumDecimalPlaces =
                _longDivisionDisplayMode ==
                LongDivisionDisplayMode.Elementary
                    ? 0
                    : 8;

            LongDivisionResult divisionResult =
                LongDivisionCalculator.Calculate(
                    dividend,
                    divisor,
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

    private async void OnBasicTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchSubTabAsync(
            CalculationSubTab.Basic);
    }

    private async void OnAverageTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchSubTabAsync(
            CalculationSubTab.Average);
    }

    private async void OnPowerRootTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchSubTabAsync(
            CalculationSubTab.PowerRoot);
    }

    private async void OnFractionTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchSubTabAsync(
            CalculationSubTab.Fraction);
    }

    private async void OnFindXTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchSubTabAsync(
            CalculationSubTab.FindX);
    }

    private async void OnQuadraticTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchSubTabAsync(
            CalculationSubTab.Quadratic);
    }

    private async void OnGeometryTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchSubTabAsync(
            CalculationSubTab.Geometry);
    }

    private void RefreshNumberDisplaysIfSettingChanged()
    {
        bool showFullNumbers =
            ResultNumberDisplayMode.ShowFullNumbers;

        if (_lastAppliedShowFullNumbers ==
            showFullNumbers)
        {
            return;
        }

        _lastAppliedShowFullNumbers =
            showFullNumbers;

        RefreshAllNumberDisplays();
    }

    private void RefreshAllNumberDisplays()
    {
        if (_isExpressionMode &&
            !ArithmeticExpressionEditor.IsFocused)
        {
            CompactArithmeticExpressionEditorDisplay();
        }

        if (ResultBorder.IsVisible)
        {
            OnCalculateClicked(
                this,
                EventArgs.Empty);
        }

        AverageSolverView.RefreshNumberDisplay();
        FractionSolverView.RefreshNumberDisplay();
        FindXSolverView.RefreshNumberDisplay();
        QuadraticSolverView.RefreshNumberDisplay();
        GeometrySolverView.RefreshNumberDisplay();
    }

    private async Task SwitchSubTabAsync(
        CalculationSubTab selectedTab)
    {
        if (_isPowerRootCalculationInteractionLocked ||
            _isSubTabTransitioning)
        {
            return;
        }

        Button selectedButton =
            GetSubTabButton(
                selectedTab);

        if (_selectedSubTab == selectedTab)
        {
            await ScrollSubTabIntoViewAsync(
                selectedButton);

            return;
        }

        _isSubTabTransitioning =
            true;

        try
        {
            CalculationSubTab previousTab =
                _selectedSubTab;

            VisualElement outgoingContent =
                GetSubTabContent(
                    previousTab);

            VisualElement incomingContent =
                GetSubTabContent(
                    selectedTab);

            int direction =
                (int)selectedTab >
                (int)previousTab
                    ? 1
                    : -1;

            outgoingContent.CancelAnimations();
            incomingContent.CancelAnimations();

            _selectedSubTab =
                selectedTab;

            UpdateSubTabButtonStyles();

            incomingContent.IsVisible =
                true;

            incomingContent.Opacity =
                0d;

            incomingContent.TranslationX =
                direction *
                28d;

            incomingContent.Scale =
                0.995d;

            await Task.WhenAll(
                outgoingContent.FadeToAsync(
                    0d,
                    85,
                    Easing.CubicIn),

                outgoingContent.TranslateToAsync(
                    direction *
                    -18d,
                    0d,
                    85,
                    Easing.CubicIn));

            outgoingContent.IsVisible =
                false;

            outgoingContent.Opacity =
                1d;

            outgoingContent.TranslationX =
                0d;

            outgoingContent.Scale =
                1d;

            await Task.WhenAll(
                incomingContent.FadeToAsync(
                    1d,
                    150,
                    Easing.CubicOut),

                incomingContent.TranslateToAsync(
                    0d,
                    0d,
                    190,
                    Easing.CubicOut),

                incomingContent.ScaleToAsync(
                    1d,
                    190,
                    Easing.CubicOut));

            await ScrollSubTabIntoViewAsync(
                selectedButton);
        }
        finally
        {
            _isSubTabTransitioning =
                false;
        }
    }

    private void OnPowerRootCalculationInteractionLockChanged(
        bool isLocked)
    {
        _isPowerRootCalculationInteractionLocked =
            isLocked;

        Button[] desktopButtons =
        [
            BasicTabButton,
            AverageTabButton,
            PowerRootTabButton,
            FractionTabButton,
            FindXTabButton,
            QuadraticTabButton,
            GeometryTabButton
        ];

        Button[] androidButtons =
        [
            AndroidBasicTabButton,
            AndroidAverageTabButton,
            AndroidPowerRootTabButton,
            AndroidFractionTabButton,
            AndroidFindXTabButton,
            AndroidQuadraticTabButton,
            AndroidGeometryTabButton
        ];

        foreach (Button button in desktopButtons)
        {
            button.IsEnabled =
                !isLocked;
        }

        foreach (Button button in androidButtons)
        {
            button.IsEnabled =
                !isLocked;
        }

        if (!isLocked)
        {
            UpdateSubTabButtonStyles();
        }

        if (Shell.Current is AppShell appShell)
        {
            appShell.SetPowerRootCalculationInteractionLocked(
                isLocked);
        }
    }

    private void OnCalculationSubTabScrollViewSizeChanged(
        object? sender,
        EventArgs e)
    {
#if ANDROID
        // Android dùng Material-style horizontal tabs với kích thước theo nội dung.
        return;
#else
        double availableWidth =
            CalculationSubTabScrollView.Width;

        if (availableWidth <= 0)
        {
            return;
        }

        Button[] buttons =
        [
            BasicTabButton,
            AverageTabButton,
            PowerRootTabButton,
            FractionTabButton,
            FindXTabButton,
            QuadraticTabButton,
            GeometryTabButton
        ];

        double minimumButtonsWidth =
            buttons.Sum(
                button =>
                    button.MinimumWidthRequest);

        double totalSpacing =
            CalculationSubTabSpacing *
            (buttons.Length - 1);

        double extraWidthPerButton =
            Math.Max(
                0d,
                (availableWidth -
                 minimumButtonsWidth -
                 totalSpacing) /
                buttons.Length);

        foreach (Button button in buttons)
        {
            button.WidthRequest =
                button.MinimumWidthRequest +
                extraWidthPerButton;
        }

        CalculationSubTabGrid.WidthRequest =
            Math.Max(
                availableWidth,
                minimumButtonsWidth +
                totalSpacing);
#endif
    }

    private async Task ScrollSubTabIntoViewAsync(
        Button selectedButton)
    {
        try
        {
#if ANDROID
            await AndroidCalculationSubTabScrollView.ScrollToAsync(
                selectedButton,
                ScrollToPosition.Center,
                true);
#else
            await CalculationSubTabScrollView.ScrollToAsync(
                selectedButton,
                ScrollToPosition.Center,
                true);
#endif
        }
        catch (InvalidOperationException)
        {
            // View có thể vừa bị gỡ khỏi visual tree khi đổi tab chính.
        }
    }

    private VisualElement GetSubTabContent(
        CalculationSubTab tab)
    {
        return tab switch
        {
            CalculationSubTab.Basic => BasicTabContent,
            CalculationSubTab.Average => AverageTabContent,
            CalculationSubTab.PowerRoot => PowerRootTabContent,
            CalculationSubTab.Fraction => FractionTabContent,
            CalculationSubTab.FindX => FindXTabContent,
            CalculationSubTab.Quadratic => QuadraticTabContent,
            CalculationSubTab.Geometry => GeometryTabContent,
            _ => BasicTabContent
        };
    }

    private Button GetSubTabButton(
        CalculationSubTab tab)
    {
#if ANDROID
        return tab switch
        {
            CalculationSubTab.Basic => AndroidBasicTabButton,
            CalculationSubTab.Average => AndroidAverageTabButton,
            CalculationSubTab.PowerRoot => AndroidPowerRootTabButton,
            CalculationSubTab.Fraction => AndroidFractionTabButton,
            CalculationSubTab.FindX => AndroidFindXTabButton,
            CalculationSubTab.Quadratic => AndroidQuadraticTabButton,
            CalculationSubTab.Geometry => AndroidGeometryTabButton,
            _ => AndroidBasicTabButton
        };
#else
        return tab switch
        {
            CalculationSubTab.Basic => BasicTabButton,
            CalculationSubTab.Average => AverageTabButton,
            CalculationSubTab.PowerRoot => PowerRootTabButton,
            CalculationSubTab.Fraction => FractionTabButton,
            CalculationSubTab.FindX => FindXTabButton,
            CalculationSubTab.Quadratic => QuadraticTabButton,
            CalculationSubTab.Geometry => GeometryTabButton,
            _ => BasicTabButton
        };
#endif
    }

    private void SelectSubTab(CalculationSubTab selectedTab)
    {
        _selectedSubTab = selectedTab;

        BasicTabContent.IsVisible = selectedTab == CalculationSubTab.Basic;

        AverageTabContent.IsVisible =
            selectedTab ==
            CalculationSubTab.Average;

        PowerRootTabContent.IsVisible =
            selectedTab ==
            CalculationSubTab.PowerRoot;

        FractionTabContent.IsVisible = selectedTab == CalculationSubTab.Fraction;

        FindXTabContent.IsVisible =
            selectedTab ==
            CalculationSubTab.FindX;

        QuadraticTabContent.IsVisible =
            selectedTab ==
            CalculationSubTab.Quadratic;

        GeometryTabContent.IsVisible =
            selectedTab ==
            CalculationSubTab.Geometry;


        UpdateSubTabButtonStyles();
    }

    private void UpdateSubTabButtonStyles()
    {
#if ANDROID
        ApplyAndroidSubTabState(
            AndroidBasicTabButton,
            AndroidBasicTabIndicator,
            _selectedSubTab == CalculationSubTab.Basic);

        ApplyAndroidSubTabState(
            AndroidAverageTabButton,
            AndroidAverageTabIndicator,
            _selectedSubTab == CalculationSubTab.Average);

        ApplyAndroidSubTabState(
            AndroidPowerRootTabButton,
            AndroidPowerRootTabIndicator,
            _selectedSubTab == CalculationSubTab.PowerRoot);

        ApplyAndroidSubTabState(
            AndroidFractionTabButton,
            AndroidFractionTabIndicator,
            _selectedSubTab == CalculationSubTab.Fraction);

        ApplyAndroidSubTabState(
            AndroidFindXTabButton,
            AndroidFindXTabIndicator,
            _selectedSubTab == CalculationSubTab.FindX);

        ApplyAndroidSubTabState(
            AndroidQuadraticTabButton,
            AndroidQuadraticTabIndicator,
            _selectedSubTab == CalculationSubTab.Quadratic);

        ApplyAndroidSubTabState(
            AndroidGeometryTabButton,
            AndroidGeometryTabIndicator,
            _selectedSubTab == CalculationSubTab.Geometry);
#else
        Button selectedButton =
            _selectedSubTab switch
            {
                CalculationSubTab.Basic => BasicTabButton,
                CalculationSubTab.Average => AverageTabButton,
                CalculationSubTab.PowerRoot => PowerRootTabButton,
                CalculationSubTab.Fraction => FractionTabButton,
                CalculationSubTab.FindX => FindXTabButton,
                CalculationSubTab.Quadratic => QuadraticTabButton,
                CalculationSubTab.Geometry => GeometryTabButton,
                _ => BasicTabButton
            };

        SelectionButtonStyler.Select(
            selectedButton,
            BasicTabButton,
            AverageTabButton,
            PowerRootTabButton,
            FractionTabButton,
            FindXTabButton,
            QuadraticTabButton,
            GeometryTabButton);
#endif
    }

#if ANDROID
    private static void ApplyAndroidSubTabState(
        Button button,
        BoxView indicator,
        bool isSelected)
    {
        button.SetDynamicResource(
            Button.TextColorProperty,
            isSelected
                ? "PrimaryColor"
                : "WallpaperTextSecondaryColor");

        button.BackgroundColor =
            Microsoft.Maui.Graphics.Colors.Transparent;

        // Android Material/DevCheck state must be deterministic.  Keep the
        // indicator permanently bound to the accent color and only toggle
        // visibility.  This avoids the Transparent -> DynamicResource +
        // opacity animation race that could leave later tabs with no line.
        indicator.SetDynamicResource(
            BoxView.BackgroundColorProperty,
            "PrimaryColor");

        indicator.CancelAnimations();
        indicator.Opacity = 1d;
        indicator.Scale = 1d;
        indicator.IsVisible = isSelected;
    }
#endif

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

    private void PreferElementaryLongDivisionMode()
    {
        _isUpdatingLongDivisionMode =
            true;

        try
        {
            _longDivisionDisplayMode =
                LongDivisionDisplayMode.Elementary;

            if (!ElementaryDivisionModeRadioButton.IsChecked)
            {
                ElementaryDivisionModeRadioButton.IsChecked =
                    true;
            }

            UpdateLongDivisionModeStyles();
        }
        finally
        {
            _isUpdatingLongDivisionMode =
                false;
        }
    }

    private void OnLongDivisionDisplayModeChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isUpdatingLongDivisionMode ||
            !e.Value)
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
                ? "WallpaperPrimarySoftColor"
                : "WallpaperSurfaceColor");

        border.SetDynamicResource(
            Border.StrokeProperty,
            selected
                ? "PrimaryBrush"
                : "WallpaperBorderBrush");

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
public enum NumberInputType
{
    Integer,
    Decimal
}

public enum CalculationSubTab
{
    Basic,
    Average,
    PowerRoot,
    Fraction,
    FindX,
    Quadratic,
    Geometry
}
public enum LongDivisionDisplayMode
{
    Elementary,
    Decimal
}
