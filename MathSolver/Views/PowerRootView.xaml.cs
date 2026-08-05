using CommunityToolkit.Maui.Storage;
using MathSolver.Numerics;
using MathSolver.Services;
using MathSolver.Views.Base;
using Microsoft.Maui.ApplicationModel;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace MathSolver.Views;

public partial class PowerRootView : LocalizedSolverView
{
    private const long MaxBaseMagnitude =
        1_000_000_000_000_000_000L;

    private const int MaxBaseInputDigits =
        19;

    private const int MaxExponent =
        1_000_000;

    private const int MaxExponentInputDigits =
        7;

    private const int FullResultDigitThreshold =
        18;

    private const int ExportDigitThreshold =
        100_001;

    private const int ProgressDigitThreshold =
        100_000;

    private const int ExportLeafDigitCount =
        4_096;

    private const int ExactPreviewConversionLimit =
        100_100;

    private const int PreviewLeadingDigits =
        12;

    // Decimal constants keep enough fractional precision to distinguish
    // (10^18 - 1)^1,000,000 from 10^18,000,000. A double loses that distinction
    // because its ULP is already much larger at an 18-million logarithm.
    private const decimal NaturalLogarithmOfTwo =
        0.6931471805599453094172321215m;

    private const decimal NaturalLogarithmOfTen =
        2.3025850929940456840179914557m;

    private CancellationTokenSource? _calculationCancellation;
    private CancellationTokenSource? _exportCancellation;
    private PowerCalculationState? _calculationState;
    private bool _isCalculating;
    private bool _isExporting;
    private bool _isPowerMode = true;
    private bool _isUpdatingInputText;
    private int _calculationVersion;
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

    protected override void OnSolverUnloaded()
    {
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

        Button selectedButton =
            powerMode
                ? PowerModeButton
                : RootModeButton;

        SelectionButtonStyler.Select(
            selectedButton,
            PowerModeButton,
            RootModeButton);

        PowerContent.IsVisible =
            powerMode;

        RootComingSoonBorder.IsVisible =
            !powerMode;
    }

    private void OnInputTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (_isCalculating ||
            _isUpdatingInputText)
        {
            return;
        }

        if (sender is Entry entry &&
            IsInputOutsideAllowedRange(
                entry,
                e.NewTextValue))
        {
            RestoreRejectedInput(
                entry,
                e.OldTextValue);

            ShowError(
                Translate(
                    ReferenceEquals(
                        entry,
                        BaseEntry)
                        ? "PowerRoot.BaseRangeError"
                        : "PowerRoot.ExponentRangeError"));

            return;
        }

        if (sender is Entry validEntry)
        {
            string newText =
                e.NewTextValue ??
                string.Empty;

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
                        validEntry.CursorPosition);

