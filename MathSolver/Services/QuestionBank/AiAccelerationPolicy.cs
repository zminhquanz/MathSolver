namespace MathSolver.Services.QuestionBank;

public enum AiComputeMode { Cpu, Gpu }

public enum AiGpuBackend { Cpu, Cuda, Vulkan }

public sealed record AiGpuAdapter(string Name, uint VendorId, ulong DedicatedMemoryBytes, bool IsSoftware = false);

public sealed record AiNativeGpuDevice(string Name, string Description, ulong MemoryBytes);

/// <summary>Hardware preference only; native backend/driver availability is checked by the runtime.</summary>
public static class AiAccelerationPolicy
{
    public static AiNativeGpuDevice? SelectNativeDevice(IEnumerable<AiNativeGpuDevice> devices,
        AiGpuBackend backend, string preferredAdapterName)
    {
        string prefix = backend switch
        {
            AiGpuBackend.Cuda => "CUDA",
            AiGpuBackend.Vulkan => "Vulkan",
            _ => string.Empty
        };
        if (prefix.Length == 0) return null;
        string preferred = NormalizeAdapterName(preferredAdapterName);
        return devices.Where(device => IsNumberedBackendDevice(device.Name, prefix))
            .OrderByDescending(device => preferred.Length > 0 && NormalizeAdapterName(device.Description) == preferred)
            .ThenByDescending(device => device.MemoryBytes)
            .FirstOrDefault();
    }

    private static bool IsNumberedBackendDevice(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length > prefix.Length &&
        name.AsSpan(prefix.Length).IndexOfAnyExceptInRange('0', '9') < 0;

    private static string NormalizeAdapterName(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static AiGpuBackend SelectBackend(IEnumerable<AiGpuAdapter> adapters, bool isWindows) =>
        SelectAdapter(adapters, isWindows)?.VendorId switch
        {
            0x10de => AiGpuBackend.Cuda,
            0x1002 or 0x8086 => AiGpuBackend.Vulkan,
            _ => AiGpuBackend.Cpu
        };

    public static AiGpuAdapter? SelectAdapter(IEnumerable<AiGpuAdapter> adapters, bool isWindows)
    {
        if (!isWindows) return null;
        // DXGI orders by connected displays, so integrated graphics can precede NVIDIA.
        return adapters.Where(adapter => !adapter.IsSoftware &&
                adapter.VendorId is 0x10de or 0x1002 or 0x8086)
            .OrderByDescending(adapter => adapter.VendorId == 0x10de)
            .ThenByDescending(adapter => adapter.DedicatedMemoryBytes)
            .FirstOrDefault();
    }
}
