using CommunityToolkit.Maui.Storage;
using MathSolver.Services;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace MathSolver.Views;

public partial class PowerRootView : ContentView
{
    private const int MaxBaseMagnitude =
        1_000_000_000;

    private const int MaxExponent =
        1_000_000;

    private const int FullResultDigitThreshold =
        18;

    private const int ExportDigitThreshold =
        100_001;

    private const int ParallelDigitThreshold =
        100_000;

    private const int ProgressDigitThreshold =
        100_000;

    private const int ExportLeafDigitCount =
        4_096;

    private const int ExactPreviewConversionLimit =
        100_100;

    private const int PreviewLeadingDigits =
        12;

    private readonly int _recommendedWorkerCount;

    private CancellationTokenSource? _calculationCancellation;
    private CancellationTokenSource? _exportCancellation;
    private PowerCalculationState? _calculationState;
    private bool _isCultureSubscribed;
    private bool _isCalculating;
    private bool _isExporting;
    private bool _isPowerMode = true;
    private int _calculationVersion;
    private int _completedSteps;

    public PowerRootView()
    {
        InitializeComponent();

        LocalizationService.Attach(
            this);

        int physicalCoreCount =
            PhysicalCoreDetector.GetPhysicalCoreCount();

        _recommendedWorkerCount =
            Math.Max(
                1,
                physicalCoreCount / 2);

        Loaded +=
            OnLoaded;

        Unloaded +=
            OnUnloaded;

        SelectMode(
            powerMode: true);

        RefreshLocalizedDynamicText();
    }

    private void OnLoaded(
        object? sender,
        EventArgs e)
    {
        if (!_isCultureSubscribed)
        {
            LocalizationService.CultureChanged +=
                OnCultureChanged;

            _isCultureSubscribed =
                true;
        }

        RefreshLocalizedDynamicText();
    }

    private void OnUnloaded(
        object? sender,
        EventArgs e)
    {
        if (_isCultureSubscribed)
        {
            LocalizationService.CultureChanged -=
                OnCultureChanged;

            _isCultureSubscribed =
                false;
        }

        _calculationCancellation?.Cancel();
        _exportCancellation?.Cancel();
    }

