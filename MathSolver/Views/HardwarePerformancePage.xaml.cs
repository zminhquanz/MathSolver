using MathSolver.Graphics;
using MathSolver.Services;
using MathSolver.Services.Localization;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

#if WINDOWS
using MathSolver.Platforms.Windows;
#endif

#if ANDROID
using MathSolver.Platforms.Android;
using System.Runtime.Intrinsics.Arm;
#pragma warning disable SYSLIB5003
#endif

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

    private enum RawBenchmarkKind
    {
        None,
        Standard,
        ThreadComparison,
        SimdComparison
    }

    private sealed class FloatingPointBenchmarkResult
    {
        public required double FloatMops { get; init; }

        public required double DoubleMops { get; init; }

        public required double Score { get; init; }

        public required CalculationSimdMode SimdMode { get; init; }

        public required bool UsedMultithreading { get; init; }

        public required int WorkerCount { get; init; }

        public required double ElapsedMilliseconds { get; init; }

        public required double Checksum { get; init; }
    }

    private sealed record ThreadComparisonResult(
        BenchmarkResult SingleThread,
        BenchmarkResult MultiThread);

    private sealed record SimdComparisonModeResult(
        CalculationSimdMode Mode,
        int VectorWidthBits,
        FloatingPointBenchmarkResult? SingleThread,
        FloatingPointBenchmarkResult? MultiThread);

    private sealed record SimdComparisonResult(
        IReadOnlyList<SimdComparisonModeResult> Modes);

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
    private ThreadComparisonResult? _lastThreadComparisonResult;
    private SimdComparisonResult? _lastSimdComparisonResult;
    private RawBenchmarkKind _activeRawBenchmarkKind;
    private bool _isBenchmarkRunning;
    private bool _isLoadingAccelerationState;
    private bool _hasPlayedEntryAnimation;
    private bool _isClosing;
    private bool _isStopConfirmationVisible;
    private bool _isPageDisappearing;
    private CancellationTokenSource? _benchmarkCancellationTokenSource;
    private TaskCompletionSource<bool>? _benchmarkCompletionSource;
    private bool _showLlmPerformance;
    private bool _isUpdatingBenchmarkTestPicker;

    private readonly List<SimdModeOption>
        _simdModeOptions =
            [];

    public HardwarePerformancePage()
    {
        InitializeComponent();

        LlmBenchmarkView.BenchmarkRunningChanged +=
            OnLlmBenchmarkRunningChanged;

#if ANDROID
        AndroidPickerVisualHelper.Attach(
            SimdModePicker);
        AndroidPickerVisualHelper.Attach(
            BenchmarkTestPicker);
#endif

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
        RefreshExtendedBenchmarkUi();
        UpdateBenchmarkModeTabs();
        LlmBenchmarkView.RefreshState();
        PreparePageEntryAnimation();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Shell.SetTabBarIsVisible(
            this,
            false);

        _isPageDisappearing =
            false;

        AppLanguageManager.LanguageChanged +=
            OnLanguageChanged;

        LocalizationService.Attach(
            this);

        LoadHardwareInformation();
        RenderBenchmarkResult();
        RefreshExtendedBenchmarkUi();
        UpdateBenchmarkModeTabs();
        LlmBenchmarkView.RefreshState();

#if WINDOWS
        MathSolver.Platforms.Windows.WindowStateManager.SetCloseGuard(
            this,
            ConfirmWindowsWindowCloseAsync);
#endif

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
        _isPageDisappearing =
            true;

        // Chỉ gửi yêu cầu dừng không phát sinh exception.
        // CloseAsync sẽ chờ benchmark kết thúc khi điều hướng trong ứng dụng.
        _benchmarkCancellationTokenSource?.Cancel();
        LlmBenchmarkView.CancelBenchmark();

#if WINDOWS
        MathSolver.Platforms.Windows.WindowStateManager.ClearCloseGuard(
            this);
#endif

        AppLanguageManager.LanguageChanged -=
            OnLanguageChanged;

        Shell.SetTabBarIsVisible(
            this,
            true);

        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ =
            CloseAsync();

        return true;
    }

#if WINDOWS
    private async Task<bool> ConfirmWindowsWindowCloseAsync()
    {
        // When no benchmark is active, X and Alt+F4 close immediately.
        if (!_isBenchmarkRunning &&
            !LlmBenchmarkView.IsBenchmarkRunning)
        {
            return true;
        }

        bool shouldStop =
            await ConfirmStopBenchmarkAsync();

        if (!shouldStop)
        {
            // Keep the app open and let the benchmark continue.
            return false;
        }

        // The application can close only after every benchmark worker has
        // observed cancellation and the benchmark task has completed.
        await StopBenchmarkAndWaitAsync();
        await LlmBenchmarkView.StopAndWaitAsync();

        return true;
    }
#endif

    private void PreparePageEntryAnimation()
    {
#if ANDROID
        // Material shared-axis style: enter from the trailing edge without
        // scaling the page surface. This keeps the motion lighter on phones.
        HardwarePageContentRoot.Opacity =
            0d;

        HardwarePageContentRoot.TranslationX =
            24d;

        HardwarePageContentRoot.Scale =
            1d;
#else
        HardwarePageContentRoot.Opacity =
            0d;

        HardwarePageContentRoot.TranslationX =
            42d;

        HardwarePageContentRoot.Scale =
            0.995d;
#endif
    }

    private async Task PlayPageEntryAnimationAsync()
    {
        HardwarePageContentRoot.CancelAnimations();

#if ANDROID
        await Task.WhenAll(
            HardwarePageContentRoot.FadeToAsync(
                1d,
                170,
                Easing.CubicOut),

            HardwarePageContentRoot.TranslateToAsync(
                0d,
                0d,
                220,
                Easing.CubicOut));
#else
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
#endif
    }

    private async Task PlayPageExitAnimationAsync()
    {
        HardwarePageContentRoot.CancelAnimations();

#if ANDROID
        await Task.WhenAll(
            HardwarePageContentRoot.FadeToAsync(
                0d,
                110,
                Easing.CubicIn),

            HardwarePageContentRoot.TranslateToAsync(
                24d,
                0d,
                150,
                Easing.CubicIn));
#else
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
#endif
    }

    private void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        LocalizationService.RefreshAll();
        LoadHardwareInformation();
        RenderBenchmarkResult();
        RefreshExtendedBenchmarkUi();
        UpdateBenchmarkModeTabs();
        LlmBenchmarkView.RefreshState();
        SetBenchmarkButtonRunningState(
            _isBenchmarkRunning);
    }

    private void OnLlmBenchmarkRunningChanged(
        bool isRunning)
    {
        UpdateBenchmarkTabLockState();
    }

    private void UpdateBenchmarkTabLockState()
    {
        bool anyBenchmarkRunning =
            _isBenchmarkRunning ||
            LlmBenchmarkView.IsBenchmarkRunning;

        RawPerformanceTabButton.IsEnabled =
            !anyBenchmarkRunning;

#if WINDOWS
        LlmPerformanceTabButton.IsEnabled =
            !anyBenchmarkRunning;
#else
        LlmPerformanceTabButton.IsEnabled = false;
#endif
    }

    private void OnRawPerformanceTabClicked(
        object? sender,
        EventArgs e)
    {
        _showLlmPerformance = false;
        UpdateBenchmarkModeTabs();
    }

    private void OnLlmPerformanceTabClicked(
        object? sender,
        EventArgs e)
    {
#if WINDOWS
        _showLlmPerformance = true;
        LlmBenchmarkView.RefreshState();
        UpdateBenchmarkModeTabs();
#endif
    }

    private void UpdateBenchmarkModeTabs()
    {
#if !WINDOWS
        _showLlmPerformance = false;
#endif

        bool vietnamese =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.Vietnamese;

        RawPerformanceTabButton.Text = vietnamese
            ? "⚙ Hiệu năng thuần"
            : "⚙ Raw performance";
        LlmPerformanceTabButton.Text = "🤖 AI / LLM";

        RawPerformanceContent.IsVisible =
            !_showLlmPerformance;
        LlmBenchmarkView.IsVisible =
            _showLlmPerformance;

        if (_showLlmPerformance)
        {
            LlmPerformanceTabButton.SetDynamicResource(
                Button.BackgroundColorProperty,
                "PrimaryColor");
            LlmPerformanceTabButton.SetDynamicResource(
                Button.TextColorProperty,
                "OnPrimaryColor");
            RawPerformanceTabButton.SetDynamicResource(
                Button.BackgroundColorProperty,
                "SurfaceAltColor");
            RawPerformanceTabButton.SetDynamicResource(
                Button.TextColorProperty,
                "TextPrimaryColor");
        }
        else
        {
            RawPerformanceTabButton.SetDynamicResource(
                Button.BackgroundColorProperty,
                "PrimaryColor");
            RawPerformanceTabButton.SetDynamicResource(
                Button.TextColorProperty,
                "OnPrimaryColor");
            LlmPerformanceTabButton.SetDynamicResource(
                Button.BackgroundColorProperty,
                "SurfaceAltColor");
            LlmPerformanceTabButton.SetDynamicResource(
                Button.TextColorProperty,
                "TextPrimaryColor");
        }

        UpdateBenchmarkTabLockState();
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

        ProcessorNameValueLabel.Text =
            NormalizeText(
                GetProcessorName());

        ProcessorClockValueLabel.Text =
            GetProcessorClockText();

        CpuArchitectureValueLabel.Text =
            GetCpuArchitectureText();

        OsArchitectureValueLabel.Text =
            Environment.Is64BitOperatingSystem
                ? "64-bit"
                : "32-bit";

        PhysicalCoresValueLabel.Text =
            GetPhysicalCoreCountText();

        LogicalProcessorsValueLabel.Text =
            Environment
                .ProcessorCount
                .ToString(
                    CultureInfo.CurrentCulture);

        SimdValueLabel.Text =
            GetSupportedSimdInstructionSets();

        bool hasSimd =
            CalculationAccelerationManager.IsSimdAvailable;

        bool hasMultipleThreads =
            CalculationThreadingManager.IsMultithreadingAvailable;

        _isLoadingAccelerationState =
            true;

        HardwareAccelerationSwitch.IsEnabled =
            hasSimd &&
            !_isBenchmarkRunning;

        HardwareAccelerationSwitch.IsToggled =
            CalculationAccelerationManager.UseSimd;

        LoadSimdModeOptions();

        MultithreadingSwitch.IsEnabled =
            hasMultipleThreads &&
            !_isBenchmarkRunning;

        MultithreadingSwitch.IsToggled =
            CalculationThreadingManager.UseMultithreading;

        _isLoadingAccelerationState =
            false;

        UpdateAccelerationStateText();
        UpdateBenchmarkControlLockState();

        VectorWidthValueLabel.Text =
            GetMaximumVectorWidthText();

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

#if ANDROID
        ArmRuntimeSupportLabel.Text =
            BuildAndroidArmRuntimeSupportText();
#endif

        UpdateSimdModeSelectorVisibility();
    }

