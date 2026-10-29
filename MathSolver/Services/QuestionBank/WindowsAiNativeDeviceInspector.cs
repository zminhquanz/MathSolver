using System.Runtime.InteropServices;

namespace MathSolver.Services.QuestionBank;

/// <summary>Reads metadata from an already registered device without loading or owning its native library.</summary>
public static class WindowsAiNativeDeviceInspector
{
    public static AiNativeGpuDevice Describe(string name, IntPtr nativeDevice)
    {
        string description = string.Empty;
        ulong memoryBytes = 0;
        if (!OperatingSystem.IsWindows() || nativeDevice == IntPtr.Zero)
            return new(name, description, memoryBytes);
        try
        {
            // This handle belongs to the active LLamaSharp runtime. Never load a
            // second ggml-base copy or release this borrowed module handle.
            IntPtr module = GetModuleHandleW("ggml-base.dll");
            if (module == IntPtr.Zero) return new(name, description, memoryBytes);
            if (NativeLibrary.TryGetExport(module, "ggml_backend_dev_description", out IntPtr describe))
            {
                IntPtr text = Marshal.GetDelegateForFunctionPointer<DescriptionDelegate>(describe)(nativeDevice);
                description = Marshal.PtrToStringUTF8(text) ?? string.Empty;
            }
            if (NativeLibrary.TryGetExport(module, "ggml_backend_dev_memory", out IntPtr memory))
            {
                Marshal.GetDelegateForFunctionPointer<MemoryDelegate>(memory)(nativeDevice, out _, out nuint total);
                memoryBytes = total;
            }
        }
        catch (Exception)
        {
            // Metadata is optional; the registered backend name remains usable.
        }
        return new(name, description, memoryBytes);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr GetModuleHandleW(string moduleName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr DescriptionDelegate(IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MemoryDelegate(IntPtr device, out nuint free, out nuint total);
}
