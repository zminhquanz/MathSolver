using MathSolver.Services;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq;

namespace MathSolver.Views;

public partial class HardwarePerformancePage : ContentPage
{
    private readonly record struct TimedBenchmarkResult(
        double BestMops,
        double Checksum);

    private readonly record struct WorkerResult(
        long OperationCount,
        double Checksum);

    private sealed class SimdModeOption
    {
        public required CalculationSimdMode Mode { get; init; }

        public required string DisplayName { get; init; }

        public override string ToString() =>
            DisplayName;
    }

    private delegate WorkerResult TimedWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken);

    private sealed class BenchmarkResult
    {
        public required double Int32Mops { get; init; }

        public required double Int64Mops { get; init; }

        public required double FloatMops { get; init; }

        public required double DoubleMops { get; init; }

        public required double ElapsedMilliseconds { get; init; }

        public required double Score { get; init; }

        public required bool UsedSimd { get; init; }

        public required CalculationSimdMode UsedSimdMode { get; init; }

        public required bool UsedMultithreading { get; init; }

        public required int WorkerCount { get; init; }

        public required double Checksum { get; init; }
    }

    private BenchmarkResult? _lastBenchmarkResult;
    private bool _isBenchmarkRunning;
    private bool _isLoadingAccelerationState;
    private bool _hasPlayedEntryAnimation;
    private bool _isClosing;
    private CancellationTokenSource? _benchmarkCancellationTokenSource;

    private readonly List<SimdModeOption>
        _simdModeOptions =
            [];

    public HardwarePerformancePage()
    {
        InitializeComponent();

        Shell.SetNavBarIsVisible(
            this,
            false);

        Shell.SetTabBarIsVisible(
            this,
            false);

        LocalizationService.Attach(
            this);

        LoadHardwareInformation();
        RenderBenchmarkResult();
        PreparePageEntryAnimation();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        AppLanguageManager.LanguageChanged +=
            OnLanguageChanged;

        LocalizationService.Attach(
            this);

        LoadHardwareInformation();
        RenderBenchmarkResult();

        if (!_hasPlayedEntryAnimation)
        {
            _hasPlayedEntryAnimation =
                true;

            Dispatcher.Dispatch(
                async () =>
                    await PlayPageEntryAnimationAsync());
        }
    }

    protected override void OnDisappearing()
    {
        _benchmarkCancellationTokenSource?.Cancel();

        AppLanguageManager.LanguageChanged -=
            OnLanguageChanged;

        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ =
            CloseAsync();

        return true;
    }

    private void PreparePageEntryAnimation()
    {
        HardwarePageContentRoot.Opacity =
            0d;

        HardwarePageContentRoot.TranslationX =
            42d;

        HardwarePageContentRoot.Scale =
            0.995d;
    }

    private async Task PlayPageEntryAnimationAsync()
    {
        HardwarePageContentRoot.CancelAnimations();

        await Task.WhenAll(
            HardwarePageContentRoot.FadeToAsync(
                1d,
                190,
                Easing.CubicOut),

            HardwarePageContentRoot.TranslateToAsync(
                0d,
                0d,
                240,
                Easing.CubicOut),

            HardwarePageContentRoot.ScaleToAsync(
                1d,
                240,
                Easing.CubicOut));
    }

    private async Task PlayPageExitAnimationAsync()
    {
        HardwarePageContentRoot.CancelAnimations();

        await Task.WhenAll(
            HardwarePageContentRoot.FadeToAsync(
                0d,
                125,
                Easing.CubicIn),

            HardwarePageContentRoot.TranslateToAsync(
                34d,
                0d,
                155,
                Easing.CubicIn),

            HardwarePageContentRoot.ScaleToAsync(
                0.995d,
                155,
                Easing.CubicIn));
    }

    private void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        LocalizationService.RefreshAll();
        LoadHardwareInformation();
        RenderBenchmarkResult();
        SetBenchmarkButtonRunningState(
            _isBenchmarkRunning);
    }

    private void LoadHardwareInformation()
    {
        IDeviceInfo deviceInfo =
            DeviceInfo.Current;

        DeviceNameValueLabel.Text =
            NormalizeText(
                deviceInfo.Name);

        DeviceModelValueLabel.Text =
            NormalizeText(
                deviceInfo.Model);

        ManufacturerValueLabel.Text =
            NormalizeText(
                deviceInfo.Manufacturer);

        PlatformValueLabel.Text =
            deviceInfo.Platform.ToString();

        OsVersionValueLabel.Text =
            NormalizeText(
                deviceInfo.VersionString);

        DeviceIdiomValueLabel.Text =
            GetDeviceIdiomText(
                deviceInfo.Idiom);

        DeviceTypeValueLabel.Text =
            GetDeviceTypeText(
                deviceInfo.DeviceType);

        DisplayInfo displayInfo =
            DeviceDisplay.Current.MainDisplayInfo;

        DisplayValueLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                "{0:N0} × {1:N0} px • {2:N2}x",
                displayInfo.Width,
                displayInfo.Height,
                displayInfo.Density);

        CpuArchitectureValueLabel.Text =
            RuntimeInformation
                .ProcessArchitecture
                .ToString();

        OsArchitectureValueLabel.Text =
            RuntimeInformation
                .OSArchitecture
                .ToString();

        LogicalProcessorsValueLabel.Text =
            Environment
                .ProcessorCount
                .ToString(
                    CultureInfo.CurrentCulture);

        ProcessBitnessValueLabel.Text =
            LocalizationService.Translate(
                Environment.Is64BitProcess
                    ? "Có"
                    : "Không");

        string simdText =
            GetBestSimdName();

        SimdValueLabel.Text =
            simdText;

        bool hasSimd =
            CalculationAccelerationManager.IsSimdAvailable;

        bool hasMultipleThreads =
            CalculationThreadingManager.IsMultithreadingAvailable;

        _isLoadingAccelerationState =
            true;

        HardwareAccelerationSwitch.IsEnabled =
            hasSimd;

        HardwareAccelerationSwitch.IsToggled =
            CalculationAccelerationManager.UseSimd;

        LoadSimdModeOptions();

        MultithreadingSwitch.IsEnabled =
            hasMultipleThreads;

        MultithreadingSwitch.IsToggled =
            CalculationThreadingManager.UseMultithreading;

        _isLoadingAccelerationState =
            false;

        UpdateAccelerationStateText();

        VectorWidthValueLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                "{0} bit",
                CalculationAccelerationManager
                    .SimdVectorWidthBits);

        AvailableMemoryValueLabel.Text =
            GetAvailableMemoryText();

        RuntimeValueLabel.Text =
            RuntimeInformation.FrameworkDescription;
    }

    private void LoadSimdModeOptions()
    {
        _simdModeOptions.Clear();

        foreach (CalculationSimdMode mode
                 in CalculationAccelerationManager
                     .AvailableSelectableModes)
        {
            _simdModeOptions.Add(
                new SimdModeOption
                {
                    Mode =
                        mode,

                    DisplayName =
                        CalculationAccelerationManager
                            .GetModeDisplayName(
                                mode)
                });
        }

        SimdModePicker.ItemsSource =
            _simdModeOptions;

        CalculationSimdMode selectedMode =
            CalculationAccelerationManager
                .SelectedSimdMode;

        SimdModePicker.SelectedItem =
            _simdModeOptions.FirstOrDefault(
                option =>
                    option.Mode ==
                    selectedMode);

        UpdateSimdModeSelectorVisibility();
    }

    private void OnSimdModeSelectionChanged(
        object? sender,
        EventArgs e)
    {
        if (_isLoadingAccelerationState ||
            SimdModePicker.SelectedItem
                is not SimdModeOption option)
        {
            return;
        }

        CalculationAccelerationManager
            .SetSelectedSimdMode(
                option.Mode);

        VectorWidthValueLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                "{0} bit",
                CalculationAccelerationManager
                    .SimdVectorWidthBits);

        UpdateAccelerationStateText();
    }

    private void UpdateSimdModeSelectorVisibility()
    {
        SimdModeSelectorContainer.IsVisible =
            HardwareAccelerationSwitch.IsToggled &&
            _simdModeOptions.Count >
            0;

        SimdModePicker.IsEnabled =
            !_isBenchmarkRunning;
    }

    private void OnHardwareAccelerationToggled(
        object? sender,
        ToggledEventArgs e)
    {
        if (_isLoadingAccelerationState)
        {
            return;
        }

        CalculationAccelerationManager.SetUseSimd(
            e.Value);

        UpdateSimdModeSelectorVisibility();
        UpdateAccelerationStateText();
    }

    private void OnMultithreadingToggled(
        object? sender,
        ToggledEventArgs e)
    {
        if (_isLoadingAccelerationState)
        {
            return;
        }

        CalculationThreadingManager.SetUseMultithreading(
            e.Value);

        UpdateAccelerationStateText();
    }

    private void UpdateAccelerationStateText()
    {
        bool hasSimd =
            CalculationAccelerationManager.IsSimdAvailable;

        bool useSimd =
            CalculationAccelerationManager.UseSimd;

        bool hasMultipleThreads =
            CalculationThreadingManager.IsMultithreadingAvailable;

        bool useMultithreading =
            CalculationThreadingManager.UseMultithreading;

        int workerCount =
            useMultithreading
                ? CalculationThreadingManager.RecommendedWorkerCount
                : 1;

        CalculationSimdMode selectedMode =
            CalculationAccelerationManager
                .SelectedSimdMode;

        FloatingPointModeValueLabel.Text =
            BuildFloatingPointModeText(
                useSimd,
                selectedMode,
                useMultithreading,
                workerCount);

        IntegerModeValueLabel.Text =
            BuildIntegerModeText(
                useMultithreading,
                workerCount);

        AccelerationStatusLabel.Text =
            !hasSimd
                ? LocalizationService.Translate(
                    "Thiết bị không hỗ trợ SIMD. Float và Double sẽ dùng Scalar.")
                : useSimd
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizationService.Translate(
                            "Float và Double đang dùng {0}."),
                        CalculationAccelerationManager
                            .GetModeDisplayName(
                                selectedMode))
                    : LocalizationService.Translate(
                        "Float và Double đang dùng Scalar.");

        MultithreadingStatusLabel.Text =
            !hasMultipleThreads
                ? LocalizationService.Translate(
                    "Thiết bị chỉ có một luồng logic.")
                : useMultithreading
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizationService.Translate(
                            "Benchmark đang dùng {0} luồng CPU."),
                        workerCount)
                    : LocalizationService.Translate(
                        "Benchmark đang dùng một luồng CPU.");
    }

    private static string BuildFloatingPointModeText(
        bool useSimd,
        CalculationSimdMode simdMode,
        bool useMultithreading,
        int workerCount)
    {
        string processingMode =
            useSimd
                ? CalculationAccelerationManager
                    .GetModeDisplayName(
                        simdMode)
                : "Scalar";

        return useMultithreading
            ? string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "{0} + đa luồng ({1} luồng)"),
                processingMode,
                workerCount)
            : string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "{0} + đơn luồng"),
                processingMode);
    }

    private static string BuildIntegerModeText(
        bool useMultithreading,
        int workerCount)
    {
        return useMultithreading
            ? string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Scalar + đa luồng ({0} luồng)"),
                workerCount)
            : LocalizationService.Translate(
                "Scalar + đơn luồng");
    }

    private static string NormalizeText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                   value)
            ? LocalizationService.Translate(
                "Không xác định")
            : value.Trim();
    }

    private static string GetDeviceIdiomText(
        DeviceIdiom idiom)
    {
        string source =
            idiom == DeviceIdiom.Desktop
                ? "Máy tính để bàn"
                : idiom == DeviceIdiom.Phone
                    ? "Điện thoại"
                    : idiom == DeviceIdiom.Tablet
                        ? "Máy tính bảng"
                        : idiom == DeviceIdiom.TV
                            ? "TV"
                            : idiom == DeviceIdiom.Watch
                                ? "Đồng hồ"
                                : "Không xác định";

        return LocalizationService.Translate(
            source);
    }

    private static string GetDeviceTypeText(
        DeviceType deviceType)
    {
        return LocalizationService.Translate(
            deviceType == DeviceType.Physical
                ? "Thiết bị thật"
                : "Thiết bị ảo");
    }

    private static string GetBestSimdName()
    {
        if (CalculationAccelerationManager
                .IsAvx512Available)
        {
            return "AVX512";
        }

        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            return "AVX2";
        }

        if (System.Runtime.Intrinsics.X86.Avx.IsSupported)
        {
            return "AVX";
        }

        if (System.Runtime.Intrinsics.X86.Sse42.IsSupported)
        {
            return "SSE4.2";
        }

        if (System.Runtime.Intrinsics.X86.Sse41.IsSupported)
        {
            return "SSE4.1";
        }

        if (System.Runtime.Intrinsics.X86.Ssse3.IsSupported)
        {
            return "SSSE3";
        }

        if (System.Runtime.Intrinsics.X86.Sse3.IsSupported)
        {
            return "SSE3";
        }

        if (System.Runtime.Intrinsics.X86.Sse2.IsSupported)
        {
            return "SSE2";
        }

        if (System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported)
        {
            return "ARM NEON";
        }

        return LocalizationService.Translate(
            "Không được hỗ trợ");
    }

    private static string GetAvailableMemoryText()
    {
        long availableBytes =
            GC.GetGCMemoryInfo()
                .TotalAvailableMemoryBytes;

        if (availableBytes <= 0 ||
            availableBytes ==
            long.MaxValue)
        {
            return LocalizationService.Translate(
                "Không xác định");
        }

        double availableMegabytes =
            availableBytes /
            1024d /
            1024d;

        return string.Format(
            CultureInfo.CurrentCulture,
            "{0:N0} MB",
            availableMegabytes);
    }

    private async void OnRunBenchmarkClicked(
        object? sender,
        EventArgs e)
    {
        if (_isBenchmarkRunning)
        {
            BenchmarkStatusLabel.Text =
                LocalizationService.Translate(
                    "Đang dừng đo sức mạnh…");

            _benchmarkCancellationTokenSource?.Cancel();
            return;
        }

        _isBenchmarkRunning =
            true;

        SetBenchmarkButtonRunningState(
            true);

        HardwareAccelerationSwitch.IsEnabled =
            false;

        SimdModePicker.IsEnabled =
            false;

        MultithreadingSwitch.IsEnabled =
            false;

        BenchmarkProgress.IsVisible =
            true;

        BenchmarkProgress.IsRunning =
            true;

        BenchmarkCountdownLabel.IsVisible =
            true;

        BenchmarkResultsBorder.IsVisible =
            false;

        _benchmarkCancellationTokenSource?.Dispose();

        _benchmarkCancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            _benchmarkCancellationTokenSource.Token;

        try
        {
            bool useSimd =
                CalculationAccelerationManager.UseSimd;

            CalculationSimdMode simdMode =
                CalculationAccelerationManager
                    .SelectedSimdMode;

            bool useMultithreading =
                CalculationThreadingManager.UseMultithreading;

            _lastBenchmarkResult =
                await RunCalculationBenchmarkAsync(
                    useSimd,
                    simdMode,
                    useMultithreading,
                    cancellationToken);

            BenchmarkStatusLabel.Text =
                LocalizationService.Translate(
                    "Đo sức mạnh hoàn tất.");

            RenderBenchmarkResult();
        }
        catch (OperationCanceledException)
        {
            _lastBenchmarkResult =
                null;

            BenchmarkResultsBorder.IsVisible =
                false;

            BenchmarkStatusLabel.Text =
                LocalizationService.Translate(
                    "Đã hủy đo sức mạnh.");
        }
        catch
        {
            _lastBenchmarkResult =
                null;

            BenchmarkResultsBorder.IsVisible =
                false;

            BenchmarkStatusLabel.Text =
                LocalizationService.Translate(
                    "Không thể chạy đo sức mạnh trên thiết bị này.");
        }
        finally
        {
            _isBenchmarkRunning =
                false;

            BenchmarkProgress.IsRunning =
                false;

            BenchmarkProgress.IsVisible =
                false;

            BenchmarkCountdownLabel.IsVisible =
                false;

            BenchmarkCountdownLabel.Text =
                string.Empty;

            SetBenchmarkButtonRunningState(
                false);

            HardwareAccelerationSwitch.IsEnabled =
                CalculationAccelerationManager.IsSimdAvailable;

            MultithreadingSwitch.IsEnabled =
                CalculationThreadingManager.IsMultithreadingAvailable;

            UpdateSimdModeSelectorVisibility();

            _benchmarkCancellationTokenSource?.Dispose();

            _benchmarkCancellationTokenSource =
                null;
        }
    }

    private void SetBenchmarkButtonRunningState(
        bool isRunning)
    {
        RunBenchmarkButton.Text =
            LocalizationService.Translate(
                isRunning
                    ? "Dừng đo sức mạnh"
                    : "Chạy đo sức mạnh");

        if (isRunning)
        {
            RunBenchmarkButton.BackgroundColor =
                Color.FromArgb(
                    "#DC2626");
        }
        else
        {
            RunBenchmarkButton.SetDynamicResource(
                Button.BackgroundColorProperty,
                "PrimaryColor");
        }
    }

    private void RenderBenchmarkResult()
    {
        if (_lastBenchmarkResult is null)
        {
            BenchmarkResultsBorder.IsVisible =
                false;

            if (!_isBenchmarkRunning)
            {
                BenchmarkStatusLabel.Text =
                    LocalizationService.Translate(
                        "Chưa chạy đo sức mạnh.");
            }

            return;
        }

        BenchmarkResultsBorder.IsVisible =
            true;

        BenchmarkScoreValueLabel.Text =
            _lastBenchmarkResult
                .Score
                .ToString(
                    "N0",
                    CultureInfo.CurrentCulture);

        BenchmarkFloatingModeResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Float / Double: {0}"),
                BuildFloatingPointModeText(
                    _lastBenchmarkResult.UsedSimd,
                    _lastBenchmarkResult.UsedSimdMode,
                    _lastBenchmarkResult.UsedMultithreading,
                    _lastBenchmarkResult.WorkerCount));

        BenchmarkIntegerModeResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Int32 / Int64: {0}"),
                BuildIntegerModeText(
                    _lastBenchmarkResult.UsedMultithreading,
                    _lastBenchmarkResult.WorkerCount));

        Int32ResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Int32: {0:N1} MOPS • {1:N3} GOPS"),
                _lastBenchmarkResult.Int32Mops,
                _lastBenchmarkResult.Int32Mops /
                1000d);

        Int64ResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Int64: {0:N1} MOPS • {1:N3} GOPS"),
                _lastBenchmarkResult.Int64Mops,
                _lastBenchmarkResult.Int64Mops /
                1000d);

        FloatResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Float: {0:N1} MFLOPS • {1:N3} GFLOPS"),
                _lastBenchmarkResult.FloatMops,
                _lastBenchmarkResult.FloatMops /
                1000d);

        DoubleResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Double: {0:N1} MFLOPS • {1:N3} GFLOPS"),
                _lastBenchmarkResult.DoubleMops,
                _lastBenchmarkResult.DoubleMops /
                1000d);

        ElapsedResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Tổng thời gian: {0:N1} giây"),
                _lastBenchmarkResult.ElapsedMilliseconds /
                1000d);
    }

    private async Task<BenchmarkResult> RunCalculationBenchmarkAsync(
        bool useSimd,
        CalculationSimdMode simdMode,
        bool useMultithreading,
        CancellationToken cancellationToken)
    {
        bool actualUseSimd =
            useSimd &&
            CalculationAccelerationManager.IsSimdAvailable &&
            CalculationAccelerationManager.IsModeAvailable(
                simdMode);

        bool actualUseMultithreading =
            useMultithreading &&
            CalculationThreadingManager.IsMultithreadingAvailable;

        int workerCount =
            actualUseMultithreading
                ? CalculationThreadingManager.RecommendedWorkerCount
                : 1;

        var totalStopwatch =
            Stopwatch.StartNew();

        TimedBenchmarkResult int32Result =
            await RunBenchmarkStageAsync(
                "Int32",
                1,
                progress => RunInt32Benchmark(
                    workerCount,
                    progress,
                    cancellationToken),
                cancellationToken);

        await RunBenchmarkRestAsync(
            "Int64",
            cancellationToken);

        TimedBenchmarkResult int64Result =
            await RunBenchmarkStageAsync(
                "Int64",
                2,
                progress => RunInt64Benchmark(
                    workerCount,
                    progress,
                    cancellationToken),
                cancellationToken);

        await RunBenchmarkRestAsync(
            "Float",
            cancellationToken);

        TimedBenchmarkResult floatResult =
            await RunBenchmarkStageAsync(
                "Float",
                3,
                progress => RunFloatBenchmark(
                    actualUseSimd,
                    simdMode,
                    workerCount,
                    progress,
                    cancellationToken),
                cancellationToken);

        await RunBenchmarkRestAsync(
            "Double",
            cancellationToken);

        TimedBenchmarkResult doubleResult =
            await RunBenchmarkStageAsync(
                "Double",
                4,
                progress => RunDoubleBenchmark(
                    actualUseSimd,
                    simdMode,
                    workerCount,
                    progress,
                    cancellationToken),
                cancellationToken);

        totalStopwatch.Stop();

        // Trung bình nhân giúp một kiểu dữ liệu quá nhanh không che lấp
        // hoàn toàn một kiểu dữ liệu chậm hơn.
        double score =
            Math.Exp(
                (
                    Math.Log(
                        Math.Max(
                            int32Result.BestMops,
                            1e-9d)) +
                    Math.Log(
                        Math.Max(
                            int64Result.BestMops,
                            1e-9d)) +
                    Math.Log(
                        Math.Max(
                            floatResult.BestMops,
                            1e-9d)) +
                    Math.Log(
                        Math.Max(
                            doubleResult.BestMops,
                            1e-9d))
                ) /
                4d);

        return new BenchmarkResult
        {
            Int32Mops =
                int32Result.BestMops,

            Int64Mops =
                int64Result.BestMops,

            FloatMops =
                floatResult.BestMops,

            DoubleMops =
                doubleResult.BestMops,

            ElapsedMilliseconds =
                totalStopwatch.Elapsed.TotalMilliseconds,

            Score =
                score,

            UsedSimd =
                actualUseSimd,

            UsedSimdMode =
                simdMode,

            UsedMultithreading =
                actualUseMultithreading,

            WorkerCount =
                workerCount,

            Checksum =
                int32Result.Checksum +
                int64Result.Checksum +
                floatResult.Checksum +
                doubleResult.Checksum
        };
    }

    private async Task<TimedBenchmarkResult> RunBenchmarkStageAsync(
        string dataTypeName,
        int stageNumber,
        Func<IProgress<int>, TimedBenchmarkResult> benchmark,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var progress =
            new Progress<int>(
                remainingSeconds =>
                {
                    BenchmarkCountdownLabel.Text =
                        string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizationService.Translate(
                                "{0} giây"),
                            remainingSeconds);

                    BenchmarkStatusLabel.Text =
                        string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizationService.Translate(
                                "Đang đo {0} ({1}/4)"),
                            dataTypeName,
                            stageNumber);
                });

        return await Task.Run(
            () => benchmark(
                progress),
            cancellationToken);
    }

    private async Task RunBenchmarkRestAsync(
        string nextDataTypeName,
        CancellationToken cancellationToken)
    {
        const int restSeconds =
            3;

        for (int remainingSeconds =
                 restSeconds;
             remainingSeconds >=
             1;
             remainingSeconds--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BenchmarkCountdownLabel.Text =
                string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationService.Translate(
                        "{0} giây"),
                    remainingSeconds);

            BenchmarkStatusLabel.Text =
                string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationService.Translate(
                        "Đang nghỉ trước bài đo {0}"),
                    nextDataTypeName);

            await Task.Delay(
                1000,
                cancellationToken);
        }
    }

    private static TimedBenchmarkResult RunInt32Benchmark(
        int workerCount,
        IProgress<int> countdownProgress,
        CancellationToken cancellationToken)
    {
        WarmUpWorker(
            RunInt32Worker,
            cancellationToken);

        return RunTenSecondBenchmark(
            workerCount,
            RunInt32Worker,
            countdownProgress,
            cancellationToken);
    }

    private static TimedBenchmarkResult RunInt64Benchmark(
        int workerCount,
        IProgress<int> countdownProgress,
        CancellationToken cancellationToken)
    {
        WarmUpWorker(
            RunInt64Worker,
            cancellationToken);

        return RunTenSecondBenchmark(
            workerCount,
            RunInt64Worker,
            countdownProgress,
            cancellationToken);
    }

    private static TimedBenchmarkResult RunFloatBenchmark(
        bool useSimd,
        CalculationSimdMode simdMode,
        int workerCount,
        IProgress<int> countdownProgress,
        CancellationToken cancellationToken)
    {
        TimedWorker worker =
            !useSimd
                ? RunFloatScalarWorker
                : simdMode switch
                {
                    CalculationSimdMode.Avx512 =>
                        RunFloatAvx512Worker,

                    CalculationSimdMode.AvxAvx2 =>
                        RunFloatAvxWorker,

                    CalculationSimdMode.Sse =>
                        RunFloatSseWorker,

                    _ =>
                        RunFloatPortableSimdWorker
                };

        WarmUpWorker(
            worker,
            cancellationToken);

        return RunTenSecondBenchmark(
            workerCount,
            worker,
            countdownProgress,
            cancellationToken);
    }

    private static TimedBenchmarkResult RunDoubleBenchmark(
        bool useSimd,
        CalculationSimdMode simdMode,
        int workerCount,
        IProgress<int> countdownProgress,
        CancellationToken cancellationToken)
    {
        TimedWorker worker =
            !useSimd
                ? RunDoubleScalarWorker
                : simdMode switch
                {
                    CalculationSimdMode.Avx512 =>
                        RunDoubleAvx512Worker,

                    CalculationSimdMode.AvxAvx2 =>
                        RunDoubleAvxWorker,

                    CalculationSimdMode.Sse =>
                        RunDoubleSseWorker,

                    _ =>
                        RunDoublePortableSimdWorker
                };

        WarmUpWorker(
            worker,
            cancellationToken);

        return RunTenSecondBenchmark(
            workerCount,
            worker,
            countdownProgress,
            cancellationToken);
    }

    private static void WarmUpWorker(
        TimedWorker worker,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        long warmupDeadline =
            Stopwatch.GetTimestamp() +
            Math.Max(
                1L,
                Stopwatch.Frequency /
                25L);

        _ =
            worker(
                0,
                warmupDeadline,
                cancellationToken);
    }

    private static TimedBenchmarkResult RunTenSecondBenchmark(
        int requestedWorkerCount,
        TimedWorker worker,
        IProgress<int> countdownProgress,
        CancellationToken cancellationToken)
    {
        const int sampleCount =
            10;

        int workerCount =
            Math.Clamp(
                requestedWorkerCount,
                1,
                64);

        double bestMops =
            0d;

        double checksum =
            0d;

        var parallelOptions =
            new ParallelOptions
            {
                MaxDegreeOfParallelism =
                    workerCount,

                CancellationToken =
                    cancellationToken
            };

        for (int sampleIndex = 0;
             sampleIndex < sampleCount;
             sampleIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            countdownProgress.Report(
                sampleCount -
                sampleIndex);

            WorkerResult[] workerResults =
                new WorkerResult[workerCount];

            long startTimestamp =
                Stopwatch.GetTimestamp();

            long deadlineTimestamp =
                startTimestamp +
                Stopwatch.Frequency;

            if (workerCount ==
                1)
            {
                workerResults[0] =
                    worker(
                        0,
                        deadlineTimestamp,
                        cancellationToken);
            }
            else
            {
                Parallel.For(
                    0,
                    workerCount,
                    parallelOptions,
                    workerIndex =>
                    {
                        workerResults[workerIndex] =
                            worker(
                                workerIndex,
                                deadlineTimestamp,
                                cancellationToken);
                    });
            }

            long endTimestamp =
                Stopwatch.GetTimestamp();

            long operationCount =
                0;

            double sampleChecksum =
                0d;

            foreach (WorkerResult workerResult
                     in workerResults)
            {
                operationCount +=
                    workerResult.OperationCount;

                sampleChecksum +=
                    workerResult.Checksum;
            }

            double elapsedSeconds =
                Math.Max(
                    (
                        endTimestamp -
                        startTimestamp
                    ) /
                    (double)Stopwatch.Frequency,
                    1e-9d);

            double sampleMops =
                operationCount /
                elapsedSeconds /
                1_000_000d;

            bestMops =
                Math.Max(
                    bestMops,
                    sampleMops);

            checksum +=
                sampleChecksum;
        }

        return new TimedBenchmarkResult(
            bestMops,
            checksum);
    }

    private static WorkerResult RunInt32Worker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        const int iterationsPerBatch =
            8_192;

        const int operationsPerIteration =
            10;

        int state =
            unchecked(
                0x13579BDF +
                workerIndex *
                97);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < iterationsPerBatch;
                 index++)
            {
                state =
                    unchecked(
                        state *
                        1_664_525 +
                        1_013_904_223);

                state ^=
                    state <<
                    13;

                state ^=
                    (int)(
                        (uint)state >>
                        17);

                state ^=
                    state <<
                    5;

                state =
                    unchecked(
                        state +
                        state *
                        31 +
                        index);
            }

            operationCount +=
                (long)iterationsPerBatch *
                operationsPerIteration;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            state);
    }

    private static WorkerResult RunInt64Worker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        const int iterationsPerBatch =
            8_192;

        const int operationsPerIteration =
            10;

        long state =
            unchecked(
                0x13579BDF2468ACE1L +
                workerIndex *
                193L);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < iterationsPerBatch;
                 index++)
            {
                state =
                    unchecked(
                        state *
                        6_364_136_223_846_793_005L +
                        1_442_695_040_888_963_407L);

                state ^=
                    state <<
                    13;

                state ^=
                    (long)(
                        (ulong)state >>
                        17);

                state ^=
                    state <<
                    7;

                state =
                    unchecked(
                        state +
                        state *
                        31 +
                        index);
            }

            operationCount +=
                (long)iterationsPerBatch *
                operationsPerIteration;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            state);
    }

    private static WorkerResult RunFloatScalarWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        const int iterationsPerBatch =
            8_192;

        const int operationsPerIteration =
            8;

        float value =
            0.125f +
            workerIndex *
            0.0001f;

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < iterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    1.000001f +
                    0.0001f;

                value =
                    value *
                    0.999999f -
                    0.00005f;

                value =
                    value +
                    value *
                    0.00001f;

                value =
                    value *
                    0.99999f +
                    0.00001f;
            }

            operationCount +=
                (long)iterationsPerBatch *
                operationsPerIteration;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            value);
    }

    private static WorkerResult RunDoubleScalarWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        const int iterationsPerBatch =
            8_192;

        const int operationsPerIteration =
            8;

        double value =
            0.125d +
            workerIndex *
            0.0001d;

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < iterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    1.0000001192092896d +
                    0.0001d;

                value =
                    value *
                    0.9999998807907104d -
                    0.00005d;

                value =
                    value +
                    value *
                    0.00001d;

                value =
                    value *
                    0.99999d +
                    0.00001d;
            }

            operationCount +=
                (long)iterationsPerBatch *
                operationsPerIteration;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            value);
    }

    [MethodImpl(
        MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static WorkerResult RunFloatAvx512Worker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsAvx512Available)
        {
            return RunFloatPortableSimdWorker(
                workerIndex,
                deadlineTimestamp,
                cancellationToken);
        }

        const int vectorIterationsPerBatch =
            2_048;

        const int operationsPerLane =
            8;

        int laneCount =
            Vector512<float>.Count;

        Vector512<float> value =
            Vector512.Create(
                0.125f +
                workerIndex *
                0.0001f);

        Vector512<float> multiplierA =
            Vector512.Create(
                1.000001f);

        Vector512<float> addA =
            Vector512.Create(
                0.0001f);

        Vector512<float> multiplierB =
            Vector512.Create(
                0.999999f);

        Vector512<float> subtractB =
            Vector512.Create(
                0.00005f);

        Vector512<float> smallScale =
            Vector512.Create(
                0.00001f);

        Vector512<float> multiplierC =
            Vector512.Create(
                0.99999f);

        Vector512<float> addC =
            Vector512.Create(
                0.00001f);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    multiplierA +
                    addA;

                value =
                    value *
                    multiplierB -
                    subtractB;

                value =
                    value +
                    value *
                    smallScale;

                value =
                    value *
                    multiplierC +
                    addC;
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            Vector512.Sum(
                value));
    }

    [MethodImpl(
        MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static WorkerResult RunDoubleAvx512Worker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsAvx512Available)
        {
            return RunDoublePortableSimdWorker(
                workerIndex,
                deadlineTimestamp,
                cancellationToken);
        }

        const int vectorIterationsPerBatch =
            2_048;

        const int operationsPerLane =
            8;

        int laneCount =
            Vector512<double>.Count;

        Vector512<double> value =
            Vector512.Create(
                0.125d +
                workerIndex *
                0.0001d);

        Vector512<double> multiplierA =
            Vector512.Create(
                1.0000001192092896d);

        Vector512<double> addA =
            Vector512.Create(
                0.0001d);

        Vector512<double> multiplierB =
            Vector512.Create(
                0.9999998807907104d);

        Vector512<double> subtractB =
            Vector512.Create(
                0.00005d);

        Vector512<double> smallScale =
            Vector512.Create(
                0.00001d);

        Vector512<double> multiplierC =
            Vector512.Create(
                0.99999d);

        Vector512<double> addC =
            Vector512.Create(
                0.00001d);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    multiplierA +
                    addA;

                value =
                    value *
                    multiplierB -
                    subtractB;

                value =
                    value +
                    value *
                    smallScale;

                value =
                    value *
                    multiplierC +
                    addC;
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            Vector512.Sum(
                value));
    }

    [MethodImpl(
        MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static WorkerResult RunFloatAvxWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsAvxAvx2Available)
        {
            return RunFloatPortableSimdWorker(
                workerIndex,
                deadlineTimestamp,
                cancellationToken);
        }

        const int vectorIterationsPerBatch =
            2_048;

        const int operationsPerLane =
            8;

        int laneCount =
            Vector256<float>.Count;

        Vector256<float> value =
            Vector256.Create(
                0.125f +
                workerIndex *
                0.0001f);

        Vector256<float> multiplierA =
            Vector256.Create(
                1.000001f);

        Vector256<float> addA =
            Vector256.Create(
                0.0001f);

        Vector256<float> multiplierB =
            Vector256.Create(
                0.999999f);

        Vector256<float> subtractB =
            Vector256.Create(
                0.00005f);

        Vector256<float> smallScale =
            Vector256.Create(
                0.00001f);

        Vector256<float> multiplierC =
            Vector256.Create(
                0.99999f);

        Vector256<float> addC =
            Vector256.Create(
                0.00001f);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    multiplierA +
                    addA;

                value =
                    value *
                    multiplierB -
                    subtractB;

                value =
                    value +
                    value *
                    smallScale;

                value =
                    value *
                    multiplierC +
                    addC;
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            Vector256.Sum(
                value));
    }

    [MethodImpl(
        MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static WorkerResult RunDoubleAvxWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsAvxAvx2Available)
        {
            return RunDoublePortableSimdWorker(
                workerIndex,
                deadlineTimestamp,
                cancellationToken);
        }

        const int vectorIterationsPerBatch =
            2_048;

        const int operationsPerLane =
            8;

        int laneCount =
            Vector256<double>.Count;

        Vector256<double> value =
            Vector256.Create(
                0.125d +
                workerIndex *
                0.0001d);

        Vector256<double> multiplierA =
            Vector256.Create(
                1.0000001192092896d);

        Vector256<double> addA =
            Vector256.Create(
                0.0001d);

        Vector256<double> multiplierB =
            Vector256.Create(
                0.9999998807907104d);

        Vector256<double> subtractB =
            Vector256.Create(
                0.00005d);

        Vector256<double> smallScale =
            Vector256.Create(
                0.00001d);

        Vector256<double> multiplierC =
            Vector256.Create(
                0.99999d);

        Vector256<double> addC =
            Vector256.Create(
                0.00001d);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    multiplierA +
                    addA;

                value =
                    value *
                    multiplierB -
                    subtractB;

                value =
                    value +
                    value *
                    smallScale;

                value =
                    value *
                    multiplierC +
                    addC;
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            Vector256.Sum(
                value));
    }

    [MethodImpl(
        MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static WorkerResult RunFloatSseWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsSseAvailable)
        {
            return RunFloatPortableSimdWorker(
                workerIndex,
                deadlineTimestamp,
                cancellationToken);
        }

        const int vectorIterationsPerBatch =
            2_048;

        const int operationsPerLane =
            8;

        int laneCount =
            Vector128<float>.Count;

        Vector128<float> value =
            Vector128.Create(
                0.125f +
                workerIndex *
                0.0001f);

        Vector128<float> multiplierA =
            Vector128.Create(
                1.000001f);

        Vector128<float> addA =
            Vector128.Create(
                0.0001f);

        Vector128<float> multiplierB =
            Vector128.Create(
                0.999999f);

        Vector128<float> subtractB =
            Vector128.Create(
                0.00005f);

        Vector128<float> smallScale =
            Vector128.Create(
                0.00001f);

        Vector128<float> multiplierC =
            Vector128.Create(
                0.99999f);

        Vector128<float> addC =
            Vector128.Create(
                0.00001f);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    multiplierA +
                    addA;

                value =
                    value *
                    multiplierB -
                    subtractB;

                value =
                    value +
                    value *
                    smallScale;

                value =
                    value *
                    multiplierC +
                    addC;
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            Vector128.Sum(
                value));
    }

    [MethodImpl(
        MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static WorkerResult RunDoubleSseWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsSseAvailable)
        {
            return RunDoublePortableSimdWorker(
                workerIndex,
                deadlineTimestamp,
                cancellationToken);
        }

        const int vectorIterationsPerBatch =
            2_048;

        const int operationsPerLane =
            8;

        int laneCount =
            Vector128<double>.Count;

        Vector128<double> value =
            Vector128.Create(
                0.125d +
                workerIndex *
                0.0001d);

        Vector128<double> multiplierA =
            Vector128.Create(
                1.0000001192092896d);

        Vector128<double> addA =
            Vector128.Create(
                0.0001d);

        Vector128<double> multiplierB =
            Vector128.Create(
                0.9999998807907104d);

        Vector128<double> subtractB =
            Vector128.Create(
                0.00005d);

        Vector128<double> smallScale =
            Vector128.Create(
                0.00001d);

        Vector128<double> multiplierC =
            Vector128.Create(
                0.99999d);

        Vector128<double> addC =
            Vector128.Create(
                0.00001d);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    multiplierA +
                    addA;

                value =
                    value *
                    multiplierB -
                    subtractB;

                value =
                    value +
                    value *
                    smallScale;

                value =
                    value *
                    multiplierC +
                    addC;
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            Vector128.Sum(
                value));
    }

    private static WorkerResult RunFloatPortableSimdWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsPortableSimdAvailable &&
            !CalculationAccelerationManager.IsArmNeonAvailable)
        {
            return RunFloatScalarWorker(
                workerIndex,
                deadlineTimestamp,
                cancellationToken);
        }

        const int vectorIterationsPerBatch =
            2_048;

        const int operationsPerLane =
            8;

        int laneCount =
            Vector<float>.Count;

        Vector<float> value =
            new(
                0.125f +
                workerIndex *
                0.0001f);

        Vector<float> multiplierA =
            new(
                1.000001f);

        Vector<float> addA =
            new(
                0.0001f);

        Vector<float> multiplierB =
            new(
                0.999999f);

        Vector<float> subtractB =
            new(
                0.00005f);

        Vector<float> smallScale =
            new(
                0.00001f);

        Vector<float> multiplierC =
            new(
                0.99999f);

        Vector<float> addC =
            new(
                0.00001f);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    multiplierA +
                    addA;

                value =
                    value *
                    multiplierB -
                    subtractB;

                value =
                    value +
                    value *
                    smallScale;

                value =
                    value *
                    multiplierC +
                    addC;
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        double checksum =
            0d;

        for (int lane = 0;
             lane < laneCount;
             lane++)
        {
            checksum +=
                value[lane];
        }

        return new WorkerResult(
            operationCount,
            checksum);
    }

    private static WorkerResult RunDoublePortableSimdWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsPortableSimdAvailable &&
            !CalculationAccelerationManager.IsArmNeonAvailable)
        {
            return RunDoubleScalarWorker(
                workerIndex,
                deadlineTimestamp,
                cancellationToken);
        }

        const int vectorIterationsPerBatch =
            2_048;

        const int operationsPerLane =
            8;

        int laneCount =
            Vector<double>.Count;

        Vector<double> value =
            new(
                0.125d +
                workerIndex *
                0.0001d);

        Vector<double> multiplierA =
            new(
                1.0000001192092896d);

        Vector<double> addA =
            new(
                0.0001d);

        Vector<double> multiplierB =
            new(
                0.9999998807907104d);

        Vector<double> subtractB =
            new(
                0.00005d);

        Vector<double> smallScale =
            new(
                0.00001d);

        Vector<double> multiplierC =
            new(
                0.99999d);

        Vector<double> addC =
            new(
                0.00001d);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    value *
                    multiplierA +
                    addA;

                value =
                    value *
                    multiplierB -
                    subtractB;

                value =
                    value +
                    value *
                    smallScale;

                value =
                    value *
                    multiplierC +
                    addC;
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            cancellationToken.ThrowIfCancellationRequested();
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        double checksum =
            0d;

        for (int lane = 0;
             lane < laneCount;
             lane++)
        {
            checksum +=
                value[lane];
        }

        return new WorkerResult(
            operationCount,
            checksum);
    }

    private async void OnCloseClicked(
        object? sender,
        EventArgs e)
    {
        _benchmarkCancellationTokenSource?.Cancel();

        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing =
            true;

        try
        {
            await PlayPageExitAnimationAsync();

            // Hỗ trợ bản cũ từng mở trang bằng PushModalAsync.
            if (Navigation.ModalStack.Contains(
                    this))
            {
                await Navigation.PopModalAsync(
                    animated:
                        false);

                return;
            }

            // Cách điều hướng hiện tại: global route qua Shell.GoToAsync.
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(
                    "..",
                    animate:
                        false);
            }
        }
        finally
        {
            _isClosing =
                false;
        }
    }
}
