using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using MathSolver.Services;
using Microsoft.Maui.Devices;

namespace MathSolver.Views;

public partial class HardwarePerformancePage : ContentPage
{
    private sealed class BenchmarkResult
    {
        public required double FloatingPointMops { get; init; }

        public required double BigIntegerOpsPerSecond { get; init; }

        public required double ElapsedMilliseconds { get; init; }

        public required double Score { get; init; }

        public required bool UsedSimd { get; init; }

        public required double Checksum { get; init; }
    }

    private BenchmarkResult? _lastBenchmarkResult;
    private bool _isBenchmarkRunning;
    private bool _isLoadingAccelerationState;
    private bool _hasPlayedEntryAnimation;
    private bool _isClosing;

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

        _isLoadingAccelerationState =
            true;

        HardwareAccelerationSwitch.IsEnabled =
            hasSimd;

        HardwareAccelerationSwitch.IsToggled =
            CalculationAccelerationManager.UseSimd;

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

    private void UpdateAccelerationStateText()
    {
        bool hasSimd =
            CalculationAccelerationManager.IsSimdAvailable;

        bool useSimd =
            CalculationAccelerationManager.UseSimd;

        AccelerationModeValueLabel.Text =
            useSimd
                ? "SIMD"
                : "Scalar";

        AccelerationStatusLabel.Text =
            LocalizationService.Translate(
                !hasSimd
                    ? "Thiết bị không hỗ trợ SIMD. Ứng dụng sẽ dùng Scalar."
                    : useSimd
                        ? "Đang dùng SIMD."
                        : "Đang dùng Scalar.");
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
        if (Avx2.IsSupported)
        {
            return "AVX2";
        }

        if (Avx.IsSupported)
        {
            return "AVX";
        }

        if (Sse42.IsSupported)
        {
            return "SSE4.2";
        }

        if (Sse41.IsSupported)
        {
            return "SSE4.1";
        }

        if (Sse3.IsSupported)
        {
            return "SSE3";
        }

        if (Sse2.IsSupported)
        {
            return "SSE2";
        }

        if (AdvSimd.IsSupported)
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

        BenchmarkProgress.IsVisible =
            true;

        BenchmarkProgress.IsRunning =
            true;

        BenchmarkStatusLabel.Text =
            LocalizationService.Translate(
                "Đang đo sức mạnh xử lý…");

        BenchmarkResultsBorder.IsVisible =
            false;

        try
        {
            await Task.Yield();

            bool useSimd =
                CalculationAccelerationManager.UseSimd;

            _lastBenchmarkResult =
                await Task.Run(
                    () => RunCalculationBenchmark(
                        useSimd));

            BenchmarkStatusLabel.Text =
                LocalizationService.Translate(
                    "Đo sức mạnh hoàn tất.");

            RenderBenchmarkResult();
        }
        catch
        {
            _lastBenchmarkResult =
                null;

            BenchmarkStatusLabel.Text =
                LocalizationService.Translate(
                    "Không thể chạy đo sức mạnh trên thiết bị này.");

            BenchmarkResultsBorder.IsVisible =
                false;
        }
        finally
        {
            BenchmarkProgress.IsRunning =
                false;

            BenchmarkProgress.IsVisible =
                false;

            RunBenchmarkButton.IsEnabled =
                true;

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

        BenchmarkModeResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Chế độ xử lý: {0}"),
                _lastBenchmarkResult.UsedSimd
                    ? "SIMD"
                    : "Scalar");

        FloatingPointResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Số thực: {0:N1} triệu phép tính/giây"),
                _lastBenchmarkResult.FloatingPointMops);

        BigIntegerResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Số nguyên lớn: {0:N0} phép tính/giây"),
                _lastBenchmarkResult.BigIntegerOpsPerSecond);

        ElapsedResultLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.Translate(
                    "Tổng thời gian: {0:N0} ms"),
                _lastBenchmarkResult.ElapsedMilliseconds);
    }

    private static BenchmarkResult RunCalculationBenchmark(
        bool useSimd)
    {
        const int floatingPointIterations =
            12_000_000;

        var totalStopwatch =
            Stopwatch.StartNew();

        var floatingStopwatch =
            Stopwatch.StartNew();

        double floatingChecksum =
            useSimd
                ? RunSimdFloatingPointWorkload(
                    floatingPointIterations)
                : RunScalarFloatingPointWorkload(
                    floatingPointIterations);

        floatingStopwatch.Stop();

        const int bigIntegerIterations =
            60_000;

        BigInteger bigIntegerValue =
            BigInteger.Parse(
                "123456789012345678901234567890123456789");

        BigInteger modulus =
            (BigInteger.One <<
             521) -
            BigInteger.One;

        var bigIntegerStopwatch =
            Stopwatch.StartNew();

        for (int index = 1;
             index <= bigIntegerIterations;
             index++)
        {
            bigIntegerValue =
                (bigIntegerValue *
                 1_000_003 +
                 index *
                 97 +
                 17) %
                modulus;
        }

        bigIntegerStopwatch.Stop();
        totalStopwatch.Stop();

        double floatingSeconds =
            Math.Max(
                floatingStopwatch.Elapsed.TotalSeconds,
                1e-9d);

        double bigIntegerSeconds =
            Math.Max(
                bigIntegerStopwatch.Elapsed.TotalSeconds,
                1e-9d);

        double floatingPointMops =
            floatingPointIterations /
            floatingSeconds /
            1_000_000d;

        double bigIntegerOpsPerSecond =
            bigIntegerIterations /
            bigIntegerSeconds;

        double score =
            floatingPointMops *
            20d +
            bigIntegerOpsPerSecond /
            10d;

        double checksum =
            floatingChecksum +
            (double)(
                BigInteger.Abs(
                    bigIntegerValue) %
                1_000_003);

        return new BenchmarkResult
        {
            FloatingPointMops =
                floatingPointMops,

            BigIntegerOpsPerSecond =
                bigIntegerOpsPerSecond,

            ElapsedMilliseconds =
                totalStopwatch.Elapsed.TotalMilliseconds,

            Score =
                score,

            UsedSimd =
                useSimd,

            Checksum =
                checksum
        };
    }

    private static double RunScalarFloatingPointWorkload(
        int operationCount)
    {
        double value =
            0.125d;

        for (int index = 1;
             index <= operationCount;
             index++)
        {
            value =
                value *
                1.0000001192092896d +
                (index &
                 1023) *
                0.000001d;

            if (value >
                1_000_000d)
            {
                value *=
                    0.000001d;
            }
        }

        return value;
    }

    private static double RunSimdFloatingPointWorkload(
        int operationCount)
    {
        if (!CalculationAccelerationManager.IsSimdAvailable)
        {
            return RunScalarFloatingPointWorkload(
                operationCount);
        }

        int laneCount =
            Vector<double>.Count;

        Vector<double> values =
            new(
                0.125d);

        Vector<double> multiplier =
            new(
                1.0000001192092896d);

        double[] additions =
            new double[laneCount];

        int vectorIterations =
            Math.Max(
                1,
                operationCount /
                laneCount);

        for (int index = 1;
             index <= vectorIterations;
             index++)
        {
            for (int lane = 0;
                 lane < laneCount;
                 lane++)
            {
                additions[lane] =
                    ((index *
                      laneCount +
                      lane) &
                     1023) *
                    0.000001d;
            }

            values =
                values *
                multiplier +
                new Vector<double>(
                    additions);
        }

        double checksum =
            0d;

        for (int lane = 0;
             lane < laneCount;
             lane++)
        {
            checksum +=
                values[lane];
        }

        return checksum;
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
