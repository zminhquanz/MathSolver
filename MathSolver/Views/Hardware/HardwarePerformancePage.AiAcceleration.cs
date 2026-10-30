using MathSolver.Services;
using MathSolver.Services.Localization;
using MathSolver.Services.QuestionBank;
using System.Diagnostics;

namespace MathSolver.Views;

public partial class HardwarePerformancePage
{
#if WINDOWS
    private bool _aiAccelerationSubscribed;
    private bool _aiModeChangeInProgress;
    private bool _aiModeChangeFailed;
    private int _aiAccelerationRefreshQueued;
#endif

    private void InitializeAiAccelerationUi()
    {
        // This section owns its VI/EN text, including dynamic runtime status.
        LocalizationService.ExcludeSubtreeFromLegacyTracking(AiAccelerationSection);
        // Native button images use PNG names generated from our SVG resources.
        // Follow TextColor so selection, theme and accent changes keep their contrast.
        AiCpuModeButton.SetBinding(Button.ImageSourceProperty, new Binding(
            nameof(Button.TextColor), BindingMode.OneWay,
            converter: new AiProcessorIconConverter("ai_cpu"), source: AiCpuModeButton));
        AiGpuModeButton.SetBinding(Button.ImageSourceProperty, new Binding(
            nameof(Button.TextColor), BindingMode.OneWay,
            converter: new AiProcessorIconConverter("ai_gpu"), source: AiGpuModeButton));
#if !WINDOWS
        AiAccelerationSection.IsVisible = false;
#endif
    }

