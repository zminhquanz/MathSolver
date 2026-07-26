using MathSolver.Services;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;

namespace MathSolver.Views;

public partial class HardwarePerformancePage : ContentPage
{
    private readonly record struct TimedBenchmarkResult(
        double BestMops,
        double Checksum);

    private readonly record struct WorkerResult(
        long OperationCount,
        double Checksum);

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
                Vector<byte>.Count *
                8);

        AvailableMemoryValueLabel.Text =
            GetAvailableMemoryText();

        RuntimeValueLabel.Text =
            RuntimeInformation.FrameworkDescription;
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

        string floatingMode =
            BuildFloatingPointModeText(
                useSimd,
                useMultithreading,
                workerCount);

        string integerMode =
            BuildIntegerModeText(
                useMultithreading,
                workerCount);

        FloatingPointModeValueLabel.Text =
            floatingMode;

        IntegerModeValueLabel.Text =
            integerMode;

        AccelerationStatusLabel.Text =
            LocalizationService.Translate(
                !hasSimd
                    ? "Thiết bị không hỗ trợ SIMD. Float và Double sẽ dùng Scalar."
                    : useSimd
                        ? "Float và Double đang dùng SIMD."
                        : "Float và Double đang dùng Scalar.");

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
        bool useMultithreading,
        int workerCount)
    {
        string processingMode =
            useSimd
                ? "SIMD"
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
        if (System.Runtime.Intrinsics.X86.Avx512BW.IsSupported || System.Runtime.Intrinsics.X86.Avx512CD.IsSupported || System.Runtime.Intrinsics.X86.Avx512DQ.IsSupported || System.Runtime.Intrinsics.X86.Avx512F.IsSupported || System.Runtime.Intrinsics.X86.Avx512Vbmi.IsSupported || System.Runtime.Intrinsics.X86.Avx512Vbmi2.IsSupported)
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
            return;
        }

        _isBenchmarkRunning =
            true;

        RunBenchmarkButton.IsEnabled =
            false;

        HardwareAccelerationSwitch.IsEnabled =
            false;

        MultithreadingSwitch.IsEnabled =
            false;

        BenchmarkProgress.IsVisible =
            true;

        BenchmarkProgress.IsRunning =
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

            bool useMultithreading =
                CalculationThreadingManager.UseMultithreading;

            _lastBenchmarkResult =
                await RunCalculationBenchmarkAsync(
                    useSimd,
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
            BenchmarkProgress.IsRunning =
                false;

            BenchmarkProgress.IsVisible =
                false;

            RunBenchmarkButton.IsEnabled =
                true;

            HardwareAccelerationSwitch.IsEnabled =
                CalculationAccelerationManager.IsSimdAvailable;

            MultithreadingSwitch.IsEnabled =
                CalculationThreadingManager.IsMultithreadingAvailable;

            _benchmarkCancellationTokenSource?.Dispose();

            _benchmarkCancellationTokenSource =
                null;

            _isBenchmarkRunning =
                false;
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
                    "Int32: {0:N1} triệu phép tính/giây"),
                _lastBenchmarkResult.Int32Mops);

        Int64ResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Int64: {0:N1} triệu phép tính/giây"),
                _lastBenchmarkResult.Int64Mops);

        FloatResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Float: {0:N1} triệu phép tính/giây"),
                _lastBenchmarkResult.FloatMops);

        DoubleResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Double: {0:N1} triệu phép tính/giây"),
                _lastBenchmarkResult.DoubleMops);

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
        bool useMultithreading,
        CancellationToken cancellationToken)
    {
        bool actualUseSimd =
            useSimd &&
            CalculationAccelerationManager.IsSimdAvailable;

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
                () => RunInt32Benchmark(
                    workerCount,
                    cancellationToken),
                cancellationToken);

        TimedBenchmarkResult int64Result =
            await RunBenchmarkStageAsync(
                "Int64",
                2,
                () => RunInt64Benchmark(
                    workerCount,
                    cancellationToken),
                cancellationToken);

        TimedBenchmarkResult floatResult =
            await RunBenchmarkStageAsync(
                "Float",
                3,
                () => RunFloatBenchmark(
                    actualUseSimd,
                    workerCount,
                    cancellationToken),
                cancellationToken);

        TimedBenchmarkResult doubleResult =
            await RunBenchmarkStageAsync(
                "Double",
                4,
                () => RunDoubleBenchmark(
                    actualUseSimd,
                    workerCount,
                    cancellationToken),
                cancellationToken);

        totalStopwatch.Stop();

        // Dùng trung bình nhân để một kiểu dữ liệu quá nhanh không che lấp
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
        Func<TimedBenchmarkResult> benchmark,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        BenchmarkStatusLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Đang đo {0} ({1}/4) • 10 giây…"),
                dataTypeName,
                stageNumber);

        await Task.Yield();

        return await Task.Run(
            benchmark,
            cancellationToken);
    }

    private static TimedBenchmarkResult RunInt32Benchmark(
        int workerCount,
        CancellationToken cancellationToken)
    {
        WarmUpWorker(
            RunInt32Worker,
            cancellationToken);

        return RunTenSecondBenchmark(
            workerCount,
            RunInt32Worker,
            cancellationToken);
    }

    private static TimedBenchmarkResult RunInt64Benchmark(
        int workerCount,
        CancellationToken cancellationToken)
    {
        WarmUpWorker(
            RunInt64Worker,
            cancellationToken);

        return RunTenSecondBenchmark(
            workerCount,
            RunInt64Worker,
            cancellationToken);
    }

    private static TimedBenchmarkResult RunFloatBenchmark(
        bool useSimd,
        int workerCount,
        CancellationToken cancellationToken)
    {
        TimedWorker worker =
            useSimd
                ? RunFloatSimdWorker
                : RunFloatScalarWorker;

        WarmUpWorker(
            worker,
            cancellationToken);

        return RunTenSecondBenchmark(
            workerCount,
            worker,
            cancellationToken);
    }

    private static TimedBenchmarkResult RunDoubleBenchmark(
        bool useSimd,
        int workerCount,
        CancellationToken cancellationToken)
    {
        TimedWorker worker =
            useSimd
                ? RunDoubleSimdWorker
                : RunDoubleScalarWorker;

        WarmUpWorker(
            worker,
            cancellationToken);

        return RunTenSecondBenchmark(
            workerCount,
            worker,
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

    private static WorkerResult RunFloatSimdWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsSimdAvailable)
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

    private static WorkerResult RunDoubleSimdWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsSimdAvailable)
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