    private void OnCultureChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            RefreshLocalizedDynamicText);
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

        ResetModeButton(
            PowerModeButton);

        ResetModeButton(
            RootModeButton);

        Button selectedButton =
            powerMode
                ? PowerModeButton
                : RootModeButton;

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");

        PowerContent.IsVisible =
            powerMode;

        RootComingSoonBorder.IsVisible =
            !powerMode;
    }

    private static void ResetModeButton(
        Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "SurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            "TextPrimaryColor");
    }

    private void OnInputTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (_isCalculating)
        {
            return;
        }

        HideError();
        HideResult();

        ProgressBorder.IsVisible =
            false;
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

        if (int.TryParse(
                normalizedText,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int value))
        {
            entry.Text =
                value.ToString(
                    "N0",
                    CultureInfo.InvariantCulture);
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
                out int baseValue,
                out int exponent))
        {
            return;
        }

        await CalculatePowerAsync(
            baseValue,
            exponent);
    }

    private bool TryReadInputs(
        out int baseValue,
        out int exponent)
    {
        baseValue = 0;
        exponent = 0;

        string baseText =
            RemoveGroupSeparators(
                BaseEntry.Text);

        if (!int.TryParse(
                baseText,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out baseValue) ||
            Math.Abs((long)baseValue) >
            MaxBaseMagnitude)
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
        int baseValue,
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

        bool showCalculationProgress =
            estimatedDigitCount >=
            ProgressDigitThreshold;

        int requestedWorkerCount =
            estimatedDigitCount >
            ParallelDigitThreshold
                ? _recommendedWorkerCount
                : 1;

        IReadOnlyList<int> exponentParts =
            SplitExponent(
                exponent,
                requestedWorkerCount);

        int activeWorkerCount =
            exponent == 0
                ? 1
                : exponentParts.Count;

        int totalSteps =
            GetTotalStepCount(
                exponentParts,
                exponent);

        _completedSteps = 0;
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

        UpdateRunningProgressText(
            baseValue,
            exponent,
            0,
            totalSteps);

        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            BigInteger result =
                await ComputePowerParallelAsync(
                    baseValue,
                    exponent,
                    exponentParts,
                    totalSteps,
                    calculationVersion,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            PowerCalculationState state =
                await Task.Run(
                    () => CreateCalculationState(
                        baseValue,
                        exponent,
                        result,
                        exponentParts,
                        activeWorkerCount,
                        stopwatch.Elapsed),
                    cancellationToken);

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

            CalculationProgressBar.Progress = 1d;
            ProgressPercentLabel.Text = "100%";
            ProgressTitleLabel.Text =
                Format(
                    "PowerRoot.ProgressCompleted",
                    FormatPlainExpression(
                        baseValue,
                        exponent));

            ProgressStepsLabel.Text =
                Format(
                    "PowerRoot.ProgressSteps",
                    totalSteps,
                    totalSteps);

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

    private async Task<BigInteger> ComputePowerParallelAsync(
        int baseValue,
        int exponent,
        IReadOnlyList<int> exponentParts,
        int totalSteps,
        int calculationVersion,
        CancellationToken cancellationToken)
    {
        if (exponent == 0)
        {
            ReportCompletedStep(
                baseValue,
                exponent,
                totalSteps,
                calculationVersion);

            return BigInteger.One;
        }

        Task<BigInteger>[] workerTasks =
            exponentParts
                .Select(
                    part =>
                        Task.Run(
                            () => ComputePowerChunk(
                                baseValue,
                                part,
                                exponent,
                                totalSteps,
                                calculationVersion,
                                cancellationToken),
                            cancellationToken))
                .ToArray();

        BigInteger[] partialResults =
            await Task.WhenAll(
                workerTasks);

        var currentLevel =
            partialResults.ToList();

        while (currentLevel.Count > 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nextLevel =
                new BigInteger[
                    (currentLevel.Count + 1) / 2];

            var combineTasks =
                new List<Task>();

            for (int index = 0;
                 index < currentLevel.Count;
                 index += 2)
            {
                int sourceIndex = index;
                int targetIndex = index / 2;

                if (sourceIndex + 1 >=
                    currentLevel.Count)
                {
                    nextLevel[targetIndex] =
                        currentLevel[sourceIndex];

                    continue;
                }

                combineTasks.Add(
                    Task.Run(
                        () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            nextLevel[targetIndex] =
                                currentLevel[sourceIndex] *
                                currentLevel[sourceIndex + 1];

                            cancellationToken.ThrowIfCancellationRequested();

                            ReportCompletedStep(
                                baseValue,
                                exponent,
                                totalSteps,
                                calculationVersion);
                        },
                        cancellationToken));
            }

            await Task.WhenAll(
                combineTasks);

            currentLevel =
                nextLevel.ToList();
        }

        return currentLevel[0];
    }

    private BigInteger ComputePowerChunk(
        int baseValue,
        int chunkExponent,
        int fullExponent,
        int totalSteps,
        int calculationVersion,
        CancellationToken cancellationToken)
    {
        BigInteger result =
            BigInteger.One;

        BigInteger factor =
            new(
                baseValue);

        int remainingExponent =
            chunkExponent;

        while (remainingExponent > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((remainingExponent & 1) != 0)
            {
                result *=
                    factor;
            }

            remainingExponent >>= 1;

            if (remainingExponent > 0)
            {
                factor *=
                    factor;
            }

            cancellationToken.ThrowIfCancellationRequested();

            ReportCompletedStep(
                baseValue,
                fullExponent,
                totalSteps,
                calculationVersion);
        }

        return result;
    }

    private void ReportCompletedStep(
        int baseValue,
        int exponent,
        int totalSteps,
        int calculationVersion)
    {
        int completedSteps =
            Math.Min(
                totalSteps,
                Interlocked.Increment(
                    ref _completedSteps));

        Dispatcher.Dispatch(
            () =>
            {
                if (calculationVersion !=
                    _calculationVersion)
                {
                    return;
                }

                double progress =
                    totalSteps > 0
                        ? (double)completedSteps /
                          totalSteps
                        : 1d;

                CalculationProgressBar.Progress =
                    progress;

                ProgressPercentLabel.Text =
                    $"{progress:P0}";

                UpdateRunningProgressText(
                    baseValue,
                    exponent,
                    completedSteps,
                    totalSteps);
            });
    }

    private void UpdateRunningProgressText(
        int baseValue,
        int exponent,
        int completedSteps,
        int totalSteps)
    {
        ProgressTitleLabel.Text =
            Format(
                "PowerRoot.ProgressTitle",
                FormatPlainExpression(
                    baseValue,
                    exponent));

        ProgressStepsLabel.Text =
            Format(
                "PowerRoot.ProgressSteps",
                completedSteps,
                totalSteps);
    }

    private static IReadOnlyList<int> SplitExponent(
        int exponent,
        int requestedWorkerCount)
    {
        if (exponent == 0)
        {
            return [0];
        }

        int workerCount =
            Math.Clamp(
                requestedWorkerCount,
                1,
                exponent);

        int quotient =
            exponent / workerCount;

        int remainder =
            exponent % workerCount;

        var parts =
            new int[workerCount];

        for (int index = 0;
             index < workerCount;
             index++)
        {
            parts[index] =
                quotient +
                (index < remainder
                    ? 1
                    : 0);
        }

        return parts;
    }

    private static int GetTotalStepCount(
        IReadOnlyList<int> exponentParts,
        int exponent)
    {
        if (exponent == 0)
        {
            return 1;
        }

        int workerSteps =
            exponentParts.Sum(
                GetBinaryDigitCount);

        int combineSteps =
            Math.Max(
                0,
                exponentParts.Count - 1);

        return Math.Max(
            1,
            workerSteps +
            combineSteps);
    }

    private static int GetBinaryDigitCount(
        int value)
    {
        int count = 0;

        while (value > 0)
        {
            count++;
            value >>= 1;
        }

        return count;
    }

    private static PowerCalculationState CreateCalculationState(
        int baseValue,
        int exponent,
        BigInteger result,
        IReadOnlyList<int> exponentParts,
        int activeWorkerCount,
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
                result,
                baseValue,
                exponent,
                digitCount,
                exactResultText);

        long estimatedPeakRamBytes =
            EstimatePeakRamBytes(
                result,
                digitCount,
                activeWorkerCount,
                exactResultText is not null);

        return new PowerCalculationState(
            baseValue,
            exponent,
            result,
            digitCount,
            compactResult,
            exponentParts.ToArray(),
            activeWorkerCount,
            estimatedPeakRamBytes,
            elapsed);
    }

    private static int EstimateDecimalDigitCount(
        int baseValue,
        int exponent)
    {
        if (exponent == 0 ||
            baseValue is 0 or 1 or -1)
        {
            return 1;
        }

        long absoluteBase =
            Math.Abs(
                (long)baseValue);

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

        double logarithm =
            exponent *
            Math.Log10(
                absoluteBase);

        return checked(
            (int)Math.Floor(
                logarithm) +
            1);
    }

    private static string CreateCompactResult(
        BigInteger result,
        int baseValue,
        int exponent,
        int digitCount,
        string? exactResultText)
    {
        if (digitCount <=
            FullResultDigitThreshold)
        {
            return result.ToString(
                "N0",
                CultureInfo.InvariantCulture);
        }

        bool isNegative =
            result.Sign < 0;

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
        int baseValue,
        int exponent)
    {
        double logarithm =
            exponent *
            Math.Log10(
                Math.Abs(
                    (long)baseValue));

        double fractionalPart =
            logarithm -
            Math.Floor(
                logarithm);

        long leadingValue =
            (long)Math.Floor(
                Math.Pow(
                    10d,
                    fractionalPart +
                    PreviewLeadingDigits -
                    1));

        long upperBound =
            (long)Math.Pow(
                10d,
                PreviewLeadingDigits);

        if (leadingValue >=
            upperBound)
        {
            leadingValue /= 10;
        }

        return leadingValue.ToString(
            $"D{PreviewLeadingDigits}",
            CultureInfo.InvariantCulture);
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

        string exponentSplit =
            string.Join(
                " + ",
                state.ExponentParts.Select(
                    part =>
                        part.ToString(
                            "N0",
                            CultureInfo.InvariantCulture)));

        string compactFactors =
            string.Join(
                " × ",
                state.ExponentParts.Select(
                    part =>
                        FormatDisplayExpression(
                            state.BaseValue,
                            part)));

        const int MaximumFactorLineLength =
            260;

        if (compactFactors.Length >
            MaximumFactorLineLength)
        {
            compactFactors =
                compactFactors[..MaximumFactorLineLength] +
                " …";
        }

        return string.Join(
            Environment.NewLine +
            Environment.NewLine,
            Format(
                "PowerRoot.StepGiven",
                expression),
            Format(
                "PowerRoot.StepSplit",
                state.Exponent.ToString(
                    "N0",
                    CultureInfo.InvariantCulture),
                exponentSplit),
            Format(
                "PowerRoot.StepCombine",
                compactFactors),
            Format(
                "PowerRoot.StepResult",
                state.CompactResult));
    }

    private string CreateLargeResultInformation(
        PowerCalculationState state)
    {
        return string.Join(
            Environment.NewLine,
            "• Engine: BigInteger",
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
                    CultureInfo.InvariantCulture)));
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

            var creationProgress =
                new Progress<ExportFileProgress>(
                    update =>
                    {
                        double normalizedProgress =
                            Math.Clamp(
                                update.TotalWorkUnits > 0
                                    ? (double)update.CompletedWorkUnits /
                                      update.TotalWorkUnits
                                    : 0d,
                                0d,
                                1d);

                        // Tao file tam la cong doan nang nhat, chiem 90%.
                        double displayedProgress =
                            normalizedProgress *
                            0.9d;

                        ShowExportStatus(
                            CreateExportProgressMessage(
                                update),
                            displayedProgress,
                            isBusy: true);
                    });

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
                progress: 0.9d,
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
                            0.9d +
                            normalizedProgress *
                            0.1d,
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
        IProgress<ExportFileProgress>? progress,
        CancellationToken cancellationToken)
    {
        int resultWorkUnits =
            CountDecimalWorkUnits(
                state.DigitCount);

        int totalWorkUnits =
            checked(
                resultWorkUnits +
                2);

        int totalBlocks =
            CountDecimalLeafBlocks(
                state.DigitCount);

        int completedWorkUnits = 0;
        int completedBlocks = 0;

        void Report(
            ExportFilePhase phase)
        {
            completedWorkUnits++;

            progress?.Report(
                new ExportFileProgress(
                    completedWorkUnits,
                    totalWorkUnits,
                    phase,
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
            "Engine: BigInteger");

        writer.WriteLine(
            $"Digits: {state.DigitCount.ToString(CultureInfo.InvariantCulture)}");

        writer.WriteLine();
        writer.WriteLine("Result:");

        cancellationToken.ThrowIfCancellationRequested();

        Report(
            ExportFilePhase.Preparing);

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
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                Report(
                    ExportFilePhase.Splitting);
            },
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                completedBlocks++;

                Report(
                    ExportFilePhase.Writing);
            },
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        writer.WriteLine();
        writer.Flush();
        cancellationToken.ThrowIfCancellationRequested();

        Report(
            ExportFilePhase.Finalizing);
    }

    private static void WriteDecimalBlocks(
        TextWriter writer,
        BigInteger value,
        int digitWidth,
        bool padToWidth,
        IDictionary<int, BigInteger> powersOfTen,
        Action reportSplit,
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

        cancellationToken.ThrowIfCancellationRequested();
        reportSplit();

        WriteDecimalBlocks(
            writer,
            highValue,
            highDigitWidth,
            padToWidth,
            powersOfTen,
            reportSplit,
            reportBlockWritten,
            cancellationToken);

        WriteDecimalBlocks(
            writer,
            lowValue,
            lowDigitWidth,
            padToWidth: true,
            powersOfTen,
            reportSplit,
            reportBlockWritten,
            cancellationToken);
    }

    private static int CountDecimalWorkUnits(
        int digitCount)
    {
        if (digitCount <=
            ExportLeafDigitCount)
        {
            return 1;
        }

        int lowDigitCount =
            digitCount / 2;

        int highDigitCount =
            digitCount -
            lowDigitCount;

        return checked(
            1 +
            CountDecimalWorkUnits(
                highDigitCount) +
            CountDecimalWorkUnits(
                lowDigitCount));
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
        int percentage =
            progress.TotalWorkUnits > 0
                ? (int)Math.Round(
                    100d *
                    progress.CompletedWorkUnits /
                    progress.TotalWorkUnits)
                : 0;

        int nextBlock =
            Math.Min(
                progress.TotalBlocks,
                progress.CompletedBlocks + 1);

        return progress.Phase switch
        {
            ExportFilePhase.Preparing =>
                Format(
                    "PowerRoot.ExportStepPreparing",
                    percentage,
                    nextBlock,
                    progress.TotalBlocks),

            ExportFilePhase.Splitting =>
                Format(
                    "PowerRoot.ExportStepSplitting",
                    percentage,
                    nextBlock,
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
                out int baseValue,
                out int exponent))
        {
            int estimatedDigitCount =
                EstimateDecimalDigitCount(
                    baseValue,
                    exponent);

            int requestedWorkerCount =
                estimatedDigitCount >
                ParallelDigitThreshold
                    ? _recommendedWorkerCount
                    : 1;

            IReadOnlyList<int> exponentParts =
                SplitExponent(
                    exponent,
                    requestedWorkerCount);

            int totalSteps =
                GetTotalStepCount(
                    exponentParts,
                    exponent);

            UpdateRunningProgressText(
                baseValue,
                exponent,
                Math.Min(
                    _completedSteps,
                    totalSteps),
                totalSteps);
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
        out int baseValue,
        out int exponent)
    {
        bool isBaseValid =
            int.TryParse(
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
            .Trim();
    }

    private static string FormatPlainExpression(
        int baseValue,
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
        int baseValue,
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
        int BaseValue,
        int Exponent,
        BigInteger Result,
        int DigitCount,
        string CompactResult,
        IReadOnlyList<int> ExponentParts,
        int ActiveWorkerCount,
        long EstimatedPeakRamBytes,
        TimeSpan Elapsed);

    private enum ExportFilePhase
    {
        Preparing,
        Splitting,
        Writing,
        Finalizing
    }

    private sealed record ExportFileProgress(
        int CompletedWorkUnits,
        int TotalWorkUnits,
        ExportFilePhase Phase,
        int CompletedBlocks,
        int TotalBlocks);
}