    private sealed class AiProcessorIconConverter(string resource) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter,
            System.Globalization.CultureInfo culture) =>
            ImageSource.FromFile(resource + (value is Color color && color.GetLuminosity() > 0.5f
                ? "_light.png" : ".png"));

        public object ConvertBack(object? value, Type targetType, object? parameter,
            System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    private void SubscribeAiAccelerationUi()
    {
#if WINDOWS
        if (!_aiAccelerationSubscribed)
        {
            _aiAccelerationSubscribed = true;
            AiAccelerationManager.Changed += QueueAiAccelerationRefresh;
            AiQuestionBank.Current.Changed += QueueAiAccelerationRefresh;
            AiQuestionBank.Current.Generation.Changed += QueueAiAccelerationRefresh;
            LocalizationService.CultureChanged += QueueAiAccelerationRefresh;
        }
        RefreshAiAccelerationUi();
#endif
    }

    private void UnsubscribeAiAccelerationUi()
    {
#if WINDOWS
        _aiAccelerationSubscribed = false;
        AiAccelerationManager.Changed -= QueueAiAccelerationRefresh;
        AiQuestionBank.Current.Changed -= QueueAiAccelerationRefresh;
        AiQuestionBank.Current.Generation.Changed -= QueueAiAccelerationRefresh;
        LocalizationService.CultureChanged -= QueueAiAccelerationRefresh;
#endif
    }

    private void RefreshAiAccelerationUi()
    {
#if WINDOWS
        if (Dispatcher.IsDispatchRequired)
        {
            QueueAiAccelerationRefresh(this, EventArgs.Empty);
            return;
        }

        var bank = AiQuestionBank.Current;
        var runtime = bank.Runtime;
        bool gpuSelected = AiAccelerationManager.Mode == AiComputeMode.Gpu;
        bool busy = _aiModeChangeInProgress || bank.IsManaging || bank.Generation.IsRunning;
        string preferredBackend = AiBackendName(AiAccelerationManager.PreferredBackend);

        AiAccelerationTitleLabel.Text = AiText("Bộ xử lý AI", "AI processor");
        AiAccelerationDescriptionLabel.Text = AiText(
            "Chọn CPU hoặc GPU cho mô hình AI trên Windows. GPU NVIDIA dùng CUDA; GPU AMD/Intel dùng Vulkan. Thiết lập này độc lập với SIMD và đa luồng benchmark bên dưới.",
            "Choose CPU or GPU for the AI model on Windows. NVIDIA GPUs use CUDA; AMD/Intel GPUs use Vulkan. This setting is separate from SIMD and benchmark threading below.");
        AiAdapterLabel.Text = AiAccelerationManager.IsSupported
            ? AiText("GPU được phát hiện: ", "Detected GPU: ") + AiAccelerationManager.AdapterName
            : AiText("Không phát hiện GPU Windows được hỗ trợ. AI sử dụng CPU.",
                "No supported Windows GPU detected. AI uses the CPU.");

        AiCpuModeButton.Caption = gpuSelected ? "CPU" : "CPU ✓";
        AiGpuModeButton.Caption = gpuSelected ? "GPU ✓" : "GPU";
        SetAiModeButtonTheme(AiCpuModeButton, !gpuSelected);
        SetAiModeButtonTheme(AiGpuModeButton, gpuSelected);
        AiCpuModeButton.IsEnabled = !busy;
        AiGpuModeButton.IsEnabled = !busy && AiAccelerationManager.IsSupported;

        AiPreferredBackendLabel.Text = AiText("Đã chọn: ", "Selected: ")
            + (gpuSelected ? $"GPU ({preferredBackend})" : "CPU");

        if (!runtime.IsLoaded)
        {
            AiRuntimeStatusLabel.Text = AiText(
                "Mô hình chưa được nạp. Lựa chọn sẽ áp dụng khi nạp mô hình lần tới.",
                "The model is not loaded. This preference applies on the next model load.");
        }
        else
        {
            bool gpuActive = runtime.ActiveBackend != AiGpuBackend.Cpu && runtime.GpuLayerCount > 0;
            AiRuntimeStatusLabel.Text = gpuActive
                ? AiText($"Đang dùng: GPU ({AiBackendName(runtime.ActiveBackend)}, {runtime.GpuLayerCount} lớp).",
                    $"Currently using: GPU ({AiBackendName(runtime.ActiveBackend)}, {runtime.GpuLayerCount} layers).")
                : AiText("Đang dùng: CPU.", "Currently using: CPU.");
        }

        // Never show a saved GPU preference as evidence of successful offload.
        // Native diagnostic details belong in logs, not in user-facing text.
        bool fallback = gpuSelected && !string.IsNullOrWhiteSpace(runtime.AccelerationFallbackReason);
        AiFallbackStatusLabel.IsVisible = fallback;
        AiFallbackStatusLabel.Text = fallback
            ? AiText("Không thể dùng GPU với cấu hình hiện tại; AI chuyển sang CPU. Có thể thử lại GPU sau khi kiểm tra trình điều khiển hoặc khởi động lại ứng dụng.",
                "GPU acceleration is unavailable with the current configuration; AI falls back to the CPU. You can retry GPU after checking the driver or restarting the app.")
            : "";

        AiModeBusyLabel.IsVisible = busy || _aiModeChangeFailed;
        AiModeBusyLabel.Text = _aiModeChangeInProgress
            ? AiText("Đang áp dụng lựa chọn bộ xử lý AI…", "Applying the AI processor preference…")
            : bank.Generation.IsRunning
                ? AiText("AI đang tạo câu hỏi. Có thể đổi CPU/GPU sau khi hoàn tất.",
                    "AI is generating questions. You can change CPU/GPU when it finishes.")
                : bank.IsManaging
                    ? AiText("Đang quản lý mô hình AI. Có thể đổi CPU/GPU sau khi hoàn tất.",
                        "The AI model is being managed. You can change CPU/GPU when it finishes.")
                    : _aiModeChangeFailed
                        ? AiText("Chưa thể đổi bộ xử lý AI. Vui lòng thử lại khi AI đã sẵn sàng.",
                            "The AI processor could not be changed. Please retry when AI is ready.")
                        : "";
#endif
    }

    private void OnAiCpuModeClicked(object? sender, EventArgs e)
    {
#if WINDOWS
        _ = ChangeAiComputeModeAsync(AiComputeMode.Cpu);
#endif
    }

    private void OnAiGpuModeClicked(object? sender, EventArgs e)
    {
#if WINDOWS
        _ = ChangeAiComputeModeAsync(AiComputeMode.Gpu);
#endif
    }

#if WINDOWS
    private void QueueAiAccelerationRefresh(object? sender, EventArgs e)
    {
        if (!_aiAccelerationSubscribed || Interlocked.Exchange(ref _aiAccelerationRefreshQueued, 1) != 0)
            return;

        if (!Dispatcher.Dispatch(() =>
        {
            Interlocked.Exchange(ref _aiAccelerationRefreshQueued, 0);
            if (_aiAccelerationSubscribed) RefreshAiAccelerationUi();
        })) Interlocked.Exchange(ref _aiAccelerationRefreshQueued, 0);
    }

    private async Task ChangeAiComputeModeAsync(AiComputeMode mode)
    {
        var bank = AiQuestionBank.Current;
        if (_aiModeChangeInProgress || bank.IsManaging || bank.Generation.IsRunning
            || mode == AiAccelerationManager.Mode
            || (mode == AiComputeMode.Gpu && !AiAccelerationManager.IsSupported))
        {
            RefreshAiAccelerationUi();
            return;
        }

        _aiModeChangeInProgress = true;
        _aiModeChangeFailed = false;
        RefreshAiAccelerationUi();
        try
        {
            await bank.SetAccelerationModeAsync(mode);
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[AI acceleration] Could not change compute mode: {error}");
            _aiModeChangeFailed = true;
        }
        finally
        {
            _aiModeChangeInProgress = false;
            if (_aiAccelerationSubscribed) RefreshAiAccelerationUi();
        }
    }

    private static string AiText(string vietnamese, string english) =>
        LocalizationManager.Instance.CurrentCulture.StartsWith("vi", StringComparison.OrdinalIgnoreCase)
            ? vietnamese : english;

    private static string AiBackendName(AiGpuBackend backend) => backend switch
    {
        AiGpuBackend.Cuda => "CUDA",
        AiGpuBackend.Vulkan => "Vulkan",
        _ => "CPU"
    };

    private static void SetAiModeButtonTheme(Button button, bool selected)
    {
        button.SetDynamicResource(Button.BackgroundColorProperty, selected ? "PrimaryColor" : "SurfaceAltColor");
        button.SetDynamicResource(Button.TextColorProperty, selected ? "OnPrimaryColor" : "TextPrimaryColor");
        button.SetDynamicResource(Button.BorderColorProperty, selected ? "PrimaryColor" : "PrimaryBorderColor");
        button.BorderWidth = 1.5;
    }
#endif
}
