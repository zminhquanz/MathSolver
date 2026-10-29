using System.Reflection;
using MathSolver.Services.QuestionBank;

internal static class AiAccelerationPolicyTests
{
    public static void Run()
    {
        // A first-adapter rule would select Intel and miss the discrete NVIDIA GPU.
        CheckBackend("Cuda", true, ("Intel", 0x8086u, false), ("NVIDIA", 0x10deu, false));
        CheckBackend("Cuda", true, ("AMD", 0x1002u, false), ("NVIDIA", 0x10deu, false));
        // Vendor IDs, rather than names or CPU brands, determine the backend.
        CheckBackend("Vulkan", true, ("Discrete graphics", 0x1002u, false));
        CheckBackend("Vulkan", true, ("Integrated graphics", 0x8086u, false));
        CheckBackend("Cpu", true, ("NVIDIA in name only", 0x1234u, false));
        CheckBackend("Cpu", true, ("Software NVIDIA", 0x10deu, true));
        CheckBackend("Vulkan", true, ("Software NVIDIA", 0x10deu, true), ("Intel", 0x8086u, false));
        CheckBackend("Cpu", true);
        // This increment must not turn on an Android or other non-Windows GPU path.
        CheckBackend("Cpu", false, ("NVIDIA", 0x10deu, false));

        // Native backend order can differ from DXGI, including Intel/AMD Vulkan.
        CheckNativeDevice("Vulkan1", "Vulkan", "AMD Radeon RX 7900 XTX",
            ("Vulkan0", "Intel Arc", 16UL), ("Vulkan1", "AMD Radeon RX 7900 XTX", 8UL));
        CheckNativeDevice("CUDA1", "Cuda", "  nvidia   geforce RTX 4070 Ti SUPER  ",
            ("CUDA0", "NVIDIA GeForce RTX 4090", 24UL), ("CUDA1", "NVIDIA GeForce RTX 4070 Ti SUPER", 16UL));
        // With no description match, prefer the largest device within the requested backend.
        CheckNativeDevice("CUDA1", "Cuda", "Unmatched DXGI adapter",
            ("CUDA0", "Small NVIDIA", 8UL), ("CUDA1", "Large NVIDIA", 16UL),
            ("CPU", "CPU", 1024UL), ("CUDA_Host", "NVIDIA host buffer", 512UL),
            ("Vulkan0", "AMD", 128UL), ("CUDA", "Unnumbered CUDA entry", 256UL),
            ("CUDA2Invalid", "Malformed CUDA entry", 256UL));
        CheckNativeDevice("Vulkan2", "Vulkan", "",
            ("Vulkan0", "Intel", 1UL), ("Vulkan2", "AMD", 12UL), ("CUDA0", "NVIDIA", 24UL));
        CheckNativeDevice(null, "Cuda", "CPU", ("CPU", "CPU", 100UL), ("CUDA_Host", "Host", 100UL));
        CheckNativeDevice(null, "Cpu", "NVIDIA", ("CUDA0", "NVIDIA", 16UL));

        var unavailableDevice = RequireType("WindowsAiNativeDeviceInspector").GetMethod("Describe")!
            .Invoke(null, ["CUDA0", IntPtr.Zero])!;
        Check(unavailableDevice.GetType().GetProperty("Name")!.GetValue(unavailableDevice)?.ToString() == "CUDA0"
            && unavailableDevice.GetType().GetProperty("Description")!.GetValue(unavailableDevice)?.ToString() == ""
            && (ulong)unavailableDevice.GetType().GetProperty("MemoryBytes")!.GetValue(unavailableDevice)! == 0UL,
            "An unavailable native device must retain its name without inventing metadata or initializing a backend.");

        var manager = RequireType("AiAccelerationManager");
        bool supported = (bool)manager.GetProperty("IsSupported")!.GetValue(null)!;
        Console.WriteLine($"Windows AI detection: supported={supported}, adapter={manager.GetProperty("AdapterName")!.GetValue(null)}, preferred={manager.GetProperty("PreferredBackend")!.GetValue(null)}");
        var mode = manager.GetProperty("Mode")!;
        Check(mode.GetValue(null)!.ToString() == (supported ? "Gpu" : "Cpu"),
            "Fresh acceleration setting must default to GPU only on supported Windows hardware.");
        string original = mode.GetValue(null)!.ToString()!;
        Type modeType = RequireType("AiComputeMode");
        MethodInfo setMode = manager.GetMethod("SetMode")!;
        int changes = 0;
        EventHandler changed = (_, _) => changes++;
        var changedEvent = manager.GetEvent("Changed")!;
        changedEvent.AddEventHandler(null, changed);
        try
        {
            setMode.Invoke(null, [Enum.Parse(modeType, "Cpu")]);
            Check(mode.GetValue(null)!.ToString() == "Cpu", "Explicit CPU mode must disable GPU execution.");
            if (supported)
                Check(!ReadStoredGpuPreference(), "CPU selection must be persisted independently of the detected GPU.");
            int beforeRepeated = changes;
            setMode.Invoke(null, [Enum.Parse(modeType, "Cpu")]);
            Check(changes == beforeRepeated, "Selecting the current mode must not trigger model reloads.");
            setMode.Invoke(null, [Enum.Parse(modeType, "Gpu")]);
            Check(mode.GetValue(null)!.ToString() == (supported ? "Gpu" : "Cpu"),
                "Unsupported hardware must normalize a GPU request to CPU.");
            if (supported)
            {
                Check(changes >= 2, "A supported CPU/GPU change must notify runtime and UI consumers.");
                Check(ReadStoredGpuPreference(), "GPU selection must be persisted for the next launch.");
            }
            setMode.Invoke(null, [Enum.ToObject(modeType, 99)]);
            Check(mode.GetValue(null)!.ToString() == "Cpu", "An invalid mode must never enable GPU execution.");
        }
        finally
        {
            changedEvent.RemoveEventHandler(null, changed);
            setMode.Invoke(null, [Enum.Parse(modeType, original)]);
        }
        Console.WriteLine("PASS AI acceleration policy: hybrid/vendor/platform/software selection, native adapter mapping and CPU/GPU settings.");
    }

