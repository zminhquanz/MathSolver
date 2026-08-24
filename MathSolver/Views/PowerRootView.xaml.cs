using CommunityToolkit.Maui.Storage;
using MathSolver.Controls;
using MathSolver.Numerics;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Views.Base;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

#if WINDOWS
using Microsoft.Maui.Platform;
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace MathSolver.Views;

public partial class PowerRootView : LocalizedSolverView
{
    public event Action<bool>? CalculationInteractionLockChanged;

    private readonly PowerRootEngine _powerRootEngine = new();
    private const long MaxBaseMagnitude =
        1_000_000_000_000_000_000L;

    private const int MaxBaseInputDigits =
        19;

    private const int LegacyNttMaximumExponent =
        10_000_000;

    private const int MaxExponent =
        100_000_000;

    private const int MaxExponentInputDigits =
        9;

    private const int FullResultDigitThreshold =
        18;

    // Từ 19 chữ số, tab Lũy thừa dùng luồng xuất TXT có sẵn thay vì
    // cố đưa toàn bộ kết quả lên giao diện.
    private const int ExportDigitThreshold =
        19;

    private const int ParallelComputationDigitThreshold =
        100_000;

    private const int ExportLeafDigitCount =
        4_096;

    private const int ExactPreviewConversionLimit =
        100_100;

    // Temporary NTT workspaces above this size are large enough that waiting
    // for a later opportunistic Gen2 collection can leave gigabytes of dead
    // LOH segments committed after the calculation. The cleanup runs only
    // after the measured stopwatch has stopped.
    private const long LargeCalculationMemoryCleanupThresholdBytes =
        512L * 1024L * 1024L;

    private const int PreviewLeadingDigits =
        12;

    private const int MaxRootInputDigits =
        39;

    private const int RootScientificDisplayDigitThreshold =
        18;

    private const int RootScientificDisplaySignificantDigits =
        12;

    private const sbyte MinRootDegree =
        sbyte.MinValue;

    private const sbyte MaxRootDegree =
        sbyte.MaxValue;

    private const int RootMaximumDecimalPlaces =
        10;

    private static readonly BigInteger MinRootRadicand =
        (BigInteger)Int128.MinValue;

    private static readonly BigInteger MaxRootRadicand =
        (BigInteger)Int128.MaxValue;

    // Decimal constants keep enough fractional precision for leading-digit
    // and digit-count estimates through the 100,000,000 exponent ceiling. A
    // double loses useful low-order logarithm detail for near-10^18 bases.
    private const decimal NaturalLogarithmOfTwo =
        0.6931471805599453094172321215m;

    private const decimal NaturalLogarithmOfTen =
        2.3025850929940456840179914557m;

    private CancellationTokenSource? _calculationCancellation;
    private CancellationTokenSource? _exportCancellation;
    private TaskCompletionSource<bool>? _calculationCompletionSource;
    private TaskCompletionSource<bool>? _exportCompletionSource;
    private PowerCalculationState? _calculationState;
    private RootCalculationState? _rootCalculationState;
    private bool _isCalculating;
    private bool _isExporting;
    private bool _isStopConfirmationVisible;
    private bool _isDiagnosticsVisible;
    private bool _isPowerMode = true;
    private bool _isUpdatingInputText;
    private bool _isUpdatingRootInputText;
    private readonly Dictionary<Entry, string> _pendingRestoredEntryTexts = [];
    private readonly Dictionary<Entry, string> _rootExactInputValues = [];
    private int _calculationVersion;
    private int _calculationActiveWorkerCount = 1;
    private CalculationProgressPhase _calculationProgressPhase;
    private int _calculationPhaseCompleted;
    private int _calculationPhaseTotal;

    public PowerRootView()
    {
        InitializeComponent();

        InitializeLocalization();

        SelectMode(
            powerMode: true);

        RefreshLocalizedDynamicText();
    }

    protected override void RefreshLocalizedContent()
    {
        base.RefreshLocalizedContent();
        RefreshLocalizedDynamicText();
    }

    protected override void OnSolverLoaded()
    {
        base.OnSolverLoaded();

        DeveloperModeManager.DeveloperModeChanged +=
            OnDeveloperModeChanged;

        AppThemeManager.ThemeChanged +=
            OnThemeChanged;

        // SelectionButtonStyler gắn DynamicResource lên hai nút chế độ.
        // Trên WinUI, sau khi ResourceDictionary của theme bị thay trong lúc
        // view đang sống, visual state của Button đôi khi vẫn giữ brush cũ.
        // Gắn lại resource mỗi lần view Loaded để trạng thái luôn khớp theme.
        RefreshModeButtonTheme();
        UpdateDeveloperDiagnosticsVisibility();
    }

    protected override void OnSolverUnloaded()
    {
        DeveloperModeManager.DeveloperModeChanged -=
            OnDeveloperModeChanged;

        AppThemeManager.ThemeChanged -=
            OnThemeChanged;

#if WINDOWS
        MathSolver.Platforms.Windows.WindowStateManager.ClearCloseGuard(
            this);
#endif

        _calculationCancellation?.Cancel();
        _exportCancellation?.Cancel();
    }

    private void OnPowerModeClicked(
        object? sender,
        EventArgs e)
    {
        SelectMode(
            powerMode: true);
    }

    private void OnRootModeClicked(
        object? sender,
        EventArgs e)
    {
        SelectMode(
            powerMode: false);
    }

    private void SelectMode(
        bool powerMode)
    {
        if (_isCalculating)
        {
            return;
        }

        _isPowerMode =
            powerMode;

        RefreshModeButtonTheme();

        PowerContent.IsVisible =
            powerMode;

        RootContent.IsVisible =
            !powerMode;
    }

    private void RefreshModeButtonTheme()
    {
        SelectionButtonStyler.Select(
            _isPowerMode
                ? PowerModeButton
                : RootModeButton,
            PowerModeButton,
            RootModeButton);
    }

    private void OnThemeChanged(
        object? sender,
        EventArgs e)
    {
        // AppThemeManager phát ThemeChanged sau khi palette mới đã được gắn.
        // Dispatch thêm một UI tick để WinUI thoát khỏi visual state cũ trước
        // khi SetDynamicResource được gắn lại. Không gọi SelectMode vì khi đang
        // tính toán SelectMode chủ động return và sẽ làm màu nút không refresh.
        Dispatcher.Dispatch(
            RefreshModeButtonTheme);
    }

    private void OnInputTextChanged(
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

        // MAUI có thể phát TextChanged khôi phục sau khi cờ cập nhật đã được
        // hạ xuống. Bỏ qua đúng sự kiện đó để thông báo vừa hiện không bị
        // nhánh dữ liệu hợp lệ ẩn ngay lập tức.
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

        if (_isCalculating ||
            _isUpdatingInputText)
        {
            return;
        }

        if (IsInputOutsideAllowedRange(
                entry,
                newText))
        {
            ShowError(
                Translate(
                    ReferenceEquals(
                        entry,
                        BaseEntry)
                        ? "PowerRoot.BaseRangeError"
                        : "PowerRoot.ExponentRangeError"));

            HideResult();
            ProgressBorder.IsVisible =
                false;

            RestoreRejectedInput(
                entry,
                e.OldTextValue);

            return;
        }

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

            SetInputText(
                entry,
                formattedText,
                IntegerInputFormatter.FindCursorPosition(
                    formattedText,
                    logicalPosition));
        }

        HideError();
        HideResult();