#if ANDROID
    private static string BuildAndroidArmRuntimeSupportText()
    {
        bool useEnglish =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English;

        string neonBackend =
            CalculationAccelerationManager.IsArmNeonAdvSimdAvailable
                ? ".NET AdvSimd (Mono JIT)"
                : CalculationAccelerationManager.IsArmNeonManagedAvailable
                    ? ".NET Vector128 (ARM64 Mono JIT)"
                    : (useEnglish
                        ? "NEON hardware detected; managed JIT backend unavailable"
                        : "Đã phát hiện NEON; backend managed JIT chưa khả dụng");

        return useEnglish
            ? $"Benchmark: NEON/AdvSIMD only • {neonBackend} • SVE/SVE2 and SME/SME2: hardware information only"
            : $"Benchmark: chỉ NEON/AdvSIMD • {neonBackend} • SVE/SVE2 và SME/SME2: chỉ hiển thị thông tin phần cứng";
    }
#endif

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

        UpdateAccelerationStateText();
    }

    private void UpdateSimdModeSelectorVisibility()
    {
#if ANDROID
        // Trên Android luôn cho người dùng thấy backend ARM mà thiết bị có.
        // Picker chỉ thao tác được khi công tắc tăng tốc đã bật.
        SimdModeSelectorContainer.IsVisible =
            _simdModeOptions.Count >
            0;

        SimdModePicker.IsEnabled =
            HardwareAccelerationSwitch.IsToggled &&
            !_isBenchmarkRunning;
#else
        SimdModeSelectorContainer.IsVisible =
            HardwareAccelerationSwitch.IsToggled &&
            _simdModeOptions.Count >
            0;

        SimdModePicker.IsEnabled =
            !_isBenchmarkRunning;
#endif
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

#if ANDROID
        bool runtimeCanExecuteSimd =
            CalculationAccelerationManager.IsArmNeonManagedAvailable;

        bool effectiveUseSimd =
            useSimd &&
            runtimeCanExecuteSimd;
#else
        const bool runtimeCanExecuteSimd = true;
        bool effectiveUseSimd = useSimd;
#endif

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
                effectiveUseSimd,
                selectedMode,
                useMultithreading,
                workerCount);

        IntegerModeValueLabel.Text =
            BuildIntegerModeText(
                useMultithreading,
                workerCount);

#if ANDROID
        bool hasArmHardwareSimd =
            (RuntimeInformation.ProcessArchitecture is
                Architecture.Arm or
                Architecture.Arm64) &&
            AndroidCpuInfo.HasArmSimd;
#else
        const bool hasArmHardwareSimd =
            false;
#endif

        AccelerationStatusLabel.Text =
#if ANDROID
            hasSimd &&
            !runtimeCanExecuteSimd
                ? LocalizationService.TranslateKey(
                    LocalizationKeys.Hardware.RuntimeSimdUnavailable)
                :