    private static void CheckBackend(string expected, bool windows,
        params (string Name, uint Vendor, bool Software)[] adapters)
    {
        Type adapterType = RequireType("AiGpuAdapter");
        Array input = Array.CreateInstance(adapterType, adapters.Length);
        for (int i = 0; i < adapters.Length; i++)
            input.SetValue(Activator.CreateInstance(adapterType,
                adapters[i].Name, adapters[i].Vendor, 0UL, adapters[i].Software), i);
        object? selected = RequireType("AiAccelerationPolicy").GetMethod("SelectBackend")!
            .Invoke(null, [input, windows]);
        Check(selected?.ToString() == expected,
            $"Expected {expected} for Windows={windows}, adapters={string.Join(",", adapters.Select(a => a.Name))}; got {selected}.");
    }

    private static void CheckNativeDevice(string? expectedName, string backend, string preferredAdapter,
        params (string Name, string Description, ulong Memory)[] devices)
    {
        Type deviceType = RequireType("AiNativeGpuDevice");
        Array input = Array.CreateInstance(deviceType, devices.Length);
        for (int i = 0; i < devices.Length; i++)
            input.SetValue(Activator.CreateInstance(deviceType, devices[i].Name, devices[i].Description, devices[i].Memory), i);
        object? selected = RequireType("AiAccelerationPolicy").GetMethod("SelectNativeDevice")!
            .Invoke(null, [input, Enum.Parse(RequireType("AiGpuBackend"), backend), preferredAdapter]);
        string? selectedName = selected?.GetType().GetProperty("Name")!.GetValue(selected)?.ToString();
        Check(selectedName == expectedName,
            $"Expected native {expectedName ?? "none"} for {backend}/{preferredAdapter}, got {selectedName ?? "none"}.");
    }

    private static Type RequireType(string name)
    {
        Type? type = typeof(AiModelLibrary).Assembly.GetType($"MathSolver.Services.QuestionBank.{name}");
        Check(type is not null, $"AI acceleration feature is missing: {name}.");
        return type!;
    }

    private static bool ReadStoredGpuPreference()
    {
        // TestHost replaces only the platform storage boundary with an in-memory store.
        Type preferences = typeof(AiModelLibrary).Assembly.GetType("Microsoft.Maui.Storage.Preferences")!;
        object store = preferences.GetProperty("Default", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        return (bool)store.GetType().GetMethod("Get")!.Invoke(store, ["AiUseGpu", true])!;
    }

    private static void Check(bool condition, string message) => MathSolver.Testing.TestAssert.That(condition, message);
}
