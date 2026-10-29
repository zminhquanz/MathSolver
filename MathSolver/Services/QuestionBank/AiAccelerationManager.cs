using Microsoft.Maui.Storage;

namespace MathSolver.Services.QuestionBank;

/// <summary>Persists the user's requested mode; the runtime reports actual offload separately.</summary>
public static class AiAccelerationManager
{
    private const string UseGpuPreferenceKey = "AiUseGpu";
    private static readonly object Sync = new();
    private static readonly Lazy<Settings> Current = new(() => new Settings());

    public static event EventHandler? Changed;

    public static bool IsSupported => PreferredBackend != AiGpuBackend.Cpu;
    public static AiGpuBackend PreferredBackend => Current.Value.Backend;
    public static string AdapterName => Current.Value.Adapter?.Name ?? string.Empty;
    public static AiComputeMode Mode { get { lock (Sync) return Current.Value.Mode; } }

    public static void SetMode(AiComputeMode mode)
    {
        lock (Sync)
        {
            AiComputeMode normalized = mode == AiComputeMode.Gpu && IsSupported ? AiComputeMode.Gpu : AiComputeMode.Cpu;
            if (Current.Value.Mode == normalized) return;
            Preferences.Default.Set(UseGpuPreferenceKey, normalized == AiComputeMode.Gpu);
            Current.Value.Mode = normalized;
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private sealed class Settings
    {
        internal AiGpuAdapter? Adapter { get; }
        internal AiGpuBackend Backend { get; }
        internal AiComputeMode Mode { get; set; }

        internal Settings()
        {
            Adapter = AiAccelerationPolicy.SelectAdapter(WindowsAiGpuDetector.GetAdapters(), OperatingSystem.IsWindows());
            Backend = AiAccelerationPolicy.SelectBackend(Adapter is null ? [] : [Adapter], OperatingSystem.IsWindows());
            bool supported = Backend != AiGpuBackend.Cpu;
            Mode = supported && Preferences.Default.Get(UseGpuPreferenceKey, true) ? AiComputeMode.Gpu : AiComputeMode.Cpu;
        }
    }
}