#endif
            !hasSimd &&
            hasArmHardwareSimd
                ? LocalizationService.TranslateKey(
                    LocalizationKeys.Hardware.RuntimeSimdUnavailable)
                : !hasSimd
                    ? LocalizationService.Translate(
                        "Thiết bị không hỗ trợ SIMD. Float và Double sẽ dùng Scalar.")
                    : effectiveUseSimd
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

    private void UpdateBenchmarkControlLockState()
    {
        bool isLocked =
            _isBenchmarkRunning;

        BenchmarkTestPicker.IsEnabled =
            !isLocked;
        BenchmarkTestPicker.Opacity =
            isLocked ? 0.55d : 1d;

        UpdateBenchmarkTabLockState();

        bool useEnglish =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English;

        // Disabled VisualState đã giảm opacity; gán trực tiếp ở đây để
        // bảo đảm Windows cập nhật ngay cả khi native Switch giữ màu cũ.
        HardwareAccelerationSwitch.Opacity =
            isLocked
                ? 0.42d
                : HardwareAccelerationSwitch.IsEnabled
                    ? 1d
                    : 0.55d;

        MultithreadingSwitch.Opacity =
            isLocked
                ? 0.42d
                : MultithreadingSwitch.IsEnabled
                    ? 1d
                    : 0.55d;

        string hardwareDescription =
            (useEnglish, isLocked) switch
            {
                (true, true) => "Hardware acceleration is locked while the benchmark is running.",
                (true, false) => "Turn hardware acceleration on or off.",
                (false, true) => "Tăng tốc phần cứng đang bị khóa trong lúc đo sức mạnh.",
                _ => "Bật hoặc tắt tăng tốc phần cứng."
            };

        string multithreadingDescription =
            (useEnglish, isLocked) switch
            {
                (true, true) => "Multithreading is locked while the benchmark is running.",
                (true, false) => "Turn benchmark multithreading on or off.",
                (false, true) => "Đa luồng đang bị khóa trong lúc đo sức mạnh.",
                _ => "Bật hoặc tắt đa luồng cho benchmark."
            };

        SemanticProperties.SetDescription(
            HardwareAccelerationSwitch,
            hardwareDescription);

        SemanticProperties.SetDescription(
            MultithreadingSwitch,
            multithreadingDescription);
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

    private static string GetDeviceTypeText(
        DeviceType deviceType)
    {
        return LocalizationService.Translate(
            deviceType == DeviceType.Physical
                ? "Thiết bị thật"
                : "Thiết bị ảo");
    }

    private static string GetCpuArchitectureText()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 or
            Architecture.X64 =>
                "x86",

            Architecture.Arm or
            Architecture.Arm64 =>
                "ARM",

            Architecture.Wasm =>
                "WebAssembly",

            _ =>
                RuntimeInformation
                    .ProcessArchitecture
                    .ToString()
        };
    }

    private static string GetSupportedSimdInstructionSets()
    {
#if ANDROID
        if (RuntimeInformation.ProcessArchitecture is
            Architecture.Arm or
            Architecture.Arm64)
        {
            string androidArmSets =
                AndroidCpuInfo
                    .GetSupportedArmSimdInstructionSets();

            if (!string.IsNullOrWhiteSpace(
                    androidArmSets))
            {
                return androidArmSets;
            }
        }
#endif

        var supportedSets =
            new List<string>();

        if (System.Runtime.Intrinsics.X86.Sse2.IsSupported)
        {
            supportedSets.Add(
                "SSE2");
        }

        if (System.Runtime.Intrinsics.X86.Sse3.IsSupported)
        {
            supportedSets.Add(
                "SSE3");
        }

        if (System.Runtime.Intrinsics.X86.Ssse3.IsSupported)
        {
            supportedSets.Add(
                "SSSE3");
        }

        if (System.Runtime.Intrinsics.X86.Sse41.IsSupported)
        {
            supportedSets.Add(
                "SSE4.1");
        }

        if (System.Runtime.Intrinsics.X86.Sse42.IsSupported)
        {
            supportedSets.Add(
                "SSE4.2");
        }

        if (System.Runtime.Intrinsics.X86.Avx.IsSupported)
        {
            supportedSets.Add(
                "AVX");
        }

        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            supportedSets.Add(
                "AVX2");
        }

        if (System.Runtime.Intrinsics.X86.Fma.IsSupported)
        {
            supportedSets.Add(
                "FMA3");
        }

        if (CalculationAccelerationManager
                .IsAvx512Available ||
            Vector512.IsHardwareAccelerated)
        {
            supportedSets.Add(
                "AVX-512");
        }

        if (System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported)
        {
            supportedSets.Add(
                "ARM NEON");
        }

        return supportedSets.Count >
               0
            ? string.Join(
                ", ",
                supportedSets)
            : LocalizationService.Translate(
                "Không được hỗ trợ");
    }

    private static string GetMaximumVectorWidthText()
    {
        int maximumWidthBits =
            GetMaximumVectorWidthBits();

        return maximumWidthBits >
               0
            ? string.Format(
                CultureInfo.CurrentCulture,
                "{0} bit",
                maximumWidthBits)
            : LocalizationService.Translate(
                "Không được hỗ trợ");
    }

    private static int GetMaximumVectorWidthBits()
    {
#if ANDROID
        if (RuntimeInformation.ProcessArchitecture is
            Architecture.Arm or
            Architecture.Arm64)
        {
            int androidArmWidth =
                AndroidCpuInfo
                    .GetMaximumVectorWidthBits();

            if (androidArmWidth > 0)
            {
                return androidArmWidth;
            }
        }
#endif

        if (CalculationAccelerationManager
                .IsAvx512Available ||
            Vector512.IsHardwareAccelerated)
        {
            return 512;
        }

        if (System.Runtime.Intrinsics.X86.Avx.IsSupported ||
            Vector256.IsHardwareAccelerated)
        {
            return 256;
        }

        if (System.Runtime.Intrinsics.X86.Sse2.IsSupported ||
            System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported ||
            Vector128.IsHardwareAccelerated)
        {
            return 128;
        }

        if (Vector.IsHardwareAccelerated)
        {
            return Vector<byte>.Count *
                   8;
        }

        return 0;
    }

    private static string? GetProcessorName()
    {
#if ANDROID
        string? androidName =
            AndroidCpuInfo
                .GetProcessorName();

        if (!string.IsNullOrWhiteSpace(
                androidName))
        {
            return androidName;
        }
#endif

#if WINDOWS
        string? windowsName =
            GetWindowsProcessorName();

        if (!string.IsNullOrWhiteSpace(
                windowsName))
        {
            return windowsName;
        }
#endif

#if IOS || MACCATALYST || MACOS
        string? appleName =
            ReadAppleSysctlString(
                "machdep.cpu.brand_string");

        if (string.IsNullOrWhiteSpace(
                appleName))
        {
            appleName =
                ReadAppleSysctlString(
                    "hw.model");
        }

        if (!string.IsNullOrWhiteSpace(
                appleName))
        {
            return appleName;
        }
#endif

        string? socName =
            ReadFirstNonEmptyFile(
                "/sys/devices/soc0/machine",
                "/sys/devices/soc0/soc_id",
                "/sys/devices/soc0/family");

        if (!string.IsNullOrWhiteSpace(
                socName) &&
            !string.Equals(
                socName.Trim(),
                "0",
                StringComparison.Ordinal))
        {
            return socName;
        }

        return ReadCpuInfoValue(
            RuntimeInformation.ProcessArchitecture is
                Architecture.Arm or
                Architecture.Arm64
                ? [
                    "Hardware",
                    "model name",
                    "Processor",
                    "CPU model"
                ]
                : [
                    "model name",
                    "Processor",
                    "Hardware",
                    "CPU model"
                ]);
    }

    private static string GetProcessorClockText()
    {
        double? maximumMegahertz =
            GetMaximumProcessorClockMegahertz();

        if (!maximumMegahertz.HasValue ||
            maximumMegahertz.Value <=
            0d)
        {
            return LocalizationService.Translate(
                "Không xác định");
        }

        return maximumMegahertz.Value >=
               1000d
            ? string.Format(
                CultureInfo.CurrentCulture,
                "{0:N2} GHz",
                maximumMegahertz.Value /
                1000d)
            : string.Format(
                CultureInfo.CurrentCulture,
                "{0:N0} MHz",
                maximumMegahertz.Value);
    }

    private static double? GetMaximumProcessorClockMegahertz()
    {
#if WINDOWS
        double? windowsClock =
            GetWindowsProcessorClockMegahertz();

        if (windowsClock.HasValue)
        {
            return windowsClock;
        }
#endif

#if IOS || MACCATALYST || MACOS
        ulong? appleFrequency =
            ReadAppleSysctlUInt64(
                "hw.cpufrequency_max");

        if (!appleFrequency.HasValue)
        {
            appleFrequency =
                ReadAppleSysctlUInt64(
                    "hw.cpufrequency");
        }

        if (appleFrequency.HasValue &&
            appleFrequency.Value >
            0)
        {
            return appleFrequency.Value /
                   1_000_000d;
        }
#endif

        double? sysFsClock =
            ReadMaximumLinuxClockMegahertz();

        if (sysFsClock.HasValue)
        {
            return sysFsClock;
        }

        string? cpuMegahertzText =
            ReadCpuInfoValue(
                [
                    "cpu MHz",
                    "clock"
                ]);

        if (TryParseFrequencyMegahertz(
                cpuMegahertzText,
                out double cpuMegahertz))
        {
            return cpuMegahertz;
        }

        return null;
    }

    private static string GetPhysicalCoreCountText()
    {
        int physicalCoreCount =
            GetPhysicalCoreCount();

        return physicalCoreCount >
               0
            ? physicalCoreCount.ToString(
                CultureInfo.CurrentCulture)
            : LocalizationService.Translate(
                "Không xác định");
    }

    private static int GetPhysicalCoreCount()
    {
#if WINDOWS
        int windowsCoreCount =
            GetWindowsPhysicalCoreCount();

        if (windowsCoreCount >
            0)
        {
            return windowsCoreCount;
        }
#endif

#if IOS || MACCATALYST || MACOS
        int? appleCoreCount =
            ReadAppleSysctlInt32(
                "hw.physicalcpu_max") ??
            ReadAppleSysctlInt32(
                "hw.physicalcpu");

        if (appleCoreCount >
            0)
        {
            return appleCoreCount.Value;
        }
#endif

        int linuxCoreCount =
            GetLinuxPhysicalCoreCount();

        if (linuxCoreCount >
            0)
        {
            return linuxCoreCount;
        }

        return Environment
            .ProcessorCount;
    }

    private static string? ReadFirstNonEmptyFile(
        params string[] paths)
    {
        foreach (string path
                 in paths)
        {
            try
            {
                if (!File.Exists(
                        path))
                {
                    continue;
                }

                string value =
                    File.ReadAllText(
                            path)
                        .Trim();

                if (!string.IsNullOrWhiteSpace(
                        value))
                {
                    return value;
                }
            }
            catch
            {
                // Một số hệ điều hành giới hạn quyền đọc sysfs.
            }
        }

        return null;
    }

    private static string? ReadCpuInfoValue(
        IReadOnlyList<string> keys)
    {
        const string cpuInfoPath =
            "/proc/cpuinfo";

        try
        {
            if (!File.Exists(
                    cpuInfoPath))
            {
                return null;
            }

            string[] lines =
                File.ReadAllLines(
                    cpuInfoPath);

            foreach (string key
                     in keys)
            {
                foreach (string line
                         in lines)
                {
                    int separatorIndex =
                        line.IndexOf(
                            ':');

                    if (separatorIndex <=
                        0)
                    {
                        continue;
                    }

                    string currentKey =
                        line[..separatorIndex]
                            .Trim();

                    if (!currentKey.Equals(
                            key,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string value =
                        line[(separatorIndex +
                              1)..]
                            .Trim();

                    if (!string.IsNullOrWhiteSpace(
                            value))
                    {
                        return value;
                    }
                }
            }
        }
        catch
        {
            // /proc có thể không tồn tại hoặc bị sandbox chặn.
        }

        return null;
    }

    private static double? ReadMaximumLinuxClockMegahertz()
    {
        double maximumMegahertz =
            0d;

        foreach (string candidateFile
                 in EnumerateLinuxClockFiles())
        {
            try
            {
                string rawText =
                    File.ReadAllText(
                            candidateFile)
                        .Trim();

                if (!double.TryParse(
                        rawText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double rawFrequency) ||
                    rawFrequency <=
                    0d)
                {
                    continue;
                }

                // sysfs thường dùng kHz. Một số hệ thống có thể trả MHz.
                double megahertz =
                    rawFrequency >
                    100_000d
                        ? rawFrequency /
                          1000d
                        : rawFrequency;

                maximumMegahertz =
                    Math.Max(
                        maximumMegahertz,
                        megahertz);
            }
            catch
            {
                // Android/Linux có thể giới hạn quyền đọc một số policy.
            }
        }

        return maximumMegahertz >
               0d
            ? maximumMegahertz
            : null;
    }

    private static IEnumerable<string> EnumerateLinuxClockFiles()
    {
        string policyRoot =
            "/sys/devices/system/cpu/cpufreq";

        if (Directory.Exists(
                policyRoot))
        {
            IEnumerable<string> policyDirectories;

            try
            {
                policyDirectories =
                    Directory.EnumerateDirectories(
                            policyRoot,
                            "policy*",
                            SearchOption.TopDirectoryOnly)
                        .ToArray();
            }
            catch
            {
                policyDirectories =
                    [];
            }

            foreach (string policyDirectory
                     in policyDirectories)
            {
                foreach (string fileName
                         in new[]
                         {
                             "cpuinfo_max_freq",
                             "scaling_max_freq"
                         })
                {
                    string candidate =
                        Path.Combine(
                            policyDirectory,
                            fileName);

                    if (File.Exists(
                            candidate))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        string cpuRoot =
            "/sys/devices/system/cpu";

        if (!Directory.Exists(
                cpuRoot))
        {
            yield break;
        }

        IEnumerable<string> cpuDirectories;

        try
        {
            cpuDirectories =
                Directory.EnumerateDirectories(
                        cpuRoot,
                        "cpu*",
                        SearchOption.TopDirectoryOnly)
                    .ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (string cpuDirectory
                 in cpuDirectories)
        {
            string cpuName =
                Path.GetFileName(
                    cpuDirectory);

            if (cpuName.Length <=
                3 ||
                !int.TryParse(
                    cpuName[3..],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out _))
            {
                continue;
            }

            string frequencyDirectory =
                Path.Combine(
                    cpuDirectory,
                    "cpufreq");

            foreach (string fileName
                     in new[]
                     {
                         "cpuinfo_max_freq",
                         "scaling_max_freq"
                     })
            {
                string candidate =
                    Path.Combine(
                        frequencyDirectory,
                        fileName);

                if (File.Exists(
                        candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static bool TryParseFrequencyMegahertz(
        string? text,
        out double megahertz)
    {
        megahertz =
            0d;

        if (string.IsNullOrWhiteSpace(
                text))
        {
            return false;
        }

        string normalized =
            text.Trim()
                .Replace(
                    "MHz",
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                .Replace(
                    "GHz",
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                .Trim();

        if (!double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double numericValue))
        {
            return false;
        }

        bool isGigahertz =
            text.Contains(
                "GHz",
                StringComparison.OrdinalIgnoreCase);

        megahertz =
            isGigahertz
                ? numericValue *
                  1000d
                : numericValue;

        return megahertz >
               0d;
    }

    private static int GetLinuxPhysicalCoreCount()
    {
        const string cpuRoot =
            "/sys/devices/system/cpu";

        try
        {
            if (!Directory.Exists(
                    cpuRoot))
            {
                return 0;
            }

            var physicalCoreKeys =
                new HashSet<string>(
                    StringComparer.Ordinal);

            int logicalCpuDirectoryCount =
                0;

            foreach (string cpuDirectory
                     in Directory.EnumerateDirectories(
                         cpuRoot,
                         "cpu*",
                         SearchOption.TopDirectoryOnly))
            {
                string cpuName =
                    Path.GetFileName(
                        cpuDirectory);

                if (cpuName.Length <=
                    3 ||
                    !int.TryParse(
                        cpuName[3..],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out _))
                {
                    continue;
                }

                logicalCpuDirectoryCount++;

                string topologyDirectory =
                    Path.Combine(
                        cpuDirectory,
                        "topology");

                string coreIdPath =
                    Path.Combine(
                        topologyDirectory,
                        "core_id");

                if (!File.Exists(
                        coreIdPath))
                {
                    continue;
                }

                string coreId =
                    File.ReadAllText(
                            coreIdPath)
                        .Trim();

                string packageIdPath =
                    Path.Combine(
                        topologyDirectory,
                        "physical_package_id");

                string packageId =
                    File.Exists(
                        packageIdPath)
                        ? File.ReadAllText(
                                packageIdPath)
                            .Trim()
                        : "0";

                if (!string.IsNullOrWhiteSpace(
                        coreId))
                {
                    physicalCoreKeys.Add(
                        $"{packageId}:{coreId}");
                }
            }

            if (RuntimeInformation.ProcessArchitecture is
                    Architecture.Arm or
                    Architecture.Arm64)
            {
                // ARM mobile processors practically expose one hardware thread
                // per physical core. Some kernels repeat core_id per cluster.
                return Math.Max(
                    physicalCoreKeys.Count,
                    logicalCpuDirectoryCount);
            }

            return physicalCoreKeys.Count;
        }
        catch
        {
            return 0;
        }
    }

#if WINDOWS
    private static string? GetWindowsProcessorName()
    {
        try
        {
            return Microsoft.Win32.Registry.GetValue(
                       @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                       "ProcessorNameString",
                       null)
                   as string;
        }
        catch
        {
            return null;
        }
    }

    private static double? GetWindowsProcessorClockMegahertz()
    {
        try
        {
            object? registryValue =
                Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                    "~MHz",
                    null);

            return registryValue switch
            {
                int value when value >
                               0 =>
                    value,

                long value when value >
                                0 =>
                    value,

                string value
                    when double.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double parsedValue) &&
                         parsedValue >
                         0d =>
                    parsedValue,

                _ =>
                    null
            };
        }
        catch
        {
            return null;
        }
    }

    private static int GetWindowsPhysicalCoreCount()
    {
        uint requiredLength =
            0;

        _ =
            GetLogicalProcessorInformationEx(
                RelationProcessorCore,
                IntPtr.Zero,
                ref requiredLength);

        if (requiredLength ==
            0)
        {
            return 0;
        }

        IntPtr buffer =
            Marshal.AllocHGlobal(
                checked(
                    (int)requiredLength));

        try
        {
            if (!GetLogicalProcessorInformationEx(
                    RelationProcessorCore,
                    buffer,
                    ref requiredLength))
            {
                return 0;
            }

            int coreCount =
                0;

            long offset =
                0;

            while (offset <
                   requiredLength)
            {
                IntPtr current =
                    IntPtr.Add(
                        buffer,
                        checked(
                            (int)offset));

                int relationship =
                    Marshal.ReadInt32(
                        current,
                        0);

                int structureSize =
                    Marshal.ReadInt32(
                        current,
                        4);

                if (structureSize <=
                    0)
                {
                    break;
                }

                if (relationship ==
                    RelationProcessorCore)
                {
                    coreCount++;
                }

                offset +=
                    structureSize;
            }

            return coreCount;
        }
        catch
        {
            return 0;
        }
        finally
        {
            Marshal.FreeHGlobal(
                buffer);
        }
    }

    private const int RelationProcessorCore =
        0;

    [DllImport(
        "kernel32.dll",
        SetLastError =
            true)]
    [return: MarshalAs(
        UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(
        int relationshipType,
        IntPtr buffer,
        ref uint returnedLength);
#endif

#if IOS || MACCATALYST || MACOS
    private static string? ReadAppleSysctlString(
        string name)
    {
        nuint length =
            0;

        if (SysctlByName(
                name,
                IntPtr.Zero,
                ref length,
                IntPtr.Zero,
                0) !=
            0 ||
            length ==
            0)
        {
            return null;
        }

        IntPtr buffer =
            Marshal.AllocHGlobal(
                checked(
                    (int)length));

        try
        {
            if (SysctlByName(
                    name,
                    buffer,
                    ref length,
                    IntPtr.Zero,
                    0) !=
                0)
            {
                return null;
            }

            return Marshal.PtrToStringUTF8(
                       buffer)
                   ?.TrimEnd(
                       '\0')
                   .Trim();
        }
        finally
        {
            Marshal.FreeHGlobal(
                buffer);
        }
    }

    private static int? ReadAppleSysctlInt32(
        string name)
    {
        int value =
            0;

        nuint length =
            (nuint)sizeof(int);

        IntPtr buffer =
            Marshal.AllocHGlobal(
                sizeof(int));

        try
        {
            if (SysctlByName(
                    name,
                    buffer,
                    ref length,
                    IntPtr.Zero,
                    0) !=
                0)
            {
                return null;
            }

            value =
                Marshal.ReadInt32(
                    buffer);

            return value;
        }
        finally
        {
            Marshal.FreeHGlobal(
                buffer);
        }
    }

    private static ulong? ReadAppleSysctlUInt64(
        string name)
    {
        nuint length =
            (nuint)sizeof(ulong);

        IntPtr buffer =
            Marshal.AllocHGlobal(
                sizeof(ulong));

        try
        {
            if (SysctlByName(
                    name,
                    buffer,
                    ref length,
                    IntPtr.Zero,
                    0) !=
                0)
            {
                return null;
            }

            return unchecked(
                (ulong)Marshal.ReadInt64(
                    buffer));
        }
        finally
        {
            Marshal.FreeHGlobal(
                buffer);
        }
    }

    [DllImport(
        "/usr/lib/libSystem.dylib",
        EntryPoint =
            "sysctlbyname")]
    private static extern int SysctlByName(
        string name,
        IntPtr oldValue,
        ref nuint oldLength,
        IntPtr newValue,
        nuint newLength);
#endif

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

    private bool IsVietnamese =>
        AppLanguageManager.CurrentLanguage ==
        AppLanguage.Vietnamese;

    private Label ActiveBenchmarkStatusLabel =>
        _activeRawBenchmarkKind switch
        {
            RawBenchmarkKind.ThreadComparison =>
                ThreadComparisonStatusLabel,

            RawBenchmarkKind.SimdComparison =>
                SimdComparisonStatusLabel,

            _ =>
                BenchmarkStatusLabel
        };

    private Label ActiveBenchmarkCountdownLabel =>
        _activeRawBenchmarkKind switch
        {
            RawBenchmarkKind.ThreadComparison =>
                ThreadComparisonCountdownLabel,

            RawBenchmarkKind.SimdComparison =>
                SimdComparisonCountdownLabel,

            _ =>
                BenchmarkCountdownLabel
        };

    private void RefreshExtendedBenchmarkUi()
    {
        BenchmarkTestSectionTitleLabel.Text =
            IsVietnamese
                ? "Bài kiểm tra hiệu năng"
                : "Performance test";

        StandardBenchmarkDescriptionLabel.Text =
            IsVietnamese
                ? "Mỗi kiểu được đo liên tiếp trong 10 giây và lấy kết quả cao nhất. Float/Double hiển thị MFLOPS/GFLOPS; Int32/Int64 hiển thị MOPS/GOPS."
                : "Each data type is measured continuously for 10 seconds and the best result is kept. Float/Double are shown in MFLOPS/GFLOPS; Int32/Int64 in MOPS/GOPS.";

        StandardBenchmarkNoteLabel.Text =
            IsVietnamese
                ? "Bài đo kéo dài khoảng 40 giây, chưa tính thời gian khởi động ngắn giữa các kiểu dữ liệu."
                : "The benchmark takes about 40 seconds, excluding the short warm-up between data types.";

        ThreadComparisonDescriptionLabel.Text =
            IsVietnamese
                ? "Chạy cùng bài đo Scalar hai lần: một luồng và đa luồng. Điểm là trung bình nhân của Int32, Int64, Float và Double."
                : "Run the same Scalar benchmark twice: single-threaded and multi-threaded. The score is the geometric mean of Int32, Int64, Float and Double.";

        ThreadComparisonNoteLabel.Text =
            IsVietnamese
                ? "Bài đo chạy 2 lượt, mỗi lượt giữ nguyên chuẩn 4 kiểu dữ liệu × 10 giây."
                : "The test runs 2 passes; each pass keeps the standard 4 data types × 10 seconds.";

        SimdComparisonDescriptionLabel.Text =
            IsVietnamese
                ? "Đo Float và Double với SSE 128-bit, AVX 256-bit và AVX-512 512-bit; mỗi loại chạy một lần đơn luồng và một lần đa luồng."
                : "Benchmark Float and Double with 128-bit SSE, 256-bit AVX and 512-bit AVX-512; each width runs once single-threaded and once multi-threaded.";

        SimdComparisonNoteLabel.Text =
            IsVietnamese
                ? "Tối đa 6 lượt: 3 độ rộng SIMD × đơn luồng / đa luồng. Nhóm CPU không hỗ trợ sẽ hiển thị N/A và được bỏ qua."
                : "Up to 6 passes: 3 SIMD widths × single-thread / multi-thread. Unsupported CPU groups are shown as N/A and skipped.";

        SimdComparisonAvailabilityLabel.Text =
            BuildSimdComparisonAvailabilityText();

        if (!_isBenchmarkRunning)
        {
            if (_lastThreadComparisonResult is null)
            {
                ThreadComparisonStatusLabel.Text =
                    IsVietnamese
                        ? "Chưa chạy so sánh đơn luồng / đa luồng."
                        : "Single-thread / multi-thread comparison has not been run yet.";
            }

            if (_lastSimdComparisonResult is null)
            {
                SimdComparisonStatusLabel.Text =
                    IsVietnamese
                        ? "Chưa chạy so sánh SIMD."
                        : "SIMD comparison has not been run yet.";
            }
        }

        RefreshBenchmarkTestPicker();
        RenderThreadComparisonResult();
        RenderSimdComparisonResult();
        UpdateRawBenchmarkButtonsState();
    }

    private void RefreshBenchmarkTestPicker()
    {
        int selectedIndex =
            BenchmarkTestPicker.SelectedIndex >= 0
                ? BenchmarkTestPicker.SelectedIndex
                : 0;

        _isUpdatingBenchmarkTestPicker = true;

        try
        {
            BenchmarkTestPickerLabel.Text =
                IsVietnamese
                    ? "Chọn bài kiểm tra"
                    : "Choose a test";

            BenchmarkTestPicker.Items.Clear();
            BenchmarkTestPicker.Items.Add(
                IsVietnamese
                    ? "Đo hiệu năng phép tính"
                    : "Calculation performance");
            BenchmarkTestPicker.Items.Add(
                IsVietnamese
                    ? "Đơn luồng / Đa luồng"
                    : "Single-thread / Multi-thread");

#if WINDOWS
            BenchmarkTestPicker.Items.Add(
                "SIMD 128 / 256 / 512-bit");
#endif

            int maximumIndex =
                Math.Max(
                    0,
                    BenchmarkTestPicker.Items.Count - 1);

            BenchmarkTestPicker.SelectedIndex =
                Math.Min(
                    selectedIndex,
                    maximumIndex);
        }
        finally
        {
            _isUpdatingBenchmarkTestPicker = false;
        }

        UpdateBenchmarkTestPanels();
    }

    private void OnBenchmarkTestPickerSelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (_isUpdatingBenchmarkTestPicker)
        {
            return;
        }

        UpdateBenchmarkTestPanels();
    }

    private void UpdateBenchmarkTestPanels()
    {
        int selectedIndex =
            BenchmarkTestPicker.SelectedIndex;

        StandardBenchmarkPanel.IsVisible =
            selectedIndex <= 0;

        ThreadComparisonPanel.IsVisible =
            selectedIndex == 1;

#if WINDOWS
        SimdComparisonPanel.IsVisible =
            selectedIndex == 2;
#else
        SimdComparisonPanel.IsVisible = false;
#endif
    }

    private string BuildSimdComparisonAvailabilityText()
    {
#if WINDOWS
        static string State(bool available) =>
            available ? "✓" : "N/A";

        return IsVietnamese
            ? $"Khả dụng: 128-bit SSE {State(CalculationAccelerationManager.IsModeAvailable(CalculationSimdMode.Sse))} • 256-bit AVX/AVX2 {State(CalculationAccelerationManager.IsModeAvailable(CalculationSimdMode.AvxAvx2))} • 512-bit AVX-512 {State(CalculationAccelerationManager.IsModeAvailable(CalculationSimdMode.Avx512))}"
            : $"Available: 128-bit SSE {State(CalculationAccelerationManager.IsModeAvailable(CalculationSimdMode.Sse))} • 256-bit AVX/AVX2 {State(CalculationAccelerationManager.IsModeAvailable(CalculationSimdMode.AvxAvx2))} • 512-bit AVX-512 {State(CalculationAccelerationManager.IsModeAvailable(CalculationSimdMode.Avx512))}";
#else
        return IsVietnamese
            ? "Bài so sánh SSE / AVX / AVX-512 chỉ khả dụng trên Windows x86/x64."
            : "The SSE / AVX / AVX-512 comparison is available only on Windows x86/x64.";
#endif
    }

    private void UpdateRawBenchmarkButtonsState()
    {
        ConfigureRawBenchmarkButton(
            RunBenchmarkButton,
            RawBenchmarkKind.Standard,
            IsVietnamese
                ? "Chạy đo sức mạnh"
                : "Run benchmark",
            canRunWhenIdle: true);

        ConfigureRawBenchmarkButton(
            RunThreadComparisonButton,
            RawBenchmarkKind.ThreadComparison,
            IsVietnamese
                ? "Chạy so sánh đơn / đa luồng"
                : "Run thread comparison",
            canRunWhenIdle:
                CalculationThreadingManager.IsMultithreadingAvailable);

#if WINDOWS
        bool simdComparisonAvailable =
            CalculationAccelerationManager.IsModeAvailable(
                CalculationSimdMode.Sse) ||
            CalculationAccelerationManager.IsModeAvailable(
                CalculationSimdMode.AvxAvx2) ||
            CalculationAccelerationManager.IsModeAvailable(
                CalculationSimdMode.Avx512);
#else
        bool simdComparisonAvailable = false;
#endif

        ConfigureRawBenchmarkButton(
            RunSimdComparisonButton,
            RawBenchmarkKind.SimdComparison,
            IsVietnamese
                ? "Chạy so sánh SIMD"
                : "Run SIMD comparison",
            canRunWhenIdle:
                simdComparisonAvailable);
    }

    private void ConfigureRawBenchmarkButton(
        Button button,
        RawBenchmarkKind kind,
        string idleText,
        bool canRunWhenIdle)
    {
        bool isActive =
            _isBenchmarkRunning &&
            _activeRawBenchmarkKind == kind;

        button.IsEnabled =
            isActive ||
            (!_isBenchmarkRunning && canRunWhenIdle);

        button.Text =
            isActive
                ? (IsVietnamese
                    ? "Dừng đo sức mạnh"
                    : "Stop benchmark")
                : idleText;

        // The red running color is a local value. Clear it before restoring
        // PrimaryColor so WinUI cannot keep the old red brush after finish/cancel.
        button.ClearValue(
            Button.BackgroundColorProperty);

        if (isActive)
        {
            button.BackgroundColor =
                Color.FromArgb(
                    "#DC2626");
        }
        else
        {
            button.SetDynamicResource(
                Button.BackgroundColorProperty,
                "PrimaryColor");
        }
    }

    private void RenderThreadComparisonResult()
    {
        if (_lastThreadComparisonResult is null)
        {
            ThreadComparisonChartView.IsVisible = false;
            ThreadComparisonResultLabel.IsVisible = false;
            return;
        }

        double singleScore =
            _lastThreadComparisonResult.SingleThread.Score;

        double multiScore =
            _lastThreadComparisonResult.MultiThread.Score;

        ThreadComparisonChartView.Drawable =
            new BenchmarkVerticalChartDrawable
            {
                SeriesLabels =
                    [IsVietnamese ? "Điểm" : "Score"],

                Groups =
                [
                    new BenchmarkVerticalChartGroup(
                        IsVietnamese ? "Đơn luồng" : "Single",
                        [singleScore]),

                    new BenchmarkVerticalChartGroup(
                        IsVietnamese ? "Đa luồng" : "Multi",
                        [multiScore])
                ]
            };

        ThreadComparisonChartView.IsVisible = true;
        ThreadComparisonChartView.Invalidate();

        double scaling =
            singleScore <= 0d
                ? 0d
                : multiScore / singleScore;

        ThreadComparisonResultLabel.Text =
            IsVietnamese
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    "Đơn luồng: {0:N0} • Đa luồng: {1:N0} • Tăng tốc: {2:N2}×",
                    singleScore,
                    multiScore,
                    scaling)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    "Single: {0:N0} • Multi: {1:N0} • Scaling: {2:N2}×",
                    singleScore,
                    multiScore,
                    scaling);

        ThreadComparisonResultLabel.IsVisible = true;
    }

    private void RenderSimdComparisonResult()
    {
        if (_lastSimdComparisonResult is null)
        {
            SimdComparisonChartView.IsVisible = false;
            SimdComparisonResultLabel.IsVisible = false;
            return;
        }

        var groups =
            new List<BenchmarkVerticalChartGroup>();

        var detailParts =
            new List<string>();

        foreach (SimdComparisonModeResult mode
                 in _lastSimdComparisonResult.Modes)
        {
            double? singleScore =
                mode.SingleThread?.Score;

            double? multiScore =
                mode.MultiThread?.Score;

            string groupLabel =
                mode.Mode switch
                {
                    CalculationSimdMode.Sse =>
                        "128-bit SSE",

                    CalculationSimdMode.AvxAvx2 =>
                        "256-bit AVX",

                    CalculationSimdMode.Avx512 =>
                        "512-bit AVX-512",

                    _ =>
                        $"{mode.VectorWidthBits}-bit"
                };

            groups.Add(
                new BenchmarkVerticalChartGroup(
                    groupLabel,
                    [singleScore, multiScore]));

            string singleText =
                singleScore.HasValue
                    ? singleScore.Value.ToString(
                        "N0",
                        CultureInfo.CurrentCulture)
                    : "N/A";

            string multiText =
                multiScore.HasValue
                    ? multiScore.Value.ToString(
                        "N0",
                        CultureInfo.CurrentCulture)
                    : "N/A";

            detailParts.Add(
                $"{mode.VectorWidthBits}-bit: {singleText} / {multiText}");
        }

        SimdComparisonChartView.Drawable =
            new BenchmarkVerticalChartDrawable
            {
                SeriesLabels =
                [
                    IsVietnamese ? "Đơn luồng" : "Single",
                    IsVietnamese ? "Đa luồng" : "Multi"
                ],

                Groups = groups
            };

        SimdComparisonChartView.IsVisible = true;
        SimdComparisonChartView.Invalidate();

        SimdComparisonResultLabel.Text =
            (IsVietnamese
                ? "Điểm FP (Float/Double) — Đơn / Đa: "
                : "FP score (Float/Double) — Single / Multi: ") +
            string.Join(
                " • ",
                detailParts);

        SimdComparisonResultLabel.IsVisible = true;
    }

    private async void OnRunThreadComparisonClicked(
        object? sender,
        EventArgs e)
    {
        if (_isBenchmarkRunning)
        {
            if (_activeRawBenchmarkKind !=
                RawBenchmarkKind.ThreadComparison)
            {
                return;
            }

            bool shouldStop =
                await ConfirmStopBenchmarkAsync();

            if (shouldStop &&
                _isBenchmarkRunning)
            {
                RequestBenchmarkStop();
            }

            return;
        }

        if (!CalculationThreadingManager.IsMultithreadingAvailable)
        {
            ThreadComparisonStatusLabel.Text =
                IsVietnamese
                    ? "CPU chỉ có một luồng logic nên không thể chạy phép so sánh này."
                    : "The CPU exposes only one logical processor, so this comparison cannot run.";
            return;
        }

        BeginExtendedRawBenchmark(
            RawBenchmarkKind.ThreadComparison,
            ThreadComparisonProgress,
            ThreadComparisonCountdownLabel);

        _lastThreadComparisonResult = null;
        ThreadComparisonChartView.IsVisible = false;
        ThreadComparisonResultLabel.IsVisible = false;

        CancellationToken cancellationToken =
            _benchmarkCancellationTokenSource!.Token;

        try
        {
            BenchmarkResult single =
                await RunCalculationBenchmarkAsync(
                    useSimd: false,
                    simdMode: CalculationSimdMode.Portable,
                    useMultithreading: false,
                    cancellationToken: cancellationToken,
                    runLabel: IsVietnamese
                        ? "Lượt 1/2 • Đơn luồng"
                        : "Pass 1/2 • Single-thread");

            cancellationToken.ThrowIfCancellationRequested();

            BenchmarkResult multi =
                await RunCalculationBenchmarkAsync(
                    useSimd: false,
                    simdMode: CalculationSimdMode.Portable,
                    useMultithreading: true,
                    cancellationToken: cancellationToken,
                    runLabel: IsVietnamese
                        ? "Lượt 2/2 • Đa luồng"
                        : "Pass 2/2 • Multi-thread");

            cancellationToken.ThrowIfCancellationRequested();

            _lastThreadComparisonResult =
                new ThreadComparisonResult(
                    single,
                    multi);

            ThreadComparisonStatusLabel.Text =
                IsVietnamese
                    ? "So sánh đơn luồng / đa luồng hoàn tất."
                    : "Single-thread / multi-thread comparison completed.";

            RenderThreadComparisonResult();
        }
        catch (OperationCanceledException)
        {
            _lastThreadComparisonResult = null;
            ThreadComparisonChartView.IsVisible = false;
            ThreadComparisonResultLabel.IsVisible = false;

            if (!_isPageDisappearing)
            {
                ThreadComparisonStatusLabel.Text =
                    IsVietnamese
                        ? "Đã hủy so sánh đơn luồng / đa luồng."
                        : "Thread comparison was cancelled.";
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Thread comparison benchmark failed: {exception}");

            _lastThreadComparisonResult = null;
            ThreadComparisonChartView.IsVisible = false;
            ThreadComparisonResultLabel.IsVisible = false;

            if (!_isPageDisappearing)
            {
                ThreadComparisonStatusLabel.Text =
                    IsVietnamese
                        ? "Không thể chạy so sánh đơn luồng / đa luồng trên thiết bị này."
                        : "The thread comparison could not run on this device.";
            }
        }
        finally
        {
            EndExtendedRawBenchmark(
                ThreadComparisonProgress,
                ThreadComparisonCountdownLabel);
        }
    }

    private async void OnRunSimdComparisonClicked(
        object? sender,
        EventArgs e)
    {
#if WINDOWS
        if (_isBenchmarkRunning)
        {
            if (_activeRawBenchmarkKind !=
                RawBenchmarkKind.SimdComparison)
            {
                return;
            }

            bool shouldStop =
                await ConfirmStopBenchmarkAsync();

            if (shouldStop &&
                _isBenchmarkRunning)
            {
                RequestBenchmarkStop();
            }

            return;
        }

        CalculationSimdMode[] modes =
        [
            CalculationSimdMode.Sse,
            CalculationSimdMode.AvxAvx2,
            CalculationSimdMode.Avx512
        ];

        if (!modes.Any(
                CalculationAccelerationManager.IsModeAvailable))
        {
            SimdComparisonStatusLabel.Text =
                IsVietnamese
                    ? "CPU không hỗ trợ các nhóm SIMD x86 dùng cho bài đo này."
                    : "The CPU does not support the x86 SIMD groups used by this benchmark.";
            return;
        }

        BeginExtendedRawBenchmark(
            RawBenchmarkKind.SimdComparison,
            SimdComparisonProgress,
            SimdComparisonCountdownLabel);

        _lastSimdComparisonResult = null;
        SimdComparisonChartView.IsVisible = false;
        SimdComparisonResultLabel.IsVisible = false;

        CancellationToken cancellationToken =
            _benchmarkCancellationTokenSource!.Token;

        try
        {
            var results =
                new List<SimdComparisonModeResult>();

            int totalPasses =
                modes.Count(
                    CalculationAccelerationManager.IsModeAvailable) *
                2;

            int passIndex = 0;

            foreach (CalculationSimdMode mode in modes)
            {
                int vectorWidth =
                    GetSimdModeVectorWidthBits(mode);

                if (!CalculationAccelerationManager.IsModeAvailable(mode))
                {
                    results.Add(
                        new SimdComparisonModeResult(
                            mode,
                            vectorWidth,
                            null,
                            null));
                    continue;
                }

                string modeName =
                    CalculationAccelerationManager
                        .GetModeDisplayName(mode);

                FloatingPointBenchmarkResult single =
                    await RunFloatingPointBenchmarkAsync(
                        simdMode: mode,
                        useMultithreading: false,
                        cancellationToken: cancellationToken,
                        runLabel:
                        (IsVietnamese
                            ? $"Lượt {++passIndex}/{totalPasses} • "
                            : $"Pass {++passIndex}/{totalPasses} • ") +
                        $"{vectorWidth}-bit {modeName} • " +
                        (IsVietnamese
                            ? "Đơn luồng"
                            : "Single-thread"));

                cancellationToken.ThrowIfCancellationRequested();

                FloatingPointBenchmarkResult multi =
                    await RunFloatingPointBenchmarkAsync(
                        simdMode: mode,
                        useMultithreading: true,
                        cancellationToken: cancellationToken,
                        runLabel:
                        (IsVietnamese
                            ? $"Lượt {++passIndex}/{totalPasses} • "
                            : $"Pass {++passIndex}/{totalPasses} • ") +
                        $"{vectorWidth}-bit {modeName} • " +
                        (IsVietnamese
                            ? "Đa luồng"
                            : "Multi-thread"));

                cancellationToken.ThrowIfCancellationRequested();

                results.Add(
                    new SimdComparisonModeResult(
                        mode,
                        vectorWidth,
                        single,
                        multi));
            }

            _lastSimdComparisonResult =
                new SimdComparisonResult(
                    results);

            SimdComparisonStatusLabel.Text =
                IsVietnamese
                    ? "So sánh SIMD hoàn tất."
                    : "SIMD comparison completed.";

            RenderSimdComparisonResult();
        }
        catch (OperationCanceledException)
        {
            _lastSimdComparisonResult = null;
            SimdComparisonChartView.IsVisible = false;
            SimdComparisonResultLabel.IsVisible = false;

            if (!_isPageDisappearing)
            {
                SimdComparisonStatusLabel.Text =
                    IsVietnamese
                        ? "Đã hủy so sánh SIMD."
                        : "SIMD comparison was cancelled.";
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"SIMD comparison benchmark failed: {exception}");

            _lastSimdComparisonResult = null;
            SimdComparisonChartView.IsVisible = false;
            SimdComparisonResultLabel.IsVisible = false;

            if (!_isPageDisappearing)
            {
                SimdComparisonStatusLabel.Text =
                    IsVietnamese
                        ? "Không thể chạy so sánh SIMD trên thiết bị này."
                        : "The SIMD comparison could not run on this device.";
            }
        }
        finally
        {
            EndExtendedRawBenchmark(
                SimdComparisonProgress,
                SimdComparisonCountdownLabel);
        }
#endif
    }

    private void BeginExtendedRawBenchmark(
        RawBenchmarkKind kind,
        ActivityIndicator progress,
        Label countdownLabel)
    {
        _activeRawBenchmarkKind = kind;
        _isBenchmarkRunning = true;

        _benchmarkCompletionSource =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        _benchmarkCancellationTokenSource?.Dispose();
        _benchmarkCancellationTokenSource =
            new CancellationTokenSource();

        HardwareAccelerationSwitch.IsEnabled = false;
        SimdModePicker.IsEnabled = false;
        MultithreadingSwitch.IsEnabled = false;

        progress.IsVisible = true;
        progress.IsRunning = true;
        countdownLabel.IsVisible = true;
        countdownLabel.Text = string.Empty;

        UpdateRawBenchmarkButtonsState();
        UpdateBenchmarkControlLockState();
    }

    private void EndExtendedRawBenchmark(
        ActivityIndicator progress,
        Label countdownLabel)
    {
        _isBenchmarkRunning = false;
        _activeRawBenchmarkKind =
            RawBenchmarkKind.None;

        if (!_isPageDisappearing)
        {
            progress.IsRunning = false;
            progress.IsVisible = false;
            countdownLabel.IsVisible = false;
            countdownLabel.Text = string.Empty;

            HardwareAccelerationSwitch.IsEnabled =
                CalculationAccelerationManager.IsSimdAvailable;

            MultithreadingSwitch.IsEnabled =
                CalculationThreadingManager.IsMultithreadingAvailable;

            UpdateSimdModeSelectorVisibility();
            UpdateBenchmarkControlLockState();
            UpdateRawBenchmarkButtonsState();
        }

        _benchmarkCancellationTokenSource?.Dispose();
        _benchmarkCancellationTokenSource = null;

        _benchmarkCompletionSource?.TrySetResult(true);
        _benchmarkCompletionSource = null;
    }

    private static int GetSimdModeVectorWidthBits(
        CalculationSimdMode mode) =>
        mode switch
        {
            CalculationSimdMode.Sse => 128,
            CalculationSimdMode.AvxAvx2 => 256,
            CalculationSimdMode.Avx512 => 512,
            _ => 0
        };

    private async Task<FloatingPointBenchmarkResult>
        RunFloatingPointBenchmarkAsync(
            CalculationSimdMode simdMode,
            bool useMultithreading,
            CancellationToken cancellationToken,
            string runLabel)
    {
        if (!CalculationAccelerationManager.IsModeAvailable(
                simdMode))
        {
            throw new NotSupportedException(
                $"SIMD mode {simdMode} is not available.");
        }

        bool actualUseMultithreading =
            useMultithreading &&
            CalculationThreadingManager.IsMultithreadingAvailable;

        int workerCount =
            actualUseMultithreading
                ? CalculationThreadingManager.RecommendedWorkerCount
                : 1;

        var totalStopwatch =
            Stopwatch.StartNew();

        TimedBenchmarkResult floatResult =
            await RunBenchmarkStageAsync(
                "Float",
                1,
                progress => RunFloatBenchmark(
                    useSimd: true,
                    simdMode: simdMode,
                    workerCount: workerCount,
                    countdownProgress: progress,
                    cancellationToken: cancellationToken),
                cancellationToken,
                runLabel,
                stageCount: 2);

        cancellationToken.ThrowIfCancellationRequested();

        TimedBenchmarkResult doubleResult =
            await RunBenchmarkStageAsync(
                "Double",
                2,
                progress => RunDoubleBenchmark(
                    useSimd: true,
                    simdMode: simdMode,
                    workerCount: workerCount,
                    countdownProgress: progress,
                    cancellationToken: cancellationToken),
                cancellationToken,
                runLabel,
                stageCount: 2);

        totalStopwatch.Stop();

        double score =
            Math.Sqrt(
                Math.Max(floatResult.BestMops, 1e-9d) *
                Math.Max(doubleResult.BestMops, 1e-9d));

        return new FloatingPointBenchmarkResult
        {
            FloatMops = floatResult.BestMops,
            DoubleMops = doubleResult.BestMops,
            Score = score,
            SimdMode = simdMode,
            UsedMultithreading = actualUseMultithreading,
            WorkerCount = workerCount,
            ElapsedMilliseconds =
                totalStopwatch.Elapsed.TotalMilliseconds,
            Checksum =
                floatResult.Checksum +
                doubleResult.Checksum
        };
    }

    private async void OnRunBenchmarkClicked(
        object? sender,
        EventArgs e)
    {
        if (_isBenchmarkRunning)
        {
            if (_activeRawBenchmarkKind !=
                RawBenchmarkKind.Standard)
            {
                return;
            }

            bool shouldStop =
                await ConfirmStopBenchmarkAsync();

            if (!shouldStop ||
                !_isBenchmarkRunning)
            {
                return;
            }

            RequestBenchmarkStop();

            return;
        }

#if ANDROID
        if (HardwareAccelerationSwitch.IsToggled &&
            CalculationAccelerationManager.SelectedSimdMode ==
                CalculationSimdMode.ArmNeon &&
            !CalculationAccelerationManager.IsArmNeonManagedAvailable)
        {
            BenchmarkStatusLabel.Text =
                AppLanguageManager.CurrentLanguage == AppLanguage.English
                    ? "NEON is present in hardware, but this Mono build did not expose the managed AdvSIMD/Vector128 backend. Rebuild Android Debug with the Mono interpreter disabled."
                    : "CPU có NEON, nhưng bản Mono hiện tại chưa expose backend managed AdvSIMD/Vector128. Hãy rebuild Android Debug với Mono interpreter đã tắt.";

            return;
        }
#endif

        _activeRawBenchmarkKind =
            RawBenchmarkKind.Standard;

        _isBenchmarkRunning =
            true;

        _benchmarkCompletionSource =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        SetBenchmarkButtonRunningState(
            true);

        HardwareAccelerationSwitch.IsEnabled =
            false;

        SimdModePicker.IsEnabled =
            false;

        MultithreadingSwitch.IsEnabled =
            false;

        UpdateBenchmarkControlLockState();

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

            BenchmarkResult benchmarkResult =
                await RunCalculationBenchmarkAsync(
                    useSimd,
                    simdMode,
                    useMultithreading,
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                _lastBenchmarkResult =
                    null;

                if (!_isPageDisappearing)
                {
                    BenchmarkResultsBorder.IsVisible =
                        false;

                    BenchmarkStatusLabel.Text =
                        LocalizationService.Translate(
                            "Đã hủy đo sức mạnh.");
                }

                return;
            }

            _lastBenchmarkResult =
                benchmarkResult;

            if (!_isPageDisappearing)
            {
                BenchmarkStatusLabel.Text =
                    LocalizationService.Translate(
                        "Đo sức mạnh hoàn tất.");

                RenderBenchmarkResult();
            }
        }
        catch (OperationCanceledException)
        {
            // Dự phòng cho các runtime có thể vẫn phát sinh cancellation
            // từ tác vụ native/Parallel.For. Không cập nhật UI khi trang đang đóng.
            _lastBenchmarkResult =
                null;

            if (!_isPageDisappearing)
            {
                BenchmarkResultsBorder.IsVisible =
                    false;

                BenchmarkStatusLabel.Text =
                    LocalizationService.Translate(
                        "Đã hủy đo sức mạnh.");
            }
        }
        catch
        {
            _lastBenchmarkResult =
                null;

            if (!_isPageDisappearing)
            {
                BenchmarkResultsBorder.IsVisible =
                    false;

                BenchmarkStatusLabel.Text =
                    LocalizationService.Translate(
                        "Không thể chạy đo sức mạnh trên thiết bị này.");
            }
        }
        finally
        {
            _isBenchmarkRunning =
                false;

            _activeRawBenchmarkKind =
                RawBenchmarkKind.None;

            if (!_isPageDisappearing)
            {
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
                UpdateBenchmarkControlLockState();
            }

            _benchmarkCancellationTokenSource?.Dispose();

            _benchmarkCancellationTokenSource =
                null;

            _benchmarkCompletionSource?.TrySetResult(
                true);

            _benchmarkCompletionSource =
                null;
        }
    }

    private async Task<bool> ConfirmStopBenchmarkAsync()
    {
        if (_isStopConfirmationVisible)
        {
            return false;
        }

        _isStopConfirmationVisible =
            true;

        try
        {
            return await MaterialDialogService.ConfirmAsync(
                this,
                LocalizationService.Translate(
                    "Xác nhận dừng"),
                LocalizationService.Translate(
                    "Bạn có muốn dừng trình đo sức mạnh không?"),
                LocalizationService.Translate(
                    "Có"),
                LocalizationService.Translate(
                    "Không"));
        }
        finally
        {
            _isStopConfirmationVisible =
                false;
        }
    }

    private void RequestBenchmarkStop()
    {
        if (!_isBenchmarkRunning)
        {
            return;
        }

        ActiveBenchmarkStatusLabel.Text =
            IsVietnamese
                ? "Đang dừng đo sức mạnh…"
                : "Stopping benchmark…";

        _benchmarkCancellationTokenSource?.Cancel();
    }

    private async Task StopBenchmarkAndWaitAsync()
    {
        Task? benchmarkCompletionTask =
            _benchmarkCompletionSource?.Task;

        RequestBenchmarkStop();

        if (benchmarkCompletionTask is not null)
        {
            await benchmarkCompletionTask;
        }
    }

    private void SetBenchmarkButtonRunningState(
        bool isRunning)
    {
        // The active benchmark kind determines which of the three buttons
        // becomes the red Stop button. The parameter is retained so existing
        // call sites remain simple.
        _ = isRunning;
        UpdateRawBenchmarkButtonsState();
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
        CancellationToken cancellationToken,
        string? runLabel = null)
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
                "Int",
                1,
                progress => RunInt32Benchmark(
                    workerCount,
                    progress,
                    cancellationToken),
                cancellationToken,
                runLabel);


        TimedBenchmarkResult int64Result =
            await RunBenchmarkStageAsync(
                "Long",
                2,
                progress => RunInt64Benchmark(
                    workerCount,
                    progress,
                    cancellationToken),
                cancellationToken,
                runLabel);


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
                cancellationToken,
                runLabel);


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
                cancellationToken,
                runLabel);

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
        CancellationToken cancellationToken,
        string? runLabel = null,
        int stageCount = 4)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return default;
        }

        var progress =
            new Progress<int>(
                remainingSeconds =>
                {
                    if (cancellationToken.IsCancellationRequested ||
                        _isPageDisappearing)
                    {
                        return;
                    }

                    Label countdownLabel =
                        ActiveBenchmarkCountdownLabel;

                    Label statusLabel =
                        ActiveBenchmarkStatusLabel;

                    countdownLabel.Text =
                        string.Format(
                            CultureInfo.CurrentCulture,
                            IsVietnamese
                                ? "{0} giây"
                                : "{0} seconds",
                            remainingSeconds);

                    string stageText =
                        IsVietnamese
                            ? $"Đang đo {dataTypeName} ({stageNumber}/{stageCount})"
                            : $"Benchmarking {dataTypeName} ({stageNumber}/{stageCount})";

                    statusLabel.Text =
                        string.IsNullOrWhiteSpace(runLabel)
                            ? stageText
                            : $"{runLabel} • {stageText}";
                });

        return await Task.Run(
            () => benchmark(
                progress));
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

#if ANDROID
                    CalculationSimdMode.ArmNeon =>
                        RunFloatNeonWorker,

#endif

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

#if ANDROID
                    CalculationSimdMode.ArmNeon =>
                        RunDoubleNeonWorker,

#endif

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
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

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
                    workerCount
            };

        for (int sampleIndex = 0;
             sampleIndex < sampleCount;
             sampleIndex++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        while (Stopwatch.GetTimestamp() <
               deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            Vector128.Sum(
                value));
    }

#if ANDROID
    [MethodImpl(
        MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static WorkerResult RunFloatNeonWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsArmNeonManagedAvailable)
        {
            throw new PlatformNotSupportedException(
                "The current Mono runtime did not expose a managed NEON/AdvSIMD backend.");
        }

        if (!AdvSimd.IsSupported)
        {
            return RunFloatNeonVector128Worker(
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
            Vector128.Create(1.000001f);

        Vector128<float> addA =
            Vector128.Create(0.0001f);

        Vector128<float> multiplierB =
            Vector128.Create(0.999999f);

        Vector128<float> subtractB =
            Vector128.Create(0.00005f);

        Vector128<float> smallScale =
            Vector128.Create(0.00001f);

        Vector128<float> multiplierC =
            Vector128.Create(0.99999f);

        Vector128<float> addC =
            Vector128.Create(0.00001f);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    AdvSimd.Add(
                        AdvSimd.Multiply(
                            value,
                            multiplierA),
                        addA);

                value =
                    AdvSimd.Subtract(
                        AdvSimd.Multiply(
                            value,
                            multiplierB),
                        subtractB);

                value =
                    AdvSimd.Add(
                        value,
                        AdvSimd.Multiply(
                            value,
                            smallScale));

                value =
                    AdvSimd.Add(
                        AdvSimd.Multiply(
                            value,
                            multiplierC),
                        addC);
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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
    private static WorkerResult RunDoubleNeonWorker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!CalculationAccelerationManager.IsArmNeonManagedAvailable)
        {
            throw new PlatformNotSupportedException(
                "The current Mono runtime did not expose a managed NEON/AdvSIMD backend.");
        }

        if (!AdvSimd.Arm64.IsSupported)
        {
            return RunDoubleNeonVector128Worker(
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
            Vector128.Create(0.0001d);

        Vector128<double> multiplierB =
            Vector128.Create(
                0.9999998807907104d);

        Vector128<double> subtractB =
            Vector128.Create(0.00005d);

        Vector128<double> smallScale =
            Vector128.Create(0.00001d);

        Vector128<double> multiplierC =
            Vector128.Create(0.99999d);

        Vector128<double> addC =
            Vector128.Create(0.00001d);

        long operationCount =
            0;

        do
        {
            for (int index = 0;
                 index < vectorIterationsPerBatch;
                 index++)
            {
                value =
                    AdvSimd.Arm64.Add(
                        AdvSimd.Arm64.Multiply(
                            value,
                            multiplierA),
                        addA);

                value =
                    AdvSimd.Arm64.Subtract(
                        AdvSimd.Arm64.Multiply(
                            value,
                            multiplierB),
                        subtractB);

                value =
                    AdvSimd.Arm64.Add(
                        value,
                        AdvSimd.Arm64.Multiply(
                            value,
                            smallScale));

                value =
                    AdvSimd.Arm64.Add(
                        AdvSimd.Arm64.Multiply(
                            value,
                            multiplierC),
                        addC);
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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
    private static WorkerResult RunFloatNeonVector128Worker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            throw new PlatformNotSupportedException(
                "Vector128 hardware acceleration is unavailable on this Mono runtime.");
        }

        const int vectorIterationsPerBatch = 2_048;
        const int operationsPerLane = 8;
        int laneCount = Vector128<float>.Count;

        Vector128<float> value = Vector128.Create(
            0.125f + workerIndex * 0.0001f);
        Vector128<float> multiplierA = Vector128.Create(1.000001f);
        Vector128<float> addA = Vector128.Create(0.0001f);
        Vector128<float> multiplierB = Vector128.Create(0.999999f);
        Vector128<float> subtractB = Vector128.Create(0.00005f);
        Vector128<float> smallScale = Vector128.Create(0.00001f);
        Vector128<float> multiplierC = Vector128.Create(0.99999f);
        Vector128<float> addC = Vector128.Create(0.00001f);

        long operationCount = 0;

        do
        {
            for (int index = 0; index < vectorIterationsPerBatch; index++)
            {
                value =
                    Vector128.Add(
                        Vector128.Multiply(value, multiplierA),
                        addA);

                value =
                    Vector128.Subtract(
                        Vector128.Multiply(value, multiplierB),
                        subtractB);

                value =
                    Vector128.Add(
                        value,
                        Vector128.Multiply(value, smallScale));

                value =
                    Vector128.Add(
                        Vector128.Multiply(value, multiplierC),
                        addC);
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        while (Stopwatch.GetTimestamp() < deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            Vector128.Sum(value));
    }

    [MethodImpl(
        MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    private static WorkerResult RunDoubleNeonVector128Worker(
        int workerIndex,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            throw new PlatformNotSupportedException(
                "Vector128 hardware acceleration is unavailable on this Mono runtime.");
        }

        const int vectorIterationsPerBatch = 2_048;
        const int operationsPerLane = 8;
        int laneCount = Vector128<double>.Count;

        Vector128<double> value = Vector128.Create(
            0.125d + workerIndex * 0.0001d);
        Vector128<double> multiplierA = Vector128.Create(1.0000001192092896d);
        Vector128<double> addA = Vector128.Create(0.0001d);
        Vector128<double> multiplierB = Vector128.Create(0.9999998807907104d);
        Vector128<double> subtractB = Vector128.Create(0.00005d);
        Vector128<double> smallScale = Vector128.Create(0.00001d);
        Vector128<double> multiplierC = Vector128.Create(0.99999d);
        Vector128<double> addC = Vector128.Create(0.00001d);

        long operationCount = 0;

        do
        {
            for (int index = 0; index < vectorIterationsPerBatch; index++)
            {
                value =
                    Vector128.Add(
                        Vector128.Multiply(value, multiplierA),
                        addA);

                value =
                    Vector128.Subtract(
                        Vector128.Multiply(value, multiplierB),
                        subtractB);

                value =
                    Vector128.Add(
                        value,
                        Vector128.Multiply(value, smallScale));

                value =
                    Vector128.Add(
                        Vector128.Multiply(value, multiplierC),
                        addC);
            }

            operationCount +=
                (long)vectorIterationsPerBatch *
                laneCount *
                operationsPerLane;

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        while (Stopwatch.GetTimestamp() < deadlineTimestamp);

        return new WorkerResult(
            operationCount,
            Vector128.Sum(value));
    }
#endif

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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
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

        HardwareBackButton.IsEnabled =
            false;

        try
        {
            if (_isBenchmarkRunning ||
                LlmBenchmarkView.IsBenchmarkRunning)
            {
                bool shouldStop =
                    await ConfirmStopBenchmarkAsync();

                if (!shouldStop)
                {
                    return;
                }
            }

            // Sau khi người dùng xác nhận, dừng nhẹ nhàng và chờ benchmark
            // thuần hoặc AI/LLM thoát trước khi trở về màn hình chính.
            await StopBenchmarkAndWaitAsync();
            await LlmBenchmarkView.StopAndWaitAsync();

            await PlayPageExitAnimationAsync();

            // Các trang Settings chỉ dùng global route; không đi qua modal
            // để tránh lỗi PlatformView null của PopModalAsync trên WinUI.
            if (Shell.Current is AppShell appShell)
            {
                await appShell.CloseSettingsAsync(
                    this);

                return;
            }

            // Fallback only when this page is hosted without AppShell.
            // Pop the navigation stack directly; don't use Shell URI-back.
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync(
                    animated: false);
            }
        }
        finally
        {
            _isClosing =
                false;

            HardwareBackButton.IsEnabled =
                true;
        }
    }
#if ANDROID
#pragma warning restore SYSLIB5003
#endif

}