                SetInputText(
                    validEntry,
                    formattedText,
                    IntegerInputFormatter.FindCursorPosition(
                        formattedText,
                        logicalPosition));
            }
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

        await CalculatePowerAsync(
            baseValue,
            exponent);
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
            SelectComputationStrategy(
                baseValue,
                exponent,
                out int decimalExponent);

        int activeWorkerCount = 1;

        if (strategy ==
                PowerComputationStrategy.BigIntegerPow &&
            estimatedDigitCount >
                ProgressDigitThreshold &&
            CalculationThreadingManager.UseMultithreading)
        {
            activeWorkerCount =
                CalculationThreadingManager.RecommendedWorkerCount;

            strategy =
                PowerComputationStrategy.ParallelNttPower;
        }

        bool showCalculationProgress =
            estimatedDigitCount >=
            ProgressDigitThreshold;

        _calculationProgressPhase =
            CalculationProgressPhase.Preparing;
        _calculationPhaseCompleted = 0;
        _calculationPhaseTotal = 1;
        _isCalculating = true;

        SetInputEnabled(
            enabled: false);

        ResultBorder.IsVisible = false;
        ProgressBorder.IsVisible =
            showCalculationProgress;
        StopButton.IsVisible =
            showCalculationProgress;
        StopButton.IsEnabled =
            showCalculationProgress;

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
                    await ComputeParallelPowerAsync(
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

                state =
                    await Task.Run(
                        () => CreateParallelCalculationState(
                            baseValue,
                            exponent,
                            parallelResult.Magnitude,
                            isNegative,
                            activeWorkerCount,
                            stopwatch.Elapsed,
                            parallelResult.Diagnostics),
                        cancellationToken);
            }
            else
            {
                BigInteger result;

                if (strategy ==
                    PowerComputationStrategy.BitShift)
                {
                    SetCalculationProgress(
                        baseValue,
                        exponent,
                        CalculationProgressPhase.BitShift,
                        0,
                        1);

                    if (!TryGetPowerOfTwoExponent(
                            baseValue,
                            out int basePowerOfTwoExponent))
                    {
                        throw new InvalidOperationException(
                            "The bit-shift strategy requires |base| = 2^k.");
                    }

                    int totalBitShift =
                        checked(
                            basePowerOfTwoExponent *
                            exponent);

                    // If |a| = 2^k, then |a|^n = 2^(k*n). BigInteger.One is
                    // the required seed because shifting zero still gives zero.
                    cancellationToken.ThrowIfCancellationRequested();
                    result =
                        BigInteger.One << totalBitShift;

                    if (baseValue < 0 &&
                        (exponent & 1) != 0)
                    {
                        result =
                            BigInteger.Negate(
                                result);
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
                    SetCalculationProgress(
                        baseValue,
                        exponent,
                        CalculationProgressPhase.Computing,
                        0,
                        1);

                    result =
                        await ComputeBigIntegerPowAsync(
                            baseValue,
                            exponent,
                            cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    SetCalculationProgress(
                        baseValue,
                        exponent,
                        CalculationProgressPhase.Computing,
                        1,
                        1);
                }

                cancellationToken.ThrowIfCancellationRequested();

                SetCalculationProgress(
                    baseValue,
                    exponent,
                    CalculationProgressPhase.Formatting,
                    0,
                    1);

                state =
                    await Task.Run(
                        () => CreateBigIntegerCalculationState(
                            baseValue,
                            exponent,
                            result,
                            strategy,
                            stopwatch.Elapsed),
                        cancellationToken);
            }

            stopwatch.Stop();

            if (calculationVersion !=
                _calculationVersion)
            {
                return;
            }

            _calculationState =
                state with
                {
                    Elapsed =
                        stopwatch.Elapsed
                };

            SetCalculationProgress(
                baseValue,
                exponent,
                CalculationProgressPhase.Completed,
                1,
                1);

            StopButton.IsVisible = false;

            ShowResult(
                _calculationState);
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

                SetInputEnabled(
                    enabled: true);
            }
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
                    0.95d,
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
                FormatPlainExpression(
                    baseValue,
                    exponent));

        ProgressStepsLabel.Text =
            CreateCalculationPhaseText(
                phase,
                baseValue,
                exponent,
                completedSteps,
                totalSteps);
    }

    private static string CreateCalculationPhaseText(
        CalculationProgressPhase phase,
        long baseValue,
        int exponent,
        int completedSteps,
        int totalSteps)
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

        int totalBitShift = 0;

        if (phase == CalculationProgressPhase.BitShift &&
            TryGetPowerOfTwoExponent(
                baseValue,
                out int basePowerOfTwoExponent))
        {
            totalBitShift =
                checked(
                    basePowerOfTwoExponent *
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
                Format(
                    "PowerRoot.ProgressPhaseComputing",
                    phaseNumber,
                    phaseCount,
                    completedSteps,
                    totalSteps),
            CalculationProgressPhase.Formatting =>
                Format(
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

    private static PowerComputationStrategy SelectComputationStrategy(
        long baseValue,
        int exponent,
        out int decimalExponent)
    {
        decimalExponent = 0;

        if (exponent > 0 &&
            TryGetPowerOfTenExponent(
                baseValue,
                out decimalExponent))
        {
            return PowerComputationStrategy.DecimalPowerOfTen;
        }

        if (exponent > 0 &&
            TryGetPowerOfTwoExponent(
                baseValue,
                out _))
        {
            return PowerComputationStrategy.BitShift;
        }

        return PowerComputationStrategy.BigIntegerPow;
    }

    private static Task<BigInteger> ComputeBigIntegerPowAsync(
        long baseValue,
        int exponent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // BigInteger.Pow uses the runtime's optimized binary-exponentiation
        // implementation. LongRunning only gives that single-threaded work a
        // dedicated background thread; it does not split the exponent.
        return Task.Factory.StartNew(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                BigInteger result =
                    BigInteger.Pow(
                        new BigInteger(
                            baseValue),
                        exponent);

                cancellationToken.ThrowIfCancellationRequested();

                return result;
            },
            cancellationToken,
            TaskCreationOptions.LongRunning |
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    private static Task<ParallelPowerResult> ComputeParallelPowerAsync(
        long baseValue,
        int exponent,
        int workerCount,
        Action<int, int> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ulong magnitude =
            (ulong)Math.Abs(
                baseValue);

        return Task.Factory.StartNew(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return ParallelBigUnsigned.Pow(
                    magnitude,
                    exponent,
                    workerCount,
                    progress,
                    cancellationToken);
            },
            cancellationToken,
            TaskCreationOptions.LongRunning |
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    private static bool TryGetPowerOfTenExponent(
        long baseValue,
        out int decimalExponent)
    {
        decimalExponent = 0;

        long magnitude =
            Math.Abs(
                baseValue);

        // 10^k uses k >= 1. Values +/-1 are handled by the power-of-two path.
        if (magnitude < 10)
        {
            return false;
        }

        while (magnitude % 10 == 0)
        {
            magnitude /= 10;
            decimalExponent++;
        }

        return magnitude == 1;
    }

    private static bool TryGetPowerOfTwoExponent(
        long baseValue,
        out int powerOfTwoExponent)
    {
        powerOfTwoExponent = 0;

        ulong magnitude =
            baseValue < 0
                ? unchecked((ulong)(-(baseValue + 1))) + 1UL
                : (ulong)baseValue;

        if (magnitude == 0 ||
            (magnitude & (magnitude - 1UL)) != 0)
        {
            return false;
        }

        powerOfTwoExponent =
            BitOperations.TrailingZeroCount(
                magnitude);

        return true;
    }

    private static PowerCalculationState CreateBigIntegerCalculationState(
        long baseValue,
        int exponent,
        BigInteger result,
        PowerComputationStrategy strategy,
        TimeSpan elapsed)
    {
        int estimatedDigitCount =
            EstimateDecimalDigitCount(
                baseValue,
                exponent);

        string? exactResultText =
            null;

        int digitCount =
            estimatedDigitCount;

        if (estimatedDigitCount <=
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

        return new PowerCalculationState(
            baseValue,
            exponent,
            result,
            digitCount,
            compactResult,
            ActiveWorkerCount: 1,
            Strategy: strategy,
            DecimalZeroCount: 0,
            IsNegative: result.Sign < 0,
            EstimatedPeakRamBytes: estimatedPeakRamBytes,
            Elapsed: elapsed,
            ParallelMagnitude: null,
            ParallelDiagnostics: null);
    }

    private static PowerCalculationState CreateParallelCalculationState(
        long baseValue,
        int exponent,
        ParallelBigUnsigned magnitude,
        bool isNegative,
        int activeWorkerCount,
        TimeSpan elapsed,
        ParallelPowerDiagnostics diagnostics)
    {
        int digitCount =
            magnitude.DigitCount;

        string? exactResultText =
            null;

        if (digitCount <=
            ExactPreviewConversionLimit)
        {
            exactResultText =
                magnitude.ToDecimalString();

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

        // Two transform buffers, two residue arrays, CRT coefficients and the
        // normalized result dominate the peak. The estimate deliberately uses
        // a conservative multiplier because the transform length is rounded
        // up to the next power of two.
        long estimatedPeakRamBytes =
            checked(
                magnitude.StorageBytes *
                14L +
                (exactResultText is not null
                    ? (long)exactResultText.Length *
                      sizeof(char)
                    : 0L));

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
        ResultExpressionLabel.Text =
            FormatDisplayExpression(
                state.BaseValue,
                state.Exponent);

        ResultPreviewEditor.Text =
            state.CompactResult;

        bool isCompact =
            state.DigitCount >
            FullResultDigitThreshold;

        ResultCompactHintLabel.IsVisible =
            isCompact;

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
            CopyResultButton,
            canExport
                ? 1
                : 2);

        LargeResultInfoBorder.IsVisible =
            canExport;

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
    }

    private string CreateSolution(
        PowerCalculationState state)
    {
        string expression =
            FormatDisplayExpression(
                state.BaseValue,
                state.Exponent);

        string formattedBase =
            state.BaseValue.ToString(
                "N0",
                CultureInfo.InvariantCulture);

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
            if (!TryGetPowerOfTwoExponent(
                    state.BaseValue,
                    out int basePowerOfTwoExponent))
            {
                throw new InvalidOperationException(
                    "The bit-shift strategy requires |base| = 2^k.");
            }

            int totalPowerOfTwoExponent =
                checked(
                    basePowerOfTwoExponent *
                    state.Exponent);

            steps.Add(
                Format(
                    "PowerRoot.StepPowerOfTwoBaseRule",
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
        else
        {
            steps.Add(
                Translate(
                    "PowerRoot.StepRepeatedSquaring"));

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
            _calculationCancellation is null)
        {
            return;
        }

        StopButton.IsEnabled = false;
        ProgressTitleLabel.Text =
            Translate(
                "PowerRoot.ProgressStopping");

        _calculationCancellation.Cancel();

        await Task.CompletedTask;
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
            if (_exportCancellation is not null &&
                !_exportCancellation.IsCancellationRequested)
            {
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

        _isExporting = true;
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

            // Khong dung Progress<T> o day. Progress<T> chi dua callback vao
            // hang doi UI, vi vay worker co the tao xong file truoc khi giao
            // dien kip hien thi tung block. Callback dong bo nay dam bao moi
            // block da ghi duoc UI nhan truoc khi worker xu ly block ke tiep.
            Action<ExportFileProgress> creationProgress =
                update =>
                {
                    double normalizedProgress =
                        Math.Clamp(
                            update.TotalBlocks > 0
                                ? (double)update.CompletedBlocks /
                                  update.TotalBlocks
                                : 0d,
                            0d,
                            1d);

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

            await Task.Run(
                () => WriteFullResultFile(
                    temporaryPath,
                    state,
                    creationProgress,
                    cancellationToken),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            ShowExportStatus(
                Format(
                    "PowerRoot.ExportSavingProgress",
                    0),
                progress: 0d,
                isBusy: true);

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
                                Math.Round(
                                    normalizedProgress *
                                    100d)),
                            normalizedProgress,
                            isBusy: true);
                    });

            FileSaverResult saveResult =
                await FileSaver.Default.SaveAsync(
                    fileName,
                    sourceStream,
                    saveProgress,
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
                        savedPath),
                    progress: 1d,
                    isBusy: false);
            }
            else if (saveResult.Exception is
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
        }
    }

    private static void WriteFullResultFile(
        string filePath,
        PowerCalculationState state,
        Action<ExportFileProgress>? progress,
        CancellationToken cancellationToken)
    {
        int totalBlocks =
            state.Strategy switch
            {
                PowerComputationStrategy.DecimalPowerOfTen =>
                    checked(
                        1 +
                        (state.DecimalZeroCount +
                         ExportLeafDigitCount - 1) /
                        ExportLeafDigitCount),
                PowerComputationStrategy.ParallelNttPower =>
                    checked(
                        (state.DigitCount +
                         ExportLeafDigitCount - 1) /
                        ExportLeafDigitCount),
                _ =>
                    CountDecimalLeafBlocks(
                        state.DigitCount)
            };

        int completedBlocks = 0;

        void ReportBlockWritten()
        {
            cancellationToken.ThrowIfCancellationRequested();
            completedBlocks++;

            progress?.Invoke(
                new ExportFileProgress(
                    ExportFilePhase.Writing,
                    completedBlocks,
                    totalBlocks));
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
                    ? "Engine: parallel exact NTT/CRT power"
                    : "Engine: BigInteger");

        writer.WriteLine(
            $"Digits: {state.DigitCount.ToString(CultureInfo.InvariantCulture)}");

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
        else if (state.Strategy ==
                 PowerComputationStrategy.ParallelNttPower &&
                 state.ParallelMagnitude is not null)
        {
            if (state.IsNegative)
            {
                writer.Write('-');
            }

            state.ParallelMagnitude.WriteDecimalBlocks(
                writer,
                ExportLeafDigitCount,
                ReportBlockWritten,
                cancellationToken);
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

        BaseEntry.Text =
            string.Empty;

        ExponentEntry.Text =
            string.Empty;

        HideError();
        HideResult();

        ProgressBorder.IsVisible =
            false;

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

    private void SetInputEnabled(
        bool enabled)
    {
        BaseEntry.IsEnabled = enabled;
        ExponentEntry.IsEnabled = enabled;
        CalculateButton.IsEnabled = enabled;
        ClearButton.IsEnabled = enabled;
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
        string formattedBase =
            baseValue < 0
                ? $"({baseValue.ToString("N0", CultureInfo.InvariantCulture)})"
                : baseValue.ToString(
                    "N0",
                    CultureInfo.InvariantCulture);

        return
            $"{formattedBase}{ToSuperscript(exponent)}";
    }

    private static string ToSuperscript(
        int value)
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
        ParallelPowerDiagnostics? ParallelDiagnostics);

    private enum PowerComputationStrategy
    {
        BigIntegerPow,
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
}