        ProgressBorder.IsVisible =
            false;
    }

    private bool IsInputOutsideAllowedRange(
        Entry entry,
        string? text)
    {
        string normalizedText =
            RemoveGroupSeparators(
                    text)
                .Replace(
                    '−',
                    '-');

        bool isBaseEntry =
            ReferenceEquals(
                entry,
                BaseEntry);

        if (normalizedText.Length == 0 ||
            isBaseEntry &&
            normalizedText == "-")
        {
            return false;
        }

        int startIndex =
            isBaseEntry &&
            normalizedText[0] == '-'
                ? 1
                : 0;

        if (startIndex == 1 &&
            normalizedText.Length == 1)
        {
            return false;
        }

        for (int index = startIndex;
             index < normalizedText.Length;
             index++)
        {
            if (!char.IsAsciiDigit(
                    normalizedText[index]))
            {
                return true;
            }
        }

        if (startIndex == 0 &&
            normalizedText[0] == '-')
        {
            return true;
        }

        int digitCount =
            normalizedText.Length -
            startIndex;

        int maximumDigitCount =
            isBaseEntry
                ? MaxBaseInputDigits
                : MaxExponentInputDigits;

        if (digitCount >
            maximumDigitCount)
        {
            return true;
        }

        if (!BigInteger.TryParse(
                normalizedText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out BigInteger value))
        {
            return true;
        }

        return isBaseEntry
            ? value < -MaxBaseMagnitude ||
              value > MaxBaseMagnitude
            : value < BigInteger.Zero ||
              value > MaxExponent;
    }

    private void RestoreRejectedInput(
        Entry entry,
        string? oldText)
    {
        string restoredText =
            oldText ??
            string.Empty;

        _pendingRestoredEntryTexts[entry] =
            restoredText;

        SetInputText(
            entry,
            restoredText);
    }

    private void SetInputText(
        Entry entry,
        string text,
        int? cursorPosition = null)
    {

        _isUpdatingInputText = true;

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
            _isUpdatingInputText = false;
        }
    }

    private void OnIntegerEntryUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry ||
            string.IsNullOrWhiteSpace(
                entry.Text))
        {
            return;
        }

        string normalizedText =
            RemoveGroupSeparators(
                entry.Text);

        if (long.TryParse(
                normalizedText,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out long value))
        {
            SetInputText(
                entry,
                value.ToString(
                    "N0",
                    CultureInfo.InvariantCulture));
        }
    }

    private async void OnCalculateClicked(
        object? sender,
        EventArgs e)
    {
        if (_isCalculating ||
            !_isPowerMode)
        {
            return;
        }

        HideError();
        HideResult();

        if (!TryReadInputs(
                out long baseValue,
                out int exponent))
        {
            return;
        }

        if (exponent > LegacyNttMaximumExponent &&
            !await ConfirmVeryLargePowerAsync(
                baseValue,
                exponent))
        {
            return;
        }

        await CalculatePowerAsync(
            baseValue,
            exponent);
    }

    private async Task<bool> ConfirmVeryLargePowerAsync(
        long baseValue,
        int exponent)
    {
        int estimatedDigitCount =
            EstimateDecimalDigitCount(
                baseValue,
                exponent);

        string estimatedDigits =
            estimatedDigitCount.ToString(
                "N0",
                CultureInfo.InvariantCulture);

        return await MaterialDialogService.ConfirmAsync(
            GetOwningPage(),
            Translate(
                "PowerRoot.LargeExponentWarningTitle"),
            Format(
                "PowerRoot.LargeExponentWarningMessage",
                exponent.ToString(
                    "N0",
                    CultureInfo.InvariantCulture),
                estimatedDigits),
            Translate(
                "PowerRoot.LargeExponentContinue"),
            Translate(
                "PowerRoot.LargeExponentCancel"));
    }

    private Page GetOwningPage()
    {
        Element? current = this;

        while (current is not null)
        {
            if (current is Page page)
            {
                return page;
            }

            current = current.Parent;
        }

        return Shell.Current ??
               throw new InvalidOperationException(
                   "Unable to resolve the owning page for the RAM warning dialog.");
    }

    private bool TryReadInputs(
        out long baseValue,
        out int exponent)
    {
        baseValue = 0;
        exponent = 0;

        string baseText =
            RemoveGroupSeparators(
                BaseEntry.Text);

        if (!long.TryParse(
                baseText,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out baseValue) ||
            baseValue < -MaxBaseMagnitude ||
            baseValue > MaxBaseMagnitude)
        {
            ShowError(
                Translate(
                    "PowerRoot.BaseRangeError"));

            BaseEntry.Focus();
            return false;
        }

        string exponentText =
            RemoveGroupSeparators(
                ExponentEntry.Text);

        if (!int.TryParse(
                exponentText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out exponent) ||
            exponent < 0 ||
            exponent > MaxExponent)
        {
            ShowError(
                Translate(
                    "PowerRoot.ExponentRangeError"));

            ExponentEntry.Focus();
            return false;
        }

        if (baseValue == 0 &&
            exponent == 0)
        {
            ShowError(
                Translate(
                    "PowerRoot.ZeroPowerZeroError"));

            return false;
        }

        return true;
    }

    private void OnRootInputTextChanged(
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

        if (_isUpdatingRootInputText)
        {
            return;
        }

        bool isRadicand =
            ReferenceEquals(
                entry,
                RootRadicandEntry);

        bool isValid =
            isRadicand
                ? IsValidRootRadicandWhileTyping(
                    newText)
                : IsValidRootDegreeWhileTyping(
                    newText);

        if (!isValid)
        {
            ShowRootError(
                Translate(
                    isRadicand
                        ? "PowerRoot.RootRadicandRangeError"
                        : "PowerRoot.RootDegreeRangeError"));

            HideRootResult();

            string previousValidText =
                e.OldTextValue ??
                string.Empty;

            _pendingRestoredEntryTexts[entry] =
                previousValidText;

            SetRootEntryText(
                entry,
                previousValidText);

            return;
        }

        if (isRadicand)
        {
            _rootExactInputValues.Remove(
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

                SetRootEntryText(
                    entry,
                    formattedText,
                    IntegerInputFormatter.FindCursorPosition(
                        formattedText,
                        logicalPosition));
            }
        }

        HideRootError();
        HideRootResult();
    }

    private static bool IsValidRootRadicandWhileTyping(
        string text)
    {
        if (text.Length == 0 ||
            text is "-" or "−")
        {
            return true;
        }

        string normalized =
            RemoveGroupSeparators(
                text);

        int firstDigitIndex =
            normalized.Length > 0 &&
            normalized[0] == '-'
                ? 1
                : 0;

        if (firstDigitIndex ==
            normalized.Length)
        {
            return true;
        }

        int digitCount = 0;

        for (int index = firstDigitIndex;
             index < normalized.Length;
             index++)
        {
            if (!char.IsAsciiDigit(
                    normalized[index]))
            {
                return false;
            }

            digitCount++;

            if (digitCount >
                MaxRootInputDigits)
            {
                return false;
            }
        }

        return BigInteger.TryParse(
                   normalized,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out BigInteger value) &&
               value >= MinRootRadicand &&
               value <= MaxRootRadicand;
    }

    private static bool IsValidRootDegreeWhileTyping(
        string text)
    {
        string normalized =
            RemoveGroupSeparators(
                text);

        if (normalized.Length == 0 ||
            normalized == "-")
        {
            return true;
        }

        // sbyte allows −128..127. The extra character is the optional minus.
        if (normalized.Length > 4)
        {
            return false;
        }

        return sbyte.TryParse(
            normalized,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out _);
    }

    private void OnRootRadicandFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry ||
            !_rootExactInputValues.TryGetValue(
                entry,
                out string? exactText) ||
            !BigInteger.TryParse(
                exactText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out BigInteger value))
        {
            return;
        }

        _rootExactInputValues.Remove(
            entry);

        SetRootEntryText(
            entry,
            value.ToString(
                "N0",
                CultureInfo.InvariantCulture));
    }

    private void OnRootRadicandUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string normalized =
            RemoveGroupSeparators(
                entry.Text);

        if (!Int128.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out Int128 value))
        {
            return;
        }

        BigInteger bigValue =
            (BigInteger)value;

        string exactText =
            bigValue.ToString(
                CultureInfo.InvariantCulture);

        int digitCount =
            BigInteger.Abs(
                    bigValue)
                .ToString(
                    CultureInfo.InvariantCulture)
                .Length;

        if (digitCount >
            RootScientificDisplayDigitThreshold)
        {
            _rootExactInputValues[entry] =
                exactText;

            SetRootEntryText(
                entry,
                FormatRootScientificInput(
                    bigValue));
        }
        else
        {
            _rootExactInputValues.Remove(
                entry);

            SetRootEntryText(
                entry,
                bigValue.ToString(
                    "N0",
                    CultureInfo.InvariantCulture));
        }
    }

    private void OnRootDegreeUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string normalized =
            RemoveGroupSeparators(
                entry.Text);

        if (sbyte.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out sbyte degree))
        {
            SetRootEntryText(
                entry,
                degree.ToString(
                    CultureInfo.InvariantCulture));
        }
    }

    private void SetRootEntryText(
        Entry entry,
        string text,
        int? cursorPosition = null)
    {
        _isUpdatingRootInputText = true;

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
            _isUpdatingRootInputText = false;
        }
    }

    private void OnRootCalculateClicked(
        object? sender,
        EventArgs e)
    {
        if (_isCalculating ||
            _isPowerMode)
        {
            return;
        }

        HideRootError();
        HideRootResult();

        if (!TryReadRootInputs(
                out Int128 radicand,
                out sbyte degree))
        {
            return;
        }

        try
        {
            RootCalculationResult calculation =
                _powerRootEngine.CalculateRoot(
                    radicand,
                    degree);

            RootCalculationMethod method =
                (RootCalculationMethod)calculation.Method;

            bool isComplex = calculation.IsComplex;
            DoubleDouble realResult = calculation.RealResult;
            DoubleDouble imaginaryResult =
                calculation.ImaginaryResult;

            string resultText;

            if (!calculation.IsFinite)
            {
                ShowRootError(Translate("PowerRoot.RootNotFinite"));
                return;
            }

            resultText = isComplex
                ? FormatRootComplex(
                    realResult,
                    imaginaryResult)
                : FormatRootReal(realResult);

            _rootCalculationState =
                new RootCalculationState(
                    radicand,
                    degree,
                    isComplex,
                    realResult,
                    imaginaryResult,
                    resultText,
                    method);

            ShowRootResult(
                _rootCalculationState);
        }
        catch (Exception exception)
        {
            ShowRootError(
                Format(
                    "PowerRoot.RootCalculationError",
                    exception.Message));
        }
    }

    private bool TryReadRootInputs(
        out Int128 radicand,
        out sbyte degree)
    {
        radicand = Int128.Zero;
        degree = 0;

        string radicandText =
            _rootExactInputValues.TryGetValue(
                RootRadicandEntry,
                out string? exactText)
                ? exactText
                : RemoveGroupSeparators(
                    RootRadicandEntry.Text);

        if (!Int128.TryParse(
                radicandText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out radicand))
        {
            ShowRootError(
                Translate(
                    "PowerRoot.RootRadicandRangeError"));

            RootRadicandEntry.Focus();
            return false;
        }

        string degreeText =
            RemoveGroupSeparators(
                RootDegreeEntry.Text);

        if (!sbyte.TryParse(
                degreeText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out degree) ||
            degree < MinRootDegree ||
            degree > MaxRootDegree ||
            degree == 0)
        {
            ShowRootError(
                Translate(
                    "PowerRoot.RootDegreeRangeError"));

            RootDegreeEntry.Focus();
            return false;
        }

        if (radicand == 0 &&
            degree < 0)
        {
            ShowRootError(
                Translate(
                    "PowerRoot.NegativeDegreeZeroError"));

            RootRadicandEntry.Focus();
            return false;
        }

        return true;
    }

    private void ShowRootResult(
        RootCalculationState state)
    {
        ShowTextbookRootExpression(
            state.Radicand,
            state.Degree);

        RootResultValueLabel.Text =
            state.ResultText;

        RootResultKindLabel.Text =
            Translate(
                state.IsComplex
                    ? "PowerRoot.RootResultComplex"
                    : "PowerRoot.RootResultReal");

        RenderRootSolution(
            state);

        RootResultBorder.IsVisible =
            true;
    }

    private void ShowTextbookRootExpression(
        Int128 radicand,
        sbyte degree)
    {
        RootResultExpressionView.Degree =
            degree;

        RootResultExpressionView.RadicandText =
            FormatTextbookRadicand(
                (BigInteger)radicand);
    }

    private void RenderRootSolution(
        RootCalculationState state)
    {
        RootSolutionStack.Children.Clear();

        string formattedRadicand =
            FormatRootInteger(
                (BigInteger)state.Radicand);

        string formattedDegree =
            state.Degree.ToString(
                CultureInfo.InvariantCulture);

        string unknownPower =
            $"x{ToSignedSuperscript(state.Degree)}";

        BigInteger absoluteRadicand =
            BigInteger.Abs(
                (BigInteger)state.Radicand);

        int absoluteDegree =
            Math.Abs((int)state.Degree);

        bool hasExactIntegerRoot =
            _powerRootEngine.TryGetExactIntegerRoot(
                absoluteRadicand,
                absoluteDegree,
                out BigInteger exactMagnitude);

        int stepNumber = 1;

        AddRootVisualStep(
            Format(
                "PowerRoot.RootStepGivenVisual",
                stepNumber++),
            (BigInteger)state.Radicand,
            state.Degree);

        // Bậc căn âm cần được giải thích theo cách trực quan trước.
        // Nếu dùng ngay x^(-n) = a, học sinh phải hiểu số mũ âm trước khi
        // hiểu phép căn âm, làm lời giải khó theo dõi hơn mức cần thiết.
        if (state.Degree < 0)
        {
            RenderNegativeDegreeRootSolution(
                state,
                absoluteDegree,
                formattedRadicand,
                hasExactIntegerRoot,
                exactMagnitude,
                ref stepNumber);
            return;
        }

        AddRootTextStep(
            Format(
                "PowerRoot.RootStepMeaning",
                stepNumber++,
                unknownPower,
                formattedRadicand,
                formattedDegree));

        if (state.Radicand == 0)
        {
            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepZero",
                    stepNumber++,
                    $"0{ToSuperscript(state.Degree)}",
                    formattedDegree));

            AddRootConclusion(
                Translate(
                    "PowerRoot.RootStepRealExactConclusionVisual"),
                BigInteger.Zero,
                state.Degree,
                "=",
                "0");

            return;
        }

        if (state.IsComplex)
        {
            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepNegativeEven",
                    stepNumber++,
                    formattedDegree,
                    formattedRadicand));

            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepComplexIntroduction",
                    stepNumber++));

            if (state.Degree == 2)
            {
                string magnitudeResult =
                    hasExactIntegerRoot
                        ? FormatRootInteger(
                            exactMagnitude)
                        : FormatRootReal(
                            DoubleDouble.Abs(
                                state.ImaginaryResult));

                bool isExact =
                    hasExactIntegerRoot;

                var complexSquareRootStep =
                    CreateRootSolutionGroup();

                complexSquareRootStep.Children.Add(
                    CreateRootSolutionLabel(
                        Format(
                            isExact
                                ? "PowerRoot.RootStepComplexSqrtExactVisual"
                                : "PowerRoot.RootStepComplexSqrtApproxVisual",
                            stepNumber++)));

                complexSquareRootStep.Children.Add(
                    CreateRootEquation(
                        absoluteRadicand,
                        2,
                        isExact
                            ? "="
                            : "≈",
                        magnitudeResult));

                complexSquareRootStep.Children.Add(
                    CreateRootSolutionLabel(
                        Translate(
                            "PowerRoot.RootStepComplexSqrtAttachIVisual")));

                complexSquareRootStep.Children.Add(
                    CreateRootEquation(
                        (BigInteger)state.Radicand,
                        state.Degree,
                        isExact
                            ? "="
                            : "≈",
                        state.ResultText));

                RootSolutionStack.Children.Add(
                    complexSquareRootStep);
            }
            else
            {
                AddRootVisualStep(
                    Format(
                        "PowerRoot.RootStepComplexGeneralVisual",
                        stepNumber++,
                        formattedDegree),
                    (BigInteger)state.Radicand,
                    state.Degree,
                    "≈",
                    state.ResultText);
            }

            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepApproxCheck",
                    stepNumber++,
                    state.ResultText,
                    ToSuperscript(
                        state.Degree),
                    formattedRadicand));

            string complexRelation =
                state.Degree == 2 &&
                hasExactIntegerRoot
                    ? "="
                    : "≈";

            AddRootConclusion(
                Translate(
                    "PowerRoot.RootStepComplexConclusionVisual"),
                (BigInteger)state.Radicand,
                state.Degree,
                complexRelation,
                state.ResultText);

            return;
        }

        AddRootTextStep(
            Format(
                state.Radicand < 0
                    ? "PowerRoot.RootStepNegativeOdd"
                    : "PowerRoot.RootStepPositive",
                stepNumber++,
                formattedDegree));

        if (hasExactIntegerRoot)
        {
            BigInteger exactRoot =
                state.Radicand < 0
                    ? -exactMagnitude
                    : exactMagnitude;

            string exactRootText =
                FormatRootInteger(
                    exactRoot);

            string poweredBase =
                exactRoot.Sign < 0
                    ? $"({exactRootText})"
                    : exactRootText;

            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepExactCandidate",
                    stepNumber++,
                    exactRootText));

            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepExactCheck",
                    stepNumber++,
                    $"{poweredBase}{ToSuperscript(state.Degree)}",
                    formattedRadicand));

            AddRootConclusion(
                Translate(
                    "PowerRoot.RootStepRealExactConclusionVisual"),
                (BigInteger)state.Radicand,
                state.Degree,
                "=",
                exactRootText);
        }
        else
        {
            AddRootVisualStep(
                Format(
                    "PowerRoot.RootStepApproxCalculationVisual",
                    stepNumber++,
                    formattedDegree,
                    formattedRadicand),
                (BigInteger)state.Radicand,
                state.Degree,
                "≈",
                state.ResultText);

            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepApproxCheck",
                    stepNumber++,
                    state.ResultText,
                    ToSuperscript(
                        state.Degree),
                    formattedRadicand));

            AddRootConclusion(
                Translate(
                    "PowerRoot.RootStepRealApproxConclusionVisual"),
                (BigInteger)state.Radicand,
                state.Degree,
                "≈",
                state.ResultText);
        }
    }

    private void RenderNegativeDegreeRootSolution(
        RootCalculationState state,
        int absoluteDegree,
        string formattedRadicand,
        bool hasExactIntegerRoot,
        BigInteger exactMagnitude,
        ref int stepNumber)
    {
        string absoluteDegreeText =
            absoluteDegree.ToString(
                CultureInfo.InvariantCulture);

        AddRootTextStep(
            Format(
                "PowerRoot.RootStepNegativeDegreeReciprocal",
                stepNumber++,
                state.Degree.ToString(
                    CultureInfo.InvariantCulture),
                absoluteDegreeText));

        if (state.IsComplex)
        {
            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepNegativeEven",
                    stepNumber++,
                    absoluteDegreeText,
                    formattedRadicand));

            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepComplexIntroduction",
                    stepNumber++));

            AddRootVisualStep(
                Format(
                    "PowerRoot.RootStepNegativeDegreeComplexVisual",
                    stepNumber++,
                    absoluteDegreeText),
                (BigInteger)state.Radicand,
                state.Degree,
                "≈",
                state.ResultText);

            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepNegativeDegreeCheck",
                    stepNumber++,
                    state.ResultText,
                    ToSuperscript(absoluteDegree),
                    formattedRadicand));

            AddRootConclusion(
                Translate(
                    "PowerRoot.RootStepComplexConclusionVisual"),
                (BigInteger)state.Radicand,
                state.Degree,
                "≈",
                state.ResultText);

            return;
        }

        AddRootTextStep(
            Format(
                state.Radicand < 0
                    ? "PowerRoot.RootStepNegativeOdd"
                    : "PowerRoot.RootStepPositive",
                stepNumber++,
                absoluteDegreeText));

        string relation;

        if (hasExactIntegerRoot)
        {
            BigInteger signedRoot =
                state.Radicand < 0
                    ? -exactMagnitude
                    : exactMagnitude;

            bool displayedReciprocalIsExact =
                HasTerminatingReciprocalWithinDisplayScale(
                    signedRoot,
                    RootMaximumDecimalPlaces);

            relation =
                displayedReciprocalIsExact
                    ? "="
                    : "≈";

            string signedRootText =
                FormatRootInteger(signedRoot);

            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepNegativeDegreeExactReciprocal",
                    stepNumber++,
                    absoluteDegreeText,
                    formattedRadicand,
                    signedRootText,
                    signedRootText,
                    relation,
                    state.ResultText));
        }
        else
        {
            // Với bậc âm, state.RealResult là 1 / căn_bậc_dương.
            // Lấy nghịch đảo một lần chỉ để trình bày giá trị trung gian
            // giúp lời giải cho học sinh dễ theo dõi hơn.
            DoubleDouble positiveDegreeRoot =
                DoubleDouble.One /
                state.RealResult;

            string positiveDegreeRootText =
                FormatRootReal(positiveDegreeRoot);

            relation = "≈";

            AddRootTextStep(
                Format(
                    "PowerRoot.RootStepNegativeDegreeApproxReciprocal",
                    stepNumber++,
                    absoluteDegreeText,
                    formattedRadicand,
                    positiveDegreeRootText,
                    state.ResultText));
        }

        AddRootTextStep(
            Format(
                "PowerRoot.RootStepNegativeDegreeCheck",
                stepNumber++,
                state.ResultText,
                ToSuperscript(absoluteDegree),
                formattedRadicand));

        AddRootConclusion(
            Translate(
                relation == "="
                    ? "PowerRoot.RootStepRealExactConclusionVisual"
                    : "PowerRoot.RootStepRealApproxConclusionVisual"),
            (BigInteger)state.Radicand,
            state.Degree,
            relation,
            state.ResultText);
    }

    private static bool HasTerminatingReciprocalWithinDisplayScale(
        BigInteger denominator,
        int maximumDecimalPlaces)
    {
        denominator = BigInteger.Abs(denominator);

        if (denominator.IsZero)
        {
            return false;
        }

        int factorTwoCount = 0;
        int factorFiveCount = 0;

        while ((denominator & BigInteger.One) == BigInteger.Zero)
        {
            denominator >>= 1;
            factorTwoCount++;
        }

        while (denominator % 5 == 0)
        {
            denominator /= 5;
            factorFiveCount++;
        }

        return denominator.IsOne &&
               Math.Max(
                   factorTwoCount,
                   factorFiveCount) <=
               maximumDecimalPlaces;
    }

    private void AddRootTextStep(
        string text)
    {
        RootSolutionStack.Children.Add(
            CreateRootSolutionLabel(
                text));
    }

    private void AddRootVisualStep(
        string text,
        BigInteger radicand,
        sbyte degree,
        string? relation = null,
        string? result = null)
    {
        var group =
            CreateRootSolutionGroup();

        group.Children.Add(
            CreateRootSolutionLabel(
                text));

        group.Children.Add(
            CreateRootEquation(
                radicand,
                degree,
                relation,
                result));

        RootSolutionStack.Children.Add(
            group);
    }

    private void AddRootConclusion(
        string text,
        BigInteger radicand,
        sbyte degree,
        string relation,
        string result)
    {
        var group =
            CreateRootSolutionGroup();

        group.Children.Add(
            CreateRootSolutionLabel(
                text,
                isConclusion: true));

        group.Children.Add(
            CreateRootEquation(
                radicand,
                degree,
                relation,
                result,
                isConclusion: true));

        RootSolutionStack.Children.Add(
            group);
    }

    private static VerticalStackLayout CreateRootSolutionGroup()
    {
        return new VerticalStackLayout
        {
            Spacing = 7
        };
    }

    private static Label CreateRootSolutionLabel(
        string text,
        bool isConclusion = false)
    {
        var label =
            new Label
            {
                Text = text,
                FontSize = 16,
                LineHeight = 1.3,
                LineBreakMode = LineBreakMode.WordWrap,
                FontAttributes =
                    isConclusion
                        ? FontAttributes.Bold
                        : FontAttributes.None
            };

        label.SetDynamicResource(
            Label.TextColorProperty,
            "WallpaperTextPrimaryColor");

        return label;
    }

    private static View CreateRootEquation(
        BigInteger radicand,
        sbyte degree,
        string? relation,
        string? result,
        bool isConclusion = false)
    {
        var radicalExpression =
            new TextbookRadicalExpressionView
            {
                Degree = degree,
                RadicandText =
                    FormatTextbookRadicand(
                        radicand),
                FontSize =
                    isConclusion
                        ? 18
                        : 17,
                DegreeFontSize =
                    isConclusion
                        ? 12
                        : 11,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };

        radicalExpression.SetDynamicResource(
            TextbookRadicalExpressionView.LineColorProperty,
            isConclusion
                ? "SuccessColor"
                : "WallpaperTextPrimaryColor");

        radicalExpression.SetDynamicResource(
            TextbookRadicalExpressionView.TextColorProperty,
            isConclusion
                ? "SuccessColor"
                : "WallpaperTextPrimaryColor");

        var equationLayout =
            new HorizontalStackLayout
            {
                Spacing = 7,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

        equationLayout.Children.Add(
            radicalExpression);

        if (!string.IsNullOrWhiteSpace(
                relation) &&
            result is not null)
        {
            var resultLabel =
                new Label
                {
                    Text = $"{relation} {result}",
                    FontSize =
                        isConclusion
                            ? 19
                            : 17,
                    FontAttributes = FontAttributes.Bold,
                    VerticalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.NoWrap
                };

            resultLabel.SetDynamicResource(
                Label.TextColorProperty,
                isConclusion
                    ? "SuccessColor"
                    : "WallpaperTextPrimaryColor");

            equationLayout.Children.Add(
                resultLabel);
        }

        return new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility =
                ScrollBarVisibility.Never,
            HorizontalOptions = LayoutOptions.Fill,
            Content = equationLayout
        };
    }

    private static string FormatTextbookRadicand(
        BigInteger radicand)
    {
        string radicandText =
            FormatRootInteger(
                radicand);

        return radicand.Sign < 0
            ? $"({radicandText})"
            : radicandText;
    }

    private static string FormatRootInteger(
        BigInteger value)
    {
        string digits =
            BigInteger.Abs(
                    value)
                .ToString(
                    CultureInfo.InvariantCulture);

        if (digits.Length >
            RootScientificDisplayDigitThreshold)
        {
            return FormatScientificInteger(
                value);
        }

        return value
            .ToString(
                "N0",
                CultureInfo.InvariantCulture)
            .Replace(
                '-',
                '−');
    }

    private static string FormatRootReal(
        DoubleDouble value)
    {
        if (value.IsZero)
        {
            return "0";
        }

        double approximateMagnitude =
            Math.Abs(
                value.ToDouble());

        int integerDigitCount =
            Math.Max(
                1,
                (int)Math.Floor(
                    Math.Log10(
                        approximateMagnitude)) +
                1);

        int significantDigits =
            integerDigitCount >
                RootScientificDisplayDigitThreshold
                ? RootScientificDisplaySignificantDigits
                : Math.Clamp(
                    integerDigitCount +
                    RootMaximumDecimalPlaces,
                    1,
                    DoubleDouble.SignificantDigits);

        string text =
            value.ToGeneralString(
                significantDigits,
                scientificUpperExponent:
                    RootScientificDisplayDigitThreshold,
                scientificLowerExponent:
                    -RootMaximumDecimalPlaces);

        return FormatRootNumberForDisplay(
            text);
    }

    private static string FormatRootComplex(
        DoubleDouble realValue,
        DoubleDouble imaginaryValue)
    {
        DoubleDouble real =
            NormalizeDisplayedComplexComponent(
                realValue);

        DoubleDouble imaginary =
            NormalizeDisplayedComplexComponent(
                imaginaryValue);

        if (imaginary.IsZero)
        {
            return FormatRootReal(real);
        }

        string imaginaryMagnitude =
            FormatRootReal(
                DoubleDouble.Abs(
                    imaginary));

        string imaginaryTerm =
            imaginaryMagnitude == "1"
                ? "i"
                : $"{imaginaryMagnitude}i";

        if (real.IsZero)
        {
            return imaginary.Sign < 0
                ? $"−{imaginaryTerm}"
                : imaginaryTerm;
        }

        return
            $"{FormatRootReal(real)} " +
            $"{(imaginary.Sign < 0 ? '−' : '+')} " +
            imaginaryTerm;
    }

    private static DoubleDouble NormalizeDisplayedComplexComponent(
        DoubleDouble value)
    {
        // Half a unit at the last displayed (10th) decimal place. The
        // computation remains DoubleDouble; this only suppresses visual
        // residue such as cos(π/2) in a principal square root.
        DoubleDouble threshold =
            new(0.00000000005d);

        return DoubleDouble.Abs(value) < threshold
            ? DoubleDouble.Zero
            : value;
    }

    private static string FormatRootNumberForDisplay(
        string text)
    {
        int exponentMarkerIndex =
            text.IndexOfAny(
                ['e', 'E']);

        if (exponentMarkerIndex < 0)
        {
            return GroupRootNumber(
                text);
        }

        string mantissa =
            GroupRootNumber(
                text[..exponentMarkerIndex]);

        if (!int.TryParse(
                text[(exponentMarkerIndex + 1)..],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int exponent))
        {
            return text
                .Replace(
                    '-',
                    '−');
        }

        return
            $"{mantissa} × 10{ToSignedSuperscript(exponent)}";
    }

    private static string GroupRootNumber(
        string text)
    {
        return
            IntegerInputFormatter
                .AddThousandsSeparatorsToPlainNumber(
                    text);
    }

    private static string FormatRootScientificInput(
        BigInteger value)
    {
        return FormatScientificInteger(
            value);
    }

    private static string FormatScientificInteger(
        BigInteger value)
    {
        bool isNegative =
            value.Sign < 0;

        string digits =
            BigInteger.Abs(
                    value)
                .ToString(
                    CultureInfo.InvariantCulture);

        int exponent =
            digits.Length - 1;

        int keptDigitCount =
            Math.Min(
                RootScientificDisplaySignificantDigits,
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

    private void OnRootClearClicked(
        object? sender,
        EventArgs e)
    {
        if (_isCalculating)
        {
            return;
        }

        _rootExactInputValues.Clear();
        _pendingRestoredEntryTexts.Remove(
            RootRadicandEntry);
        _pendingRestoredEntryTexts.Remove(
            RootDegreeEntry);
        _rootCalculationState = null;

        SetRootEntryText(
            RootRadicandEntry,
            string.Empty);

        SetRootEntryText(
            RootDegreeEntry,
            string.Empty);

        HideRootError();
        HideRootResult();

        RootRadicandEntry.Focus();
    }

    private async void OnRootCopyResultClicked(
        object? sender,
        EventArgs e)
    {
        if (_rootCalculationState is null)
        {
            return;
        }

        await Clipboard.Default.SetTextAsync(
            _rootCalculationState.ResultText);

        RootCopyResultButton.Text =
            Translate(
                "PowerRoot.Copied");

        await Task.Delay(
            1200);

        if (_rootCalculationState is not null)
        {
            RootCopyResultButton.Text =
                Translate(
                    "PowerRoot.RootCopyResult");
        }
    }

    private void ShowRootError(
        string message)
    {
        RootErrorLabel.Text =
            message;

        RootErrorBorder.IsVisible =
            true;
    }

    private void HideRootError()
    {
        RootErrorBorder.IsVisible =
            false;

        RootErrorLabel.Text =
            string.Empty;
    }

    private void HideRootResult()
    {
        _rootCalculationState = null;
        RootResultBorder.IsVisible =
            false;
    }

    private async Task CalculatePowerAsync(
        long baseValue,
        int exponent)
    {
        _calculationCancellation?.Dispose();

        _calculationCancellation =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            _calculationCancellation.Token;

        int calculationVersion =
            ++_calculationVersion;

        int estimatedDigitCount =
            EstimateDecimalDigitCount(
                baseValue,
                exponent);

        PowerComputationStrategy strategy =
            (PowerComputationStrategy)
            _powerRootEngine.SelectPowerStrategy(
                baseValue,
                exponent,
                out int decimalExponent);

        int activeWorkerCount = 1;

        if (strategy ==
                PowerComputationStrategy.SingleThreadedBigIntegerPower &&
            baseValue != 0 &&
            exponent > LegacyNttMaximumExponent)
        {
            // The 10,000,001..100,000,000 range deliberately uses the new
            // memory-bounded NTT/CRT path. The legacy <=10M path below is left
            // untouched. If multithreading is disabled, the same exact engine
            // runs with one worker instead of falling back to a giant
            // single-threaded BigInteger power.
            activeWorkerCount =
                CalculationThreadingManager.UseMultithreading
                    ? CalculationThreadingManager.RecommendedWorkerCount
                    : 1;

            strategy =
                PowerComputationStrategy.ParallelNttPower;
        }
        else if (strategy ==
                     PowerComputationStrategy.SingleThreadedBigIntegerPower &&
                 estimatedDigitCount >=
                     ParallelComputationDigitThreshold &&
                 CalculationThreadingManager.UseMultithreading)
        {
            // Legacy <=10M selection rule: do not change its NTT/CRT behavior.
            activeWorkerCount =
                CalculationThreadingManager.RecommendedWorkerCount;

            strategy =
                PowerComputationStrategy.ParallelNttPower;
        }

        _calculationActiveWorkerCount =
            activeWorkerCount;
        _calculationProgressPhase =
            CalculationProgressPhase.Preparing;
        _calculationPhaseCompleted = 0;
        _calculationPhaseTotal = 1;

        var calculationCompletionSource =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        _calculationCompletionSource =
            calculationCompletionSource;

        _isCalculating = true;
        UpdateWindowsCloseGuard();

        SetCalculationInteractionLocked(
            isLocked: true);

        // Every new calculation starts with developer diagnostics collapsed.
        _isDiagnosticsVisible = false;
        DiagnosticsToggleButton.IsVisible = false;
        LargeResultInfoBorder.IsVisible = false;
        DiagnosticsToggleButton.Text =
            Translate(
                "PowerRoot.ShowDetails");

        ResultBorder.IsVisible = false;
        ProgressBorder.IsVisible = true;
        CalculationActivityIndicator.IsVisible = true;
        CalculationActivityIndicator.IsRunning = true;
        StopButton.IsVisible = true;
        StopButton.IsEnabled = true;

        CalculationProgressBar.Progress = 0d;
        ProgressPercentLabel.Text = "0%";

        SetCalculationProgress(
            baseValue,
            exponent,
            CalculationProgressPhase.Preparing,
            0,
            1);

        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            PowerCalculationState state;

            if (strategy ==
                PowerComputationStrategy.DecimalPowerOfTen)
            {
                SetCalculationProgress(
                    baseValue,
                    exponent,
                    CalculationProgressPhase.DecimalShift,
                    0,
                    1);

                cancellationToken.ThrowIfCancellationRequested();

                state =
                    CreatePowerOfTenCalculationState(
                        baseValue,
                        exponent,
                        decimalExponent,
                        stopwatch.Elapsed);

                cancellationToken.ThrowIfCancellationRequested();

                SetCalculationProgress(
                    baseValue,
                    exponent,
                    CalculationProgressPhase.DecimalShift,
                    1,
                    1);
            }
            else if (strategy ==
                     PowerComputationStrategy.ParallelNttPower)
            {
                SetCalculationProgress(
                    baseValue,
                    exponent,
                    CalculationProgressPhase.Computing,
                    0,
                    1);

                ParallelPowerResult parallelResult =
                    exponent > LegacyNttMaximumExponent
                        ? await _powerRootEngine.ComputeMemoryBoundedParallelPowerAsync(
                            baseValue,
                            exponent,
                            activeWorkerCount,
                            (completed, total) =>
                                ReportCalculationPhase(
                                    baseValue,
                                    exponent,
                                    CalculationProgressPhase.Computing,
                                    completed,
                                    total,
                                    calculationVersion),
                            cancellationToken)
                        : await _powerRootEngine.ComputeParallelPowerAsync(
                            baseValue,
                            exponent,
                            activeWorkerCount,
                            (completed, total) =>
                                ReportCalculationPhase(
                                    baseValue,
                                    exponent,
                                    CalculationProgressPhase.Computing,
                                    completed,
                                    total,
                                    calculationVersion),
                            cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                SetCalculationProgress(
                    baseValue,
                    exponent,
                    CalculationProgressPhase.Formatting,
                    0,
                    1);

                bool isNegative =
                    baseValue < 0 &&
                    (exponent & 1) != 0;

                bool useSimdForFormatting =
                    CalculationAccelerationManager.UseSimd;

                state =
                    await Task.Run(
                        () => CreateParallelCalculationState(
                            baseValue,
                            exponent,
                            parallelResult.Magnitude,
                            isNegative,
                            activeWorkerCount,
                            stopwatch.Elapsed,
                            parallelResult.Diagnostics,
                            useSimdForFormatting),
                        cancellationToken);
            }
            else
            {
                BigInteger result =
                    BigInteger.Zero;

                long virtualBitShiftExponent =
                    0L;

                if (strategy ==
                    PowerComputationStrategy.BitShift)
                {
                    SetCalculationProgress(
                        baseValue,
                        exponent,
                        CalculationProgressPhase.BitShift,
                        0,
                        1);

                    if (!_powerRootEngine.TryGetPowerOfTwoExponent(
                            baseValue,
                            out int basePowerOfTwoExponent))
                    {
                        throw new InvalidOperationException(
                            "The bit-shift strategy requires |base| = 2^k.");
                    }

                    long totalBitShift =
                        checked(
                            (long)basePowerOfTwoExponent *
                            exponent);

                    if (PowerRootEngine.CanMaterializePowerOfTwoAsBigInteger(
                            totalBitShift))
                    {
                        result =
                            await _powerRootEngine.ComputeBitShiftPowerAsync(
                                baseValue,
                                exponent,
                                totalBitShift,
                                cancellationToken);
                    }
                    else
                    {
                        // .NET 9+ caps BigInteger at Int32.MaxValue significant
                        // bits. Keep an exact virtual 2^k representation instead
                        // of attempting a BigInteger allocation that is guaranteed
                        // to throw OverflowException. The compact UI result and
                        // mathematical explanation need only k; full decimal TXT
                        // generation is deferred until the user explicitly exports.
                        virtualBitShiftExponent =
                            totalBitShift;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    SetCalculationProgress(
                        baseValue,
                        exponent,
                        CalculationProgressPhase.BitShift,
                        1,
                        1);
                }
                else
                {
                    int multiplicationCount =
                        _powerRootEngine.CountPowerMultiplications(
                            exponent);

                    SetCalculationProgress(
                        baseValue,
                        exponent,
                        CalculationProgressPhase.Computing,
                        0,
                        Math.Max(
                            1,
                            multiplicationCount));

                    result =
                        await _powerRootEngine.ComputeSingleThreadedPowerAsync(
                            baseValue,
                            exponent,
                            (completed, total) =>
                                ReportCalculationPhase(
                                    baseValue,
                                    exponent,
                                    CalculationProgressPhase.Computing,
                                    completed,
                                    total,
                                    calculationVersion),
                            cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    SetCalculationProgress(
                        baseValue,
                        exponent,
                        CalculationProgressPhase.Computing,
                        Math.Max(
                            1,
                            multiplicationCount),
                        Math.Max(
                            1,
                            multiplicationCount));
                }

                cancellationToken.ThrowIfCancellationRequested();

                SetCalculationProgress(
                    baseValue,
                    exponent,
                    CalculationProgressPhase.Formatting,
                    0,
                    1);

                ParallelBigUnsigned? preparedBitShiftMagnitude =
                    null;

                if (strategy ==
                        PowerComputationStrategy.BitShift &&
                    virtualBitShiftExponent == 0L &&
                    estimatedDigitCount >=
                        ExportDigitThreshold)
                {
                    // Keep the actual power calculation on the exact, very cheap
                    // single-threaded BigInteger bit-shift path. Only phase 3 is
                    // rebuilt through the same base-10,000 NTT/CRT engine used by
                    // normal parallel powers. This removes the giant serial
                    // BigInteger DivRem nodes from decimal preparation.
                    int decimalPreparationWorkerCount =
                        CalculationThreadingManager.UseMultithreading
                            ? CalculationThreadingManager.RecommendedWorkerCount
                            : 1;

                    _calculationActiveWorkerCount =
                        decimalPreparationWorkerCount;

                    ulong unsignedBase =
                        (ulong)Math.Abs(
                            baseValue);

                    ParallelPowerResult preparedDecimalResult =
                        await Task.Run(
                            () => exponent > LegacyNttMaximumExponent
                                ? ParallelBigUnsigned.PowMemoryBounded(
                                    unsignedBase,
                                    exponent,
                                    decimalPreparationWorkerCount,
                                    (completed, total) =>
                                        ReportCalculationPhase(
                                            baseValue,
                                            exponent,
                                            CalculationProgressPhase.Formatting,
                                            completed,
                                            total,
                                            calculationVersion),
                                    cancellationToken)
                                : ParallelBigUnsigned.Pow(
                                    unsignedBase,
                                    exponent,
                                    decimalPreparationWorkerCount,
                                    (completed, total) =>
                                        ReportCalculationPhase(
                                            baseValue,
                                            exponent,
                                            CalculationProgressPhase.Formatting,
                                            completed,
                                            total,
                                            calculationVersion),
                                    cancellationToken),
                            cancellationToken);

                    preparedBitShiftMagnitude =
                        preparedDecimalResult.Magnitude;

                    cancellationToken.ThrowIfCancellationRequested();

                    // The worker budget above belongs only to phase 3.
                    _calculationActiveWorkerCount = 1;
                }

                state =
                    virtualBitShiftExponent > 0L
                        ? CreateVirtualBitShiftCalculationState(
                            baseValue,
                            exponent,
                            virtualBitShiftExponent,
                            stopwatch.Elapsed)
                        : await Task.Run(
                            () => CreateBigIntegerCalculationState(
                                baseValue,
                                exponent,
                                result,
                                strategy,
                                stopwatch.Elapsed,
                                preparedBitShiftMagnitude),
                            cancellationToken);
            }

            stopwatch.Stop();

            if (calculationVersion !=
                _calculationVersion)
            {
                return;
            }

            long privateMemoryBeforeCleanup =
                GetCurrentProcessPrivateMemoryBytes();

            PowerCalculationState completedState =
                state with
                {
                    Elapsed =
                        stopwatch.Elapsed,
                    ProcessPrivateMemoryBytes =
                        privateMemoryBeforeCleanup,
                    ProcessPrivateMemoryBeforeCleanupBytes =
                        privateMemoryBeforeCleanup
                };

            _calculationState =
                completedState;

            SetCalculationProgress(
                baseValue,
                exponent,
                CalculationProgressPhase.Completed,
                1,
                1);

            StopButton.IsVisible = false;

            // Show the completed result immediately. Memory cleanup is outside
            // the measured calculation and only refreshes the diagnostic line.
            ShowResult(
                completedState);

            // The measured calculation has already ended. Reclaim only very
            // large dead NTT/LOH workspaces here so benchmark time remains
            // apples-to-apples while process Private Bytes can fall promptly.
            if (ShouldReleaseLargeTemporaryMemory(
                    completedState))
            {
                long privateMemoryAfterCleanup =
                    await ReleaseLargeTemporaryMemoryAsync();

                if (calculationVersion !=
                    _calculationVersion)
                {
                    return;
                }

                completedState =
                    completedState with
                    {
                        ProcessPrivateMemoryBytes =
                            privateMemoryAfterCleanup
                    };

                _calculationState =
                    completedState;

                if (LargeResultInfoBorder.IsVisible)
                {
                    LargeResultInfoLabel.Text =
                        CreateLargeResultInformation(
                            completedState);
                }
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            if (calculationVersion ==
                _calculationVersion)
            {
                ProgressTitleLabel.Text =
                    Translate(
                        "PowerRoot.ProgressStopped");

                StopButton.IsVisible = false;
                ResultBorder.IsVisible = false;
            }
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            if (calculationVersion ==
                _calculationVersion)
            {
                ProgressBorder.IsVisible = false;

                ShowError(
                    Format(
                        "PowerRoot.CalculationError",
                        exception.Message));
            }
        }
        finally
        {
            if (calculationVersion ==
                _calculationVersion)
            {
                _isCalculating = false;
                CalculationActivityIndicator.IsRunning = false;
                CalculationActivityIndicator.IsVisible = false;

                SetCalculationInteractionLocked(
                    isLocked: false);
            }

            calculationCompletionSource.TrySetResult(
                true);

            if (ReferenceEquals(
                    _calculationCompletionSource,
                    calculationCompletionSource))
            {
                _calculationCompletionSource = null;
            }

            UpdateWindowsCloseGuard();
        }
    }

    private void ReportCalculationPhase(
        long baseValue,
        int exponent,
        CalculationProgressPhase phase,
        int completedSteps,
        int totalSteps,
        int calculationVersion)
    {

        Dispatcher.Dispatch(
            () =>
            {
                if (calculationVersion !=
                    _calculationVersion)
                {
                    return;
                }

                SetCalculationProgress(
                    baseValue,
                    exponent,
                    phase,
                    completedSteps,
                    totalSteps);
            });
    }

    private void SetCalculationProgress(
        long baseValue,
        int exponent,
        CalculationProgressPhase phase,
        int completedSteps,
        int totalSteps)
    {
        if ((int)phase <
            (int)_calculationProgressPhase)
        {
            return;
        }

        if (phase == _calculationProgressPhase &&
            completedSteps < _calculationPhaseCompleted)
        {
            return;
        }

        _calculationProgressPhase = phase;
        _calculationPhaseCompleted = completedSteps;
        _calculationPhaseTotal = totalSteps;

        double phaseProgress =
            totalSteps > 0
                ? Math.Clamp(
                    (double)completedSteps /
                    totalSteps,
                    0d,
                    1d)
                : 1d;

        double progress =
            phase switch
            {
                CalculationProgressPhase.Preparing =>
                    0.03d,
                CalculationProgressPhase.BitShift =>
                    0.08d +
                    0.82d * phaseProgress,
                CalculationProgressPhase.DecimalShift =>
                    0.08d +
                    0.82d * phaseProgress,
                CalculationProgressPhase.Computing =>
                    0.08d +
                    0.82d * phaseProgress,
                CalculationProgressPhase.Formatting =>
                    0.90d +
                    0.09d * phaseProgress,
                CalculationProgressPhase.Completed =>
                    1d,
                _ =>
                    0d
            };

        CalculationProgressBar.Progress =
            progress;

        ProgressPercentLabel.Text =
            $"{progress:P0}";

        ProgressTitleLabel.Text =
            Format(
                phase == CalculationProgressPhase.Completed
                    ? "PowerRoot.ProgressCompleted"
                    : "PowerRoot.ProgressTitle",
                FormatProgressExpression(
                    baseValue,
                    exponent));

        ProgressStepsLabel.Text =
            CreateCalculationPhaseText(
                phase,
                baseValue,
                exponent,
                completedSteps,
                totalSteps,
                _calculationActiveWorkerCount);
    }

    private string CreateCalculationPhaseText(
        CalculationProgressPhase phase,
        long baseValue,
        int exponent,
        int completedSteps,
        int totalSteps,
        int activeWorkerCount)
    {
        const int phaseCount = 3;

        int phaseNumber =
            phase switch
            {
                CalculationProgressPhase.Preparing => 1,
                CalculationProgressPhase.BitShift => 2,
                CalculationProgressPhase.DecimalShift => 2,
                CalculationProgressPhase.Computing => 2,
                CalculationProgressPhase.Formatting => 3,
                CalculationProgressPhase.Completed => phaseCount,
                _ => 1
            };

        long totalBitShift = exponent;

        if (phase == CalculationProgressPhase.BitShift &&
            _powerRootEngine.TryGetPowerOfTwoExponent(
                baseValue,
                out int basePowerOfTwoExponent))
        {
            totalBitShift =
                checked(
                    (long)basePowerOfTwoExponent *
                    exponent);
        }

        return phase switch
        {
            CalculationProgressPhase.Preparing =>
                Format(
                    "PowerRoot.ProgressPhasePreparing",
                    phaseNumber,
                    phaseCount),
            CalculationProgressPhase.BitShift =>
                Format(
                    "PowerRoot.ProgressPhaseBitShift",
                    phaseNumber,
                    phaseCount,
                    totalBitShift.ToString(
                        "N0",
                        CultureInfo.InvariantCulture),
                    baseValue.ToString(
                        "N0",
                        CultureInfo.InvariantCulture)),
            CalculationProgressPhase.DecimalShift =>
                Format(
                    "PowerRoot.ProgressPhaseDecimalShift",
                    phaseNumber,
                    phaseCount),
            CalculationProgressPhase.Computing =>
                string.Join(
                    Environment.NewLine,
                    Format(
                        "PowerRoot.ProgressPhaseComputing",
                        phaseNumber,
                        phaseCount,
                        completedSteps,
                        totalSteps),
                    Format(
                        "PowerRoot.ProgressWorkers",
                        activeWorkerCount)),
            CalculationProgressPhase.Formatting =>
                activeWorkerCount > 1
                    ? string.Join(
                        Environment.NewLine,
                        Format(
                            "PowerRoot.ProgressPhaseFormatting",
                            phaseNumber,
                            phaseCount),
                        Format(
                            "PowerRoot.ProgressWorkers",
                            activeWorkerCount))
                    : Format(
                        "PowerRoot.ProgressPhaseFormatting",
                        phaseNumber,
                        phaseCount),
            CalculationProgressPhase.Completed =>
                Format(
                    "PowerRoot.ProgressPhaseCompleted",
                    phaseCount,
                    phaseCount),
            _ =>
                string.Empty
        };
    }

    private static PowerCalculationState CreateVirtualBitShiftCalculationState(
        long baseValue,
        int exponent,
        long totalBitShift,
        TimeSpan elapsed)
    {
        int digitCount =
            EstimateDecimalDigitCount(
                baseValue,
                exponent);

        bool isNegative =
            baseValue < 0 &&
            (exponent & 1) != 0;

        string compactResult =
            CreateCompactResult(
                isNegative,
                baseValue,
                exponent,
                digitCount,
                exactResultText: null);

        // No giant BigInteger or NTT workspace is materialized during the
        // calculation. The exact value is represented as sign + 2^k. Keep a
        // small conservative allowance for UI/logarithm/formatting work; the
        // process-private-memory diagnostic below remains authoritative.
        const long VirtualBitShiftWorkingSetAllowance =
            16L * 1024L * 1024L;

        return new PowerCalculationState(
            baseValue,
            exponent,
            BigInteger.Zero,
            digitCount,
            compactResult,
            ActiveWorkerCount: 1,
            Strategy: PowerComputationStrategy.BitShift,
            DecimalZeroCount: 0,
            IsNegative: isNegative,
            EstimatedPeakRamBytes: VirtualBitShiftWorkingSetAllowance,
            Elapsed: elapsed,
            ParallelMagnitude: null,
            ParallelDiagnostics: null,
            VirtualBitShiftExponent: totalBitShift);
    }

    private static PowerCalculationState CreateBigIntegerCalculationState(
        long baseValue,
        int exponent,
        BigInteger result,
        PowerComputationStrategy strategy,
        TimeSpan elapsed,
        ParallelBigUnsigned? preparedBitShiftMagnitude)
    {
        int estimatedDigitCount =
            EstimateDecimalDigitCount(
                baseValue,
                exponent);

        string? exactResultText =
            null;

        int digitCount =
            estimatedDigitCount;

        if (preparedBitShiftMagnitude is not null)
        {
            digitCount =
                preparedBitShiftMagnitude.DigitCount;
        }
        else if (estimatedDigitCount <=
                 ExactPreviewConversionLimit)
        {
            exactResultText =
                result.ToString(
                    CultureInfo.InvariantCulture);

            digitCount =
                exactResultText[0] == '-'
                    ? exactResultText.Length - 1
                    : exactResultText.Length;
        }

        string compactResult =
            CreateCompactResult(
                result.Sign < 0,
                baseValue,
                exponent,
                digitCount,
                exactResultText);

        long estimatedPeakRamBytes =
            EstimatePeakRamBytes(
                result,
                digitCount,
                activeWorkerCount: 1,
                exactResultText is not null);

        if (preparedBitShiftMagnitude is not null)
        {
            estimatedPeakRamBytes =
                checked(
                    estimatedPeakRamBytes +
                    preparedBitShiftMagnitude.StorageBytes);
        }

        BigInteger retainedResult =
            strategy == PowerComputationStrategy.BitShift &&
            exponent > LegacyNttMaximumExponent &&
            preparedBitShiftMagnitude is not null
                ? BigInteger.Zero
                : result;

        return new PowerCalculationState(
            baseValue,
            exponent,
            retainedResult,
            digitCount,
            compactResult,
            ActiveWorkerCount: 1,
            Strategy: strategy,
            DecimalZeroCount: 0,
            IsNegative: result.Sign < 0,
            EstimatedPeakRamBytes: estimatedPeakRamBytes,
            Elapsed: elapsed,
            ParallelMagnitude:
                strategy == PowerComputationStrategy.BitShift
                    ? preparedBitShiftMagnitude
                    : null,
            ParallelDiagnostics: null);
    }

    private static PowerCalculationState CreateParallelCalculationState(
        long baseValue,
        int exponent,
        ParallelBigUnsigned magnitude,
        bool isNegative,
        int activeWorkerCount,
        TimeSpan elapsed,
        ParallelPowerDiagnostics diagnostics,
        bool useSimdForFormatting)
    {
        int digitCount =
            magnitude.DigitCount;

        string? exactResultText =
            null;

        if (digitCount <=
            ExactPreviewConversionLimit)
        {
            exactResultText =
                magnitude.ToDecimalString(
                    useSimdForFormatting);

            if (isNegative)
            {
                exactResultText =
                    $"-{exactResultText}";
            }
        }

        string compactResult =
            CreateCompactResult(
                isNegative,
                baseValue,
                exponent,
                digitCount,
                exactResultText);

        // Transform buffers, compact P1 residues and the normalized result
        // dominate the peak. CRT -> carry is block-streamed and reuses the P1
        // residue array in place. v32 additionally reuses a dead inverse-P2
        // tail as CRT scratch when possible, but keep this estimate deliberately
        // conservative rather than subtracting a workload-dependent few MiB.
        // Split mode still keeps both partial powers alive before final combine.
        long estimatedPeakRamBytes;

        if (diagnostics.UsedMemoryBoundedLargePower)
        {
            // Large mode keeps uint32 operands/results plus at most bounded
            // 2^26 NTT segments resident. A 4x final-magnitude envelope tracks
            // the accumulator + live operands conservatively, then adds the
            // measured peak NTT lease and a modest runtime/twiddle headroom.
            estimatedPeakRamBytes =
                checked(
                    magnitude.StorageBytes * 4L +
                    diagnostics.NttWorkspacePeakBytes +
                    256L * 1024L * 1024L +
                    (exactResultText is not null
                        ? (long)exactResultText.Length * sizeof(char)
                        : 0L));
        }
        else
        {
            long parallelStorageMultiplier =
                diagnostics.UsedExponentSplit
                    ? 16L
                    : 12L;

            estimatedPeakRamBytes =
                checked(
                    magnitude.StorageBytes *
                    parallelStorageMultiplier +
                    (exactResultText is not null
                        ? (long)exactResultText.Length *
                          sizeof(char)
                        : 0L));
        }

        if (!diagnostics.UsedMemoryBoundedLargePower &&
            diagnostics.UsedExponentSplit)
        {
            // v31 shares one P1/P2 twiddle-plan set across both concurrent
            // PowSplit branches. v30 held one identical set per branch. Each
            // plan owns forward + inverse arrays, so the duplicate live set was
            // ~64 MiB on 8T+ systems (2^21 cache) or ~32 MiB below that. Keep
            // the existing conservative multiplier and subtract only this
            // concrete storage reduction.
            long sharedTwiddleSavingsBytes =
                (Environment.ProcessorCount >= 8
                    ? 64L
                    : 32L) *
                1024L *
                1024L;

            estimatedPeakRamBytes =
                Math.Max(
                    magnitude.StorageBytes,
                    estimatedPeakRamBytes -
                    sharedTwiddleSavingsBytes);
        }

        return new PowerCalculationState(
            baseValue,
            exponent,
            BigInteger.Zero,
            digitCount,
            compactResult,
            ActiveWorkerCount: activeWorkerCount,
            Strategy: PowerComputationStrategy.ParallelNttPower,
            DecimalZeroCount: 0,
            IsNegative: isNegative,
            EstimatedPeakRamBytes: estimatedPeakRamBytes,
            Elapsed: elapsed,
            ParallelMagnitude: magnitude,
            ParallelDiagnostics: diagnostics);
    }

    private static PowerCalculationState CreatePowerOfTenCalculationState(
        long baseValue,
        int exponent,
        int decimalExponent,
        TimeSpan elapsed)
    {
        int zeroCount =
            checked(
                decimalExponent *
                exponent);

        int digitCount =
            checked(
                zeroCount + 1);

        bool isNegative =
            baseValue < 0 &&
            (exponent & 1) != 0;

        string sign =
            isNegative
                ? "−"
                : string.Empty;

        string compactResult;

        if (digitCount <=
            FullResultDigitThreshold)
        {
            string exactResult =
                $"{(isNegative ? "-" : string.Empty)}1" +
                new string(
                    '0',
                    zeroCount);

            compactResult =
                IntegerInputFormatter.FormatWhileTyping(
                    exactResult);
        }
        else
        {
            compactResult =
                $"{sign}1 × 10{ToSuperscript(zeroCount)}";
        }

        // The number is represented symbolically as sign + 1 + zero count.
        // Only a fixed-size zero block is allocated later during TXT export.
        long estimatedPeakRamBytes =
            ExportLeafDigitCount *
            sizeof(char);

        return new PowerCalculationState(
            baseValue,
            exponent,
            BigInteger.Zero,
            digitCount,
            compactResult,
            ActiveWorkerCount: 1,
            Strategy: PowerComputationStrategy.DecimalPowerOfTen,
            DecimalZeroCount: zeroCount,
            IsNegative: isNegative,
            EstimatedPeakRamBytes: estimatedPeakRamBytes,
            Elapsed: elapsed,
            ParallelMagnitude: null,
            ParallelDiagnostics: null);
    }

    private static int EstimateDecimalDigitCount(
        long baseValue,
        int exponent)
    {
        if (exponent == 0 ||
            baseValue is 0 or 1 or -1)
        {
            return 1;
        }

        long absoluteBase =
            Math.Abs(
                baseValue);

        int powerOfTen = 0;
        long reducedBase =
            absoluteBase;

        while (reducedBase > 1 &&
               reducedBase % 10 == 0)
        {
            reducedBase /= 10;
            powerOfTen++;
        }

        if (reducedBase == 1)
        {
            return checked(
                exponent *
                powerOfTen +
                1);
        }

        decimal logarithm =
            ComputePowerLogarithmBaseTen(
                absoluteBase,
                exponent);

        return checked(
            (int)decimal.Floor(
                logarithm) +
            1);
    }

    private static string CreateCompactResult(
        bool isNegative,
        long baseValue,
        int exponent,
        int digitCount,
        string? exactResultText)
    {
        if (digitCount <=
            FullResultDigitThreshold)
        {
            string exactText =
                exactResultText ??
                BigInteger.Pow(
                        new BigInteger(baseValue),
                        exponent)
                    .ToString(
                        CultureInfo.InvariantCulture);

            return IntegerInputFormatter.FormatWhileTyping(
                exactText);
        }

        string leadingDigits;

        if (exactResultText is not null)
        {
            string unsignedText =
                exactResultText.TrimStart('-');

            leadingDigits =
                unsignedText[..Math.Min(
                    PreviewLeadingDigits,
                    unsignedText.Length)];
        }
        else
        {
            leadingDigits =
                EstimateLeadingDigits(
                    baseValue,
                    exponent);
        }

        string mantissa =
            leadingDigits.Length > 1
                ? $"{leadingDigits[0]}.{leadingDigits[1..]}"
                : leadingDigits;

        string sign =
            isNegative
                ? "−"
                : string.Empty;

        return
            $"{sign}{mantissa} × 10{ToSuperscript(digitCount - 1)}";
    }

    private static string EstimateLeadingDigits(
        long baseValue,
        int exponent)
    {
        decimal logarithm =
            ComputePowerLogarithmBaseTen(
                Math.Abs(
                    baseValue),
                exponent);

        decimal fractionalPart =
            logarithm -
            decimal.Floor(
                logarithm);

        decimal mantissa =
            DecimalExp(
                fractionalPart *
                NaturalLogarithmOfTen);

        decimal previewScale =
            Pow10Decimal(
                PreviewLeadingDigits - 1);

        long leadingValue =
            decimal.ToInt64(
                decimal.Floor(
                    mantissa *
                    previewScale));

        long lowerBound =
            (long)previewScale;

        long upperBound =
            checked(
                lowerBound * 10L);

        leadingValue =
            Math.Clamp(
                leadingValue,
                lowerBound,
                upperBound - 1L);

        return leadingValue.ToString(
            $"D{PreviewLeadingDigits}",
            CultureInfo.InvariantCulture);
    }

    private static decimal ComputePowerLogarithmBaseTen(
        long absoluteBase,
        int exponent)
    {
        decimal normalizedBase =
            absoluteBase;

        int decimalScale = 0;
        decimal divisor = 1m;

        while (normalizedBase >=
               divisor * 10m)
        {
            divisor *= 10m;
            decimalScale++;
        }

        normalizedBase /=
            divisor;

        decimal baseLogarithm =
            decimalScale +
            NaturalLogarithm(
                normalizedBase) /
            NaturalLogarithmOfTen;

        return baseLogarithm *
               exponent;
    }

    private static decimal NaturalLogarithm(
        decimal value)
    {
        int binaryScale = 0;

        while (value >= 2m)
        {
            value /= 2m;
            binaryScale++;
        }

        decimal ratio =
            (value - 1m) /
            (value + 1m);

        decimal ratioSquared =
            ratio * ratio;

        decimal term =
            ratio;

        decimal sum = 0m;

        for (int denominator = 1;
             denominator <= 199;
             denominator += 2)
        {
            decimal nextSum =
                sum +
                term /
                denominator;

            if (nextSum == sum)
            {
                break;
            }

            sum =
                nextSum;

            term *=
                ratioSquared;
        }

        return 2m * sum +
               binaryScale *
               NaturalLogarithmOfTwo;
    }

    private static decimal DecimalExp(
        decimal value)
    {
        decimal sum = 1m;
        decimal term = 1m;

        for (int divisor = 1;
             divisor <= 199;
             divisor++)
        {
            term =
                term *
                value /
                divisor;

            decimal nextSum =
                sum +
                term;

            if (nextSum == sum)
            {
                break;
            }

            sum =
                nextSum;
        }

        return sum;
    }

    private static decimal Pow10Decimal(
        int exponent)
    {
        decimal result = 1m;

        for (int index = 0;
             index < exponent;
             index++)
        {
            result *= 10m;
        }

        return result;
    }

    private static long EstimatePeakRamBytes(
        BigInteger result,
        int digitCount,
        int activeWorkerCount,
        bool createdExactText)
    {
        long resultBytes =
            BigInteger.Abs(
                    result)
                .GetByteCount(
                    isUnsigned: true);

        double intermediateMultiplier =
            2.5d +
            Math.Min(
                activeWorkerCount,
                16) *
            0.65d;

        long intermediateBytes =
            (long)Math.Ceiling(
                resultBytes *
                intermediateMultiplier);

        long textBytes =
            createdExactText
                ? (long)digitCount *
                  sizeof(char)
                : 0;

        return checked(
            intermediateBytes +
            textBytes);
    }

    private void ShowResult(
        PowerCalculationState state)
    {
        // Gán mới cả FormattedString để buộc MAUI Windows vẽ lại kết quả.
        // Chỉ đổi Text trên Span được tạo từ XAML có thể không invalidate Label.
        Span expressionSpan =
            new()
            {
                Text = FormatDisplayExpression(
                    state.BaseValue,
                    state.Exponent),
                FontSize = 32,
                FontAttributes = FontAttributes.Bold
            };

        expressionSpan.SetDynamicResource(
            Span.TextColorProperty,
            "PrimaryColor");

        Span equalsSpan =
            new()
            {
                Text = " = ",
                FontSize = 32,
                FontAttributes = FontAttributes.Bold
            };

        equalsSpan.SetDynamicResource(
            Span.TextColorProperty,
            "PrimaryColor");

        Span valueSpan =
            new()
            {
                Text = state.CompactResult,
                FontSize = 32,
                FontAttributes = FontAttributes.Bold
            };

        valueSpan.SetDynamicResource(
            Span.TextColorProperty,
            "PrimaryColor");

        ResultExpressionLabel.FormattedText =
            new FormattedString
            {
                Spans =
                {
                    expressionSpan,
                    equalsSpan,
                    valueSpan
                }
            };

        bool isCompact =
            state.DigitCount >
            FullResultDigitThreshold;

        CopyResultButton.Text =
            Translate(
                isCompact
                    ? "PowerRoot.CopyCompact"
                    : "PowerRoot.CopyResult");

        bool canExport =
            state.DigitCount >=
            ExportDigitThreshold;

        ExportTextButton.IsVisible =
            canExport;

        Grid.SetColumnSpan(
            CopyResultButtonContainer,
            canExport
                ? 1
                : 2);

        if (canExport)
        {
            LargeResultInfoLabel.Text =
                CreateLargeResultInformation(
                    state);
        }

        SolutionLabel.Text =
            CreateSolution(
                state);

        ResultBorder.IsVisible =
            true;

        UpdateDeveloperDiagnosticsVisibility();
    }

    private void OnDiagnosticsToggleClicked(
        object? sender,
        EventArgs e)
    {
        if (!DeveloperModeManager.IsEnabled ||
            !DiagnosticsToggleButton.IsVisible)
        {
            return;
        }

        _isDiagnosticsVisible =
            !_isDiagnosticsVisible;

        LargeResultInfoBorder.IsVisible =
            _isDiagnosticsVisible;

        DiagnosticsToggleButton.Text =
            Translate(
                _isDiagnosticsVisible
                    ? "PowerRoot.HideDetails"
                    : "PowerRoot.ShowDetails");
    }

    private void OnDeveloperModeChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            UpdateDeveloperDiagnosticsVisibility);
    }

    private void UpdateDeveloperDiagnosticsVisibility()
    {
        bool canShowDeveloperDiagnostics =
            DeveloperModeManager.IsEnabled &&
            ResultBorder.IsVisible &&
            _calculationState is not null &&
            _calculationState.DigitCount >=
                ExportDigitThreshold;

        DiagnosticsToggleButton.IsVisible =
            canShowDeveloperDiagnostics;

        if (!canShowDeveloperDiagnostics)
        {
            _isDiagnosticsVisible = false;
        }

        LargeResultInfoBorder.IsVisible =
            canShowDeveloperDiagnostics &&
            _isDiagnosticsVisible;

        DiagnosticsToggleButton.Text =
            Translate(
                _isDiagnosticsVisible
                    ? "PowerRoot.HideDetails"
                    : "PowerRoot.ShowDetails");
    }

    private string CreateSolution(
        PowerCalculationState state)
    {
        string expression =
            FormatDisplayExpression(
                state.BaseValue,
                state.Exponent);

        string formattedBase =
            FormatCompactIntegerForDisplay(
                new BigInteger(
                    state.BaseValue));

        string formattedExponent =
            state.Exponent.ToString(
                "N0",
                CultureInfo.InvariantCulture);

        if (state.Exponent == 0)
        {
            return string.Join(
                Environment.NewLine +
                Environment.NewLine,
                Format(
                    "PowerRoot.StepGiven",
                    expression),
                Translate(
                    "PowerRoot.StepZeroExponent"),
                Format(
                    "PowerRoot.StepResult",
                    state.CompactResult));
        }

        if (state.BaseValue == 0)
        {
            return string.Join(
                Environment.NewLine +
                Environment.NewLine,
                Format(
                    "PowerRoot.StepGiven",
                    expression),
                Format(
                    "PowerRoot.StepZeroBase",
                    formattedExponent),
                Format(
                    "PowerRoot.StepResult",
                    state.CompactResult));
        }

        if (state.Exponent == 1)
        {
            return string.Join(
                Environment.NewLine +
                Environment.NewLine,
                Format(
                    "PowerRoot.StepGiven",
                    expression),
                Format(
                    "PowerRoot.StepFirstExponent",
                    formattedBase),
                Format(
                    "PowerRoot.StepResult",
                    state.CompactResult));
        }

        if (state.BaseValue == 1 ||
            state.BaseValue == -1)
        {
            string unitBaseRule;

            if (state.BaseValue == 1)
            {
                unitBaseRule =
                    Format(
                        "PowerRoot.StepOneBase",
                        formattedExponent,
                        ToSuperscript(
                            state.Exponent));
            }
            else
            {
                unitBaseRule =
                    Format(
                        state.Exponent % 2 == 0
                            ? "PowerRoot.StepNegativeOneEven"
                            : "PowerRoot.StepNegativeOneOdd",
                        formattedExponent,
                        ToSuperscript(
                            state.Exponent));
            }

            return string.Join(
                Environment.NewLine +
                Environment.NewLine,
                Format(
                    "PowerRoot.StepGiven",
                    expression),
                unitBaseRule,
                Format(
                    "PowerRoot.StepResult",
                    state.CompactResult));
        }

        var steps =
            new List<string>
            {
                Format(
                    "PowerRoot.StepGiven",
                    expression),
                Format(
                    "PowerRoot.StepDefinition",
                    formattedBase,
                    formattedExponent),
                CreateSignExplanation(
                    state)
            };

        if (state.Strategy ==
            PowerComputationStrategy.DecimalPowerOfTen)
        {
            int zerosPerFactor =
                state.DecimalZeroCount /
                state.Exponent;

            steps.Add(
                Format(
                    "PowerRoot.StepDecimalPowerRule",
                    zerosPerFactor.ToString(
                        "N0",
                        CultureInfo.InvariantCulture),
                    formattedExponent,
                    state.DecimalZeroCount.ToString(
                        "N0",
                        CultureInfo.InvariantCulture),
                    ToSuperscript(
                        zerosPerFactor),
                    ToSuperscript(
                        state.Exponent),
                    ToSuperscript(
                        state.DecimalZeroCount)));
        }
        else if (state.Strategy ==
                 PowerComputationStrategy.BitShift)
        {
            if (!_powerRootEngine.TryGetPowerOfTwoExponent(
                    state.BaseValue,
                    out int basePowerOfTwoExponent))
            {
                throw new InvalidOperationException(
                    "The bit-shift strategy requires |base| = 2^k.");
            }

            long totalPowerOfTwoExponent =
                checked(
                    (long)basePowerOfTwoExponent *
                    state.Exponent);

            if (basePowerOfTwoExponent == 1)
            {
                steps.Add(
                    Format(
                        "PowerRoot.StepBitShiftBaseTwo",
                        formattedExponent,
                        ToSuperscript(
                            state.Exponent)));
            }
            else
            {
                steps.Add(
                    Format(
                        "PowerRoot.StepBitShiftPowerOfTwo",
                        basePowerOfTwoExponent.ToString(
                            "N0",
                            CultureInfo.InvariantCulture),
                        formattedExponent,
                        totalPowerOfTwoExponent.ToString(
                            "N0",
                            CultureInfo.InvariantCulture),
                        ToSuperscript(
                            basePowerOfTwoExponent),
                        ToSuperscript(
                            state.Exponent),
                        ToSuperscript(
                            totalPowerOfTwoExponent)));
            }
        }
        else
        {
            steps.Add(
                Translate(
                    "PowerRoot.StepRepeatedSquaring"));

            if (state.ParallelDiagnostics?.UsedMemoryBoundedLargePower ==
                true)
            {
                steps.Add(
                    Format(
                        "PowerRoot.StepLargeNttMemoryBounded",
                        state.ParallelDiagnostics.LargePowerChunkExponent.ToString(
                            "N0",
                            CultureInfo.InvariantCulture)));
            }

            int[] selectedPowers =
                GetSelectedBinaryPowers(
                    state.Exponent);

            if (selectedPowers.Length == 1)
            {
                steps.Add(
                    Format(
                        "PowerRoot.StepPowerOfTwoExponent",
                        formattedExponent,
                        ToSuperscript(
                            state.Exponent)));
            }
            else
            {
                string decomposition =
                    string.Join(
                        " + ",
                        selectedPowers.Select(
                            power =>
                                power.ToString(
                                    "N0",
                                    CultureInfo.InvariantCulture)));

                string selectedProduct =
                    string.Join(
                        " × ",
                        selectedPowers.Select(
                            power =>
                                $"a{ToSuperscript(power)}"));

                steps.Add(
                    Format(
                        "PowerRoot.StepDecomposeExponent",
                        formattedExponent,
                        decomposition,
                        selectedProduct,
                        ToSuperscript(
                            state.Exponent)));
            }

            int squaringCount =
                GetHighestSetBitIndex(
                    state.Exponent);

            int selectedProductCount =
                Math.Max(
                    0,
                    selectedPowers.Length - 1);

            steps.Add(
                Format(
                    "PowerRoot.StepOperationCount",
                    squaringCount.ToString(
                        "N0",
                        CultureInfo.InvariantCulture),
                    selectedProductCount.ToString(
                        "N0",
                        CultureInfo.InvariantCulture),
                    formattedExponent));
        }

        steps.Add(
            Format(
                "PowerRoot.StepResultWithDigits",
                state.CompactResult,
                state.DigitCount.ToString(
                    "N0",
                    CultureInfo.InvariantCulture)));

        return string.Join(
            Environment.NewLine +
            Environment.NewLine,
            steps);
    }

    private static string CreateSignExplanation(
        PowerCalculationState state)
    {
        if (state.BaseValue > 0)
        {
            return Translate(
                "PowerRoot.StepPositiveSign");
        }

        return Format(
            state.Exponent % 2 == 0
                ? "PowerRoot.StepNegativeEvenSign"
                : "PowerRoot.StepNegativeOddSign",
            state.Exponent.ToString(
                "N0",
                CultureInfo.InvariantCulture));
    }

    private static int[] GetSelectedBinaryPowers(
        int exponent)
    {
        var selectedPowers =
            new List<int>();

        int power = 1;
        int remaining = exponent;

        while (remaining > 0)
        {
            if ((remaining & 1) != 0)
            {
                selectedPowers.Add(
                    power);
            }

            remaining >>= 1;
            power <<= 1;
        }

        selectedPowers.Reverse();

        return selectedPowers.ToArray();
    }

    private static int GetHighestSetBitIndex(
        int value)
    {
        int index = -1;

        while (value > 0)
        {
            value >>= 1;
            index++;
        }

        return Math.Max(
            0,
            index);
    }

    private string CreateLargeResultInformation(
        PowerCalculationState state)
    {
        var lines =
            new List<string>
            {
            Translate(
                state.Strategy switch
                {
                    PowerComputationStrategy.BitShift =>
                        "PowerRoot.InfoEngineBitShift",
                    PowerComputationStrategy.DecimalPowerOfTen =>
                        "PowerRoot.InfoEnginePowerOfTen",
                    PowerComputationStrategy.ParallelNttPower
                        when state.ParallelDiagnostics?.UsedMemoryBoundedLargePower ==
                             true =>
                        "PowerRoot.InfoEngineParallelNttLarge",
                    PowerComputationStrategy.ParallelNttPower
                        when state.ParallelDiagnostics?.UsedExponentSplit ==
                             true =>
                        "PowerRoot.InfoEngineParallelNttSplit",
                    PowerComputationStrategy.ParallelNttPower =>
                        "PowerRoot.InfoEngineParallelNtt",
                    _ =>
                        "PowerRoot.InfoEngine"
                }),
            Format(
                "PowerRoot.InfoDigits",
                state.DigitCount.ToString(
                    "N0",
                    CultureInfo.InvariantCulture)),
            Format(
                "PowerRoot.InfoRam",
                FormatByteSize(
                    state.EstimatedPeakRamBytes)),
            state.ProcessPrivateMemoryBytes > 0
                ? state.ProcessPrivateMemoryBeforeCleanupBytes >
                  state.ProcessPrivateMemoryBytes
                    ? Format(
                        "PowerRoot.InfoProcessRamTrimmed",
                        FormatByteSize(
                            state.ProcessPrivateMemoryBytes),
                        FormatByteSize(
                            state.ProcessPrivateMemoryBeforeCleanupBytes))
                    : Format(
                        "PowerRoot.InfoProcessRam",
                        FormatByteSize(
                            state.ProcessPrivateMemoryBytes))
                : string.Empty,
            Format(
                "PowerRoot.InfoTxtSize",
                FormatByteSize(
                    EstimateTxtFileSizeBytes(
                        state))),
            Format(
                "PowerRoot.InfoThreads",
                state.ActiveWorkerCount),
            Format(
                "PowerRoot.InfoTime",
                state.Elapsed.TotalSeconds.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture))
            };

        if (state.Strategy ==
                PowerComputationStrategy.ParallelNttPower &&
            state.ParallelDiagnostics is not null)
        {
            ParallelPowerDiagnostics diagnostics =
                state.ParallelDiagnostics;

            lines.Insert(
                4,
                Format(
                    "PowerRoot.InfoWorkerBudget",
                    diagnostics.WorkerCount,
                    CalculationThreadingManager.LogicalProcessorCount));

            lines.Insert(
                5,
                Format(
                    "PowerRoot.InfoNttBufferPool",
                    FormatByteSize(
                        diagnostics.NttWorkspacePeakBytes),
                    FormatByteSize(
                        diagnostics.NttPoolPeakRetainedBytes),
                    diagnostics.NttBufferReuseCount,
                    diagnostics.NttBufferRentCount));

            if (diagnostics.UsedMemoryBoundedLargePower)
            {
                lines.Insert(
                    6,
                    Format(
                        "PowerRoot.InfoLargeNttMode",
                        diagnostics.LargePowerChunkExponent.ToString(
                            "N0",
                            CultureInfo.InvariantCulture),
                        diagnostics.SegmentedNttMultiplicationCount,
                        diagnostics.SegmentedNttPairCount));
            }

            if (diagnostics.UsedExponentSplit)
            {
                lines.Insert(
                    6,
                    Format(
                        "PowerRoot.InfoExponentSplit",
                        diagnostics.FirstExponent.ToString(
                            "N0",
                            CultureInfo.InvariantCulture),
                        diagnostics.SecondExponent.ToString(
                            "N0",
                            CultureInfo.InvariantCulture),
                        diagnostics.FirstBranchWorkerCount,
                        diagnostics.SecondBranchWorkerCount));

                lines.Add(
                    Format(
                        "PowerRoot.InfoExponentSplitProfile",
                        FormatProfileSeconds(
                            diagnostics.FirstBranchElapsed),
                        FormatProfileSeconds(
                            diagnostics.SecondBranchElapsed),
                        FormatProfileSeconds(
                            diagnostics.FinalCombineElapsed)));
            }

            lines.Add(
                Format(
                    "PowerRoot.InfoNttProfile",
                    diagnostics.NttMultiplicationCount,
                    FormatProfileSeconds(
                        diagnostics.BitReversal),
                    FormatProfileSeconds(
                        diagnostics.ForwardTransform),
                    FormatProfileSeconds(
                        diagnostics.InverseTransform)));

            lines.Add(
                Format(
                    "PowerRoot.InfoNttPostProfile",
                    FormatProfileSeconds(
                        diagnostics.Pointwise),
                    FormatProfileSeconds(
                        diagnostics.Crt),
                    FormatProfileSeconds(
                        diagnostics.Carry)));
        }

        lines.RemoveAll(
            static line =>
                string.IsNullOrWhiteSpace(
                    line));

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static string FormatProfileSeconds(
        TimeSpan elapsed)
    {
        return elapsed.TotalSeconds.ToString(
            "0.###",
            CultureInfo.InvariantCulture);
    }

    private async void OnStopClicked(
        object? sender,
        EventArgs e)
    {
        if (!_isCalculating ||
            _calculationCancellation is null ||
            _calculationCancellation.IsCancellationRequested)
        {
            return;
        }

        bool shouldStop =
            await ConfirmStopCalculationAsync(
                closingApplication: false);

        if (!shouldStop ||
            !_isCalculating)
        {
            return;
        }

        RequestCalculationStop();
    }

    private async Task<bool> ConfirmStopCalculationAsync(
        bool closingApplication)
    {
        return await ConfirmStopOperationAsync(
            closingApplication
                ? "PowerRoot.ConfirmCloseCalculation"
                : "PowerRoot.ConfirmStopCalculation");
    }

    private async Task<bool> ConfirmStopExportAsync(
        bool closingApplication)
    {
        return await ConfirmStopOperationAsync(
            closingApplication
                ? "PowerRoot.ConfirmCloseExport"
                : "PowerRoot.ConfirmStopExport");
    }

    private async Task<bool> ConfirmStopOperationAsync(
        string messageKey)
    {
        if (_isStopConfirmationVisible)
        {
            return false;
        }

        Page? dialogPage =
            Shell.Current;

        dialogPage ??=
            Application.Current?.Windows.FirstOrDefault()?.Page;

        if (dialogPage is null)
        {
            return false;
        }

        _isStopConfirmationVisible = true;

        try
        {
            return await MaterialDialogService.ConfirmAsync(
                dialogPage,
                Translate(
                    "PowerRoot.ConfirmStopTitle"),
                Translate(
                    messageKey),
                Translate(
                    "PowerRoot.ConfirmYes"),
                Translate(
                    "PowerRoot.ConfirmNo"));
        }
        finally
        {
            _isStopConfirmationVisible = false;
        }
    }

    private void RequestCalculationStop()
    {
        if (!_isCalculating ||
            _calculationCancellation is null ||
            _calculationCancellation.IsCancellationRequested)
        {
            return;
        }

        StopButton.IsEnabled = false;
        ProgressTitleLabel.Text =
            Translate(
                "PowerRoot.ProgressStopping");

        _calculationCancellation.Cancel();
    }

    private void RequestExportStop()
    {
        if (!_isExporting ||
            _exportCancellation is null ||
            _exportCancellation.IsCancellationRequested)
        {
            return;
        }

        _exportCancellation.Cancel();
        ExportTextButton.IsEnabled = false;
        ExportTextButton.Text =
            Translate(
                "PowerRoot.ExportStopping");

        ShowExportStatus(
            Translate(
                "PowerRoot.ExportStopping"),
            ExportProgressBar.Progress,
            isBusy: true);
    }

#if WINDOWS
    private async Task<bool> ConfirmWindowsWindowCloseAsync()
    {
        if (_isExporting)
        {
            bool shouldStopExport =
                await ConfirmStopExportAsync(
                    closingApplication: true);

            if (!shouldStopExport)
            {
                return false;
            }

            Task? exportCompletionTask =
                _exportCompletionSource?.Task;

            RequestExportStop();

            if (exportCompletionTask is not null)
            {
                await exportCompletionTask;
            }
        }

        if (_isCalculating)
        {
            bool shouldStopCalculation =
                await ConfirmStopCalculationAsync(
                    closingApplication: true);

            if (!shouldStopCalculation)
            {
                return false;
            }

            Task? calculationCompletionTask =
                _calculationCompletionSource?.Task;

            RequestCalculationStop();

            if (calculationCompletionTask is not null)
            {
                await calculationCompletionTask;
            }
        }

        return true;
    }
#endif

    private void UpdateWindowsCloseGuard()
    {
#if WINDOWS
        if (_isCalculating ||
            _isExporting)
        {
            MathSolver.Platforms.Windows.WindowStateManager.SetCloseGuard(
                this,
                ConfirmWindowsWindowCloseAsync);
        }
        else
        {
            MathSolver.Platforms.Windows.WindowStateManager.ClearCloseGuard(
                this);
        }
#endif
    }

    private async void OnCopyResultClicked(
        object? sender,
        EventArgs e)
    {
        if (_calculationState is null)
        {
            return;
        }

        await Clipboard.Default.SetTextAsync(
            _calculationState.CompactResult);

        string originalText =
            CopyResultButton.Text;

        CopyResultButton.Text =
            Translate(
                "PowerRoot.Copied");

        await Task.Delay(
            1200);

        if (_calculationState is not null)
        {
            CopyResultButton.Text =
                originalText;
        }
    }

    private async void OnExportTextClicked(
        object? sender,
        EventArgs e)
    {
        if (_isExporting)
        {
            if (_exportCancellation is null ||
                _exportCancellation.IsCancellationRequested)
            {
                return;
            }

            bool shouldStop =
                await ConfirmStopExportAsync(
                    closingApplication: false);

            if (!shouldStop ||
                !_isExporting)
            {
                return;
            }

            RequestExportStop();
            return;
        }

        PowerCalculationState? state =
            _calculationState;

        if (state is null ||
            state.DigitCount <
            ExportDigitThreshold)
        {
            return;
        }

        string? temporaryPath =
            null;

        _exportCancellation?.Dispose();
        _exportCancellation =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            _exportCancellation.Token;

        var exportCompletionSource =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        _exportCompletionSource =
            exportCompletionSource;

        _isExporting = true;
        UpdateWindowsCloseGuard();

        SetInputEnabled(
            enabled: false);

        CopyResultButton.IsEnabled = false;
        ExportTextButton.IsEnabled = true;
        ExportTextButton.Text =
            Translate(
                "PowerRoot.ExportStop");

        ShowExportStatus(
            Translate(
                "PowerRoot.ExportPreparing"),
            progress: 0d,
            isBusy: true);

        // Cho phep MAUI ve trang thai "Dang tao TXT" truoc khi bat dau
        // tach va ghi tung khoi chu so cua BigInteger.
        await Task.Yield();

        try
        {
            string fileName =
                $"power_{state.BaseValue}_{state.Exponent}.txt";

            temporaryPath =
                Path.Combine(
                    FileSystem.CacheDirectory,
                    $"{Guid.NewGuid():N}_{fileName}");

            int lastReportedCreationBlock =
                0;

            long lastCreationReportTimestamp =
                Stopwatch.GetTimestamp();

            // Cap nhat toi da khoang 400 moc tien trinh, hoac sau moi 100 ms.
            // Neu dua ca hang chuc nghin block 4 KB vao UI dispatcher, Windows
            // co the trong nhu bi treo du file van dang duoc ghi binh thuong.
            Action<ExportFileProgress> creationProgress =
                update =>
                {
                    int minimumBlockDelta =
                        Math.Max(
                            1,
                            update.TotalBlocks /
                            400);

                    bool isFinalUpdate =
                        update.CompletedBlocks >=
                        update.TotalBlocks;

                    bool shouldReport =
                        isFinalUpdate ||
                        update.CompletedBlocks -
                        lastReportedCreationBlock >=
                        minimumBlockDelta ||
                        Stopwatch.GetElapsedTime(
                            lastCreationReportTimestamp) >=
                        TimeSpan.FromMilliseconds(
                            100d);

                    if (!shouldReport)
                    {
                        return;
                    }

                    double normalizedProgress =
                        Math.Clamp(
                            update.TotalBlocks > 0
                                ? (double)update.CompletedBlocks /
                                  update.TotalBlocks
                                : 0d,
                            0d,
                            1d);

                    lastReportedCreationBlock =
                        update.CompletedBlocks;

                    lastCreationReportTimestamp =
                        Stopwatch.GetTimestamp();

                    MainThread
                        .InvokeOnMainThreadAsync(
                            () =>
                            {
                                ShowExportStatus(
                                    CreateExportProgressMessage(
                                        update),
                                    normalizedProgress,
                                    isBusy: true);
                            })
                        .GetAwaiter()
                        .GetResult();
                };

            // Read the shared Hardware acceleration switch at export start.
            // Windows dispatches to AVX2 when available; Android ARM64 dispatches
            // to NEON/AdvSIMD. Turning the switch off forces the scalar formatter.
            bool useSimdForExport =
                CalculationAccelerationManager.UsePowerExportSimd;

            await Task.Run(
                () => WriteFullResultFile(
                    temporaryPath,
                    state,
                    creationProgress,
                    cancellationToken,
                    useSimdForExport),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            long fileSizeBytes =
                new FileInfo(
                    temporaryPath)
                .Length;

            ShowExportStatus(
                Format(
                    "PowerRoot.ExportSavingProgress",
                    0),
                progress: 0d,
                isBusy: true);

            ExportSaveResult saveResult =
                await SaveTemporaryFileAsync(
                    fileName,
                    temporaryPath,
                    fileSizeBytes,
                    cancellationToken);

            if (saveResult.IsSuccessful)
            {
                string savedPath =
                    !string.IsNullOrWhiteSpace(
                        saveResult.FilePath)
                        ? saveResult.FilePath!
                        : fileName;

                ShowExportStatus(
                    Format(
                        "PowerRoot.ExportSuccess",
                        savedPath,
                        FormatByteSize(
                            fileSizeBytes)),
                    progress: 1d,
                    isBusy: false);
            }
            else if (saveResult.IsCanceled ||
                     saveResult.Exception is
                     OperationCanceledException ||
                     cancellationToken.IsCancellationRequested)
            {
                ShowExportStatus(
                    Translate(
                        "PowerRoot.ExportCanceled"),
                    progress: 0d,
                    isBusy: false);
            }
            else
            {
                string errorMessage =
                    Format(
                        "PowerRoot.ExportError",
                        saveResult.Exception?.Message ??
                        Translate(
                            "PowerRoot.UnknownError"));

                ShowExportStatus(
                    errorMessage,
                    progress: 0d,
                    isBusy: false);

                ShowError(
                    errorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            ShowExportStatus(
                Translate(
                    "PowerRoot.ExportCanceled"),
                progress: 0d,
                isBusy: false);
        }
        catch (Exception exception)
        {
            string errorMessage =
                Format(
                    "PowerRoot.ExportError",
                    exception.Message);

            ShowExportStatus(
                errorMessage,
                progress: 0d,
                isBusy: false);

            ShowError(
                errorMessage);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(
                    temporaryPath))
            {
                try
                {
                    File.Delete(
                        temporaryPath);
                }
                catch
                {
                    // File tam trong cache se duoc he dieu hanh don dep.
                }
            }

            _isExporting = false;
            SetInputEnabled(
                enabled: true);

            CopyResultButton.IsEnabled = true;
            ExportTextButton.IsEnabled = true;
            ExportTextButton.Text =
                Translate(
                    "PowerRoot.ExportTxt");

            ExportActivityIndicator.IsRunning = false;
            ExportActivityIndicator.IsVisible = false;

            _exportCancellation?.Dispose();
            _exportCancellation = null;

            exportCompletionSource.TrySetResult(
                true);

            if (ReferenceEquals(
                    _exportCompletionSource,
                    exportCompletionSource))
            {
                _exportCompletionSource = null;
            }

            UpdateWindowsCloseGuard();
        }
    }

    private async Task<ExportSaveResult> SaveTemporaryFileAsync(
        string fileName,
        string temporaryPath,
        long fileSizeBytes,
        CancellationToken cancellationToken)
    {
#if WINDOWS
        var picker =
            new FileSavePicker
            {
                SuggestedStartLocation =
                    PickerLocationId.DocumentsLibrary,
                SuggestedFileName =
                    Path.GetFileNameWithoutExtension(
                        fileName)
            };

        picker.FileTypeChoices.Add(
            "Text file",
            [".txt"]);

        MauiWinUIWindow nativeWindow =
            Application.Current?
                .Windows
                .FirstOrDefault()?
                .Handler?
                .PlatformView as MauiWinUIWindow ??
            throw new InvalidOperationException(
                "The Windows application window is not ready.");

        nint windowHandle =
            WindowNative.GetWindowHandle(
                nativeWindow);

        InitializeWithWindow.Initialize(
            picker,
            windowHandle);

        global::Windows.Storage.StorageFile? destinationFile =
            await picker.PickSaveFileAsync();

        if (destinationFile is null)
        {
            return ExportSaveResult.Canceled;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // PickSaveFileAsync only chooses the destination. Copy the bytes
        // ourselves so large files do not keep the MAUI UI thread inside the
        // toolkit saver after the file already exists on disk.
        await ReportSaveProgressAsync(
            0d);

        await using Stream destinationStream =
            await destinationFile
                .OpenStreamForWriteAsync()
                .ConfigureAwait(
                    false);

        destinationStream.SetLength(
            0L);

        if (destinationStream.CanSeek)
        {
            destinationStream.Position =
                0L;
        }

        await CopyTemporaryFileAsync(
                temporaryPath,
                destinationStream,
                fileSizeBytes,
                cancellationToken)
            .ConfigureAwait(
                false);

        string savedPath =
            !string.IsNullOrWhiteSpace(
                destinationFile.Path)
                ? destinationFile.Path
                : destinationFile.Name;

        return ExportSaveResult.Success(
            savedPath);
#else
        await using FileStream sourceStream =
            File.OpenRead(
                temporaryPath);

        var saveProgress =
            new Progress<double>(
                progress =>
                {
                    double normalizedProgress =
                        Math.Clamp(
                            progress,
                            0d,
                            1d);

                    ShowExportStatus(
                        Format(
                            "PowerRoot.ExportSavingProgress",
                            Math.Floor(
                                normalizedProgress *
                                100d)),
                        normalizedProgress,
                        isBusy: true);
                });

        FileSaverResult toolkitResult =
            await FileSaver.Default.SaveAsync(
                fileName,
                sourceStream,
                saveProgress,
                cancellationToken);

        return new ExportSaveResult(
            toolkitResult.IsSuccessful,
            toolkitResult.Exception is OperationCanceledException,
            toolkitResult.FilePath,
            toolkitResult.Exception);
#endif
    }

#if WINDOWS
    private async Task CopyTemporaryFileAsync(
        string temporaryPath,
        Stream destinationStream,
        long fileSizeBytes,
        CancellationToken cancellationToken)
    {
        const int CopyBufferSize =
            1024 * 1024;

        byte[] buffer =
            GC.AllocateUninitializedArray<byte>(
                CopyBufferSize);

        await using var sourceStream =
            new FileStream(
                temporaryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        long copiedBytes =
            0L;

        double lastReportedProgress =
            0d;

        long lastReportTimestamp =
            Stopwatch.GetTimestamp();

        while (true)
        {
            int bytesRead =
                await sourceStream.ReadAsync(
                        buffer.AsMemory(),
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            if (bytesRead == 0)
            {
                break;
            }

            await destinationStream.WriteAsync(
                    buffer.AsMemory(
                        0,
                        bytesRead),
                    cancellationToken)
                .ConfigureAwait(
                    false);

            copiedBytes +=
                bytesRead;

            // Reserve 100% for FlushAsync: seeing 100% now guarantees that
            // every byte has also been flushed and the save operation ended.
            double normalizedProgress =
                fileSizeBytes > 0L
                    ? Math.Min(
                        0.99d,
                        (double)copiedBytes /
                        fileSizeBytes)
                    : 0.99d;

            bool shouldReport =
                normalizedProgress -
                lastReportedProgress >=
                0.0025d ||
                Stopwatch.GetElapsedTime(
                    lastReportTimestamp) >=
                TimeSpan.FromMilliseconds(
                    100d);

            if (!shouldReport)
            {
                continue;
            }

            await ReportSaveProgressAsync(
                    normalizedProgress)
                .ConfigureAwait(
                    false);

            lastReportedProgress =
                normalizedProgress;

            lastReportTimestamp =
                Stopwatch.GetTimestamp();
        }

        await destinationStream.FlushAsync(
                cancellationToken)
            .ConfigureAwait(
                false);

        await ReportSaveProgressAsync(
                1d)
            .ConfigureAwait(
                false);
    }

    private Task ReportSaveProgressAsync(
        double progress)
    {
        double normalizedProgress =
            Math.Clamp(
                progress,
                0d,
                1d);

        return MainThread.InvokeOnMainThreadAsync(
            () =>
            {
                ShowExportStatus(
                    Format(
                        "PowerRoot.ExportSavingProgress",
                        Math.Floor(
                            normalizedProgress *
                            100d)),
                    normalizedProgress,
                    isBusy: true);
            });
    }
#endif

    private static void WriteFullResultFile(
        string filePath,
        PowerCalculationState state,
        Action<ExportFileProgress>? progress,
        CancellationToken cancellationToken,
        bool useSimdForExport)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ParallelBigUnsigned? exportMagnitude =
            state.ParallelMagnitude;

        if (state.Strategy ==
                PowerComputationStrategy.BitShift &&
            state.VirtualBitShiftExponent >
                PowerRootEngine.MaximumBigIntegerPowerOfTwoExponent &&
            exportMagnitude is null)
        {
            // The UI calculation intentionally keeps 2^k virtual once k exceeds
            // the .NET BigInteger limit. Only an explicit full-TXT export pays
            // the cost of materializing base-10,000 limbs. Reuse the existing
            // exact memory-bounded NTT/CRT engine on the original |base|^n so
            // the export stays exact without ever constructing an oversized
            // BigInteger.
            int exportWorkerCount =
                CalculationThreadingManager.UseMultithreading
                    ? CalculationThreadingManager.RecommendedWorkerCount
                    : 1;

            ulong unsignedBase =
                state.BaseValue < 0
                    ? unchecked((ulong)(-(state.BaseValue + 1))) + 1UL
                    : (ulong)state.BaseValue;

            ParallelPowerResult prepared =
                state.Exponent > LegacyNttMaximumExponent
                    ? ParallelBigUnsigned.PowMemoryBounded(
                        unsignedBase,
                        state.Exponent,
                        exportWorkerCount,
                        (completed, total) =>
                            progress?.Invoke(
                                new ExportFileProgress(
                                    ExportFilePhase.Preparing,
                                    completed,
                                    Math.Max(1, total))),
                        cancellationToken)
                    : ParallelBigUnsigned.Pow(
                        unsignedBase,
                        state.Exponent,
                        exportWorkerCount,
                        (completed, total) =>
                            progress?.Invoke(
                                new ExportFileProgress(
                                    ExportFilePhase.Preparing,
                                    completed,
                                    Math.Max(1, total))),
                        cancellationToken);

            exportMagnitude =
                prepared.Magnitude;

            cancellationToken.ThrowIfCancellationRequested();
        }

        int totalBlocks =
            state.Strategy ==
                PowerComputationStrategy.DecimalPowerOfTen
                ? checked(
                    1 +
                    (state.DecimalZeroCount +
                     ExportLeafDigitCount - 1) /
                    ExportLeafDigitCount)
                : checked(
                    (state.DigitCount +
                     ExportLeafDigitCount - 1) /
                    ExportLeafDigitCount);

        int completedBlocks = 0;
        object progressGate =
            new();

        void ReportProgress(
            ExportFilePhase phase,
            int increment)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (progressGate)
            {
                completedBlocks =
                    Math.Min(
                        totalBlocks,
                        checked(
                            completedBlocks +
                            increment));

                progress?.Invoke(
                    new ExportFileProgress(
                        phase,
                        completedBlocks,
                        totalBlocks));
            }
        }

        void ReportBlockWritten()
        {
            ReportProgress(
                ExportFilePhase.Writing,
                1);
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var writer =
            new StreamWriter(
                filePath,
                append: false,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: true));

        writer.WriteLine(
            $"Expression: {FormatPlainExpression(state.BaseValue, state.Exponent)}");

        writer.WriteLine(
            state.Strategy ==
            PowerComputationStrategy.DecimalPowerOfTen
                ? "Engine: direct decimal power-of-ten generation"
                : state.Strategy ==
                  PowerComputationStrategy.ParallelNttPower
                    ? state.ParallelDiagnostics?.UsedMemoryBoundedLargePower == true
                        ? "Engine: memory-bounded segmented/in-place exact NTT/CRT power"
                        : "Engine: parallel exact NTT/CRT power"
                    : state.Strategy ==
                      PowerComputationStrategy.BitShift
                        ? state.VirtualBitShiftExponent >
                          PowerRootEngine.MaximumBigIntegerPowerOfTwoExponent
                            ? "Engine: virtual exact 2^k bit shift + on-demand memory-bounded NTT/CRT TXT export"
                            : "Engine: BigInteger bit shift + prepared base-10,000 export"
                        : "Engine: BigInteger");

        int exactExportDigitCount =
            exportMagnitude?.DigitCount ??
            state.DigitCount;

        writer.WriteLine(
            $"Digits: {exactExportDigitCount.ToString(CultureInfo.InvariantCulture)}");

        writer.WriteLine();
        writer.WriteLine("Result:");

        cancellationToken.ThrowIfCancellationRequested();

        if (state.Strategy ==
            PowerComputationStrategy.DecimalPowerOfTen)
        {
            WritePowerOfTenDecimalBlocks(
                writer,
                state.DecimalZeroCount,
                state.IsNegative,
                ReportBlockWritten,
                cancellationToken);
        }
        else if (exportMagnitude is not null)
        {
            if (state.IsNegative)
            {
                writer.Write('-');
            }

            exportMagnitude.WriteDecimalBlocks(
                writer,
                ExportLeafDigitCount,
                ReportBlockWritten,
                cancellationToken,
                useSimdForExport);
        }
        else
        {
            BigInteger unsignedResult =
                BigInteger.Abs(
                    state.Result);

            if (state.Result.Sign < 0)
            {
                writer.Write('-');
            }

            var powersOfTen =
                new Dictionary<int, BigInteger>();

            WriteDecimalBlocks(
                writer,
                unsignedResult,
                state.DigitCount,
                padToWidth: false,
                powersOfTen,
                ReportBlockWritten,
                cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        writer.WriteLine();
        writer.Flush();
        cancellationToken.ThrowIfCancellationRequested();

        progress?.Invoke(
            new ExportFileProgress(
                ExportFilePhase.Finalizing,
                totalBlocks,
                totalBlocks));
    }

    private static void WritePowerOfTenDecimalBlocks(
        TextWriter writer,
        int zeroCount,
        bool isNegative,
        Action reportBlockWritten,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (isNegative)
        {
            writer.Write('-');
        }

        writer.Write('1');
        reportBlockWritten();

        string zeroBlock =
            new(
                '0',
                ExportLeafDigitCount);

        int remainingZeros =
            zeroCount;

        while (remainingZeros > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int blockLength =
                Math.Min(
                    ExportLeafDigitCount,
                    remainingZeros);

            writer.Write(
                zeroBlock.AsSpan(
                    0,
                    blockLength));

            remainingZeros -=
                blockLength;

            reportBlockWritten();
        }
    }

    private static void WriteDecimalBlocks(
        TextWriter writer,
        BigInteger value,
        int digitWidth,
        bool padToWidth,
        IDictionary<int, BigInteger> powersOfTen,
        Action reportBlockWritten,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (digitWidth <=
            ExportLeafDigitCount)
        {
            string blockText =
                value.ToString(
                    CultureInfo.InvariantCulture);

            if (padToWidth &&
                blockText.Length <
                digitWidth)
            {
                writer.Write(
                    new string(
                        '0',
                        digitWidth -
                        blockText.Length));
            }

            writer.Write(
                blockText);

            cancellationToken.ThrowIfCancellationRequested();
            reportBlockWritten();
            return;
        }

        int lowDigitWidth =
            digitWidth / 2;

        int highDigitWidth =
            digitWidth -
            lowDigitWidth;

        if (!powersOfTen.TryGetValue(
                lowDigitWidth,
                out BigInteger divisor))
        {
            divisor =
                BigInteger.Pow(
                    10,
                    lowDigitWidth);

            powersOfTen[lowDigitWidth] =
                divisor;
        }

        BigInteger highValue =
            BigInteger.DivRem(
                value,
                divisor,
                out BigInteger lowValue);

        WriteDecimalBlocks(
            writer,
            highValue,
            highDigitWidth,
            padToWidth,
            powersOfTen,
            reportBlockWritten,
            cancellationToken);

        WriteDecimalBlocks(
            writer,
            lowValue,
            lowDigitWidth,
            padToWidth: true,
            powersOfTen,
            reportBlockWritten,
            cancellationToken);
    }

    private static int CountDecimalLeafBlocks(
        int digitCount)
    {
        if (digitCount <=
            ExportLeafDigitCount)
        {
            return 1;
        }

        int lowDigitCount =
            digitCount / 2;

        return checked(
            CountDecimalLeafBlocks(
                digitCount -
                lowDigitCount) +
            CountDecimalLeafBlocks(
                lowDigitCount));
    }

    private static string CreateExportProgressMessage(
        ExportFileProgress progress)
    {
        string percentage =
            progress.TotalBlocks > 0
                ? (100d *
                   progress.CompletedBlocks /
                   progress.TotalBlocks)
                    .ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)
                : "0.00";

        return progress.Phase switch
        {
            ExportFilePhase.Preparing =>
                Format(
                    "PowerRoot.ExportStepPreparing",
                    percentage,
                    progress.CompletedBlocks,
                    progress.TotalBlocks),

            ExportFilePhase.Splitting =>
                Format(
                    "PowerRoot.ExportStepSplitting",
                    percentage,
                    progress.CompletedBlocks,
                    progress.TotalBlocks),

            ExportFilePhase.Writing =>
                Format(
                    "PowerRoot.ExportStepWriting",
                    percentage,
                    progress.CompletedBlocks,
                    progress.TotalBlocks),

            _ =>
                Format(
                    "PowerRoot.ExportStepFinalizing",
                    percentage)
        };
    }

    private void ShowExportStatus(
        string message,
        double progress,
        bool isBusy)
    {
        ExportStatusBorder.IsVisible = true;
        ExportStatusLabel.Text = message;
        ExportProgressBar.Progress =
            Math.Clamp(
                progress,
                0d,
                1d);

        ExportActivityIndicator.IsVisible =
            isBusy;

        ExportActivityIndicator.IsRunning =
            isBusy;
    }

    private void OnClearClicked(
        object? sender,
        EventArgs e)
    {
        if (_isCalculating)
        {
            return;
        }

        _calculationState = null;
        _pendingRestoredEntryTexts.Remove(
            BaseEntry);
        _pendingRestoredEntryTexts.Remove(
            ExponentEntry);

        BaseEntry.Text =
            string.Empty;

        ExponentEntry.Text =
            string.Empty;

        HideError();
        HideResult();

        ProgressBorder.IsVisible =
            false;
        CalculationActivityIndicator.IsRunning = false;
        CalculationActivityIndicator.IsVisible = false;

        BaseEntry.Focus();
    }

    private void RefreshLocalizedDynamicText()
    {
        if (_isCalculating &&
            TryGetCurrentInputValues(
                out long baseValue,
                out int exponent))
        {
            SetCalculationProgress(
                baseValue,
                exponent,
                _calculationProgressPhase,
                _calculationPhaseCompleted,
                _calculationPhaseTotal);
        }
        else if (_calculationState is not null &&
                 ProgressBorder.IsVisible)
        {
            SetCalculationProgress(
                _calculationState.BaseValue,
                _calculationState.Exponent,
                _calculationProgressPhase,
                _calculationPhaseCompleted,
                _calculationPhaseTotal);
        }

        if (_calculationState is not null)
        {
            ShowResult(
                _calculationState);
        }

        if (_rootCalculationState is not null)
        {
            ShowRootResult(
                _rootCalculationState);
        }

        if (_isExporting)
        {
            ExportTextButton.Text =
                _exportCancellation?.IsCancellationRequested == true
                    ? Translate(
                        "PowerRoot.ExportStopping")
                    : Translate(
                        "PowerRoot.ExportStop");
        }
    }

    private bool TryGetCurrentInputValues(
        out long baseValue,
        out int exponent)
    {
        bool isBaseValid =
            long.TryParse(
                RemoveGroupSeparators(
                    BaseEntry.Text),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out baseValue);

        bool isExponentValid =
            int.TryParse(
                RemoveGroupSeparators(
                    ExponentEntry.Text),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out exponent);

        return isBaseValid &&
               isExponentValid;
    }

    private void SetCalculationInteractionLocked(
        bool isLocked)
    {
        SetInputEnabled(
            enabled: !isLocked);

        // These actions normally live inside the result/diagnostics surface and
        // are hidden when a new calculation starts, but keep their enabled state
        // synchronized too so Stop remains the only state-changing action even
        // if a layout update makes one of them visible during a long operation.
        CopyResultButton.IsEnabled =
            !isLocked;
        ExportTextButton.IsEnabled =
            !isLocked;
        DiagnosticsToggleButton.IsEnabled =
            !isLocked;

        CalculationInteractionLockChanged?.Invoke(
            isLocked);
    }

    private void SetInputEnabled(
        bool enabled)
    {
        BaseEntry.IsEnabled = enabled;
        ExponentEntry.IsEnabled = enabled;
        CalculateButton.IsEnabled = enabled;
        ClearButton.IsEnabled = enabled;
        RootRadicandEntry.IsEnabled = enabled;
        RootDegreeEntry.IsEnabled = enabled;
        RootCalculateButton.IsEnabled = enabled;
        RootClearButton.IsEnabled = enabled;
        RootCopyResultButton.IsEnabled = enabled;
        PowerModeButton.IsEnabled = enabled;
        RootModeButton.IsEnabled = enabled;
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
        _calculationState =
            null;

        ResultBorder.IsVisible =
            false;

        _isDiagnosticsVisible =
            false;

        DiagnosticsToggleButton.IsVisible =
            false;

        LargeResultInfoBorder.IsVisible =
            false;

        DiagnosticsToggleButton.Text =
            Translate(
                "PowerRoot.ShowDetails");

        if (!_isExporting)
        {
            ExportStatusBorder.IsVisible =
                false;

            ExportActivityIndicator.IsRunning =
                false;
        }
    }

    private static string RemoveGroupSeparators(
        string? text)
    {
        return (text ??
                string.Empty)
            .Replace(
                ",",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                '−',
                '-')
            .Trim();
    }

    private static string FormatProgressExpression(
        long baseValue,
        int exponent)
    {
        string formattedBase =
            baseValue < 0
                ? $"({baseValue.ToString(CultureInfo.InvariantCulture)})"
                : baseValue.ToString(
                    CultureInfo.InvariantCulture);

        return
            $"{formattedBase}{ToSuperscript(exponent)}";
    }

    private static string FormatPlainExpression(
        long baseValue,
        int exponent)
    {
        string formattedBase =
            baseValue < 0
                ? $"({baseValue.ToString(CultureInfo.InvariantCulture)})"
                : baseValue.ToString(
                    CultureInfo.InvariantCulture);

        return
            $"{formattedBase}^{exponent.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatDisplayExpression(
        long baseValue,
        int exponent)
    {
        BigInteger integerBase =
            new(baseValue);

        string formattedBase =
            FormatCompactIntegerForDisplay(
                integerBase);

        int baseDigitCount =
            BigInteger.Abs(
                    integerBase)
                .ToString(
                    CultureInfo.InvariantCulture)
                .Length;

        if (baseValue < 0 ||
            baseDigitCount >
                FullResultDigitThreshold)
        {
            formattedBase =
                $"({formattedBase})";
        }

        return
            $"{formattedBase}{ToSuperscript(exponent)}";
    }

    private static string FormatCompactIntegerForDisplay(
        BigInteger value)
    {
        string digits =
            BigInteger.Abs(
                    value)
                .ToString(
                    CultureInfo.InvariantCulture);

        return digits.Length >
               FullResultDigitThreshold
            ? FormatScientificInteger(
                value)
            : value
                .ToString(
                    "N0",
                    CultureInfo.InvariantCulture)
                .Replace(
                    '-',
                    '−');
    }

    private static string ToSuperscript(
        long value)
    {
        const string NormalDigits =
            "0123456789";

        const string SuperscriptDigits =
            "⁰¹²³⁴⁵⁶⁷⁸⁹";

        string text =
            value.ToString(
                CultureInfo.InvariantCulture);

        var builder =
            new StringBuilder(
                text.Length);

        foreach (char character in text)
        {
            int index =
                NormalDigits.IndexOf(
                    character);

            builder.Append(
                index >= 0
                    ? SuperscriptDigits[index]
                    : character);
        }

        return builder.ToString();
    }

    private static string ToSignedSuperscript(
        int value)
    {
        return value < 0
            ? $"⁻{ToSuperscript(-value)}"
            : ToSuperscript(
                value);
    }

    private static long EstimateTxtFileSizeBytes(
        PowerCalculationState state)
    {
        string engineText =
            state.Strategy ==
            PowerComputationStrategy.DecimalPowerOfTen
                ? "Engine: direct decimal power-of-ten generation"
                : state.Strategy ==
                  PowerComputationStrategy.ParallelNttPower
                    ? state.ParallelDiagnostics?.UsedMemoryBoundedLargePower == true
                        ? "Engine: memory-bounded segmented/in-place exact NTT/CRT power"
                        : "Engine: parallel exact NTT/CRT power"
                    : state.Strategy ==
                      PowerComputationStrategy.BitShift
                        ? state.VirtualBitShiftExponent >
                          PowerRootEngine.MaximumBigIntegerPowerOfTwoExponent
                            ? "Engine: virtual exact 2^k bit shift + on-demand memory-bounded NTT/CRT TXT export"
                            : "Engine: BigInteger bit shift"
                        : "Engine: BigInteger";

        string header =
            $"Expression: {FormatPlainExpression(state.BaseValue, state.Exponent)}" +
            Environment.NewLine +
            engineText +
            Environment.NewLine +
            $"Digits: {state.DigitCount.ToString(CultureInfo.InvariantCulture)}" +
            Environment.NewLine +
            Environment.NewLine +
            "Result:" +
            Environment.NewLine;

        long signBytes =
            state.IsNegative
                ? 1L
                : 0L;

        return checked(
            3L + // UTF-8 BOM emitted by WriteFullResultFile.
            Encoding.UTF8.GetByteCount(
                header) +
            state.DigitCount +
            signBytes +
            Encoding.UTF8.GetByteCount(
                Environment.NewLine));
    }

    private static bool ShouldReleaseLargeTemporaryMemory(
        PowerCalculationState state)
    {
        return state.ParallelMagnitude is not null &&
               state.EstimatedPeakRamBytes >=
               LargeCalculationMemoryCleanupThresholdBytes;
    }

    private static async Task<long> ReleaseLargeTemporaryMemoryAsync()
    {
        await Task.Run(
            () =>
            {
                // NttBufferPool and FixedWorkerTeam are already disposed before
                // ParallelBigUnsigned.Pow returns. A blocking Gen2 sweep is
                // therefore sufficient to reclaim dead transform/twiddle LOH
                // arrays. Do not compact the LOH here: moving the live final
                // 100-200 MiB base-10,000 result would add unnecessary copying.
                GC.Collect(
                    GC.MaxGeneration,
                    GCCollectionMode.Forced,
                    blocking: true,
                    compacting: false);
            });

        return GetCurrentProcessPrivateMemoryBytes();
    }

    private static long GetCurrentProcessPrivateMemoryBytes()
    {
        try
        {
            using Process process =
                Process.GetCurrentProcess();

            process.Refresh();

            return Math.Max(
                0L,
                process.PrivateMemorySize64);
        }
        catch
        {
            // Process memory telemetry is diagnostic only; never fail a
            // completed calculation if an OS/runtime cannot expose it.
            return 0L;
        }
    }

    private static string FormatByteSize(
        long byteCount)
    {
        string[] units =
        [
            "B",
            "KB",
            "MB",
            "GB"
        ];

        double value =
            Math.Max(
                0,
                byteCount);

        int unitIndex = 0;

        while (value >= 1024d &&
               unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return
            $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {units[unitIndex]}";
    }

    private static string Translate(
        string key)
    {
        return LocalizationService.TranslateKey(
            key);
    }

    private static string Format(
        string key,
        params object?[] values)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            Translate(
                key),
            values);
    }

    private sealed record PowerCalculationState(
        long BaseValue,
        int Exponent,
        BigInteger Result,
        int DigitCount,
        string CompactResult,
        int ActiveWorkerCount,
        PowerComputationStrategy Strategy,
        int DecimalZeroCount,
        bool IsNegative,
        long EstimatedPeakRamBytes,
        TimeSpan Elapsed,
        ParallelBigUnsigned? ParallelMagnitude,
        ParallelPowerDiagnostics? ParallelDiagnostics,
        long VirtualBitShiftExponent = 0L,
        long ProcessPrivateMemoryBytes = 0L,
        long ProcessPrivateMemoryBeforeCleanupBytes = 0L);

    private sealed record RootCalculationState(
        Int128 Radicand,
        sbyte Degree,
        bool IsComplex,
        DoubleDouble RealResult,
        DoubleDouble ImaginaryResult,
        string ResultText,
        RootCalculationMethod Method);

    private enum RootCalculationMethod
    {
        Sqrt,
        Cbrt,
        Pow
    }

    private enum PowerComputationStrategy
    {
        SingleThreadedBigIntegerPower,
        ParallelNttPower,
        BitShift,
        DecimalPowerOfTen
    }

    private enum CalculationProgressPhase
    {
        Preparing,
        BitShift,
        DecimalShift,
        Computing,
        Formatting,
        Completed
    }

    private enum ExportFilePhase
    {
        Preparing,
        Splitting,
        Writing,
        Finalizing
    }

    private sealed record ExportFileProgress(
        ExportFilePhase Phase,
        int CompletedBlocks,
        int TotalBlocks);

    private sealed record ExportSaveResult(
        bool IsSuccessful,
        bool IsCanceled,
        string? FilePath,
        Exception? Exception)
    {
        public static ExportSaveResult Canceled { get; } =
            new(
                IsSuccessful: false,
                IsCanceled: true,
                FilePath: null,
                Exception: null);

        public static ExportSaveResult Success(
            string filePath)
        {
            return new ExportSaveResult(
                IsSuccessful: true,
                IsCanceled: false,
                FilePath: filePath,
                Exception: null);
        }
    }
}
